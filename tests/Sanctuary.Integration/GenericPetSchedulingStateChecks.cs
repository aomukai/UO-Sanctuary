// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GenericPetSchedulingStateChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetSchedulingStateChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int N(JsonElement e,string name)=>e.GetProperty(name).GetInt32();
    private static bool B(JsonElement e,string name)=>e.GetProperty(name).GetBoolean();
    private static int? Maybe(JsonElement e,string name)=>e.GetProperty(name).ValueKind==JsonValueKind.Null?null:N(e,name);
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-scheduling-state-check","Current uninterruptible completion and timed-event guards",_=>Core.LoopContext.Post(()=>
    {try{Source();Log.Information("pet-scheduling-state-check: passed");}catch(Exception e){Log.Error(e,"pet-scheduling-state-check: failed");}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-uninterruptible-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var wanted=row.GetProperty("output");var trace=new List<string>();var events=new GenericPetTimedEvents();
            var clock=new GenericPetEventClock(100,1,1,55,false,0,0,0);
            foreach(var(eventType,frame)in new[]{(2,110),(1,112),(2,115)}){events.Schedule(101,eventType,frame,()=>clock,()=>throw new InvalidOperationException());}
            if(B(p,"processing")){events.Processing(3);}
            int kind=N(p,"kind");bool game=B(p,"game"),state54=true,state92=B(p,"delayed");
            var services=new GenericPetUninterruptibleServices(_=>new(kind,game?71:0,checked((byte)N(p,"selector")),B(p,"root"),N(p,"root1c"),state92?0x10000000u:0,p.GetProperty("flags128").GetUInt32(),N(p,"lastKind"),N(p,"lastId")),
                (actor,state,value)=>
                {
                    Require(actor==(B(p,"actor")?101:0),"State toggle lost actor");trace.Add($"state:{state}:{value}");
                    if(state==54){state54=value!=0;if(p.GetProperty("after54Game").ValueKind!=JsonValueKind.Null){game=B(p,"after54Game");}kind=Maybe(p,"after54Kind")??kind;}
                    else{Require(state==92&&value==0,"Unexpected state toggle");state92=false;}
                },_=>N(p,"count"),(world,type,id)=>
                {Require(world==71&&type==N(p,"lastKind")&&id==N(p,"lastId"),"Last-attacker context changed");trace.Add($"resolve:{type}:{id}");return B(p,"attacker")?202:0;},
                (world,attacker,actor,packet)=>
                {Require(world==71&&attacker==202&&actor==101&&packet==new PreparedMonsterDamagePacket(0,0,0,0,2),"Fresh death return replayed damage or changed context");trace.Add("fresh:2");kind=Maybe(p,"afterReturnKind")??kind;},
                (world,actor,killer,detach)=>{Require(world==71&&actor==101&&killer==0&&detach==1,"Owner-loss context changed");trace.Add("loss:0:1");kind=Maybe(p,"afterLossKind")??kind;},
                events,handle=>{events.Remove(handle);trace.Add("remove:"+handle);});
            bool unsupported=false;try{GenericPetUninterruptible.Change(B(p,"actor")?101:0,N(p,"enabled"),services);}catch(NotSupportedException){unsupported=true;}
            Require(unsupported==B(wanted,"unsupported")&&kind==N(wanted,"kind")&&state54==B(wanted,"state54")&&state92==B(wanted,"state92"),$"Current completion guards/state writes differ at{count}");
            Require(trace.SequenceEqual(wanted.GetProperty("trace").EnumerateArray().Select(v=>v.GetString()))&&events.Unit(101).SequenceEqual(Ints(wanted.GetProperty("unit"))),$"Current completion callback/removal order differs at{count}");
            var actual=events.Nodes.Select(n=>new[]{n.Handle,n.Type,n.Frame,(int)n.Flags,n.Param0,n.Param1,n.Param2}).ToArray();var expected=wanted.GetProperty("nodes").EnumerateArray().Select(Ints).ToArray();
            Require(actual.Length==expected.Length&&!actual.Where((a,i)=>!a.SequenceEqual(expected[i])).Any(),$"Current completion node/processing fields differ at{count}");count++;
        }
        Require(count==37,"Missing current completion controls");
        Log.Information("Uninterruptible completion:37current contexts/649new bytes;shared mode policy and actual timed remover retain guards,state54/92 order,fresh zero-damage death return,owner-loss fallback,cached game and processing-node removal. Native death/return leaves remain explicit.");
    }
    private sealed class Completion(GenericPetTimedEvents events,int actor):MonsterModeCompletion
    {
        internal int Removed;
        internal override bool HasGame(BaseCreature body)=>body is{Deleted:false}&&body.Map!=null;
        internal override MonsterReturnServices ReturnServices(BaseCreature body,Mobile attacker)=>throw new NotSupportedException("Native delayed-death return remains a separate binding");
        internal override void DeleteQueuedThink(BaseCreature body)
        {
            Require(checked((int)body.Serial.Value)==actor,"Native completion lost original actor");
            foreach(int handle in events.Unit(actor)){if(events.Node(handle).Type==2){events.Remove(handle);Removed++;}}
        }
    }
    internal static void Original(SkeletonGuardian body)
    {
        Require(!MonsterUninterruptibles.Active(body)&&!MonsterUninterruptibles.Delayed(body),"Original retained unexpected mode state");
        int actor=checked((int)body.Serial.Value);var events=new GenericPetTimedEvents();var completion=new Completion(events,actor);
        var baseline=new GenericPetEventClock(100,1,1,232,false,0,0x80,0);
        GenericPetEventClock Live()=>GenericPetNativeControlStates.Scheduling(body,baseline);
        int old=events.Schedule(actor,2,110,Live,()=>throw new InvalidOperationException());
        int other=events.Schedule(actor,1,112,Live,()=>throw new InvalidOperationException());
        MonsterUninterruptibles.Change(body,true,completion);
        Require(MonsterUninterruptibles.Active(body)&&events.Node(old).Flags==2&&events.Unit(actor).SequenceEqual(new[]{other})&&Live().Rootb34==0x400080,
            "Actual mode activation failed to remove AI events or preserve unrelated bits");
        var switchInput=new GenericPetAiSwitchUnit(1,WarlockDemonPlacement.ClassId(body),1,1,4,0,true,0,232,true,-1,0x80,0);
        GenericPetAiSwitchServices Switch()=>new(_=>GenericPetNativeControlStates.Switching(body,switchInput),(_,_)=>new(0,1),(_,_)=>true);
        Require(!GenericPetAiSwitch.Check(actor,false,false,false,false,Switch()),"Actual uninterruptible state allowed AI switching");
        int callbacks=0;int next=events.Schedule(actor,2,115,Live,()=>{callbacks++;MonsterUninterruptibles.Change(body,false,completion);});
        Require(callbacks==1&&!MonsterUninterruptibles.Active(body)&&Live().Rootb34==0x80&&events.Unit(actor).SequenceEqual(new[]{next,other})
            &&events.Node(next).Frame==115&&completion.Removed==1,"Native source scheduling guard did not clear actual mode state and allocate exactly one future AI event");
        Require(GenericPetAiSwitch.Check(actor,false,false,false,false,Switch()),"Cleared mode state still prevented AI switching");
        Log.Information("Uninterruptible original:actual saved mode provider drives scheduling guard and AI switch;shared completion clears state,removes only prior AI and retains non-AI/future event. Native delayed-death leaves remain gated.");
    }
}
