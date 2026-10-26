// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record RavenBodyState(uint Owner,RavenBirth Birth,MonsterDifficulty Difficulty,int Map,ActorMotionState Motion,
    ulong Seed,DateTime NextFrameUtc,RavenAttack Packet,int Frame=0,int Mode=8,int NextThink=25,int ActionAt=0,int EndAt=14,
    int Remaining=-1,int NextAttack=3,int Orbit=0,uint Focus=0,uint Target=0,int Attempts=0,int Hits=0,string Fault=null)
{
    // Current raw animation data plus the inspected legacy NPC scheduler.
    // Blizzard changed Raven AI in2.4; exact current scheduling is not proven.
    public string TimingProfile{get;init;}="legacy-npc-clock-current-hk-metadata-v1";
}

// Actual minion backend. The explicit legacy timing profile is not a claim
// of complete current-executable AI/event/geometry equivalence.
[SerializationGenerator(0)]
public partial class DruidRavenBody:SkeletonGuardian
{
    [SerializableField(0,setter:"private")] private string _saved="{}";
    private RavenBodyState _prepared;
    private static RavenBodyState Copy(RavenBodyState state)=>state with{Motion=state.Motion with{Points=[..state.Motion.Points],SavedSteps=[..state.Motion.SavedSteps]}};
    internal RavenBodyState Prepared
    {
        get
        {
            var state=_prepared??(_saved=="{}"?null:_prepared=JsonSerializer.Deserialize<RavenBodyState>(_saved));
            return state==null?null:Copy(state);
        }
    }
    internal string SavedText=>_saved;
    public override DiabloClass SummonClass=>DiabloClass.Druid;
    public override bool Permitted(PlayerMobile owner)=>ClassCreation.Profiles.Histories(owner.Serial)
        .Any(x=>x.Profile.Class==DiabloClass.Druid && x.Ranks.GetValueOrDefault("druid.raven")>0);
    public override bool BlocksFit=>false;
    public override bool CanBeDamaged()=>false;
    public override bool CanRegenStam=>false;
    public override bool UsesNativeSummonTimer=>false;
    public override bool IsDispellable=>false;
    public override bool OnMoveOver(Mobile mobile)=>true;
    public override long SourceLife256=>Prepared?.Birth.Life256??26*256;
    public override MonsterProfile CombatProfile=>Prepared is{} s
        ?new(s.Birth.Level,s.Birth.Life256/256,s.Birth.Attack+s.Packet.AttackBonus,s.Birth.Defense,
            s.Packet.Minimum256/256,s.Packet.Maximum256/256,1000,400,0,0,0,0,0)
        :new(1,26,0,25,2,4,1000,400,0,0,0,0,0);
    public override (int Min,int Max) SkillWeaponDamage=>(CombatProfile.MinDamage,CombatProfile.MaxDamage);
    public DruidRavenBody()
    {
        Name="Raven [placeholder]";Body=0x6;BaseSoundID=-1;Blessed=true;CantWalk=true;ControlSlots=0;
        AIObject?.AITimer.Stop();SetHits(26);SetDamage(0,0);
    }
    // The native skeleton swing is suppressed; only the source frame actor
    // may request an attack. CantWalk also suppresses ordinary native AI moves.
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromSeconds(1);
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    public override bool CanBeHarmful(Mobile target,bool message,bool ignoreOurBlessedness)
        =>IsEnemy(target)&&base.CanBeHarmful(target,message,true);
    internal static Point2D Cell(RavenBodyState s)=>DruidRavenMotion.Cell(s.Motion);
    private Point3D Position(RavenBodyState s)=>new((Cell(s).X+1)/2,(Cell(s).Y+1)/2,Z);
    private static ActorMotionState Stop(ActorMotionState motion)=>motion with{Points=[],Index=0,Flags=motion.Flags&~0x28,Vector=new(0,0)};
    internal static void Validate(RavenBodyState s)
    {
        if(s?.Birth is not{Rank:>=1 and <=255,Level:>=1 and <=352,Life256:6656,Pierce:0,Blind:-1}
            || s.Motion?.Points==null || s.Motion.SavedSteps==null || !((Serial)s.Owner).IsMobile || s.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
            || s.Map<0 || s.Map>=Map.Maps.Length || Map.Maps[s.Map]==null || Map.Maps[s.Map]==Map.Internal
            || s.NextFrameUtc.Kind!=DateTimeKind.Utc || s.Frame<0 || s.Frame>int.MaxValue-100
            || s.Mode is not(1 or 2 or 4 or 8) || s.Remaining is <-1 or >266 || s.Orbit is <0 or >1
            || s.NextAttack<0 || s.NextThink<0 || s.NextThink!=0&&s.NextThink<=s.Frame
            || s.Attempts<0 || s.Hits<0 || s.Hits>s.Attempts || s.Packet.Rank is <1 or >255
            || s.Packet.Minimum256<0 || s.Packet.Maximum256<s.Packet.Minimum256
            || s.TimingProfile!="legacy-npc-clock-current-hk-metadata-v1"
            || s.Mode==1&&(s.NextThink==0 || s.ActionAt!=0 || s.EndAt!=0 || s.Target!=0)
            || s.Mode==2&&(s.NextThink!=0 || s.ActionAt!=0 || s.EndAt!=0 || (s.Motion.Flags&0x20)==0)
            || s.Mode==4&&(s.NextThink!=0 || s.Target==0 || s.EndAt<=s.Frame || s.ActionAt!=0&&s.ActionAt<=s.Frame)
            || s.Mode==8&&(s.Frame>=s.EndAt || s.NextThink!=25 || s.ActionAt!=0 || s.Target!=0))
        {throw new InvalidDataException("Invalid prepared Raven actor");}
        var cell=Cell(s);var map=Map.Maps[s.Map];
        if(cell.X<=0 || cell.Y<=0 || cell.X>=map.Width*2 || cell.Y>=map.Height*2
            || DruidRavenStats.Rescale(s.Birth,s.Difficulty)!=s.Birth)
        {throw new InvalidDataException("Raven pose/birth disagrees with source context");}
        SourceActorMotion.Advance(s.Motion with{Flags=s.Motion.Flags&~0x20},(_,_)=>throw new InvalidOperationException());
    }
    internal void RestorePrepared(RavenBodyState state){Validate(state);var copy=Copy(state);string json=JsonSerializer.Serialize(copy);_prepared=copy;_saved=json;}
    internal static DruidRavenBody CreatePrepared(PlayerMobile owner,Point3D point,DateTime at,ulong seed,bool enforce=true)
    {
        if(at.Kind!=DateTimeKind.Utc || owner.Map==null || owner.Map==Map.Internal || point.X<=0 || point.Y<=0
            || point.X>=owner.Map.Width || point.Y>=owner.Map.Height || Math.Abs(point.Z-owner.Z)>=16)
        {throw new ArgumentException("Raven needs a supported world point/UTC clock");}
        var birth=DruidRavenStats.Capture(owner);int orbit=(int)(SourceRandom.Roll(ref seed)&1);
        var packet=DruidRavenStats.Attack(SkillMechanics.Values(owner,CombatAbility.Raven));
        var state=new RavenBodyState(owner.Serial.Value,birth,SummonDifficulties.For(owner.Map),owner.Map.MapID,
            DruidRavenMotion.Center(point.X*2,point.Y*2),seed,at.AddMilliseconds(40),packet,EndAt:DruidRavenMotion.Clock(true).End,Orbit:orbit);
        Validate(state);var body=new DruidRavenBody();
        try
        {
            body.RestorePrepared(state);body.Initialize(owner,birth.Rank,0);
            foreach(var kind in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){body.SetResistance(kind,0);}
            if(!body.SetControlMaster(owner)){throw new InvalidOperationException("Raven control master refused");}
            BattleCommand.Track(body);PersistentMinions.Profiles.Track(body);body.MoveToWorld(point,owner.Map);body.AIObject?.AITimer.Stop();
            DruidRavens.Track(body);if(enforce){DruidRavens.Enforce(owner);}return body;
        }
        catch{body.Delete();throw;}
    }
    internal override void RefreshDifficulty()
    {
        if(Prepared is{} s){RestorePrepared(s with{Birth=DruidRavenStats.Rescale(s.Birth,SummonDifficulty),Difficulty=SummonDifficulty});}
        base.RefreshDifficulty();
    }
    public override void SuspendActions()
    {
        base.SuspendActions();
        if(Prepared is{} s && s.Mode is 2 or 4)
        {RestorePrepared(s with{Mode=1,NextThink=checked(s.Frame+1),ActionAt=0,EndAt=0,Target=0,Motion=Stop(s.Motion)});}
    }
    internal bool Eligible(PlayerMobile owner,Mobile target)=>target is BaseCreature creature && target is{Alive:true,Deleted:false}
        && target.Map==Map && !target.Blessed && target.CanBeDamaged() && creature is not BaseVendor
        && CreatureProfiles.For(creature).Traits.Size is >=0 and <=3 && Math.Abs(target.Z-Z)<16
        && ClassCombat.IsHostile(owner,target) && !SpellHelper.IsTown(target.Location,owner)
        && base.CanBeHarmful(target,false,true);
    internal void FocusOn(Mobile target)
    {
        if(Prepared is{} s && ControlMaster is PlayerMobile owner && Eligible(owner,target))
        {RestorePrepared(s with{Focus=target.Serial.Value});}
    }
    private Mobile SelectTarget(PlayerMobile owner,RavenBodyState s)
    {
        var focused=World.FindMobile((Serial)s.Focus);if(Eligible(owner,focused)){return focused;}
        Mobile chosen=null;int best=int.MaxValue;var cell=Cell(s);
        foreach(var candidate in Map.GetMobilesInRange<BaseCreature>(Location,19))
        {
            if(!Eligible(owner,candidate)){continue;}
            int distance=DruidRavenMotion.Distance(cell,new(candidate.X*2,candidate.Y*2),0,CreatureProfiles.For(candidate).Traits.Size.Value);
            if(distance<35 && distance<best){chosen=candidate;best=distance;}
        }
        return chosen;
    }
    private static int Draw(ref ulong seed,int bound)=>(int)SourceRandom.Limited(ref seed,bound);
    private bool Melee(Mobile target,RavenBodyState s)
        =>target is BaseCreature creature && DruidRavenMotion.MeleeDistance(Cell(s),new(target.X*2,target.Y*2),CreatureProfiles.For(creature).Traits.Size.Value)<=1
            && Map.LineOfSight(new Point3D(X,Y,Z+12),new Point3D(target.X,target.Y,target.Z+12));
    private RavenBodyState Idle(RavenBodyState s,int delay=15)=>s with{Mode=1,NextThink=checked(s.Frame+delay),ActionAt=0,EndAt=0,Target=0,Motion=Stop(s.Motion)};
    private bool Path(RavenBodyState s,Point2D target,int type,int budget,int velocityBonus,out RavenBodyState moved)
    {
        moved=s;
        if(target.X<=0 || target.Y<=0 || target.X>=Map.Width*2 || target.Y>=Map.Height*2){return false;}
        var speed=SourceActorMotion.Speed(NecromancerSummons.Source("druidhawk","Velocity"),208,100+velocityBonus,0);
        var motion=s.Motion with{PathType=type,Flags=0,Points=[],Index=0,Velocity=speed.Velocity,MaximumVelocity=speed.Velocity,Distance=budget};
        var path=SourceActorPath.Initialize(new(motion,target,motion.FinalTarget),budget,new(1,1,Map.Width*2-1,Map.Height*2-1),
            (x,y)=>x>0&&y>0&&x<Map.Width*2&&y<Map.Height*2?0:0x3c01);
        moved=s with{Mode=2,NextThink=0,ActionAt=0,EndAt=0,Motion=path.Motion};
        return path.Motion.Points.Length!=0 && (path.Motion.Flags&0x20)!=0;
    }
    private RavenBodyState Spread(PlayerMobile owner,RavenBodyState s)
    {
        var cell=Cell(s);var center=new Point2D(owner.X*2,owner.Y*2);
        int distance=DruidRavenMotion.Distance(cell,center,0,2);
        if(distance>0 && Path(s,new(center.X+8*(cell.X-center.X)/distance,center.Y+8*(cell.Y-center.Y)/distance),2,20,0,out var moved)){return moved;}
        return Warp(owner,s);
    }
    private RavenBodyState Warp(PlayerMobile owner,RavenBodyState s)
    {
        MoveToWorld(owner.Location,owner.Map);AIObject?.AITimer.Stop();
        return Idle(s with{Map=owner.Map.MapID,Motion=DruidRavenMotion.Center(owner.X*2,owner.Y*2)});
    }
    private RavenBodyState Think(PlayerMobile owner,RavenBodyState s)
    {
        if(s.Remaining<0){s=s with{Remaining=DruidRavenStats.InitialBudget(SkillMechanics.Values(owner,CombatAbility.Raven))};}
        else if(s.Remaining==0){Delete();return s;}
        var cell=Cell(s);var center=new Point2D(owner.X*2,owner.Y*2);int ownerDistance=DruidRavenMotion.Distance(cell,center);
        if(ownerDistance>50){return Warp(owner,s);}
        if(ownerDistance>28){return Path(s,center,2,20,100,out var follow)?follow:Idle(s);}
        var target=SelectTarget(owner,s);ulong seed=s.Seed;
        if(target!=null && s.NextAttack<s.Frame)
        {
            bool chance=Draw(ref seed,100)<NecromancerSummons.Source("druidhawk","aip4");s=s with{Seed=seed};
            int distance=DruidRavenMotion.Distance(cell,new(target.X*2,target.Y*2),0,CreatureProfiles.For((BaseCreature)target).Traits.Size.Value);
            if(chance && distance<NecromancerSummons.Source("druidhawk","aip5"))
            {
                if(Melee(target,s))
                {
                    var clock=DruidRavenMotion.Clock(false);Direction=GetDirectionTo(target);Animate(4,5,1,true,false,0); // Identified bird-attack placeholder.
                    return s with{Mode=4,NextThink=0,ActionAt=checked(s.Frame+clock.Action),EndAt=checked(s.Frame+clock.End),Target=target.Serial.Value,
                        Packet=DruidRavenStats.Attack(SkillMechanics.Values(owner,CombatAbility.Raven)),Remaining=s.Remaining-1,
                        NextAttack=checked(s.Frame+10*NecromancerSummons.Source("druidhawk","aip3")),Attempts=checked(s.Attempts+1),Motion=Stop(s.Motion)};
                }
                return Path(s,new(target.X*2,target.Y*2),2,20,0,out var chase)?chase:Idle(s);
            }
        }
        if(ownerDistance<6 || ownerDistance>10){return Spread(owner,s);}
        if(Path(s,center,s.Orbit==0?6:5,4,0,out var orbit)){return orbit;}
        s=s with{Orbit=1-s.Orbit};
        return Path(s,center,s.Orbit==0?6:5,4,0,out orbit)?orbit:Spread(owner,s);
    }
    private RavenBodyState Strike(PlayerMobile owner,RavenBodyState s)
    {
        var target=World.FindMobile((Serial)s.Target);if(!Eligible(owner,target)||!Melee(target,s)){return s with{ActionAt=0};}
        ulong seed=s.Seed;int Next(int bound)=>Draw(ref seed,bound);
        using var attempted=PaladinThorns.Attempt(this,target,false);
        bool hit=ClassStats.CheckHit(this,target,0,()=>Next(1_000_000)/1_000_000.0)==true;
        if(hit)
        {
            int min=s.Packet.Minimum256/256,max=s.Packet.Maximum256/256;int damage=checked(min+Next(max-min+1));
            if(Next(100)<NecromancerSummons.Source("druidhawk","Crit")){damage=checked(damage*2);}
            SkillCombat.ResolveHitParts(this,target,new(damage,0,0,0,0),true,true);
            NamedControl.ApplySummonBlind(this,target,s.Birth.Blind,false,()=>Next(100));
        }
        return s with{ActionAt=0,Seed=seed,Hits=checked(s.Hits+(hit?1:0))};
    }
    internal void Advance(DateTime at,bool actions)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Raven clock must be UTC");}
        var s=Prepared;if(s==null || s.Fault!=null || s.NextFrameUtc>at){return;}
        if(ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner || owner.Serial.Value!=s.Owner){Delete();return;}
        if(!PersistentMinions.Active(owner,this)||!Permitted(owner))
        {PersistentMinions.ParkClass(owner,DiabloClass.Druid);Inactive(at);return;}
        if(!actions || owner.Map==null || owner.Map==Map.Internal)
        {PersistentMinions.Suspend(owner);Inactive(at);return;}
        if(Map==Map.Internal)
        {
            PersistentMinions.Resume(owner);
            // Zero-size Raven can share its owner's legal point even when the
            // ordinary follower placement search finds no floor/adjacent cell.
            if(Map==Map.Internal){MoveToWorld(owner.Location,owner.Map);AIObject?.AITimer.Stop();}
        }
        s=Prepared;
        if(Map!=owner.Map){s=Warp(owner,s);}
        if(s.Map!=Map.MapID || Location!=Position(s))
        {s=Idle(s with{Map=Map.MapID,Motion=DruidRavenMotion.Center(X*2,Y*2)});}
        while(!Deleted && s.NextFrameUtc<=at)
        {
            s=s with{Frame=checked(s.Frame+1),NextFrameUtc=s.NextFrameUtc.AddMilliseconds(40)};
            if(s.Mode==2)
            {
                var moved=SourceActorMotion.Advance(s.Motion,(x,y)=>x>0&&y>0&&x<Map.Width*2&&y<Map.Height*2?0:0x3c01);
                if(moved.RepathRequired){throw new NotSupportedException("Raven unit-target path repair is not connected");}
                s=s with{Motion=moved.State};
                if(!moved.Moving){s=Idle(s,0);}
            }
            if(s.Mode==4 && s.ActionAt==s.Frame){s=Strike(owner,s);}
            if(s.Mode is 4 or 8 && s.EndAt==s.Frame)
            {int next=s.NextThink>s.Frame?s.NextThink:checked(s.Frame+15);s=Idle(s) with{NextThink=next};}
            if(s.NextThink==s.Frame)
            {s=Think(owner,s);if(Deleted){return;}}
            RestorePrepared(s);var position=Position(s);if(Location!=position){MoveToWorld(position,Map);}AIObject?.AITimer.Stop();
        }
    }
    private void Inactive(DateTime at)
    {
        if(Prepared is not{} s || s.NextFrameUtc>at){return;}
        int count=checked((int)((at-s.NextFrameUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1));
        int frame=checked(s.Frame+count);s=s with{Frame=frame,NextFrameUtc=s.NextFrameUtc.AddMilliseconds((long)count*40)};
        if(s.Mode!=8 || frame>=s.EndAt){s=Idle(s,1);}
        RestorePrepared(s);
    }
    internal void Fault(string reason){if(Prepared is{} s){RestorePrepared(s with{Fault=reason});}}
    public override bool OnBeforeDeath(){Delete();return false;}
    public override void OnAfterDelete(){DruidRavens.Forget(this);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterRavenLoad()
    {AIObject?.AITimer.Stop();_prepared=JsonSerializer.Deserialize<RavenBodyState>(_saved);Validate(_prepared);DruidRavens.Track(this);}
}

internal static class DruidRavens
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidRavens));
    private static readonly HashSet<DruidRavenBody> Bodies=new();
    internal static void Track(DruidRavenBody body)=>Bodies.Add(body);
    internal static void Forget(DruidRavenBody body)=>Bodies.Remove(body);
    internal static DruidRavenBody[] For(PlayerMobile owner)=>(owner.AllFollowers??[]).OfType<DruidRavenBody>()
        .Where(x=>x is{Alive:true,Deleted:false}&&x.ControlMaster==owner).OrderBy(BattleCommand.PetOrder).ThenBy(x=>x.Serial.Value).ToArray();
    internal static void Enforce(PlayerMobile owner)
    {
        var bodies=For(owner);int limit=DruidRavenStats.Limit(SkillMechanics.Values(owner,CombatAbility.Raven));
        foreach(var body in bodies.Take(Math.Max(0,bodies.Length-limit))){body.Delete();}
    }
    internal static void Focus(PlayerMobile owner,Mobile target){foreach(var body in For(owner)){body.FocusOn(target);}}
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static void Tick(DateTime at,bool? actions=null)
    {
        var owners=new HashSet<PlayerMobile>();
        foreach(var body in Bodies.ToArray())
        {
            if(body.Deleted){Forget(body);continue;}
            try
            {
                if(body.ControlMaster is PlayerMobile owner && body.Prepared.NextFrameUtc<=at && owners.Add(owner)
                    && PersistentMinions.Active(owner,body)&&body.Permitted(owner)){Enforce(owner);}
                if(!body.Deleted){body.Advance(at,actions??body.ControlMaster?.NetState is{Running:true});}
            }
            catch(Exception e){body.Fault(e.Message);Log.Error(e,"Prepared Raven controller refused unsupported context for {Serial}",body.Serial);}
        }
    }
}
