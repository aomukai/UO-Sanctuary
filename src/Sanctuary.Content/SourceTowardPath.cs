// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

// Source path2/5/6/13, point target and collision pattern0. Circle paths5/6
// use the source direction offsets+2/-2 in the same three-direction kernel.
// Keep ray ordering, repeated waypoints and failure-side motion changes: these
// routines do not share wall-follow's line/budget rejection or simplification.
internal static class SourceTowardPath
{
    private static readonly int[][] Directions=
    [
        [5,4,6],[4,5,6],[4,3,5],[4,3,2],[3,4,2],
        [6,5,4],[5,4,6],[4,3,5],[3,4,2],[2,3,4],
        [6,7,5],[6,7,5],[6,7,5],[2,1,3],[2,1,3],
        [6,7,0],[7,0,6],[0,1,7],[1,0,2],[2,1,0],
        [7,0,6],[0,7,6],[0,1,7],[0,1,2],[1,0,2]
    ];
    private static readonly Point2D[] Offsets=[new(1,0),new(1,1),new(0,1),new(-1,1),new(-1,0),new(-1,-1),new(0,-1),new(1,-1)];
    private static readonly int[] Distances=
    [
        -1,-1,-1,0,2,4,6,8, -1,-1,0,1,2,4,6,8,
        -1,0,0,2,3,5,7,8, 0,1,2,2,4,5,7,8,
        2,2,3,4,5,6,7,9, 4,4,5,5,6,7,8,9,
        6,6,7,7,7,8,10,10, 8,8,8,8,9,9,10,11
    ];
    private static Point2D Cell(int x,int y)=>new(unchecked((ushort)x),unchecked((ushort)y));
    private static int DirectionIndex(Point2D a,Point2D b)
    {
        int x=b.X-a.X,y=b.Y-a.Y,ax=Math.Abs(x),ay=Math.Abs(y);
        if(ax<2*ay)
        {
            if(ay>=2*ax)
            {
                if(x<0){if(y< -1){return 5;}if(y>1){y=2;}return y+7;}
                x&=1;
            }
        }
        else{y=y>=0?y&1:-1;}
        x=Math.Clamp(x,-2,2);
        if(y< -1){return 5*x+10;}if(y>1){y=2;}return y+5*x+12;
    }
    private static int Distance(Point2D a,Point2D b)
    {
        int x=Math.Abs(a.X-b.X),y=Math.Abs(a.Y-b.Y);
        if(x>=8 || y>=8){return Math.Min(x,y)+2*Math.Max(x,y);}
        return Math.Max(0,Distances[x+8*y]+1);
    }
    internal static int ShortDistance(int x,int y)=>x is >=0 and <8 && y is >=0 and <8
        ?Distances[x+8*y]:throw new ArgumentOutOfRangeException();
    private static bool FreeDirection(Point2D point,Point2D target,Func<int,int,bool> blocked,out int direction,int directionOffset=0)
    {
        foreach(int raw in Directions[DirectionIndex(point,target)])
        {
            int d=(raw+directionOffset+256)%8;
            var offset=Offsets[d];
            // The source passes promoted coordinates to collision, then stores
            // successful movement in the uint16 path record.
            if(!blocked(point.X+offset.X,point.Y+offset.Y)){direction=d;return true;}
        }
        direction=0;return false;
    }
    private static (bool Clear,Point2D Destination) Ray(Point2D origin,Point2D target,Func<int,int,bool> blocked)
    {
        var last=origin;var point=origin;int dx=target.X-origin.X,dy=target.Y-origin.Y;
        int nx=Math.Abs(dx)+1,ny=Math.Abs(dy)+1,sx=dx>=0?1:-1,sy=dy>=0?1:-1;
        if(nx>ny)
        {
            if(dx==0)
            {
                int y=origin.Y;
                while(y!=target.Y)
                {y+=sy;point=Cell(point.X,y);if(blocked(origin.X,y)){return (false,last);}last=point;}
            }
            else if(origin.X!=target.X)
            {
                int offset=ny;
                while(true)
                {
                    point=Cell(point.X+sx,point.Y);
                    if(blocked(point.X,point.Y)){return (false,last);}
                    offset+=ny;
                    if(offset>=nx)
                    {
                        offset-=nx;point=Cell(point.X,point.Y+sy);
                        if(offset>0 && blocked(point.X,point.Y)){return (false,last);}
                    }
                    last=point;if(point.X==target.X){break;}
                }
            }
            return (true,target);
        }
        if(nx>=ny)
        {
            while(point.X!=target.X)
            {point=Cell(point.X+sx,point.Y+sy);if(blocked(point.X,point.Y)){return (false,last);}last=point;}
            return (true,target);
        }
        if(dy==0)
        {
            while(point.X!=target.X)
            {point=Cell(point.X+sx,point.Y);if(blocked(point.X,origin.Y)){return (false,last);}last=point;}
            return (true,target);
        }
        int endOffset=nx;
        while(point.Y!=target.Y)
        {
            point=Cell(point.X,point.Y+sy);
            if(blocked(point.X,point.Y)){return (false,last);}
            endOffset+=nx;
            if(endOffset>=ny)
            {
                endOffset-=ny;point=Cell(point.X+sx,point.Y);
                if(endOffset>0 && blocked(point.X,point.Y)){return (false,last);}
            }
            last=point;
        }
        return (true,target);
    }
    internal static (ActorMotionState Motion,Point2D[] Points) Find(ActorMotionState state,Point2D target,int budget,Func<int,int,bool> blocked)
    {
        ArgumentNullException.ThrowIfNull(blocked);
        if(state.PathType is not(2 or 5 or 6 or 13) || budget is <0 or >77 || target.X is <1 or >65535 || target.Y is <1 or >65535)
        {throw new NotSupportedException("Unsupported source toward-path context");}
        var origin=new Point2D((int)(state.X>>16),(int)(state.Y>>16));var motion=state with{Index=0,Points=[]};
        if(state.PathType is 5 or 6){return(motion,Circle(origin,target,budget,state.PathType==5?2:-2,blocked));}
        Point2D next=origin;bool clear=false;
        if(motion.Velocity!=0)
        {
            motion=SourceActorMotion.DirectionToNext(motion with{Points=[target]});
            if(motion.Velocity!=0){(clear,next)=Ray(origin,target,blocked);}
        }
        var points=new List<Point2D>();
        void Add(Point2D p){if(points.Count>=77){throw new NotSupportedException("Unverified source toward path buffer edge");}points.Add(p);}
        if(!clear && Distance(next,target)>1) // PATH_UpdateTargetUnit returns1 for point targets.
        {
            var point=origin;var current=origin;
            if(origin!=next){Add(next);point=next;current=next;}
            int previous=255,traveled=0;bool turn=false,append=false;
            if(budget>0)
            {
                do
                {
                    if(current==target){break;}turn=false;
                    if(!FreeDirection(point,target,blocked,out int direction) || ((direction-4)&7)==previous){append=true;break;}
                    current=Cell(current.X+Offsets[direction].X,current.Y+Offsets[direction].Y);
                    if(direction!=previous){if(point!=origin){Add(point);}turn=true;}
                    point=current;previous=direction;traveled++;
                }while(traveled<budget);
                if((append || !turn) && traveled!=0){Add(point);}
            }
        }
        else{Add(next);}
        return (motion,points.ToArray());
    }
    private static Point2D[] Circle(Point2D origin,Point2D target,int budget,int offset,Func<int,int,bool> blocked)
    {
        var points=new List<Point2D>();var last=origin;var current=origin;int previous=255,traveled=0;bool added=false;
        for(;traveled<budget;traveled++)
        {
            if(current==target){break;}added=false;
            if(!FreeDirection(last,target,blocked,out int direction,offset) || (direction+4)%8==previous){break;}
            current=Cell(current.X+Offsets[direction].X,current.Y+Offsets[direction].Y);
            if(direction!=previous){points.Add(last);added=true;}
            last=current;previous=direction;
        }
        if(!added && traveled!=0){points.Add(last);}
        return points.ToArray();
    }
}
