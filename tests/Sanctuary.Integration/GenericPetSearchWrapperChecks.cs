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
public static class GenericPetSearchWrapperChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetSearchWrapperChecks));
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
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Current complete pet search wrapper and grid heap",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-search-wrapper-check");Register("generic-pet-search-wrapper-reload-check");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-search-wrapper-source-probe.json")));int count=0,fatal=0,heapCount=0;
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateArray())
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
            var trace=new List<object[]>();void A(params object[] values)=>trace.Add(values);int roomCalls=0,collisionCalls=0;
            var callback=new GenericPetSearchServices(id=>units[id],
                (pet,target,flag)=>{int value=p.GetProperty("valid").GetInt32();A(0,pet,target,flag,value);return value;},
                (unit,x,y)=>{int value=GenericPetSearch.Squared(units[unit].Position,x,y);A(1,unit,x,y,value);return value;},
                unit=>{var list=p.GetProperty("rooms");var value=list[Math.Min(roomCalls++,list.GetArrayLength()-1)];A(2,unit,value.ValueKind==JsonValueKind.Null?null:Ints(value));return value.ValueKind==JsonValueKind.Null?null:new GenericPetSearchRoom(value[0].GetInt32(),value[1].GetUInt32(),value[2].GetInt32());},
                (pet,target,mask)=>{var list=p.GetProperty("collisions");int value=list[Math.Min(collisionCalls++,list.GetArrayLength()-1)].GetInt32();A(3,pet,target,mask,value);return value;},
                (selector,level)=>{var raw=p.GetProperty("level");byte? value=raw.ValueKind==JsonValueKind.Null?null:raw.GetByte();A(4,selector,level,value);return value;},
                (pet,target)=>{int value=p.GetProperty("special").GetInt32();A(5,pet,target,value);return value;},
                unit=>{int value=p.GetProperty("corpse").GetInt32();A(6,unit,value);return value;},
                unit=>{int value=p.GetProperty("values").TryGetProperty(unit.ToString(),out var raw)?raw.GetInt32():1;A(7,unit,value);return value;},
                (selector,cls)=>{byte? value=p.GetProperty("monsters").TryGetProperty(cls.ToString(),out var raw)&&raw.ValueKind!=JsonValueKind.Null?raw.GetByte():null;A(9,selector,cls,value);return value;},
                (unit,state)=>{int value=p.GetProperty("has").TryGetProperty(unit.ToString(),out var raw)&&Ints(raw).Contains(state)?1:0;A(8,unit,state,value);return value!=0;});
            var services=new GenericPetSearchWrapperServices(
                id=>new(units[id],paths.GetValueOrDefault(id),extra.GetValueOrDefault(id)),
                owner=>{A(10,owner);var value=p.GetProperty("refreshPathTarget");if(value.ValueKind!=JsonValueKind.Null){paths[owner]=value.GetInt32();}},
                GenericPetMetadata.Table.Bind(input.GameSelector).SearchServices(),callback,
                (pet,context,flag,visit)=>{A(11,pet,flag);Require(context.Owner==input.Owner&&context.Mask==p.GetProperty("mask").GetInt32()&&flag==1,"Wrong traversal context");foreach(int target in Ints(p.GetProperty("visits"))){Require(visit(target)==0,"Source callback returned nonzero");}});
            int failed=0;var result=new GenericPetCombatSelection(new int[5],0,0);
            try
            {
                int mask=p.GetProperty("mask").GetInt32(),target=p.GetProperty("explicit").GetInt32();
                result=count%2==0?GenericPetSearchWrapper.Run(input,rows,mask,target,services):GenericPetSearchWrapper.Provider(input,services)(rows,mask,target);
            }
            catch(NotSupportedException){failed=1;fatal++;A(99,5);}
            string actual=JsonSerializer.Serialize(new object[]{failed,result.Targets,new[]{result.Word40,result.Word48},rows.Select(Row).ToArray(),trace});
            Require(actual==entry[1].GetRawText(),"Current search wrapper differs:"+count+":"+actual[..Math.Min(actual.Length,1100)]);
            if(count==0)
            {
                int before=trace.Count;bool rejected=false;try{GenericPetSearchWrapper.Provider(input,services)(new ReadOnlySlots(),0,0);}catch(NotSupportedException){rejected=true;}
                Require(rejected&&trace.Count==before,"Search provider copied an immutable source view");
            }
            count++;
        }
        foreach(var entry in doc.RootElement.GetProperty("heaps").EnumerateArray())
        {
            var cells=entry[0].EnumerateArray().Select(r=>new GenericPetSearchCell{Count=r[0].GetInt32(),Distance=r[1].GetInt32(),Target=r[2].GetInt32()}).ToArray();var indices=Ints(entry[1]).ToList();
            GenericPetSearchGrid.Heapify(indices,cells);Require(indices.SequenceEqual(Ints(entry[2])),"Grid heap permutation differs:"+heapCount);heapCount++;
        }
        Require(count==2026&&fatal==432&&heapCount==2637,"Incomplete search wrapper matrix:"+count+"/"+fatal+"/"+heapCount);
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Search wrapper enabled incomplete demons");
        Log.Information("GenericPet search wrapper:2026current-code contexts,432tile-owner boundaries,2637exact heap permutations;real callback/prep/cleanup/grid,owner refresh,traversal,payload and borrowed slots pass;world helpers/actor remain gated");
    }
}
