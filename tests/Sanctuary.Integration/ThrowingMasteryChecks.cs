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

public static class ThrowingMasteryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ThrowingMasteryChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Ordinary,Point3D Origin,int Map,int Quantity,int Maximum,int Attack,int Damage,int Durability);
    private sealed class Rolls(params int[] values):Random
    {
        public int Calls;
        public override int Next(int maximum)=>Math.Clamp(Calls<values.Length?values[Calls++]:Fallback(),0,maximum-1);
        private int Fallback(){Calls++;return int.MaxValue;}
    }
    private static string Manifest=>Path.Join(Core.BaseDirectory,"throwing-mastery.json");
    public static void Configure(){Register("throwing-mastery-check",Check);Register("throwing-mastery-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Throwing mastery shared stats, launch critical, quantity and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool test,string reason){if(!test){throw new InvalidOperationException(reason);}}
    private static PlayerMobile Fresh()
    {
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="ThrowingMasteryFixture"};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(owner,99);
        bool placed=false;
        for(int x=2700;x<3050 && !placed;x+=50)for(int y=1000;y<1350 && !placed;y+=50)
        {
            owner.MoveToWorld(new(x,y,Map.Felucca.GetAverageZ(x,y)),Map.Felucca);
            try{FixtureSpace.Place(owner,ClassSkillChecks.Corridor(owner,x,y),owner.Map);placed=true;}catch(InvalidOperationException){}
        }
        Require(placed,"No actual terrain corridor for throw fixture");
        while(owner.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength allocation");}
        while(owner.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Dexterity allocation");}
        owner.Hits=owner.HitsMax;owner.Mana=owner.ManaMax;owner.Stam=owner.StamMax;return owner;
    }
    private static void Train(PlayerMobile owner,int rank)
    {var id=SkillMechanics.For(CombatAbility.ThrowingMastery).TalentId;while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Throw mastery training");}}
    private static Item Named(PlayerMobile owner,string key)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true);
        if(item is BaseWeapon && CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}
        Require(owner.EquipItem(item),"Equip "+key);ClassGear.Refresh(owner);return item;
    }
    private static void Equip(PlayerMobile owner,BaseWeapon weapon)
    {if(CombatTiming.Equipped(owner) is{} old && old!=weapon){owner.AddToBackpack(old);}Require(weapon.Parent==owner || owner.EquipItem(weapon),"Equip weapon");ClassGear.Refresh(owner);}
    private static void Quantity(BaseWeapon weapon,int value)
    {NamedAmmunition.Track(weapon);NamedAmmunition.Quantities[weapon.Serial]=new(value,new(DateTime.MaxValue.Ticks,DateTimeKind.Utc));}
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.ThrowingMastery);
        foreach(var (rank,attack,damage,crit,pierce,conserve) in new[]{(1,44,28,5,8,9),(20,196,123,29,46,55),(21,204,128,29,46,56),(60,516,323,35,55,66)})
        {
            var v=new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>0,99);
            int[] expected=[attack,damage,crit,pierce,conserve,1];
            for(int i=0;i<6;i++){Require(v.Eval("passivecalc"+(i+1))==expected[i],"Pinned throw formula "+rank+"/"+(i+1));}
        }
        Require(ClassItemTypes.Is("tkni","thro") && ClassItemTypes.Is("taxe","thro") && ClassItemTypes.Is("jave","thro") && !ClassItemTypes.Is("knif","thro"),"Throwing type inheritance");
    }
    private static void Stats(PlayerMobile owner,BaseWeapon weapon)
    {
        int ar=BarbarianMasteries.Throwing(owner,weapon,1),damage=BarbarianMasteries.Throwing(owner,weapon,2);
        Require(ar>0 && damage>0 && BarbarianMasteries.Bonus(owner,weapon,1)==ar && BarbarianMasteries.Bonus(owner,weapon,2)==damage,"Shared selector missing throw AR/damage");
        int basis=Math.Max(1,5*owner.Dex-35+ClassStats.For(DiabloClass.Barbarian).AttackBonus+ClassGear.Bonus(owner,"att"));
        int existing=AosAttributes.GetValue(owner,AosAttribute.AttackChance)+(SorceressEffects.Enchantment(owner)?.AttackPercent??0)+PaladinAuras.AttackPercent(owner);
        Require(ClassStats.AttackRating(owner,37)==basis*(100+37+ar+existing)/100,"Throw AR not additive with existing pools");
        var raw=ClassGear.Base(weapon);int attributes=ClassStats.WeaponStatPercent(owner.Str,owner.Dex,raw.StrengthBonus,raw.DexterityBonus);
        int off=owner.Items.Where(x=>x!=weapon && ClassGear.Active(owner,x)).Sum(x=>ClassGear.Bonus(x,"dmg%"))+NamedGear.SetBonus(owner,"dmg%");
        Require(ClassStats.DamageBonus(owner,weapon)==attributes+off+damage+PaladinAuras.DamagePercent(owner),"Throw damage not additive");
    }
    private static void CriticalAndQuantity(PlayerMobile owner,BaseWeapon weapon)
    {
        int cap=NamedAmmunition.Maximum(weapon),durability=weapon.HitPoints;Require(cap>3,"Source quantity capacity");
        int mana=NecromancerEffects.ManaBalance256(owner);
        Quantity(weapon,1);var critical=new Rolls(99,0,99);
        var first=BarbarianThrowing.Release(owner,TimedEffects.Now,critical);
        Require(first is{Critical:true,Pierces:0} && critical.Calls==3 && NamedAmmunition.Remaining(weapon)==2,"Launch mastery critical must conserve and recover one before any hit");
        Quantity(weapon,1);var conserve=new Rolls(99,99,0,99);
        Require(BarbarianThrowing.Release(owner,TimedEffects.Now,conserve) is{Critical:false,Pierces:0} && conserve.Calls==4 && NamedAmmunition.Remaining(weapon)==1,"Noncritical conservation");
        Quantity(weapon,1);var spent=new Rolls(99,99,99,0,0,0,0);
        Require(BarbarianThrowing.Release(owner,TimedEffects.Now,spent) is{Critical:false,Pierces:4} && spent.Calls==7 && NamedAmmunition.Remaining(weapon)==0,"One consumed quantity and source four-continuation budget");
        var empty=new Rolls(0);Require(BarbarianThrowing.Release(owner,TimedEffects.Now,empty)==null && empty.Calls==0,"Empty stack rolled critical or generated quantity");
        Quantity(weapon,cap);var full=new Rolls(99,0,99);Require(BarbarianThrowing.Release(owner,TimedEffects.Now,full) is{Critical:true} && NamedAmmunition.Remaining(weapon)==cap,"Critical exceeded capacity");
        Require(weapon.HitPoints==durability && NecromancerEffects.ManaBalance256(owner)==mana,"Passive throw consumed durability or mana");
        var amulet=Named(owner,"Highlord's Wrath");Require(ClassGear.Bonus(owner,"deadly")>0,"Actual Deadly Strike fixture");
        Quantity(weapon,1);var deadly=new Rolls(99,0,99);Require(BarbarianThrowing.Release(owner,TimedEffects.Now,deadly) is{Critical:true} && deadly.Calls==3 && NamedAmmunition.Remaining(weapon)==2,"Projectile Deadly Strike should precede mastery and replenish");
        Quantity(weapon,1);var fallback=new Rolls(99,99,0,99);Require(BarbarianThrowing.Release(owner,TimedEffects.Now,fallback) is{Critical:true} && fallback.Calls==4 && NamedAmmunition.Remaining(weapon)==2,"Failed Deadly Strike did not try throw mastery");
        amulet.Delete();ClassGear.Refresh(owner);
    }
    private static void Damage(PlayerMobile owner,BaseWeapon weapon)
    {
        var enemy=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.SetHits(10000);enemy.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);enemy.AIObject.AITimer.Stop();
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){enemy.SetResistance(type,0);}
        try
        {
            var damage=GearEffects.Damage(owner,0,enemy);Require(damage.Fire>0 && damage.Lightning>0,"Gimmershred real elemental fixture");
            var rolls=new Rolls(0);var normal=GearEffects.PrepareWeaponImpact(owner,enemy,0,rolls,damage,projectileCritical:false);
            var critical=GearEffects.PrepareWeaponImpact(owner,enemy,0,rolls,damage,projectileCritical:true);
            Require(rolls.Calls==0 && critical.Parts.Physical==normal.Parts.Physical*2 && critical.Parts.Fire==normal.Parts.Fire && critical.Parts.Lightning==normal.Parts.Lightning,"Prepared projectile critical rerolled or doubled elements");
            Quantity(weapon,7);
            for(int i=0;i<3;i++)
            {
                int before=enemy.Hits*256-SkillHealth.Remainder256(enemy);var effects=new Rolls(99);
                GearEffects.Strike(owner,enemy,0,effects,wearWeapon:false,preparedImpact:critical,projectileCritical:true);
                Require(before-(enemy.Hits*256-SkillHealth.Remainder256(enemy))==NamedElements.To256(critical.Parts.Total),"Actual typed prepared projectile impact");
            }
            Require(NamedAmmunition.Remaining(weapon)==7,"Repeated/piercing delivery recovered more quantity");
        }
        finally{enemy.Delete();}
    }
    private static void Actions(PlayerMobile owner,BaseWeapon weapon)
    {
        var original=BarbarianThrowing.RandomSource;var origin=owner.Location;BarbarianThrowing.RandomSource=()=>new Rolls(99,0,99);
        try
        {
            Quantity(weapon,1);ClassCombat.Stop(owner);var session=ClassCombat.For(owner);session.NextAction=0;
            var point=new[]{new Point3D(owner.X+3,owner.Y,owner.Z),new Point3D(owner.X,owner.Y+3,owner.Z),new Point3D(owner.X-3,owner.Y,owner.Z),new Point3D(owner.X,owner.Y-3,owner.Z)}.First(p=>owner.InLOS(new Point3D(p.X,p.Y,p.Z+14)));
            Require(ClassCombat.Perform(owner,CombatAbility.BasicAttack,null,point,Core.TickCount),"Begin empty-ground throw");
            Require(NamedAmmunition.Remaining(weapon)==1 && !ClassCombat.ResolveAction(owner,session.EffectAt-1),"Windup changed quantity");
            bool resolved=ClassCombat.ResolveAction(owner,session.EffectAt);
            Require(resolved && NamedAmmunition.Remaining(weapon)==2,$"Empty-ground release: resolved={resolved}, quantity={NamedAmmunition.Remaining(weapon)}, supported={BarbarianThrowing.Supported(owner)}, alive={owner.Alive}, frozen={owner.Frozen}, paralyzed={owner.Paralyzed}, point={point}, origin={owner.Location}, LOS={owner.InLOS(point)}, active={ClassGear.Active(owner,weapon)}");
            Require(!ClassCombat.ResolveAction(owner,session.EffectAt+1) && NamedAmmunition.Remaining(weapon)==2,"Repeated impact duplicated quantity");
            ClassCombat.Stop(owner);session.NextAction=0;Require(ClassCombat.Perform(owner,CombatAbility.BasicAttack,null,point,Core.TickCount),"Begin cancel probe");
            owner.AddToBackpack(weapon);Require(!ClassCombat.ResolveAction(owner,session.EffectAt) && NamedAmmunition.Remaining(weapon)==2,"Weapon swap released canceled throw");Equip(owner,weapon);
            ClassCombat.Stop(owner);session.NextAction=0;Require(ClassCombat.Perform(owner,CombatAbility.BasicAttack,null,point,Core.TickCount),"Begin movement cancellation");
            owner.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);Require(!ClassCombat.ResolveAction(owner,session.EffectAt) && NamedAmmunition.Remaining(weapon)==2,"Movement released canceled throw");
        }
        finally{BarbarianThrowing.RandomSource=original;ClassCombat.Stop(owner);owner.MoveToWorld(origin,owner.Map);}
    }
    private static void Check()
    {
        Goldens();var owner=Fresh();var weapon=(BaseWeapon)Named(owner,"Gimmershred");Require(BarbarianMasteries.Bonus(owner,weapon,1)==0,"Untrained throw mastery");Train(owner,1);
        Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.ThrowingMastery) && !SkillBindings.Instance.Assign(owner,false,CombatAbility.ThrowingMastery) && !SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.ThrowingMastery),"Passive accepted active binding");
        Require(BarbarianMasteries.Bonus(owner,weapon,1)==44,"Rank1 throw AR");Train(owner,20);Stats(owner,weapon);CriticalAndQuantity(owner,weapon);Damage(owner,weapon);Actions(owner,weapon);
        var helm=Named(owner,"Tarnhelm");Require(SkillMechanics.Rank(owner,CombatAbility.ThrowingMastery)==21 && BarbarianMasteries.Bonus(owner,weapon,1)==204,"Effective throw rank/real soft gear");Stats(owner,weapon);
        var ordinary=new NamedThrowingKnife();ClassGear.Profiles.Convert(ordinary,1,GearRarity.Normal,new Random(173));Equip(owner,ordinary);Require(NamedAmmunition.Maximum(ordinary)>1 && BarbarianThrowing.Supported(owner),"Ordinary converted quantity/type adapter");Quantity(ordinary,1);Require(BarbarianThrowing.Release(owner,TimedEffects.Now,new Rolls(99,0,99)) is{Critical:true} && NamedAmmunition.Remaining(ordinary)==2,"Ordinary converted quantity recovery");
        Equip(owner,weapon);Require(BarbarianMasteries.Throwing(owner,ordinary,1)==0,"Backpack weapon gained mastery");Quantity(weapon,17);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,weapon.Serial.Value,helm.Serial.Value,ordinary.Serial.Value,owner.Location,owner.Map.MapID,17,NamedAmmunition.Maximum(weapon),ClassStats.AttackRating(owner),ClassStats.DamageBonus(owner,weapon),weapon.HitPoints)));
    }
    private static void Reload()
    {
        Goldens();var row=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)row.Owner);var weapon=(BaseWeapon)World.FindItem((Serial)row.Weapon);var ordinary=(BaseWeapon)World.FindItem((Serial)row.Ordinary);var helm=World.FindItem((Serial)row.Helm);ClassGear.Refresh(owner);
        // Unconnected players load in the normal offline/internal-map state.
        // Restore this fixture's saved world position before action/LOS checks.
        owner.MoveToWorld(row.Origin,Map.Maps[row.Map]);
        Require(ClassCreation.Profiles.GetRank(owner.Serial,SkillMechanics.For(CombatAbility.ThrowingMastery).TalentId)==20 && SkillMechanics.Rank(owner,CombatAbility.ThrowingMastery)==21,"Throw rank save");Stats(owner,weapon);
        Require(NamedAmmunition.Maximum(weapon)==row.Maximum && NamedAmmunition.Remaining(weapon)==row.Quantity && NamedAmmunition.Remaining(ordinary)==2 && weapon.HitPoints==row.Durability,"Saved native quantity/item payload");
        Require(ClassStats.AttackRating(owner)==row.Attack && ClassStats.DamageBonus(owner,weapon)==row.Damage,"Saved throw stat pools");
        owner.AddToBackpack(helm);ClassGear.Refresh(owner);Require(BarbarianMasteries.Bonus(owner,weapon,1)==196,"Soft rank removal");Require(owner.EquipItem(helm),"Reequip saved helm");ClassGear.Refresh(owner);
        int durability=weapon.HitPoints;weapon.HitPoints=0;Require(!BarbarianThrowing.Supported(owner) && BarbarianMasteries.Throwing(owner,weapon,1)==0,"Broken throw weapon retained passive");weapon.HitPoints=durability;ClassGear.Refresh(owner);
        Actions(owner,weapon);Quantity(weapon,row.Quantity);Equip(owner,ordinary);Require(NamedAmmunition.Remaining(ordinary)==2,"Trade/equip reset ordinary quantity");Equip(owner,weapon);Stats(owner,weapon);
        // History/respec must remove the live passive without rewriting its
        // trained rank or granting quantity. Use a disposable independent actor.
        var other=Fresh();Train(other,1);var blade=new NamedThrowingKnife();ClassGear.Profiles.Convert(blade,1,GearRarity.Normal,new Random(173));Equip(other,blade);Quantity(blade,3);
        other.AddToBackpack(blade);Require(ClassCreation.Profiles.TryActivateHistory(other,DiabloClass.Paladin),"History switch");
        ClassCreation.Profiles.AdvanceLevel(other,99);
        while(other.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(other,CharacterStat.Strength),"History Strength");}
        while(other.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(other,CharacterStat.Dexterity),"History Dexterity");}Equip(other,blade);
        Require(BarbarianMasteries.Throwing(other,blade,1)==0 && NamedAmmunition.Remaining(blade)==3,"Inactive history mastery/quantity");
        other.AddToBackpack(blade);Require(ClassCreation.Profiles.TryActivateHistory(other,DiabloClass.Barbarian),"History restore");Equip(other,blade);
        Require(BarbarianMasteries.Throwing(other,blade,1)==44,"History passive restore");ClassCreation.Profiles.TryGet(other.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(other,profile);
        Require(BarbarianMasteries.Throwing(other,blade,1)==0 && NamedAmmunition.Remaining(blade)==3,"Respec passive or quantity reset");other.Delete();
    }
}
