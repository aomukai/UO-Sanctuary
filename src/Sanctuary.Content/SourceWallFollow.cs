// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

// Path15 component. The collision callback supplies the caller's source pattern
// and world projection; this class does not move actors or select a UO pathfinder.
// Port of pinned D2MOO PathWF, including its documented empty-path INT_MAX fix.
// That reconstruction edge is not a current D2R executable equivalence claim.
public static class SourceWallFollow
{
    private static readonly int[] Directions=[7,0,1,6,-1,2,5,4,3];
    private static readonly Point2D[] Offsets=[new(0,-1),new(1,-1),new(1,0),new(1,1),new(0,1),new(-1,1),new(-1,0),new(-1,-1),
        new(0,-1),new(1,-1),new(1,0),new(1,1),new(0,1),new(-1,1),new(-1,0),new(-1,-1)];
    private static readonly int[] Previous1=[6,6,0,0,2,2,4,4,6,6,0,0,2,2,4,4];
    private static readonly int[] Previous2=[2,2,4,4,6,6,0,0,2,2,4,4,6,6,0,0];
    private static readonly int[] Next1=[2,2,4,4,6,6,0,0,2,2,4,4,6,6,0,0];
    private static readonly int[] Next2=[6,0,0,2,2,4,4,6,6,0,0,2,2,4,4,6];
    private sealed class Walker
    {
        internal int Direction,Count,Sync;
        internal Point2D Current,Target;
        internal bool Finished;
        internal int[] Previous,Next;
        internal Point2D[] Points=new Point2D[200];
    }
    private static Point2D Cell(int x,int y)=>new(unchecked((ushort)x),unchecked((ushort)y));
    private static int Squared(Point2D a,Point2D b)
    {int x=a.X-b.X,y=a.Y-b.Y;return checked(x*x+y*y);}
    private static bool Free(Walker path,Func<int,int,bool> blocked)
    {
        for(int i=0;i<4;i++)
        {
            var offset=Offsets[path.Direction];path.Target=Cell(path.Current.X+offset.X,path.Current.Y+offset.Y);
            if(!blocked(path.Target.X,path.Target.Y)){return true;}
            if(i<3){path.Direction=path.Next[path.Direction];}
        }
        return false;
    }
    private static void Replace(Walker path,Point2D[] points,ref int start,int last,ref int maximum)
    {
        int difference=start-last+path.Count-1,newLast=last+difference;
        Array.Copy(points,last+1,points,newLast+1,maximum-last-1);
        Array.Copy(path.Points,0,points,start,path.Count);
        maximum+=difference;start=newLast+1;
    }
    private static int Subpath(Point2D origin,Point2D target,Func<int,int,bool> blocked,Point2D subStart,Point2D[] points,
        ref int start,ref int maximum,int budget,int major)
    {
        int dx=points[start].X-subStart.X,dy=points[start].Y-subStart.Y;
        if(dx is <-1 or >1 || dy is <-1 or >1){throw new NotSupportedException("Nonadjacent source wall-follow subpath");}
        int adjacent=Directions[4+dy*3+dx];
        var first=new Walker{Previous=Previous1,Next=Next1,Direction=adjacent+1,Current=subStart};
        var second=new Walker{Previous=Previous2,Next=Next2,Direction=adjacent+7,Current=subStart};
        var current=first;var other=second;
        while(!first.Finished && !second.Finished)
        {
            if(!current.Finished)
            {
                if(!Free(current,blocked)){current.Finished=true;}
                else
                {
                    if(current.Count>0)
                    {
                        int distance=major switch{0=>subStart.Y-current.Target.Y,1=>current.Target.X-subStart.X,
                            2=>current.Target.Y-subStart.Y,3=>subStart.X-current.Target.X,_=>0};
                        if(distance>0)
                        {
                            int same=distance-1+start;
                            if(same<maximum && current.Target==points[same])
                            {
                                if(current.Count>=budget){throw new NotSupportedException("Source wall-follow path budget assertion");}
                                current.Points[current.Count++]=current.Target;
                                current.Points[current.Count]=new(0,current.Points[current.Count].Y);
                                Replace(current,points,ref start,same,ref maximum);return current.Count;
                            }
                        }
                    }
                    if(other.Count>1)
                    {
                        if(current.Sync>0)
                        {
                            // The reconstruction indexes Sync-2. Refuse its
                            // undefined negative-index edge rather than invent
                            // an arbitrary path; bounded source vectors avoid it.
                            if(current.Sync==1){throw new NotSupportedException("Unresolved legacy wall-follow sync index");}
                            if(current.Target==other.Points[current.Sync-2])
                            {points[start]=new(0,points[start].Y);maximum=start;return 0;}
                            current.Sync=0;
                        }
                        if(current.Target==other.Current){current.Sync=other.Count;}
                    }
                    current.Direction=current.Previous[current.Direction];
                    current.Points[current.Count++]=current.Target;current.Current=current.Target;
                    if(budget-maximum-1<=current.Count){current.Finished=true;}
                }
            }
            if(current.Sync==0){(current,other)=(other,current);}
        }
        if(budget>80){points[start]=new(0,points[start].Y);maximum=start;return 0;}
        int firstDistance=first.Count>0?Squared(first.Points[first.Count-1],target):int.MaxValue;
        int secondDistance=second.Count>0?Squared(second.Points[second.Count-1],target):int.MaxValue;
        int originalDistance=Squared(origin,target);Walker best;
        if(firstDistance<secondDistance)
        {if(originalDistance<firstDistance){return 0;}best=first;}
        else{if(originalDistance<secondDistance){return 0;}best=second;}
        if(best.Count>0){Array.Copy(best.Points,0,points,start,best.Count);}
        start+=best.Count;maximum=start;points[start]=new(0,points[start].Y);
        return current.Count; // Source returns current, which may differ from best.
    }
    private static Point2D[] Simplify(Point2D[] points,Point2D origin,int count)
    {
        if(count<1){return [];}
        if(count==1){return [points[0]];}
        var result=new Point2D[count];int output=0,previousX=points[0].X-origin.X,previousY=points[0].Y-origin.Y,inLine=0,i;
        for(i=0;i<count-1;i++)
        {
            int dx=points[i+1].X-points[i].X,dy=points[i+1].Y-points[i].Y;
            if(dx==previousX && dy==previousY){inLine++;}
            else if(inLine<=0 && previousX!=dx && previousY!=dy){dx=-2;inLine=1;}
            else{result[output++]=points[i];inLine=0;}
            previousX=dx;previousY=dy;
        }
        if(i<count){result[output++]=points[i];}
        Array.Resize(ref result,output);return result;
    }
    private static int Line(Point2D origin,Point2D target,int budget,Point2D[] points,out int major)
    {
        int dx=Math.Abs(target.X-origin.X),dy=Math.Abs(target.Y-origin.Y),sx=target.X<origin.X?-1:1,sy=target.Y<origin.Y?-1:1;
        major=0;int count=Math.Max(dx,dy);
        if(count==0){return 0;}
        major=dy>dx?(sy<=0?0:2):(sx<=0?3:1);
        if(count>budget-1){return 0;}
        var cursor=new SourceLineCursor(origin.X,origin.Y,target.X,target.Y);int x=origin.X,y=origin.Y;
        for(int i=1;i<=count;i++){cursor.Advance(ref x,ref y,out _,out _);points[i-1]=Cell(x,y);}
        points[count]=new(0,points[count].Y);return count;
    }
    internal static Point2D[] Find(Point2D origin,Point2D target,int maximumDistance,bool hasTargetUnit,Func<int,int,bool> blocked)
    {
        ArgumentNullException.ThrowIfNull(blocked);
        if(maximumDistance is <1 or >77 || origin.X is <0 or >65535 || origin.Y is <0 or >65535
            || target.X is <0 or >65535 || target.Y is <0 or >65535){throw new ArgumentOutOfRangeException();}
        int budget=hasTargetUnit && maximumDistance<40?40:maximumDistance;
        var points=new Point2D[78];int movement=Line(origin,target,budget,points,out int major);
        if(movement>2)
        {
            var previous=origin;
            for(int start=0;start<movement;start++)
            {
                if(blocked(points[start].X,points[start].Y))
                {
                    int count=Subpath(origin,target,blocked,previous,points,ref start,ref movement,budget-start,major);
                    if(count==0){return Simplify(points,origin,start);}
                }
                previous=points[start];
            }
            if(movement!=0){return Simplify(points,origin,movement);}
        }
        return [];
    }
}
