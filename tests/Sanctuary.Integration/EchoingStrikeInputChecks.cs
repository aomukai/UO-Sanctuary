// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class EchoingStrikeInputChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(EchoingStrikeInputChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint[] PoisonItems,bool Female,EchoingStrikeInput Input,int Life,int Mana,int Durability);
    private sealed record Manifest(int Generation,Saved[] Rows,string[] UnavailableUnique);
    private static string FileName=>Path.Join(Core.BaseDirectory,"echoing-strike-inputs-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        // A regeneration timer may already be scheduled before fixture naming.
        // Gate its first callback too;changing the next interval is insufficient.
        var canHp=ClassGearHooks.CanRegenerateHits;var canMp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>!(m.Name=="EchoingInputsSaved")&&canHp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>!(m.Name=="EchoingInputsSaved")&&canMp?.Invoke(m)!=false;
        Mobile.HitsRegenRateHandler=m=>m.Name=="EchoingInputsSaved"?TimeSpan.FromHours(1):hp?.Invoke(m)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=m=>m.Name=="EchoingInputsSaved"?TimeSpan.FromHours(1):mp?.Invoke(m)??Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"echoing-strike-inputs-reload-check":"echoing-strike-inputs-check";
            ConsoleInputHandler.RegisterCommand(name,"Echoing immutable weapon/Hex/flat-damage ledger;no final packet or flight",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Scene();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        }
    }
    private static void Scene()
    {
        var unavailable=NamedGear.Catalog.Items.Single(x=>x.SourceKey=="The Reedeemer");
        Require(!NamedGear.Available(unavailable.Id)&&NamedGear.Unavailable[unavailable.Id].Contains("Effect: skill"),"Existing unavailable skill-bearing unique policy changed");
        var rows=new Saved[2];int index=0;
        foreach(bool female in new[]{false,true})
        {
            var p=CleaveActionChecks.Fresh(female);p.Name="EchoingInputsSaved";
            if(!female){while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Actual Legendary Mallet strength");}}
            else{while(p.RawDex<70){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Actual Caduceus dexterity");}}
            FixtureSpace.Corridor(p,female?6280:6200,24,3500);ClassSkillChecks.Train(p,"warlock.echoingstrike",7);
            ClassSkillChecks.Train(p,"warlock.mirroredblades",4);ClassSkillChecks.Train(p,"warlock.bladewarp",3);ClassSkillChecks.Train(p,"warlock.hexbane");
            var old=CombatTiming.Equipped(p);p.AddToBackpack(old);
            string key=female?"Ironward":"Stone Crusher";var weapon=(BaseWeapon)NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(female?271:173),true);
            p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Actual unique melee weapon "+key);
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(31),true);
            p.AddToBackpack(helm);Require(p.EquipItem(helm),"Real soft-rank gear");
            var poisonItems=new[]{"Snakecord","Venom Grip"}.Select(k=>NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==k).Id,99,new Random(47),true)).ToArray();
            foreach(var item in poisonItems){p.AddToBackpack(item);Require(p.EquipItem(item),"Actual original poison source");}
            ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);
            Require(HexBane.CastPrepared(p,true),"Actual Bane source buff");
            int hp=SkillHealth.Balance256(p),mp=NecromancerEffects.ManaBalance256(p),durability=weapon.HitPoints;
            var captured=EchoingStrikeInputs.Capture(p);string frozen=JsonSerializer.Serialize(captured);
            Require(captured==EchoingStrikeInputs.Capture(p)&&SkillHealth.Balance256(p)==hp&&NecromancerEffects.ManaBalance256(p)==mp&&weapon.HitPoints==durability,"Collector rolled/paid/wounded/wore or changed context");
            Require(captured.Skill is{Rank:8,HardMirroredBlades:4,HardBladeWarp:3,TotalAttacks:1,WeaponSource128:116}
                &&captured.WeaponInputs.NormalDamage256==ClassGear.Bonus(weapon,"normal-damage")*256
                &&captured.WeaponInputs.NormalDamage256>0&&captured.Slow==SlowMissilePayload.Normal,"Soft-rank/count/flat ledger/raw source byte/slow context");
            Require(captured.WeaponInputs.Elements is{PoisonMin:193,PoisonMax:193,PoisonFrames:175,PoisonCount:2,LifeDrain:5}
                &&captured.WeaponInputs.Elements.PoisonImpactFrames==87,"Raw two-source poison rate/length/count/drain was prematurely scaled/averaged");
            int min=female?37:50,max=female?43:61,ed=ClassGear.Bonus(weapon,"dmg%");int plus=captured.WeaponInputs.NormalDamage256/256;
            Require(captured.WeaponInputs.Minimum256==(min*(100+ed)/100+plus)*256
                &&captured.WeaponInputs.Maximum256==(max*(100+ed)/100+plus)*256,"Independent pinned weapon base/unique ED/flat Damage+ ledger");
            Require(captured.WeaponInputs.UndeadPercent==50&&captured.WeaponInputs.Elements.MagicMin>=NamedElements.To256(HexBane.WeaponMagic(p,0))
                &&(!female||captured.WeaponInputs.Elements.MagicMin>=80*256),"Shared blunt species bonus or item/Hex magic omitted");
            p.AddToBackpack(helm);ClassGear.Refresh(p);ClassSkillChecks.Train(p,"warlock.mirroredblades",5);HexBane.ClearBuff(p);
            var changed=EchoingStrikeInputs.Capture(p);
            Require(changed.Skill.Rank==7&&changed.Skill.TotalAttacks==2&&changed.WeaponInputs.Elements.MagicMin<captured.WeaponInputs.Elements.MagicMin
                &&JsonSerializer.Serialize(captured)==frozen,"Captured source was a live view of gear/points/Hex");
            p.AddToBackpack(weapon);bool refused=false;try{EchoingStrikeInputs.Capture(p);}catch(NotSupportedException){refused=true;}
            Require(refused&&WarlockCombat.Implemented(CombatAbility.EchoingStrike)==EchoingStrikeWorld.Approved,"Unarmed capture or unqualified learned route admitted");
            Require(p.EquipItem(weapon)&&p.EquipItem(helm),"Original source gear restore");ClassGear.Refresh(p);
            NecromancerEffects.SetMana256(p,p.ManaMax*256);Require(HexBane.CastPrepared(p,true),"Restore source Hex");
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);NecromancerEffects.SetMana256(p,p.ManaMax*256-17);
            rows[index++]=new(p.Serial.Value,weapon.Serial.Value,helm.Serial.Value,poisonItems.Select(x=>x.Serial.Value).ToArray(),female,EchoingStrikeInputs.Capture(p),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),weapon.HitPoints);
            Require(SkillBindings.Instance.Assign(p,true,CombatAbility.EchoingStrike)==EchoingStrikeWorld.Approved&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.EchoingStrike)==EchoingStrikeWorld.Approved,"Input collector enabled learned binding");
        }
        File.WriteAllText(FileName,JsonSerializer.Serialize(new Manifest(0,rows,NamedGear.Unavailable[unavailable.Id])));
        Log.Information("Echoing inputs:actual StoneCrusher/Ironward base+ED+flat ledger,blunt species/item+Hex magic,critical chances/raw poison+slow,real soft gear/hard5threshold,immutable inputs and no resource/durability/roll side effects pass;Reedeemer skill-property policy unchanged;final scaling/flight gated");
    }
    private static void Reload()
    {
        var old=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(FileName));Require(old.Generation is 0 or 1,"Reload generation");
        foreach(var row in old.Rows)
        {
            var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var w=(BaseWeapon)World.FindItem((Serial)row.Weapon);ClassGear.Refresh(p);
            Require(p.Female==row.Female&&w.Parent==p&&World.FindItem((Serial)row.Helm)?.Parent==p&&w.HitPoints==row.Durability,"Original saved unique/helmet/durability");
            Require(row.PoisonItems.Length==2&&row.PoisonItems.All(n=>World.FindItem((Serial)n)?.Parent==p),"Original saved poison equipment");
            var actual=EchoingStrikeInputs.Capture(p);Require(actual==row.Input&&SkillHealth.Balance256(p)==row.Life&&NecromancerEffects.ManaBalance256(p)==row.Mana,$"Saved source inputs/Hex/items/ranks/fractional resources: life={SkillHealth.Balance256(p)}/{row.Life},mana={NecromancerEffects.ManaBalance256(p)}/{row.Mana},actual={JsonSerializer.Serialize(actual)},expected={JsonSerializer.Serialize(row.Input)}");
            Require(WarlockCombat.Implemented(CombatAbility.EchoingStrike)==EchoingStrikeWorld.Approved,"Reload admitted missing consumer");
        }
        File.WriteAllText(FileName,JsonSerializer.Serialize(old with{Generation=old.Generation+1}));
        Log.Information("Echoing inputs:original Warlock/Witch unique weapons,helm,Hex-derived immutable input values,hard/soft ranks,fractional wounds+mana and durability retain without final packet/flight");
    }
}
