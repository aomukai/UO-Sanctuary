// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class LevitationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(LevitationChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,bool Female,int Attack,int Damage,int Minimum,int Maximum);
    private sealed class Rolls(params int[] rolls):Random
    {
        public int Calls;
        public override int Next(int maximum){int value=Calls<rolls.Length?rolls[Calls]:maximum-1;Calls++;return Math.Clamp(value,0,maximum-1);}
    }
    private static string Manifest=>Path.Join(Core.BaseDirectory,"levitation-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string n,Action action)=>ConsoleInputHandler.RegisterCommand(n,"Levitation combat, recipient equip and saved effective ranks",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure()
    {
        Register("levitation-check",Check);Register("levitation-reload-check",Reload);
        Register("levitation-validation-check",Validation);
    }
    private static PlayerMobile Fresh(bool female=false,int strength=200,int dexterity=200,DiabloClass type=DiabloClass.Warlock)
    {
        var p=new PlayerMobile{Body=female?0x191:0x190,Female=female,Player=true,Name="LevitationFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,type);ClassCreation.Profiles.AdvanceLevel(p,99);FixtureSpace.Place(p,new(6100,1650,0),Map.Trammel);
        while(p.RawStr<strength){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength allocation");}
        while(p.RawDex<dexterity){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity allocation");}
        return p;
    }
    private static void Train(PlayerMobile p,int rank)
    {while(ClassCreation.Profiles.GetRank(p.Serial,"warlock.levitationmastery")<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,"warlock.levitationmastery",out _),"Levitation train");}}
    private static Item Named(string source)=>NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);
    private static T Equip<T>(PlayerMobile p,T item) where T:Item
    {
        if(item is BaseWeapon&&CombatTiming.Equipped(p) is{} old){p.AddToBackpack(old);}
        if(ClassGear.State(item)==null){ClassGear.Profiles.Convert(item,99,GearRarity.Normal,new Random(173));}
        p.AddToBackpack(item);Require(p.EquipItem(item),"Equip "+item.GetType().Name);ClassGear.Refresh(p);return item;
    }
    private static void Bonus(PlayerMobile p,BaseWeapon w,int ar,int damage,int crit)
    {Require(WeaponMasteries.Bonus(p,w,1)==ar&&WeaponMasteries.Bonus(p,w,2)==damage&&WeaponMasteries.Bonus(p,w,3)==crit,"Levitation bonus triple");}
    private static void Requirements(PlayerMobile p,Item item,int strength,int dexterity)
    {
        Require(WarlockLevitation.TryRequirements(p,item,out int s,out int d)&&s==strength&&d==dexterity,"Owner requirement pair "+item.Name);
        p.AddToBackpack(item);
        using var properties=new ObjectPropertyList(item);item.GetProperties(properties);properties.Terminate();
        Require(properties.Buffer.AsSpan().IndexOf(Encoding.Unicode.GetBytes($"Owner's requirements: Strength {strength}; Dexterity {dexterity}"))>=0,"Missing explicitly owner-labelled requirement preview");
    }
    private static void Goldens()
    {
        foreach(var (rank,ar,ed,crit,req) in new[]{(0,0,0,0,0),(1,40,25,5,-2),(2,45,29,9,-4),(20,135,101,29,-40),(25,160,121,30,-50),(60,335,261,35,-50),(255,1310,1041,35,-50)})
        {Require(WarlockLevitation.Values(rank)==new LevitationBonuses(ar,ed,crit,req),"Pinned Levitation formulas rank="+rank);}
        foreach(var (basis,expected) in new[]{(70,68),(49,48),(25,24),(35,34),(37,36),(0,0)})
        {Require(WarlockLevitation.NormalRequirement(basis,-4)==expected,"Observed signed-truncation requirement control");}
        var p=Fresh();try{Require(!ClassCombat.Available(p,CombatAbility.LevitationMastery)&&WarlockCombat.Implemented(CombatAbility.LevitationMastery),"Passive dispatch exposure");}finally{p.Delete();}
    }
    private static void NativeStats(PlayerMobile p,BaseWeapon w,int ar,int damage)
    {
        int basis=Math.Max(1,5*p.Dex-35+ClassStats.For(DiabloClass.Warlock).AttackBonus+ClassGear.Bonus(p,"att"));
        int other=AosAttributes.GetValue(p,AosAttribute.AttackChance)+(SorceressEffects.Enchantment(p)?.AttackPercent??0)+PaladinAuras.AttackPercent(p);
        Require(ClassStats.AttackRating(p,37)==basis*(100+37+ar+other)/100,"Levitation AR additive pool");
        var raw=ClassGear.Base(w);int stat=ClassStats.WeaponStatPercent(p.Str,p.Dex,raw.StrengthBonus,raw.DexterityBonus);
        int off=p.Items.Where(x=>x!=w&&ClassGear.Active(p,x)).Sum(x=>ClassGear.Bonus(x,"dmg%"))+NamedGear.SetBonus(p,"dmg%");
        Require(ClassStats.DamageBonus(p,w)==stat+off+damage+PaladinAuras.DamagePercent(p),"Levitation additive damage pool");
        var range=ClassStats.WeaponBaseDamage(p);var actual=ClassStats.PhysicalDamageRange(p);
        Require(actual.Min==Math.Max(1,range.Min*(100+stat+off+damage+PaladinAuras.DamagePercent(p))/100),"Levitation physical endpoint");
    }
    private static void Critical(PlayerMobile p)
    {
        var sword=Equip(p,(BaseWeapon)Named("Hellplague"));var amulet=Equip(p,Named("Highlord's Wrath"));var gloves=Equip(p,Named("Steelrend"));
        Require(WeaponMasteries.Bonus(p,sword,3)==29&&ClassGear.Bonus(p,"deadly")>0,"Critical/deadly fixture");
        var first=new Rolls(0);Require(NamedCombat.Deadly(p,first)==2&&first.Calls==1,"Melee mastery short-circuit");
        var second=new Rolls(99,0);Require(NamedCombat.Deadly(p,second)==2&&second.Calls==2,"Melee deadly fallback");
        var enemy=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.SetHits(10000);enemy.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){enemy.SetResistance(type,0);}
        try
        {
            Require(ClassStats.CheckHit(p,enemy,0,()=>0)==true&&ClassStats.CheckHit(p,enemy,0,()=>1)==false,"Actual AR hit/miss");
            var damage=GearEffects.Damage(p,0,enemy);Require(damage.Fire>0&&damage.Lightning>0,"Elemental critical controls");
            foreach(var (m,d,multiple) in new[]{(0,99,2),(99,0,2),(99,99,1)})
            {
                int hp=SkillHealth.Balance256(enemy);GearEffects.Strike(p,enemy,0,new Rolls(m,d),wearWeapon:false);
                Require(hp-SkillHealth.Balance256(enemy)==NamedElements.To256(damage.Physical*multiple+damage.Fire+damage.Cold+damage.Lightning+damage.Magic),"Critical physical-only native damage");TimedEffects.ClearGear(enemy);
            }
        }
        finally{enemy.Delete();sword.Delete();amulet.Delete();gloves.Delete();}
        var bow=Equip(p,new Bow());Bonus(p,bow,135,101,29);NativeStats(p,bow,135,101);
        var projectile=new Rolls(99,0);Require(NamedCombat.Deadly(p,projectile)==2&&projectile.Calls==2,"Bow generic-zero then mastery critical");
        var failure=new Rolls(99,99);Require(!NamedCombat.ProjectileCritical(p,failure)&&failure.Calls==2,"Projectile failure/draw order");bow.Delete();
        var thrown=Equip(p,new NamedThrowingKnife());Bonus(p,thrown,0,0,0);thrown.Delete();
        var naked=new Rolls(0);Require(NamedCombat.Deadly(p,naked)==1&&naked.Calls==0,"Unarmed mastery");ClassGear.Refresh(p);
        foreach(string source in new[]{"Steeldriver","Ghostflame"})
        {
            var unsupported=Named(source);p.AddToBackpack(unsupported);
            Require(!WarlockLevitation.TryRequirements(p,unsupported,out _,out _)&&ClassGear.CanEquip(unsupported,p)==false&&!p.EquipItem(unsupported),"Unresolved requirement modifier silently accepted: "+source);unsupported.Delete();
        }
    }
    private static void Boundaries()
    {
        var p=Fresh(strength:68,dexterity:48);Train(p,1);var helm=Equip(p,Named("Tarnhelm"));
        var sword=(BaseWeapon)Named("Ripsaw");Require(ClassGear.Base(sword).Strength==70&&ClassGear.Base(sword).Dexterity==49,"Real Flamberge base");
        Requirements(p,sword,68,48);Require(p.EquipItem(sword)&&sword.StrRequirement==68&&sword.DexRequirement==48,"First weapon native boundary equip");
        var other=Fresh(strength:68,dexterity:48,type:DiabloClass.Necromancer);
        Require(ClassGear.CanEquip(sword,other)==false&&!other.EquipItem(sword)&&sword.Parent==p,"Recipient inherited previous owner's mastery");
        var npc=new Ogre();npc.SetStr(68);npc.SetDex(48);Require(ClassGear.CanEquip(sword,npc)==false,"NPC inherited mastery");npc.Delete();other.Delete();
        p.AddToBackpack(sword);p.AddToBackpack(helm);ClassGear.Refresh(p);Require(!p.EquipItem(sword),"Rank1 equip ignored higher requirements");
        Require(p.EquipItem(helm),"Boundary helm restore");ClassGear.Refresh(p);p.RawStr=67;Require(!p.EquipItem(sword),"Below Strength boundary accepted");
        p.RawStr=68;p.RawDex=47;Require(!p.EquipItem(sword),"Below Dexterity boundary accepted");p.RawDex=48;Require(p.EquipItem(sword),"Exact boundary restore");
        var bow=new CompositeBow();ClassGear.Profiles.Convert(bow,99,GearRarity.Normal,new Random(173));Requirements(p,bow,24,34);bow.Delete();
        // Rusthandle is deliberately unavailable until its separate oskill
        // effects work. Check its pinned base/math without bypassing that gate.
        Require(EquipmentBalance.Base("gsc").Strength==37&&WarlockLevitation.NormalRequirement(37,-4)==36,"Reported Grand Scepter requirement");
        p.AddToBackpack(sword);var state=ClassGear.State(sword);ClassGear.Profiles.Store(sword,state with{Identified=false});Require(!p.EquipItem(sword),"Unidentified mastery equip accepted");ClassGear.Profiles.Store(sword,state);
        Require(p.EquipItem(sword),"Identified equip restore");int durability=sword.HitPoints;sword.HitPoints=0;Bonus(p,sword,0,0,0);sword.HitPoints=durability;Bonus(p,sword,45,29,9);
        p.AddToBackpack(sword);p.AddToBackpack(helm);Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Necromancer),"History exit");Require(!WarlockLevitation.TryRequirements(p,sword,out _,out _)&&!p.EquipItem(sword),"Inactive history retained requirements");
        Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Warlock),"History restore");
        ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(!WarlockLevitation.Applies(p,sword),"Respec retained mastery");p.Delete();
    }
    private static void Check()
    {
        Goldens();Boundaries();var saved=new Saved[2];int i=0;
        foreach(bool female in new[]{false,true})
        {
            var p=Fresh(female);Train(p,20);Require(!ClassCreation.Profiles.TryTrain(p.Serial,"warlock.levitationmastery",out _),"Hard cap20");
            Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.LevitationMastery)&&!SkillBindings.Instance.AssignQuick(p,0,CombatAbility.LevitationMastery),"Passive binding accepted");
            Critical(p);var sword=Equip(p,new Longsword());Bonus(p,sword,135,101,29);var helm=Equip(p,Named("Tarnhelm"));Bonus(p,sword,140,105,29);NativeStats(p,sword,140,105);
            var range=ClassStats.PhysicalDamageRange(p);saved[i++]=new(p.Serial.Value,sword.Serial.Value,helm.Serial.Value,female,ClassStats.AttackRating(p),ClassStats.DamageBonus(p,sword),range.Min,range.Max);
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        Goldens();foreach(var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var sword=(BaseWeapon)World.FindItem((Serial)s.Weapon);var helm=World.FindItem((Serial)s.Helm);ClassGear.Refresh(p);
            Require(p.Female==s.Female&&sword.Parent==p&&helm.Parent==p&&SkillMechanics.Rank(p,CombatAbility.LevitationMastery)==21,"Saved Warlock/Witch ranks and gear");Bonus(p,sword,140,105,29);NativeStats(p,sword,140,105);
            var range=ClassStats.PhysicalDamageRange(p);Require(ClassStats.AttackRating(p)==s.Attack&&ClassStats.DamageBonus(p,sword)==s.Damage&&range.Min==s.Minimum&&range.Max==s.Maximum,"Saved native payload");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Bonus(p,sword,135,101,29);Require(p.EquipItem(helm),"Reload helm restore");ClassGear.Refresh(p);Bonus(p,sword,140,105,29);
        }
    }
    private static void Validation()
    {
        var p=Fresh(strength:68,dexterity:48);Train(p,1);var helm=Equip(p,Named("Tarnhelm"));var sword=Equip(p,(BaseWeapon)Named("Ripsaw"));
        p.AddToBackpack(helm);ClassGear.Refresh(p);
        Timer.StartTimer(TimeSpan.FromMilliseconds(150),()=>
        {try{Require(sword.Parent==p.Backpack&&WeaponMasteries.Bonus(p,sword,1)==0,"Loss of soft rank did not trigger native equipment validation");p.Delete();Log.Information("levitation-validation-result: passed");}catch(Exception e){Log.Error(e,"levitation-validation-result: failed");}});
    }
}
