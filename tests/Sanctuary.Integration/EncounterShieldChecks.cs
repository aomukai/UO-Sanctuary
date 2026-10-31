// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class EncounterShieldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(EncounterShieldChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("encounter-shields-check","Encounter damage uses existing pre-resistance shield consumers",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("encounter-shields-check: passed");}catch(Exception e){Log.Error(e,"encounter-shields-check: failed");}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner(DiabloClass kind)
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="PoisonDaggerFixtureEncounterShield"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,kind);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Shield fixture strength");}
        FixtureSpace.Corridor(p,6920,30,3500);SkillHealth.SetBalance256(p,p.HitsMax*256);NecromancerEffects.SetMana256(p,p.ManaMax*256);
        Require(p.PhysicalResistance==0&&p.FireResistance==0&&p.ColdResistance==0&&p.EnergyResistance==0,"Bare resistance fixture");return p;
    }
    private static void Train(PlayerMobile p,string id)
    {
        if(ClassCreation.Profiles.GetRank(p.Serial,id)>0){return;}ClassCreation.Profiles.TryGet(p.Serial,out var profile);
        Require(TalentCatalog.TryGet(profile.Class,id,out var talent),"Shield talent");foreach(string prior in talent.Prerequisites){Train(p,prior);}
        Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Shield train "+id);
    }
    private static EncounterCreature Enemy(PlayerMobile p)
    {
        var c=new EncounterCreature{Frozen=true,CantWalk=true,NoKillAwards=true};
        c.Assign(EncounterProfiles.Profile("crypt-sentinel-60"),60,MonsterDifficulty.Normal,EncounterGrade.Ordinary,ChampionKind.Champion,[],EliteAura.None,null,new Random(173));
        c.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);c.AIObject.AITimer.Stop();Require(EliteCombat.Enemy(c,p,18),"Encounter hostility/LOS fixture");return c;
    }
    private static void Bone(PlayerMobile p)
    {
        ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=0;p.Frozen=p.Paralyzed=false;
        NecromancerEffects.SetMana256(p,p.ManaMax*256);
        Require(ClassCombat.Perform(p,CombatAbility.BoneArmor,null,p.Location,Core.TickCount),"Learned Bone Armor start");
        Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.Armor256(p)==20*256,"Learned Bone Armor release");
    }
    private static void BoneCases()
    {
        var p=Owner(DiabloClass.Necromancer);Train(p,"necromancer.bonearmor");var c=Enemy(p);
        try
        {
            Bone(p);int hp=SkillHealth.Balance256(p);
            Require(EliteCombat.Damage(c,p,physical:5)==0&&SkillHealth.Balance256(p)==hp&&NecromancerEffects.Armor256(p)==15*256,"Encounter physical bypassed Bone Armor");
            Require(EliteCombat.Damage(c,p,physical:30)==15&&SkillHealth.Balance256(p)==hp-15*256&&NecromancerEffects.Armor256(p)==0,"Encounter Bone Armor overflow/removal");
            hp=SkillHealth.Balance256(p);Require(EliteCombat.Damage(c,p,physical:5)==5&&SkillHealth.Balance256(p)==hp-5*256,"Unshielded encounter damage changed");
            Bone(p);hp=SkillHealth.Balance256(p);for(int i=0;i<20;i++){Require(EliteCombat.Damage(c,p,physical:.25)==0,"Fractional protected encounter loss");}
            Require(NecromancerEffects.Armor256(p)==15*256&&SkillHealth.Balance256(p)==hp,"Encounter rounded fractional pool consumption");
            Bone(p);hp=SkillHealth.Balance256(p);Require(EliteCombat.Damage(c,p,physical:10,fire:3,cold:4,lightning:5,magic:6)==18
                &&SkillHealth.Balance256(p)==hp-18*256&&NecromancerEffects.Armor256(p)==10*256,"Mixed encounter channels consumed physical-only armor");
            var armor=PoisonDaggerChecks.Named(p,"Shaftstop");Bone(p);hp=SkillHealth.Balance256(p);
            Require(p.PhysicalResistance==30&&EliteCombat.Damage(c,p,physical:30)==7&&SkillHealth.Balance256(p)==hp-7*256&&NecromancerEffects.Armor256(p)==0,"Encounter absorbed after physical resistance");
            p.AddToBackpack(armor);armor.Delete();ClassGear.Refresh(p);Bone(p);hp=SkillHealth.Balance256(p);
            c.MoveToWorld(new(p.X+19,p.Y,p.Z),p.Map);Require(EliteCombat.Damage(c,p,physical:5)==0&&NecromancerEffects.Armor256(p)==20*256&&SkillHealth.Balance256(p)==hp,"Rejected encounter spent shield");
            c.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);SkillHealth.SetBalance256(p,p.HitsMax*256);hp=SkillHealth.Balance256(p);
            c.Frozen=false;EliteCombat.Hit(c,p,30,ResistanceType.Physical,random:new Random(173));c.Frozen=true;
            Require(NecromancerEffects.Armor256(p)==0&&SkillHealth.Balance256(p)==hp-10*256,"Actual encounter weapon producer bypassed armor");
        }
        finally{c.Delete();p.Delete();}
    }
    private static void WardCases()
    {
        var p=Owner(DiabloClass.Warlock);Train(p,"warlock.psychicward");var c=Enemy(p);
        try
        {
            int maximum=PsychicWard.Values(SkillMechanics.Values(p,CombatAbility.PsychicWard)).Capacity256;
            Require(PsychicWard.CastPrepared(p)&&PsychicWard.State(p).Remaining256==maximum,"Prepared Ward creation with prerequisite synergies");
            // Inspected injured pool isolates overflow without discarding the
            // captured maximum or inventing zero-rank learned prerequisites.
            PsychicWard.Restore(PsychicWard.Snapshot().Select(x=>x.Owner==p.Serial.Value?x with{Remaining256=15*256}:x).ToArray());int hp=SkillHealth.Balance256(p);
            Require(EliteCombat.Damage(c,p,physical:5)==0&&SkillHealth.Balance256(p)==hp&&PsychicWard.State(p).Remaining256==10*256,"Encounter bypassed prepared Ward");
            Require(EliteCombat.Damage(c,p,fire:4)==4&&SkillHealth.Balance256(p)==hp-4*256&&PsychicWard.State(p).Remaining256==10*256,"Encounter element consumed Ward");
            Require(EliteCombat.Damage(c,p,physical:15)==5&&SkillHealth.Balance256(p)==hp-9*256&&PsychicWard.State(p)==null,"Encounter Ward overflow/removal");
            Require(!WarlockCombat.Implemented(CombatAbility.PsychicWard),"Prepared incoming fix enabled incomplete Ward casting");
        }
        finally{c.Delete();p.Delete();PsychicWard.Prune();}
    }
    private static void EnergyCases()
    {
        var p=Owner(DiabloClass.Sorceress);var c=Enemy(p);
        try
        {
            SorceressEffects.CastShield(p,new(SkillMechanics.For(CombatAbility.EnergyShield),_=>1,_=>0,99));
            Require(SorceressEffects.Shield(p).AbsorbPercent==20,"Energy Shield rank1 fixture");NecromancerEffects.SetMana256(p,20*256);int hp=SkillHealth.Balance256(p);
            Require(EliteCombat.Damage(c,p,physical:10,fire:10)==16&&SkillHealth.Balance256(p)==hp-16*256
                &&NecromancerEffects.ManaBalance256(p)==12*256&&SorceressEffects.Shield(p)!=null,"Encounter bypassed Energy Shield/shared mana");
            hp=SkillHealth.Balance256(p);Require(EliteCombat.Damage(c,p,magic:10)==8&&SkillHealth.Balance256(p)==hp-8*256
                &&NecromancerEffects.ManaBalance256(p)==8*256,"Encounter magic bypassed Energy Shield");
            NecromancerEffects.SetMana256(p,0);hp=SkillHealth.Balance256(p);Require(EliteCombat.Damage(c,p,physical:10)==10
                &&SkillHealth.Balance256(p)==hp-10*256&&SorceressEffects.Shield(p)==null,"Zero-mana encounter shield removal");
        }
        finally{c.Delete();p.Delete();}
    }
    private static void Check()
    {
        BoneCases();WardCases();EnergyCases();
        Log.Information("Encounter shields:learned Bone Armor direct/mixed/fractional/resistance/rejection/weapon hits,prepared Ward and existing Energy Shield mana consumers pass; no modern mixed-shield priority or Ward reactive-stun claim");
    }
}
