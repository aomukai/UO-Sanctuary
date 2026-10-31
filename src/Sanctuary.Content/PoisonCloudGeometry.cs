// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

// Shared MISSMODE_CreatePoisonCloudHitSubmissiles direction table. Explosion
// uses mainStep2/subStep0/loops0; Plague Javelin also emits the outer ring.
internal static class PoisonCloudGeometry
{
    internal static FanRay[] Pattern(int mainStep,int subStep,int loops)
    {
        if(subStep<0||loops<0){throw new NotSupportedException("Negative poison cloud emission parameters");}
        int[] xs=[0,1,2,2,2,2,2,1,0,-1,-2,-2,-2,-2,-2,-1],ys=[2,2,2,1,0,-1,-2,-2,-2,-2,-2,-1,0,1,2,2];
        uint flags=loops>0?0x1Fu:0x17u;var rays=new List<FanRay>();
        for(int i=0;i<16;i+=Math.Max(1,mainStep)){rays.Add(new(xs[i],ys[i],flags,0));}
        if(subStep>0){for(int i=0;i<15;i+=subStep){rays.Add(new(xs[i+1],ys[i+1],flags,0));}}
        return rays.ToArray();
    }
}
