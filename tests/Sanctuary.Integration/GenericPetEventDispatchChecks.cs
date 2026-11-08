// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class GenericPetEventDispatchChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetEventDispatchChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int N(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-event-dispatch-check","Current monster event dispatch and typed route",_=>Core.LoopContext.Post(()=>
    {try{Source();Log.Information("pet-event-dispatch-check: passed");}catch(Exception e){Log.Error(e,"pet-event-dispatch-check: failed");}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-event-dispatch-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var events=new GenericPetTimedEvents();var trace=new List<string>();int callbacks=0;
            var live=new GenericPetEventClock(N(p,"now"),N(p,"mode"),checked((byte)N(p,"selector")),N(p,"stats"),p.GetProperty("hasRoot").GetBoolean(),N(p,"root1c"),p.GetProperty("rootb34").GetUInt32(),p.GetProperty("flags").GetUInt32());
            void Deferred(){trace.Add("deferred");}
            void Neutral(){trace.Add("neutral");live=live with{Mode=1};}
            void Handle(GenericPetTimedEvent node)
            {
                trace.Add($"route:{node.Handle}:{node.Type}:{node.Param0}:{node.Param1}:{node.Param2}");callbacks++;
                if(!p.GetProperty("actions").TryGetProperty(callbacks.ToString(),out var actions)){return;}
                foreach(var cmd in actions.EnumerateArray())
                {
                    switch(cmd[0].GetString())
                    {
                        case "remove":events.Remove(cmd[1].GetInt32()==-1?node.Handle:cmd[1].GetInt32());break;
                        case "idle":events.Idle(101,cmd[1].GetInt32(),()=>live,Neutral,Deferred);break;
                        case "add":events.Schedule(101,cmd[1].GetInt32(),cmd[2].GetInt32(),()=>live,Deferred,cmd[3].GetInt32(),cmd[4].GetInt32(),cmd[5].GetInt32());break;
                        default:throw new InvalidOperationException("Unknown event callback action");
                    }
                }
            }
            int step=0;bool unsupported=false;
            foreach(var cmd in p.GetProperty("commands").EnumerateArray())
            {
                try
                {
                    switch(cmd[0].GetString())
                    {
                        case "add":events.Schedule(101,cmd[1].GetInt32(),cmd[2].GetInt32(),()=>live,Deferred,cmd[3].GetInt32(),cmd[4].GetInt32(),cmd[5].GetInt32());break;
                        case "dispatch":
                            live=live with{Frame=cmd[2].GetInt32()};
                            events.Dispatch(cmd[1].GetInt32(),live.Frame,cmd[3].GetInt32()!=0,node=>GenericPetMonsterEventRouter.Run(node,
                                _=>new(live.Selector,live.StatCount,live.HasStatRoot,live.Root1c,p.GetProperty("rootb30").GetUInt32(),live.Flags124,live.Mode),Handle));break;
                        default:throw new InvalidOperationException("Unknown dispatch command");
                    }
                }
                catch(NotSupportedException){unsupported=true;}
                var expected=row.GetProperty("output")[step++];
                Require(unsupported==expected.GetProperty("unsupported").GetBoolean()&&live.Mode==N(expected,"mode")&&events.Cursor==N(expected,"cursor"),"Current dispatch boundary/mode/cursor differs");
                Require(events.Unit(101).SequenceEqual(Ints(expected.GetProperty("unit")))&&trace.SequenceEqual(expected.GetProperty("trace").EnumerateArray().Select(v=>v.GetString())),"Current dispatch/callback order differs");
                var actual=events.Nodes.Select(n=>new[]{n.Handle,n.Type,n.Frame,(int)n.Flags,n.Param0,n.Param1,n.Param2}).ToArray();var rows=expected.GetProperty("nodes").EnumerateArray().Select(Ints).ToArray();
                Require(actual.Length==rows.Length&&!actual.Where((a,i)=>!a.SequenceEqual(rows[i])).Any(),"Current dispatch fields/removal/reuse differ");
                for(int bucket=0;bucket<64;bucket++)
                {var wanted=expected.GetProperty("buckets").TryGetProperty(bucket.ToString(),out var values)?Ints(values):[];Require(events.Bucket(bucket).SequenceEqual(wanted),"Dispatch changed bucket linkage incorrectly");}
                if(unsupported){break;}
            }
            count++;
        }
        Require(count==36,"Missing dispatcher sequences");
        Log.Information("Event dispatch:36current sequences;722-byte executor+324-byte typed router,exact due frame,self/upcoming removal,creation,idle,processing completion and all15table slots match. Host clock/fullAI entry remain gated.");
    }
    internal static void Native(SkeletonGuardian body,int owner,GenericPetWorldIdentity world,GenericPetInitializationServices leaves)
    {
        int actor=checked((int)body.Serial.Value);var events=new GenericPetTimedEvents();var clock=new GenericPetEventClock(100,4,1,55,false,0,0,0);
        int current=events.Schedule(actor,2,115,()=>clock,()=>throw new InvalidOperationException());
        int upcoming=events.Schedule(actor,2,179,()=>clock,()=>throw new InvalidOperationException());
        int retained=events.Schedule(actor,1,120,()=>clock,()=>throw new InvalidOperationException());int calls=0;
        events.Dispatch(current,115,true,node=>GenericPetMonsterEventRouter.Run(node,
            _=>new(1,55,false,0,0,0,DeathMarkWorld.State(body).Mode),active=>
            {
                Require(active.Type==2&&active.Handle==current&&(active.Flags&1)!=0&&events.Cursor==upcoming,"Original root callback entered wrong queue event");
                calls++;GenericPetControlChecks.Root(body,owner,world,leaves,events);
                Require(events.Node(current).Flags==9,"Root did not defer removal of its own processing event");
            }));
        Require(calls==1&&events.Unit(actor).SequenceEqual(new[]{retained})&&events.Node(current).Flags==2&&events.Node(upcoming).Flags==2&&events.Cursor==0,"Original root did not finish self/upcoming cancellation through source cursor");
        Log.Information("Timed event root:original demon callback removes its processing AI event and upcoming same-bucket event,retains non-AI entry,publishes controller memory. Explicit root fixture;fullAI entry and automatic ticking remain gated.");
    }
}
