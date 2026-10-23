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

public static class AmazonPenetrateChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AmazonPenetrateChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Amulet,int Rating,Point3D Origin);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"amazon-penetrate-fixtures.json");
    public static void Configure(){Register("amazon-penetrate-check",Check);Register("amazon-penetrate-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Amazon Penetrate additive AR, aura coexistence and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"amazon-penetrate-source-probe.json")));int count=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {Require(ClassStats.PlayerAttackPercent(row[0].GetInt32(),row[1].GetInt32()+row[2].GetInt32())==row[3].GetInt32(),"Compiled player AR differential");count++;}
        Require(count==120,"Incomplete source AR fixture");var source=SkillMechanics.For(CombatAbility.Penetrate);
        Require(source.Passive && source.Text("passivestat1")=="item_tohit_percent" && !source.SourceFields.ContainsKey("passiveitype"),"Pinned passive metadata");
        for(int rank=1;rank<=20;rank++)
        {Require(new SkillValues(source,_=>rank,_=>rank,99).Eval("passivecalc1")==25+rank*10,"Official Penetrate AR golden "+rank);}
        Log.Information("Amazon Penetrate:120 compiled source AR cases and20 official rank goldens passed");
    }
    private static PlayerMobile Fresh()
    {
        var owner=new PlayerMobile{Body=0x190,Name="AmazonPenetrateFixture",Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);
        FixtureSpace.Place(owner,new(2400,1200,0),Map.Trammel);
        while(owner.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Fixture strength");}
        while(owner.RawDex<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Fixture dexterity");}
        owner.Hits=owner.HitsMax;owner.Mana=0;return owner;
    }
    private static void Train(PlayerMobile owner,int rank)
    {
        if(ClassCreation.Profiles.GetRank(owner.Serial,"amazon.criticalstrike")==0){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"amazon.criticalstrike",out _),"Critical prerequisite");}
        while(ClassCreation.Profiles.GetRank(owner.Serial,"amazon.penetrate")<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"amazon.penetrate",out _),"Cannot train Penetrate");}
    }
    private static Item Named(PlayerMobile owner,string source)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);Require(owner.EquipItem(item),"Cannot equip "+source);ClassGear.Refresh(owner);return item;}
    private static BaseWeapon Equip(PlayerMobile owner,BaseWeapon weapon)
    {
        if(CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}
        ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(weapon),"Cannot equip weapon");ClassGear.Refresh(owner);return weapon;
    }
    private static void Rating(PlayerMobile owner,int passive,int skill=0)
    {
        int basis=Math.Max(1,5*owner.Dex-35+ClassStats.For(DiabloClass.Amazon).AttackBonus+ClassGear.Bonus(owner,"att"));
        int other=AosAttributes.GetValue(owner,AosAttribute.AttackChance)+(SorceressEffects.Enchantment(owner)?.AttackPercent??0)+PaladinAuras.AttackPercent(owner)+skill;
        Require(AmazonCombat.AttackPercent(owner)==passive && ClassStats.AttackRating(owner,skill)==basis+basis*(other+passive)/100,"Penetrate missing or multiplied the existing AR percentage pool");
    }
    private static void Coexist(PlayerMobile owner)
    {
        var aura=ClassSkillChecks.Player(DiabloClass.Paladin);aura.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);
        var caster=ClassSkillChecks.Player(DiabloClass.Sorceress);caster.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);
        try
        {
            Require(SkillBindings.Instance.Assign(aura,false,CombatAbility.BlessedAim) && PaladinAuras.Effect(owner,CombatAbility.BlessedAim)!=null,"Native Blessed Aim lease");
            int passive=AmazonCombat.AttackPercent(owner);Rating(owner,passive);Rating(owner,passive,80);
            SorceressEffects.CastEnchant(caster,owner,new(SkillMechanics.For(CombatAbility.Enchant),_=>1,_=>1,99));
            Require(SorceressEffects.Enchantment(owner)?.AttackPercent>0,"Real Enchant attack bonus");Rating(owner,passive);Rating(owner,passive,80);
            Require(SkillBindings.Instance.Assign(aura,false,CombatAbility.Fanaticism),"Switch to Fanaticism");Rating(owner,passive,80);
        }
        finally{PaladinAuras.Clear(owner);PaladinAuras.Clear(aura);SorceressEffects.Profiles.Clear(owner);aura.Delete();caster.Delete();}
    }
    private static void Boundary(PlayerMobile owner)
    {
        var plainOwner=Fresh();var enemy=new Ogre{NoKillAwards=true,RawDex=40000,Frozen=true,CantWalk=true};enemy.AIObject.AITimer.Stop();enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        try
        {
            int defense=NamedCombat.DefenseFor(owner,enemy,ClassStats.Defense(enemy));int enhanced=ClassStats.AttackRatingAgainst(owner,enemy);
            double hit=ClassStats.HitChance(enhanced,defense,ClassStats.Level(owner),ClassStats.Level(enemy));
            double plain=ClassStats.HitChance(ClassStats.AttackRatingAgainst(plainOwner,enemy),defense,ClassStats.Level(plainOwner),ClassStats.Level(enemy));
            Require(hit>plain+.01,"No native AR hit-boundary contrast");double boundary=(hit+plain)/2;
            Require(AmazonCombat.AttackPercent(plainOwner)==0 && ClassStats.CheckHit(plainOwner,enemy,0,()=>boundary)==false,"Unlearned native hit boundary");
            Require(ClassStats.CheckHit(owner,enemy,0,()=>boundary)==true && ClassStats.CheckHit(owner,enemy,0,()=>boundary,projectile:true)==true,"Melee/projectile hit consumer lost passive");
            Train(plainOwner,1);Require(AmazonCombat.AttackPercent(plainOwner)==35,"Respec fixture learned rank");
            ClassCreation.Profiles.TryGet(plainOwner.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(plainOwner,profile);Require(AmazonCombat.AttackPercent(plainOwner)==0,"Respec retained passive");
        }
        finally{enemy.Delete();plainOwner.Delete();}
    }
    private static void Check()
    {
        Differential();var owner=Fresh();Require(AmazonCombat.AttackPercent(owner)==0,"Unlearned passive granted");Rating(owner,0);
        var helm=Named(owner,"Tarnhelm");Require(AmazonCombat.AttackPercent(owner)==0,"Allskills granted unlearned passive");Train(owner,1);Rating(owner,45);
        owner.AddToBackpack(helm);ClassGear.Refresh(owner);Rating(owner,35);
        Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.Penetrate) && !SkillBindings.Instance.Assign(owner,false,CombatAbility.Penetrate)
            && !SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Penetrate),"Passive accepted binding");
        var bow=Equip(owner,new Bow());Rating(owner,35);bow.Delete();var weapon=Equip(owner,new Dagger());Rating(owner,35);
        var mahim=Named(owner,"The Mahim-Oak Curio");Require(AosAttributes.GetValue(owner,AosAttribute.AttackChance)>0,"Real item AR percentage");Rating(owner,35,80);Coexist(owner);mahim.Delete();ClassGear.Refresh(owner);
        Train(owner,20);Rating(owner,225);Require(!ClassCreation.Profiles.TryTrain(owner.Serial,"amazon.penetrate",out _),"Hard cap exceeded");
        Require(owner.EquipItem(helm),"Helm restore");ClassGear.Refresh(owner);Rating(owner,235);var amulet=Named(owner,"Highlord's Wrath");Rating(owner,245);
        owner.AddToBackpack(helm);ClassGear.Refresh(owner);Rating(owner,235);Require(owner.EquipItem(helm),"Restore soft22");ClassGear.Refresh(owner);Rating(owner,245);
        Coexist(owner);Boundary(owner);Rating(owner,245);Require(owner.Mana==0,"Passive used mana");
        var other=Fresh();Train(other,1);Require(ClassCreation.Profiles.TryActivateHistory(other,DiabloClass.Paladin) && AmazonCombat.AttackPercent(other)==0,"Wrong class passive");
        Require(ClassCreation.Profiles.TryActivateHistory(other,DiabloClass.Amazon) && AmazonCombat.AttackPercent(other)==35,"History restore");other.Delete();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,weapon.Serial.Value,helm.Serial.Value,amulet.Serial.Value,ClassStats.AttackRating(owner),owner.Location)));
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner!=null && World.FindItem((Serial)saved.Weapon)?.Parent==owner && World.FindItem((Serial)saved.Helm)?.Parent==owner
            && World.FindItem((Serial)saved.Amulet)?.Parent==owner && ClassCreation.Profiles.GetRank(owner.Serial,"amazon.penetrate")==20,"Saved rank/gear");
        if(owner.Map==Map.Internal){owner.MoveToWorld(saved.Origin,Map.Trammel);}
        Require(ClassStats.AttackRating(owner)==saved.Rating,"Saved AR/effective passive");Rating(owner,245);Coexist(owner);Boundary(owner);Rating(owner,245);
    }
}
