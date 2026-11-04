// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ReviveEquipmentOffenseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveEquipmentOffenseChecks));
    private static readonly string[] Codes=["normal-damage","dmg-min","dmg-max","dmg%","dmg-undead%","dmg-demon%","str","dex","deadly","extra-fire","extra-ltng","extra-cold","extra-mag","extra-pois","pierce-pois"];
    private sealed record LiveLevel(Dictionary<string,int> Bonuses,int OneMin,int OneMax,int TwoMin,int TwoMax,MissileSourceStats Elements,MissileSourceStats Poison);
    private sealed record LiveItem(FrozenCombatBase Basis,LiveLevel[] Levels);
    private sealed record Scene(uint Actor,uint Corpse,ReviveNativeState State,int BodyMin,int BodyMax,LiveItem[] Items,ReviveOffenseSnapshot Snapshot);
    private sealed record Saved(uint Owner,uint Target,Scene[] Scenes);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-equipment-offense-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("revive-equipment-offense-check",Check);Register("revive-equipment-offense-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original Revive immutable physical/elemental/poison/critical roll inputs",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Item Named(string name)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==name).Id,99,new Random(173),true);
    private static LiveItem Read(Item item)
    {
        var b=ClassGear.Base(item);var basis=new FrozenCombatBase(ClassGear.Slot(item),b.Type,ClassGear.Slot(item)==GearSlot.Weapon&&!ClassGear.Broken(item),b.TwoHanded,b.OneOrTwoHanded,b.StrengthBonus,b.DexterityBonus);
        return new(basis,Enumerable.Range(1,99).Select(level=>
        {var c=new ItemStatContext(item,level);var one=ClassGear.Damage(item,level,false,true);var two=ClassGear.Damage(item,level,true,true);
            return new LiveLevel(Codes.ToDictionary(code=>code,c.Bonus),one.Min,one.Max,two.Min,two.Max,c.Elements(),c.Poison());}).ToArray());
    }
    private static Scene Create(PlayerMobile p,Mobile target,int kind)
    {
        BaseCreature b;
        if(kind==1)
        {var profile=EncounterProfiles.Profile("crypt-sentinel-90");var e=new EncounterCreature();e.Assign(profile,profile.MinimumLevel,MonsterDifficulty.Normal,EncounterGrade.Minion,ChampionKind.Champion,[EliteModifier.ExtraStrong],EliteAura.None,null,new Random(173));b=e;}
        else{b=new Skeleton();}
        b.Name="PoisonDaggerFixtureReviveOffense";b.NoKillAwards=true;
        foreach(var item in b.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank).ToArray()){item.Delete();}
        b.RawStr=137;b.RawDex=61;b.RawInt=70;b.ManaMaxSeed=70;b.StamMaxSeed=55;b.SetDamage(7,11);
        string[][] names=[["Baranar's Star","Venom Grip","Snakecord","Gorerider"],["Bonesob","Venom Grip","Snakecord","Gorerider"],["Frostburn","Snakecord","Gorerider","Harlequin Crest"]];
        var gear=names[kind].Select(Named).ToArray();foreach(var item in gear){b.AddItem(item);}var live=gear.Select(Read).ToArray();
        var profileBefore=CreatureProfiles.For(b);int bodyMin=profileBefore.ApplyStats?profileBefore.Attack1.MinDamage??b.DamageMin:b.DamageMin,bodyMax=profileBefore.ApplyStats?profileBefore.Attack1.MaxDamage??b.DamageMax:b.DamageMax;
        b.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();Corpse corpse=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,container)=>{died?.Invoke(who,container);if(who==b){corpse=container as Corpse;corpse?.DropItem(new Gold(1));}};
        try{p.DoHarmful(b,true);b.Damage(b.Hits+1,p);CombatDamage.Finish(b,p);}finally{ClassLootHooks.CreatureDied=died;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==b,"Offensive original was not retained");corpse.BeginDecay(FixtureSpace.Retention);
        var original=ReviveOriginals.State(corpse);var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        var setup=ReviveLife.Setup(p,corpse,before.Mana,before.Stamina)with{RegenerationRate=ReviveRegeneration.Rate(CreatureProfiles.Definition(b).Reference)};
        setup=ReviveEquipmentCombat.Prepare(setup,original);
        Require(ReviveNativeActivation.CommitPrepared(p,corpse,corpse.Location,before.AiStatus.Value,setup,out var actual,out var issue)&&actual==b,"Offensive original activation failed: "+issue);
        foreach(var item in gear){Require(item.Parent==corpse,"Offensive gear was not independent loot");p.AddToBackpack(item);item.Delete();}
        var values=new SkillValues(SkillMechanics.For(CombatAbility.Enchant),key=>key=="Enchant"?3:0,_=>0,99);
        SorceressEffects.CastNpcEnchant(b,b,values);var buff=SorceressEffects.Profiles.Find(b);
        Require(buff.Enchantment is{Min256:>0},"Positive NPC Enchant control missing");
        SorceressEffects.Profiles.Store(b,buff with{Enchantment=buff.Enchantment with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}});
        var scene=new Scene(b.Serial.Value,corpse.Serial.Value,ReviveNativeActivation.State(b),bodyMin,bodyMax,live,ReviveEquipmentOffense.Capture(b,target));Expect(scene,b,target);
        // A captured attack keeps the buff it had at start; later refreshes
        // cannot reread live gear or a removed Enchant state during rolling.
        var frozen=scene.Snapshot;SorceressEffects.Profiles.Store(b,buff with{Enchantment=null});
        Require(ReviveEquipmentOffense.Capture(b,target).Elements.Stats.FireMin<frozen.Elements.Stats.FireMin,"Enchant removal control was vacuous");
        Require(ReviveEquipmentOffense.Roll(frozen,_=>0,()=>99).Damage.Elements.Fire>0,"Captured buff vanished before its offensive roll");
        SorceressEffects.Profiles.Store(b,buff with{Enchantment=buff.Enchantment with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}});return scene;
    }
    private static void Expect(Scene scene,BaseCreature b,Mobile target)
    {
        var captured=ReviveEquipmentOffense.Capture(b,target);int level=captured.Level;var rows=scene.Items.Select(item=>item.Levels[level-1]).ToArray();
        int Sum(string code)=>rows.Sum(r=>r.Bonuses[code]);var weapons=scene.Items.Select((item,i)=>(item,i)).Where(x=>x.item.Basis.Weapon).ToArray();
        Require(weapons.Length<=1,"Invalid live comparison weapons");var basis=weapons.Length==1?weapons[0].item.Basis:null;bool two=basis!=null&&(basis.TwoHanded||basis.OneOrTwoHanded);
        var profile=CreatureProfiles.For(b);int min=profile.ApplyStats?profile.Attack1.MinDamage??scene.BodyMin:scene.BodyMin,max=profile.ApplyStats?profile.Attack1.MaxDamage??scene.BodyMax:scene.BodyMax,extra=0;
        if(b is EncounterCreature strong&&strong.Inherits(EliteModifier.ExtraStrong))
        {min=strong.Basis.MinDamage;max=strong.Basis.MaxDamage;extra=EncounterProfiles.ExtraStrong(MonsterVariants.Difficulty(strong),strong.Grade==EncounterGrade.Minion).DamagePercent;}
        if(two){min=max=0;}int percent=extra+ReviveCombat.DamagePercent(b)+CombatAuras.DamagePercent(b)+NecromancerEffects.DamageStatPercent(b);
        for(int i=0;i<rows.Length;i++)
        {
            if(basis!=null&&i==weapons[0].i){min+=two?rows[i].TwoMin:rows[i].OneMin;max+=two?rows[i].TwoMax:rows[i].OneMax;}
            else{min+=rows[i].Bonuses["dmg-min"];max+=rows[i].Bonuses["dmg-max"];percent+=rows[i].Bonuses["dmg%"];}            
        }
        if(GemCombat.Undead(target)){percent+=Sum("dmg-undead%")+(basis!=null&&ClassItemTypes.Is(basis.Type,"blun")?50:0);}
        else if(NamedCombat.Demon(target)){percent+=Sum("dmg-demon%");}
        var expected=new NpcPhysicalStats(min-Sum("normal-damage"),max-Sum("normal-damage"),Sum("normal-damage"),percent,Sum("str"),Sum("dex"),basis!=null,basis?.StrengthCoefficient??0,basis?.DexterityCoefficient??0);
        Require(captured.Physical==expected&&captured.Physical.Strength!=b.RawStr,"Frozen physical composition differs from original live suppliers");
        var enchant=SorceressEffects.Enchantment(b);Require(enchant!=null,"Held Enchant vanished");
        var elements=new MissileSourceStats(FireMin:rows.Sum(r=>r.Elements.FireMin)+enchant.Min256,FireMax:rows.Sum(r=>r.Elements.FireMax)+enchant.Max256,
            LightningMin:rows.Sum(r=>r.Elements.LightningMin),LightningMax:rows.Sum(r=>r.Elements.LightningMax),MagicMin:rows.Sum(r=>r.Elements.MagicMin),MagicMax:rows.Sum(r=>r.Elements.MagicMax),
            ColdMin:rows.Sum(r=>r.Elements.ColdMin),ColdMax:rows.Sum(r=>r.Elements.ColdMax),ColdFrames:rows.Sum(r=>r.Elements.ColdFrames));
        var poison=new MissileSourceStats(PoisonMin:rows.Sum(r=>r.Poison.PoisonMin),PoisonMax:rows.Sum(r=>r.Poison.PoisonMax),PoisonFrames:rows.Sum(r=>r.Poison.PoisonFrames),PoisonCount:rows.Sum(r=>r.Poison.PoisonCount));
        Require(captured.Elements==new NpcElementSnapshot(elements,Sum("extra-fire"),Sum("extra-ltng"),Sum("extra-cold"),Sum("extra-mag"))&&captured.Poison==new NpcPoisonSnapshot(b.Serial.Value,b.ControlMaster.Serial.Value,poison,Sum("extra-pois"),Math.Max(0,Sum("pierce-pois"))),"Frozen elemental/poison contributions or owner credit changed");
        Require(captured.Critical.Deadly>0&&captured.Critical.Intrinsic>0&&poison.PoisonMin>0,"Critical/poison controls were vacuous");
        int life=SkillHealth.Balance256(b),targetLife=SkillHealth.Balance256(target),count=World.Items.Count;
        var low=ReviveEquipmentOffense.Roll(captured,_=>0,()=>99);var high=ReviveEquipmentOffense.Roll(captured,n=>n-1,()=>99);var critical=ReviveEquipmentOffense.Roll(captured,_=>0,()=>0);
        Require(low.Damage.Physical256==NpcEquipmentDamage.Roll256(expected,_=>0)&&high.Damage.Physical256==NpcEquipmentDamage.Roll256(expected,n=>n-1)
            &&critical.PhysicalCritical&&critical.Damage.Intrinsic&&critical.Damage.Physical256==low.Damage.Physical256*4
            &&critical.Damage.Elements.Fire==low.Damage.Elements.Fire*2&&critical.Damage.Elements.Cold==low.Damage.Elements.Cold*2&&critical.Damage.Poison.Rate256==low.Damage.Poison.Rate256*2
            &&critical.Damage.Poison.Frames==low.Damage.Poison.Frames,"Shared offensive endpoints/critical channel composition changed");
        Require(low.Damage.Poison.Frames==poison.PoisonImpactFrames&&low.ColdFrames==elements.ColdFrames,"Poison count/duration or cold frames changed");
        for(int seed=0;seed<32;seed++)
        {
            (NpcWeaponRoll Roll,ulong Seed) Run(){ulong value=SourceRandom.InitLow(seed);uint Raw()=>SourceRandom.Roll(ref value);int Limited(int n)=>(int)(Raw()%(uint)n);var roll=ReviveEquipmentOffense.Roll(captured,Limited,Raw);return(roll,value);}
            Require(Run()==Run(),"Captured offensive input did not replay the same supplied stream");
        }
        Require(SkillHealth.Balance256(b)==life&&SkillHealth.Balance256(target)==targetLife&&World.Items.Count==count,"Offensive preparation committed HP or recreated original items");
    }
    private static void Check()
    {
        var old=SummonDifficulties.Facets();var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureReviveOffenseOwner";p.RawStr=500;
        MagePetBuffChecks.Train(p,"necromancer.revive",20);MagePetBuffChecks.Train(p,"necromancer.skeletonmastery",20);FixtureSpace.Corridor(p,6680,36,3400);
        SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
        var target=new Skeleton{NoKillAwards=true};target.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);target.AIObject?.AITimer.Stop();Require(GemCombat.Undead(target),"Undead comparison target missing");
        try
        {
            var scenes=Enumerable.Range(0,3).Select(kind=>Create(p,target,kind)).ToArray();
            foreach(var map in new[]{Map.Felucca,Map.Trammel,Map.Felucca})
            {p.MoveToWorld(new(p.X+1,p.Y,p.Z),map);ReviveFacets.Sync(p);foreach(var scene in scenes){var b=(BaseCreature)World.FindMobile((Serial)scene.Actor);Require(ReviveFacets.Issue(b)==null,"Offensive facet projection parked");Expect(scene,b,target);}}
            var first=(BaseCreature)World.FindMobile((Serial)scenes[0].Actor);int draws=0;first.SetDamageType(ResistanceType.Physical,75);first.SetDamageType(ResistanceType.Fire,25);bool refused=false;
            try{_=ReviveEquipmentOffense.Roll(ReviveEquipmentOffense.Capture(first,target),n=>{draws++;return 0;},()=>{draws++;return 0;});}catch(NotSupportedException){refused=true;}
            finally{first.SetDamageType(ResistanceType.Physical,100);first.SetDamageType(ResistanceType.Fire,0);}
            Require(refused&&draws==0,"Unsupported elemental body consumed RNG or silently became physical");
            refused=false;try{_=ReviveEquipmentOffense.Capture(first,target,2);}catch(NotSupportedException){refused=true;}Require(refused,"Attack2 silently used attack1 inputs");
            var row=ReviveNativeActivation.State(first);var payload=ReviveBeforeDeath.Decode(row.Original.BeforeDeathJson);var other=ReviveBeforeDeath.Equipment(ReviveBeforeDeath.Decode(scenes[1].State.Original.BeforeDeathJson))[0];
            var mixed=payload with{EquipmentJson=JsonSerializer.Serialize(ReviveBeforeDeath.Equipment(payload).Append(other))};refused=false;
            try{ReviveNativeActivation.Store(row with{Original=row.Original with{BeforeDeathJson=JsonSerializer.Serialize(mixed)}});_=ReviveEquipmentOffense.Capture(first,target);}catch(NotSupportedException){refused=true;}finally{ReviveNativeActivation.Store(row);}
            Require(refused,"Ambiguous two-weapon context guessed a hand");
            var noTarget=ReviveEquipmentOffense.Roll(ReviveEquipmentOffense.Capture(first),_=>0,()=>0);Require(noTarget.Damage.HitClass==0,"Preview invented defender presentation");
            p.Internalize();target.Internalize();var saved=scenes.Select(scene=>scene with{State=ReviveNativeActivation.State((BaseCreature)World.FindMobile((Serial)scene.Actor)),Snapshot=ReviveEquipmentOffense.Capture((BaseCreature)World.FindMobile((Serial)scene.Actor),target)}).ToArray();
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,saved)));
        }
        finally{SummonDifficulties.SetFacets(old);}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Scenes.Length==3,"Missing offensive originals");var target=World.FindMobile((Serial)s.Target);
        foreach(var scene in s.Scenes)
        {
            var b=World.FindMobile((Serial)scene.Actor)as BaseCreature;Require(b is{Alive:true,Deleted:false}&&b.Map==Map.Internal&&b.ControlMaster?.Serial.Value==s.Owner&&ReviveNativeActivation.State(b)==scene.State
                &&ReviveEquipmentOffense.Capture(b,target)==scene.Snapshot,"Reload changed original offensive input/source identity");Expect(scene,b,target);
            Require(World.FindItem((Serial)scene.Corpse)is Corpse c&&c.GetAmount(typeof(Gold))==1,"Offensive independent loot changed");
        }
    }
}
