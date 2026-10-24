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

public static class ThrowingQuantityChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ThrowingQuantityChecks));
    private sealed class Maximum:Random{public override int Next(int maximum)=>maximum-1;public override double NextDouble()=>.99999;}
    private sealed record SavedItem(uint Serial,string Code,int Maximum,int Remaining,DateTime DueUtc);
    private sealed record Saved(uint Owner,SavedItem[] Items);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"throwing-quantity.json");
    public static void Configure(){Register("throwing-quantity-check",Check);Register("throwing-quantity-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Ordinary/named source quantity caps, real throw and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"throwing-quantity-source-probe.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            if(row[4].GetInt32()!=0){continue;}var source=NamedAmmunition.Source(row[0].GetString());
            Require((source.Minimum,source.Maximum,source.Spawn)==(row[1].GetInt32(),row[2].GetInt32(),row[3].GetInt32()),"Complete source getter/native base metadata differential");count++;
        }
        Require(count==36,"Complete stack projection row count");Log.Information("Throwing quantities:36 complete source/native base getter rows;216 older extra-stack cap controls remain separately bounded");
    }
    private static BaseWeapon Drop(string type,string code,GearRarity rarity)
    {
        var item=ItemOrigins.Create(ItemOrigin.Drop,()=>EquipmentCrafting.Resolve(type).CreateInstance<BaseWeapon>());
        ClassGear.Profiles.FromMonster(item,30,rarity,code,GearTier.Normal,new Random(173));return item;
    }
    private static void RealThrow(PlayerMobile owner,BaseWeapon weapon)
    {
        if(CombatTiming.Equipped(owner) is{} old && old!=weapon){owner.AddToBackpack(old);}Require(owner.EquipItem(weapon),"Ordinary throwing axe equip");ClassGear.Refresh(owner);
        var id=SkillMechanics.For(CombatAbility.ThrowingMastery).TalentId;while(ClassCreation.Profiles.GetRank(owner.Serial,id)<1){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Throw mastery prerequisite");}
        Require(BarbarianThrowing.Supported(owner),"Ordinary throwing weapon did not reach shared mastery consumer");
        while(NamedAmmunition.Remaining(weapon)>1){Require(NamedAmmunition.Consume(weapon,TimedEffects.Now),"Ordinary depletion failed");}
        var original=BarbarianThrowing.RandomSource;BarbarianThrowing.RandomSource=()=>new Maximum();
        try
        {
            ClassCombat.Stop(owner);var session=ClassCombat.For(owner);session.NextAction=0;
            var point=new[]{new Point3D(owner.X+3,owner.Y,owner.Z),new Point3D(owner.X,owner.Y+3,owner.Z),new Point3D(owner.X-3,owner.Y,owner.Z)}.First(x=>owner.InLOS(new Point3D(x.X,x.Y,x.Z+14)));
            Require(ClassCombat.Perform(owner,CombatAbility.BasicAttack,null,point,Core.TickCount),"Ordinary last-charge throw start");
            Require(NamedAmmunition.Remaining(weapon)==1 && !ClassCombat.ResolveAction(owner,session.EffectAt-1),"Ordinary windup consumed charge");
            Require(ClassCombat.ResolveAction(owner,session.EffectAt) && NamedAmmunition.Remaining(weapon)==0,"Ordinary release failed once-only debit");
            Require(!ClassCombat.ResolveAction(owner,session.EffectAt+1) && !NamedAmmunition.Consume(weapon,TimedEffects.Now),"Ordinary empty stack duplicated/debited charge");
        }
        finally{BarbarianThrowing.RandomSource=original;ClassCombat.Stop(owner);}
    }
    private static void Check()
    {
        Differential();var owner=MagicArrowChecks.Fresh("ThrowQuantityOwner",DiabloClass.Barbarian);var items=new System.Collections.Generic.List<BaseWeapon>();
        foreach(var (type,code,cap) in new[]{("Boomerang","tax",200),("Cyclone","bal",200),("SoulGlaive","glv",60)})
        {
            var item=Drop(type,code,GearRarity.Normal);owner.AddToBackpack(item);Require(NamedAmmunition.Maximum(item)==cap && item.Amount==1,"Mapped ordinary cap/item identity");
            for(int i=0;i<3;i++){Require(NamedAmmunition.Consume(item,TimedEffects.Now),"Ordinary charge debit");}
            var count=NamedAmmunition.Remaining(item);NamedAmmunition.Track(item);ClassGear.Profiles.Store(item,ClassGear.State(item) with{Identified=true});Require(NamedAmmunition.Remaining(item)==count,"Repeated tracking/gear store refilled ordinary stack");items.Add(item);
        }
        foreach(var rarity in new[]{GearRarity.Magic,GearRarity.Rare})
        {var item=Drop("Boomerang","tax",rarity);owner.AddToBackpack(item);Require(NamedAmmunition.Maximum(item)==200,"Magic/rare ordinary base required named row");NamedAmmunition.Consume(item,TimedEffects.Now);items.Add(item);}
        var named=(BaseWeapon)NamedGear.Create("unique/335",99,new Maximum(),true);owner.AddToBackpack(named);Require(NamedAmmunition.Maximum(named)==330,"Actual Gimmershred base270 plus stack60 changed");NamedAmmunition.Consume(named,TimedEffects.Now);items.Add(named);
        var novice=new PlayerMobile{Body=0x190,Player=true,Name="ThrowQuantityNovice"};novice.AddItem(new Backpack());ClassCreation.Profiles.Assign(novice,DiabloClass.Barbarian);novice.MoveToWorld(owner.Location,owner.Map);
        Require(ClassGearHooks.IgnoreAppearanceRestrictions(items[0]) && ClassGear.CanEquip(items[0],novice)==false && !novice.EquipItem(items[0]),"Appearance bypass removed pinned attribute requirements");novice.Delete();
        RealThrow(owner,items[0]);
        var clone=new Boomerang();owner.AddToBackpack(clone);ClassGear.Profiles.Copy(items[0],clone);Require(NamedAmmunition.Maximum(clone)==200 && NamedAmmunition.Remaining(clone)==0,"Copied empty ordinary stack refilled");clone.Delete();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,items.Select(item=>new SavedItem(item.Serial.Value,ClassGear.State(item).BaseCode,NamedAmmunition.Maximum(item),NamedAmmunition.Remaining(item),NamedAmmunition.Quantities[item.Serial].DueUtc)).ToArray())));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);Require(owner is{Alive:true,Deleted:false},"Saved quantity owner missing");
        foreach(var row in saved.Items)
        {
            var item=(BaseWeapon)World.FindItem((Serial)row.Serial);Require(item is{Deleted:false,Amount:1} && ClassGear.State(item).BaseCode==row.Code && NamedAmmunition.Maximum(item)==row.Maximum && NamedAmmunition.Remaining(item)==row.Remaining && NamedAmmunition.Quantities[item.Serial].DueUtc==row.DueUtc,"Saved ordinary/named count/cap/clock/item identity changed");
            if(item.Parent==owner){owner.AddToBackpack(item);}NamedAmmunition.Track(item);Require(NamedAmmunition.Remaining(item)==row.Remaining,"Trade/tracking replenished saved quantity");
        }
        var first=(BaseWeapon)World.FindItem((Serial)saved.Items[0].Serial);Require(owner.EquipItem(first),"Restored ordinary throwing equip");ClassGear.Refresh(owner);Require(!NamedAmmunition.CanAttack(first) && BarbarianThrowing.Release(owner,TimedEffects.Now,new Maximum())==null,"Saved empty stack allowed throw");
    }
}
