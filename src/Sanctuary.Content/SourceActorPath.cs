// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;

namespace Sanctuary;

internal sealed record ActorPathState(ActorMotionState Motion,Point2D Target,Point2D PreviousTarget);
internal readonly record struct ActorPathRoom(int X,int Y,int Width,int Height)
{
    internal bool Contains(Point2D p)=>p.X>=X && p.Y>=Y && (long)p.X<(long)X+Width && (long)p.Y<(long)Y+Height;
}

// Server monster / point target / path15 / pattern0 component. The complete
// reconstructed initializer is marked BROKEN upstream: its bounded execution
// is supplementary evidence, not modern executable equivalence. Successful
// repair uses other path types and is deliberately not substituted here.
internal static class SourceActorPath
{
    internal static ActorPathState Initialize(ActorPathState state,int distance,ActorPathRoom room,Func<int,int,int> collision)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(collision);
        if(state.Motion==null || distance is <0 or >77 || room.X<0 || room.Y<0 || room.Width<=0 || room.Height<=0
            || (long)room.X+room.Width>65536 || (long)room.Y+room.Height>65536
            || state.Target.X is <0 or >65535 || state.Target.Y is <0 or >65535)
        {throw new ArgumentException("Unsupported source point-path input");}
        // Validate the supplied path without advancing it or selecting repair.
        SourceActorMotion.Advance(state.Motion with{Flags=state.Motion.Flags&~0x20},(_,_)=>throw new InvalidOperationException());
        var motion=state.Motion with{CollidedMask=0};var origin=new Point2D((int)(motion.X>>16),(int)(motion.Y>>16));
        if(!room.Contains(origin)){throw new NotSupportedException("Source start room is not available");}
        ActorPathState Fail()=>state with{Motion=motion with{Points=[],Index=0,Flags=motion.Flags&~0x20}};
        if(origin==new Point2D(0,0) || state.Target==new Point2D(0,0) || origin==state.Target
            || Math.Abs(origin.X-state.Target.X)>100 || Math.Abs(origin.Y-state.Target.Y)>100 || !room.Contains(state.Target))
        {return Fail();}
        motion=motion with{FinalTarget=state.Target};
        var points=SourceWallFollow.Find(origin,state.Target,distance,false,(x,y)=>(collision(x,y)&0x3c01)!=0);
        if(points.Length==0){return Fail();}
        motion=SourceActorMotion.DirectionToNext(motion with{Points=points,Index=0});
        if(motion.Index>=points.Length){return Fail();}
        // A crossing into another source room requires recache/geometry support,
        // rather than silently enabling an unimplemented 0x1 motion branch.
        if(points.Any(p=>!room.Contains(p))){throw new NotSupportedException("Cross-room source path needs room recache");}
        motion=motion with{Flags=(motion.Flags&~1)|0x20};
        return new(motion,points[^1],state.Target);
    }
}
