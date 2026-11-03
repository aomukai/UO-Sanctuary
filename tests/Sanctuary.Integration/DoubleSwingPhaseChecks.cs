// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class DoubleSwingPhaseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DoubleSwingPhaseChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("double-swing-phase-check","Complete legacy do70/GUID target-selection comparisons",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("double-swing-phase-check: passed");}catch(Exception e){Log.Error(e,"double-swing-phase-check: failed");}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"double-swing-phase-source-probe.json")));int count=0,finds=0,phases=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            string family=row[0].GetString();var p=row[1];int I(string n)=>p.GetProperty(n).GetInt32();uint U(string n)=>p.GetProperty(n).GetUInt32();bool B(string n)=>p.GetProperty(n).GetBoolean();
            var trace=new List<object[]>();void A(params object[] values)=>trace.Add(values);
            var candidates=p.GetProperty("candidates").EnumerateArray().Select((v,i)=>v.ValueKind==JsonValueKind.Null?null:new SourceGuidTarget(i+2,v.GetUInt32())).ToArray();
            var unit=new DoubleSwingPhaseUnit(I("frame"),U("flags"));SourceGuidTarget current=B("present")?new(1,U("anchor")):null;int output=I("count");
            IEnumerable<SourceGuidTarget> Enumerate(int x,int y,int range,uint filter)
            {A(3,x,y,range,filter,U("anchor"));return candidates;}
            SourceGuidTarget Find(SourceGuidFindRequest request)=>SourceGuidTargets.Find(request,Enumerate,ref output);
            int result;
            if(family=="find")
            {
                result=Find(new(I("x"),I("y"),I("range"),U("filter"),U("anchor"),B("report")))?.Handle??0;finds++;
            }
            else
            {
                Require(family=="phase","Unknown Double Swing source family");
                result=BarbarianDoubleSwingPhase.Execute(unit,I("skill"),I("rank"),new(
                    ()=>{A(1,current?.Handle??0);return current;},()=>{A(2,I("range"));return I("range");},Find,
                    target=>{A(4,target.Handle);current=target;},
                    (skill,rank)=>{A(5,skill,rank,current?.Handle??0,I("prepare"));return I("prepare");},
                    (target,zero)=>A(6,target.Handle,zero)));phases++;
            }
            string actual=JsonSerializer.Serialize(new object[]{result,unit.Flags,current?.Handle??0,output,trace});
            Require(actual==JsonSerializer.Serialize(row[2]),"Double Swing/GUID case "+count+":"+actual+" expected:"+row[2]);count++;
        }
        Require(count==7536&&finds==1470&&phases==6066,"Incomplete Double Swing/GUID source matrix");
        Require(!BarbarianCombat.Implemented(CombatAbility.DoubleSwing),"Legacy release enabled unresolved modern start68");
        Log.Information("Double Swing:7536complete legacy cases,1470GUID selectors/6066composed release phases;flag/parity,target/count and ignored-start-return/drain order pass;modern start68 remains gated");
    }
}
