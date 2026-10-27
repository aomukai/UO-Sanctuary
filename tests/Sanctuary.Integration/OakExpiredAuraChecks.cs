// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class OakExpiredAuraChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(OakExpiredAuraChecks));
    private sealed record Saved(uint Owner,uint Body,DateTime UntilUtc,int OwnerLife,int OwnerMaximum,int OwnerBaseMaximum,
        int BodyLife,int BodyMaximum,int BodyBaseMaximum,bool SavedWithAura=false,bool Completed=false);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"oak-expired-aura-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("oak-expired-stage",Stage);Register("oak-expired-reload-check",Reload);EventSink.WorldSave+=Saving;
    }
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Real-time Oak aura expiry across actual save/reload",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Stage()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="OakSageBodyFixtureExpiredLease"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.oaksage",out _),"Train expired-lease owner");FixtureSpace.Corridor(p,6900,24,2850);
        // Only the body-recovery inspection clock is held. Both aura leases use
        // their real unmodified six-frame duration and must survive the save.
        var body=DruidOakBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddDays(2));
        int ownerBase=SkillHealth.Maximum256(p),bodyBase=body.MaximumLife256;DruidOakAuras.Pulse(body,TimedEffects.Now);
        SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+7);SkillHealth.SetBalance256(body,6407);PersistentMinions.Suspend(p);
        var lease=DruidOakAuras.State(p);Require(lease!=null&&(lease.UntilUtc-TimedEffects.Now).TotalMilliseconds is >0 and <=240,"Real lease staging deadline");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,lease.UntilUtc,SkillHealth.Balance256(p),SkillHealth.Maximum256(p),ownerBase,
            SkillHealth.Balance256(body),body.MaximumLife256,bodyBase)));
    }
    private static void Saving()
    {
        if(!File.Exists(Manifest)){return;}var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));if(saved.Completed){return;}
        var p=World.FindMobile((Serial)saved.Owner);var body=World.FindMobile((Serial)saved.Body);
        Require(p!=null&&body!=null&&DruidOakAuras.State(p)?.UntilUtc==saved.UntilUtc&&DruidOakAuras.State(body,true)!=null
            && TimedEffects.Now<saved.UntilUtc&&SkillHealth.Balance256(p)==saved.OwnerLife&&SkillHealth.Balance256(body)==saved.BodyLife,"Staged Oak lease expired before save; no reload proof");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{SavedWithAura=true}));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var body=World.FindMobile((Serial)saved.Body) as DruidOakBody;
        Require(saved.SavedWithAura&&!saved.Completed&&TimedEffects.Now>=saved.UntilUtc&&p is{Alive:true,Deleted:false}&&body is{Alive:true,Deleted:false},"Expired-lease actual reload preconditions");
        Require(DruidOakAuras.State(p)==null&&DruidOakAuras.State(body,true)==null&&SkillHealth.Maximum256(p)==saved.OwnerBaseMaximum&&body.MaximumLife256==saved.BodyBaseMaximum,
            "Expired aura retained maximum through actual reload");
        Require(SkillHealth.Balance256(p)==BattleOrders.Rescale256(saved.OwnerLife,saved.OwnerMaximum,saved.OwnerBaseMaximum)
            && SkillHealth.Balance256(body)==BattleOrders.Rescale256(saved.BodyLife,saved.BodyMaximum,saved.BodyBaseMaximum)
            && BattleOrders.Regeneration256(body)==11,"Expired reload lost current-life fraction or regeneration callback");
        PersistentMinions.Dismiss(p);p.Delete();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Completed=true}));
        Log.Information("Oak real-time expiry reload: live six-frame leases at save, expired after restart, exact owner/spirit life proportions and base regeneration restored");
    }
}
