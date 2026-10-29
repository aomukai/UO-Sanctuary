// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetTargetScoringChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetTargetScoringChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static GenericPetCombatCandidate Candidate(JsonElement r)=>new(r[0].GetInt32(),r[1].GetInt64(),r[2].GetInt32(),r[3].GetInt32(),r[4].GetInt32(),r[5].GetInt32(),r[6].GetInt32(),r[7].GetByte(),r[8].GetByte(),r[9].GetByte(),r[10].GetByte());
    private static object[] Row(GenericPetCombatCandidate r)=>[r.Target,r.Opaque8,r.Rating,r.SkillId,r.Opaque24,r.Opaque28,r.Counter,r.Byte36,r.BaseModeFlag,r.Byte38,r.Byte39];
    private static long Pair(int low,int high)=>unchecked((long)(((ulong)(uint)high<<32)|(uint)low));
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Current GenericPet per-target scoring and routing composition",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-target-scoring-check");Register("generic-pet-target-scoring-reload-check");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-target-scoring-source-probe.json")));int count=0,calls=0,fatal=0,unsupported=0,compositions=0;
        foreach(var entry in doc.RootElement.EnumerateArray())
        {
            string kind=entry[0].GetString();var p=entry[1];int I(string key)=>p.GetProperty(key).GetInt32();byte selector=p.GetProperty("selector").GetByte();
            var trace=new List<object[]>();void A(params object[] values)=>trace.Add(values);
            GenericPetScoreUnit Unit(int id)
            {
                var v=p.GetProperty("units").GetProperty(id.ToString());return new(new(v[0].GetInt32(),v[1].GetInt32()!=0,v[2].GetInt32(),v[3].GetInt32()),v[4].GetInt32(),v[5].GetInt32(),v[6].GetByte(),v[7].GetInt32()!=0,v[8].GetInt32()!=0,v[9].GetInt32()!=0,v[10].GetInt32()!=0,v[11].GetByte());
            }
            GenericPetScoreSkill Skill(byte table,int id)
            {
                var v=p.GetProperty("skill");bool found=v.ValueKind!=JsonValueKind.Null;A(0,table,id,found?v:null);
                return found?new(v[0].GetByte(),v[1].GetByte(),v[2].GetInt16(),v[3].GetInt16(),v[4].GetByte(),v[5].GetInt16(),v[6].GetInt16(),v[7].GetSByte(),v[8].GetInt32()):null;
            }
            var services=new GenericPetScoreServices(Skill,
                (unit,id)=>{A(1,unit,id,I("flag4"));return I("flag4");},Unit,
                (table,id)=>{A(2,table,id,I("cost"));return I("cost")!=0;},
                (unit,id)=>{A(3,unit,unchecked(id<<16),I("stat"));return I("stat");},
                (unit,x,y)=>{A(4,unit,x,y,I("distance"));return I("distance");},
                table=>{int n=p.GetProperty("states").GetArrayLength();A(5,table,n);return n;},
                (table,id)=>{var v=p.GetProperty("states")[id];ushort? value=v.ValueKind==JsonValueKind.Null?null:v.GetUInt16();A(6,table,id,value);return value;},
                (unit,state)=>{int value=p.GetProperty("has").TryGetProperty(unit.ToString(),out var list)&&list.EnumerateArray().Any(v=>v.GetInt32()==state)?1:0;A(7,unit,state,value);return value!=0;},
                (unit,id)=>{A(8,unit,id,I("owner0"));return I("owner0");},
                unit=>{A(9,unit,I("owner1"));return I("owner1");},
                (table,unit,token,id,rank)=>{A(10,table,unit,token,id,rank,I("calc"));return I("calc");},
                (unit,index)=>{byte value=p.GetProperty("units").GetProperty(unit.ToString())[12].GetByte();A(11,unit,index,value);return value;},
                target=>{A(12,I("pet"),target,0,0,I("collision"));return I("collision")!=0;});
            var parameters=GenericPetParameters.Decode(null) with{ModeBonus=I("mode"),MeleeMaxSquared=I("meleeMax"),MeleeBonus=I("melee"),RangedMaxSquared=I("rangedMax"),RangedBonus=I("ranged"),RandomBonus=I("random"),ProgressiveBonus=I("progressive")};
            var context=new GenericPetScoreContext(selector,p.GetProperty("game260").GetByte(),I("pet"),I("owner"),I("frame100"),I("frame104"),I("frame108"));
            var words=p.GetProperty("words").EnumerateArray().Select(v=>v.GetInt32()).ToArray();long word40=Pair(words[0],words[1]),word48=Pair(words[2],words[3]);
            var random=new GenericPetActionRandom(p.GetProperty("seed").GetUInt64());
            GenericPetCombatCandidate Score(GenericPetCombatCandidate value,long w40,long w48)
            {
                A(20,Row(value),unchecked((int)w40),unchecked((int)((ulong)w40>>32)),unchecked((int)w48),unchecked((int)((ulong)w48>>32)));calls++;
                return GenericPetTargetScoring.Rate(context,parameters,w40,w48,value,random,services);
            }
            var row=Candidate(entry[2]);int status=0;
            try
            {
                if(kind=="score"){row=Score(row,word40,word48);}
                else
                {
                    compositions++;var selection=new GenericPetCombatSelection(p.GetProperty("targets").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),word40,word48);
                    row=GenericPetRatingRouting.Rate(selector,parameters,context.Frame100,selection,row,new((table,id)=>Skill(table,id)?.Flags39,Score));
                }
            }
            catch(NotSupportedException e) when(e.Message is "Current type2 target scorer requires skill metadata" or "Current target scorer requires a pet" or "Current target scoring state-table selector requires 1..3")
            {
                status=e.Message=="Current type2 target scorer requires skill metadata"?2:1;if(status==1){fatal++;}else{unsupported++;}A(99,status);
            }
            string actual=JsonSerializer.Serialize(new object[]{status,Row(row),random.Seed,trace});
            Require(actual==entry[3].GetRawText(),"Captured current target score differs at "+count+" "+kind+":"+actual[..Math.Min(actual.Length,340)]);count++;
        }
        Require(count==25115&&calls==24111&&compositions==4176&&fatal==321&&unsupported==6,"Current target-scoring matrix incomplete: "+count+"/"+calls+"/"+fatal+"/"+unsupported);
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Prepared scoring enabled incomplete demons");
        Log.Information("GenericPet target scoring:25115captured-current contexts,4176full routing/scorer compositions,24111scorer entries,321fatal and6unsupported-metadata boundaries;exact descriptors,metadata order and shared pet RNG pass;real world adapters remain gated");
    }
}
