// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class GenericPetAiEntryChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetAiEntryChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static bool B(JsonElement e,string key)=>e.GetProperty(key).GetBoolean();
    private static int N(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-ai-entry-check","Current full AI-entry orchestration",_=>Core.LoopContext.Post(()=>
    {try{Source();Log.Information("pet-ai-entry-check: passed");}catch(Exception e){Log.Error(e,"pet-ai-entry-check: failed");}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-ai-entry-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var expected=row.GetProperty("output");var trace=new List<string>();
            var controls=new[]{new GenericPetAiControl(11,B(p,"function")?0x4b60c0:0,N(p,"field14"),B(p,"executing"),[]),
                new GenericPetAiControl(12,B(p,"function")?0x4b7000:0,N(p,"field14"),B(p,"executing"),[])};
            foreach(var entry in p.GetProperty("queue").EnumerateArray()){controls[B(p,"preSwap")||B(p,"postSwap")?1:0].Queue.Add(new(entry[0].GetInt32(),entry[1].GetInt32()));}
            var special=new[]{new GenericPetAiOverride(N(p,"flags"),B(p,"specialFunction")?0x4b63d0:0),new GenericPetAiOverride(N(p,"flags"),B(p,"specialFunction")?0x4b66e0:0)};
            var game=new GenericPetAiGame(0x12345678,2,1,3);int classId=742;GenericPetAiTick observed=null;
            var services=new GenericPetAiEntryServices(()=>game,_=>new(N(p,"kind"),classId,B(p,"data"),B(p,"control")?controls[0]:null,B(p,"snapshot")?(byte)4:(byte)0,0xbbaa998877665544),
                (selector,id)=>{trace.Add($"rowA:{selector}:{id}");if(B(p,"mutateLookup")){game=game with{Selector106=2};classId=743;}return B(p,"rowA")?1:0;},
                (selector,id)=>{trace.Add($"rowB:{selector}:{id}");return B(p,"rowB")?2:0;},index=>special[index],tick=>
                {
                    observed=tick;trace.Add("preprocess");if(B(p,"preSwap")){tick.Control=controls[1];}
                    var replacement=p.GetProperty("preSpecial");if(replacement.ValueKind!=JsonValueKind.Null){tick.Special=special[replacement.GetInt32()];}
                    return B(p,"preStop");
                },(_,state)=>{trace.Add($"ai:{state}");return N(p,"type");},(type,tick)=>
                {trace.Add($"target:{type}:{(tick.Snapshot>>32)&255}");tick.Distance=19;tick.Combat=1;return B(p,"target")?7:0;},tick=>
                {trace.Add("post");if(B(p,"postSwap")){tick.Control=controls[1];}return B(p,"postStop");},(entry,actor)=>
                {Require(actor==11,"Predicate lost original actor");trace.Add($"predicate:{entry.Token}:{entry.Command}");return !Ints(p.GetProperty("reject")).Contains(entry.Token);},(command,actor,control)=>
                {
                    Require(actor==11,"Queued executor lost actor");int index=Array.IndexOf(controls,control);trace.Add($"queued:{command}:{index+1}");
                    if(B(p,"queueFunction")){control.Function=0x4b7000;}
                    if(B(p,"append")&&command==101){control.Queue.Add(new(2,202));}
                },token=>trace.Add($"release:{token}"),(function,tick,normal)=>trace.Add($"call:{function}:{normal}"),(function,tick)=>trace.Add($"default:{function}"));
            bool unsupported=false;
            try{observed=GenericPetAiEntry.Run(B(p,"actor")?11:0,checked((byte)N(p,"tail")),services);}
            catch(NotSupportedException){unsupported=true;}
            Require(unsupported==B(expected,"unsupported"),$"AI entry refusal differs at{count}");
            Require(trace.SequenceEqual(expected.GetProperty("trace").EnumerateArray().Select(v=>v.GetString())), $"AI entry call order differs at{count}: {string.Join(',',trace)}");
            if(observed!=null)
            {
                var tick=expected.GetProperty("tick");int control=observed.Control==null?0:Array.IndexOf(controls,observed.Control)+1;
                Require(control==N(tick,"control")&&observed.Raw08==tick.GetProperty("raw08").GetUInt64()&&observed.Raw18==tick.GetProperty("raw18").GetUInt64()
                    &&observed.Target==N(tick,"target")&&observed.Distance==N(tick,"distance")&&observed.Combat==N(tick,"combat")&&observed.RowA==N(tick,"rowA")&&observed.RowB==N(tick,"rowB")
                    &&observed.Snapshot==tick.GetProperty("snapshot").GetUInt64(),$"AI entry frame differs at{count}");
                var wanted=tick.GetProperty("special");Require(wanted.ValueKind==JsonValueKind.Null?observed.Special==null:
                    observed.Special==new GenericPetAiOverride(N(wanted,"flags"),N(wanted,"function")),"AI entry override snapshot differs");
            }
            else{Require(unsupported,"Unobserved successful AI frame");}
            for(int i=0;i<2;i++)
            {
                var wanted=expected.GetProperty("controls")[i];Require(controls[i].Executing==B(wanted,"executing")&&controls[i].Function==N(wanted,"function")
                    &&controls[i].Queue.Select(e=>(e.Token,e.Command)).SequenceEqual(wanted.GetProperty("queue").EnumerateArray().Select(e=>(e[0].GetInt32(),e[1].GetInt32()))),$"AI entry FIFO/controller differs at{count}");
            }
            count++;
        }
        Require(count==57,"Missing full-entry controls");
        Log.Information("AI entry:57complete-current contexts/1096bytes;metadata/snapshot ordering,all target modes,original/replaced overrides,stable queued predicates/FIFO/release and current default call match. Helper/native-world internals remain explicit.");
    }
    internal static void Original(SkeletonGuardian body,int owner,GenericPetWorldIdentity world,GenericPetInitializationServices leaves,GenericPetTimedEvents events,
        Func<GenericPetAiControl,GenericPetAiTick,bool> preprocess=null,bool expectPreprocessStop=false)
    {
        int id=checked((int)body.Serial.Value),sourceClass=WarlockDemonPlacement.ClassId(body);int calls=0;
        // Both current table indices are exercised as explicit fixtures. Neither
        // Field14 nor override Mode10=1 is asserted as an authentic birth value.
        int index=sourceClass%2;var control=new GenericPetAiControl(0,0x4b60c0,index,false,[]);
        void Default(int function,GenericPetAiTick tick)
        {
            Require(function==0x4b60c0&&ReferenceEquals(tick.Control,control)&&tick.RowA==1&&tick.RowB==2,"Original entry/default context changed");
            calls++;GenericPetControlChecks.Root(body,owner,world,leaves,events);
        }
        var services=new GenericPetAiEntryServices(()=>new(0x12345678,2,1,3),actor=>
        {Require(actor==id,"AI entry bound wrong original");return new(1,sourceClass,true,control,0,0);},
            (selector,classId)=>{Require(selector==1&&classId==sourceClass,"Original rowA lookup changed");return 1;},
            (selector,classId)=>{Require(selector==1&&classId==sourceClass,"Original rowB lookup changed");return 2;},
            GenericPetAiOverrideTables.At,tick=>preprocess==null?GenericPetAiPreprocessChecks.Original(body,control,tick):preprocess(control,tick),(_,state)=>
            {Require(state==0,"Original fixture state changed");return GenericPetAiSelectionChecks.Original(body,state);},(_,_)=>throw new InvalidOperationException("GenericPet type0 should reuse its root targeting"),
            tick=>GenericPetAiPostTargetChecks.Original(body,tick),(_,_)=>throw new InvalidOperationException("Unexpected queued predicate"),(_,_,_)=>throw new InvalidOperationException("Unexpected queued action"),
            _=>throw new InvalidOperationException("Unexpected release"),(function,tick,fallback)=>
            {
                Require(function==GenericPetAiOverrideTables.At(index).Function,"Original override callback identity changed");
                GenericPetAiOverrideChecks.Original(body,owner,world,leaves,events,index,control,tick,fallback,Default);
            },Default);
        var result=GenericPetAiEntry.Run(id,0,services);Require(calls==(expectPreprocessStop?0:1)&&ReferenceEquals(result.Control,control)&&!control.Executing,"Original entry did not finish its expected preprocess/root path");
        if(expectPreprocessStop){Log.Information("AI entry original:live preprocessing stopped before default root;automatic admission remains gated.");}
        else{Log.Information("AI entry original:typed event->full prepared entry->registered demon root on same event store;birth/wound/command/control retained. Explicit helper/control fixtures;automatic admission remains gated.");}
    }
}
