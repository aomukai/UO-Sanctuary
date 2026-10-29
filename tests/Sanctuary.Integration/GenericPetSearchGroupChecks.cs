// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetSearchGroupChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetSearchGroupChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static GenericPetCombatCandidate Candidate(JsonElement r)=>new(r[0].GetInt32(),r[1].GetInt64(),r[2].GetInt32(),r[3].GetInt32(),r[4].GetInt32(),r[5].GetInt32(),r[6].GetInt32(),r[7].GetByte(),r[8].GetByte(),r[9].GetByte(),r[10].GetByte());
    private static object[] Row(GenericPetCombatCandidate r)=>[r.Target,r.Opaque8,r.Rating,r.SkillId,r.Opaque24,r.Opaque28,r.Counter,r.Byte36,r.BaseModeFlag,r.Byte38,r.Byte39];
    private static object[] Group(GenericPetStateGroup g)=>[g.Slot,g.StateGroup,g.States.ToArray(),g.Flag4?1:0,g.Flag2?1:0];
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Current search-state preparation before callback/cleanup",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-search-groups-check");Register("generic-pet-search-groups-reload-check");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-search-groups-source-probe.json")));
        int count=0,fatal=0,linked=0;var prepared=new GenericPetSearchStates();
        foreach(var entry in doc.RootElement.EnumerateArray())
        {
            var p=entry[0];byte selector=p.GetProperty("selector").GetByte();var states=p.GetProperty("states");var rows=entry[1].EnumerateArray().Select(Candidate).ToList();
            // Prior detached groups must be discarded even if the next call
            // has no qualifying rows or reaches a source assertion boundary.
            prepared.Groups.Clear();
            foreach(var old in entry[2].EnumerateArray())
            {var g=new GenericPetStateGroup(0,6){StateGroup=99};g.States.AddRange(old[0].EnumerateArray().Select(v=>v.GetInt32()));prepared.Groups.Add(g);}
            bool firstLookup=true;var trace=new List<object[]>();void A(params object[] values)=>trace.Add(values);
            var services=new GenericPetSearchStateServices(
                (table,id)=>
                {
                    if(firstLookup){Require(prepared.Groups.Count==0,"Previous search groups survived until first lookup");firstLookup=false;}
                    bool found=p.GetProperty("skills").TryGetProperty(id.ToString(),out var info)&&info.ValueKind!=JsonValueKind.Null;
                    short? word=found?info[0].GetInt16():null;A(1,table,id,word);return word;
                },
                (table,wide)=>{int n=states.GetArrayLength();A(2,table,wide?1:0,n);return n;},
                (table,id)=>{short? group=states[id].ValueKind==JsonValueKind.Null?null:states[id].GetInt16();A(3,table,id,group);return group;},
                (table,id)=>{short group=states[id].ValueKind==JsonValueKind.Null?(short)0:states[id].GetInt16();A(5,table,id,group);return group;});
            int failed=0;
            try{prepared.Prepare(selector,rows,services);}
            catch(NotSupportedException) when(selector is <1 or >3){failed=1;fatal++;A(99);}
            string actual=JsonSerializer.Serialize(new object[]{failed,rows.Select(Row).ToArray(),prepared.Groups.Select(Group).ToArray(),trace});
            Require(actual==entry[3].GetRawText(),"Current search groups differ at "+count+":"+actual[..Math.Min(actual.Length,400)]);
            // Each compared slot is decoded from the actual source pointer,
            // including duplicate skill IDs in different candidate slots.
            linked+=prepared.Groups.Count;
            count++;
        }
        Require(count==819&&fatal==15&&linked>0,"Incomplete search-state source matrix:"+count+"/"+fatal+"/"+linked);
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Preparation enabled incomplete demons");
        Log.Information("GenericPet search groups:{Count}captured-current contexts,{Fatal}fatal boundaries;A2,state expansion,prior clearing,live candidate slots and metadata order pass;post-cleanup callback/world actor remain gated",count,fatal);
    }
}
