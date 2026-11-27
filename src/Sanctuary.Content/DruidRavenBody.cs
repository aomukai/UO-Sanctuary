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
namespace Sanctuary;

internal sealed record RavenBodyState(uint Owner,RavenBirth Birth,MonsterDifficulty Difficulty,int Map,ActorMotionState Motion,
    ulong Seed,DateTime NextFrameUtc,RavenAttack Packet,int Frame=0,int Mode=8,int NextThink=25,int ActionAt=0,int EndAt=14,
    int Remaining=-1,int NextAttack=3,int Orbit=0,uint Focus=0,uint Target=0,int Attempts=0,int Hits=0,string Fault=null)
{
    // Current raw animation data plus the inspected legacy NPC scheduler.
    // Blizzard changed Raven AI in2.4; exact current scheduling is not proven.
    public string TimingProfile{get;init;}="legacy-npc-clock-current-hk-metadata-v1";
    // Missing historical JSON keeps the original live owner-stat behavior.
    // This stat policy is independent of the unresolved modern AI cadence.
    public string StatProfile{get;init;}=DruidRavenBody.LegacyStats;
}

// Actual minion backend. The explicit legacy timing profile is not a claim
// of complete current-executable AI/event/geometry equivalence.
[SerializationGenerator(0)]
public partial class DruidRavenBody:SkeletonGuardian
{
    internal const string LegacyStats="legacy-live-owner-mode-v1";
    internal const string SnapshotStats="sanctuary-summon-snapshot-v1";
    private const int StrikeRange=2;
    private const int CombatPollFrames=2;
    private const int DiveRecoveryFrames=15;
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
    public override bool CanTarget=>false;
    public override bool CanBeDamaged()=>false;
    // A living native Mobile needs one internal hit point. The flock has no health pool.
    public override int HitsMax=>1;
    public override bool CanRegenStam=>false;
    public override bool UsesNativeSummonTimer=>false;
    public override bool IsDispellable=>false;
    public override bool OnMoveOver(Mobile mobile)=>true;
    internal int FlockSize=>Math.Clamp(Prepared?.Birth.Rank??1,1,RavenAppearance.MaxCount);
    public override long SourceLife256=>256;
    public override MonsterProfile CombatProfile=>Prepared is{} s
        ?new(s.Birth.Level,1,s.Birth.Attack+s.Packet.AttackBonus,s.Birth.Defense,
            s.Packet.Minimum256/256*FlockSize,s.Packet.Maximum256/256*FlockSize,1000,200,0,0,0,0,0)
        :new(1,1,0,25,2,4,1000,200,0,0,0,0,0);
    public override (int Min,int Max) SkillWeaponDamage=>(CombatProfile.MinDamage,CombatProfile.MaxDamage);
    public DruidRavenBody()
    {
        ApplyAppearance(1);
        BaseSoundID=-1;Blessed=true;ControlSlots=0;
        AIObject?.AITimer.Stop();SetHits(1);SetDamage(0,0);
    }
    private void ApplyAppearance(int count)
    {
        Name=count==1?"Raven":"Raven Flock";
        Body=RavenAppearance.FirstBody+count-1;
    }
    // Drive native pathfinding from the flock controller and keep a single dive clock.
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromSeconds(1);
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    public override void OnDoubleClick(Mobile from) { }
    public override bool CanBeHarmful(Mobile target,bool message,bool ignoreOurBlessedness)
        =>IsEnemy(target)&&base.CanBeHarmful(target,message,true);
    internal static Point2D Cell(RavenBodyState s)=>DruidRavenMotion.Cell(s.Motion);
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
            || s.StatProfile is not(LegacyStats or SnapshotStats)
            || s.StatProfile==SnapshotStats&&(s.Remaining<0||s.Packet.Rank!=s.Birth.Rank)
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
    private static RavenBodyState Birth(PlayerMobile owner,Point3D point,DateTime at,ulong seed)
    {
        if(at.Kind!=DateTimeKind.Utc || owner.Map==null || owner.Map==Map.Internal || point.X<=0 || point.Y<=0
            || point.X>=owner.Map.Width || point.Y>=owner.Map.Height || Math.Abs(point.Z-owner.Z)>=16)
        {throw new ArgumentException("Raven needs a supported world point/UTC clock");}
        var birth=DruidRavenStats.Capture(owner);int orbit=(int)(SourceRandom.Roll(ref seed)&1);
        var values=SkillMechanics.Values(owner,CombatAbility.Raven);
        var packet=DruidRavenStats.Attack(values);
        return new RavenBodyState(owner.Serial.Value,birth,SummonDifficulties.For(owner),owner.Map.MapID,
            DruidRavenMotion.Center(point.X*2,point.Y*2),seed,at.AddMilliseconds(40),packet,Remaining:DruidRavenStats.InitialBudget(values),EndAt:DruidRavenMotion.Clock(true).End,Orbit:orbit){StatProfile=SnapshotStats};
    }
    private void ApplyBirth(PlayerMobile owner,RavenBodyState state)
    {
        RestorePrepared(state);
        ApplyAppearance(FlockSize);
        Initialize(owner,state.Birth.Rank,0);
        foreach(var kind in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy})
        {SetResistance(kind,0);}
    }
    internal bool CanRefresh(PlayerMobile owner)=>Map==owner.Map && InRange(owner,CombatProtocol.SightRange)
        && Math.Abs(Z-owner.Z)<16 && Prepared is{Fault:null};
    internal RavenBodyState PrepareRefresh(PlayerMobile owner,Mobile target,DateTime at,ulong seed)
    {
        var previous=Prepared;
        var next=Birth(owner,Location,at,seed) with
        {
            Frame=previous.Frame,Mode=1,NextThink=checked(previous.Frame+1),EndAt=0,
            NextAttack=previous.NextAttack,Focus=target?.Serial.Value??0
        };
        Validate(next);
        return next;
    }
    internal void Refresh(PlayerMobile owner,RavenBodyState state)
    {
        ApplyBirth(owner,state);
        AIObject?.ResetApproachState();
        AIObject?.AITimer.Stop();
    }
    internal static DruidRavenBody CreatePrepared(PlayerMobile owner,Point3D point,DateTime at,ulong seed,bool enforce=true)
    {
        var state=Birth(owner,point,at,seed);
        Validate(state);var body=new DruidRavenBody();
        try
        {
            body.ApplyBirth(owner,state);
            if(!body.SetControlMaster(owner)){throw new InvalidOperationException("Raven control master refused");}
            body.Direction=owner.Location==point?owner.Direction&Direction.Mask:owner.GetDirectionTo(point);
            BattleCommand.Track(body);PersistentMinions.Profiles.Track(body);body.MoveToWorld(point,owner.Map);body.AIObject?.AITimer.Stop();
            DruidRavens.Track(body);if(enforce){DruidRavens.KeepLatest(owner);}return body;
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
        AIObject?.ResetApproachState();
        if(Prepared is{} s && s.Mode is 2 or 4)
        {RestorePrepared(s with{Mode=1,NextThink=checked(s.Frame+1),ActionAt=0,EndAt=0,Target=0,Motion=Stop(s.Motion)});}
    }
    private bool InEngagementRange(PlayerMobile owner,Mobile target)=>target is{Alive:true,Deleted:false}
        && target.Map==Map && owner.Map==Map && owner.InRange(target.Location,CombatProtocol.SightRange+PetCombatRanges.BonusTiles);
    internal bool Eligible(PlayerMobile owner,Mobile target)=>InEngagementRange(owner,target)
        && target.CanBeDamaged() && CanBeHarmful(target,false,true);
    internal Mobile CurrentFocus(PlayerMobile owner)
    {
        var target=World.FindMobile((Serial)(Prepared?.Focus??0));
        return Eligible(owner,target)?target:null;
    }
    internal void FocusOn(Mobile target)
    {
        if(Prepared is{} s && ControlMaster is PlayerMobile owner && Eligible(owner,target))
        {RestorePrepared(s with{Focus=target.Serial.Value});}
    }
    private Mobile FindTarget(PlayerMobile owner)
    {
        var ownerTarget=World.FindMobile(ClassCombat.For(owner).Target);
        if(Eligible(owner,ownerTarget)){return ownerTarget;}
        Mobile chosen=null;double best=double.MaxValue;
        foreach(var candidate in Map.GetMobilesInRange<BaseCreature>(Location,CombatProtocol.SightRange+PetCombatRanges.BonusTiles))
        {
            if(!Eligible(owner,candidate)||!owner.InLOS(candidate)){continue;}
            double distance=this.GetDistanceToSqrt(candidate);
            if(distance<best){chosen=candidate;best=distance;}
        }
        return chosen;
    }
    private static int Draw(ref ulong seed,int bound)=>(int)SourceRandom.Limited(ref seed,bound);
    private bool InStrikeRange(Mobile target)
        =>InRange(target,StrikeRange) && Math.Abs(target.Z-Z)<16 && InLOS(target);
    private RavenBodyState Idle(RavenBodyState s,int delay=15)=>s with{Mode=1,NextThink=checked(s.Frame+delay),ActionAt=0,EndAt=0,Target=0,Motion=Stop(s.Motion)};
    private RavenBodyState Follow(Mobile target,int range,RavenBodyState s)
    {
        AIObject?.MoveTo(target,range);
        return Idle(s with{Motion=DruidRavenMotion.Center(X*2,Y*2)},CombatPollFrames);
    }
    private RavenBodyState Think(PlayerMobile owner,RavenBodyState s)
    {
        if(s.Remaining<0){s=s with{Remaining=DruidRavenStats.InitialBudget(SkillMechanics.Values(owner,CombatAbility.Raven))};}
        else if(s.Remaining==0){Delete();return s;}
        var target=World.FindMobile((Serial)s.Focus);
        if(!Eligible(owner,target))
        {
            target=FindTarget(owner);
            s=s with{Focus=target?.Serial.Value??0};
        }
        if(target!=null)
        {
            if(!InStrikeRange(target))
            {return Follow(target,InRange(target,StrikeRange)?0:StrikeRange,s);}
            AIObject?.ResetApproachState();
            if(s.Frame<s.NextAttack){return Idle(s,Math.Min(CombatPollFrames,s.NextAttack-s.Frame));}
            var clock=DruidRavenMotion.Clock(false);
            if(target.X!=X||target.Y!=Y){Direction=GetDirectionTo(target);}
            ClassCombat.SendAnimation(this,RavenAppearance.DiveAction,clock.End*40);
            Animate(RavenAppearance.DiveAction,RavenAppearance.DiveFrames,1,true,false,0);
            return Strike(target,s with{Mode=4,NextThink=0,ActionAt=0,EndAt=checked(s.Frame+clock.End),Target=target.Serial.Value,
                Packet=s.StatProfile==SnapshotStats?s.Packet:DruidRavenStats.Attack(SkillMechanics.Values(owner,CombatAbility.Raven)),
                NextAttack=checked(s.Frame+clock.End+DiveRecoveryFrames),Motion=Stop(s.Motion)});
        }
        if(!InRange(owner,3)){return Follow(owner,3,s);}
        AIObject?.ResetApproachState();
        return Idle(s);
    }
    private RavenBodyState Strike(Mobile target,RavenBodyState s)
    {
        ulong seed=s.Seed;int Next(int bound)=>Draw(ref seed,bound);
        int min=s.Packet.Minimum256/256,max=s.Packet.Maximum256/256;
        int damage=checked(min+Next(max-min+1));
        if(Next(100)<NecromancerSummons.Source("druidhawk","Crit")){damage=checked(damage*2);}
        var point=target.Location;
        var map=target.Map;
        var contact=SkillCombat.ResolveHitParts(this,target,new(damage*FlockSize,0,0,0,0),true,spell:true);
        bool blinded=NamedControl.ApplySummonBlind(this,target,s.Birth.Blind,false,()=>Next(100));
        if(contact.LifeLost>0 || blinded){DruidSummonVisuals.Contact(this,point,map);}
        return s with{Seed=seed,Remaining=s.Remaining-1,Attempts=checked(s.Attempts+1),Hits=checked(s.Hits+1)};
    }
    internal void Advance(DateTime at,bool actions)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Raven clock must be UTC");}
        var s=Prepared;if(s==null || s.Fault!=null || s.NextFrameUtc>at){return;}
        if(ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner || owner.Serial.Value!=s.Owner){Delete();return;}
        if(!PersistentMinions.Active(owner,this)||!Permitted(owner))
        {PersistentMinions.ParkClass(owner,DiabloClass.Druid);Inactive(at);return;}
        if(!actions || owner.Map==null || owner.Map==Map.Internal)
        {PersistentMinions.Suspend(this);Inactive(at);return;}
        if(MinionSafeAreas.ShouldPark(owner,this))
        {PersistentMinions.Park(this);Inactive(at);return;}
        if(Map==Map.Internal)
        {
            PersistentMinions.Resume(owner);
            // Zero-size Raven can share its owner's legal point even when the
            // ordinary follower placement search finds no floor/adjacent cell.
            if(Map==Map.Internal){MoveToWorld(owner.Location,owner.Map);AIObject?.AITimer.Stop();}
        }
        s=Prepared;
        if(Map!=owner.Map)
        {
            MoveToWorld(PersistentMinions.FindPosition(owner,out var point)?point:owner.Location,owner.Map);
            s=Idle(s with{Map=Map.MapID,Motion=DruidRavenMotion.Center(X*2,Y*2)},1);
        }
        // Replan old projected paths and any old dive that hadn't made contact yet.
        if(s.Mode==2 || s is{Mode:4,ActionAt:>0})
        {s=Idle(s with{Motion=DruidRavenMotion.Center(X*2,Y*2)},1);}
        while(!Deleted && s.NextFrameUtc<=at)
        {
            s=s with{Frame=checked(s.Frame+1),NextFrameUtc=s.NextFrameUtc.AddMilliseconds(40)};
            if(s.Mode is 4 or 8 && s.EndAt==s.Frame)
            {
                s=Idle(s,s.Mode==4?1:s.NextThink-s.Frame);
                if(s.Remaining==0){RestorePrepared(s);Delete();return;}
            }
            if(s.NextThink==s.Frame)
            {s=Think(owner,s);if(Deleted){return;}}
            RestorePrepared(s);
            AIObject?.AITimer.Stop();
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
    {
        AIObject?.AITimer.Stop();_prepared=JsonSerializer.Deserialize<RavenBodyState>(_saved);Validate(_prepared);
        CantWalk=false;Hits=1;ApplyAppearance(FlockSize);DruidRavens.Track(this);
    }
}

internal static class DruidRavens
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidRavens));
    private static readonly HashSet<DruidRavenBody> Bodies=new();
    internal static void Track(DruidRavenBody body)=>Bodies.Add(body);
    internal static void Forget(DruidRavenBody body)=>Bodies.Remove(body);
    internal static DruidRavenBody[] For(PlayerMobile owner)=>(owner.AllFollowers??[]).OfType<DruidRavenBody>()
        .Where(x=>x is{Alive:true,Deleted:false}&&x.ControlMaster==owner).OrderBy(BattleCommand.PetOrder).ThenBy(x=>x.Serial.Value).ToArray();
    internal static void KeepLatest(PlayerMobile owner)
    {
        var bodies=For(owner);
        foreach(var body in bodies.Take(bodies.Length-1)){body.Delete();}
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
                    && PersistentMinions.Active(owner,body)&&body.Permitted(owner)){KeepLatest(owner);}
                if(!body.Deleted){body.Advance(at,actions??body.ControlMaster?.NetState is{Running:true});}
            }
            catch(Exception e){body.Fault(e.Message);Log.Error(e,"Prepared Raven controller refused unsupported context for {Serial}",body.Serial);}
        }
    }
}
