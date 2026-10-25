// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

// Stationary monster, zero target lead, pattern0/mask0x3c01, temporary path2.
// Complete source composition proves identical points/queries to the existing
// toward kernel in this context. This is a reachability query, not a moving
// actor path or a claim that arbitrary unit targets equal point targets.
internal static class AssassinSentryUnitPath
{
    internal static Point2D[] Points(uint x,uint y,Point2D target,int budget,ActorPathRoom room,Func<int,int,int> collision)
    {
        if(target.X is <0 or >65535 || target.Y is <0 or >65535){throw new ArgumentOutOfRangeException(nameof(target));}
        // The unit initializer rejects either-zero target coordinates before
        // updating its path. A point initializer alone would not do this.
        if(target.X==0 || target.Y==0){return [];}
        var motion=new ActorMotionState(x,y,[],new(0,0),0,0,0,0,0,new(0,0),new(0,0),0,0,0,[],0,2);
        return SourceActorPath.Initialize(new(motion,target,new(0,0)),budget,room,collision).Motion.Points;
    }
    internal static bool Probe(AssassinSentryBody body,Mobile target)
    {
        if(body is not{Deleted:false,Alive:true} || target is not{Deleted:false,Alive:true}
            || body.Map==null || body.Map==Map.Internal || target.Map!=body.Map || Math.Abs(target.Z-body.Z)>=16)
        {throw new NotSupportedException("Sentry unit-path query requires live same-room native actors");}
        if(body.Map.Width*2>65536 || body.Map.Height*2>65536)
        {throw new NotSupportedException("Sentry native map exceeds source path coordinates");}
        uint x=((uint)body.X*2<<16)|32768,y=((uint)body.Y*2<<16)|32768;
        // Generic monster allocation uses distance14. Source path2 clears
        // footprint-removal/adjustment flags; target footprints stay in Mask.
        // No stored Control.Target, CachedAim, RNG, phase or pose is rewritten.
        return Points(x,y,new(target.X*2,target.Y*2),14,new(0,0,body.Map.Width*2,body.Map.Height*2),
            (cx,cy)=>BladeWorldSpace.Mask(body.Map,body.Z,cx,cy,body)).Length!=0;
    }
}
