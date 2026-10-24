// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class ActorMotionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ActorMotionChecks));
    public static void Configure(){Register("actor-motion-check");Register("actor-motion-reload-check");}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Source actor movement component, no playable Blade Sentinel",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static Point2D Point(JsonElement x)=>new(x[0].GetInt32(),x[1].GetInt32());
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"actor-motion-source-probe.json")));int contexts=0,frames=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var input=row.GetProperty("input");int N(int i)=>input[i].GetInt32();var path=row.GetProperty("path").EnumerateArray().Select(Point).ToArray();
            var speed=SourceActorMotion.Speed(12,128,N(2),N(3));Require(speed.Velocity==row.GetProperty("velocity").GetInt32() && speed.Animation==row.GetProperty("animation").GetInt32(),"Full source movement speed mismatch");
            var state=new ActorMotionState((uint)((100<<16)+N(1)),(uint)((100<<16)+N(1)),path,path[^1],0,speed.Velocity,speed.Velocity,N(5),0,new(0,0),new(0,0),20,0x820+(N(6)!=0?0x20000:0),0,[],0);
            state=SourceActorMotion.DirectionToNext(state);
            if(N(0)>=4){state=state with{Velocity=speed.Velocity,Index=path.Length,FinalTarget=N(0)==4?new(120,100):new(100,100)};}int frame=0;
            foreach(var expected in row.GetProperty("frames").EnumerateArray())
            {
                var queries=new List<Point2D>();int Collision(int x,int y){queries.Add(new(x,y));return x<80||x>=128||y<80||y>=128?0x3c01:N(4)==1&&x==103?1:N(4)==2&&y==102?0x100:0;}
                var result=SourceActorMotion.Advance(state,Collision);state=result.State;
                var actual=new long[]{result.Moving?1:0,state.X,state.Y,state.Flags,state.Points.Length,state.Index,state.Velocity,state.MaximumVelocity,state.Acceleration,state.AccelerationCounter,state.Direction.X,state.Direction.Y,state.Vector.X,state.Vector.Y,state.Distance,state.StepsCount,state.CollidedMask,result.RepathRequired?1:0};
                for(int i=0;i<actual.Length;i++){Require(actual[i]==expected[i].GetInt64(),$"Actor source state mismatch context{contexts} frame{frame} field{i}: actual{actual[i]} expected{expected[i]}");}
                Require(queries.SequenceEqual(expected[18].EnumerateArray().Select(Point)),"Actor collision query ordering mismatch");
                Require((N(6)!=0?state.SavedSteps:[]).SequenceEqual(expected[19].EnumerateArray().Select(Point)),"Actor saved crossings mismatch");
                // Component JSON compatibility only, not a saved production actor.
                if(frame==7){state=JsonSerializer.Deserialize<ActorMotionState>(JsonSerializer.Serialize(state));}
                frames++;frame++;
            }
            contexts++;
        }
        Require(contexts==3240 && frames==64800,"Incomplete full actor motion fixture");
        Require(SourceActorMotion.Speed(12,128,75,0)==(2304,96),"Blade default75percent source velocity golden");
        Require(!AssassinCombat.Implemented(CombatAbility.BladeSentinel),"Component enabled an unfinished blade handler");
        Log.Information("Actor motion: {Contexts} source contexts / {Frames} exact frames, collision ordering and component JSON round-trips pass; no moving blade actor",contexts,frames);
    }
}
