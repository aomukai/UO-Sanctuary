// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class AssassinTrapMovementChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AssassinTrapMovementChecks));
    public static void Configure()
    {Register("assassin-trap-movement-check");Register("assassin-trap-movement-reload-check");}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Wall-follow/monster attack components only; no moving blade actor",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static Point2D Cell(JsonElement row)=>new(row[0].GetInt32(),row[1].GetInt32());
    internal static void Paths()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"wall-follow-source-probe.json")));
        var grids=doc.RootElement.GetProperty("grids").EnumerateArray().Select(grid=>grid.EnumerateArray().Select(Cell).ToHashSet()).ToArray();
        Require(grids.Length==24,"Incomplete source collision grids");int count=0;long queries=0;int empty=0,partial=0;
        foreach(var row in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();var origin=new Point2D(100,100);var target=new Point2D(100+N(1),100+N(2));
            var trace=new List<Point2D>();var blocked=grids[N(0)];
            var actual=SourceWallFollow.Find(origin,target,N(3),N(4)!=0,(x,y)=>{var point=new Point2D(x,y);trace.Add(point);return blocked.Contains(point);});
            string context=$"grid{N(0)} target{N(1)},{N(2)} budget{N(3)} unit{N(4)}";
            Require(actual.Length==N(5) && actual.SequenceEqual(row[6].EnumerateArray().Select(Cell)),"Full source wall-follow route mismatch "+context);
            Require(trace.SequenceEqual(row[7].EnumerateArray().Select(Cell)),"Full source collision query ordering mismatch "+context);
            if(actual.Length==0){empty++;}else if(actual[^1]!=target){partial++;}
            queries+=trace.Count;count++;
        }
        Require(count==13200 && queries>100000 && empty>0 && partial>0,"Incomplete wall-follow route/query/budget coverage");
        bool called=false;Require(SourceWallFollow.Find(new(100,100),new(102,100),20,false,(_,_)=>{called=true;return true;}).Length==0 && !called,"Short source path unexpectedly checks collision or moves");
        Require(SourceWallFollow.Find(new(100,100),new(119,100),20,false,(_,_)=>false).SequenceEqual(new Point2D[]{new(119,100)}),"Legal nineteen-cell path endpoint");
        Require(SourceWallFollow.Find(new(100,100),new(120,100),20,false,(_,_)=>false).Length==0,"At-budget path unexpectedly accepted");
        Require(SourceWallFollow.Find(new(100,100),new(120,100),20,true,(_,_)=>false).SequenceEqual(new Point2D[]{new(120,100)}),"Target unit did not raise budget to40");
        bool invalid=false;try{SourceWallFollow.Find(new(100,100),new(103,100),78,false,(_,_)=>false);}catch(ArgumentOutOfRangeException){invalid=true;}Require(invalid,"Unbounded source distance accepted");
        Log.Information("Wall-follow: {Cases} full source paths and {Queries} exact collision calls match; empty and partial paths retained",count,queries);
    }
    private static void Attacks()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"assassin-trap-attack-source-probe.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();int chance=AssassinTrapAttack.Chance(N(0),N(2),N(6),N(3),N(1),N(4),N(5));
            Require(AssassinTrapAttack.Hit(chance,(uint)N(7))==(N(8)!=0) && N(8)==N(9),"Complete monster-owner hit-success differential");count++;
        }
        Require(count==6804,"Incomplete monster-owner source fixture");
        Require(AssassinTrapAttack.Chance(1,0,0,0,99,0)==5 && AssassinTrapAttack.Hit(5,4) && !AssassinTrapAttack.Hit(5,5),"Minimum chance or equality boundary");
        Require(AssassinTrapAttack.Chance(99,250,0,0,1,1000)==39,"Integer factor then level weighting");
        Require(!AssassinTrapAttack.Hit(95,uint.MaxValue),"Source unsigned RNG modulo/equality");
        bool invalid=false;try{AssassinTrapAttack.Chance(0,250,0,0,1,1000);}catch(ArgumentOutOfRangeException){invalid=true;}Require(invalid,"Invalid actor level accepted");
        Log.Information("Trap attack: {Cases} full source monster-owner outcomes match; nested prevent-heal remains a source service",count);
    }
    private static void Check()
    {
        Paths();Attacks();Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Movement components enabled an incomplete actor handler");
        // These deterministic kernels are repeated after reload. They create no
        // actor/world state and do not claim production blade persistence/input.
    }
}
