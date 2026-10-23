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

public static class BowProjectileChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BowProjectileChecks));
    private sealed record Saved(uint Owner,uint Bow,uint Arrow,uint Bolts,uint Bag,int ArrowsLeft,int BoltsLeft);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bow-projectile-fixtures.json");
    public static void Configure()
    {
        Register("bow-projectile-check",Check);Register("bow-projectile-reload-check",Reload);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Bow ammunition and source projectile stats foundation",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static MissileSourceStats Stats(JsonElement values,string[] fields)
    {
        int V(string key)=>values[Array.IndexOf(fields,key)].GetInt32();
        return new(V("nFireMinDamage"),V("nFireMaxDamage"),V("nLightMinDamage"),V("nLightMaxDamage"),
            V("nMagicMinDamage"),V("nMagicMaxDamage"),V("nColdMinDamage"),V("nColdMaxDamage"),V("nColdLength"),
            V("nPoisonMinDamage"),V("nPoisonMaxDamage"),V("nPoisonLength"),V("nPoisonCount"),
            V("nLifeDrainMinDamage"),V("nManaDrainMinDamage"),V("nBurningMin"),V("nBurningMax"),V("nBurnLength"));
    }
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bow-projectile-source-probe.json")));
        var fields=data.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();int count=0;
        foreach(var row in data.RootElement.GetProperty("scaleCases").EnumerateArray())
        {
            var before=Stats(row[3],fields);var expected=Stats(row[4],fields);
            Require(before.Scale(row[0].GetInt32())==expected,"Compiled source typed damage/cold/poison/drain/burn scaling differential");count++;
        }
        Require(count==150,"Incomplete source scale fixture");
        var poison=new MissileSourceStats(PoisonMin:257,PoisonMax:513,PoisonFrames:101,PoisonCount:2);
        var scaled=poison.Scale(96);Require(scaled.PoisonMin==192 && scaled.PoisonMax==384 && scaled.PoisonFrames==101
            && scaled.PoisonCount==1 && poison.PoisonImpactFrames==50 && scaled.PoisonImpactFrames==101,"Poison count/duration metadata lost");
        Log.Information("C# missile source-stat differential: {Count} compiled cases; poison rate/count/aggregate duration retained",count);
    }
    private static BaseWeapon Equip(PlayerMobile owner,BaseWeapon weapon)
    {owner.AddToBackpack(weapon);Require(owner.EquipItem(weapon),"Fixture ranged equip");Require(ClassGear.Active(owner,weapon),"Inactive source ranged fixture");return weapon;}
    private static void Check()
    {
        Differential();var owner=ClassSkillChecks.Player(DiabloClass.Amazon);owner.Name="BowProjectileFixture";
        while(owner.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength");}
        while(owner.RawDex<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Dexterity");}
        if(owner.Backpack==null){owner.AddItem(new Backpack());}var bow=Equip(owner,new Bow());var bag=new Bag();owner.AddToBackpack(bag);
        var wrong=new Bolt(9);owner.AddToBackpack(wrong);
        Require(!BowAmmunition.CanRelease(owner,bow) && !BowAmmunition.Consume(owner,bow) && wrong.Amount==9,"Wrong/no ammunition accepted");
        var arrows=new Arrow(7);bag.DropItem(arrows);
        Require(BowAmmunition.CanRelease(owner,bow) && arrows.Amount==7,"Eligibility debited ammunition");
        int mana=NecromancerEffects.ManaBalance256(owner),wear=bow.HitPoints;
        var fan=FanGeometry.Create(20,0,24,2,1);Require(fan.Length==24 && BowAmmunition.Consume(owner,bow)
            && arrows.Amount==6 && wrong.Amount==9 && bow.HitPoints==wear && NecromancerEffects.ManaBalance256(owner)==mana,"One fan debit touched mana/weapon or consumed per missile");
        owner.AddToBackpack(bow);Require(!BowAmmunition.Consume(owner,bow) && arrows.Amount==6,"Backpack weapon consumed ammo");
        var crossbow=Equip(owner,new Crossbow());Require(BowAmmunition.Consume(owner,crossbow) && wrong.Amount==8 && arrows.Amount==6,"Crossbow did not select bolts");
        owner.AddToBackpack(crossbow);crossbow.Delete();Require(owner.EquipItem(bow),"Re-equip saved bow");
        var external=new Arrow(3);external.MoveToWorld(owner.Location,owner.Map);
        bag.MoveToWorld(owner.Location,owner.Map);Require(!BowAmmunition.CanRelease(owner,bow) && !BowAmmunition.Consume(owner,bow)
            && external.Amount==3 && arrows.Amount==6,"Ground/foreign stack consumed");
        var last=new Arrow(1);owner.AddToBackpack(last);Require(BowAmmunition.Consume(owner,bow) && last.Deleted
            && !BowAmmunition.Consume(owner,bow) && wrong.Amount==8,"Last arrow depletion/wrong-type fallback");
        owner.AddToBackpack(bag);external.Delete();
        int hp=bow.HitPoints;bow.HitPoints=0;Require(!BowAmmunition.Consume(owner,bow) && arrows.Amount==6,"Broken bow consumed ammo");bow.HitPoints=hp;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,bow.Serial.Value,arrows.Serial.Value,wrong.Serial.Value,bag.Serial.Value,6,8)));
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var bow=World.FindItem((Serial)saved.Bow) as BaseWeapon;
        var arrow=World.FindItem((Serial)saved.Arrow);var bolts=World.FindItem((Serial)saved.Bolts);var bag=World.FindItem((Serial)saved.Bag);
        Require(owner!=null && bow?.Parent==owner && arrow?.Parent==bag && bag?.RootParent==owner && bolts?.RootParent==owner
            && arrow.Amount==saved.ArrowsLeft && bolts.Amount==saved.BoltsLeft && BowAmmunition.CanRelease(owner,bow),"Saved native stacks/ownership or ammo replay");
        Require(BowAmmunition.Consume(owner,bow) && arrow.Amount==saved.ArrowsLeft-1 && bolts.Amount==saved.BoltsLeft,"Resumed once-only ammunition debit");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{ArrowsLeft=saved.ArrowsLeft-1}));
    }
}
