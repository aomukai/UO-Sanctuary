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

public static class WeaponHandChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WeaponHandChecks));
    private sealed record Payload(int Attack,int Bonus,int Minimum,int Maximum,double Fire,double Poison,int Leech,int Knock,int Ias);
    private sealed record Saved(uint Owner,uint Primary,uint Secondary,uint Helm,uint Gloves,Point3D Origin,int Map,Payload First,Payload Second);
    private sealed class Rolls(int value):Random{public override int Next(int maximum)=>Math.Clamp(value,0,maximum-1);}
    private static string Manifest=>Path.Join(Core.BaseDirectory,"weapon-hands.json");
    public static void Configure(){Register("weapon-hands-check",Check);Register("weapon-hands-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Selected weapon hand and damage/global property contracts",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()
    {
        var actor=new PlayerMobile{Body=0x190,Name="WeaponHandsFixture",Player=true};actor.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(actor,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(actor,99);
        while(actor.RawStr<250){Require(ClassCreation.Profiles.TrySpendStat(actor,CharacterStat.Strength),"Strength allocation");}
        while(actor.RawDex<180){Require(ClassCreation.Profiles.TrySpendStat(actor,CharacterStat.Dexterity),"Dexterity allocation");}
        FixtureSpace.Place(actor,new(6100,1800,0),Map.Trammel);actor.Hits=actor.HitsMax;return actor;
    }
    private static void Train(PlayerMobile owner,CombatAbility skill,int rank)
    {string id=SkillMechanics.For(skill).TalentId;while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}}
    private static Item Named(string key)=>NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true);
    private static Payload Snapshot(PlayerMobile owner,BaseWeapon weapon)
    {
        using var hand=WeaponHands.Use(owner,weapon);Require(CombatTiming.Equipped(owner)==weapon,"Shared equipped consumer ignored selected hand");
        var range=ClassStats.PhysicalDamageRange(owner);var damage=GearEffects.Damage(owner,0);
        return new(ClassStats.AttackRating(owner),ClassStats.DamageBonus(owner,weapon),range.Min,range.Max,damage.Fire,damage.Poison,ClassGear.Bonus(owner,"life-steal%"),ClassGear.Bonus(owner,"knock"),CombatTiming.Ias(owner));
    }
    private static void Metadata()
    {
        foreach(string code in new[]{"att","att%","dmg%","dmg-min","dmg-max","swing1","deadly","pierce","hit-skill","dmg-fire","lifesteal","life-steal%","mana-steal%","dmg-demon%","normal-damage","knock"}){Require(WeaponHands.DamageRelated(code),"Missing source damage-related property "+code);}
        foreach(string code in new[]{"str","dex","enr","hp","mana","res-fire","res-all","allskills","cast1","light"}){Require(!WeaponHands.DamageRelated(code),"Global property incorrectly restricted "+code);}
        for(int frame=0;frame<20;frame++)
        {Require(WeaponHands.SecondaryForSequence(frame,true)==(frame>0 && frame%2!=0) && WeaponHands.SecondaryForSequence(frame,false)==(frame>0),"Source server sequence hand parity");}
        Require(!BarbarianCombat.Implemented(CombatAbility.DoubleSwing),"Foundation accidentally enabled unverified Double Swing");
    }
    private static void Pools(PlayerMobile owner,BaseWeapon weapon)
    {
        using var hand=WeaponHands.Use(owner,weapon);
        var basis=ClassGear.Base(weapon);int stats=ClassStats.WeaponStatPercent(owner.Str,owner.Dex,basis.StrengthBonus,basis.DexterityBonus);
        int off=owner.Items.Where(x=>ClassGear.Slot(x)!=GearSlot.Weapon && ClassGear.Active(owner,x)).Sum(x=>ClassGear.Bonus(x,"dmg%"))+NamedGear.SetBonus(owner,"dmg%");
        int mastery=BarbarianMasteries.Bonus(owner,weapon,2);
        Require(ClassStats.DamageBonus(owner,weapon)==stats+off+mastery,"Secondary local ED leaked into off-weapon pool");
        int min=(basis.MinDamage*(100+ClassGear.Bonus(weapon,"dmg%"))/100)+ClassGear.Bonus(weapon,"dmg-min");
        Require(ClassGear.Damage(weapon).Min==min,"Off-hand presentation layer incorrectly selected two-handed endpoints");
        int attackBase=Math.Max(1,5*owner.Dex-35+ClassStats.For(DiabloClass.Barbarian).AttackBonus+ClassGear.Bonus(owner,"att"));
        Require(ClassStats.AttackRating(owner)==attackBase*(100+AosAttributes.GetValue(owner,AosAttribute.AttackChance)+BarbarianMasteries.Bonus(owner,weapon,1))/100,"Selected hand attack rating pool");
    }
    private static void Exercise(PlayerMobile owner,BaseWeapon first,BaseWeapon second)
    {
        Require(WeaponHands.Pair(owner)==new WeaponPair(first,second) && ClassGear.Active(owner,first) && ClassGear.Active(owner,second),"Supported forced pair not recognized");
        Require(CombatTiming.Equipped(owner)==first,"Default native OneHanded identity changed");
        int globalFire=ClassGear.Bonus(owner,"res-fire"),globalLightning=ClassGear.Bonus(owner,"res-ltng");
        int expectedFire=owner.Items.Where(x=>ClassGear.Active(owner,x)).Sum(x=>ClassGear.Bonus(x,"res-fire"))+NamedGear.SetBonus(owner,"res-fire");
        Require(globalFire==expectedFire,"Secondary global resistance missing");
        var a=Snapshot(owner,first);var b=Snapshot(owner,second);Pools(owner,first);Pools(owner,second);
        Require(a.Fire>0 && a.Poison>0 && a.Leech>0 && a.Knock==0,$"Primary Hellplague property fixture: {a}");
        Require(b.Fire==6 && b.Poison==0 && b.Leech==0 && b.Knock==1,$"Damage/element/leech/knock property leakage between hands: {b}");
        using(var outer=WeaponHands.Use(owner,second))
        {
            Require(ClassGear.Bonus(owner,"res-fire")==globalFire && ClassGear.Bonus(owner,"res-ltng")==globalLightning,"Hand switch removed global resistance");
            using(var inner=WeaponHands.Use(owner,first)){Require(CombatTiming.Equipped(owner)==first,"Nested hand context");}
            Require(CombatTiming.Equipped(owner)==second,"Nested hand context did not restore");
        }
        Require(CombatTiming.Equipped(owner)==first,"Hand context leaked outside attack");
        var enemy=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.SetHits(10000);enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);enemy.AIObject.AITimer.Stop();
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){enemy.SetResistance(type,0);}
        try
        {
            foreach(var weapon in new[]{first,second})
            {
                using var hand=WeaponHands.Use(owner,weapon);var damage=GearEffects.Damage(owner,0,enemy);var packet=GearEffects.PrepareWeaponImpact(owner,enemy,0,new Rolls(99),damage,projectileCritical:false);
                int before=enemy.Hits*256-SkillHealth.Remainder256(enemy),own=weapon.HitPoints,other=(weapon==first?second:first).HitPoints;
                GearEffects.Strike(owner,enemy,0,new Rolls(99),wearWeapon:false,preparedImpact:packet);
                Require(before-(enemy.Hits*256-SkillHealth.Remainder256(enemy))==NamedElements.To256(packet.Parts.Total),"Selected hand native typed delivery");
                Require(weapon.HitPoints==own && (weapon==first?second:first).HitPoints==other,"Prepared no-wear impact changed either weapon");TimedEffects.ClearGear(enemy);
            }
        }
        finally{enemy.Delete();}
    }
    private static void Check()
    {
        Metadata();var owner=Fresh();Train(owner,CombatAbility.SwordMastery,20);Train(owner,CombatAbility.MaceMastery,1);
        var first=(BaseWeapon)Named("Hellplague");Require(owner.EquipItem(first),"Primary native equip");var second=(BaseWeapon)Named("Felloak");
        Require(!owner.EquipItem(second),"Native dual equip unexpectedly enabled before adapter");
        // Explicit forced native layers isolate the stat consumer. This is not
        // a substitute for the pending player/client dual-equip workflow.
        second.Layer=Layer.TwoHanded;owner.AddItem(second);ClassGear.Refresh(owner);Exercise(owner,first,second);
        var helm=Named("Tarnhelm");var gloves=Named("Steelrend");Require(owner.EquipItem(helm) && owner.EquipItem(gloves),"Real soft/off-weapon gear");ClassGear.Refresh(owner);Exercise(owner,first,second);
        var mace=(BaseWeapon)Named("Deathspade");owner.AddToBackpack(second);mace.Layer=Layer.TwoHanded;owner.AddItem(mace);ClassGear.Refresh(owner);
        int primaryAr=ClassGear.Bonus(owner,"att%");using(var hand=WeaponHands.Use(owner,mace)){Require(ClassGear.Bonus(owner,"att%")>primaryAr,"Secondary local AR omitted or shared to both hands");}Pools(owner,mace);
        owner.AddToBackpack(mace);mace.Delete();owner.AddItem(second);ClassGear.Refresh(owner);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,first.Serial.Value,second.Serial.Value,helm.Serial.Value,gloves.Serial.Value,owner.Location,owner.Map.MapID,Snapshot(owner,first),Snapshot(owner,second))));
    }
    private static void Reload()
    {
        Metadata();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var first=(BaseWeapon)World.FindItem((Serial)saved.Primary);var second=(BaseWeapon)World.FindItem((Serial)saved.Secondary);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);ClassGear.Refresh(owner);
        Exercise(owner,first,second);Require(Snapshot(owner,first)==saved.First && Snapshot(owner,second)==saved.Second,"Saved hand identity/stat payload");
        owner.AddToBackpack(second);ClassGear.Refresh(owner);Require(WeaponHands.Pair(owner)==null && CombatTiming.Equipped(owner)==first && !ClassGear.Active(owner,second),"Removed offhand remained active");
        owner.AddItem(second);ClassGear.Refresh(owner);Exercise(owner,first,second);
        int durability=second.HitPoints;second.HitPoints=0;Require(WeaponHands.Pair(owner)==null,"Broken pair accepted");second.HitPoints=durability;ClassGear.Refresh(owner);
        var polearm=new Halberd();ClassGear.Profiles.Convert(polearm,1,GearRarity.Normal,new Random(173));Require(!WeaponHands.Eligible(polearm),"True two-handed polearm allowed in a pair");polearm.Delete();
        var hybrid=new CrescentBlade{Resource=CraftResource.Valorite};ClassGear.Profiles.Convert(hybrid,99,GearRarity.Normal,new Random(173));Require(ClassGear.Base(hybrid) is{TwoHanded:true,OneOrTwoHanded:true} && WeaponHands.Eligible(hybrid),"Barbarian one/two-hand sword eligibility");
        var plain=ClassGear.State(hybrid);ClassGear.Profiles.Store(hybrid,plain with{CraftedEquipment=true,MaterialRolls=[new("damage-fire",5)]});Require(!WeaponHands.Eligible(hybrid),"Unverified crafted/material hand policy accepted");ClassGear.Profiles.Store(hybrid,plain);
        owner.AddToBackpack(second);hybrid.Layer=Layer.TwoHanded;owner.AddItem(hybrid);ClassGear.Refresh(owner);Require(!WeaponHands.TwoHanded(hybrid),"Paired hybrid sword used two-handed base");Pools(owner,hybrid);
        owner.AddToBackpack(first);ClassGear.Refresh(owner);Require(WeaponHands.TwoHanded(hybrid) && ClassGear.Damage(hybrid).Min==ClassGear.Base(hybrid).TwoHandMin,"Solo hybrid sword lost two-handed endpoints");owner.AddItem(first);
        owner.AddToBackpack(hybrid);hybrid.Delete();owner.AddItem(second);ClassGear.Refresh(owner);
        Require(Snapshot(owner,first)==saved.First && Snapshot(owner,second)==saved.Second,"Lifecycle probes changed original payload");
    }
}
