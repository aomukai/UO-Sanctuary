// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class OakSkillRankChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(OakSkillRankChecks));
    private sealed record Saved(uint Owner,uint Body,OakBodyState State,OakLease Self,int Life);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"oak-rank-fixtures.json");private static int _scene;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("oak-rank-check",Check);Register("oak-rank-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Oak innate rank and Battle Command lifecycle",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Fresh(DiabloClass kind=DiabloClass.Druid)
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="OakRankFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,kind);ClassCreation.Profiles.AdvanceLevel(p,99);
        void Train(string id)
        {
            Require(TalentCatalog.TryGet(kind,id,out var talent),"Rank fixture talent");
            foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior);}}
            Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out var why),"Train rank fixture: "+why);
        }
        Train(kind==DiabloClass.Druid?"druid.oaksage":"barbarian.battlecommand");
        FixtureSpace.Corridor(p,6900,24,2500+32*_scene++);return p;
    }
    private static void End(PlayerMobile p)
    {foreach(var pet in p.AllFollowers??[]){DruidOakAuras.Clear(pet);BattleCommand.Clear(pet);}DruidOakAuras.Clear(p);BattleCommand.Clear(p);PersistentMinions.Dismiss(p);p.Delete();}
    private static void Check()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"oak-skill-rank-source-probe.json")));int cases=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {if(row[2].GetInt32()!=-1||row[3].GetInt32()!=1){continue;}Require(DruidOakStats.EffectiveAuraRank(row[0].GetInt32(),row[1].GetInt32())==row[4].GetInt32(),"Complete NPC bonus/level getter comparison");cases++;}
        Require(cases==510,"Incomplete innate rank contexts");
        var p=Fresh();var supplier=Fresh(DiabloClass.Barbarian);var at=TimedEffects.Now.AddHours(1);var body=DruidOakBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),at.AddDays(2));
        try
        {
            var born=body.Prepared.Birth;DruidOakAuras.Pulse(body,at);Require(DruidOakAuras.State(body,true).Rank==1&&body.MaximumLife256==19968,"Base aura rank");
            BattleCommand.Apply(supplier,body,1,at);Require(BattleCommand.Bonus(body)==1&&body.Prepared.Birth==born&&DruidOakAuras.State(body,true).Rank==1,"Command changed birth or applied aura ahead of pulse");
            DruidOakAuras.Pulse(body,at.AddMilliseconds(40));Require(DruidOakAuras.State(body,true) is{Rank:2,Percent:35}&&body.MaximumLife256==20736&&DruidOakAuras.State(p).Percent==35,"Command NPC aura rank/maximum");
            var high=DruidOakAuras.State(body,true);BattleCommand.Clear(body);DruidOakAuras.Pulse(body,at.AddMilliseconds(80));
            Require(DruidOakAuras.State(body,true)==high,"Command removal bypassed weaker-rank refusal/lease lifetime");
            DruidOakAuras.Expire(high.UntilUtc);DruidOakAuras.Pulse(body,high.UntilUtc);
            Require(DruidOakAuras.State(body,true) is{Rank:1,Percent:30}&&body.Prepared.Birth==born,"Lower aura failed after stronger lease expired");
            Require(DruidOakStats.EffectiveAuraRank(98,1)==99&&DruidOakStats.EffectiveAuraRank(99,1)==99&&DruidOakStats.EffectiveAuraRank(255,0)==99,"Source/current-table NPC ceiling");
        }
        finally{End(p);End(supplier);}
        p=Fresh();supplier=Fresh(DiabloClass.Barbarian);body=DruidOakBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddDays(2));
        BattleCommand.Apply(supplier,body,1,TimedEffects.Now.AddDays(2));DruidOakAuras.Pulse(body,TimedEffects.Now.AddDays(2));SkillHealth.SetBalance256(body,6407);supplier.Delete();PersistentMinions.Suspend(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,body.Prepared,DruidOakAuras.State(body,true),SkillHealth.Balance256(body))));
        Log.Information("Oak innate rank:510complete source getter comparisons; current ceiling99, live Command pulse increase and delayed lower-rank return preserve immutable birth");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=World.FindMobile((Serial)saved.Body) as DruidOakBody;
        Require(body is{Alive:true,Deleted:false}&&body.ControlMaster==p&&body.Map==Map.Internal&&body.SavedText==JsonSerializer.Serialize(saved.State)
            && SkillHealth.Balance256(body)==saved.Life&&BattleCommand.Bonus(body)==1&&DruidOakStats.EffectiveAuraRank(body.Prepared.Birth.AuraRank,1)==2
            && DruidOakAuras.State(body,true)==saved.Self,"Saved Command/innate rank/birth/aura state changed");
    }
}
