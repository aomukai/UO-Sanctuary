// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetSearchBoundChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetSearchBoundChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int[] Ints(JsonElement v)=>v.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static GenericPetCombatCandidate Candidate(JsonElement r)=>new(r[0].GetInt32(),r[1].GetInt64(),r[2].GetInt32(),r[3].GetInt32(),r[4].GetInt32(),r[5].GetInt32(),r[6].GetInt32(),r[7].GetByte(),r[8].GetByte(),r[9].GetByte(),r[10].GetByte());
    private static object[] Row(GenericPetCombatCandidate r)=>[r.Target,r.Opaque8,r.Rating,r.SkillId,r.Opaque24,r.Opaque28,r.Counter,r.Byte36,r.BaseModeFlag,r.Byte38,r.Byte39];
    private sealed class ReadOnlySlots : IReadOnlyList<GenericPetCombatCandidate>
    {
        public int Count=>0;
        public GenericPetCombatCandidate this[int index]=>throw new IndexOutOfRangeException();
        public IEnumerator<GenericPetCombatCandidate> GetEnumerator()=>Enumerable.Empty<GenericPetCombatCandidate>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Current search with concrete refresh, registry and traversal",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-search-bound-check");Register("generic-pet-search-bound-reload-check");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-search-bound-source-probe.json")));int count=0,fatal=0;
        foreach(var entry in doc.RootElement.EnumerateArray())
        {
            var p=entry[0];int[] v=Ints(p.GetProperty("params"));
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
            var registry=new GenericPetRegistryServices((game,type,id)=>
                {Require(game==1,"Wrong registry game");int first=p.GetProperty("bucketHeads").TryGetProperty(type+":"+id,out var r)?r.GetInt32():0;A(21,game,type,id,first);return first;},
                game=>{Require(game==1,"Wrong tile game");return p.GetProperty("tileFirst").GetInt32();},
                id=>live[id].Id,id=>Get(p.GetProperty("registryNext"),id));
            var traversal=new GenericPetTraversalServices(id=>live[id],(game,type,id)=>GenericPetRegistry.Resolve(game,type,id,registry),
                id=>{int room=RoomId(id);A(20,id,room);return room;},
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
            // Deliberately return the original cached path snapshot here.
            // Bind must read the same live path object Refresh just mutated.
            var original=new GenericPetSearchWrapperServices(
                id=>new(units[id],paths.GetValueOrDefault(id),extra.GetValueOrDefault(id)),
                _=>throw new InvalidOperationException("Unbound refresh"),GenericPetMetadata.Table.Bind(input.GameSelector).SearchServices(),callback,
                (_,_,_,_)=>throw new InvalidOperationException("Unbound traversal"));
            var services=GenericPetTraversal.Bind(original,1,traversal);
            int failed=0;var result=new GenericPetCombatSelection(new int[5],0,0);
            try
            {
                int mask=p.GetProperty("mask").GetInt32(),target=p.GetProperty("explicit").GetInt32();
                result=count%2==0?GenericPetSearchWrapper.Run(input,rows,mask,target,services):GenericPetSearchWrapper.Provider(input,services)(rows,mask,target);
            }
            catch(NotSupportedException){failed=1;fatal++;A(99);}
            string actual=JsonSerializer.Serialize(new object[]{failed,result.Targets,new[]{result.Word40,result.Word48},rows.Select(Row).ToArray(),units.Keys.Select(id=>live[id].Path?.Target??paths.GetValueOrDefault(id)).ToArray(),trace});
            Require(actual==entry[1].GetRawText(),"Current search wrapper differs:"+count+":"+actual[..Math.Min(actual.Length,1100)]);
            if(count==0)
            {
                int before=trace.Count;bool rejected=false;try{GenericPetSearchWrapper.Provider(input,services)(new ReadOnlySlots(),0,0);}catch(NotSupportedException){rejected=true;}
                Require(rejected&&trace.Count==before,"Search provider copied an immutable source view");
                var incoherent=GenericPetTraversal.Bind(original,1,traversal with{Unit=id=>live[id] with{Mode=live[id].Mode+1}});
                rejected=false;try{incoherent.Unit(1);}catch(NotSupportedException){rejected=true;}
                Require(rejected,"Binder accepted conflicting unit metadata");
            }
            count++;
        }
        Require(count==1484&&fatal==66,"Incomplete bound search matrix:"+count+"/"+fatal);
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Search wrapper enabled incomplete demons");
        Log.Information("GenericPet bound search:1484current-code contexts,66explicit source boundaries;full wrapper/callback/prep/grid with actual refresh/registry/traversal,shared mutable path caches and live links pass;real world helpers/graph/actor remain gated");
    }
}
