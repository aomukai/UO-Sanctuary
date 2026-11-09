// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;
public static class GenericPetAiOverrideChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetAiOverrideChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int N(JsonElement e,string k)=>e.GetProperty(k).GetInt32();
    private static bool B(JsonElement e,string k)=>e.GetProperty(k).GetBoolean();
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-ai-overrides-check","Current special AI overrides and default initializer",_=>Core.LoopContext.Post(()=>
    {try{Source();Log.Information("pet-ai-overrides-check: passed");}catch(Exception e){Log.Error(e,"pet-ai-overrides-check: failed");}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-ai-overrides-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var expected=row.GetProperty("output");var trace=new List<string>();var removed=new List<int>();int mode=N(p,"mode"),follows=0;
            var control=new GenericPetAiControl(N(p,"state"),0x4b60c0,N(p,"index"),false,[]);var tick=new GenericPetAiTick{Control=B(p,"control")?control:null};
            GenericPetAiOverrideUnit Unit(int unit)=>unit==11?new(new(N(p,"kind"),B(p,"path"),N(p,"x"),N(p,"y")),mode,p.GetProperty("flags124").GetUInt32(),p.GetProperty("flags128").GetUInt32(),B(p,"data"),B(p,"actorControl"),B(p,"ownerGame")?1:0,0,77)
                :unit==22?new(new(N(p,"ownerKind"),B(p,"ownerPath"),N(p,"ox"),N(p,"oy")),N(p,"ownerMode"),p.GetProperty("ownerFlags124").GetUInt32(),p.GetProperty("ownerFlags128").GetUInt32(),false,false,0,0,0):throw new InvalidOperationException("Unknown override unit");
            var survey=new GenericPetOwnerSurveyServices(_=>throw new InvalidOperationException("Swarm0 unit read"),(_,_)=>throw new InvalidOperationException("Swarm0 metadata read"),_=>throw new InvalidOperationException("Swarm0 target read"),(_,_,_)=>throw new InvalidOperationException("Swarm0 validity read"),_=>throw new InvalidOperationException("Swarm0 path refresh"));
            var initialize=new GenericPetInitializationServices((_,_,_)=>throw new InvalidOperationException("Default initializer consumed a command"),_=>throw new InvalidOperationException("Default initializer queried life"),id=>Unit(id).Position.Kind,
                (_,visit)=>{trace.Add("iterator");foreach(var id in p.GetProperty("visits").EnumerateArray()){visit(id.GetInt32());}},survey);
            var events=new GenericPetTimedEvents();var handles=new Dictionary<int,int>();var logical=new Dictionary<int,int>();var clock=new GenericPetEventClock(1,1,1,232,false,0,0,0);
            var types=p.GetProperty("events").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            for(int i=types.Length-1;i>=0;i--){int handle=events.Schedule(101,types[i],20+i,()=>clock,()=>throw new InvalidOperationException("Unexpected deferred event"));handles.Add(i+1,handle);logical.Add(handle,i+1);}
            GenericPetAiOverrideFrame initial=null;int releases=0;
            var services=new GenericPetAiOverrideServices(c=>ReferenceEquals(c,control)?new(N(p,"state"),N(p,"mode10")):throw new NotSupportedException("Missing control"),Unit,
                (game,kind,id)=>{Require(game==1&&kind==0&&id==77,"Owner registry arguments changed");trace.Add("resolve:0:77");return B(p,"owner")?22:0;},
                (actor,owner)=>{trace.Add($"initialize:{(owner==22?1:0)}");return initial=GenericPetAiOverrides.Initialize(actor,owner,1,initialize);},
                (_,_,_)=>{trace.Add("follow");var list=p.GetProperty("follow");return list[Math.Min(follows++,list.GetArrayLength()-1)].GetBoolean();},
                (_,_,_)=>{trace.Add("default");var after=p.GetProperty("afterMode");if(after.ValueKind!=JsonValueKind.Null){mode=after.GetInt32();}},
                _=>{trace.Add("moving");return B(p,"moving");},
                (_,_,_,mask,target)=>{trace.Add($"search:{mask}:{target}");return new(N(p,"high"),B(p,"searchExternal"));},
                _=>events.Unit(101).Select(h=>logical[h]).ToArray(),id=>checked((byte)events.Node(handles[id]).Type),
                id=>{trace.Add($"remove:{id}");removed.Add(id);events.Remove(handles[id]);},(_,delay)=>trace.Add($"idle:{delay}"),
                _=>trace.Add("releaseSearch"),_=>releases++,(_,a,b)=>trace.Add($"missing:{a}:{b}"));
            bool unsupported=false;try{GenericPetAiOverrides.Run(N(p,"index"),B(p,"actor")?11:0,tick,B(p,"fallback")?0x4b60c0:0,services);}catch(NotSupportedException){unsupported=true;}
            Require(unsupported==B(expected,"unsupported")&&trace.SequenceEqual(expected.GetProperty("trace").EnumerateArray().Select(v=>v.GetString())),$"AI override boundary/trace differs at{count}");
            Require(mode==N(expected,"mode")&&removed.SequenceEqual(expected.GetProperty("removed").EnumerateArray().Select(v=>v.GetInt32())),$"AI override event/mode differs at{count}");
            foreach(int id in removed){Require(events.Node(handles[id]).Flags==2,"Override did not remove its actual timed event");}
            foreach(int id in Enumerable.Range(1,types.Length).Except(removed)){Require(events.Node(handles[id]).Flags==0,"Override removed an unrelated event");}
            var wanted=expected.GetProperty("initial");Require((initial==null)==(wanted.ValueKind==JsonValueKind.Null),"Override initialization gate changed");
            if(initial!=null)
            {
                var a=initial.Decision;Require(new[]{a.Parameters.LeashSquared,a.Parameters.WarpSquared,a.Parameters.CloseRaw,a.Parameters.WanderRaw}.SequenceEqual(wanted.GetProperty("prefix").EnumerateArray().Select(v=>v.GetInt32()))
                    &&new[]{a.Memory.Value0,a.Memory.Value1,a.Memory.Value2}.SequenceEqual(wanted.GetProperty("memory").EnumerateArray().Select(v=>v.GetInt32()))&&a.Memory.Target==0&&initial.Survey.Visits==N(wanted,"visits"),$"Shared default initializer differs at{count}");
                if(!unsupported){Require(releases==1,"Managed frame logical lifetime changed");}
            }
            count++;
        }
        Require(count==74,"Missing both override/initializer controls");
        Require(GenericPetAiOverrideTables.At(0)==new GenericPetAiOverride(0,0x4b63d0)&&GenericPetAiOverrideTables.At(1)==new GenericPetAiOverride(0,0x4b66e0),"Current override table changed");
        Log.Information("AI overrides:74current contexts/3410composed bytes;both callbacks/default initializer reuse copy,survey,coordinates and distance. Registry/validity/follow/default/search/event order and logical cleanup match;birth/header/world leaves remain explicit.");
    }
    internal static void Original(SkeletonGuardian body,int owner,GenericPetWorldIdentity world,GenericPetInitializationServices leaves,GenericPetTimedEvents events,int index,GenericPetAiControl control,GenericPetAiTick tick,int fallback,Action<int,GenericPetAiTick> callDefault)
    {
        int id=checked((int)body.Serial.Value);var master=body.GetMaster();int calls=0,searches=0;
        // Header Mode10=1 and source mode1 are explicit fixture inputs. Native
        // serials,owner resolution,survey and default root use existing bindings.
        GenericPetAiOverrideUnit Unit(int who)=>who==id?new(new(1,true,body.X*2,body.Y*2),1,0,0,true,true,world.Game,0,owner)
            :who==owner?new(new(0,true,master.X*2,master.Y*2),1,0,0,false,false,0,0,0):throw new InvalidOperationException("Wrong original override identity");
        var services=new GenericPetAiOverrideServices(c=>ReferenceEquals(c,control)?new(c.State,1):throw new InvalidOperationException("Wrong override header"),Unit,world.Resolve,
            (actor,found)=>{Require(actor==id&&found==owner,"Original override owner changed");return GenericPetAiOverrides.Initialize(actor,found,1,leaves);},
            (_,_,_)=>throw new InvalidOperationException("Original fixture unexpectedly exceeded leash"),
            (function,actor,t)=>{Require(actor==id,"Wrong original default actor");calls++;callDefault(function,t);},
            _=>throw new InvalidOperationException("Original mode1 should not query moving"),
            (_,_,_,mask,target)=>{Require(mask==0&&target==0,"Original search arguments changed");searches++;return new(1,false);},
            _=>events.Unit(id),handle=>checked((byte)events.Node(handle).Type),events.Remove,
            (_,_)=>throw new InvalidOperationException("Original high48 fixture should not idle"),_=>throw new InvalidOperationException("Original search is inline"),_=>{},
            (_,_,_)=>throw new InvalidOperationException("Original registered owner disappeared"));
        GenericPetAiOverrides.Run(index,id,tick,fallback,services);Require(calls==1&&searches==1,"Original override did not retain its fallback/search order");
        Log.Information($"AI override original:index{index} actual table callback->shared initializer/survey->registered root;explicit header/mode/search fixture;no birth default asserted.");
    }
}
