// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class RavenFlightChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(RavenFlightChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("raven-flight-check","Circle-offset path component; no learned Raven actor",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("raven-flight-check: passed");}catch(Exception e){Log.Error(e,"raven-flight-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static Point2D Point(JsonElement row)=>new(row[0].GetInt32(),row[1].GetInt32());
    private static void Check()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"raven-circle-source-probe.json")));int count=0,queryCount=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            var input=row.GetProperty("input");int N(int i)=>input[i].GetInt32();var queries=new List<Point2D>();
            int Collision(int x,int y){queries.Add(new(x,y));return TowardPathChecks.Mask(N(0),x,y);}
            var motion=new ActorMotionState((uint)((100<<16)+N(3)),(uint)((100<<16)+N(3)),[new(99,99)],new(99,99),0,2304,2304,0,0,new(0,0),new(0,0),20,0,0,[],0,N(5));
            var result=SourceActorPath.Initialize(new(motion,new(100+N(1),100+N(2)),new(98,98)),N(4),new(80,80,48,48),Collision);var s=result.Motion;
            var actual=new long[]{s.Points.Length,s.X,s.Y,s.Flags,s.Index,s.Velocity,s.Direction.X,s.Direction.Y,s.Vector.X,s.Vector.Y,s.CollidedMask,result.Target.X,result.Target.Y,result.PreviousTarget.X,result.PreviousTarget.Y,s.FinalTarget.X,s.FinalTarget.Y};
            var expected=row.GetProperty("state");for(int i=0;i<actual.Length;i++){Require(actual[i]==expected[i].GetInt64(),$"Circle path context{count} field{i}: actual{actual[i]} expected{expected[i]}");}
            Require(s.PathType==N(5) && s.Points.SequenceEqual(row.GetProperty("points").EnumerateArray().Select(Point)),"Source circle type/points");
            Require(queries.SequenceEqual(row.GetProperty("queries").EnumerateArray().Select(Point)),"Source circle collision ordering");
            var loaded=JsonSerializer.Deserialize<ActorPathState>(JsonSerializer.Serialize(result));Require(JsonSerializer.Serialize(loaded)==JsonSerializer.Serialize(result),"Circle path snapshot round trip");
            queryCount+=queries.Count;count++;
        }
        Require(count==6480 && queryCount>0,"Circle source context/query coverage");
        foreach(int type in new[]{5,6})
        {
            var end=new ActorMotionState(100*65536u+32768,100*65536u+32768,[new(100,100)],new(110,100),1,2304,2304,0,0,new(4096,0),new(36864,0),4,0x20,0,[],0,type);
            var stopped=SourceActorMotion.Advance(end,(_,_)=>throw new InvalidOperationException("Completed circle queried collision"));
            Require(!stopped.Moving && !stopped.RepathRequired && stopped.State.Points.Length==0,"Completed circle attempted target repath");
        }
        Require(!DruidCombat.Implemented(CombatAbility.Raven),"Circle component enabled unfinished Raven");
        Log.Information("Raven circle paths:6480complete source contexts and{Queries}ordered collision queries match; circle-end/no-repath and saved path identity pass",queryCount);
    }
}
