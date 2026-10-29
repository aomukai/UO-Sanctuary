// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetTraversalChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetTraversalChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int[] Ints(JsonElement v)=>v.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static Dictionary<int,int> Map(JsonElement v)=>v.EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>x.Value.GetInt32());
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Current path refresh and complete traversal dispatch",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-traversal-check");Register("generic-pet-traversal-reload-check");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-traversal-source-probe.json")));int count=0,fatal=0;
        foreach(var entry in doc.RootElement.EnumerateArray())
        {
            var p=entry[0];var rawUnits=p.GetProperty("units").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>Ints(x.Value));
            var units=rawUnits.ToDictionary(x=>x.Key,x=>
            {
                int[] r=x.Value;return new GenericPetTraversalUnit(r[0],r[1],r[2],(uint)r[3],(uint)r[4],r[5]==0?null:new(r[6],r[7],r[8]),r[9]!=0,r[10]==0?null:new(r[11],r[12],r[13],r[14]));
            });
            var next158=Map(p.GetProperty("next158"));var next160=Map(p.GetProperty("next160"));var tiles=Map(p.GetProperty("tiles"));var roomMap=Map(p.GetProperty("roomMap"));
            var rooms=p.GetProperty("rooms").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>new GenericPetTraversalRoom(x.Value[0].GetInt32(),x.Value[1].GetInt32(),Ints(x.Value[2]),Ints(x.Value[3])));
            var proxies=p.GetProperty("proxies").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>{int[] r=Ints(x.Value);return new GenericPetTraversalProxy((byte)r[0],r[1],r[2],r[3],r[4]);});
            var nodes=p.GetProperty("nodes").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>new GenericPetOwnerNode(x.Value[0].GetInt32(),x.Value[1].GetInt32()));
            var trace=new List<object[]>();void A(params object[] values)=>trace.Add(values);
            var registry=new GenericPetRegistryServices(
                (game,type,id)=>{int first=p.GetProperty("buckets").TryGetProperty(game+":"+type+":"+id,out var raw)?raw.GetInt32():0;A(0,game,type,id,first);return first;},
                game=>tiles.GetValueOrDefault(game),id=>units[id].Id,id=>next158.GetValueOrDefault(id));
            long Callback(int kind,int target)
            {
                var results=p.GetProperty(kind==1?"results":"overrideResults");long value=results.TryGetProperty(target.ToString(),out var raw)?raw.GetInt64():0;A(2,kind,target,value);
                if(p.GetProperty("change160").TryGetProperty(target.ToString(),out var next)){next160[target]=next.GetInt32();}
                if(p.GetProperty("changeNodes").TryGetProperty(target.ToString(),out var changed)){int n=changed[0].GetInt32();nodes[n]=nodes[n] with{Next=changed[1].GetInt32()};}
                return value;
            }
            var services=new GenericPetTraversalServices(id=>units[id],(game,type,id)=>GenericPetRegistry.Resolve(game,type,id,registry),
                id=>{int room=roomMap.GetValueOrDefault(id);A(1,id,room);return room;},id=>rooms[id],id=>next160.GetValueOrDefault(id),id=>proxies[id],id=>nodes[id],
                _=>new(p.GetProperty("kind").GetInt32(),p.GetProperty("override").GetBoolean()?target=>Callback(2,target):null));
            int failed=0;long result=0;
            try
            {
                int game=p.GetProperty("game").GetInt32(),subject=p.GetProperty("subject").GetInt32();
                switch(p.GetProperty("entry").GetString())
                {
                    case "refresh":GenericPetTraversal.Refresh(game,subject,services);break;
                    case "resolve":result=GenericPetRegistry.Resolve(game,p.GetProperty("lookupType").GetInt32(),p.GetProperty("lookupId").GetInt32(),registry);break;
                    default:result=GenericPetTraversal.Visit(game,subject,p.GetProperty("index").GetInt32(),p.GetProperty("supplied").GetBoolean()?target=>Callback(1,target):null,services);break;
                }
            }
            catch(NotSupportedException){failed=1;fatal++;A(99);}
            var pathValues=rawUnits.Select(pair=>units[pair.Key].Path?.Target??pair.Value[6]).ToArray();var cached=proxies.Values.Select(proxy=>proxy.CachedTarget).ToArray();
            string actual=JsonSerializer.Serialize(new object[]{failed,result,pathValues,cached,trace});
            Require(actual==entry[1].GetRawText(),"Current traversal differs:"+count+":"+actual[..Math.Min(actual.Length,1200)]);count++;
        }
        Require(count==3348&&fatal==1776,"Incomplete traversal matrix:"+count+"/"+fatal);
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Traversal enabled incomplete demons");
        Log.Information("GenericPet traversal:3348current-code contexts,1776explicit source boundaries;registry,stale path/proxy caches,all dispatch paths,owner closure,64bit exits and live link order pass;real world graph/helper/lifecycle bindings remain gated");
    }
}
