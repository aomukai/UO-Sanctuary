// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class GenericPetAiPostTargetChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetAiPostTargetChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int N(JsonElement e,string k)=>e.GetProperty(k).GetInt32();
    private static bool B(JsonElement e,string k)=>e.GetProperty(k).GetBoolean();
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-ai-post-target-check","Current AI post-target policy",_=>Core.LoopContext.Post(()=>
    {try{Source();Log.Information("pet-ai-post-target-check: passed");}catch(Exception e){Log.Error(e,"pet-ai-post-target-check: failed");}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-ai-post-target-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var expected=row.GetProperty("output");var trace=new List<string>();byte selector=1;int cls=742;
            var tick=new GenericPetAiTick{Target=B(p,"target")?7:0,Distance=N(p,"distance"),Combat=37};
            var services=new GenericPetAiPostServices(()=>selector,_=>cls,
                (_,t)=>{trace.Add("first");if(B(p,"clearFirst")){t.Target=0;}return B(p,"first");},
                (_,t)=>{trace.Add("second");if(B(p,"changeSecond")){t.Target=8;t.Distance=80;}return B(p,"second");},
                (s,id)=>{trace.Add($"primary:{s}:{id}");if(B(p,"changeLookup")){selector=2;cls=743;}return B(p,"primary")?new(id,"explicit",checked((byte)N(p,"primaryFlags")),0):null;},
                (s,id)=>{trace.Add($"secondary:{s}:{id}");return B(p,"secondary")?(N(p,"secondaryFlags")&4)!=0:null;},
                _=>{trace.Add("room");return B(p,"room")?40:null;},
                (s,id)=>{trace.Add($"level:{s}:{id}");return B(p,"level")?new(id,"explicit",checked((byte)N(p,"walk"))):null;},
                (_,target)=>{trace.Add($"special:{target}");return N(p,"special");},
                (_,descriptor,search)=>{Require(search.Found==0,"Search result was invented");trace.Add($"search:{descriptor}:{search.Previous}:{search.Distance}");search.Found=B(p,"found")?8:0;search.Distance=N(p,"foundDistance");},
                (_,radius)=>trace.Add($"wander:{radius}"));
            bool result=false,unsupported=false;try{result=GenericPetAiPostTarget.Run(B(p,"actor")?11:0,tick,services);}catch(NotSupportedException){unsupported=true;}
            Require(unsupported==B(expected,"unsupported")&&trace.SequenceEqual(expected.GetProperty("trace").EnumerateArray().Select(v=>v.GetString())),$"AI post-target boundary/trace differs at{count}");
            Require(tick.Target==N(expected,"target")&&tick.Distance==N(expected,"distance")&&tick.Combat==N(expected,"combat"),$"AI post-target frame differs at{count}");
            if(!unsupported){Require(result==B(expected,"result"),$"AI post-target result differs at{count}");}
            count++;
        }
        Require(count==30,"Missing post-target controls");
        Log.Information("AI post-target:30current contexts/390bytes;no-target and helper exits,live metadata lookup order,unsigned range,special predicate and retarget/wander match. Helper internals/native search context remain explicit.");
    }
    internal static bool Original(SkeletonGuardian body,GenericPetAiTick tick)
    {
        Require(tick.Target==0,"GenericPet type0 unexpectedly selected a pre-root target");
        var services=new GenericPetAiPostServices(
            ()=>throw new NotSupportedException("Live post-target selector is unbound"),_=>throw new NotSupportedException("Live post-target unit is unbound"),
            (_,_)=>throw new NotSupportedException("First post-target helper is unbound"),(_,_)=>throw new NotSupportedException("Second post-target helper is unbound"),
            (_,_)=>throw new NotSupportedException("Live primary lookup is unbound"),(_,_)=>throw new NotSupportedException("Live secondary lookup is unbound"),
            _=>throw new NotSupportedException("Live post-target room is unbound"),(_,_)=>throw new NotSupportedException("Live level lookup is unbound"),
            (_,_)=>throw new NotSupportedException("Live special predicate is unbound"),(_,_,_)=>throw new NotSupportedException("Live descriptor9search is unbound"),
            (_,_)=>throw new NotSupportedException("Live wander is unbound"));
        bool stopped=GenericPetAiPostTarget.Run(checked((int)body.Serial.Value),tick,services);
        Require(!stopped&&tick.Target==0,"No-target post stage changed the original context");
        Log.Information("AI post-target original:type0 no-target path reaches registered root without unqualified helper calls.");
        return stopped;
    }
}
