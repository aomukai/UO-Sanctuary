// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class BlessedHammerMotionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BlessedHammerMotionChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("blessed-hammer-motion-check","Complete source Hammer path14 movement comparison",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("blessed-hammer-motion-check: passed");}catch(Exception e){Log.Error(e,"blessed-hammer-motion-check: failed");}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static Point2D Point(JsonElement p)=>new(p[0].GetInt32(),p[1].GetInt32());
    internal static void Check()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"hammer-motion-source-cases.json")));int contexts=0,frames=0;
        foreach(var row in json.RootElement.EnumerateArray())
        {
            var input=row.GetProperty("input");int N(int i)=>input[i].GetInt32();var relative=row.GetProperty("path").EnumerateArray().Select(Point).ToArray();
            if(N(0)==32768){Require(relative.SequenceEqual(BlessedHammers.Waypoints()),"Hammer centered77waypoint source initializer mismatch");}
            var path=relative.Select(p=>new Point2D(p.X+100,p.Y+100)).ToArray();uint origin=(uint)((100<<16)+N(0));
            var s=new ActorMotionState(origin,origin,path,path[^1],0,N(1),7680,N(2),0,new(0,0),new(0,0),20,0x60020,0,[],0,14);
            s=SourceActorMotion.DirectionToNext(s);int frame=0;
            foreach(var expected in row.GetProperty("frames").EnumerateArray())
            {
                var queries=new List<Point2D>();int Collision(int x,int y)
                {queries.Add(new(x,y));return N(3)==3?0x100:N(3)==1&&x>=104?1:N(3)==2&&y>=104?4:0;}
                var result=SourceActorMotion.Advance(s,Collision);s=result.State;
                var actual=new long[]{result.Moving?1:0,(long)s.X-origin,(long)s.Y-origin,s.Points.Length,s.Index,s.Velocity,s.Acceleration,s.AccelerationCounter,
                    s.Direction.X,s.Direction.Y,s.Vector.X,s.Vector.Y,s.StepsCount,s.CollidedMask};
                for(int i=0;i<actual.Length;i++){Require(actual[i]==expected[i].GetInt64(),$"Hammer source context{contexts} frame{frame} field{i}:actual{actual[i]} expected{expected[i]}");}
                Require(!result.RepathRequired&&s.SavedSteps.Select(p=>new Point2D(p.X-100,p.Y-100)).SequenceEqual(expected[14].EnumerateArray().Select(Point)),"Hammer source ordered saved cells/repath mismatch");
                Require(queries.SequenceEqual(expected[16].EnumerateArray().Select(Point)),"Hammer source missile collision query ordering mismatch");
                if(frame==7){s=JsonSerializer.Deserialize<ActorMotionState>(JsonSerializer.Serialize(s));}
                frame++;frames++;
            }
            contexts++;
        }
        Require(contexts==108&&frames==12960,"Incomplete Hammer full movement source comparisons");
        Log.Information("Hammer motion:108complete contexts/12960exact frames;centered77waypoints;integer vectors/acceleration/cells;wall/barrier removal and nonblocking unit masks;component JSON;Fog/room services explicit");
    }
}
