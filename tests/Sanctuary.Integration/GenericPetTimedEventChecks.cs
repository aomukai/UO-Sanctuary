// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class GenericPetTimedEventChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetTimedEventChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int N(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-timed-events-check","Current timed AI producer/remover",_=>Core.LoopContext.Post(()=>
    {try{Source();Log.Information("pet-timed-events-check: passed");}catch(Exception e){Log.Error(e,"pet-timed-events-check: failed");}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-timed-events-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var events=new GenericPetTimedEvents();var trace=new List<string>();
            var live=new GenericPetEventClock(N(p,"now"),N(p,"mode"),checked((byte)N(p,"selector")),N(p,"stats"),p.GetProperty("hasRoot").GetBoolean(),N(p,"root1c"),p.GetProperty("rootb34").GetUInt32(),p.GetProperty("flags").GetUInt32());
            void Deferred()
            {
                trace.Add("deferred");var mode=p.GetProperty("deferredMode");var flags=p.GetProperty("deferredFlags");
                live=live with{Mode=mode.ValueKind==JsonValueKind.Null?live.Mode:mode.GetInt32(),Flags124=flags.ValueKind==JsonValueKind.Null?live.Flags124:flags.GetUInt32()};
            }
            int step=0;bool unsupported=false;
            foreach(var command in p.GetProperty("commands").EnumerateArray())
            {
                try
                {
                    switch(command[0].GetString())
                    {
                        case "add":events.Schedule(101,command[1].GetInt32(),command[2].GetInt32(),()=>live,Deferred,command[3].GetInt32(),command[4].GetInt32(),command[5].GetInt32());break;
                        case "idle":events.Idle(101,command[1].GetInt32(),()=>live,()=>{trace.Add("neutral");live=live with{Mode=1};},Deferred);break;
                        case "remove":events.Remove(command[1].GetInt32());break;
                        case "processing":events.Processing(command[1].GetInt32());break;
                        default:throw new InvalidOperationException("Unknown event probe command");
                    }
                }
                catch(NotSupportedException){unsupported=true;}
                var expected=row.GetProperty("output")[step++];
                Require(unsupported==expected.GetProperty("unsupported").GetBoolean()&&live.Mode==N(expected,"mode"),"Current event boundary/mode differs");
                Require(events.Unit(101).SequenceEqual(Ints(expected.GetProperty("unit")))&&trace.SequenceEqual(expected.GetProperty("trace").EnumerateArray().Select(v=>v.GetString())),"Current timed unit list/service order differs");
                var actual=events.Nodes.Select(n=>new[]{n.Handle,n.Type,n.Frame,(int)n.Flags,n.Param0,n.Param1,n.Param2}).ToArray();var rows=expected.GetProperty("nodes").EnumerateArray().Select(Ints).ToArray();
                Require(actual.Length==rows.Length&&!actual.Where((a,i)=>!a.SequenceEqual(rows[i])).Any(),"Current event fields/reuse/processing-removal differ");
                for(int bucket=0;bucket<64;bucket++)
                {
                    var wanted=expected.GetProperty("buckets").TryGetProperty(bucket.ToString(),out var values)?Ints(values):[];
                    Require(events.Bucket(bucket).SequenceEqual(wanted),"Current timed bucket newest order differs");
                }
                if(unsupported){break;}
            }
            count++;
        }
        Require(count==31,"Missing timed-event controls");
        Log.Information("Timed events:31current sequences/1796retained bytes;frame clamp,per-unit and64-frame bucket insertion,removal,processing flags,reuse,neutral idle replacement andpost-service status match. Available pool/null callback are declared;dispatch/timer remains gated.");
    }
    internal static GenericPetEventBinding Bind(SkeletonGuardian body,GenericPetRootContext context,GenericPetRootServices root,GenericPetTimedEvents pending=null)
    {
        int actor=checked((int)body.Serial.Value);var events=pending??new GenericPetTimedEvents();var clock=new GenericPetEventClock(100,1,1,55,false,0,0,0);
        if(pending==null)
        {
            events.Schedule(actor,2,110,()=>clock,()=>throw new InvalidOperationException());
            events.Schedule(actor,1,120,()=>clock,()=>throw new InvalidOperationException());
            events.Schedule(actor,2,111,()=>clock,()=>throw new InvalidOperationException());
        }
        var retained=events.Unit(actor).Where(id=>events.Node(id).Type!=2).ToArray();
        var binding=events.Bind(context,root,actor);var removal=root.RemoveEvent;var finish=root.Finish;
        return binding with{Services=binding.Services with
        {
            RemoveEvent=index=>{binding.Services.RemoveEvent(index);removal(index);},
            Finish=memory=>{Require(events.Unit(actor).Where(id=>(events.Node(id).Flags&1)==0).SequenceEqual(retained),"Root did not remove exactly timed AI events");finish(memory);}
        }};
    }
}
