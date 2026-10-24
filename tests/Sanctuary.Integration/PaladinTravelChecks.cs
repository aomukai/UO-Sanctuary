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

public static class PaladinTravelChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(PaladinTravelChecks));
    private sealed record Saved(uint Vigor,uint Redemption,uint Ally,uint[] Corpses,Point3D Origin,PaladinAuraSave State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"paladin-travel-checks.json");
    public static void Configure(){Register("paladin-travel-check",Check);Register("paladin-travel-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Vigor resources/movement and Redemption saved corpse identity/owner recovery",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>
    {try{action();}catch(Exception e){_log.Error(e,"{Name}: failed",name);}});
    private static void Select(PlayerMobile actor,CombatAbility ability)=>Require(SkillBindings.Instance.Assign(actor,false,ability),"Travel aura selection");
    private static void FreezeHealing(PlayerMobile actor)=>TimedEffects.Poison(actor,actor,.000001,60000,TimedEffects.Now);
    private static Corpse Body(PlayerMobile owner,int offset=3,bool unknown=false)
    {
        BaseCreature victim=unknown?new GraveyardLich():new GraveyardSkeleton();victim.Frozen=true;victim.CantWalk=true;
        victim.MoveToWorld(new(owner.X+offset,owner.Y,owner.Z),owner.Map);victim.AIObject.AITimer.Stop();
        Corpse corpse=null;var previous=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(actor,container)=>
        {
            if(actor!=victim){previous?.Invoke(actor,container);return;}
            // Redemption needs a genuine retained corpse, not a random affix
            // or ordinary throwing-base test. Keep the real capture/reward
            // consumers with deterministic gold, as the Find Item fixture does.
            int seed=0;var type=actor.GetType();var difficulty=MonsterVariants.Difficulty(actor);
            if(CreatureGold.Policy(type,difficulty) is{Eligible:true})
            {
                while(seed<10000 && CreatureGold.Select(type,new Random(seed),difficulty)!="gld"){seed++;}
                Require(seed<10000,"Ordinary Redemption gold treasure seed");
            }
            CorpseSkillRules.Capture(actor,container);RewardLife.Process(actor,container,new Random(seed));
            corpse=container as Corpse;corpse?.DropItem(new Gold(7));
        };
        try{owner.DoHarmful(victim,true);victim.Damage(victim.Hits+1,owner);CombatDamage.Finish(victim,owner);}
        finally{ClassLootHooks.CreatureDied=previous;}
        Require(corpse is{Deleted:false} && ClassLoot.HasContents(corpse),$"Real corpse/loot fixture: corpse={corpse?.Serial}, alive={victim.Alive}, deleted={victim.Deleted}");return corpse;
    }
    private static void Lifecycle()
    {
        var actor=ClassSkillChecks.Player(DiabloClass.Paladin);actor.AddItem(new Backpack());
        Select(actor,CombatAbility.Vigor);actor.Kill();actor.Resurrect();Require(PaladinVigor.StaminaPercent(actor)==0,"Death retained Vigor");
        Select(actor,CombatAbility.Redemption);actor.Kill();actor.Resurrect();
        Require(PaladinAuras.Selected(actor)==CombatAbility.None && PaladinAuras.Effect(actor,CombatAbility.Redemption)==null,"Death retained Redemption selection/state");
        Select(actor,CombatAbility.Vigor);ClassCreation.Profiles.TryGet(actor.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(actor,profile);
        Require(PaladinVigor.VelocityPercent(actor)==0 && PaladinAuras.Selected(actor)==CombatAbility.None,"Respec retained Vigor");actor.Corpse?.Delete();actor.Delete();
    }
    private static void Check()
    {
        Lifecycle();var vigor=ClassSkillChecks.Player(DiabloClass.Paladin);vigor.MoveToWorld(ClassSkillChecks.Corridor(vigor,5720,1950),vigor.Map);
        var redeemer=ClassSkillChecks.Player(DiabloClass.Paladin);redeemer.MoveToWorld(new(vigor.X+1,vigor.Y,vigor.Z),vigor.Map);
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.MoveToWorld(new(vigor.X+2,vigor.Y,vigor.Z),vigor.Map);
        var pet=new GraveyardSkeleton{Frozen=true,CantWalk=true};pet.SetStam(100);pet.MoveToWorld(new(vigor.X+4,vigor.Y,vigor.Z),vigor.Map);pet.AIObject.AITimer.Stop();Require(pet.SetControlMaster(ally),"Vigor pet control");
        int baseMax=ally.StamMax;ally.Stam=10;vigor.Stam=10;
        Require(SkillMechanics.Values(vigor,CombatAbility.Vigor).Eval("aurastatcalc1")==50
            && SkillMechanics.Values(vigor,CombatAbility.Vigor,20).Eval("aurastatcalc2")==525
            && SkillMechanics.Values(vigor,CombatAbility.Vigor).Eval("aurastatcalc3")==13
            && SkillMechanics.Values(vigor,CombatAbility.Vigor,20).Eval("aurastatcalc3")==43,"Vigor source rank values");
        vigor.Mana=0;Select(vigor,CombatAbility.Vigor);
        Require(ally.StamMax==baseMax*150/100 && ally.Stam==10 && vigor.Mana==0 && pet.StamMax==150,"Player/pet stamina cap, zero mana or free refill");
        Require(PaladinAuras.Effect(ally,CombatAbility.Vigor) is{StaminaPercent:50,VelocityPercent:13,Percent:50}
            && ClassCombatHooks.WalkingDelay(ally,400)==EquipmentBalance.RunDelay(400,0,13)
            && ClassCombatHooks.RunningDelay(ally,200)==EquipmentBalance.RunDelay(200,EquipmentBalance.RunPenalty(ally),13),"Vigor native player speed consumers");
        Require(Math.Abs(ClassGearHooks.MovementDelay(pet,.24)-.24*100/113)<.000001
            && Math.Abs(Mobile.StamRegenRateHandler(ally).TotalSeconds-5.0/1.5)<.00001,"Native pet movement/player recovery consumers");
        var necro=ClassSkillChecks.Player();var golem=new NecromancerMinion();golem.Prepare(necro,CombatAbility.ClayGolem);golem.SetControlMaster(necro);golem.MoveToWorld(new(vigor.X+3,vigor.Y,vigor.Z),vigor.Map);golem.AIObject.AITimer.Stop();
        var prison=BoneBarrierSegment.Spawn(necro,CombatAbility.BonePrison,new(vigor.X+6,vigor.Y,vigor.Z),SkillMechanics.Values(necro,CombatAbility.BonePrison),null);
        var wall=BoneBarrierSegment.Spawn(necro,CombatAbility.BoneWall,new(vigor.X+7,vigor.Y,vigor.Z),SkillMechanics.Values(necro,CombatAbility.BoneWall),null);
        Select(vigor,CombatAbility.Vigor);double expected=.5*(100+golem.VelocityPercent)/(113+golem.VelocityPercent);
        Require(Math.Abs(ClassGearHooks.MovementDelay(golem,.5)-expected)<.000001,"Vigor compounded Golem Mastery velocity");
        Require(PaladinAuras.Effect(prison,CombatAbility.Vigor)==null && PaladinAuras.Effect(wall,CombatAbility.Vigor)==null,"Pinned MonStats noAura flag allowed a Bone Prison or Bone Wall buff");
        prison.Delete();wall.Delete();golem.Delete();necro.Delete();
        ally.Stam=ally.StamMax;ally.MoveToWorld(new(vigor.X+45,vigor.Y,vigor.Z),vigor.Map);
        var values=SkillMechanics.Values(redeemer,CombatAbility.Redemption);
        Require(values.Eval("calc1")==23 && values.Eval("calc2")==25 && values.Mana256==0
            && SkillMechanics.Values(redeemer,CombatAbility.Redemption,20).Eval("calc1")==85
            && SkillMechanics.Values(redeemer,CombatAbility.Redemption,20).Eval("calc3")==120,"Redemption pinned chance/gain/cost");
        var body=Body(redeemer);var metadata=CorpseSkillRules.Profiles.Find(body);
        Require(metadata is{Source:"skeleton1",Selectable:true,Owned:false} && (body.Owner==null || body.Owner.Deleted),"Death-time corpse species snapshot");
        redeemer.Hits=redeemer.HitsMax-100;redeemer.Mana=0;int hp=redeemer.Hits;
        Require(!PaladinRedemption.Redeem(redeemer,body,values,23) && !body.SanctuaryConsumed,"Chance boundary changed corpse");
        Require(PaladinRedemption.Redeem(redeemer,body,values,22) && redeemer.Hits==hp+25 && redeemer.Mana==25
            && body.SanctuaryConsumed && !body.Deleted && ClassLoot.HasContents(body)
            && !PaladinRedemption.Redeem(redeemer,body,values,0),"Owner recovery, single consumption or retained loot");
        var unknown=Body(redeemer,4,true);Require(!PaladinRedemption.Redeem(redeemer,unknown,values,0) && CorpseSkillRules.Profiles.Find(unknown).Source==null,"Unmapped species was invented");unknown.Delete();body.Delete();
        var rip=Body(redeemer);rip.SanctuaryRestInPeace=true;Require(!PaladinRedemption.Redeem(redeemer,rip,values,0),"Rest in Peace corpse consumed");rip.Delete();
        var full=Body(redeemer);redeemer.Hits=redeemer.HitsMax;redeemer.Mana=redeemer.ManaMax;
        Require(PaladinRedemption.Redeem(redeemer,full,values,0) && full.SanctuaryConsumed,"Full resources incorrectly protected corpse");full.Delete();
        var timed=Body(redeemer);FreezeHealing(redeemer);redeemer.Hits=redeemer.HitsMax-100;hp=redeemer.Hits;
        var oldRoll=PaladinRedemption.RollPercent;PaladinRedemption.RollPercent=()=>0;Select(redeemer,CombatAbility.Redemption);
        Require(!timed.SanctuaryConsumed && PaladinAuras.Effect(ally,CombatAbility.Redemption)==null,"Redemption activated immediately or spread to ally");
        Later(1400,"paladin-travel-result",()=>Require(!timed.SanctuaryConsumed && redeemer.Hits==hp,"Redemption first pulse ran early"));
        Later(2300,"paladin-travel-result",()=>
        {
            PaladinRedemption.RollPercent=oldRoll;
            Require(timed.SanctuaryConsumed && redeemer.Hits==hp+25 && ally.StamMax==baseMax && ally.Stam<=baseMax,"Periodic owner recovery or Vigor expiry/cap clamp");timed.Delete();
        });
        Later(6100,"paladin-travel-result",()=>
        {
            Require(vigor.Stam>10,"Actual native Vigor stamina recovery did not run");pet.Delete();
            ally.MoveToWorld(new(vigor.X+2,vigor.Y,vigor.Z),vigor.Map);Select(vigor,CombatAbility.Vigor);Seed(vigor,redeemer,ally);
            _log.Information("paladin-travel-result: passed");
        });
    }
    private static void Seed(PlayerMobile vigor,PlayerMobile redeemer,PlayerMobile ally)
    {
        var snapshot=PaladinAuras.Snapshot();uint[] ids=[vigor.Serial.Value,redeemer.Serial.Value];var at=TimedEffects.Now.AddMinutes(5);
        snapshot=snapshot with{Emitters=snapshot.Emitters.Select(x=>ids.Contains(x.Owner)?x with{NextUtc=at}:x).ToArray(),
            Leases=snapshot.Leases.Select(x=>ids.Contains(x.Source)?x with{UntilUtc=at.AddMilliseconds(40)}:x).ToArray()};PaladinAuras.Restore(snapshot);
        var first=Body(redeemer,3);var second=Body(redeemer,4);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(vigor.Serial.Value,redeemer.Serial.Value,ally.Serial.Value,[first.Serial.Value,second.Serial.Value],vigor.Location,snapshot)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var snapshot=PaladinAuras.Snapshot();uint[] ids=[saved.Vigor,saved.Redemption];
        PaladinAuraSave Scope(PaladinAuraSave state)=>new(state.Emitters.Where(x=>ids.Contains(x.Owner)).ToArray(),state.Leases.Where(x=>ids.Contains(x.Source)).ToArray());
        Require(JsonSerializer.Serialize(Scope(snapshot))==JsonSerializer.Serialize(Scope(saved.State)),"Saved Vigor/Redemption emitter or secondary values changed");
        var vigor=(PlayerMobile)World.FindMobile((Serial)saved.Vigor);var redeemer=(PlayerMobile)World.FindMobile((Serial)saved.Redemption);var ally=(PlayerMobile)World.FindMobile((Serial)saved.Ally);
        vigor.MoveToWorld(saved.Origin,Map.Trammel);redeemer.MoveToWorld(new(saved.Origin.X+1,saved.Origin.Y,saved.Origin.Z),Map.Trammel);ally.MoveToWorld(new(saved.Origin.X+2,saved.Origin.Y,saved.Origin.Z),Map.Trammel);
        var corpses=saved.Corpses.Select(x=>(Corpse)World.FindItem((Serial)x)).ToArray();
        Require(corpses.All(x=>CorpseSkillRules.Profiles.Find(x) is{Source:"skeleton1",Selectable:true,Owned:false}),"Saved death-time source metadata missing");
        var corpse=corpses.First(x=>!x.SanctuaryConsumed);corpse.MoveToWorld(new(redeemer.X+3,redeemer.Y,redeemer.Z),redeemer.Map);
        foreach(var other in corpses.Where(x=>x!=corpse && !x.SanctuaryConsumed)){other.MoveToWorld(new(redeemer.X+30,redeemer.Y,redeemer.Z),redeemer.Map);}
        FreezeHealing(redeemer);redeemer.Hits=redeemer.HitsMax-100;int hp=redeemer.Hits;var roll=PaladinRedemption.RollPercent;PaladinRedemption.RollPercent=()=>0;
        PaladinAuras.Clear(ally);var now=TimedEffects.Now;
        PaladinAuras.Restore(PaladinAuras.Snapshot() with{Emitters=snapshot.Emitters.Select(x=>ids.Contains(x.Owner)?x with{NextUtc=now}:x).ToArray()});
        Later(120,"paladin-travel-reload-result",()=>
        {
            try
            {
                Require(PaladinAuras.Effect(ally,CombatAbility.Vigor) is{StaminaPercent:50,VelocityPercent:13} lease && lease.UntilUtc<now.AddSeconds(3)
                    && corpse.SanctuaryConsumed && redeemer.Hits==hp+25 && ClassLoot.HasContents(corpse),"Restored real aura/resource/corpse consumption failed");
                PaladinAuras.Restore(snapshot);_log.Information("paladin-travel-reload-result: passed");
            }
            finally{PaladinRedemption.RollPercent=roll;}
        });
    }
}
