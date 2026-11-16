// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class UnresistedShieldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(UnresistedShieldChecks));
    private sealed record Saved(uint Owner,uint Attacker,uint Chest,Point3D Point,int Life,int Mana,EnergyShieldState Shield,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"unresisted-shields-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>m.Name!="UnresistedShieldFixture"&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>m.Name!="UnresistedShieldFixture"&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"unresisted-shields-reload-check":"unresisted-shields-check";
            ConsoleInputHandler.RegisterCommand(name,"Native typed armor-ignore shield producer and original saves",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Check();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        }
    }
    private static PlayerMobile Fresh(DiabloClass kind=DiabloClass.Sorceress)
    {
        var p=new PlayerMobile{Name="UnresistedShieldFixture",Player=true,Body=0x190};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,kind);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Shield fixture Strength");}
        FixtureSpace.Corridor(p,6420,24,800);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p));NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    private static void Energy(PlayerMobile p,int mana=25617)
    {
        ClassSkillChecks.Train(p,"sorceress.energyshield");ClassSkillChecks.Train(p,"sorceress.telekinesis",20);
        NecromancerEffects.SetMana256(p,p.ManaMax*256);
        Require(SorceressCombat.Release(p,CombatAbility.EnergyShield,Serial.Zero,p.Location),"Actual learned Energy Shield release");
        Require(SorceressEffects.Shield(p) is{Rank:1,AbsorbPercent:20},"Rank1 absorption fixture");
        NecromancerEffects.SetMana256(p,mana);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p));
    }
    private static void EnergyCases()
    {
        var p=Fresh();var c=EncounterShieldChecks.Enemy(p);
        try
        {
            NecromancerEffects.SetMana256(p,100*256+17);int hp=SkillHealth.Balance256(p);
            AOS.Damage(p,c,10,true,100,0,0,0,0,spellDamage:true);
            Require(SkillHealth.Balance256(p)==hp-10*256&&NecromancerEffects.ManaBalance256(p)==100*256+17,"Unshielded native fallback changed");
            Energy(p);Require(ClassCombatHooks.SkillUnresistedDamage(p,c,20,100,false,false)==null,"Legacy hook unexpectedly supplies Energy fields");
            hp=SkillHealth.Balance256(p);AOS.Damage(p,c,20,true,100,0,0,0,0,spellDamage:true);
            Require(SkillHealth.Balance256(p)==hp-16*256&&NecromancerEffects.ManaBalance256(p)==97*256+17,"Native physical Energy Shield did not spend once/absorb");
            var chest=PoisonDaggerChecks.Named(p,"Shaftstop");Require(p.PhysicalResistance==30,"Real Shaftstop physical resistance");
            Energy(p);hp=SkillHealth.Balance256(p);AOS.Damage(p,c,100,true,100,0,0,0,0,weaponDamage:true);
            Require(SkillHealth.Balance256(p)==hp-80*256&&NecromancerEffects.ManaBalance256(p)==85*256+17,"Armor-ignore incorrectly applied resistance or skipped mana shield");
            Energy(p);hp=SkillHealth.Balance256(p);AOS.Damage(p,c,100,false,100,0,0,0,0,weaponDamage:true);
            Require(SkillHealth.Balance256(p)==hp-56*256&&NecromancerEffects.ManaBalance256(p)==85*256+17,"Ordinary resisted producer changed");
            Energy(p);hp=SkillHealth.Balance256(p);AOS.Damage(p,c,100,true,20,20,20,20,20,spellDamage:true);
            Require(SkillHealth.Balance256(p)==hp-84*256&&NecromancerEffects.ManaBalance256(p)==88*256+17,"Native elemental split/order or poison exclusion lost");
            Energy(p);hp=SkillHealth.Balance256(p);AOS.Damage(p,c,20,true,0,0,0,100,0,spellDamage:true);
            Require(SkillHealth.Balance256(p)==hp-20*256&&NecromancerEffects.ManaBalance256(p)==100*256+17,"Native poison consumed Energy Shield mana");
            Energy(p);hp=SkillHealth.Balance256(p);AOS.Damage(p,c,1,true,100,0,0,0,0,spellDamage:true);
            Require(SkillHealth.Balance256(p)==hp-205&&NecromancerEffects.ManaBalance256(p)==100*256+17-38,"Native fractional shield hit rounded to whole life/mana");
            Energy(p,273);hp=SkillHealth.Balance256(p);AOS.Damage(p,c,20,true,100,0,0,0,0,spellDamage:true);
            Require(SkillHealth.Balance256(p)==hp-4756&&NecromancerEffects.ManaBalance256(p)==0&&SorceressEffects.Shield(p)==null,"Partial native mana/depletion/overflow mismatch");
            Energy(p);hp=SkillHealth.Balance256(p);var before=SorceressEffects.Shield(p);int mana=NecromancerEffects.ManaBalance256(p);
            foreach(bool direct in new[]{false,true})
            {
                bool refused=false;try{AOS.Damage(p,c,10,true,direct?100:60,direct?0:20,0,0,0,direct:direct?1:0,spellDamage:true);}catch(NotSupportedException){refused=true;}
                Require(refused&&SkillHealth.Balance256(p)==hp&&NecromancerEffects.ManaBalance256(p)==mana&&SorceressEffects.Shield(p)==before,"Unclassified split spent resources or silently invented a channel");
            }
        }
        finally{c.Delete();p.Delete();}
    }
    private static void SharedCases()
    {
        var p=Fresh(DiabloClass.Necromancer);var c=EncounterShieldChecks.Enemy(p);
        try
        {
            ClassSkillChecks.Train(p,"necromancer.bonearmor");NecromancerEffects.BoneArmor(p,SkillMechanics.Values(p,CombatAbility.BoneArmor));
            var state=NecromancerEffects.Profiles.Find(p);NecromancerEffects.Profiles.Store(p,state with{BoneArmor=10,BoneArmorPool=state.BoneArmorPool with{Remaining256=10*256}});
            Require(EnergyShields.Apply(p,new(TimedEffects.Now.Add(FixtureSpace.Retention),20)),"Existing rank-zero legacy overlap");
            NecromancerEffects.SetMana256(p,4*256);int hp=SkillHealth.Balance256(p);AOS.Damage(p,c,100,true,10,90,0,0,0,spellDamage:true);
            Require(SkillHealth.Balance256(p)==hp-90*256&&NecromancerEffects.Armor256(p)==2*256&&NecromancerEffects.ManaBalance256(p)==0,"Native physical-first Energy/Bone order mismatch");
        }
        finally{c.Delete();p.Delete();}
        p=Fresh(DiabloClass.Druid);c=EncounterShieldChecks.Enemy(p);
        try
        {
            ClassSkillChecks.Train(p,"druid.cyclonearmor");Require(DruidCycloneArmor.Cast(p,SkillMechanics.Values(p,CombatAbility.CycloneArmor)),"Actual Cyclone cast");
            int pool=DruidCycloneArmor.State(p).Remaining256,hp=SkillHealth.Balance256(p);int absorbed=Math.Min(pool,60*256);
            AOS.Damage(p,c,60,true,0,50,25,0,25,spellDamage:true);
            Require(SkillHealth.Balance256(p)==hp-(60*256-absorbed)&&(DruidCycloneArmor.State(p)?.Remaining256??0)==pool-absorbed,"Native Cyclone channels bypassed its existing finite pool");
        }
        finally{c.Delete();p.Delete();DruidCycloneArmor.Prune();}
        p=Fresh(DiabloClass.Warlock);c=EncounterShieldChecks.Enemy(p);
        try
        {
            ClassSkillChecks.Train(p,"warlock.psychicward");Require(PsychicWard.CastPrepared(p),"Existing Ward cast");
            Require(EnergyShields.Apply(p,new(TimedEffects.Now.Add(FixtureSpace.Retention),20)),"Explicit injected legacy overlap");
            var ward=PsychicWard.State(p);int hp=SkillHealth.Balance256(p),mana=NecromancerEffects.ManaBalance256(p);bool refused=false;
            try{AOS.Damage(p,c,20,true,100,0,0,0,0,weaponDamage:true);}catch(NotSupportedException){refused=true;}
            Require(refused&&PsychicWard.State(p)==ward&&SkillHealth.Balance256(p)==hp&&NecromancerEffects.ManaBalance256(p)==mana,"Unverified Ward/Energy overlap mutated state before refusal");
        }
        finally{c.Delete();PsychicWard.Clear(p);p.Delete();}
    }
    private static void Check()
    {
        Require(ClassCombatHooks.SkillUnresistedTypedDamage!=null,"Typed native hook not registered");EnergyCases();SharedCases();
        var p=Fresh();var c=EncounterShieldChecks.Enemy(p);Energy(p);var chest=PoisonDaggerChecks.Named(p,"Shaftstop");
        var shield=SorceressEffects.Shield(p) with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Require(EnergyShields.Apply(p,shield),"Held original shield capture");
        SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);SkillBindings.Instance.Assign(p,false,CombatAbility.EnergyShield);SkillBindings.Instance.AssignQuick(p,7,CombatAbility.EnergyShield);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,c.Serial.Value,chest.Serial.Value,p.Location,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),shield,0)));p.Internalize();c.Internalize();
        Log.Information("Native armor-ignore shields:physical/five native channels/poison exclusion,real resistance bypass,FP256,depletion,Energy-before-Bone,Cyclone and unsupported projection/overlap refusal pass;original Sorceress/attacker/Shaftstop/state/resources saved");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var c=(EncounterCreature)World.FindMobile((Serial)s.Attacker);
        Require(s.Generation<2&&p.Map==Map.Internal&&c.Map==Map.Internal&&World.FindItem((Serial)s.Chest)?.Parent==p&&SorceressEffects.Shield(p)==s.Shield
            &&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Original native shield save identity/resources changed");
        p.MoveToWorld(s.Point,Map.Trammel);c.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);c.AIObject.AITimer.Stop();
        var expected=EnergyShields.Calculate(new(4*256,4*256,4*256,4*256,0),s.Mana,20,12,false);
        int loss=expected.Damage.Physical+expected.Damage.Fire+expected.Damage.Lightning+expected.Damage.Cold+4*256;
        AOS.Damage(p,c,20,true,20,20,20,20,20,spellDamage:true);
        Require(SkillHealth.Balance256(p)==s.Life-loss&&NecromancerEffects.ManaBalance256(p)==expected.Mana&&SorceressEffects.Shield(p)==s.Shield,"Reload native hit lost split/FP resources or refreshed/reset shield");
        p.Internalize();c.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,Life=SkillHealth.Balance256(p),Mana=NecromancerEffects.ManaBalance256(p)}));
    }
}
