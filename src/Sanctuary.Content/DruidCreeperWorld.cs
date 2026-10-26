// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record CreeperWorldState(int Map,ActorMotionState Motion,ulong Seed,DateTime NextUtc,int GameBase,CreeperAiState Ai,
    int Frame=0,int Mode=8,int NextThink=25,int ActionAt=0,int EndAt=3,uint Target=0,int PathOverride=0,int PathBudget=0,
    int VelocityBonus=0,int MovingBonus=0,int SkillEvents=0,string Fault=null)
{
    public string Profile{get;init;}="source-ai-independent-field-contact-capture-v3";
    public CreeperHeadRequest[] LastHeads{get;init;}=[];
    public CreeperFields Fields{get;init;}=DruidCreeperFields.Empty();
    public long LastFieldId{get;init;}
}

// Internal world controller. Heads now move and create finite stationary mats;
// contacts are captured only. Poison and VINE_BEAST targeting remain separate.
internal static class DruidCreeperWorld
{
    internal static CreeperWorldState Copy(CreeperWorldState s)=>s with{Profile="source-ai-independent-field-contact-capture-v3",
        Motion=s.Motion with{Points=[..s.Motion.Points],SavedSteps=[..s.Motion.SavedSteps]},LastHeads=[..s.LastHeads],Fields=DruidCreeperFields.Copy(s.Fields)};
    internal static Point2D Cell(CreeperWorldState s)=>DruidRavenMotion.Cell(s.Motion);
    internal static void Validate(CreeperWorldState s)
    {
        if(s?.Motion?.Points==null || s.Motion.SavedSteps==null || s.LastHeads==null || s.Ai==null
            || s.Map<0 || s.Map>=Map.Maps.Length || Map.Maps[s.Map]==null || Map.Maps[s.Map]==Map.Internal
            || s.Frame<0 || s.GameBase<0 || (long)s.GameBase+s.Frame>int.MaxValue-100
            || s.Ai.LastSkillFrame<0 || s.Ai.LastSkillFrame>s.GameBase+s.Frame || s.Ai.Parameter0!=0
            || s.NextUtc.Kind!=DateTimeKind.Utc || s.Mode is not(1 or 2 or 8)
            || s.NextThink<0 || s.NextThink!=0&&s.NextThink<=s.Frame
            || s.ActionAt<0 || s.ActionAt!=0&&s.ActionAt<=s.Frame || s.EndAt<0 || s.EndAt!=0&&s.EndAt<=s.Frame
            || s.Mode==1&&(s.ActionAt!=0 || s.EndAt!=0) || s.Mode==2&&((s.Motion.Flags&0x20)==0 || s.ActionAt!=0 || s.EndAt!=0)
            || s.Mode==8&&s.EndAt==0 || s.PathOverride is not(0 or 1 or 7 or 13 or 15) || s.PathBudget is <0 or >77
            || s.VelocityBonus is <-126 or >126 || s.MovingBonus is <-126 or >126 || s.SkillEvents<0 || s.SkillEvents>s.Frame
            || s.LastFieldId<0 || s.LastHeads.Length>24 || s.Profile is not("source-ai-native-ground-vine-launch-capture-v1" or "source-ai-native-ground-vine-contact-capture-v2" or "source-ai-independent-field-contact-capture-v3"))
        {throw new InvalidDataException("Invalid saved Creeper world state");}
        DruidCreeperFields.Validate(s.Fields);
        var cell=Cell(s);var map=Map.Maps[s.Map];
        if(cell.X<=0 || cell.Y<=0 || cell.X>=map.Width*2 || cell.Y>=map.Height*2){throw new InvalidDataException("Creeper pose outside facet");}
        SourceActorMotion.Advance(s.Motion with{Flags=s.Motion.Flags&~0x20},(_,_)=>throw new InvalidOperationException());
    }
    internal static (int Action,int End) SkillClock()
    {
        int frames=NecromancerSummons.Source("plaguepoppy-animation","S1Frames"),speed=NecromancerSummons.Source("plaguepoppy-animation","S1Speed");
        int action=NecromancerSummons.Source("plaguepoppy-animation","S1SkillAction");
        if(frames!=2 || speed!=256 || action!=1){throw new NotSupportedException("Unsupported Creeper S1 metadata");}
        var flags=new int[144];flags[action]=2;var events=SourceAnimationEvents.Initial(new(0,speed,frames*256,flags,[]),0).Events;
        return(events.Single(e=>e.Type==SourceAnimationEvents.ModeChange&&e.Kind==2).Frame,events.Single(e=>e.Type==SourceAnimationEvents.EndAnimation).Frame);
    }
    internal static DruidCreeperBody Create(PlayerMobile owner,Point3D point,DateTime at,ulong seed,int? gameFrame=null)
    {
        var body=DruidCreeperBody.CreatePrepared(owner,point,at);
        try
        {
            var s=new CreeperWorldState(owner.Map.MapID,DruidRavenMotion.Center(point.X*2,point.Y*2),seed,at.AddMilliseconds(40),
                gameFrame??DruidCreepers.GameFrame,DruidCreeperAi.Initialize(new(0,0)),EndAt:SkillClock().End);
            body.RestorePrepared(body.Prepared with{Controller=s});PetOwnerRoutes.For(owner,at);DruidCreepers.Track(body);return body;
        }
        catch{body.Delete();throw;}
    }
    private static CreeperWorldState Neutral(CreeperWorldState s,int delay=15)=>s with{Mode=1,NextThink=checked(s.Frame+Math.Max(1,delay)),ActionAt=0,EndAt=0,Motion=DruidWolfGeometry.Stop(s.Motion)};
    internal static CreeperWorldState Park(CreeperWorldState s)=>Neutral(s,1) with{PathOverride=0,PathBudget=0,VelocityBonus=0};
    private static void Store(DruidCreeperBody body,CreeperWorldState s)=>body.RestorePrepared(body.Prepared with{Controller=s,NextRecoveryUtc=s.NextUtc});
    private static bool Point(DruidCreeperBody body,Point2D cell,out Point3D point)=>PetGroundWorld.Point(body,cell,out point,true);
    private static int Collision(DruidCreeperBody body,int x,int y)=>PetGroundWorld.Collision(body,x,y,true);
    private static CreeperWorldState Relocate(DruidCreeperBody body,CreeperWorldState s,Point3D point,Map map)
    {body.MoveToWorld(point,map);body.AIObject?.AITimer.Stop();return s with{Map=map.MapID,Motion=DruidRavenMotion.Center(point.X*2,point.Y*2)};}
    internal static bool Eligible(DruidCreeperBody body,PlayerMobile owner,Mobile target,int? height=null)=>target is BaseCreature c
        && c is{Alive:true,Deleted:false} && c.Map==body.Map && !c.Blessed && c.CanBeDamaged() && c is not BaseVendor
        && CreatureProfiles.For(c).Traits.Size is >=0 and <=3 && Math.Abs(c.Z-(height??body.Z))<16 && ClassCombat.IsHostile(owner,c)
        && !SpellHelper.IsTown(c.Location,owner) && body.CanBeHarmful(c,false)
        && (c is EncounterCreature || SourceAuraTargets.Rule(CreatureProfiles.Definition(c).Reference) is{Attackable:true});
    private static Mobile Target(DruidCreeperBody body,PlayerMobile owner,CreeperWorldState s)
    {
        var prior=World.FindMobile((Serial)s.Target);
        if(Eligible(body,owner,prior) && body.InLOS(prior) && DruidWolfGeometry.NoSize(Cell(s),PetOwnerRoutes.Cell(prior))<20){return prior;}
        Mobile result=null;int best=20;
        foreach(var candidate in body.Map.GetMobilesInRange<BaseCreature>(body.Location,12))
        {
            if(!Eligible(body,owner,candidate) || !body.InLOS(candidate)){continue;}
            int distance=DruidWolfGeometry.NoSize(Cell(s),PetOwnerRoutes.Cell(candidate));
            if(distance<best || distance==best&&result!=null&&candidate.Serial.Value<result.Serial.Value){result=candidate;best=distance;}
        }
        return result;
    }
    private static void Velocity(ref CreeperWorldState s,int type,int speed,int budget)
    {s=s with{PathOverride=type==0?s.PathOverride:type,VelocityBonus=speed==0?s.VelocityBonus:speed,PathBudget=budget==0?s.PathBudget:Math.Min(77,budget)};}
    private static uint Draw(ref CreeperWorldState s)
    {ulong seed=s.Seed;uint value=SourceRandom.Roll(ref seed);s=s with{Seed=seed};return value;}
    private static Point2D Wander(ref CreeperWorldState s,Point2D center,int distance)
    {
        bool first=(Draw(ref s)&1)!=0;int small=distance<=0?0:(int)(Draw(ref s)%(uint)distance);
        int x=first?distance:small,y=first?small:distance;if((Draw(ref s)&1)!=0){x=-x;}if((Draw(ref s)&1)!=0){y=-y;}
        return new(center.X+x,center.Y+y);
    }
    private static bool Path(DruidCreeperBody body,ref CreeperWorldState s,Point2D target,bool delete=false)
    {
        if(target.X<=0 || target.Y<=0 || target.X>=body.Map.Width*2 || target.Y>=body.Map.Height*2){return false;}
        int type=s.PathOverride==0?13:s.PathOverride,budget=s.PathBudget==0?5:s.PathBudget,bonus=s.VelocityBonus;
        s=s with{PathOverride=0,PathBudget=0,VelocityBonus=0};
        if(type==1){throw new NotSupportedException("Creeper source AStar fallback remains unimplemented");}
        var speed=SourceActorMotion.Speed(NecromancerSummons.Source("plaguepoppy","Velocity"),NecromancerSummons.Source("plaguepoppy-animation","WLSpeed"),100+bonus+PaladinVigor.VelocityPercent(body)-GearEffects.MovementSlow(body),0);
        var motion=s.Motion with{PathType=type,Flags=type==13?0x100:type==15?0x800:0,Velocity=speed.Velocity,MaximumVelocity=speed.Velocity,
            Acceleration=0,AccelerationCounter=0,Points=[],Index=0,Distance=budget};
        var result=SourceActorPath.Initialize(new(motion,target,motion.FinalTarget),budget,new(1,1,body.Map.Width*2-1,body.Map.Height*2-1),(x,y)=>Collision(body,x,y));
        if(result.Motion.Points.Length==0 || (result.Motion.Flags&0x20)==0)
        {
            if(type is 7 or 13){s=s with{PathOverride=15,PathBudget=budget,VelocityBonus=bonus};return Path(body,ref s,target,delete);}
            if(delete){s=s with{NextThink=0};}return false;
        }
        body.Direction=body.GetDirectionTo(new Point3D((target.X+1)/2,(target.Y+1)/2,body.Z));
        s=s with{Mode=2,NextThink=0,ActionAt=0,EndAt=0,Motion=result.Motion,MovingBonus=bonus};return true;
    }
    private static CreeperWorldState Think(DruidCreeperBody body,PlayerMobile owner,CreeperWorldState original,DateTime at)
    {
        var s=original with{NextThink=0};var ownerCell=PetOwnerRoutes.Cell(owner);var route=PetOwnerRoutes.For(owner,at);
        bool moving=ClassMovement.IsMoving(owner,Core.TickCount);int mode=moving?(ClassMovement.IsRunning(owner,Core.TickCount)?3:2):1;
        int heading=DruidWolfGeometry.NativeHeading(owner.Direction);var delta=DruidWolfGeometry.Offset(heading);
        var end=moving?new Point2D(ownerCell.X+delta.X*2,ownerCell.Y+delta.Y*2):ownerCell;
        PetMoveContext Context()=>new(Cell(s),ownerCell,end,end,mode,DruidWolfGeometry.Full(Cell(s),ownerCell,0),
            (owner.AllFollowers??[]).OfType<BaseCreature>().Count(p=>p is{Alive:true,Deleted:false}&&PersistentMinions.Active(owner,p)),moving?heading:0,route.Cursor,route.Trail,route.LastTeleport);
        int Action(PetMoveRequest r)
        {
            var p=new Point2D(r.A,r.B);
            switch(r.Kind)
            {
                case PetMoveKind.Velocity:Velocity(ref s,r.A==1?7:r.A,r.B,r.C);return 0;
                case PetMoveKind.VelocityParameters:Velocity(ref s,r.A,r.B,r.C);return 0;
                case PetMoveKind.WalkDelete:return Path(body,ref s,p,true)?1:0;
                case PetMoveKind.Walk:return Path(body,ref s,p)?1:0;
                case PetMoveKind.Wander:return Path(body,ref s,Wander(ref s,ownerCell,r.A))?1:0;
                case PetMoveKind.WalkOwner:return Path(body,ref s,Wander(ref s,ownerCell,r.A))?1:0;
                case PetMoveKind.DeleteThink:s=s with{NextThink=0};return 0;
                case PetMoveKind.Idle:s=Neutral(s,r.A);return 0;
                case PetMoveKind.MoveUnit:if(Point(body,p,out var native)){s=Relocate(body,s,native,body.Map);return 1;}return 0;
                case PetMoveKind.Refresh:case PetMoveKind.MarkTeleported:return 0;
                case PetMoveKind.CountOthers:return (owner.AllFollowers??[]).OfType<BaseCreature>().Count(p=>p!=body&&p is{Alive:true,Deleted:false}&&p.Map==body.Map&&DruidWolfGeometry.Full(Cell(s),PetOwnerRoutes.Cell(p),0)<=1);
                case PetMoveKind.Escape:
                    if(r.A>5){Velocity(ref s,0,0,r.A);}var cell=Cell(s);
                    return Path(body,ref s,new(cell.X+r.A*Math.Sign(cell.X-ownerCell.X),cell.Y+r.A*Math.Sign(cell.Y-ownerCell.Y)),r.B!=0)?1:0;
                default:throw new NotSupportedException("Unsupported Creeper movement tactic");
            }
        }
        var services=new PetMoveServices(Action,p=>PetGroundWorld.Room(body,p),p=>DruidWolfGeometry.NoSize(Cell(s),p),DruidWolfGeometry.Offset,
            ()=>Draw(ref s),()=>(int)(Draw(ref s)%100),()=>Point(body,ownerCell,out _)?ownerCell:null,p=>PetGroundWorld.Room(body,p)!=0);
        bool Common(int target,bool combat,int flag,int leash)=>SourcePetMovement.Common(Context(),services,target!=0,SpellHelper.IsTown(body.Location,owner),combat,flag,leash);
        bool Move(int kind,bool run,int speed,int leash)=>SourcePetMovement.Move(Context(),services,kind,run,speed,leash);
        CreeperAiTarget Find()
        {
            var target=Target(body,owner,s);s=s with{Target=target?.Serial.Value??0};if(target==null){return new(0,int.MaxValue,false);}
            int distance=DruidWolfGeometry.MeleeDistance(Cell(s),PetOwnerRoutes.Cell(target),0,CreatureProfiles.For((BaseCreature)target).Traits.Size.Value);
            bool combat=distance<=0 || distance<=3&&body.InLOS(target);
            return new((int)target.Serial.Value,DruidWolfGeometry.NoSize(Cell(s),PetOwnerRoutes.Cell(target)),combat);
        }
        void Request(CreeperAiRequest r)
        {
            if(r.Kind==CreeperAiKind.Idle){s=Neutral(s,r.A);return;}
            var target=World.FindMobile((Serial)(uint)r.A);if(target==null){s=Neutral(s,1);return;}
            if(r.Kind==CreeperAiKind.Walk){Path(body,ref s,PetOwnerRoutes.Cell(target));return;}
            if(r.Kind==CreeperAiKind.Escape)
            {var cell=Cell(s);var enemy=PetOwnerRoutes.Cell(target);if(r.B>5){Velocity(ref s,0,0,r.B);}Path(body,ref s,new(cell.X+r.B*Math.Sign(cell.X-enemy.X),cell.Y+r.B*Math.Sign(cell.Y-enemy.Y)));return;}
            throw new NotSupportedException("Unsupported Creeper AI action");
        }
        bool Skill(CreeperAiRequest r)
        {
            if(r.A!=8 || r.B!=294){throw new NotSupportedException("Unsupported Creeper innate skill");}
            if(BattleCommand.State(body)!=null){throw new NotSupportedException("Creeper innate-skill Battle Command transport remains unverified");}
            var clock=SkillClock();var target=World.FindMobile((Serial)(uint)r.C);if(target==null){return false;}
            body.Direction=body.GetDirectionTo(target);s=s with{Mode=8,Target=target.Serial.Value,ActionAt=s.Frame+clock.Action,EndAt=s.Frame+clock.End,NextThink=0,Motion=DruidWolfGeometry.Stop(s.Motion)};return true;
        }
        try
        {
            var ai=DruidCreeperAi.Think(new(true,DruidWolfGeometry.UnitDistance(Cell(s),ownerCell,0,2),SpellHelper.IsTown(body.Location,owner),
                checked(s.GameBase+s.Frame),294,8,body.SummonDifficulty),s.Ai,
                new(Request,Common,Move,Find,id=>World.FindMobile((Serial)(uint)id)?.Poisoned==true,id=>World.FindMobile((Serial)(uint)id)?.PoisonResistance??0,Skill));
            s=s with{Ai=ai};
        }
        catch(Exception e){Store(body,Park(s) with{Fault=e.Message});throw;}
        if(s.NextThink==0 && s.Mode is not(2 or 8)){s=s with{NextThink=s.Frame+15};}return s;
    }
    internal static void Advance(DruidCreeperBody body,DateTime at,bool actions)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Creeper world clock must be UTC");}
        DruidCreeperGround.Migrate(body);
        if(body.Deleted || body.Prepared is not{Controller:{} s} prepared || s.Fault!=null || s.NextUtc>at){return;}
        if(body.ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner || owner.Serial.Value!=prepared.Owner){body.Delete();return;}
        if(body.ControllerLoaded || !actions || !PersistentMinions.Active(owner,body) || !body.Permitted(owner) || owner.Map==null || owner.Map==Map.Internal)
        {
            body.ControllerLoaded=false;
            if(!PersistentMinions.Active(owner,body)||!body.Permitted(owner)){PersistentMinions.ParkClass(owner,DiabloClass.Druid);}else{PersistentMinions.Suspend(owner);}
            s=body.Prepared.Controller;long count=(at-s.NextUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;
            Store(body,Park(s with{Frame=checked(s.Frame+(int)count),NextUtc=s.NextUtc.AddMilliseconds(count*40)}));return;
        }
        if(body.Map==Map.Internal){PersistentMinions.Resume(owner);if(body.Map==Map.Internal){return;}}
        if(body.Map!=owner.Map)
        {body.MoveToWorld(owner.Location,owner.Map);s=Neutral(Relocate(body,s,owner.Location,owner.Map),1);}
        if(s.Map!=body.Map.MapID || body.X!=(Cell(s).X+1)/2 || body.Y!=(Cell(s).Y+1)/2)
        {s=Neutral(s with{Map=body.Map.MapID,Motion=DruidRavenMotion.Center(body.X*2,body.Y*2)},1);}
        while(!body.Deleted && s.NextUtc<=at)
        {
            var frameAt=s.NextUtc;s=s with{Frame=checked(s.Frame+1),NextUtc=s.NextUtc.AddMilliseconds(40)};
            if(!body.Poisoned && TimedEffects.CanRegenerateHits(body) && !SkillHealth.Full(body)){SkillHealth.Heal(body,BattleOrders.Regeneration256(body));}
            if(body.Frozen || body.Paralyzed){s=Park(s);Store(body,s);continue;}
            if(s.Mode==2)
            {
                var speed=SourceActorMotion.Speed(NecromancerSummons.Source("plaguepoppy","Velocity"),NecromancerSummons.Source("plaguepoppy-animation","WLSpeed"),100+s.MovingBonus+PaladinVigor.VelocityPercent(body)-GearEffects.MovementSlow(body),0);
                var moved=SourceActorMotion.Advance(s.Motion with{Velocity=speed.Velocity,MaximumVelocity=speed.Velocity},(x,y)=>Collision(body,x,y));s=s with{Motion=moved.State};
                if(!moved.Moving){s=Neutral(s,1);}if(Point(body,Cell(s),out var point) && body.Location!=point){body.MoveToWorld(point,body.Map);}
            }
            if(s.ActionAt==s.Frame)
            {
                var target=World.FindMobile((Serial)s.Target);var heads=target is{Deleted:false}&&target.Map==body.Map
                    ?DruidCreeperVines.Heads(prepared.Snapshot.Birth.VineAttackRank,target.X*2,target.Y*2):[];
                s=s with{ActionAt=0,SkillEvents=checked(s.SkillEvents+1),LastHeads=heads,
                    LastFieldId=DruidCreeperGround.Create(body,prepared.Snapshot.Birth.VineAttackRank,heads,frameAt)};
            }
            if(s.Mode==8 && s.EndAt==s.Frame){int next=s.NextThink>s.Frame?s.NextThink:s.Frame+15;s=Neutral(s) with{NextThink=next};}
            if(s.NextThink==s.Frame){s=Think(body,owner,s,frameAt);}Store(body,s);body.AIObject?.AITimer.Stop();
        }
    }
    internal static void Fault(DruidCreeperBody body,string message)
    {if(body.Prepared?.Controller is{} s){Store(body,Park(s) with{Fault=message});}}
}

internal static class DruidCreepers
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidCreepers));
    private static readonly HashSet<DruidCreeperBody>Bodies=new();private static long _epoch;
    internal static int GameFrame=>checked((int)Math.Max(0,(Core.TickCount-_epoch)/40));
    internal static void Track(DruidCreeperBody body)=>Bodies.Add(body);
    internal static void Forget(DruidCreeperBody body)=>Bodies.Remove(body);
    public static void Initialize(){_epoch=Core.TickCount;Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));}
    internal static void Tick(DateTime at,bool? actions=null)
    {
        foreach(var body in Bodies.ToArray())
        {
            if(body.Deleted){Forget(body);continue;}
            try{DruidCreeperWorld.Advance(body,at,actions??body.ControlMaster?.NetState is{Running:true});}
            catch(Exception e){DruidCreeperWorld.Fault(body,e.Message);Log.Error(e,"Creeper controller refused unsupported context for {Serial}",body.Serial);}
        }
        DruidCreeperGround.Tick(at,actions);
    }
}
