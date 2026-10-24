// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Logging;

namespace Sanctuary;

internal sealed record BladeControlState(int Frame,int Mode,int NextThink,bool MovementEvent,BladeAiState Ai,
    Point2D Origin,Point2D Destination,int Duration,int Speed,ulong Seed,bool HasCommand=true,int Creates=0,int Thinks=0,int Moves=0);
internal sealed record BladeFrameResult(BladeControlState Control,ActorMotionState Motion);

// Single source monster event phase. The caller owns the25Hz frame clock and
// provides current room/collision, owner position, movement stats and first
// missile creation. This does not substitute a dummy missile or player attack.
internal static class BladeSentinelController
{
    internal static BladeControlState Start(Point2D origin,Point2D destination,int duration,int speed,ulong seed)
    {
        if(duration<=0 || speed<=0){throw new ArgumentOutOfRangeException();}
        return new(0,1,1,false,new(),origin,destination,duration,speed,seed);
    }
    internal static BladeFrameResult Advance(BladeControlState control,ActorMotionState motion,ActorPathRoom room,
        Func<int,int,int> collision,Point2D? owner,Action createMissile)
    {
        ArgumentNullException.ThrowIfNull(control);ArgumentNullException.ThrowIfNull(collision);ArgumentNullException.ThrowIfNull(createMissile);
        if(control.Frame<0 || control.Mode is not(0 or 1 or 2) || control.Ai==null || control.Duration<=0 || control.Speed<=0)
        {throw new ArgumentException("Unsupported blade controller state");}
        var c=control with{Frame=checked(control.Frame+1)};var m=motion;
        bool Walk(Point2D target,ref BladeWalkParameters pending,bool deleteAiOnFailure)
        {
            var take=pending.TakeWalk();pending=take.Remaining;
            bool success=BladeWalkParameters.Calculate(take.Request,request=>
            {
                if(request.Velocity!=0){throw new NotSupportedException("Live blade velocity override stat list is not connected");}
                var input=m with{Flags=(m.Flags&0xFF)|(request.Path==15?0x800:request.Path==13?0x100:0),PathType=request.Path,Distance=request.Distance};
                m=SourceActorPath.Initialize(new(input,target,new(0,0)),request.Distance,room,collision).Motion;
                return m.Points.Length!=0;
            });
            // Complete mode change deletes the old per-frame event and starts
            // a new walk event if entry succeeded. Its first update is next frame.
            c=c with{Mode=success?2:1,MovementEvent=success};
            if(success){m=m with{Velocity=c.Speed,MaximumVelocity=c.Speed};}
            else
            {
                if(c.NextThink<=c.Frame){c=c with{NextThink=checked(c.Frame+15)};}
                if(deleteAiOnFailure){c=c with{NextThink=0};}
            }
            return success;
        }
        void Think()
        {
            c=c with{NextThink=0,Thinks=c.Thinks+1};var pending=new BladeWalkParameters();
            var result=BladeSentinelAi.Think(c.Ai,c.Frame,c.Duration,true,true,c.HasCommand,c.Origin,c.Destination,
                ()=>{c=c with{Creates=c.Creates+1};createMissile();},()=>pending=pending.Set(15,0,20),
                target=>Walk(target,ref pending,true),()=>
                {
                    ulong seed=c.Seed;var point=BladeSentinelAi.OwnerDestination(owner??new((int)(m.X>>16),(int)(m.Y>>16)),owner.HasValue?5:2,ref seed);
                    c=c with{Seed=seed};return Walk(point,ref pending,false);
                });
            c=c with{Ai=result.State};
            if(result.Dead){c=c with{Mode=0,NextThink=0,MovementEvent=false};}
            else if(result.IdleFrames!=0){c=c with{Mode=1,NextThink=checked(c.Frame+result.IdleFrames)};}
        }
        if(c.MovementEvent)
        {
            if(c.Mode==2)
            {
                var step=SourceActorMotion.Advance(m,collision);
                if(step.RepathRequired){throw new NotSupportedException("Blade successful repair service is not connected");}
                m=step.State;c=c with{Moves=c.Moves+1};
                if(!step.Moving){c=c with{Mode=1};Think();}
            }
            else{c=c with{MovementEvent=false};} // Neutral callback removes the old event.
        }
        if(c.NextThink==c.Frame && c.Mode!=0){Think();}
        return new(c,m);
    }
}

// A controller is never acquired by a learned skill until its real creation,
// following missile/stat supplier, trap cap and combat/input consumers exist.
public static class BladeSentinelActors
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeSentinelActors));
    private static readonly Dictionary<Serial,BladeSentinelBody> Active=new();
    internal static Func<BladeSentinelBody,AssassinTrapCreationStats> CreateFollowingMissile;
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static void Track(BladeSentinelBody body)=>Active[body.Serial]=body;
    internal static void Forget(BladeSentinelBody body)=>Active.Remove(body.Serial);
    internal static void Tick(DateTime at)
    {
        foreach(var body in Active.Values.OrderByDescending(x=>x.Serial.Value).ToArray())
        {
            if(body.Deleted){Forget(body);continue;}
            try{body.AdvanceController(at);}
            catch(Exception e){body.SuspendController(e.Message);Log.Error(e,"Prepared blade controller suspended: {Serial}",body.Serial);}
        }
    }
}
