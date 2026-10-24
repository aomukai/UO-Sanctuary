// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class ActorPathChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ActorPathChecks));
    public static void Configure(){Register("actor-path-check");Register("actor-path-reload-check");}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Source point-path initialization, no playable Blade Sentinel",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static Point2D Point(JsonElement x)=>new(x[0].GetInt32(),x[1].GetInt32());
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"actor-path-source-probe.json")));int contexts=0,queryCount=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var input=row.GetProperty("input");int N(int i)=>input[i].GetInt32();var queries=new List<Point2D>();
            int Collision(int x,int y)
            {
                queries.Add(new(x,y));
                if(x<80 || x>=128 || y<80 || y>=128){return 0x3c01;}
                return N(0) switch{1 when x==103=>1,2 when y==102=>0x100,3 when x==103 && y>=98 && y<=102=>0x1000,
                    4 when x>=97 && x<=103 && y>=97 && y<=103 && (x==97 || x==103 || y==97 || y==103)=>1,_=>0};
            }
            var motion=new ActorMotionState((uint)((100<<16)+N(3)),(uint)((100<<16)+N(3)),[new(99,99)],new(99,99),0,2304,2304,0,0,new(0,0),new(0,0),20,0x800,0,[],0);
            // The source path record stores uint16 targets; retain that ABI
            // conversion for the negative-coordinate rejection controls.
            var target=new Point2D(unchecked((ushort)(100+N(1))),unchecked((ushort)(100+N(2))));
            var result=SourceActorPath.Initialize(new(motion,target,new(98,98)),N(4),new(80,80,48,48),Collision);var s=result.Motion;
            var actual=new long[]{s.Points.Length,s.X,s.Y,s.Flags,s.Index,s.Velocity,s.Direction.X,s.Direction.Y,s.Vector.X,s.Vector.Y,s.CollidedMask,result.Target.X,result.Target.Y,result.PreviousTarget.X,result.PreviousTarget.Y,s.FinalTarget.X,s.FinalTarget.Y};
            var expected=row.GetProperty("state");for(int i=0;i<actual.Length;i++){Require(actual[i]==expected[i].GetInt64(),$"Point-path state context{contexts} field{i}: actual{actual[i]} expected{expected[i]}");}
            Require(s.Points.SequenceEqual(row.GetProperty("points").EnumerateArray().Select(Point)),"Initializer compressed path mismatch");
            Require(queries.SequenceEqual(row.GetProperty("queries").EnumerateArray().Select(Point)),"Initializer collision ordering mismatch");
            Require(JsonSerializer.Serialize(result)==JsonSerializer.Serialize(JsonSerializer.Deserialize<ActorPathState>(JsonSerializer.Serialize(result))),"Path component JSON round-trip");
            queryCount+=queries.Count;contexts++;
        }
        Require(contexts==2250,"Incomplete source initializer fixture");
        Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Initializer enabled unfinished blade");
        Log.Information("Actor path: {Contexts} complete source initializer contexts / {Queries} ordered collision queries pass; repair/AI/combat remain separate",contexts,queryCount);
    }
}
