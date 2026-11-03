// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
namespace Sanctuary;

// Complete older AStar point-target kernel, including its bounded allocator,
// stable linked-list ties, non-resorted score updates and partial best route.
// Collision pattern/room projection and modern executable parity stay caller work.
internal static class SourceAStarPath
{
    private sealed class Node(Point2D point,int heuristic)
    {
        internal readonly Point2D Point=point;
        internal short Heuristic=unchecked((short)heuristic),Distance,Score=unchecked((short)heuristic);
        internal Node Parent,Next;
        internal readonly List<Node> Children=[];
    }
    private static readonly Point2D[] Offsets=[new(-1,-1),new(-1,1),new(1,-1),new(1,1),new(-1,0),new(0,-1),new(1,0),new(0,1)];
    private static int Heuristic(Point2D a,Point2D b)
    {int x=Math.Abs(a.X-b.X),y=Math.Abs(a.Y-b.Y);return Math.Min(x,y)+2*Math.Max(x,y);}
    private static int Cost(Node a,Node b)=>a.Point.X==b.Point.X||a.Point.Y==b.Point.Y?2:3;
    internal static Point2D[] Find(Point2D origin,Point2D target,Func<int,int,bool> blocked)
    {
        ArgumentNullException.ThrowIfNull(blocked);
        if(origin.X is <0 or >65535||origin.Y is <0 or >65535||target.X is <0 or >65535||target.Y is <0 or >65535)
        {throw new ArgumentOutOfRangeException(nameof(target));}
        var pending=new Dictionary<Point2D,Node>();var visited=new Dictionary<Point2D,Node>();
        Node sorted=null,best=null;int allocated=1;
        void Candidate(Node node)
        {
            pending.Add(node.Point,node);Node previous=null;
            // The source insertion argument is uint16, while stored scores are int16.
            for(var current=sorted;current!=null&&current.Score<unchecked((ushort)node.Score);current=current.Next){previous=current;}
            if(previous==null){node.Next=sorted;sorted=node;}else{node.Next=previous.Next;previous.Next=node;}
        }
        static void Link(Node parent,Node child){if(parent.Children.Count<8){parent.Children.Add(child);}}
        static void SetParent(Node node,Node parent,int distance)
        {node.Parent=parent;node.Distance=unchecked((short)distance);node.Score=unchecked((short)(node.Distance+node.Heuristic));}
        static void Propagate(Node node)
        {
            var stack=new Stack<Node>();stack.Push(node);
            while(stack.Count!=0)
            {
                var parent=stack.Pop();
                foreach(var child in parent.Children)
                {
                    short distance=unchecked((short)(parent.Distance+Cost(parent,child)));
                    if(distance>=child.Distance){continue;}
                    SetParent(child,parent,distance);
                    if(stack.Count==200){throw new NotSupportedException("Unverified source AStar propagation-stack overflow");}
                    stack.Push(child);
                }
            }
        }
        Candidate(new(origin,Heuristic(target,origin)));
        while(sorted!=null)
        {
            var current=sorted;sorted=current.Next;pending.Remove(current.Point);visited.Add(current.Point,current);
            if(best==null||current.Heuristic<best.Heuristic||current.Heuristic==best.Heuristic&&current.Distance>best.Distance+5){best=current;}
            if(current.Heuristic==0){break;}
            bool exhausted=false;
            foreach(var offset in Offsets)
            {
                var point=new Point2D(unchecked((ushort)(current.Point.X+offset.X)),unchecked((ushort)(current.Point.Y+offset.Y)));
                if(blocked(point.X,point.Y)){continue;}
                short distance=unchecked((short)(current.Distance+(offset.X==0||offset.Y==0?2:3)));
                if(pending.TryGetValue(point,out var child))
                {
                    Link(current,child);if(distance<child.Distance){SetParent(child,current,distance);} // Deliberately no re-sort.
                }
                else if(visited.TryGetValue(point,out child))
                {
                    Link(current,child);if(distance<child.Distance){SetParent(child,current,distance);Propagate(child);}
                }
                else
                {
                    if(allocated==200){exhausted=true;break;}
                    allocated++;child=new(point,Heuristic(point,target));Link(current,child);SetParent(child,current,distance);Candidate(child);
                }
            }
            if(exhausted){break;}
        }
        var reverse=new List<Point2D>();int dx=-2,dy=-2;
        for(var node=best;node?.Parent!=null&&reverse.Count<78;node=node.Parent)
        {
            int x=node.Point.X-node.Parent.Point.X,y=node.Point.Y-node.Parent.Point.Y;
            if(x!=dx||y!=dy){reverse.Add(node.Point);dx=x;dy=y;}
        }
        if(reverse.Count is <1 or >=78){return [];}
        reverse.Reverse();return reverse.ToArray();
    }
}
