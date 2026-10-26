// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

internal sealed record ActorMotionState(uint X,uint Y,Point2D[] Points,Point2D FinalTarget,int Index,int Velocity,int MaximumVelocity,
    int Acceleration,int AccelerationCounter,Point2D Direction,Point2D Vector,int Distance,int Flags,int StepsCount,Point2D[] SavedSteps,int CollidedMask,int PathType=15);
internal sealed record ActorMotionResult(ActorMotionState State,bool Moving,bool RepathRequired);

// Server monster / point target / path2,5,6,13,15 component. The caller supplies source
// collision-mask cells. No live actor, facing animation, AI clock or path-repair
// policy is selected here; a failed declared repath is reported explicitly.
internal static class SourceActorMotion
{
    internal static (int Velocity,int Animation) Speed(int baseVelocity,int baseAnimation,int velocityPercent,int fasterMove)
    {
        int denominator=checked(fasterMove+150);
        if(fasterMove!=0 && denominator==0){throw new NotSupportedException("Undefined legacy movement percentage");}
        int extra=fasterMove==0?0:checked(fasterMove*150)/denominator;
        int percent=Math.Max(25,checked(velocityPercent+extra));
        return (checked(percent*checked(baseVelocity*256))/100,Math.Clamp(checked(percent*baseAnimation)/100,0,32767));
    }
    private static uint Center(int cell)=>unchecked(((uint)cell<<16)|32768);
    private static uint Fit(uint value)=>(value&0xffff0000)|32768;
    private static Point2D Cell(ActorMotionState s)=>new((int)(s.X>>16),(int)(s.Y>>16));
    private static void Validate(ActorMotionState s)
    {
        if(s?.Points==null || s.SavedSteps==null || s.Points.Length>77 || s.Index<0 || s.Index>s.Points.Length
            || s.Distance is <0 or >77 || s.StepsCount is <0 or >10 || s.SavedSteps.Length>10
            || s.PathType is not(2 or 5 or 6 or 13 or 15) || (s.Flags&~0x20928)!=0
            || (s.Flags&0x900)!=(s.PathType==15?0x800:s.PathType==13?0x100:0)
            || s.AccelerationCounter is <0 or >4 || s.Velocity<0 || s.MaximumVelocity<0)
        {throw new ArgumentException("Unsupported source actor path state");}
        foreach(var p in s.Points){if(p.X is <0 or >65535 || p.Y is <0 or >65535){throw new ArgumentException("Invalid source waypoint");}}
    }
    internal static ActorMotionState DirectionToNext(ActorMotionState state)
    {
        Validate(state);int index=state.Index;
        if(index>=state.Points.Length){throw new ArgumentException("Source direction requires a current waypoint");}
        while(index<state.Points.Length && Center(state.Points[index].X)==state.X && Center(state.Points[index].Y)==state.Y){index++;}
        if(index>=state.Points.Length){return state with{Index=index,Direction=new(0,0),Vector=new(0,0),Velocity=0};}
        int dx=checked((int)((long)Center(state.Points[index].X)-state.X)),dy=checked((int)((long)Center(state.Points[index].Y)-state.Y));
        // The legacy tangent numerator is signed int32. Refuse its overflow
        // region rather than silently claiming wide-math binary equivalence.
        if(Math.Max(Math.Abs((long)dx),Math.Abs((long)dy))>int.MaxValue/127){throw new NotSupportedException("Unresolved legacy direction overflow");}
        var direction=MissileVectors.Direction(dx,dy);
        return state with{Index=index,Direction=direction,Vector=new(checked(direction.X*state.Velocity)>>8,checked(direction.Y*state.Velocity)>>8)};
    }
    private static ActorMotionState Reset(ActorMotionState s)=>s with{X=Fit(s.X),Y=Fit(s.Y),Flags=s.Flags&~0x20,Points=[],Index=0,Vector=new(0,0)};
    internal static ActorMotionResult Advance(ActorMotionState state,Func<int,int,int> collision)
    {
        Validate(state);ArgumentNullException.ThrowIfNull(collision);
        var s=state with{Flags=state.Flags&~8};bool repath=false;
        if((s.Flags&0x20)==0 || s.Points.Length==0 || s.Velocity==0){return new(Reset(s),false,false);}
        if(s.Index>=s.Points.Length)
        {
            repath=s.PathType is not(5 or 6) && Cell(s)!=s.FinalTarget;
            // The declared source repath service fails. An actor controller
            // must consume this request and supply the complete repair path.
            return new(Reset(s),false,repath);
        }
        if(s.Acceleration!=0)
        {
            int counter=s.AccelerationCounter+1;
            if(counter>=5)
            {
                int velocity=checked(s.Velocity+s.Acceleration),acceleration=s.Acceleration;
                if(velocity>s.MaximumVelocity){velocity=s.MaximumVelocity;acceleration=0;}
                else{velocity=Math.Max(0,velocity);}
                s=s with{Velocity=velocity,Acceleration=acceleration,AccelerationCounter=0};
            }
            else{s=s with{AccelerationCounter=counter};}
        }
        int magnitude=checked(1024*s.Velocity)>>6;
        var vector=new Point2D(checked(magnitude*s.Direction.X)>>12,checked(magnitude*s.Direction.Y)>>12);
        s=s with{Vector=vector};
        if(vector.X==0 && vector.Y==0){return new(Reset(s),false,false);}
        int dx=checked((int)((long)Center(s.Points[s.Index].X)-s.X)),dy=checked((int)((long)Center(s.Points[s.Index].Y)-s.Y));
        long reach=Math.Max(Math.Abs((long)vector.X),Math.Abs((long)vector.Y));bool waypoint=Math.Abs((long)dx)<=reach && Math.Abs((long)dy)<=reach;
        if(!waypoint){dx=vector.X;dy=vector.Y;}
        if((long)s.X+dx is <0 or >uint.MaxValue || (long)s.Y+dy is <0 or >uint.MaxValue)
        {throw new NotSupportedException("Unverified source actor coordinate wrap");}
        uint endX=unchecked(s.X+(uint)dx),endY=unchecked(s.Y+(uint)dy);int collided=0;
        if((s.X>>16)!=(endX>>16) || (s.Y>>16)!=(endY>>16))
        {
            // Same ordered, downshifted integer-cell crossings already used by
            // missile movement. Actor impacts differ: any tested mask blocks,
            // recentering on the previous cell, with no path-change flag set.
            var crossed=SourceMissileSteps.Crossings(s.X,s.Y,dx,dy).Cells;
            int steps=0;bool save=(s.Flags&0x20000)!=0;var saved=new List<Point2D>();var previous=Cell(s);
            s=s with{Distance=s.Distance>0?s.Distance-1:0,StepsCount=0};
            foreach(var cell in crossed)
            {
                collided|=collision(cell.X,cell.Y)&0x3c01;
                if(collided!=0)
                {
                    s=s with{X=Center(previous.X),Y=Center(previous.Y),Index=s.Points.Length,StepsCount=steps,
                        SavedSteps=save?saved.ToArray():s.SavedSteps,CollidedMask=collided};
                    return new(Reset(s),false,false);
                }
                steps++;previous=new(cell.X,cell.Y);if(save){saved.Add(previous);}
            }
            s=s with{StepsCount=steps,SavedSteps=save?saved.ToArray():s.SavedSteps,Flags=steps>0?s.Flags|8:s.Flags};
        }
        s=s with{X=endX,Y=endY,Index=s.Index+(waypoint?1:0),CollidedMask=collided};
        if(s.Index<s.Points.Length){s=DirectionToNext(s);}
        return s.Index<s.Points.Length?new(s,true,false):new(Reset(s),false,false);
    }
}
