// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record OakWorldState(int Map,ActorMotionState Motion,ulong Seed,DateTime NextUtc,
    int Frame=0,int Mode=1,int NextThink=15,uint Target=0,int PathOverride=0,int PathBudget=0,
    int VelocityBonus=0,int MovingBonus=0,int Thinks=0,int EscapeAttempts=0,int Teleports=0,string Fault=null)
{
    public string Profile{get;init;}="source-totem-native-ground-v1";
    public string EntryTiming{get;init;}="current-aidel-first-think-provisional-v1";
}

// Saved Totem decisions use the existing source pet movement/path/motion
// kernels. Native room, threat scan, free-position search and first-think
// projections remain identified adapters, not a modern executable certificate.
internal static class DruidOakWorld
{
    internal static OakWorldState Copy(OakWorldState s)=>s with{Motion=s.Motion with{Points=[..s.Motion.Points],SavedSteps=[..s.Motion.SavedSteps]}};
    internal static Point2D Cell(OakWorldState s)=>DruidRavenMotion.Cell(s.Motion);
    internal static void Validate(OakWorldState s)
    {
        if(s?.Motion?.Points==null || s.Motion.SavedSteps==null || s.Map<0 || s.Map>=Map.Maps.Length
            || Map.Maps[s.Map]==null || Map.Maps[s.Map]==Map.Internal || s.Frame<0 || s.Frame>int.MaxValue-100
            || s.NextUtc.Kind!=DateTimeKind.Utc || s.NextUtc>DateTime.MaxValue.AddMilliseconds(-40)
            || s.Mode is not(1 or 2) || s.NextThink<0 || s.NextThink!=0&&s.NextThink<=s.Frame
            || s.Mode==2&&(s.Motion.Flags&0x20)==0 || s.PathOverride is not(0 or 1 or 7 or 13 or 15)
            || s.PathBudget is <0 or >77 || s.VelocityBonus is <-126 or >126 || s.MovingBonus is <-126 or >126
            || s.Thinks<0 || s.Thinks>s.Frame || s.EscapeAttempts<0 || s.EscapeAttempts>s.Thinks
            || s.Teleports<0 || s.Teleports>s.Thinks || s.Profile!="source-totem-native-ground-v1"
            || s.EntryTiming!="current-aidel-first-think-provisional-v1")
        {throw new InvalidDataException("Invalid saved Oak world state");}
        var p=Cell(s);var map=Map.Maps[s.Map];
        if(p.X<=0 || p.Y<=0 || p.X>=map.Width*2 || p.Y>=map.Height*2){throw new InvalidDataException("Oak pose outside facet");}
        SourceActorMotion.Advance(s.Motion with{Flags=s.Motion.Flags&~0x20},(_,_)=>throw new InvalidOperationException());
    }
    internal static DruidOakBody Create(PlayerMobile owner,Point3D point,DateTime at,ulong seed,CombatAbility ability=CombatAbility.OakSage)
    {
        var body=DruidOakBody.CreatePrepared(owner,point,at,ability);
        try
        {
            int first=NecromancerSummons.Source(body.SourceMonsterId,"aidel",body.SummonDifficulty);
            if(first!=15){throw new NotSupportedException("Unsupported Oak entry-delay table");}
            var state=new OakWorldState(owner.Map.MapID,DruidRavenMotion.Center(point.X*2,point.Y*2),seed,at.AddMilliseconds(40),NextThink:first);
            body.RestorePrepared(body.Prepared with{Controller=state,Profile="source-birth-and-totem-motion-v2"});PetOwnerRoutes.For(owner,at);return body;
        }
        catch{body.Delete();throw;}
    }
    private static OakWorldState Neutral(OakWorldState s,int delay=15)=>s with{Mode=1,NextThink=checked(s.Frame+Math.Max(1,delay)),Motion=DruidWolfGeometry.Stop(s.Motion)};
    internal static OakWorldState Park(OakWorldState s)=>Neutral(s,1) with{PathOverride=0,PathBudget=0,VelocityBonus=0};
    private static void Store(DruidOakBody body,OakWorldState s)=>body.RestorePrepared(body.Prepared with{Controller=s,NextRecoveryUtc=s.NextUtc});
    private static bool Point(DruidOakBody body,Point2D cell,out Point3D p)=>PetGroundWorld.Point(body,cell,out p);
    private static int Collision(DruidOakBody body,int x,int y)=>PetGroundWorld.Collision(body,x,y);
    private static OakWorldState Relocate(DruidOakBody body,OakWorldState s,Point3D point,Map map)
    {body.MoveToWorld(point,map);body.AIObject?.AITimer.Stop();return s with{Map=map.MapID,Motion=DruidRavenMotion.Center(point.X*2,point.Y*2)};}
    private static bool Eligible(DruidOakBody body,PlayerMobile owner,Mobile target)=>target is BaseCreature c
        && c is{Alive:true,Deleted:false} && c.Map==body.Map && !c.Blessed && c.CanBeDamaged() && c is not BaseVendor
        && CreatureProfiles.For(c).Traits.Size is >=0 and <=3 && Math.Abs(c.Z-body.Z)<16 && ClassCombat.IsHostile(owner,c)
        && !SpellHelper.IsTown(c.Location,owner) && body.CanBeHarmful(c,false) && body.InLOS(c)
        && (c is EncounterCreature || SourceAuraTargets.Rule(CreatureProfiles.Definition(c).Reference) is{Attackable:true});
    private static Mobile Target(DruidOakBody body,PlayerMobile owner,OakWorldState s)
    {
        var prior=World.FindMobile((Serial)s.Target);
        if(Eligible(body,owner,prior)&&DruidWolfGeometry.Full(Cell(s),PetOwnerRoutes.Cell(prior))<=24){return prior;}
        Mobile result=null;int best=25;
        foreach(var candidate in body.Map.GetMobilesInRange<BaseCreature>(body.Location,15))
        {
            if(!Eligible(body,owner,candidate)){continue;}int distance=DruidWolfGeometry.Full(Cell(s),PetOwnerRoutes.Cell(candidate));
            if(distance<best || distance==best&&result!=null&&candidate.Serial.Value<result.Serial.Value){result=candidate;best=distance;}
        }
        return result;
    }
    private static uint Draw(ref OakWorldState s)
    {ulong seed=s.Seed;uint value=SourceRandom.Roll(ref seed);s=s with{Seed=seed};return value;}
    private static void Velocity(ref OakWorldState s,int type,int speed,int budget)
    {s=s with{PathOverride=type==0?s.PathOverride:type,VelocityBonus=speed==0?s.VelocityBonus:speed,PathBudget=budget==0?s.PathBudget:Math.Min(77,budget)};}
    private static Point2D Wander(ref OakWorldState s,Point2D center,int distance)
    {
        bool first=(Draw(ref s)&1)!=0;int small=distance<=0?0:(int)(Draw(ref s)%(uint)distance);
        int x=first?distance:small,y=first?small:distance;if((Draw(ref s)&1)!=0){x=-x;}if((Draw(ref s)&1)!=0){y=-y;}
        return new(center.X+x,center.Y+y);
    }
    private static bool Path(DruidOakBody body,ref OakWorldState s,Point2D target,bool delete=false)
    {
        if(target.X<=0 || target.Y<=0 || target.X>=body.Map.Width*2 || target.Y>=body.Map.Height*2){return false;}
        int type=s.PathOverride==0?13:s.PathOverride,budget=s.PathBudget==0?5:s.PathBudget,bonus=s.VelocityBonus;
        s=s with{PathOverride=0,PathBudget=0,VelocityBonus=0};
        if(type==1){throw new NotSupportedException("Oak source AStar fallback remains unimplemented");}
        var speed=Speed(body,bonus);
        var motion=s.Motion with{PathType=type,Flags=type==13?0x100:type==15?0x800:0,Velocity=speed,MaximumVelocity=speed,
            Acceleration=0,AccelerationCounter=0,Points=[],Index=0,Distance=budget};
        var path=SourceActorPath.Initialize(new(motion,target,motion.FinalTarget),budget,new(1,1,body.Map.Width*2-1,body.Map.Height*2-1),(x,y)=>Collision(body,x,y));
        if(path.Motion.Points.Length==0 || (path.Motion.Flags&0x20)==0)
        {
            if(type is 7 or 13){s=s with{PathOverride=15,PathBudget=budget,VelocityBonus=bonus};return Path(body,ref s,target,delete);}
            if(delete){s=s with{NextThink=0};}return false;
        }
        body.Direction=body.GetDirectionTo(new Point3D((target.X+1)/2,(target.Y+1)/2,body.Z));
        s=s with{Mode=2,NextThink=0,Motion=path.Motion,MovingBonus=bonus};return true;
    }
    private static int Speed(DruidOakBody body,int bonus)=>SourceActorMotion.Speed(NecromancerSummons.Source(body.SourceMonsterId,"Velocity"),
        NecromancerSummons.Source(body.SourceMonsterId+"-animation","WLSpeed"),100+bonus+PaladinVigor.VelocityPercent(body)-GearEffects.MovementSlow(body),0).Velocity;
    private static bool Warp(DruidOakBody body,PlayerMobile owner,ref OakWorldState s)
    {
        // Shared native free-coordinate projection. Source requests size2and
        // mask0x1C09; native tile/height/ally occupancy and ring/LOS search are
        // adapters, not the original collision room implementation.
        var point=owner.Location;
        if(!Teleportation.Space(owner.Map,point,body,true)&&!PersistentMinions.FindPosition(owner,out point)){return false;}
        s=Relocate(body,s,point,owner.Map) with{Teleports=checked(s.Teleports+1)};return true;
    }
    private static OakWorldState Think(DruidOakBody body,PlayerMobile owner,OakWorldState original,DateTime at)
    {
        var s=original with{NextThink=0,Thinks=checked(original.Thinks+1)};var ownerCell=PetOwnerRoutes.Cell(owner);var route=PetOwnerRoutes.For(owner,at);
        bool moving=ClassMovement.IsMoving(owner,Core.TickCount);int mode=moving?(ClassMovement.IsRunning(owner,Core.TickCount)?3:2):1;
        int heading=DruidWolfGeometry.NativeHeading(owner.Direction);var delta=DruidWolfGeometry.Offset(heading);
        var end=moving?new Point2D(ownerCell.X+delta.X*2,ownerCell.Y+delta.Y*2):ownerCell;
        PetMoveContext Context()=>new(Cell(s),ownerCell,end,end,mode,DruidWolfGeometry.Full(Cell(s),ownerCell),
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
                case PetMoveKind.Wander:case PetMoveKind.WalkOwner:return Path(body,ref s,Wander(ref s,ownerCell,r.A))?1:0;
                case PetMoveKind.DeleteThink:s=s with{NextThink=0};return 0;
                case PetMoveKind.Idle:s=Neutral(s,r.A);return 0;
                case PetMoveKind.MoveUnit:if(Point(body,p,out var point)){s=Relocate(body,s,point,body.Map);return 1;}return 0;
                case PetMoveKind.Refresh:case PetMoveKind.MarkTeleported:return 0;
                case PetMoveKind.CountOthers:return (owner.AllFollowers??[]).OfType<BaseCreature>().Count(pet=>pet!=body&&pet is{Alive:true,Deleted:false}&&pet.Map==body.Map&&DruidWolfGeometry.Full(Cell(s),PetOwnerRoutes.Cell(pet))<=1);
                case PetMoveKind.Escape:
                    if(r.A>5){Velocity(ref s,0,0,r.A);}var cell=Cell(s);
                    return Path(body,ref s,new(cell.X+r.A*Math.Sign(cell.X-ownerCell.X),cell.Y+r.A*Math.Sign(cell.Y-ownerCell.Y)),r.B!=0)?1:0;
                default:throw new NotSupportedException("Unsupported Oak movement tactic");
            }
        }
        var services=new PetMoveServices(Action,p=>PetGroundWorld.Room(body,p),p=>DruidWolfGeometry.NoSize(Cell(s),p),DruidWolfGeometry.Offset,
            ()=>Draw(ref s),()=>(int)(Draw(ref s)%100),()=>Point(body,ownerCell,out _)?ownerCell:null,p=>PetGroundWorld.Room(body,p)!=0);
        TotemTarget Find(int range)
        {
            if(range!=24){throw new NotSupportedException("Unsupported Oak scan radius");}
            var target=Target(body,owner,s);s=s with{Target=target?.Serial.Value??0};
            return new((int)(target?.Serial.Value??0),target is BaseCreature c&&DruidWolfGeometry.MeleeDistance(Cell(s),PetOwnerRoutes.Cell(c),2,CreatureProfiles.For(c).Traits.Size.Value)<=2);
        }
        bool Escape(int id,int distance,bool delete)
        {
            var target=World.FindMobile((Serial)(uint)id);if(target==null){return false;}
            s=s with{EscapeAttempts=checked(s.EscapeAttempts+1)};
            var here=Cell(s);var enemy=PetOwnerRoutes.Cell(target);if(distance>5){Velocity(ref s,0,0,distance);}
            return Path(body,ref s,new(here.X+distance*Math.Sign(here.X-enemy.X),here.Y+distance*Math.Sign(here.Y-enemy.Y)),delete);
        }
        try
        {
            DruidTotemAi.Think(new(true,mode,body.SummonDifficulty,body.SourceMonsterId),new(delay=>s=Neutral(s,delay),Find,_=>(int)(Draw(ref s)%100),Escape,
                ()=>DruidWolfGeometry.UnitDistance(Cell(s),ownerCell,2,2),()=>Warp(body,owner,ref s),
                (kind,run,speed,leash)=>SourcePetMovement.Move(Context(),services,kind,run,speed,leash),
                (target,combat,flag,leash)=>SourcePetMovement.Common(Context(),services,target!=0,SpellHelper.IsTown(body.Location,owner),combat,flag,leash)));
        }
        catch(Exception e){Store(body,Park(s) with{Fault=e.Message});throw;}
        if(s.NextThink==0&&s.Mode!=2){s=s with{NextThink=s.Frame+15};}return s;
    }
    internal static void Advance(DruidOakBody body,DateTime at,bool actions)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Oak world clock must be UTC");}
        if(body.Deleted || body.Prepared is not{Controller:{} s} prepared || s.NextUtc>at){return;}
        if(body.ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner || owner.Serial.Value!=prepared.Owner){body.Delete();return;}
        if(!actions || !PersistentMinions.Active(owner,body) || !body.Permitted(owner) || owner.Map==null || owner.Map==Map.Internal)
        {
            if(!PersistentMinions.Active(owner,body)||!body.Permitted(owner)){PersistentMinions.ParkClass(owner,DiabloClass.Druid);}else{PersistentMinions.Suspend(owner);}
            s=body.Prepared.Controller;long frames=(at-s.NextUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;
            Store(body,Park(s with{Frame=checked(s.Frame+(int)frames),NextUtc=s.NextUtc.AddMilliseconds(frames*40)}));return;
        }
        if(body.Map==Map.Internal){PersistentMinions.Resume(owner);if(body.Map==Map.Internal){return;}}
        if(body.Map!=owner.Map)
        {
            if(!PersistentMinions.FindPosition(owner,out var point)){Store(body,Park(s));return;}
            s=Neutral(Relocate(body,s,point,owner.Map),1);
        }
        if(s.Map!=body.Map.MapID || body.X!=(Cell(s).X+1)/2 || body.Y!=(Cell(s).Y+1)/2)
        {s=Neutral(s with{Map=body.Map.MapID,Motion=DruidRavenMotion.Center(body.X*2,body.Y*2)},1);}
        while(!body.Deleted&&s.NextUtc<=at)
        {
            var frameAt=s.NextUtc;s=s with{Frame=checked(s.Frame+1),NextUtc=s.NextUtc.AddMilliseconds(40)};
            if(!body.Poisoned&&TimedEffects.CanRegenerateHits(body)&&!SkillHealth.Full(body)){SkillHealth.Heal(body,BattleOrders.Regeneration256(body));}
            if(body.Frozen||body.Paralyzed){s=Park(s);Store(body,s);continue;}
            if(s.Fault==null)
            {
                if(s.Mode==2)
                {
                    int speed=Speed(body,s.MovingBonus);var moved=SourceActorMotion.Advance(s.Motion with{Velocity=speed,MaximumVelocity=speed},(x,y)=>Collision(body,x,y));s=s with{Motion=moved.State};
                    if(!moved.Moving){s=Neutral(s,1);}if(Point(body,Cell(s),out var point)&&body.Location!=point){body.MoveToWorld(point,body.Map);}
                }
                if(s.NextThink==s.Frame){s=Think(body,owner,s,frameAt);}
            }
            Store(body,s);body.AIObject?.AITimer.Stop();
        }
        // Keep the existing no-lag-pulse-replay aura clock separate from motion.
        body.AdvanceAura(at);
    }
    internal static void Fault(DruidOakBody body,string message)
    {if(body.Prepared?.Controller is{} s){Store(body,Park(s) with{Fault=message});}}
}
