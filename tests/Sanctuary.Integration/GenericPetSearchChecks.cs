// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetSearchChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetSearchChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int[] Ints(JsonElement v)=>v.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static GenericPetCombatCandidate Candidate(JsonElement r)=>new(r[0].GetInt32(),r[1].GetInt64(),r[2].GetInt32(),r[3].GetInt32(),r[4].GetInt32(),r[5].GetInt32(),r[6].GetInt32(),r[7].GetByte(),r[8].GetByte(),r[9].GetByte(),r[10].GetByte());
    private static object[] Row(GenericPetCombatCandidate r)=>[r.Target,r.Opaque8,r.Rating,r.SkillId,r.Opaque24,r.Opaque28,r.Counter,r.Byte36,r.BaseModeFlag,r.Byte38,r.Byte39];
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Current full pet search callback and inline caller storage",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-search-check");Register("generic-pet-search-reload-check");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-search-callback-source-probe.json")));int count=0,fatal=0,composed=0;
        foreach(var entry in doc.RootElement.EnumerateArray())
        {
            var p=entry[0];int[] parameters=Ints(p.GetProperty("params")),targets=Ints(p.GetProperty("targets")),counts=Ints(p.GetProperty("counts"));
            var c=new GenericPetSearchContext{GameSelector=p.GetProperty("selector").GetByte(),Mask=p.GetProperty("mask").GetInt32(),Owner=p.GetProperty("owner").GetInt32(),Bias=p.GetProperty("bias").GetInt32(),
                Parameters=p.GetProperty("config").GetBoolean()?new(0,0,0,0,parameters[0],(ushort)parameters[1],0,false,0,0,0,0,parameters[2],parameters[3],0,parameters[4],parameters[5],0,0,0):null,
                Target20=targets[0],Target28=targets[1],Target30=targets[2],Target38=targets[3],Target40=targets[4],Count58=counts[0],Count5c=counts[1],Count60=counts[2],Count64=counts[3],
                Swarm=p.GetProperty("swarm").EnumerateArray().Select(r=>new GenericPetSearchSwarm(r[0].GetInt32(),r[1].GetInt32())).ToArray()};
            var grid=p.GetProperty("grid");c.Grid=grid.ValueKind==JsonValueKind.Null?null:grid.EnumerateArray().Select(r=>new GenericPetSearchCell{Count=r[0].GetInt32(),Distance=r[1].GetInt32(),Target=r[2].GetInt32()}).ToArray();
            var rows=p.GetProperty("rows").EnumerateArray().Select(Candidate).ToList();
            if(p.TryGetProperty("prepare",out _))
            {
                var prepared=new GenericPetSearchStates();prepared.Prepare(c.GameSelector,rows,GenericPetMetadata.Table.Bind(c.GameSelector).SearchServices());c.Groups=GenericPetSearch.InlineGroups(prepared);composed++;
            }
            else
            {
                var groups=p.GetProperty("groups");c.Groups=groups.ValueKind==JsonValueKind.Null?null:groups.EnumerateArray().Select(g=>{var value=new GenericPetStateGroup(g[0].GetInt32(),0);value.States.AddRange(Ints(g[1]));return value;}).ToArray();
            }
            var units=p.GetProperty("units").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>
            {
                var v=x.Value;int kind=v[0].GetInt32(),rawX=v[2].GetInt32(),rawY=v[3].GetInt32();bool stat=kind is 2 or 4 or 5;
                return new GenericPetSearchUnit(new(kind,v[1].GetInt32()!=0,stat?rawX:rawX>>16,stat?rawY:rawY>>16),v[4].GetInt32(),v[5].GetUInt32(),v[8].GetInt32(),v[9].GetByte(),v[6].GetInt32()!=0,v[7].GetInt32()!=0,rawX,rawY);
            });
            var trace=new List<object[]>();void A(params object[] values)=>trace.Add(values);int roomCalls=0,collisionCalls=0;
            var services=new GenericPetSearchServices(id=>units[id],
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
            int failed=0;
            try{foreach(int target in Ints(p.GetProperty("visits"))){Require(GenericPetSearch.Visit(c,p.GetProperty("pet").GetInt32(),target,rows,services)==0,"Callback returned nonzero");}}
            catch(NotSupportedException){failed=1;fatal++;A(99);}
            string actual=JsonSerializer.Serialize(new object[]{failed,new[]{c.Target20,c.Target28,c.Target30,c.Target38,c.Target40},new[]{c.Count58,c.Count5c,c.Count60,c.Count64},c.Grid?.Select(cell=>new[]{cell.Count,cell.Distance,cell.Target}).ToArray(),rows.Select(Row).ToArray(),trace});
            Require(actual==entry[1].GetRawText(),"Current full search callback differs:"+count+":"+actual[..Math.Min(actual.Length,900)]);count++;
        }
        Require(count==5824&&fatal==540&&composed==36,"Incomplete full search matrix:"+count+"/"+fatal+"/"+composed);
        var tooMany=new GenericPetSearchStates();for(int i=0;i<11;i++){tooMany.Groups.Add(new(i,0));}bool rejected=false;
        try{GenericPetSearch.InlineGroups(tooMany);}catch(NotSupportedException){rejected=true;}Require(rejected,"Heap-backed caller group storage silently retained");
        tooMany.Groups.Clear();var large=new GenericPetStateGroup(0,0);large.States.AddRange(Enumerable.Range(0,11));tooMany.Groups.Add(large);rejected=false;
        try{GenericPetSearch.InlineGroups(tooMany);}catch(NotSupportedException){rejected=true;}Require(rejected,"Heap-backed caller state storage silently retained");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Search callback enabled incomplete demon actors");
        Log.Information("GenericPet search:5824captured-current callbacks,540explicit null-AI boundaries,36owned prep-cleanup compositions;targets,counters,grid,float32,swarm,state lists and exact service traces pass;world helper bindings/caller/actor remain gated");
    }
}
