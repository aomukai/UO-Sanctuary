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

public static class BarbarianMasteryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BarbarianMasteryChecks));
    private static readonly CombatAbility[] Skills=[CombatAbility.SwordMastery,CombatAbility.AxeMastery,CombatAbility.MaceMastery,CombatAbility.PolearmMastery,CombatAbility.SpearMastery];
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,CombatAbility Ability,int Attack,int Damage,int Critical,int Minimum,int Maximum);
    private sealed class Rolls(params int[] rolls):Random
    {
        public int Calls;
        public override int Next(int maximum){int value=Calls<rolls.Length?rolls[Calls]:maximum-1;Calls++;return Math.Clamp(value,0,maximum-1);}
    }
    private static string Manifest=>Path.Join(Core.BaseDirectory,"barbarian-masteries.json");
    public static void Configure(){Register("barbarian-masteries-check",Check);Register("barbarian-masteries-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Melee mastery types, critical ordering, native delivery and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static PlayerMobile Fresh(int offset)
    {
        var actor=new PlayerMobile{Body=0x190,Name="BarbarianMasteryFixture",Player=true};actor.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(actor,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(actor,99);
        FixtureSpace.Place(actor,new(6100,1800,0),Map.Trammel);while(actor.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(actor,CharacterStat.Strength),"Fixture Strength allocation");}
        while(actor.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(actor,CharacterStat.Dexterity),"Fixture Dexterity allocation");}actor.Hits=actor.HitsMax;actor.Mana=actor.ManaMax;actor.Stam=actor.StamMax;return actor;
    }
    private static void Train(PlayerMobile owner,CombatAbility ability,int rank)
    {string talent=SkillMechanics.For(ability).TalentId;while(ClassCreation.Profiles.GetRank(owner.Serial,talent)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,talent,out _),"Cannot train "+talent);}}
    private static T Equip<T>(PlayerMobile owner,T item) where T:Item
    {
        if(item is BaseWeapon && CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}
        ClassGear.Profiles.Convert(item,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(item),"Cannot equip "+item.GetType().Name);ClassGear.Refresh(owner);return item;
    }
    private static Item Named(PlayerMobile owner,string source)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);
        if(item is BaseWeapon && CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}
        Require(owner.EquipItem(item),"Cannot equip "+source);ClassGear.Refresh(owner);return item;
    }
    private static BaseWeapon Weapon(int index)=>index switch{0=>new Longsword(),1=>new Axe(),2=>new Mace(),3=>new Halberd(),_=>new Spear()};
    private static void Bonus(PlayerMobile owner,BaseWeapon weapon,int ar,int damage,int critical)
    {Require(BarbarianMasteries.Bonus(owner,weapon,1)==ar && BarbarianMasteries.Bonus(owner,weapon,2)==damage && BarbarianMasteries.Bonus(owner,weapon,3)==critical,"Wrong mastery triple for "+weapon?.GetType().Name);}
    private static void Goldens()
    {
        foreach(var ability in Skills)
        {
            var row=SkillMechanics.For(ability);int initial=ability is CombatAbility.PolearmMastery or CombatAbility.SpearMastery?44:40;
            foreach(var (rank,damage,critical) in new[]{(1,28,5),(20,123,29),(21,128,29),(22,133,30)})
            {
                var values=new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>0,99);
                Require(values.Eval("passivecalc1")==initial+(rank-1)*8 && values.Eval("passivecalc2")==damage && values.Eval("passivecalc3")==critical,"Pinned mastery formula golden "+row.SourceKey);
            }
        }
        Require((ushort)CombatAbility.SwordMastery==132 && ClassAbilities.Find(CombatAbility.SwordMastery)?.Name=="Blade Mastery","Blade rename changed protocol identity or label");
        Require(ClassItemTypes.Is("swor","blde") && ClassItemTypes.Is("knif","blde") && ClassItemTypes.Is("staf","blun") && ClassItemTypes.Is("scep","blun") && ClassItemTypes.Is("wand","blun")
            && !ClassItemTypes.Is("bow","blde") && !ClassItemTypes.Is("axe","blun") && !ClassItemTypes.Is("unknown","mele"),"Pinned modern item-type inheritance");
    }
    private static void NativeStats(PlayerMobile owner,BaseWeapon weapon,int ar,int damage)
    {
        int basis=Math.Max(1,5*owner.Dex-35+ClassStats.For(DiabloClass.Barbarian).AttackBonus+ClassGear.Bonus(owner,"att"));
        int existing=AosAttributes.GetValue(owner,AosAttribute.AttackChance)+(SorceressEffects.Enchantment(owner)?.AttackPercent??0)+PaladinAuras.AttackPercent(owner);
        Require(ClassStats.AttackRating(owner,37)==basis*(100+37+ar+existing)/100,"Mastery attack rating did not join additive skill/item/enchant/aura pool");
        var raw=ClassGear.Base(weapon);int stat=ClassStats.WeaponStatPercent(owner.Str,owner.Dex,raw.StrengthBonus,raw.DexterityBonus);
        int off=owner.Items.Where(x=>x!=weapon && ClassGear.Active(owner,x)).Sum(x=>ClassGear.Bonus(x,"dmg%"))+NamedGear.SetBonus(owner,"dmg%");
        Require(ClassStats.DamageBonus(owner,weapon)==stat+off+damage+PaladinAuras.DamagePercent(owner),"Mastery damage multiplied other percentages or counted local weapon ED again");
        var range=ClassStats.WeaponBaseDamage(owner);var physical=ClassStats.PhysicalDamageRange(owner);
        Require(physical.Min==Math.Max(1,range.Min*(100+stat+off+damage+PaladinAuras.DamagePercent(owner))/100),"Actual physical endpoint missed mastery percentage");
    }
    private static int Life(Mobile m)=>m.Hits*256-SkillHealth.Remainder256(m);
    private static void Critical(PlayerMobile owner)
    {
        var weapon=(BaseWeapon)Named(owner,"Hellplague");var amulet=Named(owner,"Highlord's Wrath");var gloves=Named(owner,"Steelrend");
        int mastery=BarbarianMasteries.Bonus(owner,weapon,3),deadly=ClassGear.Bonus(owner,"deadly");
        Require(mastery==30 && deadly>0 && ClassGear.Bonus(weapon,"dmg%")>0 && ClassGear.Bonus(gloves,"dmg%")>0,$"Real local/offweapon/critical/deadly gear fixture: mastery={mastery}, deadly={deadly}, local={ClassGear.Bonus(weapon,"dmg%")}, off={ClassGear.Bonus(gloves,"dmg%")}");
        var first=new Rolls(0,0);Require(NamedCombat.Deadly(owner,first)==2 && first.Calls==1,"Successful mastery rolled Deadly Strike too");
        var second=new Rolls(99,0);Require(NamedCombat.Deadly(owner,second)==2 && second.Calls==2,"Failed mastery did not try Deadly Strike");
        var neither=new Rolls(99,99);Require(NamedCombat.Deadly(owner,neither)==1 && neither.Calls==2,"Failed critical/deadly doubled damage");
        NativeStats(owner,weapon,BarbarianMasteries.Bonus(owner,weapon,1),BarbarianMasteries.Bonus(owner,weapon,2));
        var enemy=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.SetHits(10000);enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);enemy.AIObject.AITimer.Stop();
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){enemy.SetResistance(type,0);}
        try
        {
            Require(ClassStats.CheckHit(owner,enemy,0,()=>0)==true && ClassStats.CheckHit(owner,enemy,0,()=>1)==false,"Shared mastery AR hit/miss route");
            var normal=GearEffects.Damage(owner,0,enemy);Require(normal.Fire>0 && normal.Lightning>0,"Named flat elemental fixture");
            foreach(var (mRoll,dRoll,multiplier) in new[]{(0,99,2),(99,0,2),(99,99,1)})
            {
                int hp=Life(enemy);GearEffects.Strike(owner,enemy,0,new Rolls(mRoll,dRoll),wearWeapon:false);
                int expected=NamedElements.To256(normal.Physical*multiplier+normal.Fire+normal.Cold+normal.Lightning+normal.Magic);
                Require(hp-Life(enemy)==expected,"Native mastery/deadly strike multiplied elements, stacked to4x or lost FP delivery");TimedEffects.ClearGear(enemy);
            }
        }
        finally{enemy.Delete();amulet.Delete();gloves.Delete();weapon.Delete();ClassGear.Refresh(owner);}
    }
    private static void Lifecycle()
    {
        var owner=Fresh(9);Train(owner,CombatAbility.SwordMastery,1);var weapon=Equip(owner,new Dagger());Bonus(owner,weapon,40,28,5);
        owner.AddToBackpack(weapon);Require(ClassCreation.Profiles.TryActivateHistory(owner,DiabloClass.Paladin),"Mastery history exit");
        Require(owner.EquipItem(weapon),"Inactive history weapon equip");ClassGear.Refresh(owner);Bonus(owner,weapon,0,0,0);owner.AddToBackpack(weapon);
        Require(ClassCreation.Profiles.TryActivateHistory(owner,DiabloClass.Barbarian),"Mastery history restore");
        Require(weapon.Parent==owner || owner.EquipItem(weapon),"History weapon restore");ClassGear.Refresh(owner);Bonus(owner,weapon,40,28,5);
        ClassCreation.Profiles.TryGet(owner.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(owner,profile);Bonus(owner,weapon,0,0,0);owner.Delete();
    }
    private static void Check()
    {
        Goldens();Lifecycle();var saved=new Saved[Skills.Length];
        for(int i=0;i<Skills.Length;i++)
        {
            var owner=Fresh(i);var weapon=Equip(owner,Weapon(i));Bonus(owner,weapon,0,0,0);
            foreach(var ability in Skills){Train(owner,ability,1);Require(!SkillBindings.Instance.Assign(owner,true,ability) && !SkillBindings.Instance.Assign(owner,false,ability) && !SkillBindings.Instance.AssignQuick(owner,0,ability),"Passive mastery accepted input binding");}
            int initial=i>=3?44:40;Bonus(owner,weapon,initial,28,5);NativeStats(owner,weapon,initial,28);
            Train(owner,Skills[i],20);Require(!ClassCreation.Profiles.TryTrain(owner.Serial,SkillMechanics.For(Skills[i]).TalentId,out _),"Mastery hard cap exceeded20");Bonus(owner,weapon,initial+152,123,29);
            var helm=Named(owner,"Tarnhelm");Bonus(owner,weapon,initial+160,128,29);NativeStats(owner,weapon,initial+160,128);
            if(i==0)
            {
                var dagger=Equip(owner,new Dagger());Bonus(owner,dagger,200,128,29);dagger.Delete();
                var bow=Equip(owner,new Bow());Bonus(owner,bow,0,0,0);var rolls=new Rolls(0);Require(NamedCombat.Deadly(owner,rolls)==1 && rolls.Calls==0,"Bow used melee critical");bow.Delete();
                Bonus(owner,null,0,0,0);var naked=new Rolls(0);Require(NamedCombat.Deadly(owner,naked)==1 && naked.Calls==0,"Unarmed used melee critical");
                var thrown=Equip(owner,new NamedThrowingKnife());Bonus(owner,thrown,0,0,0);thrown.Delete();
                Critical(owner);
            }
            if(i==2)
            {
                var staff=Equip(owner,new QuarterStaff());Bonus(owner,staff,200,128,29);staff.Delete();
                var scepter=Equip(owner,new Scepter());Bonus(owner,scepter,200,128,29);scepter.Delete();
            }
            Require(weapon.Parent==owner || owner.EquipItem(weapon),"Saved mastery weapon restore");ClassGear.Refresh(owner);
            var paladin=ClassSkillChecks.Player(DiabloClass.Paladin);paladin.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);
            Require(SkillBindings.Instance.Assign(paladin,false,CombatAbility.BlessedAim),"Mastery aura source");NativeStats(owner,weapon,initial+160,128);
            SkillBindings.Instance.Assign(paladin,false,CombatAbility.Might);NativeStats(owner,weapon,initial+160,128);PaladinAuras.Clear(owner);PaladinAuras.Clear(paladin);paladin.Delete();
            var range=ClassStats.PhysicalDamageRange(owner);saved[i]=new(owner.Serial.Value,weapon.Serial.Value,helm.Serial.Value,Skills[i],ClassStats.AttackRating(owner),ClassStats.DamageBonus(owner,weapon),29,range.Min,range.Max);
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest));
        foreach(var row in saved)
        {
            var owner=(PlayerMobile)World.FindMobile((Serial)row.Owner);var weapon=(BaseWeapon)World.FindItem((Serial)row.Weapon);var helm=World.FindItem((Serial)row.Helm);ClassGear.Refresh(owner);
            int initial=row.Ability is CombatAbility.PolearmMastery or CombatAbility.SpearMastery?44:40;
            Require(ClassCreation.Profiles.GetRank(owner.Serial,SkillMechanics.For(row.Ability).TalentId)==20 && SkillMechanics.Rank(owner,row.Ability)==21,"Mastery ranks did not persist");
            Bonus(owner,weapon,initial+160,128,row.Critical);NativeStats(owner,weapon,initial+160,128);var range=ClassStats.PhysicalDamageRange(owner);
            Require(ClassStats.AttackRating(owner)==row.Attack && ClassStats.DamageBonus(owner,weapon)==row.Damage && range.Min==row.Minimum && range.Max==row.Maximum,"Saved mastery native attack/damage payload changed");
            owner.AddToBackpack(helm);ClassGear.Refresh(owner);Bonus(owner,weapon,initial+152,123,29);
            Require(owner.EquipItem(helm),"Saved mastery helm re-equip");ClassGear.Refresh(owner);Bonus(owner,weapon,initial+160,128,29);
            owner.AddToBackpack(weapon);Bonus(owner,weapon,0,0,0);Require(owner.EquipItem(weapon),"Saved mastery weapon re-equip");ClassGear.Refresh(owner);NativeStats(owner,weapon,initial+160,128);
            int durability=weapon.HitPoints;weapon.HitPoints=0;Bonus(owner,weapon,0,0,0);weapon.HitPoints=durability;ClassGear.Refresh(owner);Bonus(owner,weapon,initial+160,128,29);
            if(row.Ability==CombatAbility.SwordMastery){Critical(owner);Require(owner.EquipItem(weapon),"Reload damage probe weapon restore");ClassGear.Refresh(owner);NativeStats(owner,weapon,initial+160,128);}
        }
    }
}
