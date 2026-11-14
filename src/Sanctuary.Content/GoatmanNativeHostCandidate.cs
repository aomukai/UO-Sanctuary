// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record GoatmanHostSnapshot(SourceFramePosition Clock)
{
    public string Profile{get;init;}="native-goatman-host-v1";
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Fault{get;init;}
}
internal sealed record GoatmanHostSource(int ClassId,byte Flags241);

// Shared original ground-demon host. The Goatman caller is already admitted;
// other species retain their explicit qualification. Source AI,search,
// movement,follow,warp,animation and damage kernels are reused throughout.
internal sealed class GoatmanNativeHostCandidate:IDisposable
{
    private static readonly Lazy<GoatmanHostSource> Source=new(()=>BalanceCatalog.Read<GoatmanHostSource>("goatman-host-source").Single());
    private readonly SkeletonGuardian Body;
    private readonly PlayerMobile Owner;
    private readonly GenericPetTimedEvents Events=new();
    private GenericPetFramePump Pump;
    private GoatmanNativeMotion Motion;
    private readonly DemonNativeCombat Combat;
    private bool Suspended,Blocked;
    private DateTime At;
    internal int Decisions,Contacts,Frames;
    internal GoatmanNativeMotion Movement=>Motion;
    internal DemonNativeCombat Attacks=>Combat;
    internal bool NeedsObservation
    {
        get
        {
            if(Suspended||Body.Map!=Owner.Map||DeathMarkWorld.State(Body)?.Map!=Body.Map?.MapID){return true;}
            var p=DeathMarkWorld.Path(Body);
            return ((int)(p.X>>16)+1)/2!=Body.X||((int)(p.Y>>16)+1)/2!=Body.Y||p.X!=Motion.Motion.X||p.Y!=Motion.Motion.Y
                ||Combat.Current is{} action&&DeathMarkWorld.State(Body).Mode!=action.Mode;
        }
    }
    internal GoatmanHostSnapshot Capture()=>new(Pump.Position){Profile=DemonNativeState.Profile(Body)};
    private int SourceClass=>WarlockDemonPlacement.ClassId(Body);
    private GoatmanHostSource Species=>Body switch{WarlockGoatmanBody=>Source.Value,WarlockTaintedBody=>new(DemonGroundMovement.Tainted.ClassId,DemonGroundMovement.Tainted.Flags241),WarlockDefilerBody=>new(DefilerFlyingMovement.Species.ClassId,DefilerFlyingMovement.Species.Flags241),_=>throw new NotSupportedException("No demon host species")};
    public void Dispose(){Combat.Dispose();Motion.Stop();ClearEvents();}
    internal void Interrupt(){Combat.Cancel();Motion.Stop();ClearEvents();Idle(1);}
    internal void Park(){Combat.Cancel();Motion.Stop();ClearEvents();Suspended=true;}
    internal static GenericPetBirthPath BirthPath(SkeletonGuardian body)
    {
        if(body is WarlockDefilerBody){return DefilerFlyingWorld.BirthPath(body);}
        if(body.Map==null||body.Map==Map.Internal){throw new NotSupportedException("Native birth path requires ground world space");}
        int size=DemonPathWorld.Size(body),cls=WarlockDemonPlacement.ClassId(body);
        var path=new GenericPetCommitPath{X=((uint)(body.X*2)<<16)|32768,Y=((uint)(body.Y*2)<<16)|32768,Flags=1,
            Room=GenericPetNativePlacement.RoomHandle(body.Map,body.X*2,body.Y*2),Size5c=size,Mask60=0x100,Mask64=0x3401,Actor=new(checked((int)body.Serial.Value),1)};
        return new(new(new(cls,0),true,1,cls,size,size,0),path,1,1,TimedEffects.Now);
    }
    internal static WarlockGoatmanBody Create(PlayerMobile owner,Point3D point,DateTime at)
        =>WarlockGoatmanBody.CreatePrepared(owner,point,at,new(1,BirthPath));
    internal static WarlockTaintedBody CreateTainted(PlayerMobile owner,Point3D point,DateTime at)
        =>WarlockTaintedBody.CreatePrepared(owner,point,at,new(1,BirthPath));
    internal static WarlockDefilerBody CreateDefiler(PlayerMobile owner,Point3D point,DateTime at)
        =>DefilerFlyingWorld.CreateCandidate(owner,point,at);
    internal GoatmanNativeHostCandidate(SkeletonGuardian body,DateTime at,GoatmanHostSnapshot saved=null)
    {
        if(at.Kind!=DateTimeKind.Utc||body is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile owner}||DeathMarkWorld.State(body)is not{Selector:1})
        {throw new NotSupportedException("Candidate host requires its original registered living Goatman");}
        Body=body;Owner=owner;At=at;_=DemonPathWorld.Size(body);
        if(saved!=null&&saved.Profile!=DemonNativeState.Profile(body)){throw new NotSupportedException("Saved ground host species differs");}
        var position=saved?.Clock??new SourceFramePosition(0,at.AddTicks(SourceFrameTimeline.IntervalTicks));SourceFrameTimeline.Validate(position);
        Pump=new(Events,position);Suspended=saved!=null||body.Map==Map.Internal;
        if(Species.ClassId!=SourceClass){throw new NotSupportedException("Missing captured ground demon movement metadata");}
        Motion=new(body,owner,Events,()=>Pump.Position.Frame,()=>At,EventClock,Idle,Species.Flags241);
        Combat=new(body,owner);
        try{if(!Suspended){Idle(1);}}catch{Combat.Dispose();throw;}
    }
    private GenericPetEventClock EventClock()=>GenericPetNativeControlStates.Scheduling(Body,
        new(Pump.Position.Frame,DeathMarkWorld.State(Body)?.Mode??1,1,GenericPetMetadata.Table.StateCount,true,-1,0,4));
    private static void Deferred()=>throw new NotSupportedException("Candidate normal Goatman has no uninterruptible-deferred completion adapter");
    private void Idle(int ticks)
    {
        Events.Idle(checked((int)Body.Serial.Value),ticks,EventClock,()=>
        {Combat.Cancel();Motion?.Stop();DeathMarkWorld.ChangeMode(Body,1);},Deferred);
        Motion?.SyncEvents();
    }
    private void ClearEvents()
    {foreach(int id in Events.Unit(checked((int)Body.Serial.Value))){Events.Remove(id);}}
    private bool Active()=>Body is{Alive:true,Deleted:false}&&Owner is{Alive:true,Deleted:false,NetState:{Running:true}}
        &&Body.ControlMaster==Owner&&PersistentMinions.Active(Owner,Body)&&Body.Permitted(Owner)&&Owner.Map!=null&&Owner.Map!=Map.Internal&&!MinionSafeAreas.Contains(Owner);
    internal void Advance(DateTime observed)
    {
        if(observed.Kind!=DateTimeKind.Utc){throw new ArgumentException("Candidate world observation must be UTC");}
        if(Body.Deleted){return;}
        if(Owner is not{Alive:true,Deleted:false}||Body.ControlMaster!=Owner){Combat.Cancel();Body.Delete();return;}
        if(!Active())
        {
            Combat.Cancel();Motion.Stop();ClearEvents();Pump=new(Events,SourceFrameTimeline.Skip(Pump.Position,observed));
            DemonNativeState.Recovery(Body,Pump.Position.NextUtc);PersistentMinions.Suspend(Owner);Suspended=true;return;
        }
        var registered=DeathMarkWorld.Path(Body);
        if(Body.Map==Owner.Map&&DeathMarkWorld.State(Body)?.Map==Body.Map.MapID&&!Suspended
            &&(registered.X!=Motion.Motion.X||registered.Y!=Motion.Motion.Y
                ||Combat.Current is{} external&&DeathMarkWorld.State(Body).Mode!=external.Mode))
        {
            // Death Mark and other qualified placement consumers already own
            // their registered destination/command/delay. Do not replace them
            // with a fresh birth path or replay the interrupted action.
            Combat.Cancel();ClearEvents();Motion.Reset();Pump=new(Events,SourceFrameTimeline.Skip(Pump.Position,observed));
            long remaining=Math.Max(0,(DeathMarkWorld.State(Body).NextAiUtc-Pump.Position.NextUtc).Ticks);
            Idle(checked((int)Math.Min(int.MaxValue,(remaining+SourceFrameTimeline.IntervalTicks-1)/SourceFrameTimeline.IntervalTicks+1)));
            DemonNativeState.Recovery(Body,Pump.Position.NextUtc);return;
        }
        if(Suspended||Body.Map!=Owner.Map||DeathMarkWorld.State(Body)?.Map!=Body.Map?.MapID
            ||Body.Map!=Map.Internal&&((int)(DeathMarkWorld.Path(Body).X>>16)+1)/2!=Body.X
            ||Body.Map!=Map.Internal&&((int)(DeathMarkWorld.Path(Body).Y>>16)+1)/2!=Body.Y)
        {
            Combat.Cancel();ClearEvents();Pump=new(Events,SourceFrameTimeline.Skip(Pump.Position,observed));
            if(Body.Map==Map.Internal){PersistentMinions.Resume(Owner);}
            if(Body.Map==Map.Internal){return;}
            if(Body.Map!=Owner.Map)
            {
                if(!Teleportation.Space(Owner.Map,Owner.Location,Body,true)){return;}
                Body.MoveToWorld(Owner.Location,Owner.Map);Body.AIObject?.AITimer.Stop();
            }
            DeathMarkWorld.RebindPrepared(Body,BirthPath(Body));Motion.Reset();
            DemonNativeState.Recovery(Body,Pump.Position.NextUtc);Suspended=false;Blocked=false;Idle(1);return;
        }
        Pump.Advance(observed,(node,frame,at)=>
        {
            if(node.Type!=2){throw new NotSupportedException("Candidate event dispatcher only owns source AITHINK entries");}
            GenericPetMonsterEventRouter.Run(node,_=>GenericPetNativeControlStates.Events(Body,1,4,DeathMarkWorld.State(Body).Mode),_=>Think());
        },(frame,at)=>
        {
            At=at;Frames++;
            // Recovery is one admitted active frame,never an offline catch-up.
            DemonNativeState.Recover(Body,at);
            uint status=GenericPetNativeControlStates.Read(Body);bool blocked=Body.Frozen||Body.Paralyzed||CreatureHitRecovery.Remaining(Body)>0||(status&2)!=0;
            if(blocked||(status&0x200000)!=0){Combat.Cancel();Motion.Stop();DeathMarkWorld.ChangeMode(Body,1);}
            if(blocked){ClearEvents();Blocked=true;return;}
            if(Blocked){Blocked=false;Idle(1);}
            bool attacking=Combat.Current!=null;int contacts=Combat.Current?.Contacts??0;
            if(attacking){Combat.Advance(frame,at);Contacts+=Math.Max(0,(Combat.Current?.Contacts??contacts)-contacts);if(Combat.Current==null){Idle(1);}}
            else if((Motion.Motion.Flags&0x20)!=0){Motion.Advance();}
            if(Events.Unit(checked((int)Body.Serial.Value)).Length==0&&Combat.Current==null&&(Motion.Motion.Flags&0x20)==0){Idle(1);}
        });
        Body.AIObject?.AITimer.Stop();
    }
    private BaseDoor Door()
    {
        BaseDoor best=null;double distance=double.MaxValue;
        foreach(var door in Body.Map.GetItemsInRange<BaseDoor>(Body.Location,1))
        {
            if(door.Open||Math.Abs(door.Z-Body.Z)>=16){continue;}double d=Body.GetDistanceToSqrt(door);
            if(d<distance||d==distance&&(best==null||door.Serial.Value<best.Serial.Value)){best=door;distance=d;}
        }
        return best;
    }
    private void Think()
    {
        var header=DeathMarkWorld.Header(Body);var current=header.Capture();int actor=checked((int)Body.Serial.Value);
        var world=new GoatmanNativeSearch(Body,Owner);var primary=GenericPetSearchTables.Bind(1).Monster(1,SourceClass);
        var path=DeathMarkWorld.Path(Body);var prePath=new GenericPetAiPrePath(path.Room,(int)(path.X>>16),(int)(path.Y>>16),path.Pattern58,
            (Motion.Motion.Flags&0x20)!=0?1:0,path.Collision68);
        var pre=new GenericPetAiPreServices(()=>1,id=>
        {
            if(id!=actor&&Server.World.FindItem((Serial)(uint)id)is BaseDoor door)
            {return new(new(2,true,door.X*2,door.Y*2),id,1,1,4,0,GenericPetMetadata.Table.StateCount,false,0,0,null,null);}
            if(id!=actor){throw new NotSupportedException("Candidate saved-follow target preprocessing is not admitted");}
            return GenericPetNativeControlStates.Preprocess(Body,new(world.Position(actor),SourceClass,1,DeathMarkWorld.State(Body).Mode,4,0,
                GenericPetMetadata.Table.StateCount,true,-1,0,prePath,Motion.Unit));
        },control=>ReferenceEquals(control,header.Control)?header.Memory:throw new NotSupportedException("Wrong original AI control"),
            row=>row==SourceClass+1?primary.Flags60:throw new NotSupportedException("Wrong source primary row token"),
            (selector,cls)=>selector==1&&cls==SourceClass?(GenericPetSecondaryTables.Table.Bind(1).Row(1,SourceClass).Flags4&8)!=0:null,
            (kind,id)=>new GenericPetWorldIdentity(current.Game).Resolve(current.Game,kind,id),
            (_,_)=>throw new NotSupportedException("Candidate nonempty saved-follow distance needs its source producer"),
            (_,_,_,_,_,_)=>throw new NotSupportedException("Candidate nonempty saved-follow point is not admitted"),
            (_,_,_,_)=>throw new NotSupportedException("Candidate nonempty saved-follow command is not admitted"),
            (_,x,y,_,_)=>checked((ushort)(Door()!=null?0x800:DemonPathWorld.Collision(Body,x,y))),
            (_,limit)=>Door()is{} door?checked((int)door.Serial.Value):0,
            (_,id)=>Server.World.FindItem((Serial)(uint)id)is BaseDoor,
            (_,id)=>Server.World.FindItem((Serial)(uint)id)is BaseDoor{Locked:false},
            (_,id)=>((BaseDoor)Server.World.FindItem((Serial)(uint)id)).Use(Body),(_,ticks)=>Idle(ticks));
        var entry=new GenericPetAiEntryServices(()=>new(0,0,1,0),id=>id==actor?new(1,SourceClass,true,header.Control,0,0):throw new NotSupportedException("Candidate actor identity"),
            (selector,cls)=>selector==1&&cls==SourceClass?SourceClass+1:0,(selector,cls)=>selector==1&&cls==SourceClass?SourceClass+1:0,GenericPetAiOverrideTables.At,
            tick=>GenericPetAiPreprocess.Run(actor,tick,pre),(_,state)=>GenericPetAiTables.Original(Body,state,_=>false).Type,
            (_,_)=>throw new NotSupportedException("Normal Goatman descriptor has no target-stage callback"),
            tick=>tick.Target==0?false:throw new NotSupportedException("Normal Goatman target stage changed"),
            (_,_)=>throw new NotSupportedException("Queued functors are not admitted"),(_,_,_)=>throw new NotSupportedException("Queued functor execution is not admitted"),
            _=>throw new NotSupportedException("Queued functor release is not admitted"),(_,_,_)=>throw new NotSupportedException("Normal candidate has no startup override"),
            (function,tick)=>{if(function!=0x4b60c0){throw new NotSupportedException("Wrong source root");}RunRoot(world);});
        try{_=GenericPetAiEntry.Run(actor,0,entry);}
        finally{path.Collision68=prePath.Collision;Motion.SyncEvents();}
    }
    private void RunRoot(GoatmanNativeSearch world)
    {
        int actor=checked((int)Body.Serial.Value);var random=new GenericPetActionRandom(NpcEquipmentRandom.Require(Body).Seed);Motion.Random=random;Motion.SyncEvents();
        var source=Species;
        if(source.ClassId!=SourceClass){throw new NotSupportedException("Missing captured Goatman movement metadata");}
        var actions=new GenericPetActionServices((_,_,_)=>throw new NotSupportedException("Goatman has no type6 skill"),world.Position,
            (x,y)=>GenericPetSearch.Squared(world.Position(actor),x,y),(id,mask)=>world.Search.Callback.Collision27c710(actor,id,mask)!=0,
            id=>Server.World.FindMobile((Serial)(uint)id)is BaseCreature target&&ReviveMeleeGeometry.Within(Body,target,0),
            ()=>{},(_,_,_,_,_)=>throw new InvalidOperationException(),()=>{},_=>source.Flags241,(_,_)=>throw new InvalidOperationException(),
            (_,_,_,_,_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),
            (_,_)=>throw new InvalidOperationException(),id=>world.Search.Callback.Valid372ec0(actor,id,1)!=0,_=>0);
        actions=Motion.BindActions(Combat.Bind(actions,()=>Pump.Position.Frame,()=>At));var timed=actions;
        actions=actions with{ExecuteSkill=(mode,skill,target,x,y,selected)=>{Motion.Stop();return timed.ExecuteSkill(mode,skill,target,x,y,selected);},
            ExecuteMode=request=>{Motion.Stop();return timed.ExecuteMode(request);}};
        var leaves=new GenericPetCombatServices(()=>throw new InvalidOperationException(),_=>0,_=>0,(_,_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
            ()=>DeathMarkWorld.Path(Body).Size5c,()=>world.Room(actor),
            (room,x,y,property,mask)=>mask==64&&property==DemonPathWorld.Size(Body)?AlliedOverlap.Blocked(Body):throw new NotSupportedException("Native standing-block mask is unbound"),
            ()=>GenericPetMovement.Prepare(Motion.Unit,0,0,0),(x,y,radius,hint,flags)=>actions.Move(x,y,checked((byte)radius),hint,flags),actions.MoveFinished,_=>throw new InvalidOperationException(),
            (_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException());
        var binding=world.Bind(random,leaves,Motion.Follow(world),Motion.FollowExecution(world),actions,
            (_,_)=>0,Idle); // Explicit candidate scratch tail,not recovered native stack bytes.
        var bound=Events.Bind(binding.Context,binding.Services,actor);GenericPetDecisions.Think(bound.Context,bound.Services);Decisions++;
    }
}
