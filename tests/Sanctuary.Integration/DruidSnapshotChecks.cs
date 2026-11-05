// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class DruidSnapshotChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidSnapshotChecks));
    private static readonly CombatAbility[] Skills=[CombatAbility.SummonSpiritWolf,CombatAbility.SummonDireWolf,CombatAbility.SummonGrizzly];
    private sealed record Actor(uint Owner,uint Body,CombatAbility Ability,int Hard,int OwnerHard,int Life,int Maximum,int Mana,long Order,string Birth,ulong Seed,SummonUpgradeSnapshot Upgrade);
    private sealed record Saved(Actor[] Actors,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-snapshot-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static bool Creating;
    private static void Rates()
    {
        var mana=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>Creating||p.Name=="DruidSnapshotOwner"?TimeSpan.FromHours(1):mana?.Invoke(p)??Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure()
    {
        Rates();Register("druid-snapshot-check",Check);Register("druid-snapshot-reload-check",Reload);
    }
    private static PlayerMobile Owner()
    {
        Creating=true;
        try{var p=SpiritWolfWorldChecks.Fresh(column:6400);p.Name="DruidSnapshotOwner";return p;}
        finally{Creating=false;}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Current Druid public births and immutable inherited ranks",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static string Birth(SkeletonGuardian b)=>b switch
    {
        DruidSpiritWolfBody wolf=>Json(wolf.Prepared.Snapshot),DruidDireWolfBody wolf=>Json(wolf.Prepared.Snapshot),
        DruidGrizzlyBody bear=>Json(bear.Prepared.Snapshot),_=>throw new InvalidOperationException("Missing beast")
    };
    private static ulong Seed(SkeletonGuardian b)=>b switch
    {DruidSpiritWolfBody wolf=>wolf.Prepared.Controller.Seed,DruidDireWolfBody wolf=>wolf.Prepared.Controller.Seed,DruidGrizzlyBody bear=>bear.Prepared.Controller.Seed,_=>0};
    private static void Hold(SkeletonGuardian b)
    {
        DateTime at=TimedEffects.Now.AddDays(3);
        switch(b)
        {
            case DruidSpiritWolfBody wolf:wolf.RestorePrepared(wolf.Prepared with{Controller=DruidSpiritWolfWorld.Park(wolf.Prepared.Controller)with{NextUtc=at}});break;
            case DruidDireWolfBody wolf:wolf.RestorePrepared(wolf.Prepared with{Controller=DruidDireWolfWorld.Park(wolf.Prepared.Controller)with{NextUtc=at}});break;
            case DruidGrizzlyBody bear:bear.RestorePrepared(bear.Prepared with{NextRecoveryUtc=at,Controller=DruidGrizzlyWorld.Park(bear.Prepared.Controller)with{NextUtc=at}});break;
        }
    }
    // Run the real AI entry path, with an explicit source clock. This is not
    // a claim of a naturally scheduled attack or full native geometry parity.
    private static BeastAttackStats Enter(SkeletonGuardian b)
    {
        Hold(b);
        for(int i=0;i<300;i++)
        {
            switch(b)
            {
                case DruidSpiritWolfBody wolf:
                    DruidSpiritWolfWorld.Advance(wolf,wolf.Prepared.Controller.NextUtc,true);
                    Require(wolf.Prepared.Controller.Fault==null,"Spirit Wolf AI fault");
                    if(wolf.Prepared.Controller is{Mode:4,AttackPacket:{} a}){return a;}break;
                case DruidDireWolfBody wolf:
                    DruidDireWolfWorld.Advance(wolf,wolf.Prepared.Controller.NextUtc,true);
                    Require(wolf.Prepared.Controller.Fault==null,"Dire Wolf AI fault");
                    if(wolf.Prepared.Controller is{Mode:4,Feeding:false,AttackPacket:{} dire}){return dire;}break;
                case DruidGrizzlyBody bear:
                    DruidGrizzlyWorld.Advance(bear,bear.Prepared.Controller.NextUtc,true);
                    Require(bear.Prepared.Controller.Fault==null,"Grizzly AI fault");
                    if(bear.Prepared.Controller is{Mode:4,AttackPacket:{} grizzly}){return grizzly;}break;
            }
        }
        throw new InvalidOperationException("No ordinary beast attack entry: "+b.GetType().Name+" birth="+Birth(b)+" point="+b.Location+" owner="+b.ControlMaster?.Location+" controller="+(b is DruidGrizzlyBody debugBear?Json(debugBear.Prepared.Controller):b is DruidDireWolfBody debugDire?Json(debugDire.Prepared.Controller):Json(((DruidSpiritWolfBody)b).Prepared.Controller)));
    }
    private static void Upgrades(SkeletonGuardian b,CombatAbility ability,int hard)
    {
        Require(SummonUpgradeSnapshots.HardRank(b,ability)==hard,"Born Druid hard rank changed");
        if(b is DruidSpiritWolfBody)
        {Require(NpcOpenWounds.Inherent(b)==(hard>=10?100:0)&&(b.ColdResistance>=100)==(hard>=20),"Born Spirit Wolf wound/cold gates");}
        if(b is DruidDireWolfBody wolf)
        {Require(DireWolfUpgrades.MovementPercent(wolf)==(hard>=10?100:0)&&(DireWolfUpgrades.DamagePercent(wolf)>0)==(hard>=20),"Born Dire Wolf Fast/Strong gates");}
        if(b is DruidGrizzlyBody)
        {Require(NpcCrushing.Chance(b)==(hard>=20?10:0),"Born Grizzly crushing gate");}
    }
    private static void Check()
    {
        var rows=new List<Actor>();
        foreach(var ability in Skills)foreach(int hard in new[]{9,10,19,20})
        {
            var p=Owner();Require(Mobile.GetManaRegenRate(p)==TimeSpan.FromHours(1),"Snapshot fixture mana hold");ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId,hard);PoisonDaggerChecks.Named(p,"Tarnhelm");
            NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
            var spiritSeed=DruidSpiritWolfCasting.SeedLow;var direSeed=DruidDireWolfCasting.SeedLow;var bearSeed=DruidGrizzlyCasting.SeedLow;
            try
            {
                DruidSpiritWolfCasting.SeedLow=()=>173;DruidDireWolfCasting.SeedLow=()=>173;DruidGrizzlyCasting.SeedLow=()=>173;
                Require(ClassCombat.Perform(p,ability,null,new(p.X+1,p.Y,p.Z),Core.TickCount),"Public learned beast cast");ClassCombat.ReleaseCast(p);ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
            }
            finally{DruidSpiritWolfCasting.SeedLow=spiritSeed;DruidDireWolfCasting.SeedLow=direSeed;DruidGrizzlyCasting.SeedLow=bearSeed;}
            var b=(p.AllFollowers??[]).OfType<SkeletonGuardian>().Single();Hold(b);
            Require(NecromancerEffects.ManaBalance256(p)==mana-cost&&b.Rank==hard+1&&SummonUpgradeSnapshots.State(b)is{HardRank:var captured}&&captured==hard,"Public beast payment/effective/hard capture");
            Log.Information("Druid current birth {Ability} hard{Hard}",ability,hard);
            var t=PoisonDaggerChecks.Target(p);t.CantWalk=true;t.AIObject?.AITimer.Stop();t.SetHits(20000);t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);
            try
            {
                var attack=Enter(b);Require(attack.Rank==hard+1,"Initial world entry birth rank");Hold(b);string birth=Birth(b);long order=BattleCommand.PetOrder(b);int maximum=SkillHealth.Maximum256(b);SkillHealth.SetBalance256(b,maximum/2+17);TimedEffects.PreventHealing(b,TimedEffects.Now);int life=SkillHealth.Balance256(b);
                foreach(var skill in Skills){ClassSkillChecks.Train(p,SkillMechanics.For(skill).TalentId,20);}
                p.Backpack.DropItem(p.FindItemOnLayer(Layer.Helm));ClassGear.Refresh(p);
                Require(Enter(b)==attack&&Birth(b)==birth&&SkillHealth.Balance256(b)==life&&SkillHealth.Maximum256(b)==maximum,"Training/gear changed born stats or next real AI attack entry");Upgrades(b,ability,hard);
                int ownerHard=20;
                if(hard==20)
                {
                    ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId,1);ownerHard=1;
                    Require(Enter(b)==attack&&Birth(b)==birth&&SkillHealth.Balance256(b)==life,"Respec changed born stats or next real AI entry");Upgrades(b,ability,hard);
                }
                if(b is DruidSpiritWolfBody&&hard==20){Require(NamedElements.Damage(t,b,100,"cold",enhance:false)==0,"Born cold immunity actual damage");}
                if(b is DruidGrizzlyBody bear)
                {
                    int draws=0;bool stunned=GrizzlyUpgrades.Stun(bear,t,false,_=>{draws++;return 0;});Require(stunned==(hard>=10)&&draws==(hard>=10?1:0),"Born stun actual effect/gate");CombatStuns.Clear(t);
                }
                Hold(b);Require(BattleCommand.PetOrder(b)==order&&SkillHealth.Maximum256(b)==maximum,"Birth order/maximum changed");
                rows.Add(new(p.Serial.Value,b.Serial.Value,ability,hard,ownerHard,life,maximum,NecromancerEffects.ManaBalance256(p),order,birth,Seed(b),SummonUpgradeSnapshots.State(b)));
                PersistentMinions.Suspend(p);p.Internalize();
            }
            finally{CombatStuns.Clear(t);t.Delete();}
        }
        File.WriteAllText(Manifest,Json(new Saved(rows.ToArray())));
        Log.Information("Druid snapshots:12 public births;hard9/10/19/20 gates,real AI attack entries after training/gear/respec and original saved actors staged");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2&&saved.Actors.Length==12,"Druid snapshot generation/count");
        foreach(var row in saved.Actors)
        {
            var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var b=(SkeletonGuardian)World.FindMobile((Serial)row.Body);
            Require(b.ControlMaster==p&&Birth(b)==row.Birth&&Seed(b)==row.Seed&&SummonUpgradeSnapshots.State(b)==row.Upgrade&&SkillHealth.Balance256(b)==row.Life
                &&SkillHealth.Maximum256(b)==row.Maximum&&NecromancerEffects.ManaBalance256(p)==row.Mana&&BattleCommand.PetOrder(b)==row.Order
                &&ClassCreation.Profiles.GetRank(p.Serial,SkillMechanics.For(row.Ability).TalentId)==row.OwnerHard,"Original Druid birth/upgrade/resources/order/RNG changed on reload: "+Json(new{Expected=row,Actual=new{Owner=b.ControlMaster?.Serial.Value,Birth=Birth(b),Seed=Seed(b),Upgrade=SummonUpgradeSnapshots.State(b),Life=SkillHealth.Balance256(b),Maximum=SkillHealth.Maximum256(b),Mana=NecromancerEffects.ManaBalance256(p),Order=BattleCommand.PetOrder(b),Hard=ClassCreation.Profiles.GetRank(p.Serial,SkillMechanics.For(row.Ability).TalentId)}}));Upgrades(b,row.Ability,row.Hard);
            Require(DruidBeastAttackStats.Values(b,p,row.Ability).Rank==row.Hard+1,"Saved birth rank lost on future attack");
        }
        File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
    }
}
