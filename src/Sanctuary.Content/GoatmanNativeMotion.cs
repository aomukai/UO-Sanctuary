// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Candidate native mode/path/follow binding. Defaults and UO room/region/warp
// projection are explicit proposal choices,not current D2 constructor proof.
internal sealed class GoatmanNativeMotion
{
    private readonly WarlockGoatmanBody Body;
    private readonly PlayerMobile Owner;
    private readonly Func<int> Frame;
    private readonly Func<DateTime> Clock;
    private readonly Action<int> Idle;
    private readonly GenericPetTimedEvents Events;
    private readonly Func<GenericPetEventClock> EventClock;
    private readonly byte Flags241;
    private readonly Dictionary<Region,int> Regions=new(ReferenceEqualityComparer.Instance);
    internal readonly GenericPetMovementUnit Unit=new(){HasAux88=true,Aux88Word1c=-1};
    internal readonly GenericPetMovementServices Services;
    internal ActorMotionState Motion{get;private set;}
    internal int Paths,Steps,Warps;
    internal GoatmanNativeMotion(WarlockGoatmanBody body,PlayerMobile owner,GenericPetTimedEvents events,
        Func<int> frame,Func<DateTime> clock,Func<GenericPetEventClock> eventClock,Action<int> idle,byte flags241)
    {
        Body=body;Owner=owner;Events=events;Frame=frame;Clock=clock;EventClock=eventClock;Idle=idle;Flags241=flags241;Reset();
        Services=new(_=>GenericPetMetadata.Table.StateCount,mode=>new(0,0,0,mode),Execute,
            id=>Events.Remove(id),(type,at,p0,p1,p2)=>Schedule(type,at,p0,p1,p2),
            radius=>{var p=Cell();_=GenericPetFollowMovement.RandomPoint(Unit,Random,p.X,p.Y,radius,0,4,Services);},Idle);
    }
    internal GenericPetActionRandom Random{get;set;}
    internal Point2D Cell()=>new((int)(Motion.X>>16),(int)(Motion.Y>>16));
    internal void Reset()
    {
        var path=DeathMarkWorld.Path(Body);Motion=DruidRavenMotion.Center((int)(path.X>>16),(int)(path.Y>>16))with{X=path.X,Y=path.Y,PathType=13,Flags=0x100};
    }
    internal void Stop()
    {
        Motion=Motion with{Flags=Motion.Flags&~0x20,Points=[],Index=0,Velocity=0,MaximumVelocity=0,Vector=new(0,0)};
        if(DeathMarkWorld.State(Body)is{Mode:2 or 15}){DeathMarkWorld.ChangeMode(Body,1);}
    }
    internal void SyncEvents()
    {
        Unit.Events.Clear();foreach(int id in Events.Unit(checked((int)Body.Serial.Value)))
        {var e=Events.Node(id);Unit.Events.Add(new(id,checked((byte)e.Type),e.Frame));}
        if(GenericPetMetadata.Table.State(60)?.Name!="decrepify"){throw new NotSupportedException("Native movement Decrepify state identity differs");}
        Unit.Aux88FlagsB34=NecromancerEffects.Curse(Body)?.Ability==CombatAbility.Decrepify?0x10000000u:0;
    }
    private void Schedule(int type,int at,int p0,int p1,int p2)
    {
        if(type!=2){throw new NotSupportedException("Native movement only produces its source AI event");}
        Events.Schedule(checked((int)Body.Serial.Value),type,at,EventClock,
            ()=>throw new NotSupportedException("Deferred uninterruptible AI scheduling needs its source completion consumer"),p0,p1,p2);SyncEvents();
    }
    private int Execute(GenericPetModeRequest request)
    {
        if(request.Opaque is not int mode||mode is not(2 or 15)){throw new NotSupportedException("Native path mode is unbound");}
        var target=request.Target==0?new Point2D(request.X,request.Y):Server.World.FindMobile((Serial)(uint)request.Target)is{} m
            ?PetOwnerRoutes.Cell(m):throw new NotSupportedException("Native movement target disappeared");
        if(target.X<=0||target.Y<=0||target.X>=Body.Map.Width*2||target.Y>=Body.Map.Height*2){return 0;}
        int type=Unit.Word20==0?13:Unit.Word20,budget=Unit.Byte28==0?5:Unit.Byte28;
        if(type is not(2 or 5 or 6 or 7 or 13 or 15)){throw new NotSupportedException("Native movement path type is not qualified");}
        var speed=GoatmanNpcBodyActions.Movement(Body,Clock(),Unit.Word24);
        var input=Motion with{PathType=type,Flags=type==13?0x100:type==15?0x800:0,Points=[],Index=0,Distance=budget,
            Velocity=speed.Velocity,MaximumVelocity=speed.Velocity,Acceleration=0,AccelerationCounter=0};
        // One native facet is the supplied planning surface;registered source
        // room changes still commit through the qualified walking adapter.
        var plan=SourceActorPath.Initialize(new(input,target,input.FinalTarget),budget,new(1,1,Body.Map.Width*2-1,Body.Map.Height*2-1),
            (x,y)=>PetGroundWorld.Collision(Body,x,y));
        if(plan.Motion.Points.Length==0||(plan.Motion.Flags&0x20)==0){return 0;}
        Motion=plan.Motion;DeathMarkWorld.ChangeMode(Body,mode);Body.Direction=Body.GetDirectionTo(new Point3D((target.X+1)/2,(target.Y+1)/2,Body.Z));Paths++;return 1;
    }
    internal bool Advance()
    {
        if((Motion.Flags&0x20)==0){return false;}
        var speed=GoatmanNpcBodyActions.Movement(Body,Clock(),Unit.Word24);
        Motion=SourceActorMotion.DirectionToNext(Motion with{Velocity=speed.Velocity,MaximumVelocity=speed.Velocity});
        var result=GoatmanNativeWalking.Advance(Body,Motion);Motion=result.State;Steps++;
        if(!result.Moving){Stop();Idle(1);}return result.Moving;
    }
    private int RegionAt(int x,int y)
    {
        if(x<=0||y<=0||x>=Body.Map.Width*2||y>=Body.Map.Height*2){return 0;}
        var region=Region.Find(new((x+1)/2,(y+1)/2,Body.Z),Body.Map);
        if(!Regions.TryGetValue(region,out int id)){id=Regions.Count+1;Regions.Add(region,id);}return id;
    }
    private int RoomAt(int _,int x,int y)=>x>0&&y>0&&x<Body.Map.Width*2&&y<Body.Map.Height*2
        ?GenericPetNativePlacement.RoomHandle(Body.Map,x,y):0;
    private int Warp()
    {
        var owner=PetOwnerRoutes.Cell(Owner);
        int result=DeathMarkWorld.WarpPrepared(Body,new(0,true,owner.X,owner.Y,true,owner.X,owner.Y),new(
            ()=>RoomAt(0,owner.X,owner.Y),(room,x,y)=>RegionAt(x*5,y*5),_=>GenericPetSearchTables.MonsterCount,
            (room,region,cls)=>
            {
                var point=new GenericPetNearbyPoint(owner.X,owner.Y);
                int found=GenericPetNearbySearch.Find(room,point,2,0x1c09,0,4,1,new(RoomAt,r=>PetGroundWorld.Collision(Body,r.X,r.Y)));
                return new(found,point.X,point.Y);
            },RoomAt,(room,x,y,skip,_)=>DeathMarkWorld.PlacePrepared(Body,new(x,y),skip!=0),
            ()=>unchecked((uint)(ushort)owner.X|((uint)(ushort)owner.Y<<16)),()=>2,
            (room,x,y,size,mask)=>PetGroundWorld.Collision(Body,x,y),()=>{Reset();Warps++;},Idle));
        return result;
    }
    internal GenericPetActionServices BindActions(GenericPetActionServices actions)
    {
        var result=GenericPetMovement.Bind(actions,Unit,Random,Frame,Services);
        return result with{Move=(x,y,radius,hint,flags)=>GenericPetFollowMovement.RandomPoint(Unit,Random,x,y,radius,hint,checked((ushort)flags),Services)!=0};
    }
    internal GenericPetFollowServices Follow(GoatmanNativeSearch search)=>new(GenericPetSearch.Squared,
        _=>ClassMovement.IsMoving(Owner,Core.TickCount),(u,x,y)=>new(RoomAt(0,x,y)!=0,true,RegionAt(x,y)),
        _=>throw new InvalidOperationException("Follow selection must bind its executor"));
    internal GenericPetFollowExecutionServices FollowExecution(GoatmanNativeSearch search)
    {
        var owner=PetOwnerRoutes.Cell(Owner);var p=search.Position(checked((int)Body.Serial.Value));
        var leaves=new GenericPetFollowExecutionServices(()=>(owner.X,owner.Y),()=>RoomAt(0,owner.X,owner.Y),RoomAt,
            (room,x,y)=>RegionAt(x*5,y*5),(x,y)=>SourceDirection64.Calculate(new(unchecked((p.X<<16)+32768),unchecked((p.Y<<16)+32768)),
                new(unchecked((x<<16)+32768),unchecked((y<<16)+32768)),CleaveActionProposal.MathRule).Heading,
            ()=>Flags241,(_,_,_)=>throw new InvalidOperationException(),()=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
            (_,_,_,_,_)=>throw new InvalidOperationException(),(_,_,_,_,_,_,_)=>throw new InvalidOperationException(),
            (_,_,_,_,_,_)=>throw new InvalidOperationException(),(_,_,_,_)=>throw new InvalidOperationException(),Warp);
        return GenericPetMotionServices.Bind(leaves,Unit,p,Random,Frame,Services);
    }
}
