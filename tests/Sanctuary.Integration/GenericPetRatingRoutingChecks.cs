// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetRatingRoutingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetRatingRoutingChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static GenericPetCombatCandidate Candidate(JsonElement r)=>new(r[0].GetInt32(),r[1].GetInt64(),r[2].GetInt32(),r[3].GetInt32(),r[4].GetInt32(),r[5].GetInt32(),r[6].GetInt32(),r[7].GetByte(),r[8].GetByte(),r[9].GetByte(),r[10].GetByte());
    private static object[] Row(GenericPetCombatCandidate r)=>[r.Target,r.Opaque8,r.Rating,r.SkillId,r.Opaque24,r.Opaque28,r.Counter,r.Byte36,r.BaseModeFlag,r.Byte38,r.Byte39];
    private static long Pair(int low,int high)=>unchecked((long)(((ulong)(uint)high<<32)|(uint)low));
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Current GenericPet target-category rating routing",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-rating-routing-check");Register("generic-pet-rating-routing-reload-check");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-rating-routing-source-probe.json")));int count=0,calls=0;
        foreach(var entry in doc.RootElement.EnumerateArray())
        {
            var p=entry[0];int I(string key)=>p.GetProperty(key).GetInt32();var words=p.GetProperty("words").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            var selection=new GenericPetCombatSelection(p.GetProperty("targets").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),Pair(words[0],words[1]),Pair(words[2],words[3]));
            var parameters=GenericPetParameters.Decode(null) with{MeleeBonus=I("melee"),RangedBonus=I("ranged")};var trace=new List<object[]>();int scored=0;
            var services=new GenericPetRatingRoutingServices(
                (selector,skill)=>{byte? flags=p.GetProperty("flags").ValueKind==JsonValueKind.Null?null:p.GetProperty("flags").GetByte();trace.Add([0,selector,skill,flags]);return flags;},
                (row,word40,word48)=>
                {
                    trace.Add([1,Row(row),unchecked((int)word40),unchecked((int)((ulong)word40>>32)),unchecked((int)word48),unchecked((int)((ulong)word48>>32))]);
                    int index=scored++;calls++;
                    // This supplied scorer deliberately changes payload fields;
                    // it tests routing copies/retries without inventing scores.
                    return row with{Target=I("retarget")==1?0:I("retarget")==2?9:row.Target,Opaque8=long.MinValue+index,Rating=p.GetProperty("ratings")[index].GetInt32(),Opaque24=unchecked(row.Opaque24+index+31),Opaque28=-index-91,Counter=unchecked(row.Counter+1),Byte38=(byte)(index+21),Byte39=(byte)(index+71)};
                });
            var result=GenericPetRatingRouting.Rate(p.GetProperty("selector").GetByte(),parameters,I("frame100"),selection,Candidate(entry[1]),services);
            string actual=JsonSerializer.Serialize(new object[]{Row(result),trace});Require(actual==entry[2].GetRawText(),"Captured current routing differs at "+count+":"+actual[..Math.Min(actual.Length,250)]);count++;
        }
        Require(count==14088,"Current rating routing matrix incomplete: "+count);
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Prepared routing enabled incomplete demons");
        Log.Information("GenericPet rating routing:14088captured-current contexts;exact target prepass,priority,retries,temporary copies and wrapped bonuses pass;per-target scoring remains a mandatory unimplemented service;calls={Calls}",calls);
    }
}
