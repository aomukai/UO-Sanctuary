// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

internal sealed record MiasmaLaunchPlan(int Flags,int X,int Y,int AimX,int AimY,int Velocity256,
    int MaximumVelocity256,int Frames,int ActivateFrame,int Skill,int Rank);

// Ordinary direct-missile wrapper (zero launch offset) and generic factory.
// This prepares a request; it does not enable casting or implement hit41.
internal static class MiasmaLaunch
{
    internal static MiasmaLaunchPlan Prepare(Point2D from,Point2D? selected,Point2D cursor,Point2D? explicitAim,
        int rank,int velocityPercent,bool hasRow,bool hasRoom,Func<bool> allocate)
    {
        if(rank is <1 or >255||velocityPercent is <0 or >100||allocate==null){throw new ArgumentOutOfRangeException();}
        int F(string key)=>NecromancerSummons.Source("miasmabolt",key);
        if(F("Vel")!=20||F("MaxVel")!=20||F("VelLev")!=0||F("LevRange")!=0||F("Range")!=50||F("Activate")!=0
            ||F("CollideType")!=3||F("CanSlow")!=1||F("LastCollide")!=1||F("pSrvDoFunc")!=1||F("pSrvHitFunc")!=41)
        {throw new NotSupportedException("Unsupported Miasma launch row");}
        var aim=explicitAim is{X:not 0,Y:not 0} supplied?supplied:selected??cursor;
        // The wrapper rejects an absent coordinate before calling the factory.
        if(aim.X==0||aim.Y==0||!hasRow||!hasRoom){return null;}
        int velocity=SlowMissilePayload.VelocityFp8(20,0,rank,33,0,true,velocityPercent);
        if(velocity!=0)
        {
            if(aim==from){aim=new(aim.X+1,aim.Y+1);}
            if(Math.Abs((long)aim.X-from.X)>=100||Math.Abs((long)aim.Y-from.Y)>=100){return null;}
        }
        if(!allocate()){return null;}
        return new(33,from.X,from.Y,aim.X,aim.Y,velocity,20<<8,50,50,395,rank);
    }
}
