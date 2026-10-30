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

internal sealed record IronGolemWorldState(int Map,ActorMotionState Motion,DateTime NextUtc,int Frame=0,int Mode=8,
    int NextThink=25,int ActionAt=0,int EndAt=0,uint Target=0,int PathOverride=0,int VelocityBonus=0,int PathBudget=0,
    int MovingBonus=0,NecromancerPetAiState Ai=default,int AttackSpeed=0,int Attempts=0,int Hits=0,string Fault=null)
{
    public string Profile{get;init;}="prepared-necropet-ground-fit-equipment-clock-v1";
}

// Prepared world consumer. Ground fit/LOS, target ordering and neutral/busy
// cadence are native adapters, not current-executable certificates. Learned
// casting remains disabled pending complete attack/event/lifecycle acceptance.
internal static class IronGolemWorld
{
    internal static Point2D Cell(IronGolemWorldState s)=>DruidRavenMotion.Cell(s.Motion);
    internal static void Validate(IronGolemWorldState s)
    {
        if(s?.Motion?.Points==null||s.Motion.SavedSteps==null||s.Map<0||s.Map>=Map.Maps.Length||Map.Maps[s.Map]==null||Map.Maps[s.Map]==Map.Internal
            ||s.NextUtc.Kind!=DateTimeKind.Utc||s.Frame<0||s.Frame>int.MaxValue-100||s.Mode is not(1 or 2 or 4 or 8)
            ||s.NextThink<0||s.NextThink!=0&&s.NextThink<=s.Frame||s.ActionAt<0||s.ActionAt!=0&&s.ActionAt<=s.Frame
            ||s.EndAt<0||s.EndAt!=0&&s.EndAt<=s.Frame||s.PathOverride is not(0 or 1 or 7 or 13 or 15)
            ||s.VelocityBonus is <-126 or >126||s.MovingBonus is <-126 or >126||s.PathBudget is <0 or >77
            ||s.Attempts<0||s.Hits<0||s.Hits>s.Attempts||s.AttackSpeed is <0 or >32767||s.Ai.Parameter0!=0
            ||s.Profile!="prepared-necropet-ground-fit-equipment-clock-v1"
            ||s.Mode==1&&(s.ActionAt!=0||s.EndAt!=0)||s.Mode==2&&((s.Motion.Flags&0x20)==0||s.ActionAt!=0||s.EndAt!=0)
            ||s.Mode==4&&(s.Target==0||s.EndAt==0)||s.Mode==8&&(s.ActionAt!=0||s.EndAt==0))
        {throw new InvalidDataException("Invalid prepared Iron Golem world state");}
        var point=Cell(s);var map=Map.Maps[s.Map];
        if(point.X<=0||point.Y<=0||point.X>=map.Width*2||point.Y>=map.Height*2){throw new InvalidDataException("Iron Golem pose outside facet");}
        SourceActorMotion.Advance(s.Motion with{Flags=s.Motion.Flags&~0x20},(_,_)=>throw new InvalidOperationException());
    }
    internal static IronGolemBody Create(PlayerMobile owner,Item original,Point3D point,DateTime at,ulong seed)
    {
        if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-40)){throw new ArgumentException("Iron Golem needs UTC world time");}
        var body=IronGolemBody.CreatePrepared(owner,original,point,b=>
        {
            NpcEquipmentRandom.Set(b,seed);
            b.RestoreController(new(owner.Map.MapID,DruidRavenMotion.Center(point.X*2,point.Y*2),at.AddMilliseconds(40),
                EndAt:DruidWolfGeometry.Clock("S1",source:"irongolem-animation").End));
            PetOwnerRoutes.For(owner,at);
        });
        IronGolems.Track(body);return body;
    }
    private static IronGolemWorldState Neutral(IronGolemWorldState s,int delay=15)=>s with{Mode=1,NextThink=checked(s.Frame+Math.Max(1,delay)),
        ActionAt=0,EndAt=0,Motion=DruidWolfGeometry.Stop(s.Motion)};
    internal static IronGolemWorldState Park(IronGolemWorldState s)=>Neutral(s,1) with{PathOverride=0,VelocityBonus=0,PathBudget=0};
    internal static bool Eligible(IronGolemBody body,PlayerMobile owner,Mobile target)=>target is BaseCreature creature
        &&target is{Alive:true,Deleted:false}&&target.Map==body.Map&&!target.Blessed&&target.CanBeDamaged()&&creature is not BaseVendor
        &&CreatureProfiles.For(creature).Traits.Size is >=0 and <=3&&Math.Abs(target.Z-body.Z)<16&&ClassCombat.IsHostile(owner,target)
        &&!SpellHelper.IsTown(target.Location,owner)&&body.CanBeHarmful(target,false);
    private static int Size(Mobile target)=>target is BaseCreature creature?CreatureProfiles.For(creature).Traits.Size??throw new NotSupportedException("Unmapped target footprint"):2;
    private static int Full(IronGolemWorldState s,Mobile target)=>DruidWolfGeometry.Full(Cell(s),PetOwnerRoutes.Cell(target),1+Size(target)/2);
    internal static bool Melee(IronGolemBody body,IronGolemWorldState s,Mobile target,int bonus)=>target is BaseCreature
        &&SourceMeleeGeometry.Within(new(Cell(s),2,1,291,0),new(PetOwnerRoutes.Cell(target),Size(target),1,0),bonus,(_,_,_)=>!body.InLOS(target));
    private static Mobile Primary(IronGolemBody body,PlayerMobile owner,IronGolemWorldState s)
    {
        var prior=World.FindMobile((Serial)s.Target);
        if(Eligible(body,owner,prior)&&Full(s,prior)<=24){return prior;}
        Mobile found=null;int nearest=25;
        foreach(var candidate in body.Map.GetMobilesInRange<BaseCreature>(body.Location,14))
        {
            if(!Eligible(body,owner,candidate)){continue;}int distance=Full(s,candidate);
            if(distance<nearest||distance==nearest&&found!=null&&candidate.Serial.Value<found.Serial.Value){found=candidate;nearest=distance;}
        }
        return found;
    }
    private static void Velocity(ref IronGolemWorldState s,int type,int speed,int budget)=>s=s with{
        PathOverride=type==0?s.PathOverride:type,VelocityBonus=speed==0?s.VelocityBonus:speed,PathBudget=budget==0?s.PathBudget:Math.Min(77,budget)};
    private static (int Velocity,int Animation) MovementSpeed(IronGolemBody body,int bonus)
    {
        var item=NpcEquipmentCombat.Context(body)??throw new NotSupportedException("Missing original-item movement context");
        int armorSpeed=ClassGear.Slot(item.Item)!=GearSlot.Weapon&&!ClassGear.Broken(item.Item)?ClassGear.Base(item.Item).Speed:0;
        int faster=checked(item.Bonus("move1")+item.Bonus("move2")+item.Bonus("move3"));
        int percent=checked(100+bonus+(int)body.BirthValues.Eval("passivecalc1")+PaladinVigor.VelocityPercent(body)-GearEffects.MovementSlow(body)-armorSpeed);
        return SourceActorMotion.Speed(NecromancerSummons.Source("irongolem","Velocity"),NecromancerSummons.Source("irongolem-animation","WLSpeed"),percent,faster);
    }
    private static bool Path(IronGolemBody body,ref IronGolemWorldState s,Point2D target,bool run,bool delete=false)
    {
        if(run){throw new NotSupportedException("No owned Iron Golem run animation row");}
        if(target.X<=0||target.Y<=0||target.X>=body.Map.Width*2||target.Y>=body.Map.Height*2){if(delete){s=s with{NextThink=0};}return false;}
        int type=s.PathOverride==0?13:s.PathOverride,budget=s.PathBudget==0?5:s.PathBudget,bonus=s.VelocityBonus;
        s=s with{PathOverride=0,VelocityBonus=0,PathBudget=0};
        if(type==1){throw new NotSupportedException("Iron Golem follow needs source AStar path binding");}
        var speed=MovementSpeed(body,bonus);
        var motion=s.Motion with{PathType=type,Flags=type==13?0x100:type==15?0x800:0,Velocity=speed.Velocity,MaximumVelocity=speed.Velocity,
            Acceleration=0,AccelerationCounter=0,Points=[],Index=0,Distance=budget};
        var path=SourceActorPath.Initialize(new(motion,target,motion.FinalTarget),budget,new(1,1,body.Map.Width*2-1,body.Map.Height*2-1),(x,y)=>PetGroundWorld.Collision(body,x,y));
        if(path.Motion.Points.Length==0||(path.Motion.Flags&0x20)==0)
        {
            if(type is 2 or 7 or 13){s=s with{PathOverride=15,PathBudget=budget,VelocityBonus=bonus};return Path(body,ref s,target,false,delete);}
            if(delete){s=s with{NextThink=0};}return false;
        }
        body.Direction=body.GetDirectionTo(new Point3D((target.X+1)/2,(target.Y+1)/2,body.Z));
        s=s with{Mode=2,NextThink=0,ActionAt=0,EndAt=0,Target=0,Motion=path.Motion,MovingBonus=bonus};return true;
    }
    private static Point2D Wander(IronGolemBody body,Point2D target,int distance)
    {
        bool first=(NpcEquipmentRandom.Raw(body)&1)!=0;int small=NpcEquipmentRandom.Limited(body,distance);
        int x=first?distance:small,y=first?small:distance;
        if((NpcEquipmentRandom.Raw(body)&1)!=0){x=-x;}if((NpcEquipmentRandom.Raw(body)&1)!=0){y=-y;}
        return new(target.X+x,target.Y+y);
    }
    private static IronGolemWorldState Relocate(IronGolemBody body,IronGolemWorldState s,Point3D point,Map map)
    {body.MoveToWorld(point,map);body.AIObject?.AITimer.Stop();return s with{Map=map.MapID,Motion=DruidRavenMotion.Center(point.X*2,point.Y*2)};}
    private static IronGolemWorldState Think(IronGolemBody body,PlayerMobile owner,IronGolemWorldState initial,DateTime at)
    {
        var s=initial with{NextThink=0};var ownerCell=PetOwnerRoutes.Cell(owner);var route=PetOwnerRoutes.For(owner,at);
        bool moving=ClassMovement.IsMoving(owner,Core.TickCount);int mode=moving?(ClassMovement.IsRunning(owner,Core.TickCount)?3:2):1;
        int heading=DruidWolfGeometry.NativeHeading(owner.Direction);var offset=DruidWolfGeometry.Offset(heading);
        var end=moving?new Point2D(ownerCell.X+offset.X*2,ownerCell.Y+offset.Y*2):ownerCell;
        PetMoveContext Context()=>new(Cell(s),ownerCell,end,end,mode,Full(s,owner),
            (owner.AllFollowers??[]).OfType<BaseCreature>().Count(x=>x is{Alive:true,Deleted:false}&&PersistentMinions.Active(owner,x)),
            moving?heading:0,route.Cursor,route.Trail,route.LastTeleport);
        int Action(PetMoveRequest r)
        {
            var point=new Point2D(r.A,r.B);
            switch(r.Kind)
            {
                case PetMoveKind.Velocity:Velocity(ref s,r.A==1?7:r.A,r.B,r.C);return 0;
                case PetMoveKind.VelocityParameters:Velocity(ref s,r.A,r.B,r.C);return 0;
                case PetMoveKind.WalkDelete:return Path(body,ref s,point,false,true)?1:0;
                case PetMoveKind.RunDelete:return Path(body,ref s,point,true,true)?1:0;
                case PetMoveKind.Walk:return Path(body,ref s,point,false)?1:0;
                case PetMoveKind.Run:return Path(body,ref s,point,true)?1:0;
                case PetMoveKind.Wander:return Path(body,ref s,Wander(body,ownerCell,r.A),false)?1:0;
                case PetMoveKind.DeleteThink:s=s with{NextThink=0};return 0;
                case PetMoveKind.Idle:s=Neutral(s,r.A);return 0;
                case PetMoveKind.MoveUnit:if(PetGroundWorld.Point(body,point,out var native)){s=Relocate(body,s,native,body.Map);return 1;}return 0;
                case PetMoveKind.Refresh:case PetMoveKind.MarkTeleported:return 0;
                case PetMoveKind.CountOthers:return(owner.AllFollowers??[]).OfType<BaseCreature>().Count(x=>x!=body&&x is{Alive:true,Deleted:false}&&x.Map==body.Map&&DruidWolfGeometry.Full(Cell(s),PetOwnerRoutes.Cell(x))<=1);
                case PetMoveKind.Escape:
                    if(r.A>5){Velocity(ref s,0,0,r.A);}var cell=Cell(s);
                    return Path(body,ref s,new(cell.X+r.A*Math.Sign(cell.X-ownerCell.X),cell.Y+r.A*Math.Sign(cell.Y-ownerCell.Y)),false,r.B!=0)?1:0;
                case PetMoveKind.WalkOwner:return Path(body,ref s,Wander(body,ownerCell,r.A),false)?1:0;
                default:throw new NotSupportedException("Unmapped Iron Golem movement tactic");
            }
        }
        var movement=new PetMoveServices(Action,p=>PetGroundWorld.Room(body,p),p=>DruidWolfGeometry.NoSize(Cell(s),p),DruidWolfGeometry.Offset,
            ()=>NpcEquipmentRandom.Raw(body),()=>NpcEquipmentRandom.Limited(body,100),()=>PetGroundWorld.Point(body,ownerCell,out _)?ownerCell:null,p=>PetGroundWorld.Room(body,p)!=0);
        bool Move(int kind,bool run,int speed,int leash)=>SourcePetMovement.Move(Context(),movement,kind,run,speed,leash);
        void Request(NecromancerPetAiRequest r)
        {
            switch(r.Kind)
            {
                case NecromancerPetAiKind.DeleteThink:s=s with{NextThink=0};break;
                case NecromancerPetAiKind.Idle:s=Neutral(s,r.A);break;
                case NecromancerPetAiKind.Wander:Path(body,ref s,Wander(body,ownerCell,r.B),false);break;
                case NecromancerPetAiKind.Velocity:Velocity(ref s,r.A,r.B,r.C);break;
                case NecromancerPetAiKind.Approach:
                    var chase=World.FindMobile((Serial)(uint)r.A);if(chase!=null){Velocity(ref s,13,0,0);Path(body,ref s,PetOwnerRoutes.Cell(chase),false);}break;
                case NecromancerPetAiKind.Attack:
                    var target=World.FindMobile((Serial)(uint)r.B);if(target!=null){body.Direction=body.GetDirectionTo(target);}
                    int ias=checked(NpcEquipmentCombat.Bonus(body,"swing1")+NpcEquipmentCombat.Bonus(body,"swing2")+NpcEquipmentCombat.Bonus(body,"swing3"));
                    var held=NpcEquipmentCombat.Context(body)??throw new NotSupportedException("Missing original-item timing context");
                    int weaponSpeed=ClassGear.Slot(held.Item)==GearSlot.Weapon&&!ClassGear.Broken(held.Item)?ClassGear.Base(held.Item).Speed:0;
                    int speed=SourceAttackRate.Speed(NecromancerSummons.Source("irongolem-animation","A1Speed"),ias,checked(SourceAttackRate.WolfRate(body)-weaponSpeed));
                    var clock=DruidWolfGeometry.Clock("A1",speed,"irongolem-animation");
                    s=s with{Mode=4,Target=(uint)r.B,AttackSpeed=speed,ActionAt=s.Frame+clock.Action,EndAt=s.Frame+clock.End,NextThink=0,Motion=DruidWolfGeometry.Stop(s.Motion)};break;
                default:throw new NotSupportedException("Unmapped Iron Golem AI request");
            }
        }
        Mobile Find(int id)=>World.FindMobile((Serial)(uint)id);
        var queries=new NecromancerPetAiServices(Request,()=>(int)owner.Serial.Value,id=>Find(id) is PlayerMobile player?(int)player.Serial.Value:0,
            id=>Find(id) is{} target?Full(s,target):throw new NotSupportedException("Missing Iron Golem distance target"),
            _=>
            {
                Mobile target=owner.Combatant;if(!Eligible(body,owner,target)){target=World.FindMobile(ClassCombat.For(owner).Target);}
                return new(Eligible(body,owner,target)?(int)target.Serial.Value:0,0,false);
            },
            (ranged,_)=>{if(ranged){throw new NotSupportedException("Iron Golem has no ranged skill binding");}var target=Primary(body,owner,s);return new(target==null?0:(int)target.Serial.Value,target==null?0:Full(s,target),target!=null&&Melee(body,s,target,0));},
            id=>!Eligible(body,owner,Find(id)),id=>Find(id) is not{} target||!body.InLOS(target),id=>Eligible(body,owner,Find(id)),
            ()=>NpcEquipmentRandom.Limited(body,100),(target,combat,flag,leash)=>SourcePetMovement.Common(Context(),movement,target!=0,SpellHelper.IsTown(body.Location,owner),combat,flag,leash),
            ()=>SpellHelper.IsTown(body.Location,owner),Move);
        var memory=NecromancerPetAi.Think(new(s.Ai,NecromancerSummons.Source("irongolem","Velocity"),NecromancerSummons.Source("irongolem","Run"),0,0),queries);
        s=s with{Ai=memory};
        if(s.NextThink==0&&s.Mode is not(2 or 4 or 8)){s=s with{NextThink=s.Frame+15};}return s;
    }
    internal static void Advance(IronGolemBody body,DateTime at,bool actions)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Iron Golem world clock must be UTC");}
        if(body.Deleted||body.Prepared?.Controller is not{} s||s.Fault!=null||s.NextUtc>at){return;}
        if(body.ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner||owner.Serial.Value!=body.Prepared.Recipe.Owner){body.Delete();return;}
        if(body.ControllerLoaded){body.ControllerLoaded=false;Skip(body,s,at);return;}
        if(!PersistentMinions.Active(owner,body)||!body.Permitted(owner)||!actions||owner.Map==null||owner.Map==Map.Internal)
        {
            if(!PersistentMinions.Active(owner,body)||!body.Permitted(owner)){PersistentMinions.ParkClass(owner,DiabloClass.Necromancer);}else{PersistentMinions.Suspend(owner);}
            Skip(body,body.Prepared.Controller,at);return;
        }
        if(body.Map==Map.Internal){PersistentMinions.Resume(owner);if(body.Map==Map.Internal){return;}}
        if(body.Map!=owner.Map)
        {if(!Teleportation.Space(owner.Map,owner.Location,body,true)){return;}s=Neutral(Relocate(body,s,owner.Location,owner.Map),1);}
        if(s.Map!=body.Map.MapID||body.X!=(Cell(s).X+1)/2||body.Y!=(Cell(s).Y+1)/2)
        {s=Neutral(s with{Map=body.Map.MapID,Motion=DruidRavenMotion.Center(body.X*2,body.Y*2)},1);}
        while(!body.Deleted&&s.NextUtc<=at)
        {
            var frameAt=s.NextUtc;s=s with{Frame=checked(s.Frame+1),NextUtc=s.NextUtc.AddMilliseconds(40)};
            if(!body.Poisoned&&TimedEffects.CanRegenerateHits(body))
            {NpcEquipmentRegeneration.Apply(body,NpcEquipmentRegeneration.Delta(BattleOrders.Regeneration256(body),NpcEquipmentRegeneration.ItemRate(body),0,false,false));}
            if(body.Deleted){return;}
            if(body.Frozen||body.Paralyzed||CreatureHitRecovery.Remaining(body)>0||SourceKnockbacks.Remaining(body)>0){s=Park(s);body.RestoreController(s);continue;}
            if(s.Mode==2)
            {
                var speed=MovementSpeed(body,s.MovingBonus);
                var moved=SourceActorMotion.Advance(s.Motion with{Velocity=speed.Velocity,MaximumVelocity=speed.Velocity},(x,y)=>PetGroundWorld.Collision(body,x,y));s=s with{Motion=moved.State};
                if(!moved.Moving){s=Neutral(s,1);}
                if(PetGroundWorld.Point(body,Cell(s),out var position)&&body.Location!=position){body.MoveToWorld(position,body.Map);}
            }
            if(s.ActionAt==s.Frame)
            {
                var target=World.FindMobile((Serial)s.Target);bool hit=false;var prior=body.Prepared.Controller;
                if(Eligible(body,owner,target)&&Melee(body,s,target,3))
                {
                    using var attempted=PaladinThorns.Attempt(body,target,false);
                    if(ClassStats.CheckHit(body,target,0,()=>NpcEquipmentRandom.Limited(body,100)/100.0)==true)
                    {if(!NpcEquipmentDamage.TryResolve(body,target,out _)){throw new NotSupportedException("Iron Golem original-item damage binding was lost");}hit=true;}
                }
                if(body.Deleted){return;}
                s=s with{ActionAt=0,Attempts=checked(s.Attempts+1),Hits=checked(s.Hits+(hit?1:0))};
                if(!ReferenceEquals(body.Prepared.Controller,prior)){s=Park(s);}
            }
            if(s.Mode is 4 or 8&&s.EndAt==s.Frame){s=Neutral(s);}
            if(s.NextThink==s.Frame){s=Think(body,owner,s,frameAt);}
            body.RestoreController(s);body.AIObject?.AITimer.Stop();
        }
    }
    private static void Skip(IronGolemBody body,IronGolemWorldState s,DateTime at)
    {long frames=(at-s.NextUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;body.RestoreController(Park(s with{Frame=checked(s.Frame+(int)frames),NextUtc=s.NextUtc.AddMilliseconds(frames*40)}));}
    internal static void Fault(IronGolemBody body,string reason)
    {if(body.Prepared?.Controller is{} s){body.RestoreController(Park(s) with{Fault=reason});}}
}

internal static class IronGolems
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IronGolems));
    private static readonly HashSet<IronGolemBody> Bodies=new();
    internal static void Track(IronGolemBody body)=>Bodies.Add(body);
    internal static void Forget(IronGolemBody body)=>Bodies.Remove(body);
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static void Tick(DateTime at,bool? actions=null)
    {
        var active=new List<IronGolemBody>();
        foreach(var body in Bodies.ToArray())
        {
            if(body.Deleted){Forget(body);continue;}
            try
            {
                if(actions??body.ControlMaster?.NetState is{Running:true}){active.Add(body);}
                else{IronGolemWorld.Advance(body,at,false);}
            }
            catch(Exception e){IronGolemWorld.Fault(body,e.Message);Log.Error(e,"Prepared Iron Golem controller refused context for {Serial}",body.Serial);}
        }
        // Match other ground pets: consume due actor frames in order so one
        // pet cannot expire owner-route history before another's earlier frame.
        var stalled=new HashSet<IronGolemBody>();
        while(true)
        {
            var due=active.Where(b=>!b.Deleted&&!stalled.Contains(b)&&b.Prepared?.Controller is{Fault:null} c&&c.NextUtc<=at)
                .OrderBy(b=>b.Prepared.Controller.NextUtc).ThenBy(b=>b.Serial.Value).FirstOrDefault();
            if(due==null){break;}var before=due.Prepared.Controller.NextUtc;
            try{IronGolemWorld.Advance(due,before,true);if(!due.Deleted&&due.Prepared.Controller.NextUtc==before){stalled.Add(due);}}
            catch(Exception e){IronGolemWorld.Fault(due,e.Message);Log.Error(e,"Prepared Iron Golem controller refused context for {Serial}",due.Serial);}
        }
    }
}
