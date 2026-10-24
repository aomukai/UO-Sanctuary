// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class TowardMotionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(TowardMotionChecks));
    public static void Configure(){Register("toward-motion-check");Register("toward-motion-reload-check");}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Source toward paths and full movement frames; no playable blade",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static Point2D Point(JsonElement x)=>new(x[0].GetInt32(),x[1].GetInt32());
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"toward-motion-source-probe.json")));int contexts=0,frames=0,zeroDistanceMoving=0,queriesTotal=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var input=row.GetProperty("input");int N(int i)=>input[i].GetInt32();
            var initial=new ActorMotionState((uint)((100<<16)+N(3)),(uint)((100<<16)+N(3)),[new(99,99)],new(99,99),0,2304,2304,0,0,new(0,0),new(0,0),N(4),N(5)==13?0x100:0,0,[],0,N(5));
            var state=SourceActorPath.Initialize(new(initial,new(100+N(1),100+N(2)),new(98,98)),N(4),new(80,80,48,48),(x,y)=>TowardPathChecks.Mask(N(0),x,y)).Motion;int frame=0;
            foreach(var expected in row.GetProperty("frames").EnumerateArray())
            {
                var queries=new List<Point2D>();int Collision(int x,int y){queries.Add(new(x,y));return TowardPathChecks.Mask(N(0),x,y);}
                var result=SourceActorMotion.Advance(state,Collision);state=result.State;
                var actual=new long[]{result.Moving?1:0,state.X,state.Y,state.Flags,state.Points.Length,state.Index,state.Velocity,state.MaximumVelocity,state.Acceleration,state.AccelerationCounter,state.Direction.X,state.Direction.Y,state.Vector.X,state.Vector.Y,state.Distance,state.StepsCount,state.CollidedMask,result.RepathRequired?1:0};
                for(int i=0;i<actual.Length;i++){Require(actual[i]==expected[i].GetInt64(),$"Toward motion context{contexts} frame{frame} field{i}: actual{actual[i]} expected{expected[i]}");}
                Require(state.PathType==N(5) && queries.SequenceEqual(expected[18].EnumerateArray().Select(Point)),"Toward motion path type/collision order");
                if(frame==17){state=JsonSerializer.Deserialize<ActorMotionState>(JsonSerializer.Serialize(state));}
                if(result.Moving && state.Distance==0){zeroDistanceMoving++;}queriesTotal+=queries.Count;frames++;frame++;
            }
            contexts++;
        }
        Require(contexts==2592 && frames==124416 && zeroDistanceMoving>0 && queriesTotal>10000,"Incomplete full toward-motion fixture");
        Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Motion component enabled unfinished blade");
        Log.Information("Toward motion: {Contexts} full source paths / {Frames} exact frames / {Queries} collision queries match, including {Zero} moving frames at zero distance; no automatic controller",contexts,frames,queriesTotal,zeroDistanceMoving);
    }
}
