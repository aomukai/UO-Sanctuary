// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetDecisionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetDecisionChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string n)=>ConsoleInputHandler.RegisterCommand(n,"Current GenericPet root/follow policy",_=>Core.LoopContext.Post(()=>{try{Check();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure(){Register("generic-pet-decisions-check");Register("generic-pet-decisions-reload-check");}
    private static int[] Array(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static void Trace(List<int[]> actual,JsonElement expected,string why)
    {Require(actual.Count==expected.GetArrayLength()&&actual.Select((x,i)=>x.SequenceEqual(Array(expected[i]))).All(x=>x),why);}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-decisions-source-probe.json")));int roots=0,follows=0;
        foreach(var row in doc.RootElement.GetProperty("root").EnumerateArray())
        {
            var p=Array(row[0]);var events=Array(row[1]).Select(x=>checked((byte)x)).ToArray();var pet=new GenericPetDecisionUnit(p[0],true,20,30);var owner=new GenericPetDecisionUnit(Math.Max(0,p[2]),p[3]!=0,45,55);
            var trace=new List<int[]>();void A(params int[] args)=>trace.Add(args);int calls=0;
            var s=new GenericPetRootServices(
                ()=>{A(0);return p[2]>=0?owner:null;},
                o=>{A(1,o!=null?1:0);return new(GenericPetParameters.Decode(null) with{LeashSquared=p[5]},new(){Target=0x777,Value0=31,Value1=32,Value2=33});},
                i=>A(2,i),(unit,x,y)=>{Require(ReferenceEquals(unit,pet),"Root distance origin");A(3,x,y);return p[4];},
                unit=>{int which=ReferenceEquals(unit,owner)?1:0;A(5,which);return p[6+which]==-1?null:p[6+which];},
                (o,f)=>{int value=p[8+calls++];A(4,o!=null?1:0,value);f.Memory.Value1++;return value!=0;},
                (o,f)=>{A(6,o!=null?1:0,p[10]);f.Memory.Value2+=2;return p[10]!=0;},n=>A(7,n),
                m=>{A(8,m.Target);m.Value0+=4;},(a,b,c)=>A(9,a,b,c));
            GenericPetDecisions.Think(new(pet,p[1]>=1,p[1]>=2,p[1]>=3,events),s);
            Trace(trace,row[2],"Current x64 root service order/state differs");roots++;
        }
        foreach(var row in doc.RootElement.GetProperty("follow").EnumerateArray())
        {
            var p=Array(row[0]);var pet=new GenericPetDecisionUnit(p[14],true,20,30);var owner=new GenericPetDecisionUnit(p[2],p[3]!=0,45,55,p[15]!=0,65,75);
            var trace=new List<int[]>();void A(params int[] args)=>trace.Add(args);int distance=0;
            var s=new GenericPetFollowServices((unit,x,y)=>{Require(ReferenceEquals(unit,pet),"Follow distance origin");A(3,x,y);return distance++==0?p[4]:p[9];},
                unit=>{Require(ReferenceEquals(unit,owner),"Moving predicate owner");A(10,p[6]);return p[6]!=0;},
                (unit,x,y)=>{int which=ReferenceEquals(unit,owner)?1:0,state=p[10+which];A(11,which);A(12,which,x,y);if(state!=0){A(13,which,x/5,y/5);}return new(state!=0,state==2,p[7+which]);},
                mode=>{A(14,mode,p[12]);return p[12]!=0;});
            bool result=GenericPetFollow.Try(new(p[0]!=0?pet:null,p[1]!=0?owner:null,p[5],p[13]),s);
            Require((result?1:0)==row[1][0].GetInt32(),"Current x64 follow return differs");Trace(trace,row[1][1],"Current x64 follow service order/mode differs");follows++;
        }
        Require(roots==12816&&follows==46936,"Current decision source matrix incomplete");
        Require(new GenericPetDecisionUnit(1,false,99,99).Coordinates()==(0,0)&&new GenericPetDecisionUnit(1,true,-1,65536).Coordinates()==(65535,0),"Dynamic coordinate fields must preserve unsigned16 representation");
        Require(new GenericPetDecisionUnit(2,true,-1,int.MinValue).Coordinates()==(-1,int.MinValue),"Static coordinate fields must preserve signed32 representation");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Detached decisions enabled incomplete summons");
        Log.Information("GenericPet decisions:12816captured-current root/46936follow comparisons;ordered services only,movement/combat/owner callbacks remain gated");
    }
}
