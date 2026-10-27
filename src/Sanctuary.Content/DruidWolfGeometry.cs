// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
namespace Sanctuary;

internal static class DruidWolfGeometry
{
    // D2Common11055, indices0..7. These are distinct from the clockwise
    // source path kernel's direction table and from native Direction values.
    private static readonly Point2D[] Offsets=[new(0,-1),new(-1,-1),new(-1,0),new(-1,1),new(0,1),new(1,1),new(1,0),new(1,-1)];
    internal static Point2D Offset(int index)=>index is >=0 and <8?Offsets[index]:throw new ArgumentOutOfRangeException(nameof(index));
    internal static int NativeHeading(Direction direction)=>(8-((int)direction&7))&7;
    internal static int Weighted(int x,int y)=>checked(Math.Min(x,y)+2*Math.Max(x,y))/2;
    internal static int NoSize(Point2D a,Point2D b)=>Weighted(Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y));
    // Literal AIUTIL full-size helper folds each signed size-adjusted axis.
    // It must not be replaced by the distinct clamped UNITS distance helper.
    internal static int Full(Point2D a,Point2D b,int size=2)=>Weighted(Math.Abs(Math.Abs(a.X-b.X)-size),Math.Abs(Math.Abs(a.Y-b.Y)-size));
    internal static int UnitDistance(Point2D a,Point2D b,int firstSize,int secondSize)
    {int size=firstSize/2+secondSize/2;return Weighted(Math.Max(0,Math.Abs(a.X-b.X)-size),Math.Max(0,Math.Abs(a.Y-b.Y)-size));}
    internal static int MeleeDistance(Point2D a,Point2D b,int firstSize,int secondSize)
    {
        if(firstSize is <0 or >3 || secondSize is <0 or >3){throw new NotSupportedException("Unsupported mapped wolf melee size");}
        int x=Math.Abs(a.X-b.X),y=Math.Abs(a.Y-b.Y);
        if(x>=8 || y>=8)
        {int size=firstSize/2+secondSize/2;return Math.Min(Math.Max(0,x-size),Math.Max(0,y-size))+2*Math.Max(Math.Max(0,x-size),Math.Max(0,y-size));}
        int d=SourceTowardPath.ShortDistance(x,y);if(d<0){return 0;}
        if(firstSize==3 || secondSize==3){d=Math.Max(0,d-1);}
        return d+(firstSize<=1 || secondSize<=1?1:0);
    }
    internal static (int Action,int End) Clock(string mode,int? suppliedSpeed=null,string source="spiritwolf-animation")
    {
        int frames=NecromancerSummons.Source(source,mode+"Frames"),speed=suppliedSpeed??NecromancerSummons.Source(source,mode+"Speed");
        if(frames is <1 or >144 || speed is <0 or >32767){throw new NotSupportedException("Unsupported wolf animation metadata");}
        var flags=new int[144];int action=NecromancerSummons.Source(source,mode+"Action",-1);
        if(action>=0){if(action>=frames){throw new NotSupportedException("Invalid wolf action flag");}flags[action]=1;}
        var plan=SourceAnimationEvents.Initial(new(0,speed,frames*256,flags,[]),0);
        return (action<0?0:plan.Events.FirstOrDefault(e=>e.Type==SourceAnimationEvents.ModeChange&&e.Kind==1)?.Frame??0,
            plan.Events.Single(e=>e.Type==SourceAnimationEvents.EndAnimation).Frame);
    }
    internal static ActorMotionState Stop(ActorMotionState s)=>s with{Points=[],Index=0,Flags=s.Flags&~0x28,Vector=new(0,0)};
}
