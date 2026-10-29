// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetActionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetActionChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static GenericPetCombatCandidate Candidate(JsonElement r)=>new(r[0].GetInt32(),r[1].GetInt64(),r[2].GetInt32(),r[3].GetInt32(),r[4].GetInt32(),r[5].GetInt32(),r[6].GetInt32(),r[7].GetByte(),r[8].GetByte(),r[9].GetByte(),r[10].GetByte());
    private static object[] Row(GenericPetCombatCandidate r)=>[r.Target,r.Opaque8,r.Rating,r.SkillId,r.Opaque24,r.Opaque28,r.Counter,r.Byte36,r.BaseModeFlag,r.Byte38,r.Byte39];
    private static long[] Opaque=>[0x1122334455667788L,0x2233445566778899L,0x33445566778899aaL,0x445566778899aabbL];
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Current GenericPet actions and weighted choice",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-actions-check");Register("generic-pet-actions-reload-check");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-actions-source-probe.json")));int count=0,fatal=0;var kinds=new Dictionary<string,int>();
        foreach(var entry in doc.RootElement.EnumerateArray())
        {
            string kind=entry[0].GetString();var p=entry[1];int I(string name)=>p.GetProperty(name).GetInt32();
            var rows=entry[2].EnumerateArray().Select(Candidate).ToList();int old=entry[3].GetInt32(),index=entry[4].GetInt32(),active=index;int? remembered=old<0?null:old;
            var memory=new GenericPetDecisionMemory{Target=7,Value0=8,Value1=9,Value2=10};var frame=new GenericPetDecisionFrame(GenericPetParameters.Decode(null) with{Flags=checked((ushort)I("flags")),RangedMinSquared=I("minimum"),RangedMaxSquared=I("maximum")},memory);
            var context=new GenericPetCombatContext(checked((byte)I("selector")),new(1,true,20,30));var random=new GenericPetActionRandom(p.GetProperty("seed").GetUInt64());var success=p.GetProperty("success").EnumerateArray().Select(v=>v.GetInt32()).ToHashSet();
            var shape=p.GetProperty("shape").EnumerateArray().Select(v=>v.GetInt32()).ToArray();var trace=new List<object[]>();void A(params object[] values)=>trace.Add(values);
            int Executed()=>kind=="composed"?(success.Contains(active)?1:0):I("execute");
            var services=new GenericPetActionServices(
                (slot,skill,rank)=>A(0,slot,skill,rank),
                target=>{Require(target==70,"Unexpected declared target");return new(shape[0],shape[1]!=0,shape[2],shape[3]);},
                (x,y)=>{A(1,x,y,I("distance"));return I("distance");},
                (target,mask)=>{A(2,target,mask,I("collision4"));return I("collision4")!=0;},
                target=>{A(3,target,0,0,I("collision0"));return I("collision0")!=0;},()=>A(4),
                (x,y,mode,zero,flags)=>{A(5,x,y,mode,zero,flags,I("move"));return I("move")!=0;},()=>A(6),
                selector=>{A(7,selector,742,I("monster"));return I("monster")<0?null:checked((byte)I("monster"));},
                (target,hint)=>{A(8,target,hint?1:0,I("approach"));return I("approach")!=0;},
                (mode,skill,target,x,y,force)=>{int accepted=Executed();A(11,mode,skill,target,x,y,force?1:0,accepted);return accepted!=0;},
                mode=>{A(12,mode);return new(77,-111,222,Opaque);},
                request=>{int accepted=Executed();A(13,request.Target,request.X,request.Y,(long[])request.Opaque,accepted);return accepted!=0;},
                (selector,skill)=>{A(14,selector,skill,I("skill"));return I("skill")<0?null:checked((byte)I("skill"));},
                target=>{A(15,target,1,I("accepted"));return I("accepted")!=0;},
                selector=>{A(16,selector,I("base"));return I("base");});
            int result;
            try
            {
                if(kind=="disposition"){result=GenericPetActions.Disposition(context,frame,rows,index,services);}
                else if(kind=="act"){result=GenericPetActions.Try(context,frame,rows,remembered,index,services)?1:0;}
                else
                {
                    result=GenericPetActions.Alternative(rows,remembered,random,(previous,selected)=>
                    {active=selected;A(20,previous??-1,selected,Row(rows[selected]));return kind=="weighted"?success.Contains(selected):GenericPetActions.Try(context,frame,rows,previous,selected,services);})?1:0;
                }
            }
            catch(NotSupportedException) when(I("selector") is not(1 or 2 or 3)&&kind=="act"&&rows[index].BaseModeFlag!=0)
            {A(99);result=-1;fatal++;}
            string actual=JsonSerializer.Serialize(new object[]{result,new[]{memory.Target,memory.Value0,memory.Value1,memory.Value2},random.Seed,rows.Select(Row).ToArray(),trace});
            Require(actual==entry[5].GetRawText(),"Captured current action differs at "+count+" "+kind+":"+actual[..Math.Min(actual.Length,180)]);count++;kinds[kind]=kinds.GetValueOrDefault(kind)+1;
        }
        Require(count==18070&&fatal==108,"Current action source matrix incomplete: "+count+"/"+fatal);
        Require(kinds["disposition"]==5446&&kinds["act"]==10512&&kinds["weighted"]==1056&&kinds["composed"]==1056,"Current action component scope differs");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Prepared actions enabled incomplete demons");
        Log.Information("GenericPet actions:18070captured-current disposition/action/weighted/composed cases,108fatal-selector boundaries;exact memory,raw mode payloads,weights and pet RNG pass;real world/mode/skill services remain gated");
    }
}
