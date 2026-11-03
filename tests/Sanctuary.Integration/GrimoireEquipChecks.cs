// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary.Integration;

public static class GrimoireEquipChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrimoireEquipChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Book,bool Female,int Mana,int Life,int DamageMin,int DamageMax,string Capture,string Gear,int Durability);
    private sealed class High:Random{public override int Next(int max)=>Math.Max(0,max-1);}
    private sealed class Veto(bool raises):ISpell
    {
        public bool BlocksMovement=>false;public bool IsCasting=>false;
        public bool OnCasterEquipping(Item item){if(raises){throw new InvalidOperationException("Grimoire late veto fixture");}return false;}
        public void OnCasterHurt(){}public void OnCasterKilled(){}public void OnConnectionChanged(){}public void FinishSequence(){}
        public bool OnCasterMoving(Direction d)=>true;public bool OnCasterUsingObject(IEntity e)=>true;public bool OnCastInTown(Region r)=>true;
    }
    private static string Manifest=>Path.Join(Core.BaseDirectory,"grimoire-equip-fixtures.json");
    public static void Configure(){Register("grimoire-equip-check",Check);Register("grimoire-equip-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original Grimoire stats and atomic innate hand pairing",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh(bool female=false,bool veto=false,DiabloClass kind=DiabloClass.Warlock,int strength=200,int dexterity=160)
    {
        PlayerMobile p=veto?new DualEquipChecks.RejectPlayer():new PlayerMobile();p.Body=female?0x191:0x190;p.Female=female;p.Player=true;p.Name="GrimoireEquipFixture";p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,kind);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<strength){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Grimoire Strength allocation");}
        while(p.RawDex<dexterity){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Grimoire Dexterity allocation");}
        FixtureSpace.Place(p,new(6100,1710,0),Map.Trammel);ClassGear.Refresh(p);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p));NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    private static T White<T>(PlayerMobile p,T item)where T:Item
    {ClassGear.Profiles.Convert(item,99,GearRarity.Normal,new Random(173));Require(p.AddToBackpack(item),"White gear placement");return item;}
    private static WarlockGrimoire Book(PlayerMobile p,string automatic="of Shadows",GrimoireGrant[] grants=null)
    {
        var book=WarlockGrimoire.CreatePrepared(new("wa1",99,GrimoirePrepared.Base("wa1").MaximumDefense,automatic,grants??[]));
        Require(p.AddToBackpack(book),"Grimoire placement");return book;
    }
    private static void Pair(PlayerMobile p,BaseWeapon weapon,WarlockGrimoire book)
    {
        Require(weapon.Parent==p&&book.Parent==p&&weapon.Layer==Layer.OneHanded&&book.Layer==Layer.TwoHanded
            &&ClassGear.Active(p,weapon)&&ClassGear.Active(p,book)&&WarlockHands.CanPair(p,weapon,book)
            &&WeaponHands.Pair(p)==null&&CombatTiming.Equipped(p)==weapon&&WeaponHands.TwoHanded(weapon),"Innate pair lost active stats,primary weapon or two-handed damage");
        var source=ClassGear.Base(weapon);var raw=ClassGear.Damage(weapon);
        Require(raw.Min==source.TwoHandMin&&raw.Max==source.TwoHandMax,"Hybrid sword used one-handed endpoints beside a Grimoire");
    }
    private static void Failures()
    {
        var p=(DualEquipChecks.RejectPlayer)Fresh(veto:true);var weapon=White(p,new CrescentBlade());var book=Book(p);
        try
        {
            Require(p.EquipItem(weapon),"Rollback primary equip");var layer=book.Layer;var parent=book.Parent;var location=book.Location;
            int str=p.Str,mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(p);long clock=p.NextCombatTime;
            p.Reject=true;Require(!p.EquipItem(book),"Native CheckEquip veto ignored");p.Reject=false;
            Require(book.Layer==layer&&book.Parent==parent&&weapon.Layer==Layer.TwoHanded&&!DualEquip.Deferred(p),"Grimoire companion layer did not roll back");
            foreach(bool raises in new[]{false,true})
            {
                bool raised=false;book.QuestItem=true;p.Spell=new Veto(raises);
                try{Require(!p.EquipItem(book),"Late spell veto ignored");}catch(InvalidOperationException e)when(e.Message=="Grimoire late veto fixture"){raised=true;}
                finally{p.Spell=null;}
                Require(raised==raises&&book.Layer==layer&&book.Parent==parent&&book.Location==location&&book.QuestItem&&weapon.Layer==Layer.TwoHanded
                    &&p.Str==str&&p.NextCombatTime==clock&&NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(p)==life&&!DualEquip.Deferred(p),"Late Grimoire failure leaked state,resources or transaction");book.QuestItem=false;
            }
            Require(p.EquipItem(book),"Rollback retry failed");Pair(p,weapon,book);
            var shield=White(p,new HeaterShield());Require(!p.EquipItem(shield)&&shield.Parent==p.Backpack,"Third hand accepted");
            p.AddToBackpack(book);Require(weapon.Layer==Layer.TwoHanded&&!p.EquipItem(shield),"Ordinary shield admitted with a Warlock two-handed sword");
            p.AddToBackpack(weapon);Require(p.EquipItem(shield)&&!p.EquipItem(weapon)&&weapon.Parent==p.Backpack,"Reverse ordinary-shield restriction failed");
            p.AddToBackpack(shield);var one=White(p,new Longsword());Require(p.EquipItem(one)&&p.EquipItem(shield),"Ordinary one-hand/shield equipment regressed");
        }
        finally{p.Delete();}
    }
    private static void Requirements()
    {
        var p=Fresh(strength:68,dexterity:48);var book=Book(p,grants:[new(383,2)]);
        var weapon=(BaseWeapon)NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Ripsaw").Id,99,new Random(173),true);p.AddToBackpack(weapon);
        try
        {
            Require(NamedSkills.Rank(p,"warlock.levitationmastery")==0&&!p.EquipItem(weapon),"Backpack staffmod altered first-equip requirements");
            Require(p.EquipItem(book)&&NamedSkills.Rank(p,"warlock.levitationmastery")==2&&p.EquipItem(weapon),"Active Grimoire staffmod did not reach first-equip Levitation requirements");
            Require(weapon.StrRequirement==68&&weapon.DexRequirement==48&&ClassCreation.Profiles.GetRank(p.Serial,"warlock.levitationmastery")==0,"Grimoire spent hard points or changed requirement truncation");
            p.AddToBackpack(weapon);p.AddToBackpack(book);var other=Fresh(kind:DiabloClass.Necromancer);other.Backpack.DropItem(book);
            try{Require(!other.EquipItem(book)&&book.Parent==other.Backpack,"Non-Warlock equipped a Grimoire");}finally{p.Backpack.DropItem(book);other.Delete();}
            var unprepared=new WarlockGrimoire();try{Require(!unprepared.CanEquip(p)&&!ClassGear.Converted(unprepared),"Unprepared native book acquired invented stats");}finally{unprepared.Delete();}
        }
        finally{p.Delete();}
    }
    private static void Properties()
    {
        var p=Fresh();var weapon=White(p,new CrescentBlade());Require(p.EquipItem(weapon),"Property primary equip");
        var target=FireBoltChecks.Target(p,8);
        try
        {
            foreach(var automatic in GrimoirePrepared.AllAutomatic)
            {
                var book=Book(p,automatic.Name,[new(374,2)]);
                try
                {
                    Require(p.EquipItem(book),"Property Grimoire equip");int baseline=ClassCreation.Profiles.GetRank(p.Serial,"warlock.demonicmastery");
                    Require(NamedSkills.Rank(p,"warlock.demonicmastery")==baseline+2,"Direct staffmod missing or counted twice");
                    var context=new ItemStatContext(book,99);var elements=context.Elements();
                    Require(elements.MagicMin==automatic.MagicMinimum*256&&elements.MagicMax==automatic.MagicMaximum*256
                        &&elements.FireMin==automatic.FireMinimum*256&&elements.FireMax==automatic.FireMaximum*256,"Shared original-item elemental endpoints changed");
                    var frozen=FrozenEquipmentEffects.Capture(book);Require(FrozenEquipmentEffects.Properties(frozen).Length==(automatic.MagicMaximum>0?2:3),"Frozen item lost inherent properties");
                    var damage=GearEffects.Damage(p,.5,target);Require(damage.Magic==(automatic.MagicMinimum+automatic.MagicMaximum)/2.0
                        &&damage.Fire==(automatic.FireMinimum+automatic.FireMaximum)/2.0,"Player weapon path lost or doubled Grimoire elements");
                    int before=SkillHealth.Balance256(target);var hit=GearEffects.PrepareWeaponImpact(p,target,.5,new High(),damage);
                    GearEffects.Strike(p,target,.5,new High(),wearWeapon:false,preparedImpact:hit);
                    Require(before-SkillHealth.Balance256(target)==NamedElements.To256(hit.Parts.Total),"Native typed Grimoire weapon impact disagrees with prepared packet");
                    TimedEffects.ClearGear(target);
                    int durability=book.HitPoints;book.HitPoints=0;Require(!ClassGear.Active(p,book)&&NamedSkills.Rank(p,"warlock.demonicmastery")==baseline
                        &&NamedGear.Elemental(p,"magic",.5)==0&&NamedGear.Elemental(p,"fire",.5)==0,"Broken Grimoire kept bonuses");book.HitPoints=durability;
                    var original=ClassGear.State(book);ClassGear.Profiles.Store(book,original with{Identified=false});Require(!ClassGear.Active(p,book)&&NamedSkills.Rank(p,"warlock.demonicmastery")==baseline,"Unidentified Grimoire kept a skill grant");ClassGear.Profiles.Store(book,original);
                    var copy=new WarlockGrimoire();book.Dupe(copy);
                    try{Require(copy.SavedText==book.SavedText&&JsonSerializer.Serialize(ClassGear.State(copy))==JsonSerializer.Serialize(original),"Native item copy lost capture or ordinary gear state");}finally{copy.Delete();}
                    var gear=JsonSerializer.Serialize(original);bool refused=false;
                    try{ClassGear.Profiles.Store(book,original with{Defense=original.Defense+1});}catch(InvalidDataException){refused=true;}
                    Require(refused&&JsonSerializer.Serialize(ClassGear.State(book))==gear,"Invalid gear replacement committed before refusal");
                }
                finally{book.Delete();}
            }
        }
        finally{target.Delete();p.Delete();}
    }
    private static Saved Scene(bool female)
    {
        var p=Fresh(female);var weapon=White(p,new CrescentBlade());var book=Book(p);
        try
        {
            Require(NamedSkills.Rank(p,"warlock.levitationmastery")==0,"Innate fixture has Mastery");
            Require(female?p.EquipItem(book)&&p.EquipItem(weapon):p.EquipItem(weapon)&&p.EquipItem(book),"Innate pair equip order failed");Pair(p,weapon,book);
            int block=int.Parse(GrimoireCaptureChecks.Rows("grimoire-charstats-source.txt").Single(r=>r["class"]=="Warlock")["BlockFactor"]);
            Require(block==25&&ClassGear.BlockChance(p)==Math.Clamp((GrimoirePrepared.Base(book.Capture.Code).Block+block)*(p.Dex-15)/(2*99),0,75),"Warlock Grimoire class block base differs from owned table");
            p.AddToBackpack(book);Require(weapon.Layer==Layer.TwoHanded&&WeaponHands.TwoHanded(weapon),"Grimoire removal changed two-handed damage");Require(p.EquipItem(book),"Original Grimoire reequip");Pair(p,weapon,book);
            book.HitPoints=17;var damage=ClassGear.Damage(weapon);var saved=new Saved(p.Serial.Value,weapon.Serial.Value,book.Serial.Value,female,
                NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),damage.Min,damage.Max,book.SavedText,JsonSerializer.Serialize(ClassGear.State(book)),17);
            p.Internalize();p=null;weapon=null;book=null;return saved;
        }
        finally{book?.Delete();weapon?.Delete();p?.Delete();}
    }
    private static void Check(){Failures();Requirements();Properties();File.WriteAllText(Manifest,JsonSerializer.Serialize(new[]{Scene(false),Scene(true)}));}
    private static void Reload()
    {
        foreach(var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var weapon=(BaseWeapon)World.FindItem((Serial)saved.Weapon);var book=(WarlockGrimoire)World.FindItem((Serial)saved.Book);
            Pair(p,weapon,book);Require(p.Female==saved.Female&&book.SavedText==saved.Capture&&JsonSerializer.Serialize(ClassGear.State(book))==saved.Gear&&book.HitPoints==saved.Durability
                &&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(p)==saved.Life&&ClassGear.Damage(weapon)==(saved.DamageMin,saved.DamageMax),"Original equipped Warlock/Witch pair,capture,durability or resources lost on reload");
            p.AddToBackpack(book);Require(weapon.Layer==Layer.TwoHanded,"Saved pair removal failed");Require(p.EquipItem(book),"Saved original pair reequip failed");Pair(p,weapon,book);
        }
    }
}
