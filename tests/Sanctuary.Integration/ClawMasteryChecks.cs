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

public static class ClawMasteryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ClawMasteryChecks));
    private sealed record Saved(uint Owner,uint Claw,uint Helm,uint Amulet,int Rank,int Attack,int Damage,int Minimum,int Maximum);
    private sealed class Rolls(params int[] values):Random
    {
        public int Calls;
        public override int Next(int maximum){int value=Calls<values.Length?values[Calls]:maximum-1;Calls++;return Math.Clamp(value,0,maximum-1);}
    }
    private static string Manifest=>Path.Join(Core.BaseDirectory,"claw-mastery-fixtures.json");
    public static void Configure(){Register("claw-mastery-check",Check);Register("claw-mastery-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Claw Mastery actual weapon/stat/critical and lifecycle/save checks",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static PlayerMobile Fresh()
    {
        var actor=new PlayerMobile{Body=0x191,Name="ClawMasteryFixture",Player=true};actor.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(actor,DiabloClass.Assassin);ClassCreation.Profiles.AdvanceLevel(actor,99);
        FixtureSpace.Place(actor,new(6050,1850,0),Map.Trammel);
        while(actor.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(actor,CharacterStat.Strength),"Strength allocation");}
        while(actor.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(actor,CharacterStat.Dexterity),"Dexterity allocation");}
        actor.Hits=actor.HitsMax;actor.Mana=actor.ManaMax;actor.Stam=actor.StamMax;return actor;
    }
    private static void Train(PlayerMobile owner,int rank)
    {while(ClassCreation.Profiles.GetRank(owner.Serial,"assassin.clawmastery")<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"assassin.clawmastery",out _),"Cannot train Claw Mastery");}}
    private static void Equip(PlayerMobile owner,Item item)
    {
        if(item is BaseWeapon && CombatTiming.Equipped(owner) is{} old && old!=item){owner.AddToBackpack(old);}
        if(item.Parent!=owner){Require(owner.EquipItem(item),"Cannot equip "+item.GetType().Name);}ClassGear.Refresh(owner);
    }
    private static T Normal<T>(PlayerMobile owner,T item) where T:Item
    {ClassGear.Profiles.Convert(item,99,GearRarity.Normal,new Random(173));owner.AddToBackpack(item);Equip(owner,item);return item;}
    private static Item Named(PlayerMobile owner,string source)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);
        owner.AddToBackpack(item);Equip(owner,item);return item;
    }
    private static void Bonus(PlayerMobile owner,BaseWeapon weapon,int attack,int damage,int critical)
    {
        Require(AssassinCombat.Mastery(owner,weapon,1)==attack && AssassinCombat.Mastery(owner,weapon,2)==damage && AssassinCombat.Mastery(owner,weapon,3)==critical,"Incorrect claw mastery triple");
        Require(WeaponMasteries.Bonus(owner,weapon,1)==attack && WeaponMasteries.Bonus(owner,weapon,2)==damage && WeaponMasteries.Bonus(owner,weapon,3)==critical,"Shared selector missed or duplicated claw supplier");
        Require(BarbarianMasteries.Bonus(owner,weapon,1)==0,"Claw skill leaked into Barbarian supplier");
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.ClawMastery);
        Require(row.SourceId==252 && row.Text("passiveitype")=="h2h" && row.Text("passivestat1")=="passive_mastery_melee_th"
            && row.Text("passivestat2")=="passive_mastery_melee_dmg" && row.Text("passivestat3")=="passive_mastery_melee_crit","Pinned mastery stat/type identity");
        foreach(var (rank,attack,damage,critical) in new[]{(1,30,35,3),(2,40,39,6),(3,50,43,9),(20,220,111,21),(21,230,115,21),(23,250,123,21),(24,260,127,22)})
        {
            var values=new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>0,99);
            Require(values.Eval("passivecalc1")==attack && values.Eval("passivecalc2")==damage && values.Eval("passivecalc3")==critical,"Pinned rank golden "+rank);
        }
        Require(ClassItemTypes.Is("h2h2","h2h") && !ClassItemTypes.Is("knif","h2h") && !ClassItemTypes.Is("swor","h2h"),"Current claw inheritance");
        Require(AssassinCombat.Mastery(null,null,1)==0 && WeaponMasteries.Bonus(null,null,1)==0,"Null mastery guard");
    }
    private static void NativeStats(PlayerMobile owner,BaseWeapon weapon,int attack,int damage)
    {
        int basis=Math.Max(1,5*owner.Dex-35+ClassStats.For(DiabloClass.Assassin).AttackBonus+ClassGear.Bonus(owner,"att"));
        int existing=AosAttributes.GetValue(owner,AosAttribute.AttackChance)+(SorceressEffects.Enchantment(owner)?.AttackPercent??0)+PaladinAuras.AttackPercent(owner);
        Require(ClassStats.AttackRating(owner,37)==basis*(100+37+existing+attack)/100,"Claw AR did not enter additive skill/item/enchant/aura pool");
        var raw=ClassGear.Base(weapon);int stat=ClassStats.WeaponStatPercent(owner.Str,owner.Dex,raw.StrengthBonus,raw.DexterityBonus);
        int off=owner.Items.Where(x=>x!=weapon && ClassGear.Slot(x)!=GearSlot.Weapon && ClassGear.Active(owner,x)).Sum(x=>ClassGear.Bonus(x,"dmg%"))+NamedGear.SetBonus(owner,"dmg%");
        int expected=stat+off+damage+PaladinAuras.DamagePercent(owner);
        Require(ClassStats.DamageBonus(owner,weapon)==expected,"Claw damage lost additive pool or doubled local weapon ED");
        var range=ClassStats.WeaponBaseDamage(owner);var physical=ClassStats.PhysicalDamageRange(owner);
        Require(physical.Min==Math.Max(1,range.Min*(100+expected)/100) && physical.Max==Math.Max(1,range.Max*(100+expected)/100),"Actual claw physical endpoints");
    }
    private static void Critical(PlayerMobile owner)
    {
        int mastery=AssassinCombat.Mastery(owner,CombatTiming.Equipped(owner),3),deadly=ClassGear.Bonus(owner,"deadly");Require(mastery>0 && deadly>0,"Critical/deadly fixture");
        var first=new Rolls(0,0);Require(NamedCombat.Deadly(owner,first)==2 && first.Calls==1,"Mastery critical did not short-circuit");
        var fallback=new Rolls(99,0);Require(NamedCombat.Deadly(owner,fallback)==2 && fallback.Calls==2,"Deadly did not follow failed mastery");
        var neither=new Rolls(99,99);Require(NamedCombat.Deadly(owner,neither)==1 && neither.Calls==2,"Failed mastery/deadly doubled physical");
        var enemy=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.SetHits(50000);enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);enemy.AIObject.AITimer.Stop();
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){enemy.SetResistance(type,0);}
        try
        {
            Require(ClassStats.CheckHit(owner,enemy,0,()=>0)==true && ClassStats.CheckHit(owner,enemy,0,()=>1)==false,"Actual claw AR hit/miss consumer");
            var damage=GearEffects.Damage(owner,0,enemy);Require(damage.Physical>0 && damage.Lightning>0,"Named physical and lightning fixture");
            foreach(var (a,b,multiplier) in new[]{(0,0,2),(99,0,2),(99,99,1)})
            {
                long before=SkillHealth.Balance256(enemy);GearEffects.Strike(owner,enemy,0,new Rolls(a,b),wearWeapon:false);
                int expected=NamedElements.To256(damage.Physical*multiplier+damage.Fire+damage.Cold+damage.Lightning+damage.Magic);
                Require(before-SkillHealth.Balance256(enemy)==expected,"Claw native critical did not double physical exactly once while retaining elements");TimedEffects.ClearGear(enemy);
            }
        }
        finally{enemy.Delete();}
    }
    private static void Lifecycle()
    {
        var owner=Fresh();Train(owner,1);var weapon=Normal(owner,new Tekagi());Bonus(owner,weapon,30,35,3);
        owner.AddToBackpack(weapon);
        Require(ClassCreation.Profiles.TryActivateHistory(owner,DiabloClass.Paladin),"Assassin history exit");Bonus(owner,weapon,0,0,0);
        Require(ClassCreation.Profiles.TryActivateHistory(owner,DiabloClass.Assassin),"Assassin history restore");Equip(owner,weapon);Bonus(owner,weapon,30,35,3);
        owner.Kill();Require(!owner.Alive && ClassCreation.Profiles.GetRank(owner.Serial,"assassin.clawmastery")==1,"Death lost learned passive");
        owner.Resurrect();if(weapon.Parent!=owner){owner.AddToBackpack(weapon);}Equip(owner,weapon);Bonus(owner,weapon,30,35,3);
        ClassCreation.Profiles.TryGet(owner.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(owner,profile);Require(SkillMechanics.Rank(owner,CombatAbility.ClawMastery)==0,"Respec retained passive");Bonus(owner,weapon,0,0,0);owner.Delete();
    }
    private static void Check()
    {
        Goldens();Lifecycle();var owner=Fresh();var claw=Normal(owner,new Tekagi());Bonus(owner,claw,0,0,0);NativeStats(owner,claw,0,0);
        var helm=Named(owner,"Tarnhelm");Require(SkillMechanics.Rank(owner,CombatAbility.ClawMastery)==0,"General skill bonus granted unlearned mastery");owner.AddToBackpack(helm);ClassGear.Refresh(owner);
        Train(owner,1);Bonus(owner,claw,30,35,3);NativeStats(owner,claw,30,35);
        Require(!ClassCombat.Available(owner,CombatAbility.ClawMastery) && !SkillBindings.Instance.Assign(owner,true,CombatAbility.ClawMastery)
            && !SkillBindings.Instance.Assign(owner,false,CombatAbility.ClawMastery) && !SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.ClawMastery),"Passive accepted active binding");
        var sword=Normal(owner,new Longsword());Bonus(owner,sword,0,0,0);NativeStats(owner,sword,0,0);sword.Delete();
        var dagger=Normal(owner,new Dagger());Bonus(owner,dagger,0,0,0);dagger.Delete();var bow=Normal(owner,new Bow());Bonus(owner,bow,0,0,0);bow.Delete();
        Equip(owner,claw);Train(owner,20);Require(!ClassCreation.Profiles.TryTrain(owner.Serial,"assassin.clawmastery",out _),"Hard rank cap exceeded");Bonus(owner,claw,220,111,21);NativeStats(owner,claw,220,111);
        var magic=ItemOrigins.Create(ItemOrigin.Drop,()=>new Tekagi());ClassGear.Profiles.FromMonster(magic,99,GearRarity.Magic,new Random(317));owner.AddToBackpack(magic);owner.AddToBackpack(claw);
        Require(owner.EquipItem(magic)==false && !ClassGear.State(magic).Identified,"Unidentified claw equipped");var scroll=new IdentifyScroll();owner.AddToBackpack(scroll);Require(Identification.Reveal(owner,scroll,magic) && scroll.Deleted,"Real claw Identify path");Equip(owner,magic);Bonus(owner,magic,220,111,21);NativeStats(owner,magic,220,111);
        Equip(owner,helm);Bonus(owner,magic,230,115,21);NativeStats(owner,magic,230,115);
        var bartuc=(BaseWeapon)Named(owner,"Cutthroat1");Require(ClassGear.Base(bartuc).Type=="h2h2","Elite claw fixture is not h2h2");
        Require(SkillMechanics.Rank(owner,CombatAbility.ClawMastery)==23 && ClassCreation.Profiles.GetRank(owner.Serial,"assassin.clawmastery")==20,"Specific class/effective versus hard rank");Bonus(owner,bartuc,250,123,21);NativeStats(owner,bartuc,250,123);
        int durability=bartuc.HitPoints;bartuc.HitPoints=0;ClassGear.Refresh(owner);Bonus(owner,bartuc,0,0,0);bartuc.HitPoints=durability;ClassGear.Refresh(owner);Bonus(owner,bartuc,250,123,21);
        var amulet=Named(owner,"Highlord's Wrath");Bonus(owner,bartuc,260,127,22);NativeStats(owner,bartuc,260,127);Critical(owner);
        var range=ClassStats.PhysicalDamageRange(owner);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,bartuc.Serial.Value,helm.Serial.Value,amulet.Serial.Value,24,ClassStats.AttackRating(owner),ClassStats.DamageBonus(owner,bartuc),range.Min,range.Max)));
    }
    private static void Reload()
    {
        Goldens();var row=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)row.Owner);var claw=(BaseWeapon)World.FindItem((Serial)row.Claw);var helm=World.FindItem((Serial)row.Helm);var amulet=World.FindItem((Serial)row.Amulet);ClassGear.Refresh(owner);
        Require(owner!=null && claw?.Parent==owner && helm?.Parent==owner && amulet?.Parent==owner,"Saved real mastery owner/gear");
        Require(ClassCreation.Profiles.GetRank(owner.Serial,"assassin.clawmastery")==20 && SkillMechanics.Rank(owner,CombatAbility.ClawMastery)==row.Rank,"Saved hard/effective claw rank");
        Bonus(owner,claw,260,127,22);NativeStats(owner,claw,260,127);var range=ClassStats.PhysicalDamageRange(owner);
        Require(ClassStats.AttackRating(owner)==row.Attack && ClassStats.DamageBonus(owner,claw)==row.Damage && range.Min==row.Minimum && range.Max==row.Maximum,"Saved native claw endpoints or additive AR/damage changed");Critical(owner);
        owner.AddToBackpack(helm);ClassGear.Refresh(owner);Bonus(owner,claw,250,123,21);Equip(owner,helm);Bonus(owner,claw,260,127,22);
        owner.AddToBackpack(claw);Bonus(owner,claw,0,0,0);Equip(owner,claw);Bonus(owner,claw,260,127,22);
        int durability=claw.HitPoints;claw.HitPoints=0;ClassGear.Refresh(owner);Bonus(owner,claw,0,0,0);claw.HitPoints=durability;ClassGear.Refresh(owner);Bonus(owner,claw,260,127,22);
    }
}
