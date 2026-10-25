// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record PhoenixChainPath(Point2D[] Points,ulong Seed,int Frames);
internal readonly record struct PhoenixCurve(int X,int Y,int Packed,uint TargetSeed,ulong Seed);

// Source-backed Phoenix kernels, distinct from generic eight-direction bolts.
// Native world motion/collision and pinned executable packing remain separate.
internal static class MartialPhoenixPaths
{
    private static readonly byte[] Angles=[0,0,0,0,0,0,0,0,0,0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,2,2,2,2,2,3,3,3,3,3,3,3,3,3,3,3,3,3,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7];
    private static readonly byte[] Octants=[0,0,0,0,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,3,3,3,3,3,3,3,3,4,4,4,4,4,4,4,4,5,5,5,5,5,5,5,5,6,6,6,6,6,6,6,6,7,7,7,7,7,7,7,7,0,0,0,0];
    private static readonly Point2D[] Vectors=[new(2,0),new(2,2),new(0,2),new(-2,2),new(-2,0),new(-2,-2),new(0,-2),new(2,-2)];
    internal static int Direction(int dx,int dy)
    {
        long x=Math.Abs((long)dx),y=Math.Abs((long)dy);
        // Full older FP16 direction numerator is signed32: bound this domain.
        if(Math.Max(x,y)>258){throw new NotSupportedException("Phoenix source direction delta overflow domain");}
        int index=Math.Max(x,y)==0?0:(int)(127*Math.Min(x,y)/Math.Max(x,y));int angle=Angles[index];
        if(x>y){angle=(-1-angle)&15;}if(dy<0){angle=(-1-angle)&31;}if(dx>=0){angle=(-1-angle)&63;}
        return(angle+8)&63;
    }
    internal static PhoenixChainPath Chain(int low,int direction,int frames,int originX,int originY)
    {
        if(frames is <0 or >255 || direction is <0 or >63 || originX is <0 or >65535 || originY is <0 or >65535)
        {throw new ArgumentException("Invalid Phoenix chain input");}
        frames=Math.Min(frames,77);ulong seed=SourceRandom.InitLow(low);int sign=2*((int)SourceRandom.Roll(ref seed)&1)-1,step=2+(int)(SourceRandom.Roll(ref seed)%3);
        int x=originX,y=originY;var points=new Point2D[frames];
        for(int i=0;i<frames;i++)
        {
            direction+=sign*step;var delta=Vectors[Octants[direction&63]];x+=delta.X;y+=delta.Y;points[i]=new(unchecked((ushort)x),unchecked((ushort)y));
            if(i%15==0){sign=-sign;step=2+(int)(SourceRandom.Roll(ref seed)%3);}
        }
        return new(points,seed,frames);
    }
    internal static bool Turn(int elapsed,int interval)=>elapsed%Math.Max(1,interval)==0;
    internal static PhoenixCurve Curve(int low,int packed)
    {
        int x=unchecked((short)packed),y=unchecked((short)(packed>>16));ulong seed=SourceRandom.InitLow(low);uint draw=SourceRandom.Roll(ref seed);
        int flipX=(draw&1)!=0?-x:x,flipY=(draw&1)!=0?y:-y;
        int nextX=(flipY+4*x)/4,nextY=(flipX+4*y)/4;if(nextX==0){nextX=1;}if(nextY==0){nextY=1;}
        // Literal older signed-add packing carries negative X into high Y.
        // Current executable parity is uncertain; do not normalize this pair.
        int nextPacked=unchecked((short)nextX+(nextY<<16));return new(nextX,nextY,nextPacked,draw,seed);
    }
}
