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

public static class DragonKickChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DragonKickChecks));
    private sealed class FixedRandom:Random{public override int Next(int max)=>Math.Max(0,max-1);}
    private sealed record Saved(uint Owner,uint[] Boots,uint Primary,uint Secondary,uint Gloves,KickDamageInput Input,MartialCharge Charge);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dragon-kick-fixtures.json");
    public static void Configure(){Register("dragon-kick-check",Check);Register("dragon-kick-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared kick physical component and actual gear persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static KickBootBase[] Bases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"dragon-kick-damage-source-evidence.json")));
        return doc.RootElement.GetProperty("boots").EnumerateArray().Select(x=>new KickBootBase(x.GetProperty("code").GetString(),x.GetProperty("min").GetInt32(),x.GetProperty("max").GetInt32(),x.GetProperty("strength").GetInt32(),x.GetProperty("dexterity").GetInt32())).ToArray();
    }
    private static void Differential()
    {
        var boots=Bases();Require(boots.Length==15,"Pinned fifteen boot bases");
        foreach(var boot in boots)
        {
            var b=EquipmentBalance.Base(boot.Code);
            Require(ClassItemTypes.Is(b.Type,"boot") && DragonKickDamage.Base(boot.Code)==boot,"Current generated boot base: "+boot.Code);
        }
        using var helpers=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"dragon-kick-damage-source-probe.json")));int helperCount=0;
        foreach(var r in helpers.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();int h=V(7),s=V(1)+((h&1)!=0?11:0)+((h&2)!=0?17:0),d=V(2)+((h&1)!=0?3:0)+((h&2)!=0?5:0);
            var input=new KickDamageInput(s,d,13,V(0)==0,V(6),V(3)>=0?boots[V(3)]:null,V(4),V(5),23);
            Require(DragonKickDamage.Body256(input)==(V(8),V(9)) && DragonKickDamage.BootDamage(input)==(V(10),V(11),V(12)),"Complete source kick body/boot helper");helperCount++;
        }
        using var packets=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"dragon-kick-packet-source-probe.json")));int packetCount=0;
        foreach(var r in packets.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();bool inventory=V(9)!=0;int h=inventory?V(8):0,s=V(2)+((h&1)!=0?11:0)+((h&2)!=0?17:0),d=V(3)+((h&1)!=0?3:0)+((h&2)!=0?5:0);
            var input=new KickDamageInput(s,d,13,true,5+7*(V(0)-1)+V(1),inventory && V(4)>=0?boots[V(4)]:null,V(5),V(6),23);int calls=0;
            var range=DragonKickDamage.Range256(input);int actual=DragonKickDamage.Roll256(input,span=>{calls++;Require(span==V(11),"Source packet exclusive span");return V(7);});
            Require(actual==V(10) && range.Max-range.Min==V(11) && calls==1 && input.SkillDamagePercent==V(12) && V(13)==3,"Complete kick physical composition");packetCount++;
        }
        var skill=SkillMechanics.For(CombatAbility.DragonTalon);Require(skill.SourceId==255 && skill.Number("Kick")==1,"Pinned kick identity");
        for(int rank=1;rank<=255;rank++)
        {var values=new SkillValues(skill,_=>rank,_=>0,99);Require(values.Mana256==1536 && values.Formula("toht")==20+35*(rank-1) && values.Eval("calc1")==rank/6+1,"Pinned Talon numeric golden");}
        Require(helperCount==24576 && packetCount==62208,"Incomplete kick source matrices");
        Log.Information("Dragon kick physical:24576 complete body/boot helpers,62208 full physical compositions,15 current boot bases and255 Talon numeric goldens match; no learned kick handler");
    }
    private static PlayerMobile Fresh()
    {
        var p=ClassSkillChecks.Player(DiabloClass.Assassin);p.Name="DragonKickFixture";p.AddItem(new Backpack());
        while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}
        while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}
        return p;
    }
    private static Item Named(PlayerMobile owner,string source)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new FixedRandom(),true);
        owner.AddToBackpack(item);Require(owner.EquipItem(item),"Equip "+source);ClassGear.Refresh(owner);return item;
    }
    private static void RemoveBoots(PlayerMobile owner)
    {foreach(var item in owner.Items.Where(x=>ClassGear.Slot(x)==GearSlot.Feet).ToArray()){owner.AddToBackpack(item);}ClassGear.Refresh(owner);}
    private static void Check()
    {
        Differential();var p=Fresh();var bare=DragonKickDamage.Capture(p,5);Require(bare.Boot==null && bare.MaxDamagePercent==0 && bare.DamagePercent==0,"Barefoot context");
        var first=new Tekagi();ClassGear.Profiles.Convert(first,99,GearRarity.Normal,new Random(173));Require(p.EquipItem(first),"Primary claw");ClassGear.Refresh(p);int strength=p.Str,dexterity=p.Dex;
        var second=(BaseWeapon)Named(p,"Cutthroat1");Require(WeaponHands.Pair(p)!=null && p.Str>strength && p.Dex>dexterity,"Actual offhand nondamage attributes");
        var armed=DragonKickDamage.Capture(p,5);Require(armed.Strength==p.Str && armed.Dexterity==p.Dex && armed.MaxDamagePercent==0,"Weapon ED excluded, weapon nondamage stats retained");
        using(WeaponHands.Use(p,second)){Require(DragonKickDamage.Capture(p,5)==armed,"Primary selection changed kick body/boot ED");}
        uint[] bootIds=new uint[3];int index=0;
        foreach(string source in new[]{"Goblin Toe","Gorerider","Shadowdancer"})
        {
            RemoveBoots(p);var item=Named(p,source);bootIds[index++]=item.Serial.Value;var input=DragonKickDamage.Capture(p,5);var basis=DragonKickDamage.Base(ClassGear.Base(item).Code);
            Require(input.Boot is{} boot && boot.Code==basis.Code && boot.Min==basis.Min && boot.Max==basis.Max && boot.StrengthPercent==120,"Actual boot source identity: "+source);
            var expected=new KickDamageInput(p.Str,p.Dex,99,true,5,Bases().Single(x=>x.Code==basis.Code),0,0,0);
            Require(DragonKickDamage.Range256(input)==DragonKickDamage.Range256(expected),"Actual normal/exceptional/elite boot packet");
            int durability=item is BaseClothing c?c.HitPoints:0;DragonKickDamage.Roll256(input,_=>0);
            Require(item is not BaseClothing c2 || c2.HitPoints==durability,"Preparing kick wore boots");
        }
        var gloves=Named(p,"Steelrend");var withArmor=DragonKickDamage.Capture(p,5);Require(withArmor.MaxDamagePercent==ClassGear.Bonus(gloves,"dmg%") && withArmor.MaxDamagePercent>0 && withArmor.DamagePercent==0,"Actual armor ED must stay outside global lower clamp");
        var charge=new MartialCharge(p.Serial.Value,CombatAbility.TigerStrike,2,3,100,TimedEffects.Now.AddHours(1));AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Append(charge).ToArray());
        int skillDamage=5+AssassinCharges.TigerPercent(charge,SkillMechanics.Rank(p,CombatAbility.TigerStrike));var captured=DragonKickDamage.Capture(p,skillDamage);
        var previous=DragonKickDamage.Capture(p,skillDamage);p.AddToBackpack(gloves);ClassGear.Refresh(p);Require(DragonKickDamage.Capture(p,skillDamage).MaxDamagePercent==0 && DragonKickDamage.Range256(captured)==DragonKickDamage.Range256(previous),"Captured boot packet changed with later armor removal");Require(p.EquipItem(gloves),"Reequip armor ED");ClassGear.Refresh(p);
        Require(AssassinCharges.State(p)==charge && DragonKickDamage.Capture(p,skillDamage)==captured,"Preparation consumed/refreshed charge or altered gear");
        Require(!AssassinCombat.Implemented(CombatAbility.DragonTalon),"Prepared component must not enable unsupported kick cast");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,bootIds,first.Serial.Value,second.Serial.Value,gloves.Serial.Value,captured,charge)));
        Log.Information("Dragon kick live capture:actual Bartuc nondamage attributes, selected hand,normal/exceptional/elite boots,Steelrend ED and immutable charge/packet controls pass");
    }
    private static void Reload()
    {
        Differential();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        Require(AssassinCharges.State(p)==s.Charge && CombatTiming.Equipped(p)?.Serial.Value==s.Primary && WeaponHands.Pair(p)?.Secondary.Serial.Value==s.Secondary,"Saved charges/selected weapons");
        Require(DragonKickDamage.Capture(p,s.Input.SkillDamagePercent)==s.Input,"Live kick capture changed across world reload");
        var selected=p.Items.Single(x=>ClassGear.Slot(x)==GearSlot.Feet);
        foreach(uint id in s.Boots)
        {RemoveBoots(p);Require(p.EquipItem(World.FindItem((Serial)id)),"Reload boot equip");ClassGear.Refresh(p);Require(DragonKickDamage.Boots(p)?.Code==ClassGear.Base(World.FindItem((Serial)id)).Code,"Reload boot identity");}
        RemoveBoots(p);Require(p.EquipItem(selected),"Restore saved footwear");ClassGear.Refresh(p);Require(DragonKickDamage.Capture(p,s.Input.SkillDamagePercent)==s.Input && AssassinCharges.State(p)==s.Charge,"Reload probes changed input/charge");
    }
}
