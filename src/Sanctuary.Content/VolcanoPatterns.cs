// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal sealed record VolcanoEmission(int X,int Y,uint Memory,ulong Seed,int Rank)
{
    public int Flags=>1312;
    public int Missile=>481;
    public int Graphics=>30;
}
internal sealed record VolcanoDebrisFlight(int X,int Y,int TargetX,int TargetY,int Rank,int Velocity,int Remaining,double Vx,double Vy)
{
    public int Total=>16;
    public int CollisionMode=>0;
}

// Source do28's emitter memory is distinct from the missile's collision RNG.
// This is a request/flight plan; it does not enable learned Volcano or create
// world actors. Modern hit-delay grouping remains separate acceptance work.
internal static class VolcanoPatterns
{
    internal static VolcanoEmission Emit(int x,int y,int elapsed,uint memory,int rank,int interval,int range)
    {
        if(rank is <1 or >255 || interval<1 || range is <1 or >32767)
        {throw new NotSupportedException("Unmapped Volcano emission bounds");}
        if(elapsed<=2 || elapsed>=128 || elapsed%interval!=0){return null;}
        ulong seed=SourceRandom.InitLow(unchecked((int)memory));
        int bound=checked(2*range+1);
        int dx=(int)SourceRandom.Limited(ref seed,bound)-range;
        int dy=(int)SourceRandom.Limited(ref seed,bound)-range;
        return new(checked(x+dx),checked(y+dy),(uint)seed,seed,rank);
    }
    internal static VolcanoDebrisFlight Flight(int x,int y,int targetX,int targetY,int rank)
    {
        if(rank is <1 or >255){throw new NotSupportedException("Unmapped Volcano debris rank");}
        if(x==targetX && y==targetY){targetX=checked(targetX+1);targetY=checked(targetY+1);}
        int dx=checked(targetX-x),dy=checked(targetY-y);
        if(Math.Abs((long)dx)>=100 || Math.Abs((long)dy)>=100){return null;}
        // Current air row has CanSlow absent. Graphics30 is not a server
        // velocity/height input. Keep total16 distinct from distance lifetime.
        int velocity=SlowMissilePayload.VelocityFp8(14,0,rank,1312,0,false,100);
        var vector=MissileVectors.VelocityFp8(dx,dy,velocity);
        return new(x,y,targetX,targetY,rank,velocity,LobProjectiles.Lifetime(dx,dy,velocity),vector.X,vector.Y);
    }
}
