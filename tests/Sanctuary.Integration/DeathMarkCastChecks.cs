// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class DeathMarkCastChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DeathMarkCastChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"death-mark-cast-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {string name=reload?"death-mark-cast-reload-check":"death-mark-cast-check";bool again=reload;ConsoleInputHandler.RegisterCommand(name,"Current do155 pet directive and state composition",_=>Core.LoopContext.Post(()=>
        {try{Check(again);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static void Check(bool reload)
    {
        int generation=reload?int.Parse(File.ReadAllText(Manifest))+1:0;
        var definition=SkillMechanics.For(CombatAbility.DeathMark);
        Require(definition.SourceId==375&&definition.Number("srvdofunc")==155&&definition.Text("auratargetstate")=="death_mark"
            &&definition.Text("pettype")=="demon"&&definition.Number("calc2")==3&&definition.Parameter(9)==38,"Owned Death Mark binding changed");
        foreach(int rank in new[]{1,20,40,255})
        {
            var values=new SkillValues(definition,k=>k==definition.SourceKey?rank:0,_=>0,99);
            Require(values.Eval("calc1")==1&&values.Eval("calc2")==3&&values.Eval("aurarangecalc")==38
                &&values.Eval("auralencalc")==125+13*(rank-1),"Death Mark captured formula context differs");
        }
        var world=NpcWorldAllocation.Capture();using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"death-mark-cast-cases.json")));int n=0;
        foreach(var item in json.RootElement.GetProperty("cases").EnumerateArray())
        {
            var input=item.GetProperty("input");int I(string k)=>input.GetProperty(k).GetInt32();bool B(string k)=>input.GetProperty(k).GetBoolean();
            var expected=item.GetProperty("output");var trace=new List<long[]>();uint flags=unchecked((uint)I("flags"));int ranges=0;
            var c=new DeathMarkCastContext(B("caster")?1U:0,new(I("kind"),B("path"),I("cx"),I("cy")),B("skillContext"),375,I("rank"),I("selector"),I("context"),checked((sbyte)I("petType")),I("petCount"),checked((short)I("state")),I("stateCount"));
            int actual;
            try
            {
                actual=DeathMarkCast.Run(c,new(()=>{trace.Add([1]);return B("row");},()=>{trace.Add([3]);return B("room");},()=>flags|=64,field=>
                {trace.Add([2,field]);return field switch{0x194=>I("maximum"),0x84=>I(ranges++==0?"range1":"range2"),0x190=>I("clear"),0x80=>I("duration"),_=>throw new InvalidOperationException("Unexpected formula")};},
                ()=>{trace.Add([5]);return B("coordinates")?new Point2D(I("x"),I("y")):null;},()=>{trace.Add([4]);return B("secondRoom")?I("currentLevel"):null;},
                ()=>{trace.Add([6]);return B("levelRow")?(byte)I("mode"):null;},(point,mask)=>{Require(point==new Point2D(I("x"),I("y"))&&mask==0x1804,"Death Mark changed teleport collision context");trace.Add([7]);return I("placement");},
                ()=>trace.Add([8]),()=>B("selfTarget")?new(1,I("kind"),true):B("target")?new((uint)I("targetId"),I("targetKind")):null,
                target=>{Require(target.Id==(uint)I("targetId"),"Death Mark validity got another actor");trace.Add([10]);return B("valid");},target=>{Require(target.Id==(uint)I("targetId"),"Death Mark target room got another actor");trace.Add([9]);return B("targetRoom")?I("targetLevel"):null;},
                (type,d)=>{trace.Add([11,type??-999,d.TargetType,d.TargetId,d.X,d.Y,d.Remaining,d.ClearControl?1:0]);d.Remaining=I("remaining");},r=>
                {Require(r.Caster==1&&r.Target.Id==(uint)I("targetId")&&r.Target.Kind==I("targetKind")&&!r.Target.IsCaster,"State creator/target changed");trace.Add([12,r.Skill,r.Rank,r.Duration,r.Stat,r.Value,r.State]);return B("allocate");},
                ()=>trace.Add([13]),()=>trace.Add([14]),(stat,value)=>trace.Add([15,stat,value])));
            }
            catch(NotSupportedException){actual=-1;}
            var wanted=expected.GetProperty("trace").EnumerateArray().Select(x=>x.EnumerateArray().Select(v=>v.GetInt64()).ToArray()).ToArray();
            Require(actual==expected.GetProperty("result").GetInt32()&&flags==expected.GetProperty("flags").GetUInt32()
                &&trace.Count==wanted.Length&&!trace.Where((t,i)=>!t.SequenceEqual(wanted[i])).Any(),"Current Death Mark source return/flags/service/directive/state ordering differs at case "+n);n++;
        }
        Require(n==122&&NpcWorldAllocation.Capture()==world&&!WarlockCombat.Implemented(CombatAbility.DeathMark),"Prepared do155 changed allocation or opened unfinished learned Death Mark");
        File.WriteAllText(Manifest,generation.ToString());Require(generation<=2,"Unexpected Death Mark comparison generation");
        Log.Information("Death Mark:122 complete current-code cases retain guards,two range evaluations and signed arithmetic,coordinate widths,town/level/collision paths,pet-type versus all-pet directive,remaining-count mark condition,state allocation failure and ordered bind/aura/stat writes. Actual pet callback/world state/payment remain explicit;no learned admission.");
    }
}
