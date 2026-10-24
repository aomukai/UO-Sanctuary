// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class TowardPathChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(TowardPathChecks));
    public static void Configure(){Register("toward-path-check");Register("toward-path-reload-check");}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Source toward/toward-finish paths; no playable Blade Sentinel",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static Point2D Point(JsonElement x)=>new(x[0].GetInt32(),x[1].GetInt32());
    private static readonly HashSet<Point2D> Random=RandomGrid();
    private static HashSet<Point2D> RandomGrid()
    {
        var random=new HashSet<Point2D>();uint seed=19;
        for(int x=80;x<128;x++)for(int y=80;y<128;y++){seed=unchecked(1664525u*seed+1013904223u);if(seed%100<22){random.Add(new(x,y));}}random.Remove(new(100,100));
        return random;
    }
    internal static int Mask(int wall,int x,int y)
    {
        if(x<80 || x>=128 || y<80 || y>=128){return 0x3c01;}
        return wall switch{1 when x==103=>1,2 when y==102=>0x100,3 when x==103 && y>=98 && y<=102=>0x1000,
            4 when x>=97 && x<=103 && y>=97 && y<=103 && (x==97 || x==103 || y==97 || y==103)=>1,
            5 when x==98=>1,6 when y==103=>1,7 when Random.Contains(new(x,y))=>1,_=>0};
    }
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"toward-path-source-probe.json")));int contexts=0,queriesTotal=0,success=0,zeroFailures=0,vectorFailures=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var input=row.GetProperty("input");int N(int i)=>input[i].GetInt32();var queries=new List<Point2D>();
            int Collision(int x,int y)
            {
                queries.Add(new(x,y));return Mask(N(0),x,y);
            }
            var motion=new ActorMotionState((uint)((100<<16)+N(3)),(uint)((100<<16)+N(3)),[new(99,99)],new(99,99),0,2304,2304,0,0,new(0,0),new(0,0),20,N(5)==13?0x100:0,0,[],0,N(5));
            var result=SourceActorPath.Initialize(new(motion,new(100+N(1),100+N(2)),new(98,98)),N(4),new(80,80,48,48),Collision);var s=result.Motion;
            var actual=new long[]{s.Points.Length,s.X,s.Y,s.Flags,s.Index,s.Velocity,s.Direction.X,s.Direction.Y,s.Vector.X,s.Vector.Y,s.CollidedMask,result.Target.X,result.Target.Y,result.PreviousTarget.X,result.PreviousTarget.Y,s.FinalTarget.X,s.FinalTarget.Y};
            var expected=row.GetProperty("state");for(int i=0;i<actual.Length;i++){Require(actual[i]==expected[i].GetInt64(),$"Toward path context{contexts} field{i}: actual{actual[i]} expected{expected[i]}");}
            Require(s.PathType==N(5) && s.Points.SequenceEqual(row.GetProperty("points").EnumerateArray().Select(Point)),"Toward source type/path mismatch");
            Require(queries.SequenceEqual(row.GetProperty("queries").EnumerateArray().Select(Point)),"Toward collision ordering mismatch");
            var loaded=JsonSerializer.Deserialize<ActorPathState>(JsonSerializer.Serialize(result));Require(loaded.Motion.PathType==N(5) && JsonSerializer.Serialize(loaded)==JsonSerializer.Serialize(result),"Saved path type/component JSON round-trip");
            if(s.Points.Length>0){success++;}else{if(s.Velocity==0){zeroFailures++;}if(s.Direction!=new Point2D(0,0)){vectorFailures++;}}
            contexts++;queriesTotal+=queries.Count;
        }
        Require(contexts==6480 && queriesTotal==62880 && success==6198 && zeroFailures==24 && vectorFailures==114,"Incomplete toward-path state/query coverage");
        // Old prepared body/movement JSON predates PathType and is path15.
        var old=new ActorMotionState(100*65536u+32768,100*65536u+32768,[new(108,100)],new(108,100),0,2304,2304,0,0,new(4096,0),new(36864,0),20,0x820,0,[],0);
        var node=JsonNode.Parse(JsonSerializer.Serialize(old)).AsObject();Require(node.Remove("PathType"),"Legacy path fixture removal");
        var legacy=JsonSerializer.Deserialize<ActorMotionState>(node.ToJsonString());Require(legacy.PathType==15,"Old movement JSON did not retain path15 default");
        Require(SourceActorMotion.Advance(legacy,(_,_)=>0).State.X==old.X+36864,"Legacy movement JSON no longer resumes");
        Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Toward-path component enabled unfinished blade");
        Log.Information("Toward paths: {Contexts} complete source contexts / {Queries} ordered collision queries match; 24 zero-velocity and114 vector-bearing failures retained; old path15 JSON resumes",contexts,queriesTotal);
    }
}
