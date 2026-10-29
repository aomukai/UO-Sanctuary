// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetGatherChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetGatherChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static GenericPetCombatCandidate Candidate(JsonElement r)=>new(r[0].GetInt32(),r[1].GetInt64(),r[2].GetInt32(),r[3].GetInt32(),r[4].GetInt32(),r[5].GetInt32(),r[6].GetInt32(),r[7].GetByte(),r[8].GetByte(),r[9].GetByte(),r[10].GetByte());
    private static object[] Row(GenericPetCombatCandidate r)=>[r.Target,r.Opaque8,r.Rating,r.SkillId,r.Opaque24,r.Opaque28,r.Counter,r.Byte36,r.BaseModeFlag,r.Byte38,r.Byte39];
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Current GenericPet gather and search mask",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-gather-check");Register("generic-pet-gather-reload-check");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-gather-source-probe.json")));
        int count=0,fatal=0;var kinds=new Dictionary<string,int>();
        foreach(var entry in doc.RootElement.EnumerateArray())
        {
            string kind=entry[0].GetString();var p=entry[1];int I(string name)=>p.GetProperty(name).GetInt32();
            var skills=entry[2].ValueKind==JsonValueKind.Null?null:entry[2].EnumerateArray().Select((e,i)=>new GenericPetGatherEntry(i,e[0].GetInt16(),e[1].GetInt32(),e[2].GetInt32(),e[3].GetInt32())).ToArray();
            var rows=entry[3].EnumerateArray().Select(Candidate).ToList();var state=new GenericPetGatherState(I("aux"));
            var context=new GenericPetGatherContext(I("pet")!=0,742,checked((byte)I("selector")),skills);
            var parameters=GenericPetParameters.Decode(null) with{GatherType=I("gather"),AllowSkillBonuses=I("allow")!=0,ModeAttack=I("mode"),ModeAiType=I("type"),Flags=checked((ushort)I("flags"))};
            var trace=new List<object[]>();void A(params object[] values)=>trace.Add(values);
            var services=new GenericPetGatherServices(
                (skill,allow)=>{int rank=entry[2][skill.Token][4].GetInt32();A(2,skill.Token,allow?1:0,rank);return rank;},
                (selector,id)=>{bool found=p.GetProperty("info").TryGetProperty(id.ToString(),out var info)&&info.ValueKind!=JsonValueKind.Null;A(3,selector,id,found?info:null);return found?new(info[0].GetByte(),info[1].GetByte(),info[2].GetByte()):null;},
                id=>{int value=p.GetProperty("bonus").TryGetProperty(id.ToString(),out var v)?v.GetInt32():0;A(4,id,value);return value;},
                (selector,unit,slot)=>{A(1,selector,unit,slot);return p.GetProperty("monster")[slot].GetInt16();},
                selector=>{A(5,selector,I("count"));return I("count");},
                (isBase,token)=>p.GetProperty("tail")[isBase?0:1].GetUInt16());
            int failed=0,mask=-1;
            try
            {
                if(kind!="mask"){GenericPetGather.Build(context,parameters,state,services,rows);}
                if(kind!="gather"){mask=GenericPetGather.SearchMask(context,parameters.Flags,rows,services);}
            }
            catch(NotSupportedException) when(kind!="mask"&&((I("pet")==0&&I("gather") is 1 or 2)||(I("gather")==0&&context.Selector is <1 or >3)))
            {A(99);failed=1;fatal++;}
            var expected=entry[4];
            // Append is a declared fixed-capacity copying service. Compare every
            // resulting descriptor independently; remaining service order is exact.
            var expectedTrace=expected[4].EnumerateArray().Where(x=>x[0].GetInt32()!=0).ToArray();
            string actual=JsonSerializer.Serialize(new object[]{failed,rows.Select(Row).ToArray(),state.Value108,mask,trace});
            string wanted=JsonSerializer.Serialize(new object[]{expected[0],expected[1],expected[2],expected[3],expectedTrace});
            Require(actual==wanted,"Captured current gather differs at "+count+" "+kind+":"+actual[..Math.Min(actual.Length,240)]);count++;kinds[kind]=kinds.GetValueOrDefault(kind)+1;
        }
        Require(count==23348&&fatal==1476&&kinds["composed"]==4896&&kinds["mask"]==18452,"Current gather matrix incomplete");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Prepared gather enabled incomplete demons");
        Log.Information("GenericPet gather:23348captured-current cases,4896three-helper compositions,1476fatal boundaries;exact descriptors,state108,service order and search masks pass;real metadata and actor AI remain gated");
    }
}
