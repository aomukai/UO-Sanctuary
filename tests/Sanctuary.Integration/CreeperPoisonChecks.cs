// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class CreeperPoisonChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CreeperPoisonChecks));
    private sealed record Saved(uint Owner,uint RemovedActor,uint Target,CreeperMat Mat,CreeperPoisonContext Context,TimedEffectState Poison,int Life);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"creeper-poison-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {Register("creeper-poison-check",Check);Register("creeper-poison-reload-check",Reload);}
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Creeper actual poison packet acceptance",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static TimedEffectState State(Mobile t)=>TimedEffects.Snapshot().FirstOrDefault(x=>x.Owner==t.Serial.Value)
        ??throw new InvalidOperationException($"Missing poison state: alive={t.Alive}, poisoned={t.Poisoned}, resistance={t.PoisonResistance}, life={SkillHealth.Balance256(t)}");
    private static void Hold(Mobile t)
    {var s=State(t);TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=t.Serial.Value).Append(s with{UpdatedUtc=TimedEffects.Now.AddDays(2)}).ToArray());}
    private static void Train(PlayerMobile p,string id)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Druid,id,out var row),"Talent");foreach(var prior in row.Prerequisites){Train(p,prior);}
        if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="CreeperPoisonFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);
        ClassCreation.Profiles.AdvanceLevel(p,99);Train(p,"druid.poisoncreeper");FixtureSpace.Corridor(p,7050,24,2900);return p;
    }
    private static GraveyardZombie Target(PlayerMobile p,int resistance=0)
    {
        var t=new GraveyardZombie{CantWalk=true,NoKillAwards=true};t.SetHits(500);t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        t.AIObject.AITimer.Stop();t.SetResistance(ResistanceType.Poison,resistance);return t;
    }
    private static CreeperMat Fixed(int rate)=>new(1,1,14000,5800,125,109)
        {Poison=new(new(rate,rate,100),100,SourceRandom.InitLow(42))};
    private static void Packets(PlayerMobile p)
    {
        var context=new CreeperPoisonContext(99,0);
        Require(DruidCreeperPoison.Damage(1,context,100)==new CreeperPoison(32,48,100),"Rank1raw poison packet");
        Require(DruidCreeperPoison.Damage(1,context with{RabiesHardRank=20},100)==new CreeperPoison(96,144,100),"Intended hard Rabies synergy");
        Require(DruidCreeperPoison.Damage(1,context,33)==new CreeperPoison(10,15,100),"Slow damage rate changed duration or rounded incorrectly");
        var at=TimedEffects.Now.AddDays(2);var t=Target(p);int life=SkillHealth.Balance256(t);var packet=Fixed(64);
        try
        {
            DruidCreeperPoison.Deliver(p,t,packet,at);Require(SkillHealth.Balance256(t)==life&&State(t) is{PoisonMs:4000,PoisonClock.Rate256:64},"Poison was an immediate hit or lost source rate");
            TimedEffects.Settle(t,at.AddMilliseconds(39));Require(SkillHealth.Balance256(t)==life,"Poison landed before source40msframe");
            TimedEffects.Settle(t,at.AddMilliseconds(40));Require(SkillHealth.Balance256(t)==life-64&&State(t).PoisonSource==p.Serial.Value,"Poison rate/source consumer");
            DruidCreeperPoison.Deliver(p,t,Fixed(32),at.AddMilliseconds(40));Require(State(t).PoisonClock.Rate256==64,"Weaker poison replaced a stronger rate");
            DruidCreeperPoison.Deliver(p,t,Fixed(128),at.AddMilliseconds(40));Require(State(t) is{PoisonMs:4000,PoisonClock.Rate256:128},"Stronger poison did not replace rate/length");
            TimedEffects.Clear(t);t.SetResistance(ResistanceType.Poison,50);DruidCreeperPoison.Deliver(p,t,packet,at.AddSeconds(1));
            Require(State(t) is{PoisonMs:4000,PoisonClock.Rate256:32},"Resistance changed poison length or failed to halve rate");
            TimedEffects.Clear(t);t.SetResistance(ResistanceType.Poison,100);life=SkillHealth.Balance256(t);DruidCreeperPoison.Deliver(p,t,packet,at.AddSeconds(1));
            Require(!t.Poisoned&&SkillHealth.Balance256(t)==life,"Immune monster received poison");
            t.SetResistance(ResistanceType.Poison,-100);DruidCreeperPoison.Deliver(p,t,packet,at.AddSeconds(2));
            Require(State(t).PoisonClock.Rate256==128,"Negative resistance did not amplify rate");
            TimedEffects.Clear(t);t.SetResistance(ResistanceType.Poison,0);DruidCreeperPoison.Deliver(p,t,packet,at.AddSeconds(3));
            TimedEffects.Settle(t,at.AddSeconds(7));Require(!t.Poisoned&&(!TimedEffects.TryActor(t,out var expired)||expired.PoisonMs==0),"Four-second poison did not expire");
            SkillHealth.SetBalance256(t,1);DruidCreeperPoison.Deliver(p,t,packet,at.AddSeconds(8));TimedEffects.Settle(t,at.AddSeconds(8).AddMilliseconds(40));
            Require(!t.Alive&&t.FindMostTotalDamager(false)==p,"Lethal poison failed or lost original-player damage attribution");
        }
        finally{t.Delete();}
    }
    private static void Check()
    {
        var p=Fresh();Packets(p);var at=TimedEffects.Now.AddDays(2);
        var old=DruidCreeperBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),at);Train(p,"druid.rabies");
        Require(DruidCreeperPoison.Capture(old.Prepared.Snapshot).RabiesHardRank==0,"Provisional birth context changed with later hard training");old.Delete();
        while(ClassCreation.Profiles.GetRank(p.Serial,"druid.rabies")<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.rabies",out _),"Raise Rabies");}
        var body=DruidCreeperBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),at);var context=DruidCreeperPoison.Capture(body.Prepared.Snapshot);
        var mat=DruidCreeperPoison.Initialize(new(1,1,(p.X+3)*2,p.Y*2,125,109){Z=p.Z},context,100);uint removed=body.Serial.Value;body.Delete();
        var target=Target(p);mat=DruidCreeperPoison.Deliver(p,target,mat,at);var state=State(target);
        Require(context.RabiesHardRank==20&&state.PoisonClock.Rate256 is >=96 and <144&&state.PoisonSource==p.Serial.Value,"Actual supplied-rank/source-zero packet roll or removed-actor delivery");
        Hold(target);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,removed,target.Serial.Value,mat,context,State(target),SkillHealth.Balance256(target))));
        Log.Information("Creeper poison: raw32/48over100frames, hard20synergy96/144, slow33rates10/15,40mshealth, resistance/immunity/negative resistance, replacement/expiry/lethal credit pass; birth Rabies timing remains provisional");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BaseCreature)World.FindMobile((Serial)saved.Target);
        Require(target is{Alive:true,Deleted:false} && World.FindMobile((Serial)saved.RemovedActor)==null && SkillHealth.Balance256(target)==saved.Life
            && JsonSerializer.Serialize(State(target))==JsonSerializer.Serialize(saved.Poison),"Saved Creeper poison lost clock/rate/source/health or replayed a tick");
        var now=TimedEffects.Now;TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=target.Serial.Value).Append(saved.Poison with{UpdatedUtc=now}).ToArray());
        TimedEffects.Settle(target,now.AddMilliseconds(40));var state=State(target);
        Require(saved.Life-SkillHealth.Balance256(target)==saved.Poison.PoisonClock.Rate256 && state.PoisonMs==saved.Poison.PoisonMs-40&&state.PoisonSource==p.Serial.Value,"Resumed Creeper poison changed rate/owner or repeated packet creation");
        Hold(target);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Poison=State(target),Life=SkillHealth.Balance256(target)}));
    }
}
