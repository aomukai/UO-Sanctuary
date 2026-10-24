// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class BladeClockChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeClockChecks));
    public static void Configure(){Register("blade-clock-check");Register("blade-clock-reload-check");}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Complete prepared blade event/AI/movement clocks; no learned skill",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static Point2D Point(JsonElement x)=>new(x[0].GetInt32(),x[1].GetInt32());
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-clock-source-probe.json")));int contexts=0,frames=0,queriesTotal=0,lateDeaths=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var input=row.GetProperty("input");int N(int i)=>input[i].GetInt32();var motion=new ActorMotionState(100*65536u+32768,100*65536u+32768,[],new(0,0),0,2304,2304,0,0,new(0,0),new(0,0),20,0x800,0,[],0);
            var control=BladeSentinelController.Start(new(100,100),new(100+N(1),100),100+12*(N(2)-1),2304,SourceRandom.InitLow(19)) with{HasCommand=N(3)!=0};int created=0;
            foreach(var expected in row.GetProperty("frames").EnumerateArray())
            {
                var queries=new List<Point2D>();int Collision(int x,int y)
                {
                    queries.Add(new(x,y));if(x<80 || x>=128 || y<80 || y>=128){return 0x3c01;}
                    return N(0) switch{1 when x==103=>1,2 when x>=97 && x<=103 && y>=97 && y<=103 && (x==97||x==103||y==97||y==103)=>1,3 when x==104 && y==100=>0x1000,_=>0};
                }
                var result=BladeSentinelController.Advance(control,motion,new(80,80,48,48),Collision,new Point2D(96,100),()=>created++);control=result.Control;motion=result.Motion;
                var actual=new long[]{control.Mode,motion.X,motion.Y,motion.Flags,motion.PathType,motion.Points.Length,motion.Index,motion.Velocity,motion.Distance,control.Ai.Expiry,control.Ai.Forward?1:0,control.Ai.MissileAttempted?1:0,control.NextThink,control.Mode==0?1:0,control.Creates,control.Thinks,control.Moves,motion.Direction.X,motion.Direction.Y,motion.Vector.X,motion.Vector.Y};
                for(int i=0;i<actual.Length;i++){Require(actual[i]==expected[i].GetInt64(),$"Blade clock context{contexts} frame{control.Frame} field{i}: actual{actual[i]} expected{expected[i]}");}
                Require(control.Seed==expected[21].GetUInt64() && queries.SequenceEqual(expected[22].EnumerateArray().Select(Point)),"Blade clock seed/query ordering");
                Require(created==control.Creates,"Blade first missile attempt count");
                if(control.Frame==77){var snapshot=JsonSerializer.Deserialize<BladeFrameResult>(JsonSerializer.Serialize(result));control=snapshot.Control;motion=snapshot.Motion;}
                if(control.Mode!=0 && control.Frame>control.Ai.Expiry){lateDeaths++;}queriesTotal+=queries.Count;frames++;
            }
            contexts++;
        }
        Require(contexts==64 && frames==11520 && lateDeaths>0,"Incomplete composed blade clock corpus / AI-observed expiry");
        Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Prepared clock enabled incomplete blade");
        Log.Information("Blade clock: {Contexts} composed source contexts / {Frames} exact event/AI/movement frames / {Queries} ordered collision queries pass; full learned combat remains separate",contexts,frames,queriesTotal);
    }
}
