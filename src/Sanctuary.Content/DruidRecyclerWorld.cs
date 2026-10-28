// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record RecyclerWorldState(int Map,ActorMotionState Motion,ulong Seed,DateTime NextUtc,int GameBase,
    CreeperAiState Ai,RecyclerRecoveryModel RecoveryModel,int Frame=0,int Mode=8,int NextThink=25,int EndAt=3,
    uint Target=0,int PathOverride=0,int PathBudget=0,int VelocityBonus=0,int MovingBonus=0,
    int SkillAttempts=0,int Reservations=0,int Allocations=0,long LastRecovery=0,string Fault=null,bool PendingVisionReset=false)
{public string Profile{get;init;}="prepared-ai111-mode-entry-recovery-v1";}

// Native room/item ordering and terrain are explicit projections. Reuse the
// source pet, path, motion, RNG and animation engines; no ordinary UO pet AI.
internal static class DruidRecyclerWorld
{
    internal static Func<DruidRecyclerBody,int,int,int> Collision=(body,x,y)=>PetGroundWorld.Collision(body,x,y,true);
    internal static RecyclerWorldState Copy(RecyclerWorldState s)=>s with
    {Motion=s.Motion with{Points=[..s.Motion.Points],SavedSteps=[..s.Motion.SavedSteps]}};
    internal static Point2D Cell(RecyclerWorldState s)=>DruidRavenMotion.Cell(s.Motion);
    internal static void Validate(RecyclerWorldState s)
    {
        if(s?.Motion?.Points==null||s.Motion.SavedSteps==null||s.Ai==null
            ||s.Map<0||s.Map>=Map.Maps.Length||Map.Maps[s.Map]==null||Map.Maps[s.Map]==Map.Internal
            ||s.Frame<0||s.GameBase<0||(long)s.GameBase+s.Frame>int.MaxValue-100
            ||s.Ai.LastSkillFrame<0||s.Ai.LastSkillFrame>s.GameBase+s.Frame||s.Ai.Parameter0!=0
            ||s.NextUtc.Kind!=DateTimeKind.Utc||s.NextUtc>DateTime.MaxValue.AddSeconds(-2)||!Enum.IsDefined(s.RecoveryModel)
            ||s.Mode is not(1 or 2 or 8)||s.NextThink<0||s.NextThink!=0&&s.NextThink<=s.Frame
            ||s.EndAt<0||s.EndAt!=0&&s.EndAt<=s.Frame||s.Mode==8&&s.EndAt==0||s.Mode!=8&&s.EndAt!=0
            ||s.Mode==2&&(s.Motion.Flags&0x20)==0||s.PathOverride is not(0 or 1 or 7 or 13 or 15)||s.PathBudget is <0 or >77
            ||s.VelocityBonus is <-126 or >126||s.MovingBonus is <-126 or >126
            ||s.SkillAttempts<0||s.SkillAttempts>s.Frame||s.Reservations<0||s.Reservations>s.SkillAttempts
            ||s.Allocations<0||s.Allocations>s.Reservations||s.LastRecovery<0
            ||s.Profile!="prepared-ai111-mode-entry-recovery-v1")
        {throw new InvalidDataException("Invalid recycler world snapshot");}
        var cell=Cell(s);var map=Map.Maps[s.Map];
        if(cell.X<=0||cell.Y<=0||cell.X>=map.Width*2||cell.Y>=map.Height*2){throw new InvalidDataException("Recycler pose outside facet");}
        SourceActorMotion.Advance(s.Motion with{Flags=s.Motion.Flags&~0x20},(_,_)=>throw new InvalidOperationException());
    }
    internal static int SkillEnd()=>DruidWolfGeometry.Clock("S1",source:"recycler-animation").End;
    internal static DruidRecyclerBody Create(PlayerMobile owner,CombatAbility ability,Point3D point,DateTime at,ulong seed,
        RecyclerRecoveryModel model,int? gameFrame=null)
    {
        if(!Enum.IsDefined(model)){throw new NotSupportedException("Recycler recovery profile must be explicit");}
        var body=DruidRecyclerBody.CreatePrepared(owner,ability,point,at);
        try
        {
            var s=new RecyclerWorldState(owner.Map.MapID,DruidRavenMotion.Center(point.X*2,point.Y*2),seed,at.AddMilliseconds(40),
                gameFrame??DruidCreepers.GameFrame,DruidCreeperAi.Initialize(new(0,0)),model,EndAt:SkillEnd());
            Store(body,s);PetOwnerRoutes.For(owner,at);return body;
        }
        catch{body.Delete();throw;}
    }
    private static RecyclerWorldState Neutral(RecyclerWorldState s,int delay=15)=>s with
    {Mode=1,NextThink=checked(s.Frame+Math.Max(1,delay)),EndAt=0,Motion=DruidWolfGeometry.Stop(s.Motion)};
    internal static RecyclerWorldState Park(RecyclerWorldState s)=>Neutral(s,1) with{PathOverride=0,PathBudget=0,VelocityBonus=0};
    private static void Store(DruidRecyclerBody body,RecyclerWorldState s)=>body.RestorePrepared(body.Prepared with{Controller=s,NextRecoveryUtc=s.NextUtc});
    private static bool Point(DruidRecyclerBody body,Point2D cell,out Point3D point)=>PetGroundWorld.Point(body,cell,out point,true);
    private static RecyclerWorldState Relocate(DruidRecyclerBody body,RecyclerWorldState s,Point3D point,Map map)
    {body.MoveToWorld(point,map);body.AIObject?.AITimer.Stop();return s with{Map=map.MapID,Motion=DruidRavenMotion.Center(point.X*2,point.Y*2)};}
    private static void Velocity(ref RecyclerWorldState s,int type,int speed,int budget)
    {s=s with{PathOverride=type==0?s.PathOverride:type,VelocityBonus=speed==0?s.VelocityBonus:speed,PathBudget=budget==0?s.PathBudget:Math.Min(77,budget)};}
    private static uint Draw(ref RecyclerWorldState s)
    {ulong seed=s.Seed;uint value=SourceRandom.Roll(ref seed);s=s with{Seed=seed};return value;}
    private static Point2D Wander(ref RecyclerWorldState s,Point2D center,int distance)
    {
        bool first=(Draw(ref s)&1)!=0;int small=distance<=0?0:(int)(Draw(ref s)%(uint)distance);
        int x=first?distance:small,y=first?small:distance;if((Draw(ref s)&1)!=0){x=-x;}if((Draw(ref s)&1)!=0){y=-y;}
        return new(center.X+x,center.Y+y);
    }
    private static bool Path(DruidRecyclerBody body,ref RecyclerWorldState s,Point2D target,bool delete=false)
    {
        if(target.X<=0||target.Y<=0||target.X>=body.Map.Width*2||target.Y>=body.Map.Height*2){return false;}
        int type=s.PathOverride==0?13:s.PathOverride,budget=s.PathBudget==0?5:s.PathBudget,bonus=s.VelocityBonus;
        s=s with{PathOverride=0,PathBudget=0,VelocityBonus=0};
        if(type==1){throw new NotSupportedException("Recycler source AStar fallback remains unimplemented");}
        var speed=Speed(body,bonus);
        var motion=s.Motion with{PathType=type,Flags=type==13?0x100:type==15?0x800:0,Velocity=speed,MaximumVelocity=speed,
            Acceleration=0,AccelerationCounter=0,Points=[],Index=0,Distance=budget};
        var result=SourceActorPath.Initialize(new(motion,target,motion.FinalTarget),budget,new(1,1,body.Map.Width*2-1,body.Map.Height*2-1),
            (x,y)=>Collision(body,x,y));
        if(result.Motion.Points.Length==0||(result.Motion.Flags&0x20)==0)
        {
            if(type is 7 or 13){s=s with{PathOverride=15,PathBudget=budget,VelocityBonus=bonus};return Path(body,ref s,target,delete);}
            if(delete){s=s with{NextThink=0};}return false;
        }
        body.Direction=body.GetDirectionTo(new Point3D((target.X+1)/2,(target.Y+1)/2,body.Z));
        s=s with{Mode=2,NextThink=0,EndAt=0,Motion=result.Motion,MovingBonus=bonus};return true;
    }
    private static int Speed(DruidRecyclerBody body,int bonus)=>SourceActorMotion.Speed(NecromancerSummons.Source(body.SourceMonsterId,"Velocity"),
        NecromancerSummons.Source("recycler-animation","WLSpeed"),100+bonus+PaladinVigor.VelocityPercent(body)-GearEffects.MovementSlow(body),0).Velocity;
    // No implicit corpse-size default. Native corpse ownership must retain a
    // mapped monster body; unsupported historical corpses are skipped.
    internal static bool CorpseSize(Corpse corpse,out int size)
    {size=corpse?.Owner is BaseCreature c?CreatureProfiles.For(c).Traits.Size??-1:-1;return size is >=0 and <=3;}
    internal static RecyclerCorpse FindCorpse(DruidRecyclerBody body,PlayerMobile owner,RecyclerWorldState s,int range)
    {
        // Preserve native sector/item order, not a nearest-corpse invention.
        foreach(var corpse in body.Map.GetItemsInRange<Corpse>(body.Location,(range+1)/2))
        {
            var cell=new Point2D(corpse.X*2,corpse.Y*2);
            if(DruidWolfGeometry.NoSize(Cell(s),cell)>range||!CorpseSize(corpse,out int size)
                ||!CorpseSkillRules.UsableAt(owner,corpse,body,double.PositiveInfinity)){continue;}
            return new((int)corpse.Serial.Value,DruidWolfGeometry.UnitDistance(Cell(s),cell,0,size),
                DruidWolfGeometry.MeleeDistance(Cell(s),cell,0,size)<=2&&body.InLOS(corpse));
        }
        return default;
    }
    internal static RecyclerWorldState WalkToCorpse(DruidRecyclerBody body,RecyclerWorldState s,Corpse corpse,int flags)
    {
        s=s with{Target=corpse.Serial.Value};
        if(Path(body,ref s,new(corpse.X*2,corpse.Y*2))){return s;}
        // A failed mode change first restores neutral. Flags7 then delete its
        // think, mark AI-control0x40 and select close-walk4 or idle10.
        s=Neutral(s);
        PetWalkFailure.Apply(flags,new(()=>s=s with{NextThink=0},()=>s=s with{PendingVisionReset=true},
            ()=>(int)(Draw(ref s)%100),delay=>s=Neutral(s,delay),distance=>
            {if(!Path(body,ref s,Wander(ref s,Cell(s),distance))){s=Neutral(s);}}));
        return s;
    }
    private static Mobile Primary(DruidRecyclerBody body,PlayerMobile owner,RecyclerWorldState s)
    {
        bool Eligible(Mobile target)=>target is BaseCreature c&&c is{Alive:true,Deleted:false}&&c.Map==body.Map&&!c.Blessed&&c.CanBeDamaged()
            &&c is not BaseVendor&&CreatureProfiles.For(c).Traits.Size is >=0 and <=3&&Math.Abs(c.Z-body.Z)<16
            &&ClassCombat.IsHostile(owner,c)&&body.CanBeHarmful(c,false)&&body.InLOS(c)&&!SpellHelper.IsTown(c.Location,owner)
            &&(c is EncounterCreature||SourceAuraTargets.Rule(CreatureProfiles.Definition(c).Reference) is{Attackable:true});
        // Source0x40forces the next vision search. A fresh native hostility
        // scan is the declared projection; source room/vision parity is open.
        var prior=s.PendingVisionReset?null:World.FindMobile((Serial)s.Target);
        if(Eligible(prior)&&DruidWolfGeometry.NoSize(Cell(s),PetOwnerRoutes.Cell(prior))<20){return prior;}
        foreach(var c in body.Map.GetMobilesInRange<BaseCreature>(body.Location,10))
        {if(Eligible(c)&&DruidWolfGeometry.NoSize(Cell(s),PetOwnerRoutes.Cell(c))<20){return c;}}
        return null;
    }
    internal static RecyclerWorldState EnterFeeding(DruidRecyclerBody body,RecyclerWorldState s,Corpse corpse,DateTime at)
    {
        if(BattleCommand.State(body)!=null){throw new NotSupportedException("Recycler innate Battle Command transport remains unverified");}
        if(corpse!=null){body.Direction=body.GetDirectionTo(corpse);}
        s=s with{Mode=8,EndAt=checked(s.Frame+SkillEnd()),NextThink=0,Motion=DruidWolfGeometry.Stop(s.Motion),SkillAttempts=checked(s.SkillAttempts+1)};
        // Complete mode8entry calls start immediately and returns success even
        // if that start fails. The later kind2action has no recycler dofunc.
        if(body.ControlMaster is PlayerMobile owner&&!SpellHelper.IsTown(body.Location,owner))
        {
            var commit=DruidRecyclerRecovery.Commit(body,corpse,at,s.RecoveryModel);
            s=s with{Reservations=s.Reservations+(commit.Reserved?1:0),Allocations=s.Allocations+(commit.Allocated?1:0),
                LastRecovery=commit.Allocated?commit.Effect:s.LastRecovery};
        }
        return s;
    }
    private static RecyclerWorldState Think(DruidRecyclerBody body,PlayerMobile owner,RecyclerWorldState original,DateTime at)
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
                case PetMoveKind.Wander:case PetMoveKind.WalkOwner:return Path(body,ref s,Wander(ref s,ownerCell,r.A))?1:0;
                case PetMoveKind.DeleteThink:s=s with{NextThink=0};return 0;
                case PetMoveKind.Idle:s=Neutral(s,r.A);return 0;
                case PetMoveKind.MoveUnit:if(Point(body,p,out var native)){s=Relocate(body,s,native,body.Map);return 1;}return 0;
                case PetMoveKind.Refresh:case PetMoveKind.MarkTeleported:return 0;
                case PetMoveKind.CountOthers:return (owner.AllFollowers??[]).OfType<BaseCreature>().Count(p=>p!=body&&p is{Alive:true,Deleted:false}&&p.Map==body.Map&&DruidWolfGeometry.Full(Cell(s),PetOwnerRoutes.Cell(p),0)<=1);
                case PetMoveKind.Escape:
                    if(r.A>5){Velocity(ref s,0,0,r.A);}var cell=Cell(s);
                    return Path(body,ref s,new(cell.X+r.A*Math.Sign(cell.X-ownerCell.X),cell.Y+r.A*Math.Sign(cell.Y-ownerCell.Y)),r.B!=0)?1:0;
                default:throw new NotSupportedException("Unsupported recycler movement tactic");
            }
        }
        var services=new PetMoveServices(Action,p=>PetGroundWorld.Room(body,p),p=>DruidWolfGeometry.NoSize(Cell(s),p),DruidWolfGeometry.Offset,
            ()=>Draw(ref s),()=>(int)(Draw(ref s)%100),()=>Point(body,ownerCell,out _)?ownerCell:null,p=>PetGroundWorld.Room(body,p)!=0);
        bool Common(int target,bool combat,int flag,int leash)=>SourcePetMovement.Common(Context(),services,target!=0,SpellHelper.IsTown(body.Location,owner),combat,flag,leash);
        bool Move(int kind,bool run,int speed,int leash)=>SourcePetMovement.Move(Context(),services,kind,run,speed,leash);
        void Request(CreeperAiRequest r)
        {
            if(r.Kind==CreeperAiKind.Idle){s=Neutral(s,r.A);return;}
            if(r.Kind==CreeperAiKind.Walk)
            {if(World.FindItem((Serial)(uint)r.A) is Corpse corpse){s=WalkToCorpse(body,s,corpse,r.B);}return;}
            if(r.Kind==CreeperAiKind.Escape&&World.FindMobile((Serial)(uint)r.A) is{} target)
            {var cell=Cell(s);var enemy=PetOwnerRoutes.Cell(target);if(r.B>5){Velocity(ref s,0,0,r.B);}Path(body,ref s,new(cell.X+r.B*Math.Sign(cell.X-enemy.X),cell.Y+r.B*Math.Sign(cell.Y-enemy.Y)));return;}
            throw new NotSupportedException("Unsupported recycler AI action");
        }
        int skill=body.VineAbility==CombatAbility.CarrionVine?307:325;var birth=body.Prepared.Snapshot.Birth;
        var primary=Primary(body,owner,s);bool combat=primary is BaseCreature creature&&DruidWolfGeometry.MeleeDistance(Cell(s),PetOwnerRoutes.Cell(primary),0,CreatureProfiles.For(creature).Traits.Size.Value)<=2;
        s=s with{Target=primary?.Serial.Value??0,PendingVisionReset=false};
        var ai=DruidRecyclerAi.Think(new(body.VineAbility,true,DruidWolfGeometry.UnitDistance(Cell(s),ownerCell,0,2),checked(s.GameBase+s.Frame),
            skill,true,birth.RecyclerRank>0,checked((int)DruidNpcSkills.Recycler(body.VineAbility,birth.RecyclerRank,birth.Level).Eval("aurarangecalc")),
            (int)(primary?.Serial.Value??0),false,combat,SkillHealth.Full(owner),NecromancerEffects.ManaBalance256(owner)>=owner.ManaMax*256,body.SummonDifficulty),s.Ai,
            new(Request,Common,Move,range=>FindCorpse(body,owner,s,range),()=>(int)(Draw(ref s)%100),r=>
            {
                if(r.A!=8||r.B!=skill){throw new NotSupportedException("Unsupported recycler innate skill");}
                s=EnterFeeding(body,s,World.FindItem((Serial)(uint)r.C) as Corpse,at);return true;
            }));
        s=s with{Ai=ai};if(s.NextThink==0&&s.Mode is not(2 or 8)){s=s with{NextThink=s.Frame+15};}return s;
    }
    internal static void Advance(DruidRecyclerBody body,DateTime at,bool actions)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Recycler world clock must be UTC");}
        if(body.Deleted||body.Prepared is not{Controller:{} s} prepared||s.Fault!=null||s.NextUtc>at){return;}
        if(body.ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner||owner.Serial.Value!=prepared.Owner){body.Delete();return;}
        if(body.ControllerLoaded||!actions||!PersistentMinions.Active(owner,body)||!body.Permitted(owner)||owner.Map==null||owner.Map==Map.Internal)
        {
            body.ControllerLoaded=false;
            if(!PersistentMinions.Active(owner,body)||!body.Permitted(owner)){PersistentMinions.ParkClass(owner,DiabloClass.Druid);}else{PersistentMinions.Suspend(owner);}
            s=body.Prepared.Controller;long count=(at-s.NextUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;
            Store(body,Park(s with{Frame=checked(s.Frame+(int)count),NextUtc=s.NextUtc.AddMilliseconds(count*40)}));return;
        }
        if(body.Map==Map.Internal){PersistentMinions.Resume(owner);if(body.Map==Map.Internal){return;}}
        if(body.Map!=owner.Map){s=Neutral(Relocate(body,s,owner.Location,owner.Map),1);}
        if(s.Map!=body.Map.MapID||body.X!=(Cell(s).X+1)/2||body.Y!=(Cell(s).Y+1)/2)
        {s=Neutral(s with{Map=body.Map.MapID,Motion=DruidRavenMotion.Center(body.X*2,body.Y*2)},1);}
        while(!body.Deleted&&s.NextUtc<=at)
        {
            var frameAt=s.NextUtc;s=s with{Frame=checked(s.Frame+1),NextUtc=s.NextUtc.AddMilliseconds(40)};
            if(!body.Poisoned&&TimedEffects.CanRegenerateHits(body)&&!SkillHealth.Full(body)){SkillHealth.Heal(body,BattleOrders.Regeneration256(body));}
            if(body.Frozen||body.Paralyzed){s=Park(s);Store(body,s);continue;}
            if(s.Mode==2)
            {
                int speed=Speed(body,s.MovingBonus);var moved=SourceActorMotion.Advance(s.Motion with{Velocity=speed,MaximumVelocity=speed},(x,y)=>Collision(body,x,y));s=s with{Motion=moved.State};
                if(!moved.Moving){s=Neutral(s,1);}if(Point(body,Cell(s),out var point)&&body.Location!=point){body.MoveToWorld(point,body.Map);}
            }
            if(s.Mode==8&&s.EndAt==s.Frame){int next=s.NextThink>s.Frame?s.NextThink:s.Frame+15;s=Neutral(s) with{NextThink=next};}
            if(s.NextThink==s.Frame){s=Think(body,owner,s,frameAt);}Store(body,s);body.AIObject?.AITimer.Stop();
        }
    }
    internal static void Fault(DruidRecyclerBody body,string message)
    {if(body.Prepared?.Controller is{} s){Store(body,Park(s) with{Fault=message});}}
}
