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

public static class AmazonCriticalChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AmazonCriticalChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Amulet,uint Target);
    private sealed class Rolls(params int[] values):Random
    {
        public int Calls;
        public override int Next(int maximum){int value=values[Math.Min(Calls++,values.Length-1)];return maximum<=0?0:Math.Clamp(value,0,maximum-1);}
    }
    private static string Manifest=>Path.Join(Core.BaseDirectory,"amazon-critical-fixtures.json");
    public static void Configure(){Register("amazon-critical-check",Check);Register("amazon-critical-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Amazon Critical Strike source ordering, gear, physical delivery and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static void Differential()
    {
        using var melee=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"barbarian-mastery-source-probe.json")));
        int count=0;
        foreach(var row in melee.RootElement.GetProperty("criticalCases").EnumerateArray())
        {
            if(row[4].GetInt32()!=0 || row[5].GetInt32()!=0){continue;}
            var rolls=new Rolls(row[3].GetInt32());
            bool result=NamedCombat.MeleeCritical(row[0].GetInt32(),row[1].GetInt32(),row[2].GetInt32(),rolls);
            Require((result?200:100)==row[6].GetInt32() && rolls.Calls==row[8].GetInt32(),"Compiled melee critical/short-circuit differential");count++;
        }
        Require(count==162,"Incomplete melee source fixture");
        using var missile=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"throwing-projectile-source-probe.json")));
        int[][] patterns=[[0,0,0],[99,0,0],[99,99,0],[99,99,99],[20,35,29],[19,34,28]];count=0;
        foreach(var row in missile.RootElement.GetProperty("criticalCases").EnumerateArray())
        {
            var rolls=new Rolls(patterns[row[3].GetInt32()]);
            bool result=NamedCombat.ProjectileCritical(row[0].GetInt32(),row[1].GetInt32(),row[4].GetInt32()==0?0:row[2].GetInt32(),rolls);
            Require((result?1:0)==row[5].GetInt32() && rolls.Calls==row[6].GetInt32(),"Compiled missile critical/short-circuit differential");count++;
        }
        Require(count==324,"Incomplete missile source fixture");
        int[] chances=[16,25,32,38,42,46,49,51,54,56,58,59,61,62,63,65,65,66,67,68];
        var source=SkillMechanics.For(CombatAbility.CriticalStrike);
        Require(source.Passive && source.Text("passivestat1")=="passive_critical_strike" && !source.SourceFields.ContainsKey("passiveitype"),"Pinned unrestricted passive metadata");
        for(int i=0;i<chances.Length;i++)
        {int rank=i+1;Require(new SkillValues(source,_=>rank,_=>rank,99).Eval("passivecalc1")==chances[i],"Official rank table critical chance "+rank);}
        Log.Information("Amazon critical:162 melee and324 missile compiled-source cases;20 official chance goldens passed");
    }
    private static PlayerMobile Fresh()
    {
        var owner=new PlayerMobile{Body=0x190,Name="AmazonCriticalFixture",Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);
        FixtureSpace.Place(owner,new(2300,1100,0),Map.Trammel);
        while(owner.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Fixture strength");}
        while(owner.RawDex<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Fixture dexterity");}
        owner.Hits=owner.HitsMax;owner.Mana=owner.ManaMax;return owner;
    }
    private static void Train(PlayerMobile owner,int rank)
    {while(ClassCreation.Profiles.GetRank(owner.Serial,"amazon.criticalstrike")<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"amazon.criticalstrike",out _),"Cannot train Critical Strike");}}
    private static Item Named(PlayerMobile owner,string source)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);
        Require(owner.EquipItem(item),"Cannot equip "+source);ClassGear.Refresh(owner);return item;
    }
    private static BaseWeapon Equip(PlayerMobile owner,BaseWeapon weapon)
    {
        if(CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}
        ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(weapon),"Cannot equip weapon");ClassGear.Refresh(owner);return weapon;
    }
    private static int Life(Mobile actor)=>actor.Hits*256-SkillHealth.Remainder256(actor);
    private static void Delivery(PlayerMobile owner,Mobile target)
    {
        Require(AmazonCombat.CriticalChance(owner)==69 && ClassGear.Bonus(owner,"deadly")>0,"Real hard20/soft22 passive and Deadly Strike");
        var damage=GearEffects.Damage(owner,0,target);Require(damage.Physical>0 && damage.Lightning>0,"Physical/real Highlord lightning fixture");
        int mana=NecromancerEffects.ManaBalance256(owner);
        foreach(var (first,second,multiplier,calls) in new[]{(0,99,2,1),(99,0,2,2),(99,99,1,2)})
        {
            var rolls=new Rolls(first,second);int before=Life(target);
            var prepared=GearEffects.PrepareWeaponImpact(owner,target,0,rolls,damage);
            Require(rolls.Calls==calls && prepared.Parts.Physical==damage.Physical*multiplier
                && prepared.Parts.Lightning==damage.Lightning,"Critical changed elements, stacked, or consumed the wrong RNG draws");
            GearEffects.Strike(owner,target,0,new Rolls(99),wearWeapon:false,preparedImpact:prepared);
            Require(before-Life(target)==NamedElements.To256(prepared.Parts.Total),"Native critical physical delivery");
            var projectile=new Rolls(first,second);bool critical=NamedCombat.ProjectileCritical(owner,projectile);
            Require(critical==(multiplier==2) && projectile.Calls==calls,"Learned projectile critical/Deadly Strike consumer");
            var flight=new WeaponMissileDamage(2560,2560,0,0,0,critical,new MissileSourceStats(LightningMin:768,LightningMax:768));
            var impact=flight.Roll(target,new Rolls(0).Next);
            Require(impact.Physical==10*multiplier && impact.Lightning==3,"Saved missile critical must double physical only once");
        }
        Require(NecromancerEffects.ManaBalance256(owner)==mana,"Passive consumed mana");
    }
    private static void Check()
    {
        Differential();var owner=Fresh();Require(AmazonCombat.CriticalChance(owner)==0,"Unlearned passive granted");Train(owner,1);
        Require(AmazonCombat.Implemented(CombatAbility.CriticalStrike) && AmazonCombat.CriticalChance(owner)==16,"Learned unarmed passive");
        Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.CriticalStrike) && !SkillBindings.Instance.Assign(owner,false,CombatAbility.CriticalStrike)
            && !SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.CriticalStrike),"Passive accepted a binding");
        var bow=Equip(owner,new Bow());Require(AmazonCombat.CriticalChance(owner)==16,"Bow lost generic passive");bow.Delete();
        var weapon=Equip(owner,new Dagger());Require(AmazonCombat.CriticalChance(owner)==16,"Dagger lost generic passive");
        var helm=Named(owner,"Tarnhelm");Require(AmazonCombat.CriticalChance(owner)==25,"Actual allskills rank");
        var amulet=Named(owner,"Highlord's Wrath");Require(AmazonCombat.CriticalChance(owner)==32,"Two real soft ranks");
        owner.AddToBackpack(helm);ClassGear.Refresh(owner);Require(AmazonCombat.CriticalChance(owner)==25,"Backpack allskills applied");Require(owner.EquipItem(helm),"Helm restore");ClassGear.Refresh(owner);
        Train(owner,20);Require(AmazonCombat.CriticalChance(owner)==69 && !ClassCreation.Profiles.TryTrain(owner.Serial,"amazon.criticalstrike",out _),"Hard cap or soft22");
        owner.AddToBackpack(amulet);ClassGear.Refresh(owner);Require(AmazonCombat.CriticalChance(owner)==68,"Soft21 curve");owner.AddToBackpack(helm);ClassGear.Refresh(owner);Require(AmazonCombat.CriticalChance(owner)==68,"Hard20 curve");
        Require(owner.EquipItem(helm) && owner.EquipItem(amulet),"Gear restore");ClassGear.Refresh(owner);
        var target=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};target.SetHits(20000);
        target.MoveToWorld(CombatPractice.FindFloor(owner.Map,owner.X+1,owner.Y),owner.Map);target.AIObject.AITimer.Stop();
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){target.SetResistance(type,0);}
        Delivery(owner,target);
        var other=Fresh();Train(other,1);Require(ClassCreation.Profiles.TryActivateHistory(other,DiabloClass.Paladin) && AmazonCombat.CriticalChance(other)==0,"Wrong-class passive");
        Require(ClassCreation.Profiles.TryActivateHistory(other,DiabloClass.Amazon) && AmazonCombat.CriticalChance(other)==16,"History restore");
        ClassCreation.Profiles.TryGet(other.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(other,profile);Require(AmazonCombat.CriticalChance(other)==0,"Respec retained passive");other.Delete();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,weapon.Serial.Value,helm.Serial.Value,amulet.Serial.Value,target.Serial.Value)));
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner!=null && World.FindItem((Serial)saved.Weapon)?.Parent==owner && World.FindItem((Serial)saved.Helm)?.Parent==owner
            && World.FindItem((Serial)saved.Amulet)?.Parent==owner && ClassCreation.Profiles.GetRank(owner.Serial,"amazon.criticalstrike")==20,"Saved ranks/real gear ownership");
        var target=World.FindMobile((Serial)saved.Target);Require(target!=null && target.Alive,"Saved victim");Delivery(owner,target);
    }
}
