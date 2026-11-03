// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class AStarPathChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AStarPathChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("astar-path-check","Complete source AStar/straight obstacle repair",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("astar-path-check: passed");}catch(Exception e){Log.Error(e,"astar-path-check: failed");}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static Point2D Point(JsonElement p)=>new(p[0].GetInt32(),p[1].GetInt32());
    private static void Check()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"astar-path-source-cases.json")));
        int contexts=0,queriesTotal=0,partial=0,repaired=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            var input=row.GetProperty("input");int N(int i)=>input[i].GetInt32();var queries=new List<Point2D>();
            int Collision(int x,int y){queries.Add(new(x,y));return TowardPathChecks.Mask(N(1),x,y);}
            var target=new Point2D(100+N(2),100+N(3));Point2D[] points;
            if(N(0)==1){points=SourceAStarPath.Find(new(100,100),target,(x,y)=>(Collision(x,y)&0x3c01)!=0);}
            else
            {
                var motion=new ActorMotionState((uint)((100<<16)+N(4)),(uint)((100<<16)+N(4)),[new(99,99)],new(99,99),0,2304,2304,0,0,new(0,0),new(0,0),20,0,0,[],0,7);
                var result=SourceActorPath.Initialize(new(motion,target,new(98,98)),N(5),new(80,80,48,48),Collision);var s=result.Motion;points=s.Points;
                var actual=new long[]{points.Length,s.X,s.Y,s.Flags,s.Index,s.Velocity,s.Direction.X,s.Direction.Y,s.Vector.X,s.Vector.Y,s.CollidedMask,result.Target.X,result.Target.Y,result.PreviousTarget.X,result.PreviousTarget.Y,s.FinalTarget.X,s.FinalTarget.Y};
                var expected=row.GetProperty("state");
                for(int i=0;i<actual.Length;i++){Require(actual[i]==expected[i].GetInt64(),$"Straight context{contexts} input{input} field{i}: actual{actual[i]} expected{expected[i]}");}
                Require(s.PathType==7,"Straight repair rewrote path type");
                var loaded=JsonSerializer.Deserialize<ActorPathState>(JsonSerializer.Serialize(result));
                Require(JsonSerializer.Serialize(loaded)==JsonSerializer.Serialize(result),"Straight repaired path did not round-trip");
                if(points.Length>0)
                {
                    var before=SourceTowardPath.Find(motion with{PathType=2},target,N(5),(x,y)=>(TowardPathChecks.Mask(N(1),x,y)&0x3c01)!=0);
                    if(!before.Points.SequenceEqual(points)){repaired++;}
                    if(points[^1]!=target){partial++;}
                }
            }
            Require(points.SequenceEqual(row.GetProperty("points").EnumerateArray().Select(Point)),$"AStar route mismatch at{contexts} input{input}");
            Require(queries.SequenceEqual(row.GetProperty("queries").EnumerateArray().Select(Point)),$"AStar query order mismatch at{contexts} input{input}");
            queriesTotal+=queries.Count;contexts++;
        }
        Require(contexts==3360&&queriesTotal==827372&&partial>0&&repaired>0,"Incomplete AStar success/partial/repair source coverage");
        Log.Information("AStar:{Contexts} complete source contexts/{Queries} ordered queries;{Repaired} changed straight routes/{Partial} partial routes; saved component round-trips",contexts,queriesTotal,repaired,partial);
    }
}
