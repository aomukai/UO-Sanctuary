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

internal sealed record SpiritWolfWorldState(int Map,ActorMotionState Motion,ulong Seed,DateTime NextUtc,
    int Frame=0,int Mode=8,int NextThink=25,int ActionAt=0,int EndAt=47,uint Target=0,
    bool Teleport=false,Point2D TeleportPoint=default,int PathOverride=0,int VelocityBonus=0,int PathBudget=0,
    int StrikeRequests=0,uint PendingStrikeTarget=0,int PendingStrikeFrame=0,int PendingStrikeRank=0,string Fault=null,int MovingBonus=0,
    SpiritWolfHitResult LastHit=null,int Hits=0,int AttackSpeed=0,int DeferredThinks=0)
{
    public string Profile{get;init;}="source-ai-native-ground-fit-cold-hit-v2";
    public string AttackThinkPolicy{get;init;}="observed-bite-continuation-neutral-delay-v1";
}

// Live saved movement/AI and bounded cold-melee backend. This prepared factory
// is not a learned cast route; its remaining physical-floor uncertainty is explicit.
// Native fit/LOS and facet-wide room projection remain geometry adapters.
internal static class DruidSpiritWolfWorld
{
    internal static SpiritWolfWorldState Copy(SpiritWolfWorldState s)=>s with{Motion=s.Motion with{Points=[..s.Motion.Points],SavedSteps=[..s.Motion.SavedSteps]}};
    internal static SpiritWolfWorldState Upgrade(SpiritWolfWorldState s)=>s.Fault=="Spirit Wolf AI event overlaps unfinished A1; source/current busy-mode dispatch is unverified"
        ?Park(s with{Fault=null}):s;
    internal static void Validate(SpiritWolfWorldState s)
    {
        if(s?.Motion?.Points==null || s.Motion.SavedSteps==null || s.Map<0 || s.Map>=Map.Maps.Length || Map.Maps[s.Map]==null || Map.Maps[s.Map]==Map.Internal
            || s.NextUtc.Kind!=DateTimeKind.Utc || s.Frame<0 || s.Frame>int.MaxValue-100 || s.Mode is not(1 or 2 or 4 or 8 or 15)
            || s.NextThink<0 || s.NextThink!=0&&s.NextThink<=s.Frame || s.ActionAt<0 || s.ActionAt!=0&&s.ActionAt<=s.Frame
            || s.EndAt<0 || s.EndAt!=0&&s.EndAt<=s.Frame || s.PathOverride is not(0 or 1 or 7 or 13 or 15)
            || s.VelocityBonus is <-126 or >126 || s.MovingBonus is <-126 or >126 || s.PathBudget is <0 or >77 || s.StrikeRequests<0 || s.PendingStrikeRank is <0 or >255
            || s.Profile is not("source-ai-native-ground-fit-pending-hit-v1" or "source-ai-native-ground-fit-cold-hit-v2")
            || s.Hits<0 || s.Hits>s.StrikeRequests
            || s.AttackSpeed is <0 or >32767
            || s.DeferredThinks<0 || s.DeferredThinks>s.Frame || s.AttackThinkPolicy!="observed-bite-continuation-neutral-delay-v1"
            || s.LastHit is{} hit && (!Enum.IsDefined(hit.Outcome) || hit.Chance is <0 or >95 || hit.Cold256<0 || hit.Fire256<0 || hit.Lightning256<0 || hit.ColdFrames<0 || hit.LifeLost<0)
            || s.Mode==1&&(s.ActionAt!=0 || s.EndAt!=0 || s.Teleport)
            || s.Mode is 2 or 15 && ((s.Motion.Flags&0x20)==0 || s.ActionAt!=0 || s.EndAt!=0)
            || s.Mode==4&&(s.EndAt==0 || s.Target==0) || s.Teleport&&s.Mode!=8)
        {throw new InvalidDataException("Invalid saved Spirit Wolf world state");}
        var p=Cell(s);var map=Map.Maps[s.Map];
        if(p.X<=0 || p.Y<=0 || p.X>=map.Width*2 || p.Y>=map.Height*2){throw new InvalidDataException("Wolf source pose outside its world facet");}
        SourceActorMotion.Advance(s.Motion with{Flags=s.Motion.Flags&~0x20},(_,_)=>throw new InvalidOperationException());
    }
    internal static DruidSpiritWolfBody Create(PlayerMobile owner,Point3D point,DateTime at,ulong seed)
    {
        var body=DruidSpiritWolfBody.CreatePrepared(owner,point,at);
        try
        {
            var state=new SpiritWolfWorldState(owner.Map.MapID,DruidRavenMotion.Center(point.X*2,point.Y*2),seed,at.AddMilliseconds(40),EndAt:DruidWolfGeometry.Clock("S1").End);
            body.RestorePrepared(body.Prepared with{Controller=state});PetOwnerRoutes.For(owner,at);DruidSpiritWolves.Track(body);return body;
        }
        catch{body.Delete();throw;}
    }
    internal static Point2D Cell(SpiritWolfWorldState s)=>DruidRavenMotion.Cell(s.Motion);
    private static SpiritWolfWorldState Neutral(SpiritWolfWorldState s,int delay=15)=>s with{Mode=1,NextThink=checked(s.Frame+Math.Max(1,delay)),
        ActionAt=0,EndAt=0,Teleport=false,Motion=DruidWolfGeometry.Stop(s.Motion)};
    internal static SpiritWolfWorldState Park(SpiritWolfWorldState s)=>Neutral(s,1) with{PathOverride=0,VelocityBonus=0,PathBudget=0};
    private static void Store(DruidSpiritWolfBody body,SpiritWolfWorldState s)
    {body.RestorePrepared(body.Prepared with{Controller=s,NextRecoveryUtc=s.NextUtc});}
    private static bool Point(DruidSpiritWolfBody body,Point2D cell,out Point3D point)
    {
        point=new((cell.X+1)/2,(cell.Y+1)/2,body.Z);var map=body.Map;
        if(map==null || map==Map.Internal || cell.X<=0 || cell.Y<=0 || cell.X>=map.Width*2 || cell.Y>=map.Height*2){return false;}
        if(Teleportation.Space(map,point,body,true)){return true;}
        int z=map.GetAverageZ(point.X,point.Y);if(Math.Abs(z-body.Z)>=16){return false;}
        point=new(point.X,point.Y,z);return Teleportation.Space(map,point,body,true);
    }
    private static int Collision(DruidSpiritWolfBody body,int x,int y)=>Point(body,new(x,y),out _)?0:0x3c01;
    internal static bool Eligible(DruidSpiritWolfBody body,PlayerMobile owner,Mobile target)=>target is BaseCreature creature
        && target is{Alive:true,Deleted:false} && target.Map==body.Map && !target.Blessed && target.CanBeDamaged()
        && creature is not BaseVendor && CreatureProfiles.For(creature).Traits.Size is >=0 and <=3 && Math.Abs(target.Z-body.Z)<16
        && ClassCombat.IsHostile(owner,target) && !SpellHelper.IsTown(target.Location,owner) && body.CanBeHarmful(target,false);
    private static bool Visible(DruidSpiritWolfBody body,Mobile target)=>body.InLOS(target);
    private static bool Melee(DruidSpiritWolfBody body,SpiritWolfWorldState s,Mobile target)=>target is BaseCreature creature
        && DruidWolfGeometry.MeleeDistance(Cell(s),PetOwnerRoutes.Cell(target),2,CreatureProfiles.For(creature).Traits.Size.Value)<=2 && Visible(body,target);
    private static Mobile Primary(DruidSpiritWolfBody body,PlayerMobile owner,SpiritWolfWorldState s)
    {
        var prior=World.FindMobile((Serial)s.Target);
        if(Eligible(body,owner,prior) && DruidWolfGeometry.Full(Cell(s),PetOwnerRoutes.Cell(prior))<=20){return prior;}
        Mobile chosen=null;int best=21;
        foreach(var candidate in body.Map.GetMobilesInRange<BaseCreature>(body.Location,12))
        {
            if(!Eligible(body,owner,candidate)){continue;}
            int distance=DruidWolfGeometry.Full(Cell(s),PetOwnerRoutes.Cell(candidate));
            if(distance<best || distance==best && chosen!=null && candidate.Serial.Value<chosen.Serial.Value)
            {chosen=candidate;best=distance;}
        }
        return chosen;
    }
    private static SpiritWolfWorldState Relocate(DruidSpiritWolfBody body,SpiritWolfWorldState s,Point3D point,Map map)
    {
        body.MoveToWorld(point,map);body.AIObject?.AITimer.Stop();
        return s with{Map=map.MapID,Motion=DruidRavenMotion.Center(point.X*2,point.Y*2)};
    }
    private static bool Path(DruidSpiritWolfBody body,ref SpiritWolfWorldState s,Point2D target,bool run,bool deleteOnFailure=false)
    {
        if(target.X<=0 || target.Y<=0 || target.X>=body.Map.Width*2 || target.Y>=body.Map.Height*2)
        {if(deleteOnFailure){s=s with{NextThink=0};}return false;}
        int type=s.PathOverride==0?13:s.PathOverride,budget=s.PathBudget==0?5:s.PathBudget,bonus=s.VelocityBonus;
        s=s with{PathOverride=0,VelocityBonus=0,PathBudget=0};
        if(type==1){throw new NotSupportedException("Wolf follow needs unimplemented source AStar fallback");}
        var curse=NecromancerEffects.Curse(body);
        if(run && curse?.Ability==CombatAbility.Decrepify){run=false;type=13;bonus=0;budget=5;}
        int percent=100+bonus+PaladinVigor.VelocityPercent(body)-GearEffects.MovementSlow(body);
        var speed=SourceActorMotion.Speed(NecromancerSummons.Source("spiritwolf","Velocity"),NecromancerSummons.Source("spiritwolf-animation",run?"RNSpeed":"WLSpeed"),percent,0);
        int flags=type==13?0x100:type==15?0x800:0;
        var motion=s.Motion with{PathType=type,Flags=flags,Velocity=speed.Velocity,MaximumVelocity=speed.Velocity,Acceleration=0,AccelerationCounter=0,Points=[],Index=0,Distance=budget};
        var path=SourceActorPath.Initialize(new(motion,target,motion.FinalTarget),budget,new(1,1,body.Map.Width*2-1,body.Map.Height*2-1),(x,y)=>Collision(body,x,y));
        if(path.Motion.Points.Length==0 || (path.Motion.Flags&0x20)==0)
        {
            if(type is 2 or 7 or 13)
            {s=s with{PathOverride=15,PathBudget=budget,VelocityBonus=bonus};return Path(body,ref s,target,run,deleteOnFailure);}
            if(deleteOnFailure){s=s with{NextThink=0};}return false;
        }
        body.Direction=body.GetDirectionTo(new Point3D((target.X+1)/2,(target.Y+1)/2,body.Z));
        s=s with{Mode=run?15:2,NextThink=0,ActionAt=0,EndAt=0,Teleport=false,Motion=path.Motion,MovingBonus=bonus};return true;
    }
    private static void Velocity(ref SpiritWolfWorldState s,int type,int speed,int budget)
    {
        // The source setter preserves existing fields when passed zero.
        s=s with{PathOverride=type==0?s.PathOverride:type,VelocityBonus=speed==0?s.VelocityBonus:speed,PathBudget=budget==0?s.PathBudget:Math.Min(77,budget)};
    }
    private static uint Draw(ref SpiritWolfWorldState s)
    {ulong seed=s.Seed;uint result=SourceRandom.Roll(ref seed);s=s with{Seed=seed};return result;}
    private static Point2D Wander(ref SpiritWolfWorldState s,Point2D target,int distance)
    {
        bool first=(Draw(ref s)&1)!=0;int small=distance<=0?0:(int)(Draw(ref s)%(uint)distance);
        int x=first?distance:small,y=first?small:distance;
        if((Draw(ref s)&1)!=0){x=-x;}if((Draw(ref s)&1)!=0){y=-y;}
        return new(target.X+x,target.Y+y);
    }
    private static int Room(DruidSpiritWolfBody body,Point2D p)=>p.X>0&&p.Y>0&&p.X<body.Map.Width*2&&p.Y<body.Map.Height*2?body.Map.MapID+1:0;
    private static SpiritWolfWorldState Think(DruidSpiritWolfBody body,PlayerMobile owner,SpiritWolfWorldState initial,DateTime at)
    {
        var s=initial with{NextThink=0};var primary=Primary(body,owner,s);Mobile potential=owner.Combatant;
        if(!Eligible(body,owner,potential)){potential=World.FindMobile(ClassCombat.For(owner).Target);}
        if(!Eligible(body,owner,potential)){potential=null;}
        var route=PetOwnerRoutes.For(owner,at);var ownerCell=PetOwnerRoutes.Cell(owner);
        bool ownerMoving=ClassMovement.IsMoving(owner,Core.TickCount);int mode=ownerMoving?(ClassMovement.IsRunning(owner,Core.TickCount)?3:2):1;
        int heading=DruidWolfGeometry.NativeHeading(owner.Direction);var offset=DruidWolfGeometry.Offset(heading);
        var end=ownerMoving?new Point2D(ownerCell.X+offset.X*2,ownerCell.Y+offset.Y*2):ownerCell;
        PetMoveContext Context()=>new(Cell(s),ownerCell,end,end,mode,DruidWolfGeometry.Full(Cell(s),ownerCell),
            (owner.AllFollowers??[]).OfType<BaseCreature>().Count(x=>x is{Alive:true,Deleted:false}&&PersistentMinions.Active(owner,x)),
            ownerMoving?heading:0,route.Cursor,route.Trail,route.LastTeleport);
        int Action(PetMoveRequest request)
        {
            var point=new Point2D(request.A,request.B);
            switch(request.Kind)
            {
                case PetMoveKind.Velocity:Velocity(ref s,request.A==1?7:request.A,request.B,request.C);return 0;
                case PetMoveKind.VelocityParameters:Velocity(ref s,request.A,request.B,request.C);return 0;
                case PetMoveKind.WalkDelete:return Path(body,ref s,point,false,true)?1:0;
                case PetMoveKind.RunDelete:return Path(body,ref s,point,true,true)?1:0;
                case PetMoveKind.Walk:return Path(body,ref s,point,false)?1:0;
                case PetMoveKind.Run:return Path(body,ref s,point,true)?1:0;
                case PetMoveKind.Wander:return Path(body,ref s,Wander(ref s,ownerCell,request.A),false)?1:0;
                case PetMoveKind.DeleteThink:s=s with{NextThink=0};return 0;
                case PetMoveKind.Idle:s=Neutral(s,request.A);return 0;
                case PetMoveKind.MoveUnit:
                    if(Point(body,point,out var native)){s=Relocate(body,s,native,body.Map);return 1;}return 0;
                case PetMoveKind.Refresh:case PetMoveKind.MarkTeleported:return 0;
                case PetMoveKind.CountOthers:
                    return (owner.AllFollowers??[]).OfType<BaseCreature>().Count(x=>x!=body&&x is{Alive:true,Deleted:false}&&x.Map==body.Map&&DruidWolfGeometry.Full(Cell(s),PetOwnerRoutes.Cell(x))<=1);
                case PetMoveKind.Escape:
                    if(request.A>5){Velocity(ref s,0,0,request.A);}var cell=Cell(s);
                    return Path(body,ref s,new(cell.X+request.A*Math.Sign(cell.X-ownerCell.X),cell.Y+request.A*Math.Sign(cell.Y-ownerCell.Y)),false,request.B!=0)?1:0;
                case PetMoveKind.WalkOwner:return Path(body,ref s,Wander(ref s,ownerCell,request.A),false)?1:0;
                default:throw new NotSupportedException("Unsupported wolf movement tactic");
            }
        }
        uint Random()=>Draw(ref s);int Percent()=>(int)(Draw(ref s)%100);
        var services=new PetMoveServices(Action,p=>Room(body,p),p=>DruidWolfGeometry.NoSize(Cell(s),p),DruidWolfGeometry.Offset,Random,Percent,
            ()=>Point(body,ownerCell,out _)?ownerCell:null,p=>Room(body,p)!=0);
        bool Common(int target,bool combat,int flag,int leash)=>SourcePetMovement.Common(Context(),services,target!=0,SpellHelper.IsTown(body.Location,owner),combat,flag,leash);
        bool Move(int kind,bool run,int speed,int leash)=>SourcePetMovement.Move(Context(),services,kind,run,speed,leash);
        void Request(SpiritWolfAiRequest request)
        {
            switch(request.Kind)
            {
                case 0:s=Neutral(s,request.A);break;
                case 2:
                    var clock=DruidWolfGeometry.Clock("S1");s=s with{Mode=8,NextThink=0,ActionAt=0,EndAt=s.Frame+clock.End,Teleport=true,
                        TeleportPoint=new(request.C,request.D),Motion=DruidWolfGeometry.Stop(s.Motion)};break;
                case 3:s=s with{ActionAt=s.Frame+request.A};break;
                case 4:s=s with{NextThink=s.Frame+Math.Max(1,request.A)};break;
                case 6:
                    var enemy=World.FindMobile((Serial)(uint)request.B);if(enemy!=null){body.Direction=body.GetDirectionTo(enemy);}
                    int attackSpeed=SourceAttackRate.WolfSpeed(body);var attack=DruidWolfGeometry.Clock("A1",attackSpeed);
                    s=s with{Mode=4,ActionAt=attack.Action==0?0:s.Frame+attack.Action,EndAt=s.Frame+attack.End,NextThink=0,AttackSpeed=attackSpeed,
                        Target=(uint)request.B,Teleport=false,Motion=DruidWolfGeometry.Stop(s.Motion)};break;
                case 7:
                    var cell=Cell(s);int distance=DruidWolfGeometry.Full(cell,ownerCell),sign=Math.Sign(distance-request.B),amount=Math.Min(Math.Abs(distance-request.B),request.A);
                    int dx=Math.Abs(ownerCell.X-cell.X),dy=Math.Abs(ownerCell.Y-cell.Y),sum=Math.Max(dx+dy,amount),x=0,y=0;
                    if(sum>0){x=amount*dx/sum;y=amount*dy/sum;while(x+y<amount){x++;y++;}}
                    Path(body,ref s,new(cell.X+sign*x*Math.Sign(ownerCell.X-cell.X),cell.Y+sign*y*Math.Sign(ownerCell.Y-cell.Y)),false);break;
                case 8:Velocity(ref s,request.A,request.B,request.C);break;
                case 9:
                    var target=World.FindMobile((Serial)(uint)request.A);if(target!=null){s=s with{Target=target.Serial.Value};Path(body,ref s,PetOwnerRoutes.Cell(target),true);}break;
                case 10:Path(body,ref s,Wander(ref s,Cell(s),request.A),false);break;
                default:throw new NotSupportedException("Unsupported wolf AI request");
            }
        }
        var context=new SpiritWolfAiContext(true,0,true,(int)owner.Serial.Value,ownerCell.X,ownerCell.Y,SpellHelper.IsTown(body.Location,owner),
            primary==null?0:(int)primary.Serial.Value,potential==null?0:(int)potential.Serial.Value,primary!=null&&Visible(body,primary),potential!=null&&Visible(body,potential),
            potential==null?0:DruidWolfGeometry.Full(Cell(s),PetOwnerRoutes.Cell(potential)),DruidWolfGeometry.UnitDistance(ownerCell,Cell(s),2,2),mode,true,
            primary!=null&&Melee(body,s,primary),body.SummonDifficulty,primary==null?0:DruidWolfGeometry.UnitDistance(ownerCell,PetOwnerRoutes.Cell(primary),2,CreatureProfiles.For((BaseCreature)primary).Traits.Size.Value),
            NecromancerSummons.Source("spiritwolf","Velocity"),NecromancerSummons.Source("spiritwolf","Run"));
        try{DruidSpiritWolfAi.CorrectedThink(context,new(Request,Common,Move,Percent));}
        catch(Exception e){Store(body,Park(s) with{Fault=e.Message});throw;}
        if(s.NextThink==0 && s.Mode is not(2 or 15 or 4 or 8)){s=s with{NextThink=s.Frame+15};}
        return s;
    }
    internal static void Advance(DruidSpiritWolfBody body,DateTime at,bool actions)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Wolf world clock must be UTC");}
        if(body.Deleted || body.Prepared is not{Controller:{} s} prepared || s.Fault!=null || s.NextUtc>at){return;}
        if(body.ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner || owner.Serial.Value!=prepared.Owner){body.Delete();return;}
        if(body.ControllerLoaded)
        {
            body.ControllerLoaded=false;long skipped=(at-s.NextUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;
            Store(body,Park(s with{Frame=checked(s.Frame+(int)skipped),NextUtc=s.NextUtc.AddMilliseconds(skipped*40)}));return;
        }
        if(!PersistentMinions.Active(owner,body) || !body.Permitted(owner) || !actions || owner.Map==null || owner.Map==Map.Internal)
        {
            if(!PersistentMinions.Active(owner,body)||!body.Permitted(owner)){PersistentMinions.ParkClass(owner,DiabloClass.Druid);}else{PersistentMinions.Suspend(owner);}
            s=body.Prepared.Controller;long count=(at-s.NextUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;
            s=Park(s with{Frame=checked(s.Frame+(int)count),NextUtc=s.NextUtc.AddMilliseconds(count*40)});Store(body,s);return;
        }
        if(body.Map==Map.Internal){PersistentMinions.Resume(owner);if(body.Map==Map.Internal){return;}}
        if(body.Map!=owner.Map)
        {
            if(!Teleportation.Space(owner.Map,owner.Location,body,true)){return;}
            s=Neutral(Relocate(body,s,owner.Location,owner.Map),1);
        }
        if(s.Map!=body.Map.MapID || body.X!=(Cell(s).X+1)/2 || body.Y!=(Cell(s).Y+1)/2)
        {s=Neutral(s with{Map=body.Map.MapID,Motion=DruidRavenMotion.Center(body.X*2,body.Y*2)},1);}
        while(!body.Deleted && s.NextUtc<=at)
        {
            var frameAt=s.NextUtc;s=s with{Frame=checked(s.Frame+1),NextUtc=s.NextUtc.AddMilliseconds(40)};
            if(body.Poisoned==false && TimedEffects.CanRegenerateHits(body) && !SkillHealth.Full(body))
            {SkillHealth.Heal(body,BattleOrders.Regeneration256(body));}
            if(body.Frozen || body.Paralyzed || CreatureHitRecovery.Remaining(body)>0)
            {s=Park(s);Store(body,s);continue;}
            if(s.Mode is 2 or 15)
            {
                var speed=SourceActorMotion.Speed(NecromancerSummons.Source("spiritwolf","Velocity"),NecromancerSummons.Source("spiritwolf-animation",s.Mode==15?"RNSpeed":"WLSpeed"),
                    100+s.MovingBonus+PaladinVigor.VelocityPercent(body)-GearEffects.MovementSlow(body),0);
                s=s with{Motion=s.Motion with{Velocity=speed.Velocity,MaximumVelocity=speed.Velocity}};
                var moved=SourceActorMotion.Advance(s.Motion,(x,y)=>Collision(body,x,y));s=s with{Motion=moved.State};
                if(!moved.Moving){s=Neutral(s,1);}
                if(Point(body,Cell(s),out var position) && body.Location!=position){body.MoveToWorld(position,body.Map);}
            }
            if(s.ActionAt==s.Frame)
            {
                if(s.Mode==8 && s.Teleport)
                {if(Point(body,s.TeleportPoint,out var destination)){s=Relocate(body,s,destination,body.Map);}s=s with{ActionAt=0,Teleport=false};}
                else if(s.Mode==4)
                {
                    ulong seed=s.Seed;
                    var hit=DruidSpiritWolfHit.Resolve(body,owner,s,World.FindMobile((Serial)s.Target) as BaseCreature,ref seed);
                    if(body.Deleted){return;}
                    s=s with{ActionAt=0,StrikeRequests=checked(s.StrikeRequests+1),PendingStrikeTarget=s.Target,PendingStrikeFrame=s.Frame,
                        PendingStrikeRank=SkillMechanics.Rank(owner,CombatAbility.SummonSpiritWolf),Seed=seed,LastHit=hit,Hits=checked(s.Hits+(hit.Outcome==SpiritWolfHitOutcome.Hit?1:0))};
                }
            }
            if(s.Mode is 4 or 8 && s.EndAt==s.Frame)
            {int next=s.NextThink>s.Frame?s.NextThink:s.Frame+15;s=Neutral(s) with{NextThink=next};}
            if(s.NextThink==s.Frame)
            {
                if(s.Mode==4 && s.EndAt>s.Frame)
                {
                    // Observed3.3.93847wolves keep biting under Decrepify
                    // without Fanaticism. Consume this busy decision without
                    // restarting A1; mode-end's existing neutral callback owns
                    // the next think. Exact modern cadence remains provisional.
                    s=s with{NextThink=0,DeferredThinks=checked(s.DeferredThinks+1)};
                }
                else{s=Think(body,owner,s,frameAt);}
            }
            Store(body,s);body.AIObject?.AITimer.Stop();
        }
    }
    internal static void Fault(DruidSpiritWolfBody body,string reason)
    {if(body.Prepared?.Controller is{} s){Store(body,Park(s) with{Fault=reason});}}
}

internal static class DruidSpiritWolves
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidSpiritWolves));
    private static readonly HashSet<DruidSpiritWolfBody> Bodies=new();
    internal static void Track(DruidSpiritWolfBody body)=>Bodies.Add(body);
    internal static void Forget(DruidSpiritWolfBody body)=>Bodies.Remove(body);
    internal static DruidSpiritWolfBody[] For(PlayerMobile p)=>(p.AllFollowers??[]).OfType<DruidSpiritWolfBody>().Where(x=>x is{Alive:true,Deleted:false}&&x.ControlMaster==p).ToArray();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static void Tick(DateTime at,bool? actions=null)
    {
        var active=new List<DruidSpiritWolfBody>();
        foreach(var body in Bodies.ToArray())
        {
            if(body.Deleted){Forget(body);continue;}
            try
            {
                bool allowed=actions??body.ControlMaster?.NetState is{Running:true};
                if(allowed){active.Add(body);}else{DruidSpiritWolfWorld.Advance(body,at,false);}
            }
            catch(Exception e){DruidSpiritWolfWorld.Fault(body,e.Message);Log.Error(e,"Spirit Wolf world controller refused unsupported context for {Serial}",body.Serial);}
        }
        // Consume due actors in shared frame order so one pet cannot expire
        // the owner's teleport marker before another pet's earlier frame.
        var stalled=new HashSet<DruidSpiritWolfBody>();
        while(true)
        {
            var due=active.Where(b=>!b.Deleted&&!stalled.Contains(b)&&b.Prepared.Controller is{Fault:null} c&&c.NextUtc<=at)
                .OrderBy(b=>b.Prepared.Controller.NextUtc).ThenBy(b=>b.Serial.Value).FirstOrDefault();
            if(due==null){break;}var before=due.Prepared.Controller.NextUtc;
            try{DruidSpiritWolfWorld.Advance(due,before,true);if(!due.Deleted&&due.Prepared.Controller.NextUtc==before){stalled.Add(due);}}
            catch(Exception e){DruidSpiritWolfWorld.Fault(due,e.Message);Log.Error(e,"Spirit Wolf world controller refused unsupported context for {Serial}",due.Serial);}
        }
    }
}
