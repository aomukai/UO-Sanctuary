// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
namespace Sanctuary;

internal readonly record struct RavenClock(int Action,int End);

internal static class DruidRavenMotion
{
    internal static RavenClock Clock(bool emerging)
    {
        string mode=emerging?"S1":"A1";
        int frames=NecromancerSummons.Source("druidhawk-animation",mode+"Frames"),speed=NecromancerSummons.Source("druidhawk-animation",mode+"Speed");
        if(frames is <1 or >144 || speed is <1 or >32767){throw new NotSupportedException("Unsupported Raven animation row");}
        var flags=new int[144];
        if(!emerging)
        {
            int action=NecromancerSummons.Source("druidhawk-animation","A1Action");
            if(action<0 || action>=frames){throw new NotSupportedException("Unsupported Raven action frame");}
            flags[action]=1;
        }
        var plan=SourceAnimationEvents.Initial(new(0,speed,checked(frames*256),flags,[]),0);
        return new(emerging?0:plan.Events.Single(x=>x.Type==SourceAnimationEvents.ModeChange&&x.Kind==1).Frame,
            plan.Events.Single(x=>x.Type==SourceAnimationEvents.EndAnimation).Frame);
    }
    // Distinct source helpers. Raven has size0; a target's source size comes
    // from its mapped creature profile, not the renderer's native tile width.
    internal static int Distance(Point2D a,Point2D b,int firstSize=0,int secondSize=0)
    {
        int size=firstSize/2+secondSize/2,x=Math.Max(0,Math.Abs(a.X-b.X)-size),y=Math.Max(0,Math.Abs(a.Y-b.Y)-size);
        return checked(Math.Min(x,y)+2*Math.Max(x,y))/2;
    }
    internal static int MeleeDistance(Point2D a,Point2D b,int targetSize)
    {
        if(targetSize is <0 or >3){throw new NotSupportedException("Unmapped Raven melee target size");}
        int x=Math.Abs(a.X-b.X),y=Math.Abs(a.Y-b.Y);
        if(x>=8 || y>=8)
        {x=Math.Max(0,x-targetSize/2);y=Math.Max(0,y-targetSize/2);return checked(Math.Min(x,y)+2*Math.Max(x,y));}
        int distance=SourceTowardPath.ShortDistance(x,y);
        return distance<0?0:Math.Max(0,distance-(targetSize==3?1:0))+1;
    }
    internal static ActorMotionState Center(int x,int y,int type=2)=>new(((uint)x<<16)|32768,((uint)y<<16)|32768,
        [],new(x,y),0,2560,2560,0,0,new(0,0),new(0,0),20,0,0,[],0,type);
    internal static Point2D Cell(ActorMotionState motion)=>new((int)(motion.X>>16),(int)(motion.Y>>16));
}
