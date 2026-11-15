// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class BoneArmorRuleChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BoneArmorRuleChecks));
    private sealed record Saved(int Generation,uint Owner,Point3D Point,int Life,int Mana,BoneArmorState Armor,EnergyShieldState Energy);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bone-armor-rule-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>m.Name!="BoneArmorRuleFixture"&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>m.Name!="BoneArmorRuleFixture"&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"bone-armor-rule-reload-check":"bone-armor-rule-check";
            ConsoleInputHandler.RegisterCommand(name,"Bone Armor breaking-hit policy,shared shield order and original saved overlap",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Check();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        }
    }
    private static PlayerMobile Fresh()
    {
        var p=EncounterShieldChecks.Owner(DiabloClass.Necromancer);p.Name="BoneArmorRuleFixture";ClassSkillChecks.Train(p,"necromancer.bonearmor");return p;
    }
    private static void Armor(PlayerMobile p,int remaining=5120)
    {
        NecromancerEffects.BoneArmor(p,SkillMechanics.Values(p,CombatAbility.BoneArmor));
        var state=NecromancerEffects.Profiles.Find(p);
        Require(state.BoneArmorPool is{Rank:1,Maximum256:5120},"Unexpected rank1/hard-synergy fixture");
        NecromancerEffects.Profiles.Store(p,state with{BoneArmor=(remaining+255)/256,BoneArmorPool=state.BoneArmorPool with{Remaining256=remaining}});
    }
    private static EnergyShieldState Energy(PlayerMobile p)
    {
        // Rank0 is the supported historical capture, not a new oskill grant.
        // This isolates incoming order without inventing Sorceress training.
        var shield=new EnergyShieldState(TimedEffects.Now.Add(FixtureSpace.Retention),20);
        Require(EnergyShields.Apply(p,shield),"Supported legacy Energy Shield capture refused");return shield;
    }
    private static void Check()
    {
        var p=Fresh();var c=EncounterShieldChecks.Enemy(p);bool kept=false;
        try
        {
            foreach(var (pool,hit,left,passed) in new[]{(256,1,255,0),(256,256,0,0),(256,257,0,1),(704,832,0,128)})
            {
                Armor(p,pool);var r=SkillCombat.MitigatePrepared(c,p,new(hit/256.0,0,0,0,0));
                Require(NamedElements.To256(r.Physical)==passed&&NecromancerEffects.Armor256(p)==left
                    &&(left>0||NecromancerEffects.Profiles.Find(p).BoneArmorPool==null),"Breaking/equal/fractional shield policy mismatch");
            }
            var ignoreChest=PoisonDaggerChecks.Named(p,"Shaftstop");Armor(p);int hp=SkillHealth.Balance256(p);
            AOS.Damage(p,c,30,true,100,0,0,0,0,weaponDamage:true);
            Require(p.PhysicalResistance==30&&NecromancerEffects.Armor256(p)==0&&SkillHealth.Balance256(p)==hp-10*256,"Native armor-ignore bypassed Bone Armor or incorrectly used physical resistance");
            Armor(p);hp=SkillHealth.Balance256(p);AOS.Damage(p,c,20,true,50,50,0,0,0,weaponDamage:true);
            Require(NecromancerEffects.Armor256(p)==10*256&&SkillHealth.Balance256(p)==hp-10*256,"Native armor-ignore mixed packet lost physical shield or nonphysical remainder");
            p.AddToBackpack(ignoreChest);ignoreChest.Delete();ClassGear.Refresh(p);
            Armor(p);Energy(p);NecromancerEffects.SetMana256(p,100*256+17);
            var mixed=SkillCombat.MitigatePrepared(c,p,new(50,10,0,0,3));
            Require(mixed.Physical==20&&mixed.Fire==8&&mixed.Poison==3&&NecromancerEffects.Armor256(p)==0
                &&NecromancerEffects.ManaBalance256(p)==76*256+17,"Energy/Bone order or poison exclusion changed");
            var chest=PoisonDaggerChecks.Named(p,"Shaftstop");Armor(p);Energy(p);NecromancerEffects.SetMana256(p,100*256);
            var reduced=SkillCombat.MitigatePrepared(c,p,new(50,0,0,0,0));
            Require(reduced.Physical==14&&NecromancerEffects.ManaBalance256(p)==80*256&&NecromancerEffects.Armor256(p)==0,"Shield ordering relative to actual physical resistance changed");
            p.AddToBackpack(chest);chest.Delete();ClassGear.Refresh(p);Armor(p);Energy(p);NecromancerEffects.SetMana256(p,256);
            var poor=SkillCombat.MitigatePrepared(c,p,new(50,0,0,0,0));
            Require(poor.Physical==29.5&&NecromancerEffects.ManaBalance256(p)==0&&SorceressEffects.Shield(p)==null&&NecromancerEffects.Armor256(p)==0,"Partial mana failure did not settle once before Bone Armor");
            var energy=Energy(p);Armor(p,704);Require(SorceressEffects.Shield(p)==energy,"Bone Armor group replacement removed the independent mana shield");
            NecromancerEffects.SetMana256(p,100*256+17);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);
            var saved=new Saved(0,p.Serial.Value,p.Location,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),NecromancerEffects.Profiles.Find(p).BoneArmorPool,energy);
            p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));kept=true;
            Log.Information("Bone Armor rule:conventional breaking/equal/fractional hits,actual armor-ignore physical/mixed hits before real Shaftstop resistance,Energy-before-Bone typed order,poison exclusion and partial mana pass;modern ordering/overflow remain explicit proposal");
        }
        finally{c.Delete();if(!kept){p.Delete();}}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(p is{Deleted:false,Alive:true}&&p.Map==Map.Internal&&NecromancerEffects.Profiles.Find(p).BoneArmorPool==saved.Armor
            &&SorceressEffects.Shield(p)==saved.Energy&&SkillHealth.Balance256(p)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Saved original overlap,metadata or fractional resources changed");
        p.MoveToWorld(saved.Point,Map.Trammel);var c=EncounterShieldChecks.Enemy(p);
        try
        {
            var r=SkillCombat.MitigatePrepared(c,p,new(50,10,0,0,3));
            Require(r.Physical==37.25&&r.Fire==8&&r.Poison==3&&NecromancerEffects.Armor256(p)==0&&NecromancerEffects.ManaBalance256(p)==76*256+17,"Restored overlap failed live mitigation order");
            NecromancerEffects.Profiles.Store(p,NecromancerEffects.Profiles.Find(p) with{BoneArmor=(saved.Armor.Remaining256+255)/256,BoneArmorPool=saved.Armor});
            SorceressEffects.Profiles.Store(p,(SorceressEffects.Profiles.Find(p)??new()) with{Shield=saved.Energy});
            NecromancerEffects.SetMana256(p,saved.Mana);SkillHealth.SetBalance256(p,saved.Life);p.Internalize();
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
        }
        finally{c.Delete();}
    }
}
