// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetPreparationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetPreparationChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static GenericPetCombatCandidate Candidate(JsonElement r)=>new(r[0].GetInt32(),r[1].GetInt64(),r[2].GetInt32(),r[3].GetInt32(),r[4].GetInt32(),r[5].GetInt32(),r[6].GetInt32(),r[7].GetByte(),r[8].GetByte(),r[9].GetByte(),r[10].GetByte());
    private static object[] Row(GenericPetCombatCandidate r)=>[r.Target,r.Opaque8,r.Rating,r.SkillId,r.Opaque24,r.Opaque28,r.Counter,r.Byte36,r.BaseModeFlag,r.Byte38,r.Byte39];
    private static int[] Values(JsonElement value)=>value.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Current GenericPet buff recipient preparation",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-preparation-check");Register("generic-pet-preparation-reload-check");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-preparation-source-probe.json")));int count=0,fatal=0;
        foreach(var entry in doc.RootElement.EnumerateArray())
        {
            var p=entry[0];byte selector=p.GetProperty("selector").GetByte();var states=p.GetProperty("states");var rows=entry[1].EnumerateArray().Select(Candidate).ToList();
            var trace=new List<object[]>();void A(params object[] values)=>trace.Add(values);
            var services=new GenericPetPreparationServices(
                (table,id)=>{bool found=p.GetProperty("skills").TryGetProperty(id.ToString(),out var info)&&info.ValueKind!=JsonValueKind.Null;A(1,table,id,found?info:null);return found?new(info[0].GetInt16(),info[1].GetByte()):null;},
                (table,wide)=>{int n=states.GetArrayLength();A(2,table,wide?1:0,n);return n;},
                (table,id)=>{short? value=states[id].ValueKind==JsonValueKind.Null?null:states[id].GetInt16();A(3,table,id,value);return value;},
                (table,id)=>{short value=states[id].ValueKind==JsonValueKind.Null?(short)0:states[id].GetInt16();A(5,table,id,value);return value;},
                (unit,state)=>{int value=p.GetProperty("has").TryGetProperty(unit.ToString(),out var list)&&Values(list).Contains(state)?1:0;A(4,unit,state,value);return value!=0;},
                unit=>{int[] list=Values(p.GetProperty("visits"));A(6,unit,list);return list;},
                unit=>{int[] list=p.GetProperty("nested").TryGetProperty(unit.ToString(),out var v)?Values(v):[];A(7,unit,list);return list;},
                unit=>{var v=p.GetProperty("units").GetProperty(unit.ToString());return new(v[0].GetInt32(),v[1].GetInt32()!=0,v[2].GetInt32(),v[3].GetInt32());},
                (unit,x,y)=>{var units=p.GetProperty("units");int candidate=-1;foreach(var v in units.EnumerateObject()){var values=Values(v.Value);var point=new GenericPetDecisionUnit(values[0],values[1]!=0,values[2],values[3]).Coordinates();if(point==(x,y)&&v.Name!="1"){candidate=int.Parse(v.Name);break;}}Require(candidate>=0,"Unknown distance fixture coordinate");int result=p.GetProperty("distance").TryGetProperty(candidate.ToString(),out var d)?d.GetInt32():25;A(8,unit,x,y,result);return result;});
            int failed=0;
            try{GenericPetPreparation.Apply(new(selector,1,p.GetProperty("owner").GetInt32()),rows,services);}
            catch(NotSupportedException) when(selector is <1 or >3){failed=1;fatal++;A(99);}
            string actual=JsonSerializer.Serialize(new object[]{failed,rows.Select(Row).ToArray(),trace});
            Require(actual==entry[2].GetRawText(),"Captured current preparation differs at "+count+":"+actual[..Math.Min(actual.Length,250)]);count++;
        }
        Require(count==4682&&fatal==200,"Current preparation matrix incomplete: "+count+"/"+fatal);
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Prepared buff targeting enabled incomplete demons");
        Log.Information("GenericPet preparation:4682captured-current contexts,200fatal boundaries;exact descriptor targets,state groups,recipient callbacks and service order pass;real world iterators/state/geometry remain gated");
    }
}
