// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetCombatChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetCombatChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string n)=>ConsoleInputHandler.RegisterCommand(n,"Current GenericPet combat orchestration",_=>Core.LoopContext.Post(()=>{try{Check();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure(){Register("generic-pet-combat-check");Register("generic-pet-combat-reload-check");}
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static GenericPetCombatCandidate Candidate(JsonElement r)=>new(r[0].GetInt32(),r[1].GetInt64(),r[2].GetInt32(),r[3].GetInt32(),r[4].GetInt32(),r[5].GetInt32(),r[6].GetInt32(),r[7].GetByte(),r[8].GetByte(),r[9].GetByte(),r[10].GetByte());
    private static object[] Row(GenericPetCombatCandidate c)=>[c.Target,c.Opaque8,c.Rating,c.SkillId,c.Opaque24,c.Opaque28,c.Counter,c.Byte36,c.BaseModeFlag,c.Byte38,c.Byte39];
    private static object[][] Rows(IEnumerable<GenericPetCombatCandidate> c)=>c.Select(Row).ToArray();
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-combat-source-probe.json")));int count=0,fatal=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row[0];int I(string n)=>p.GetProperty(n).GetInt32();var input=Ints(p.GetProperty("memory"));var memory=new GenericPetDecisionMemory{Target=input[0],Value0=input[1],Value1=input[2],Value2=input[3]};
            var frame=new GenericPetDecisionFrame(GenericPetParameters.Decode(null) with{ChooseType=I("choose")},memory);var pet=new GenericPetDecisionUnit(1,true,20,30);
            var trace=new List<object[]>();void A(params object[] values)=>trace.Add(values);List<GenericPetCombatCandidate> rows=null;int ranges=0,searches=0;
            var scores=Ints(p.GetProperty("scores"));var success=Ints(p.GetProperty("success")).ToHashSet();
            var s=new GenericPetCombatServices(
                ()=>{A(0);rows=p.GetProperty("rows").EnumerateArray().Select(Candidate).ToList();return rows;},
                selector=>{Require(selector is >=1 and <=3,"Invalid selector reached synthetic table lookup");return I("base");},
                items=>{int value=p.GetProperty("ranges")[Math.Min(ranges++,1)].GetInt32();A(1,Rows(items),value);return value;},
                (items,radius,bias)=>{int index=searches++;var targets=Ints(p.GetProperty("targets")[Math.Min(index,1)]);long a=101+index,b=201+index;A(2,Rows(items),radius,bias,targets,new[]{a,b});return new(targets,a,b);},
                (c,selection)=>{A(3,Row(c),new[]{selection.Word40,selection.Word48});selection.Word40++;selection.Word48^=c.Opaque24;return c with{Rating=scores[c.Opaque24],Counter=unchecked(c.Counter+10)};},
                ()=>{A(5,2);return 2;},()=>{A(6);return 1;},
                (room,x,y,property,mask)=>{Require(room==1&&x==20&&y==30&&property==2&&mask==64,"Current blocked-position service arguments");A(7,I("blocked"));return I("blocked")!=0;},
                ()=>A(8),(x,y,mode,zero,flags)=>{Require(x==20&&y==30&&mode==5&&zero==0&&flags==4,"Current movement service arguments");A(9,I("move"));return I("move")!=0;},()=>A(10),
                items=>A(11),
                (c,selection)=>{A(4,Row(c),new long[]{selection.Targets[0],selection.Targets[1],selection.Targets[2],selection.Targets[3],selection.Targets[4],selection.Word40,selection.Word48});return c with{Rating=scores[c.Opaque24],Counter=unchecked(c.Counter+10)};},
                items=>{var sorted=I("order")==0?items.ToArray():I("order")==1?items.Reverse().ToArray():items.OrderByDescending(c=>c.Rating).ThenBy(c=>c.Opaque24).ToArray();A(12,sorted.Select(c=>c.Opaque24).ToArray());for(int i=0;i<sorted.Length;i++){items[i]=sorted[i];}},
                (items,remembered,index)=>{var c=items[index];int ok=success.Contains(c.Opaque24)?1:0;A(13,remembered.HasValue?Row(items[remembered.Value]):null,Row(c),ok);if(ok!=0){memory.Target=c.Target;memory.Value0=c.SkillId;memory.Value1=unchecked(c.Counter+1);}return ok!=0;},
                (items,remembered)=>{A(14,remembered.HasValue?Row(items[remembered.Value]):null,Rows(items),I("alternative"));return I("alternative")!=0;});
            int result;
            try{result=GenericPetCombat.Try(new(checked((byte)I("selector")),pet),frame,s)?1:0;}
            catch(NotSupportedException) when(I("selector") is not(1 or 2 or 3)&&input[1]>-1&&input[2]<25){A(15);result=-1;fatal++;}
            string actual=JsonSerializer.Serialize(new object[]{result,new[]{memory.Target,memory.Value0,memory.Value1,memory.Value2},Rows(rows),trace});
            Require(actual==row[1].GetRawText(),"Current x64 combat orchestration differs at context"+count+":"+actual[..Math.Min(actual.Length,160)]);count++;
        }
        Require(count==8079&&fatal==3,"Current combat source controls incomplete");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Detached combat enabled incomplete summons");
        Log.Information("GenericPet combat:8079captured-current orchestration comparisons,3fatal-selector boundaries;real targeting/movement/rating/skill helpers remain gated");
    }
}
