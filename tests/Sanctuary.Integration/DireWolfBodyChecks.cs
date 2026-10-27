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

public static class DireWolfBodyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DireWolfBodyChecks));
    private sealed record Saved(uint Owner,uint Wolf,DireWolfBodyState State,int Life256,int Map,Point3D Point,uint Eaten,uint Fresh,CorpseSkillRecord EatenRow,CorpseSkillRecord FreshRow);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dire-wolf-body-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("dire-wolf-body-check",Check);Register("dire-wolf-body-reload-check",Reload);}
    private static void Register(string command,Action action)=>ConsoleInputHandler.RegisterCommand(command,"Prepared Fenris body and approved type-independent corpse consumer",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Command}: passed",command);}catch(Exception e){Log.Error(e,"{Command}: failed",command);}}));
    private static void Train(PlayerMobile p,string id)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Druid,id,out var row),"Druid talent");
        foreach(var prerequisite in row.Prerequisites){Train(p,prerequisite);}
        if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Fresh()
    {
        var p=SpiritWolfWorldChecks.Fresh(-1408);p.Name="DireWolfBodyFixture";Train(p,"druid.summondirewolf");return p;
    }
    private static Corpse Corpse(PlayerMobile owner,bool skeletal,int offset)
    {
        BaseCreature victim=skeletal?new GraveyardSkeleton():new GraveyardZombie();victim.CantWalk=true;victim.NoKillAwards=true;
        victim.MoveToWorld(new(owner.X+offset,owner.Y,owner.Z),owner.Map);victim.AIObject.AITimer.Stop();Corpse corpse=null;
        var previous=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(actor,container)=>
        {
            if(actor!=victim){previous?.Invoke(actor,container);return;}
            CorpseSkillRules.Capture(actor,container);corpse=container as Corpse;corpse?.DropItem(new Gold(1));
        };
        try{owner.DoHarmful(victim,true);victim.Damage(victim.Hits+1,owner);CombatDamage.Finish(victim,owner);}
        finally{ClassLootHooks.CreatureDied=previous;}
        Require(corpse is{Deleted:false} && CorpseSkillRules.Profiles.Find(corpse)!=null,"Actual monster death lost its corpse metadata");
        corpse.BeginDecay(TimeSpan.FromHours(1));return corpse;
    }
    private static void Snapshots(DruidDireWolfBody wolf)
    {
        var good=wolf.Prepared;string before=wolf.SavedText;
        foreach(var state in new[]{good with{Snapshot=null},good with{Owner=0},good with{Profile="unknown"},
            good with{NextRecoveryUtc=DateTime.SpecifyKind(good.NextRecoveryUtc,DateTimeKind.Local)},
            good with{Snapshot=good.Snapshot with{Birth=good.Snapshot.Birth with{Life256=1}}},
            good with{Snapshot=good.Snapshot with{Birth=good.Snapshot.Birth with{PhysicalSunder=17}}},
            good with{Snapshot=good.Snapshot with{SpiritRank=256}}})
        {bool refused=false;try{wolf.RestorePrepared(state);}catch(InvalidDataException){refused=true;}Require(refused && wolf.SavedText==before,"Invalid restore changed the live/saved Dire Wolf");}
    }
    private static void Corpses(PlayerMobile owner,DruidDireWolfBody wolf)
    {
        int health=SkillHealth.Balance256(wolf),mana=NecromancerEffects.ManaBalance256(owner),calls=0;
        foreach(bool skeletal in new[]{true,false})
        {
            var corpse=Corpse(owner,skeletal,3);var original=CorpseSkillRules.Profiles.Find(corpse);
            // Type-independent consumption must not require a historical
            // source life snapshot, seed, material or selectable flag.
            CorpseSkillRules.Profiles.Store(original with{Selectable=false,Source=null,BaseLife=null});
            Require(!CorpseSkillRules.Usable(owner,corpse,10) && CorpseSkillRules.DireWolfUsableAt(owner,corpse,wolf,10),"Dire type exception leaked into another corpse skill or rejected a monster");
            var result=DruidDireWolfCorpses.Consume(wolf,corpse,()=>
            {
                calls++;Require(corpse.SanctuaryConsumed && corpse.Animated && CorpseSkillRules.NoSelect(corpse),"Rage was allocated before reservation/consumption");
                Require(!CorpseSkillRules.Reserve(corpse),"Competing consumer reserved the same corpse");return true;
            });
            Require(result is{Consumed:true,RageApplied:true} && corpse.GetAmount(typeof(Gold))==1
                && !DruidDireWolfCorpses.Consume(wolf,corpse,()=>{calls++;return true;}).Consumed,"Skeletal/fleshy corpse consumed twice or destroyed its loot");
            corpse.Delete();
        }
        Require(calls==2,"Duplicate corpse buff callbacks");
        var guarded=Corpse(owner,true,3);var row=CorpseSkillRules.Profiles.Find(guarded);
        foreach(var bad in new[]{row with{Owned=true},row with{Shattered=true},row with{NoSelect=true}})
        {CorpseSkillRules.Profiles.Store(bad);Require(!DruidDireWolfCorpses.Consume(wolf,guarded,()=>throw new InvalidOperationException("Ineligible corpse buffed")).Consumed && !guarded.SanctuaryConsumed,"Shared ownership/shatter/reservation restriction bypassed");}
        CorpseSkillRules.Profiles.Store(row);guarded.SanctuaryHarvested=true;
        Require(!DruidDireWolfCorpses.Consume(wolf,guarded,()=>true).Consumed,"Harvested corpse reused");guarded.SanctuaryHarvested=false;
        var oldMap=guarded.Map;guarded.MoveToWorld(guarded.Location,Map.Internal);
        Require(!DruidDireWolfCorpses.Consume(wolf,guarded,()=>true).Consumed,"Cross-map corpse consumed");guarded.MoveToWorld(guarded.Location,oldMap);
        var failed=DruidDireWolfCorpses.Consume(wolf,guarded,()=>false);
        Require(failed is{Consumed:true,RageApplied:false} && guarded.GetAmount(typeof(Gold))==1 && CorpseSkillRules.NoSelect(guarded),"Failed buff allocation restored corpse or lost loot");guarded.Delete();
        var empty=Corpse(owner,false,3);foreach(var item in empty.Items.ToArray()){item.Delete();}
        Require(DruidDireWolfCorpses.Consume(wolf,empty,()=>true).Consumed && empty.Deleted,"Empty eaten corpse did not use shared cleanup");
        Require(SkillHealth.Balance256(wolf)==health && NecromancerEffects.ManaBalance256(owner)==mana,"Consumption invented direct healing or mana writes");
    }
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.SummonDireWolf),"Learned Fenris handler missing");
        var p=Fresh();var at=TimedEffects.Now.AddHours(1);var wolf=DruidDireWolfBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),at);
        Require(wolf is{Blessed:false,BleedImmune:false,PoisonImmune:null,CanRegenHits:false,CanRegenMana:false,CanRegenStam:false,ControlSlots:0}
            && wolf.ControlMaster==p && wolf.Permitted(p) && wolf.CanBeDamaged(),"Dire ownership/vulnerability inherited skeleton immunity");
        Require(wolf.SourceLife256==88473 && wolf.MaximumLife256==88473 && wolf.HitsMax==346 && SkillHealth.Balance256(wolf)==88473
            && BattleOrders.Regeneration256(wolf)==54 && wolf.FireResistance==5 && wolf.PhysicalResistance==0,"Dire noRatio fractional life/regeneration/resistances");
        Require(CreatureProfiles.Definition(wolf).Reference=="fenris" && !GemCombat.Undead(wolf) && !NamedCombat.Demon(wolf)
            && GemCombat.Drain(wolf)==100 && !DruidSpiritWolfBody.ObservedChillImmune(wolf)
            && CreatureProfiles.For(wolf).Traits is{Size:2,ChillEffectiveness:50} && CreatureProfiles.For(wolf).HitRecovery is{Enabled:true,Frames:7,Speed:256},"Fenris identity/chill/recovery inherited skeleton or Spirit Wolf rules");
        Require(ClassStats.Defense(wolf)==ClassStats.ScaleDefense(wolf.CombatProfile.Defense,14),"Dire armor passive missing from shared defense pool");
        SkillHealth.SetBalance256(wolf,20007);wolf.AdvanceRecovery(at.AddMilliseconds(40),true);
        Require(SkillHealth.Balance256(wolf)==20061,"Dire recovery not fractional source54FP");
        var captured=wolf.Prepared.Snapshot;var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
        Require(p.EquipItem(helm),"Actual Tarnhelm equip");ClassGear.Refresh(p);
        Require(wolf.Prepared.Snapshot==captured && DruidDireWolfStats.Capture(p).Birth.Rank==2,"Gear rewrote birth or fresh capture ignored gear");
        var target=new GraveyardZombie{CantWalk=true};target.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();int life=SkillHealth.Balance256(target);
        wolf.Combatant=target;wolf.BeginMelee(target);wolf.OnThink();Require(wolf.Combatant==null && SkillHealth.Balance256(target)==life,"Prepared Dire leaked skeleton combat");target.Delete();
        Corpses(p,wolf);Snapshots(wolf);
        wolf.AdvanceRecovery(wolf.Prepared.NextRecoveryUtc.AddMinutes(5),false);
        Require(wolf.Map==Map.Internal && SkillHealth.Balance256(wolf)==20061,"Offline frames replayed healing");
        wolf.AdvanceRecovery(wolf.Prepared.NextRecoveryUtc,true);Require(wolf.Map==p.Map && SkillHealth.Balance256(wolf)==20115,"Resume replayed offline time");
        Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Park Druid history");
        wolf.AdvanceRecovery(wolf.Prepared.NextRecoveryUtc.AddMinutes(5),true);Require(wolf.Map==Map.Internal && SkillHealth.Balance256(wolf)==20115 && wolf.Permitted(p),"Inactive history healed or lost permission");
        Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Druid,out _,out _),"Restore Druid history");wolf.AdvanceRecovery(wolf.Prepared.NextRecoveryUtc,true);
        var facets=SummonDifficulties.Facets();
        try
        {
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
            int before=SkillHealth.Balance256(wolf),maximum=wolf.MaximumLife256;captured=wolf.Prepared.Snapshot;p.MoveToWorld(p.Location,Map.Felucca);
            Require(wolf.Prepared.Difficulty==MonsterDifficulty.Hell && wolf.Prepared.Snapshot==DruidDireWolfStats.Rescale(captured,MonsterDifficulty.Hell)
                && SkillHealth.Balance256(wolf)==SummonDifficulties.Life256(before,maximum,wolf.MaximumLife256),"Facet change lost captured ranks or life percentage");
        }
        finally{SummonDifficulties.SetFacets(facets);p.MoveToWorld(p.Location,Map.Trammel);}
        // This life-only component has no travelling world controller yet.
        // Use the shared suspend/resume placement after the facet control;
        // automatic cross-facet motion is a later world-controller check.
        PersistentMinions.Suspend(p);wolf.AdvanceRecovery(wolf.Prepared.NextRecoveryUtc,true);
        var orphan=DruidDireWolfBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),at);orphan.SetControlMaster(null);orphan.AdvanceRecovery(at.AddMilliseconds(40),true);Require(orphan.Deleted,"Orphan Dire remained active");
        var eaten=Corpse(p,true,3);var fresh=Corpse(p,false,4);Require(DruidDireWolfCorpses.Consume(wolf,eaten,()=>true).Consumed,"Stage consumed corpse");
        PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,wolf.Serial.Value,wolf.Prepared,SkillHealth.Balance256(wolf),p.Map.MapID,p.Location,eaten.Serial.Value,fresh.Serial.Value,CorpseSkillRules.Profiles.Find(eaten),CorpseSkillRules.Profiles.Find(fresh))));
        Log.Information("Dire body: saved vulnerable fractional life/54FP recovery, immutable gear, facet life ratio, offline/history/orphan controls and actual skeletal/fleshy one-time loot-safe feeding pass; rage callback is a service, automatic AI/combat/casting remain unfinished");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var wolf=(DruidDireWolfBody)World.FindMobile((Serial)saved.Wolf);
        Require(wolf is{Alive:true,Deleted:false} && wolf.ControlMaster==p && wolf.Map==Map.Internal && wolf.Permitted(p)
            && wolf.SavedText==JsonSerializer.Serialize(saved.State) && SkillHealth.Balance256(wolf)==saved.Life256,"Saved Dire state/ownership/life changed");Snapshots(wolf);
        var eaten=(Corpse)World.FindItem((Serial)saved.Eaten);var fresh=(Corpse)World.FindItem((Serial)saved.Fresh);
        Require(CorpseSkillRules.Profiles.Find(eaten)==saved.EatenRow && CorpseSkillRules.Profiles.Find(fresh)==saved.FreshRow && eaten.SanctuaryConsumed && !fresh.SanctuaryConsumed,"Saved shared corpse state changed");
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}
        wolf.AdvanceRecovery(wolf.Prepared.NextRecoveryUtc,false);Require(SkillHealth.Balance256(wolf)==saved.Life256,"Reload replayed healing");
        wolf.AdvanceRecovery(wolf.Prepared.NextRecoveryUtc,true);Require(wolf.Map==p.Map && SkillHealth.Balance256(wolf)==saved.Life256+54,"Reload one active recovery frame");
        Require(!DruidDireWolfCorpses.Consume(wolf,eaten,()=>throw new InvalidOperationException("Saved eaten corpse buffed again")).Consumed,"Reload revived a consumed corpse");
        Require(DruidDireWolfCorpses.Consume(wolf,fresh,()=>true).Consumed && fresh.GetAmount(typeof(Gold))==1,"Reload fresh corpse feeding/loot failed");fresh.Delete();fresh=Corpse(p,false,4);
        PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{State=wolf.Prepared,Life256=SkillHealth.Balance256(wolf),Fresh=fresh.Serial.Value,FreshRow=CorpseSkillRules.Profiles.Find(fresh)}));
    }
}
