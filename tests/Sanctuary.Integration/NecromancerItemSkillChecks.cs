// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class NecromancerItemSkillChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NecromancerItemSkillChecks));
    private static readonly string[] Keys=["Maelstromwrath","Umes Lament","Blackbog's Sharp","Boneshade"];
    private sealed record Held(uint Owner,uint Item,string Key,string Gear,Dictionary<string,int> Ranks,int Life,int Mana,string Effects);
    private sealed record Saved(Held[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"necromancer-item-skill-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static SkillDefinition[] Skills(Item item)=>EquipmentIntrinsicProperties.For(item).Where(x=>x.Property.Code=="skill")
        .Select(x=>SkillMechanics.All.Single(s=>s.SourceId==ItemSingleSkillProperty.Parameter(x.Property))).ToArray();
    private static int Expected(Item item,SkillDefinition skill)
    {
        int direct=0,general=0;
        foreach(var (property,roll) in EquipmentIntrinsicProperties.For(item))
        {
            if(property.Code=="skill"&&ItemSingleSkillProperty.Parameter(property)==skill.SourceId){direct+=roll.Value;}
            else if(property.Code is "nec" or "allskills"||property.Code=="skilltab"&&NamedPropertyRuntime.Parameter(property)==skill.SourceTab){general+=roll.Value;}
        }
        return direct+general;
    }
    private static PlayerMobile Owner(int index)
    {
        var p=PoisonDaggerChecks.Owner();ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);
        while(p.RawStr<100)Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Post-respec Strength allocation");
        while(p.RawDex<100)Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Post-respec Dexterity allocation");
        p.Name="PoisonDaggerFixtureNecroItemSkill"+index;FixtureSpace.Corridor(p,6900,24,2800+index*40);return p;
    }
    private static Dictionary<string,int> Ranks(PlayerMobile p,Item item)
    {
        var result=new Dictionary<string,int>();
        foreach(var skill in Skills(item))
        {
            int expected=Expected(item,skill);Require(ClassCreation.Profiles.GetRank(p.Serial,skill.TalentId)==0&&SkillMechanics.Rank(p,skill.Ability)==expected
                &&ClassCombat.Available(p,skill.Ability)&&SkillBindings.Instance.Assign(p,false,skill.Ability)&&SkillBindings.Instance.AssignQuick(p,7,skill.Ability),
                "Item grant failed rank/zero-hard/right/quick availability: "+skill.SourceKey);result.Add(skill.TalentId,expected);
        }
        Require(SkillMechanics.Rank(p,CombatAbility.LifeTap)==0&&!ClassCombat.Available(p,CombatAbility.LifeTap),"General bonus granted an unlearned/ungranted skill");return result;
    }
    private static void Suppression(PlayerMobile p,Item item)
    {
        string gear=Json(ClassGear.State(item));var state=ClassGear.State(item);ClassGear.Profiles.Store(item,state with{Identified=false});ClassGear.Refresh(p);
        foreach(var skill in SkillsFromDefinition(item))Require(SkillMechanics.Rank(p,skill.Ability)==0&&!ClassCombat.Available(p,skill.Ability),"Unidentified individual grant stayed active");
        ClassGear.Profiles.Store(item,state);ClassGear.Refresh(p);_=Ranks(p,item);
        p.Backpack.DropItem(item);ClassGear.Refresh(p);
        foreach(var skill in SkillsFromDefinition(item))Require(SkillMechanics.Rank(p,skill.Ability)==0&&!ClassCombat.Available(p,skill.Ability),"Unequipped individual grant stayed active");
        Require(Json(ClassGear.State(item))==gear&&p.EquipItem(item),"Unequip/reidentify changed original item or prevented re-equip");ClassGear.Refresh(p);_=Ranks(p,item);
        var foreign=new PlayerMobile{Name="PoisonDaggerFixtureForeignItemSkill",Player=true,Body=0x190};foreign.AddItem(new Backpack());
        try
        {
            ClassCreation.Profiles.Assign(foreign,DiabloClass.Sorceress);ClassCreation.Profiles.AdvanceLevel(foreign,99);
            while(foreign.RawStr<100)Require(ClassCreation.Profiles.TrySpendStat(foreign,CharacterStat.Strength),"Foreign Strength allocation");
            while(foreign.RawDex<100)Require(ClassCreation.Profiles.TrySpendStat(foreign,CharacterStat.Dexterity),"Foreign Dexterity allocation");
            p.Backpack.DropItem(item);foreign.AddToBackpack(item);Require(foreign.EquipItem(item),"Foreign item equip failed");ClassGear.Refresh(foreign);
            foreach(var skill in SkillsFromDefinition(item))Require(SkillMechanics.Rank(foreign,skill.Ability)==0&&!ClassCombat.Available(foreign,skill.Ability),"Class-specific grant leaked to another class");
            p.AddToBackpack(item);Require(p.EquipItem(item),"Could not return original item from foreign class");ClassGear.Refresh(p);_=Ranks(p,item);
        }
        finally{foreign.Delete();}
    }
    private static SkillDefinition[] SkillsFromDefinition(Item item)=>NamedGear.Catalog.Item(ClassGear.State(item).NamedItemId).Properties.Where(x=>x.Code=="skill")
        .Select(x=>SkillMechanics.All.Single(s=>s.SourceId==ItemSingleSkillProperty.Parameter(x))).ToArray();
    private static void Cast(PlayerMobile p,Item item,SkillDefinition skill)
    {
        var target=PoisonDaggerChecks.Target(p);Corpse corpse=null;
        try
        {
            var ability=skill.Ability;var point=target.Location;Serial selected=target.Serial;
            if(ability is CombatAbility.CorpseExplosion or CombatAbility.PoisonExplosion)
            {corpse=DeathSentryExplosionChecks.Body(p,new(p.X+4,p.Y,p.Z));point=corpse.Location;selected=corpse.Serial;}
            if(ability==CombatAbility.BoneWall){point=new(p.X+5,p.Y,p.Z);selected=Serial.Zero;}
            NecromancerSpellMissiles.Cancel(p);PoisonDaggerChecks.Ready(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);
            Require(SkillBindings.Instance.AssignQuick(p,7,ability),"Granted quick binding failed");var values=SkillMechanics.Values(p,ability);int mana=NecromancerEffects.ManaBalance256(p);
            Require(SkillBindingNetwork.Use(p,DiabloClass.Necromancer,7,ability,selected,point),"Zero-hard item skill did not begin: "+skill.SourceKey);
            var session=ClassCombat.For(p);int afterStart=mana-(ability==CombatAbility.PoisonDagger?values.Mana256:0);
            Require(NecromancerEffects.ManaBalance256(p)==afterStart&&!ClassCombat.ResolveAction(p,session.EffectAt-1),"Item-granted cast changed declared start-payment/release timing: "+skill.SourceKey);
            Require(ClassCombat.ResolveAction(p,session.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-values.Mana256&&!ClassCombat.ResolveAction(p,session.EffectAt),"Item-granted cast failed release/payment/replay: "+skill.SourceKey);
            if(NecromancerEffects.IsCurse(ability))Require(NecromancerEffects.Curse(target) is{} curse&&curse.Ability==ability&&curse.Rank==values.Rank,"Curse did not retain granted rank: "+skill.SourceKey);
            else if(ability==CombatAbility.BoneArmor)Require(NecromancerEffects.Profiles.Find(p)?.BoneArmorPool?.Rank==values.Rank,"Bone Armor lost granted rank");
            else if(ability==CombatAbility.BoneWall)
            {
                var walls=World.Mobiles.Values.OfType<BoneBarrierBody>().Where(b=>b.SummonMaster==p&&!b.Deleted).ToArray();
                Require(walls.Length>0&&walls.All(b=>b.Prepared.Rank==values.Rank),"Bone Wall did not receive granted birth rank");foreach(var b in walls){b.Delete();}
            }
            else if(ability==CombatAbility.PoisonDagger)Require(TimedEffects.Snapshot().Any(x=>x.Owner==target.Serial.Value&&x.PoisonMs>0&&x.PoisonSource==p.Serial.Value),"Granted Poison Dagger applied no poison");
            if(corpse!=null)Require(corpse.SanctuaryConsumed,"Granted corpse skill did not consume its selected corpse");
            if(ability is CombatAbility.Teeth or CombatAbility.BoneSpear or CombatAbility.BoneSpirit or CombatAbility.PoisonExplosion or CombatAbility.PoisonNova)
            {
                var flights=NecromancerSpellMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value).ToArray();Require(flights.Length>0&&flights.All(f=>f.Rank==values.Rank&&f.Ability==ability),"Missile producer lost granted rank");
                string captured=Json(flights);p.Backpack.DropItem(item);ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,ability)==0&&Json(NecromancerSpellMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value))==captured,"Unequip rewrote launched item-granted missiles");
                Require(p.EquipItem(item),"Could not restore original weapon");ClassGear.Refresh(p);
            }
            Require(ClassCreation.Profiles.GetRank(p.Serial,skill.TalentId)==0,"Item cast spent a hard point");
        }
        finally{ClassCombat.Stop(p);NecromancerSpellMissiles.Cancel(p);corpse?.Owner?.Delete();corpse?.Delete();target.Delete();}
    }
    private static void Check(NetState net,bool reload)
    {
        if(!reload)
        {
            var held=new List<Held>();int casts=0;var hit=PoisonDaggerPackets.HitRoll;var poison=PoisonDaggerPackets.PoisonRoll;var block=SourceBlocking.BlockRoll;
            try
            {
                PoisonDaggerPackets.HitRoll=()=>0;PoisonDaggerPackets.PoisonRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
                for(int i=0;i<Keys.Length;i++)
                {
                    var p=Owner(i);FixtureNetwork.Authenticate(net,p,"necro-item-skill-");var item=PoisonDaggerChecks.Named(p,Keys[i]);Suppression(p,item);
                    foreach(var skill in Skills(item)){Cast(p,item,skill);casts++;}
                    var ranks=Ranks(p,item);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-1237);TimedEffects.PreventHealing(p,TimedEffects.Now);
                    held.Add(new(p.Serial.Value,item.Serial.Value,Keys[i],Json(ClassGear.State(item)),ranks,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),Json(NecromancerEffects.Profiles.Find(p))));p.Internalize();
                }
            }
            finally{PoisonDaggerPackets.HitRoll=hit;PoisonDaggerPackets.PoisonRoll=poison;SourceBlocking.BlockRoll=block;}
            Require(casts==14,"Incomplete whole-item skill cast matrix");File.WriteAllText(Manifest,Json(new Saved(held.ToArray(),0)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
            FixtureNetwork.Authenticate(net,(PlayerMobile)World.FindMobile((Serial)saved.Actors[0].Owner),"necro-item-skill-reload-");
            foreach(var row in saved.Actors)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var item=World.FindItem((Serial)row.Item);
                Require(item is{Deleted:false}&&item.Parent==p&&Json(ClassGear.State(item))==row.Gear&&Json(Ranks(p,item))==Json(row.Ranks)
                    &&SkillHealth.Balance256(p)==row.Life&&NecromancerEffects.ManaBalance256(p)==row.Mana&&Json(NecromancerEffects.Profiles.Find(p))==row.Effects,"Original skill-grant item/owner/ranks/wounds/resources/buff changed on reload");
            }
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
        Log.Information("Necromancer individual item skills:4whole uniques/14zero-hard quick releases with exact payment;curse/armor/barrier/poison/corpse/missile consumers;unidentified/unequip/foreign-class refusal;launched rank snapshots;original gear/ranks/wounds/resources retained");
    }
    public static void Configure()
    {
        foreach(string name in new[]{"necromancer-item-skill-check","necromancer-item-skill-reload-check"})ConsoleInputHandler.RegisterCommand(name,"Necromancer original item skill grants",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
}
