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

public static class SpiritWolfBodyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SpiritWolfBodyChecks));
    private sealed record Saved(uint Owner,uint Wolf,SpiritWolfBodyState State,int Life256,int Maximum256,int Regeneration256,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"spirit-wolf-body-fixtures.json");
    private static int _scene;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("spirit-wolf-body-check",Check);Register("spirit-wolf-body-reload-check",Reload);}
    private static void Register(string command,Action action)=>ConsoleInputHandler.RegisterCommand(command,"Prepared saved wolf life/ownership; AI and casting pending",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Command}: passed",command);}catch(Exception e){Log.Error(e,"{Command}: failed",command);}}));
    private static void Train(PlayerMobile p,string id)
    {
        Require(ClassCreation.Profiles.TryGet(p.Serial,out var profile) && TalentCatalog.TryGet(profile.Class,id,out _),"Fixture talent");
        TalentCatalog.TryGet(profile.Class,id,out var definition);
        foreach(var prerequisite in definition.Prerequisites){Train(p,prerequisite);}
        if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="SpiritWolfBodyFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        Train(p,"druid.summondirewolf");FixtureSpace.Corridor(p,7100,24,1800+32*_scene++);return p;
    }
    private static void End(PlayerMobile p){PersistentMinions.Dismiss(p);p.Delete();}
    private static PlayerMobile Supplier()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="WolfOrdersSupplier"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(p,99);Train(p,"barbarian.battleorders");return p;
    }
    private static Point3D Point(PlayerMobile p)=>new(p.X+1,p.Y,p.Z);
    private static void Snapshots(DruidSpiritWolfBody wolf)
    {
        var good=wolf.Prepared;string before=wolf.SavedText;
        var bad=new[]{good with{Snapshot=null},good with{Owner=0},good with{Profile="unknown"},
            good with{NextRecoveryUtc=DateTime.SpecifyKind(good.NextRecoveryUtc,DateTimeKind.Local)},
            good with{Snapshot=good.Snapshot with{Birth=good.Snapshot.Birth with{Life256=1}}},
            good with{Snapshot=good.Snapshot with{Birth=good.Snapshot.Birth with{ColdSunder=17}}},
            good with{Snapshot=good.Snapshot with{DireRank=256}}};
        foreach(var state in bad)
        {bool refused=false;try{wolf.RestorePrepared(state);}catch(InvalidDataException){refused=true;}Require(refused&&wolf.SavedText==before,"Invalid restore mutated saved wolf");}
        wolf.RestorePrepared(JsonSerializer.Deserialize<SpiritWolfBodyState>(before));Require(wolf.SavedText==before,"Immutable JSON snapshot round trip");
    }
    private static void LifeAndStats()
    {
        var p=Fresh();var supplier=Supplier();var at=TimedEffects.Now.AddHours(1);var wolf=DruidSpiritWolfBody.CreatePrepared(p,Point(p),at);
        var inner=AmazonInnerSight.Snapshot();
        try
        {
            Require(wolf is{Blessed:false,BleedImmune:false,PoisonImmune:null,CanRegenHits:false,CanRegenMana:false,CanRegenStam:false,ControlSlots:0}
                && wolf.ControlMaster==p && wolf.SummonClass==DiabloClass.Druid && wolf.Permitted(p)&&wolf.CanBeDamaged(),"Wolf inherited skeleton/Raven immunity or ownership");
            Require(wolf.SourceLife256==41600 && wolf.HitsMax==163 && wolf.Hits==163 && SkillHealth.Balance256(wolf)==41600
                && SkillHealth.Maximum256(wolf)==41600 && SkillHealth.Full(wolf),"162.5 source maximum rounded to native ceiling");
            Require(BattleOrders.Regeneration256(wolf)==32 && wolf.FireResistance==5 && wolf.ColdResistance==5
                && wolf.PoisonResistance==5 && wolf.EnergyResistance==5 && wolf.PhysicalResistance==0,"Wolf source regeneration/resistances");
            Require(CreatureProfiles.Definition(wolf).Reference=="spiritwolf" && SourceAuraTargets.Eligible(wolf),"Source aura species identity");
            Require(!GemCombat.Undead(wolf) && !NamedCombat.Demon(wolf) && GemCombat.Drain(wolf)==100
                && CreatureProfiles.For(wolf).Traits is{Size:2,ChillEffectiveness:50,BlockChance:0}
                && CreatureProfiles.For(wolf).HitRecovery is{Enabled:true,Frames:7,Speed:256},"Wolf inherited skeleton species/drain or lost source footprint/recovery metadata");
            int basis=wolf.CombatProfile.Defense;
            Require(ClassStats.Defense(wolf)==ClassStats.ScaleDefense(basis,57),"Copied armor passive not in defense percent pool");
            BarbarianShout.Apply(supplier,wolf,1,TimedEffects.Now);int shout=BarbarianShout.Defense(wolf);
            Require(shout>0 && ClassStats.Defense(wolf)==ClassStats.ScaleDefense(basis,57+shout),$"Armor/Shout pool: basis={basis},own={wolf.SourceDefensePercent},shout={shout},actual={ClassStats.Defense(wolf)},state={BarbarianShout.State(wolf)}");
            AmazonInnerSight.Restore(inner.Append(new InnerSightState(wolf.Serial.Value,p.Serial.Value,1,40,TimedEffects.Now.AddHours(1))).ToArray());
            Require(ClassStats.Defense(wolf)==AmazonInnerSight.DefenseValue(basis,57+shout,40),"Inner Sight did not act on raw armor before the shared percent pool");
            SkillHealth.SetBalance256(wolf,20000);wolf.AdvanceRecovery(at.AddMilliseconds(40),true);
            Require(SkillHealth.Balance256(wolf)==20032,"One source frame did not heal32FP");
            wolf.AdvanceRecovery(at.AddMilliseconds(80),true);Require(SkillHealth.Balance256(wolf)==20064,"FP recovery fraction was lost");
            SkillHealth.SetBalance256(wolf,41599);wolf.AdvanceRecovery(at.AddMilliseconds(120),true);
            Require(SkillHealth.Balance256(wolf)==41600 && SkillHealth.Heal(wolf,1000)==0,"Recovery exceeded fractional maximum");
            SkillHealth.SetBalance256(wolf,20000);int before=SkillHealth.Balance256(wolf);
            BattleOrders.Apply(supplier,wolf,1,TimedEffects.Now);int maximum=wolf.MaximumLife256;
            // ComputeStatPercentage uses a double ratio before truncation:
            // 41600*(35/100.0) truncates14559, one FP below exact rational arithmetic.
            Require(maximum==56159 && wolf.HitsMax==220 && SkillHealth.Balance256(wolf)==BattleOrders.Rescale256(before,41600,56159),$"Orders max/ratio: source={wolf.SourceLife256},maximum={maximum},native={wolf.HitsMax},seed={wolf.HitsMaxSeed},life={SkillHealth.Balance256(wolf)},before={before},state={BattleOrders.State(wolf)}");
            Require(BattleOrders.Regeneration256(wolf)==54,"Post-maximum source regen used skeleton row or rounded-up native maximum");
            before=SkillHealth.Balance256(wolf);BattleOrders.Clear(wolf);
            Require(wolf.MaximumLife256==41600 && wolf.HitsMax==163 && SkillHealth.Balance256(wolf)==BattleOrders.Rescale256(before,maximum,41600)
                && BattleOrders.Regeneration256(wolf)==40,"Orders removal source max/regen recomputation");
            var captured=wolf.Prepared.Snapshot;var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
            Require(p.EquipItem(helm),"Real wolf owner Tarnhelm equip");ClassGear.Refresh(p);
            Require(wolf.Prepared.Snapshot==captured && DruidSpiritWolfStats.Capture(p).Birth.Rank==2,"Owner gear rewrote saved birth or fresh capture ignored gear");
            var target=new GraveyardZombie{CantWalk=true};target.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
            int life=SkillHealth.Balance256(target);wolf.Combatant=target;wolf.BeginMelee(target);wolf.OnThink();
            Require(wolf.Combatant==null && SkillHealth.Balance256(target)==life,"Prepared wolf leaked native skeleton combat");target.Delete();Snapshots(wolf);
        }
        finally{AmazonInnerSight.Restore(inner);End(p);supplier.Delete();}
    }
    private static void Lifecycle()
    {
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var wolf=DruidSpiritWolfBody.CreatePrepared(p,Point(p),at);var facets=SummonDifficulties.Facets();
        try
        {
            SkillHealth.SetBalance256(wolf,20000);var before=wolf.Prepared.Snapshot;
            wolf.AdvanceRecovery(at.AddMinutes(5),false);
            Require(wolf.Map==Map.Internal && SkillHealth.Balance256(wolf)==20000 && wolf.Prepared.Snapshot==before,"Offline frames healed or rewrote birth");
            var next=wolf.Prepared.NextRecoveryUtc;wolf.AdvanceRecovery(next,true);
            Require(wolf.Map==p.Map && SkillHealth.Balance256(wolf)==20032,"Resume replayed offline healing or failed ownership placement");
            Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Park wolf wardrobe");
            int life=SkillHealth.Balance256(wolf);wolf.AdvanceRecovery(wolf.Prepared.NextRecoveryUtc.AddMinutes(5),true);
            Require(wolf.Map==Map.Internal && SkillHealth.Balance256(wolf)==life && wolf.Permitted(p),"Inactive history healed or forgot wolf permission");
            Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Druid,out _,out _),"Return learned wolf history");
            wolf.AdvanceRecovery(wolf.Prepared.NextRecoveryUtc,true);Require(wolf.Map==p.Map,"Wardrobe wolf did not resume");
            var other=DruidSpiritWolfBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),at);other.SetControlMaster(null);other.AdvanceRecovery(at.AddMilliseconds(40),true);
            Require(other.Deleted,"Orphan prepared wolf survived");
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
            life=SkillHealth.Balance256(wolf);int maximum=wolf.MaximumLife256;var birth=wolf.Prepared.Snapshot;
            p.MoveToWorld(p.Location,Map.Felucca);
            Require(wolf.Prepared.Difficulty==MonsterDifficulty.Hell && wolf.Prepared.Snapshot==DruidSpiritWolfStats.Rescale(birth,MonsterDifficulty.Hell)
                && SkillHealth.Balance256(wolf)==SummonDifficulties.Life256(life,maximum,wolf.MaximumLife256),"Owner facet rescale did not preserve birth ranks/life ratio");
        }
        finally{SummonDifficulties.SetFacets(facets);End(p);}
    }
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.SummonSpiritWolf),"Learned summon route missing");LifeAndStats();Lifecycle();
        var p=Fresh();var wolf=DruidSpiritWolfBody.CreatePrepared(p,Point(p),TimedEffects.Now.AddDays(2));
        var supplier=Supplier();SkillHealth.SetBalance256(wolf,20007);BattleOrders.Apply(supplier,wolf,1,TimedEffects.Now.AddHours(1));supplier.Delete();PersistentMinions.Suspend(p);Snapshots(wolf);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,wolf.Serial.Value,wolf.Prepared,SkillHealth.Balance256(wolf),wolf.MaximumLife256,BattleOrders.Regeneration256(wolf),p.Map.MapID,p.Location)));
        Log.Information("Prepared Spirit Wolf saved backend: fractional life/regen, shared armor pool, resistance/vulnerability, immutable gear capture, offline/wardrobe/facet/orphan controls pass; individual AI, attack and casting pending");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        var wolf=World.FindMobile((Serial)saved.Wolf) as DruidSpiritWolfBody;
        Require(wolf is{Deleted:false,Alive:true} && wolf.ControlMaster==p && wolf.Map==Map.Internal && wolf.Permitted(p),"Saved wolf ownership/history/parking");
        Require(wolf.SavedText==JsonSerializer.Serialize(saved.State) && SkillHealth.Balance256(wolf)==saved.Life256
            && wolf.MaximumLife256==saved.Maximum256 && BattleOrders.Regeneration256(wolf)==saved.Regeneration256,"Reload changed snapshot/fractional life/Orders/regen");Snapshots(wolf);
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}
        var next=wolf.Prepared.NextRecoveryUtc;wolf.AdvanceRecovery(next,false);
        Require(SkillHealth.Balance256(wolf)==saved.Life256,"Reload replayed offline recovery");
        wolf.AdvanceRecovery(wolf.Prepared.NextRecoveryUtc,true);
        Require(wolf.Map==p.Map && SkillHealth.Balance256(wolf)==Math.Min(saved.Maximum256,saved.Life256+saved.Regeneration256),"Reload active frame recovery");
        PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{State=wolf.Prepared,Life256=SkillHealth.Balance256(wolf)}));
    }
}
