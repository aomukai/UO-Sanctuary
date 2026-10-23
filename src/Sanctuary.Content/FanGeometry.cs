// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

// Relative source-subtile aims and raw factory flags, shared by server do8 skills.
public readonly record struct FanRay(int X,int Y,uint Flags,int ActivationFrame);

public static class FanGeometry
{
    public static FanRay[] Create(int dx,int dy,int count,int central=0,int activationFrame=0)
    {
        if(count is <1 or >24 || central<0 || central>count)
        {throw new ArgumentOutOfRangeException(nameof(count),"Unsupported source fan count/central partition");}
        int x=dx,y=dy;
        if((long)x*x+(long)y*y<4){x*=4;y*=4;}
        if((long)x*x+(long)y*y<16){x*=2;y*=2;}
        (x,y)=(y,-x);
        while((long)x*x+(long)y*y>3){x/=2;y/=2;}
        int firstX=dx+count*x/-2,firstY=dy+count*y/-2;
        if(central==0){central=count;}
        int before=(count-central)/2;
        var rays=new FanRay[count];
        for(int i=0;i<count;i++)
        {rays[i]=new(firstX+i*x,firstY+i*y,(uint)(2080+(i<before || i>=before+central?0x10000:0)),activationFrame);}
        return rays;
    }
    public static (double X,double Y) Velocity(FanRay ray,int sourceVelocity)
    {
        // Separate missile-factory zero-target fallback, after do8 has generated aims.
        int x=ray.X,y=ray.Y;if(x==0 && y==0){x=y=1;}
        return MissileVectors.Velocity(x,y,sourceVelocity);
    }
}
