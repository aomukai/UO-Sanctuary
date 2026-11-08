// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class MiasmaChainLaunchChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(MiasmaChainLaunchChecks));
    private sealed record Saved(uint Owner,uint Helm,MiasmaChainRequest Request,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"miasma-chain-launch-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("miasma-chain-launch-check",Check);Register("miasma-chain-launch-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Current Miasma Chain launch request;no maker/cloud execution",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static int[] Request(MiasmaChainRequest r)=>[r.Flags,(int)r.Caster,r.Missile,r.X,r.Y,r.TargetX,r.TargetY,r.Skill,r.Rank];
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"miasma-chain-launch-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var i=row.GetProperty("input");int I(string k)=>i.GetProperty(k).GetInt32();bool B(string k)=>i.GetProperty(k).GetBoolean();
            var context=new MiasmaChainCast(B("actor")?1u:0,I("kind"),B("path"),I("x"),I("y"),I("skill"),I("rank"),I("flags"));
            var trace=new List<string>();var requests=new List<int[]>();int flags=context.Flags;
            var result=MiasmaChainLaunch.Run(context,new(()=>{trace.Add("row");return B("row");},()=>{trace.Add("progressive");return I("missile");},
                ()=>{trace.Add("target");return(B("target"),I("aimX"),I("aimY"));},f=>flags=f,
                r=>{trace.Add("create");Require(flags==(context.Flags|0x40),"Action flag not visible at factory call");requests.Add(Request(r));return B("allocation");}));
            var expected=row.GetProperty("output");Require(result.Result==expected.GetProperty("result").GetInt32()&&result.Flags==expected.GetProperty("flags").GetInt32(),"Current Miasma Chain result/flag mismatch");
            Require(trace.SequenceEqual(expected.GetProperty("trace").EnumerateArray().Select(x=>x.GetString())),"Current Miasma Chain service order");
            var want=expected.GetProperty("requests").EnumerateArray().Select(r=>r.EnumerateArray().Select(n=>n.GetInt32()).ToArray()).ToArray();
            Require(requests.Count==want.Length&&requests.Where((r,n)=>!r.SequenceEqual(want[n])).Any()==false,"Current Miasma Chain factory request mismatch");count++;
        }
        Require(count==20&&!WarlockCombat.Implemented(CombatAbility.MiasmaChain),"Missing current contexts or unfinished Chain enabled");
    }
    private static MiasmaChainRequest Primary(PlayerMobile owner)
    {
        var values=SkillMechanics.Values(owner,CombatAbility.MiasmaChain);int maker=MiasmaChainLaunch.PinnedMaker(values);MiasmaChainRequest request=null;
        var result=MiasmaChainLaunch.Run(new(owner.Serial.Value,0,true,180,220,399,values.Rank,0),new(()=>true,()=>maker,()=>(true,212,230),_=>{},r=>{request=r;return true;}));
        Require(result.Result==1&&request is{Flags:33,Missile:711,X:180,Y:220,TargetX:212,TargetY:230,Skill:399},"Pinned Chain must create maker711,not a direct bolt/chain/cloud");return request;
    }
    private static void Check()
    {
        Source();var p=ClassSkillChecks.Player(DiabloClass.Warlock);ClassSkillChecks.Train(p,"warlock.miasmachain",1);p.RawStr=1000;
        var before=Primary(p);Require(before.Rank==1&&SkillMechanics.Values(p,CombatAbility.MiasmaChain).Mana256==1152,"Rank1 Chain costs4.5mana");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Tarnhelm").Id,99,new Random(27),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Chain +skills helm");ClassGear.Refresh(p);
        var after=Primary(p);Require(after.Rank==2&&SkillMechanics.Values(p,CombatAbility.MiasmaChain).Mana256==1280,"Effective +skills/mana scaling");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,helm.Serial.Value,after,0)));
        Log.Information("Miasma Chain current do167:20ordered request/guard/failure contexts;source/path width,flags33,zero reserved fields and return only on allocation success. Primary creates711maker43→712chain45/hit41→713maker43→696cloud41;original gear/request persists. No missile stage or learned cast enabled.");
    }
    private static void Reload()
    {
        Source();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var helm=World.FindItem((Serial)saved.Helm);
        Require(saved.Generation<2&&helm?.Parent==p,"Original Chain gear lost on reload");ClassGear.Refresh(p);Require(Primary(p)==saved.Request,"Original Chain rank/request changed after reload");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
