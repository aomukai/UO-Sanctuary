// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class CleaveSweepChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(CleaveSweepChecks));
    private sealed record Saved(uint Owner,uint Helm,CleaveSweepRequest Request,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"cleave-sweep-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("cleave-sweep-check",Check);Register("cleave-sweep-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Current Cleave outer sweep request",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static int[] Request(CleaveSweepRequest r)=>[r.X,r.Y,r.CenterX,r.CenterY,r.Skill,r.Rank,r.Start,r.End,r.Extra,r.SourceDamage,r.Radius,r.Filter];
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"cleave-sweep-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var i=row.GetProperty("input");int I(string k)=>i.GetProperty(k).GetInt32();bool B(string k)=>i.GetProperty(k).GetBoolean();
            var input=new CleaveSweepInput(B("actor"),I("kind"),B("path"),I("x"),I("y"),I("skill"),I("rank"),I("flags"),I("filter"),I("sourceDamage"));
            var trace=new List<string>();var points=new List<uint[]>();var requests=new List<CleaveSweepRequest>();int written=input.Flags;
            var result=CleaveSweep.Run(input,new(()=>{trace.Add("row");return B("row");},()=>{trace.Add("target");return(B("target"),I("targetX"),I("targetY"));},f=>written=f,
                ()=>{trace.Add("gate");Require(written==(input.HasActor?input.Flags|0x40:input.Flags),"Do flag not visible before gate");return B("delayed");},
                (a,b)=>{trace.Add("direction");points.Add([unchecked((uint)a.X),unchecked((uint)a.Y),unchecked((uint)b.X),unchecked((uint)b.Y)]);return I("heading");},
                field=>{trace.Add("calc"+((field-0x1a0)/4+5));return I(field==0x1a0?"width":field==0x1a4?"total":"extra");},
                ()=>{trace.Add("reach");return I("reach");},()=>{trace.Add("center");return new(I("centerX"),I("centerY"));},r=>{trace.Add("area");requests.Add(r);}));
            var o=row.GetProperty("output");Require(result.Result==o.GetProperty("result").GetInt32()&&result.Flags==o.GetProperty("flags").GetInt32(),"Current Cleave result/flags");
            Require(trace.SequenceEqual(o.GetProperty("trace").EnumerateArray().Select(x=>x.GetString())),"Current Cleave service order");
            var expectedPoints=o.GetProperty("points").EnumerateArray().Select(x=>x.EnumerateArray().Select(n=>n.GetUInt32()).ToArray()).ToArray();
            var expected=o.GetProperty("requests").EnumerateArray().Select(Ints).ToArray();
            Require(points.Count==expectedPoints.Length&&points.Where((p,n)=>!p.SequenceEqual(expectedPoints[n])).Any()==false,"Fixed-point source/target coordinates");
            Require(requests.Count==expected.Length&&requests.Where((r,n)=>!Request(r).SequenceEqual(expected[n])).Any()==false,"Current full Cleave arc/area context");count++;
        }
        Require(count==26&&!WarlockCombat.Implemented(CombatAbility.Cleave),"Missing sweep contexts or unfinished admission enabled");
    }
    private static CleaveSweepRequest Primary(PlayerMobile owner)
    {
        var values=SkillMechanics.Values(owner,CombatAbility.Cleave);CleaveSweepRequest request=null;
        var result=CleaveSweep.Run(new(true,0,true,180,220,391,values.Rank,0,41347,128),
            new(()=>true,()=>(true,212,230),_=>{},()=>false,(_,_)=>0,f=>CleaveSweep.Formula(values,f),()=>2,()=>new(180,220),r=>request=r));
        Require(result.Result==1&&request!=null,"Primary Cleave sweep not produced");return request;
    }
    private static void Check()
    {
        Source();var owner=ClassSkillChecks.Player(DiabloClass.Warlock);ClassSkillChecks.Train(owner,"warlock.cleave",19);owner.RawStr=1000;
        var before=Primary(owner);Require(before.Rank==19&&before.Start==34&&before.End==30,"Pinned rank19 outer arc");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(27),true);owner.AddToBackpack(helm);Require(owner.EquipItem(helm),"Cleave Tarnhelm");ClassGear.Refresh(owner);
        var after=Primary(owner);Require(after.Rank==20&&after.Start==43&&after.End==21&&after.Radius==6,"Effective rank20 exact current quantized arc/radius");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,helm.Serial.Value,after,0)));
        Log.Information("Cleave outer sweep:26 current-instruction contexts;ordered flags/gate/formulas,fixed coordinates,float32 bounds/wrap,radius/source-damage/filter context. Original Tarnhelm19→20 preserves the pinned width drop. Recipient callback/start68/actual area contacts remain unavailable;DTO persistence only.");
    }
    private static void Reload()
    {
        Source();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var helm=World.FindItem((Serial)saved.Helm);
        Require(saved.Generation<2&&helm?.Parent==owner,"Original Cleave gear lost over reload");ClassGear.Refresh(owner);Require(Primary(owner)==saved.Request,"Saved sweep/actual gear continuation changed");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
