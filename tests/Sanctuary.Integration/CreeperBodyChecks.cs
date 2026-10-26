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

public static class CreeperBodyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CreeperBodyChecks));
    private sealed record Saved(uint Owner,uint Creeper,CreeperBodyState State,int Life,int Maximum,int Regeneration,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"creeper-body-fixtures.json");
    private static int _scene;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("creeper-body-check",Check);Register("creeper-body-reload-check",Reload);}
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Prepared saved Creeper life/ownership",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Train(PlayerMobile p,string id)
    {
        ClassCreation.Profiles.TryGet(p.Serial,out var profile);Require(TalentCatalog.TryGet(profile.Class,id,out var definition),"Fixture talent");
        foreach(var prior in definition.Prerequisites){Train(p,prior);}
        if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="CreeperBodyFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);Train(p,"druid.poisoncreeper");Train(p,"druid.rabies");
        FixtureSpace.Corridor(p,7050,24,1200+32*_scene++);return p;
    }
    private static PlayerMobile Supplier()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="CreeperBuffFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(p,99);Train(p,"barbarian.battleorders");return p;
    }
    private static Point3D Point(PlayerMobile p)=>new(p.X+1,p.Y,p.Z);
    private static void End(PlayerMobile p){PersistentMinions.Dismiss(p);p.Delete();}
    private static void Malformed(DruidCreeperBody body)
    {
        var good=body.Prepared;string text=body.SavedText;
        foreach(var bad in new[]{good with{Owner=0},good with{Snapshot=null},good with{Profile="unknown"},
            good with{NextRecoveryUtc=DateTime.SpecifyKind(good.NextRecoveryUtc,DateTimeKind.Local)},
            good with{Snapshot=good.Snapshot with{OwnerLevel=0}},good with{Snapshot=good.Snapshot with{RabiesHardRank=21}},
            good with{Snapshot=good.Snapshot with{Birth=good.Snapshot.Birth with{Life256=1}}},
            good with{Snapshot=good.Snapshot with{Birth=good.Snapshot.Birth with{Pierce=1}}}})
        {bool refused=false;try{body.RestorePrepared(bad);}catch(InvalidDataException){refused=true;}Require(refused && body.SavedText==text,"Invalid Creeper state changed saved bytes");}
    }
    private static void LifeAndBuffs()
    {
        var p=Fresh();var supplier=Supplier();var at=TimedEffects.Now.AddHours(1);var body=DruidCreeperBody.CreatePrepared(p,Point(p),at);var inner=AmazonInnerSight.Snapshot();
        try
        {
            Require(body.ControlMaster==p && body.Permitted(p) && body.SummonClass==DiabloClass.Druid && body.CanBeDamaged()
                && body is{Blessed:false,BleedImmune:false,PoisonImmune:null,CanRegenHits:false,CanRegenMana:false,CanRegenStam:false,ControlSlots:0},"Creeper inherited skeleton/Raven immunity or owner routing");
            Require(body.Prepared.Snapshot is{OwnerLevel:99,RabiesHardRank:1,Birth.Rank:1,Birth.Level:75,Birth.Life256:12800,Birth.Defense:25,Birth.Attack:0,Birth.Regeneration256:9}
                && ClassStats.Level(body)==75 && CreatureProfiles.NativeAttack(body)==0 && ClassStats.Defense(body)==25,"Creeper copied Raven/owner level-based defense or AR");
            Require(body.MaximumLife256==12800 && SkillHealth.Balance256(body)==12800 && body.HitsMax==50 && BattleOrders.Regeneration256(body)==9,"Creeper noRatio life/regen");
            Require(CreatureProfiles.Definition(body).Reference=="plaguepoppy" && !GemCombat.Undead(body) && !NamedCombat.Demon(body)
                && GemCombat.Drain(body)==0 && CreatureProfiles.For(body).Traits is{Size:0,ChillEffectiveness:50,BlockChance:0}
                && !CreatureProfiles.For(body).HitRecovery.Enabled && !SourceAuraTargets.Eligible(body),"Creeper source species/size/drain/GH/aura mapping");
            Require(body.PhysicalResistance==0 && body.FireResistance==0 && body.ColdResistance==0 && body.EnergyResistance==0 && body.PoisonResistance==0,"Skeleton or Summon Resist leaked into Creeper");
            SkillHealth.SetBalance256(body,6407);body.AdvanceRecovery(at.AddMilliseconds(40),true);Require(SkillHealth.Balance256(body)==6416,"Creeper one-frame9FP recovery");
            body.AdvanceRecovery(at.AddMilliseconds(80),true);Require(SkillHealth.Balance256(body)==6425,"Creeper regeneration fraction lost");
            SkillHealth.SetBalance256(body,12799);body.AdvanceRecovery(at.AddMilliseconds(120),true);Require(SkillHealth.Balance256(body)==12800,"Creeper recovery exceeded source maximum");
            SkillHealth.SetBalance256(body,6407);int before=SkillHealth.Balance256(body);BattleOrders.Apply(supplier,body,1,TimedEffects.Now);
            Require(body.MaximumLife256==17280 && body.HitsMax==68 && SkillHealth.Balance256(body)==BattleOrders.Rescale256(before,12800,17280)
                && BattleOrders.Regeneration256(body)==12,"Creeper Orders exact maximum/proportion/regen");
            BattleOrders.Clear(body);Require(body.MaximumLife256==12800 && BattleOrders.Regeneration256(body)==9,"Creeper Orders removal");
            BarbarianShout.Apply(supplier,body,1,TimedEffects.Now);int percent=BarbarianShout.Defense(body);
            Require(percent>0 && ClassStats.Defense(body)==ClassStats.ScaleDefense(25,percent),"Creeper Shout raw defense");
            // Inject the stat only as a raw-defense consumer control. The
            // ordinary offensive aura filter correctly excludes isAtt0vines.
            AmazonInnerSight.Restore(inner.Append(new InnerSightState(body.Serial.Value,p.Serial.Value,1,20,TimedEffects.Now.AddHours(1))).ToArray());
            Require(ClassStats.Defense(body)==AmazonInnerSight.DefenseValue(25,percent,20),"Creeper Inner Sight/percent order");
            var birth=body.Prepared.Snapshot;var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
            Require(p.EquipItem(helm),"Creeper owner Tarnhelm");ClassGear.Refresh(p);var fresh=DruidCreeperStats.Capture(p);
            Require(body.Prepared.Snapshot==birth && fresh.RabiesHardRank==1 && fresh.Birth is{Rank:2,Life256:19200,Level:76,Regeneration256:9},"Immutable Creeper birth or fresh soft rank");
            var enemy=new GraveyardZombie{CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();
            int life=SkillHealth.Balance256(enemy);body.Combatant=enemy;body.BeginMelee(enemy);body.OnThink();
            Require(body.Combatant==null && SkillHealth.Balance256(enemy)==life,"Prepared Creeper leaked skeleton melee");enemy.Delete();Malformed(body);
        }
        finally{AmazonInnerSight.Restore(inner);End(p);supplier.Delete();}
    }
    private static void Lifecycle()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var body=DruidCreeperBody.CreatePrepared(p,Point(p),at);var facets=SummonDifficulties.Facets();
        try
        {
            SkillHealth.SetBalance256(body,6007);body.AdvanceRecovery(at.AddMinutes(5),false);
            Require(body.Map==Map.Internal && SkillHealth.Balance256(body)==6007,"Offline Creeper healed or remained active");
            body.AdvanceRecovery(body.Prepared.NextRecoveryUtc,true);Require(body.Map==p.Map && SkillHealth.Balance256(body)==6016,"Creeper resume replayed offline recovery");
            Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Creeper wardrobe park");int life=SkillHealth.Balance256(body);
            body.AdvanceRecovery(body.Prepared.NextRecoveryUtc.AddMinutes(5),true);Require(body.Map==Map.Internal && body.Permitted(p) && SkillHealth.Balance256(body)==life,"Inactive Creeper history healed or disappeared");
            Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Druid,out _,out _),"Creeper wardrobe restore");body.AdvanceRecovery(body.Prepared.NextRecoveryUtc,true);Require(body.Map==p.Map,"Creeper wardrobe resume");
            var orphan=DruidCreeperBody.CreatePrepared(p,Point(p),at);orphan.SetControlMaster(null);orphan.AdvanceRecovery(at.AddMilliseconds(40),true);Require(orphan.Deleted,"Orphan Creeper survived");
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
            life=SkillHealth.Balance256(body);var birth=body.Prepared.Snapshot;p.MoveToWorld(p.Location,Map.Felucca);
            Require(body.Prepared.Difficulty==MonsterDifficulty.Hell && body.Prepared.Snapshot==DruidCreeperStats.Rescale(birth,MonsterDifficulty.Hell)
                && body.MaximumLife256==12800 && SkillHealth.Balance256(body)==life,"Creeper facet invented a noRatio HP multiplier or changed injury");
            p.Kill();Require(body.Deleted,"Owner death retained Creeper");p.Corpse?.Delete();
        }
        finally{SummonDifficulties.SetFacets(facets);End(p);}
    }
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.PoisonCreeper),"Life preparation enabled learned Creeper");LifeAndBuffs();Lifecycle();
        var p=Fresh();var body=DruidCreeperBody.CreatePrepared(p,Point(p),TimedEffects.Now.AddDays(2));var supplier=Supplier();
        SkillHealth.SetBalance256(body,6007);BattleOrders.Apply(supplier,body,1,TimedEffects.Now.AddHours(1));supplier.Delete();PersistentMinions.Suspend(p);Malformed(body);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,body.Prepared,SkillHealth.Balance256(body),body.MaximumLife256,BattleOrders.Regeneration256(body),p.Map.MapID,p.Location)));
        Log.Information("Prepared Creeper body: noRatio50life/25defense/0AR/9FPregen, explicit75level, source traits, exact Orders/Shout/Inner Sight, immutable gear capture and offline/wardrobe/facet/orphan/death controls pass; VINE_BEAST, world AI/mats/casting remain pending");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=World.FindMobile((Serial)saved.Creeper) as DruidCreeperBody;
        Require(body is{Alive:true,Deleted:false} && body.ControlMaster==p && body.Permitted(p) && body.Map==Map.Internal,"Saved Creeper owner/history/parking");
        Require(body.SavedText==JsonSerializer.Serialize(saved.State) && SkillHealth.Balance256(body)==saved.Life && body.MaximumLife256==saved.Maximum
            && BattleOrders.Regeneration256(body)==saved.Regeneration,"Reload changed Creeper birth/fractional injury/Orders/regen");Malformed(body);
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}
        body.AdvanceRecovery(body.Prepared.NextRecoveryUtc,false);Require(SkillHealth.Balance256(body)==saved.Life,"Reload replayed offline Creeper healing");
        body.AdvanceRecovery(body.Prepared.NextRecoveryUtc,true);Require(body.Map==p.Map && SkillHealth.Balance256(body)==Math.Min(saved.Maximum,saved.Life+saved.Regeneration),"Reload Creeper active recovery");
        PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{State=body.Prepared,Life=SkillHealth.Balance256(body)}));
    }
}
