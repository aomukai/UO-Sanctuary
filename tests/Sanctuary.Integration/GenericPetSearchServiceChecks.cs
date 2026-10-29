// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetSearchServiceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetSearchServiceChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int[] Ints(JsonElement v)=>v.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static GenericPetCombatCandidate Candidate(JsonElement r)=>new(r[0].GetInt32(),r[1].GetInt64(),r[2].GetInt32(),r[3].GetInt32(),r[4].GetInt32(),r[5].GetInt32(),r[6].GetInt32(),r[7].GetByte(),r[8].GetByte(),r[9].GetByte(),r[10].GetByte());
    private static object[] Row(GenericPetCombatCandidate r)=>[r.Target,r.Opaque8,r.Rating,r.SkillId,r.Opaque24,r.Opaque28,r.Counter,r.Byte36,r.BaseModeFlag,r.Byte38,r.Byte39];
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Nine actual search helpers and full pipeline",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-search-services-check");Register("generic-pet-search-services-reload-check");}
    internal sealed record Scene(GenericPetSearchWrapperContext Input,List<GenericPetCombatCandidate> Rows,
        GenericPetSearchWrapperServices Wrapper,GenericPetSearchWorldServices World,Dictionary<int,GenericPetSearchWorldUnit> Units,
        Dictionary<int,GenericPetTraversalUnit> Traversal,List<object[]> Trace);
    internal static Scene Create(JsonElement p)
    {
            int[] v=Ints(p.GetProperty("params"));
            var input=new GenericPetSearchWrapperContext(p.GetProperty("selector").GetByte(),p.GetProperty("pet").GetInt32(),p.GetProperty("owner").GetInt32(),
                p.GetProperty("config").GetBoolean()?new(0,0,0,0,v[0],(ushort)v[1],0,false,0,0,0,0,v[2],v[3],0,v[4],v[5],0,0,0):null,
                p.GetProperty("swarm").EnumerateArray().Select(r=>new GenericPetSearchSwarm(r[0].GetInt32(),r[1].GetInt32())).ToArray());
            var rows=p.GetProperty("rows").EnumerateArray().Select(Candidate).ToList();
            var units=p.GetProperty("units").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>
            {
                var r=x.Value;int kind=r[0].GetInt32(),rx=r[2].GetInt32(),ry=r[3].GetInt32();bool stat=kind is 2 or 4 or 5;
                return new GenericPetSearchUnit(new(kind,r[1].GetInt32()!=0,stat?rx:rx>>16,stat?ry:ry>>16),r[4].GetInt32(),r[5].GetUInt32(),r[8].GetInt32(),r[9].GetByte(),r[6].GetInt32()!=0,r[7].GetInt32()!=0,rx,ry);
            });
            var paths=p.GetProperty("pathTargets").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>x.Value.GetInt32());
            var extra=p.GetProperty("flags128").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>x.Value.GetUInt32());
            var trace=new List<object[]>();void A(params object[] values)=>trace.Add(values);int collisionCalls=0;
            int Get(JsonElement map,int id)=>map.TryGetProperty(id.ToString(),out var x)?x.GetInt32():0;
            var refs=p.GetProperty("pathRefs");
            var live=units.ToDictionary(x=>x.Key,x=>
            {
                int[] key=refs.TryGetProperty(x.Key.ToString(),out var r)?Ints(r):[0,0];var u=x.Value;
                return new GenericPetTraversalUnit(u.Position.Kind,u.Mode,Get(p.GetProperty("unitIds"),x.Key),u.Flags124,extra.GetValueOrDefault(x.Key),
                    u.Position.HasPath?new(paths.GetValueOrDefault(x.Key),key[0],key[1]):null,u.HasMonsterAi,null);
            });
            var next=p.GetProperty("next160").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>x.Value.GetInt32());
            int RoomId(int id)=>Get(p.GetProperty("unitRoom"),id);
            var graph=p.GetProperty("graphRooms");
            var worldUnits=units.ToDictionary(x=>x.Key,x=>
            {
                var u=x.Value;var r=p.GetProperty("world").GetProperty(x.Key.ToString());var stats=r.GetProperty("stats");var own=r.GetProperty("owner");int[] cache=Ints(r.GetProperty("cache")),missile=Ints(r.GetProperty("missile"));
                return new GenericPetSearchWorldUnit(u.Position,u.ClassId,u.Mode,u.Selector445,r.GetProperty("room").GetInt32(),r.GetProperty("flags128").GetUInt32(),u.HasMonsterAi,r.GetProperty("monsterFlags26").GetByte(),
                    own.ValueKind==JsonValueKind.Null?null:new(own[0].GetInt32(),own[1].GetInt32(),own[2].GetInt32(),0),missile[0],missile[1],
                    stats.ValueKind==JsonValueKind.Null?null:new(stats[0].GetInt32(),stats[1].EnumerateArray().Select(x=>x.GetUInt32()).ToArray()),new(cache[0],cache[1],cache[2],cache[3]));
            });
            var registry=new GenericPetRegistryServices((game,type,id)=>
                {Require(game==1,"Wrong registry game");int first=p.GetProperty("bucketHeads").TryGetProperty(type+":"+id,out var r)?r.GetInt32():0;A(21,game,type,id,first);return first;},
                game=>{Require(game==1,"Wrong tile game");return p.GetProperty("tileFirst").GetInt32();},
                id=>live[id].Id,id=>Get(p.GetProperty("registryNext"),id));
            GenericPetSearchWorldServices worldServices=null;
            var traversal=new GenericPetTraversalServices(id=>live[id],(game,type,id)=>GenericPetRegistry.Resolve(game,type,id,registry),
                id=>GenericPetSearchWorld.Room(id,worldServices),
                id=>{var r=graph.GetProperty(id.ToString());return new(r[2].GetInt32(),r[3].GetInt32(),Ints(r[4]),[]);},
                id=>next.GetValueOrDefault(id),_=>throw new InvalidOperationException("Index1 read proxy"),_=>throw new InvalidOperationException("Index1 read owner node"),
                index=>{Require(index==1,"Wrong search descriptor");return new(0,null);});
            var callback=new GenericPetSearchServices(id=>units[id],
                (pet,target,flag)=>{if(p.GetProperty("change160").TryGetProperty(target.ToString(),out var changed)){next[target]=changed.GetInt32();}int value=p.GetProperty("valid").GetInt32();A(0,pet,target,flag,value);return value;},
                (unit,x,y)=>{int value=GenericPetSearch.Squared(units[unit].Position,x,y);A(1,unit,x,y,value);return value;},
                unit=>{int room=RoomId(unit);int[] value=room==0?null:graph.GetProperty(room.ToString()).EnumerateArray().Take(3).Select(x=>x.GetInt32()).ToArray();A(2,unit,value);return value==null?null:new GenericPetSearchRoom(value[0],(uint)value[1],value[2]);},
                (pet,target,mask)=>{var list=p.GetProperty("collisions");int value=list[Math.Min(collisionCalls++,list.GetArrayLength()-1)].GetInt32();A(3,pet,target,mask,value);return value;},
                (selector,level)=>{var raw=p.GetProperty("level");byte? value=raw.ValueKind==JsonValueKind.Null?null:raw.GetByte();A(4,selector,level,value);return value;},
                (pet,target)=>{int value=p.GetProperty("special").GetInt32();A(5,pet,target,value);return value;},
                unit=>{int value=p.GetProperty("corpse").GetInt32();A(6,unit,value);return value;},
                unit=>{int value=p.GetProperty("values").TryGetProperty(unit.ToString(),out var raw)?raw.GetInt32():1;A(7,unit,value);return value;},
                (selector,cls)=>{byte? value=p.GetProperty("monsters").TryGetProperty(cls.ToString(),out var raw)&&raw.ValueKind!=JsonValueKind.Null?raw.GetByte():null;A(9,selector,cls,value);return value;},
                (unit,state)=>{int value=p.GetProperty("has").TryGetProperty(unit.ToString(),out var raw)&&Ints(raw).Contains(state)?1:0;A(8,unit,state,value);return value!=0;});
            int pointCalls=0;
            GenericPetSearchTableView View(byte selector)=>GenericPetSearchTables.Bind(selector);
            worldServices=new(id=>worldUnits[id],
                (selector,id)=>{A(40,selector,id);return View(selector).Monster(selector,id);},
                (selector,id)=>View(selector).Level(selector,id),
                selector=>{if(selector is <1 or >3){throw new NotSupportedException("Unbound state table");}return p.GetProperty("stateCount").GetInt32();},
                (game,type,id)=>GenericPetRegistry.Resolve(game,type,id,registry),
                (source,target)=>{var raw=p.GetProperty("relation");uint? value=raw.ValueKind==JsonValueKind.Null?null:raw.GetUInt32();A(47,source,target,value);return value;},
                (source,target)=>{int value=p.GetProperty("allegiance").GetInt32();A(48,source,target,value);return value;},
                (selector,id)=>{var raw=p.GetProperty("monster2");int[] value=raw.ValueKind==JsonValueKind.Null?null:Ints(raw);A(45,selector,id,value);return value==null?null:new((byte)value[0],(byte)value[1]);},
                (selector,id)=>{int value=p.GetProperty("corpseMask").GetInt32();A(46,selector,id,value);return value;},
                id=>{int value=p.GetProperty("world").GetProperty(id.ToString()).GetProperty("sizes")[0].GetInt32();A(51,id,value);return value;},
                id=>{int value=p.GetProperty("world").GetProperty(id.ToString()).GetProperty("sizes")[1].GetInt32();A(52,id,value);return value;},
                (x,y,size,tx,ty,targetSize,room,mask)=>{int value=p.GetProperty("ray").GetInt32();A(53,x,y,size,tx,ty,targetSize,room,mask,value);return value;},
                (source,target)=>{int value=p.GetProperty("specialIndex").GetInt32();A(49,source,target,value);return value;},
                (source,x,y,mask)=>{var list=p.GetProperty("points");int value=list[Math.Min(pointCalls++,list.GetArrayLength()-1)].GetInt32();A(50,source,x,y,mask,value);return value;});
            callback=GenericPetSearchWorld.Bind(callback,1,worldServices,id=>
            {var r=graph.GetProperty(id.ToString());return new(r[0].GetInt32(),r[1].GetUInt32(),r[2].GetInt32());});
            // Deliberately return the original cached path snapshot here.
            // Bind must read the same live path object Refresh just mutated.
            var original=new GenericPetSearchWrapperServices(
                id=>new(units[id],paths.GetValueOrDefault(id),extra.GetValueOrDefault(id)),
                _=>throw new InvalidOperationException("Unbound refresh"),GenericPetMetadata.Table.Bind(input.GameSelector).SearchServices(),callback,
                (_,_,_,_)=>throw new InvalidOperationException("Unbound traversal"));
            var services=GenericPetTraversal.Bind(original,1,traversal);
            return new(input,rows,services,worldServices,worldUnits,live,trace);
    }
    internal static object[] Caches(Scene scene)=>scene.Units.Values.Select(u=>new[]{u.Cache.Kind,u.Cache.ClassId,u.Cache.SizeX,u.Cache.SizeY}).Cast<object>().ToArray();
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-search-services-source-probe.json")));
        int count=0,fatal=0,pipeline=0,pipelineFatal=0;
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var scene=Create(entry[0]);int failed=0;object result=0;string name=entry[1].GetString();int[] args=entry[2].EnumerateArray().Select(x=>x[1].GetInt32()).ToArray();
            try
            {
                result=name switch
                {
                    "room"=>GenericPetSearchWorld.Room(args[0],scene.World),
                    "monster"=>scene.World.Monster((byte)args[0],args[1]) is{} monster?new[]{monster.Id,(int)monster.Flags60,monster.Threat137}:(object)(-1),
                    "level"=>scene.World.Level((byte)args[0],args[1]) is{} level?new[]{level.Id,(int)level.Walk63}:(object)(-1),
                    "has"=>GenericPetSearchWorld.HasState(args[0],args[1],scene.World),
                    "value"=>GenericPetSearchWorld.Priority(args[0],scene.World),
                    "corpse"=>GenericPetSearchWorld.Corpse(args[0],scene.World),
                    "valid"=>GenericPetSearchWorld.Valid(args[0],args[1],args[2],(byte)args[3],scene.World),
                    "collision"=>GenericPetSearchWorld.Collision(args[0],args[1],args[2],scene.World),
                    "special"=>GenericPetSearchWorld.Special(args[0],args[1],scene.World),
                    _=>throw new InvalidOperationException("Unknown helper")
                };
            }
            catch(NotSupportedException){failed=1;fatal++;scene.Trace.Add([99]);}
            string actual=JsonSerializer.Serialize(new object[]{failed,result,Caches(scene),scene.Trace});
            Require(actual==entry[3].GetRawText(),"Search helper differs:"+count+":"+name+":"+actual[..Math.Min(actual.Length,1400)]);count++;
        }
        foreach(var entry in doc.RootElement.GetProperty("pipeline").EnumerateArray())
        {
            var p=entry[0];var scene=Create(p);int failed=0;var result=new GenericPetCombatSelection(new int[5],0,0);
            try
            {
                int mask=p.GetProperty("mask").GetInt32(),target=p.GetProperty("explicit").GetInt32();
                result=pipeline%2==0?GenericPetSearchWrapper.Run(scene.Input,scene.Rows,mask,target,scene.Wrapper):GenericPetSearchWrapper.Provider(scene.Input,scene.Wrapper)(scene.Rows,mask,target);
            }
            catch(NotSupportedException){failed=1;pipelineFatal++;scene.Trace.Add([99]);}
            int[] paths=scene.Traversal.Values.Select(u=>u.Path?.Target??0).ToArray();
            string actual=JsonSerializer.Serialize(new object[]{failed,result.Targets,new[]{result.Word40,result.Word48},scene.Rows.Select(Row).ToArray(),paths,Caches(scene),scene.Trace});
            Require(actual==entry[1].GetRawText(),"Search helper pipeline differs:"+pipeline+":"+actual[..Math.Min(actual.Length,1600)]);pipeline++;
        }
        Require(count==9392&&fatal==848&&pipeline==216&&pipelineFatal==0,"Incomplete source helper matrix:"+count+"/"+fatal+"/"+pipeline+"/"+pipelineFatal);
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Search helpers enabled incomplete demons");
        Log.Information("GenericPet search services:9392current helper contexts,848explicit source boundaries and216full search compositions;actual room,owned tables,state bitmasks,priority,corpse,ancestry,collision cache and special scans pass;world/nested predicates/actor remain gated");
    }
}
