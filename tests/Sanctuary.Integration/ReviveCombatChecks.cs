// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

internal static class ReviveCombatChecks
{
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    internal static void Numeric()
    {
        foreach(int original in new[]{1,50,110})foreach(int level in new[]{1,50,99})foreach(int rank in new[]{1,2,20,255})foreach(int mastery in new[]{0,1,20,255})
        {
            // Only the explicit formula context is supplied here; live cases
            // below use fully validated corpse/HP plans from real deaths.
            var life=new ReviveLifePlan("formula-context",MonsterDifficulty.Hell,original,level,mastery,1,2,1,1,256,256,256);
            var result=ReviveCombat.Calculate(life,rank,_=>0);
            Require(result==new ReviveCombatPlan(Math.Min(original,level),mastery*10,50),"Pinned Revive combat formula differs");
            foreach(string element in new[]{"cold","fire","light","poison","damage","magic"})
            {
                bool refused=false;
                try{ReviveCombat.Calculate(life,rank,key=>key=="item_pierce_"+element+"_immunity"?1:0);}
                catch(NotSupportedException){refused=true;}
                Require(refused,"Unimplemented inherited sunder was silently ignored:"+element);
            }
        }
    }
    internal static void Saved(BaseCreature actor,ReviveNativeState row)
    {
        var stats=row.Setup.CombatPlan;
        Require(stats!=null&&ClassStats.Level(actor)==stats.Level&&CreatureProfiles.Snapshot(actor).Level==stats.Level
            &&ClassCombatHooks.SourceWeaponDamage(actor)
            &&PaladinAuras.NativeWeaponMultiplier(actor,true)==1+stats.DamagePercent/100.0
            &&PaladinAuras.NativeWeaponMultiplier(actor,false)==1
            &&Math.Abs(ClassGearHooks.MovementDelay(actor,1)-100.0/(100+stats.VelocityPercent))<1e-12,
            "Saved original lost effective level,physical-only mastery or speed");
    }
    internal static void Live(BaseCreature actor,BaseCreature enemy)
    {
        var row=ReviveNativeActivation.State(actor);Saved(actor,row);
        var before=ReviveBeforeDeath.Decode(row.Original.BeforeDeathJson);
        Require(ReviveCreatureSnapshots.Combat(before.Creature).Level==row.Setup.LifePlan.OriginalLevel,"Original predeath level was overwritten");
        int oldLevel=row.Setup.LifePlan.OriginalLevel,newLevel=ClassStats.Level(actor),enemyLevel=ClassStats.Level(enemy);
        int ar=ClassStats.AttackRatingAgainst(actor,enemy),defense=NamedCombat.DefenseFor(actor,enemy,ClassStats.Defense(enemy),false);
        double reduced=ClassStats.HitChance(ar,defense,newLevel,enemyLevel),prior=ClassStats.HitChance(ar,defense,oldLevel,enemyLevel);
        Require(reduced<prior&&ClassStats.CheckHit(actor,enemy,0,()=>(reduced+prior)/2,notifyMelee:false)==false,
            "Native NPC attacker did not use the reduced level in actual hit resolution");
        ar=ClassStats.AttackRatingAgainst(enemy,actor);defense=NamedCombat.DefenseFor(enemy,actor,ClassStats.Defense(actor),false);
        reduced=ClassStats.HitChance(ar,defense,enemyLevel,newLevel);prior=ClassStats.HitChance(ar,defense,enemyLevel,oldLevel);
        Require(reduced>prior&&ClassStats.CheckHit(enemy,actor,0,()=>(reduced+prior)/2,notifyMelee:false)==true,
            "Native NPC defender did not use the reduced level in actual hit resolution");

        var parts=SkillCombat.ResistedDamage(enemy,actor,100,50,50,0,0,0,false,true);
        int physical=NamedElements.Resolve256(actor,enemy,150*256,"physical",enhance:false).Damage256;
        int fire=NamedElements.Resolve256(actor,enemy,50*256,"fire",enhance:false).Damage256;
        Require(parts is{} packet&&packet.Physical256==physical&&packet.Total256==physical+fire,"Mastery did not enhance only the physical weapon channel");
        int balance=SkillHealth.Balance256(enemy);AOS.Damage(enemy,actor,100,false,50,50,0,0,0,weaponDamage:true);
        Require(SkillHealth.Balance256(enemy)==balance-physical-fire,"Actual native AOS delivery lost the prepared physical-only bonus");
        var spell=SkillCombat.ResistedDamage(enemy,actor,100,0,100,0,0,0,true,false);
        Require(spell is{} magic&&magic.Total256==NamedElements.Resolve256(actor,enemy,100*256,"fire",enhance:false).Damage256,
            "Physical mastery changed a native nonweapon fire attack");

        var auras=PaladinAuras.Snapshot();
        try
        {
            var vigor=new PaladinAuraLease(actor.Serial.Value,row.Owner,CombatAbility.Vigor,1,0,TimedEffects.Now.AddMinutes(1),VelocityPercent:43);
            var might=new PaladinAuraLease(actor.Serial.Value,row.Owner,CombatAbility.Might,1,25,TimedEffects.Now.AddMinutes(1));
            PaladinAuras.Restore(auras with{Leases=auras.Leases.Concat([vigor,might]).ToArray()});
            Require(Math.Abs(ClassGearHooks.MovementDelay(actor,1)-100.0/193)<1e-12&&PaladinAuras.NativeWeaponMultiplier(actor,true)==3.25,
                "Revive speed/damage multiplied existing aura bonuses instead of adding to their stat pools");
        }
        finally{PaladinAuras.Restore(auras);}

        var states=ReviveNativeActivation.Snapshot();bool rejected=false;
        try{ReviveNativeActivation.Restore([row with{Setup=row.Setup with{CombatPlan=row.Setup.CombatPlan with{DamagePercent=201}}}]);}
        catch(InvalidDataException){rejected=true;}
        Require(rejected&&ReviveNativeActivation.Snapshot().SequenceEqual(states),"Invalid combat plan changed the registry");
        // Decode a genuine prior shape: the optional combat property is absent.
        var oldSetup=JsonSerializer.Deserialize<ReviveNativeSetup>(JsonSerializer.Serialize(new{row.Setup.Rank,row.Setup.MaximumLife,row.Setup.Life,row.Setup.Mana,row.Setup.Stamina,row.Setup.LifePlan}));
        try
        {
            ReviveNativeActivation.Restore(states.Select(s=>s.Actor==row.Actor?s with{Setup=oldSetup}:s).ToArray());
            Require(ClassStats.Level(actor)==oldLevel&&PaladinAuras.NativeWeaponMultiplier(actor,true)==1&&ClassGearHooks.MovementDelay(actor,1)==1,
                "Legacy source-life save retroactively acquired an unrecorded combat plan");
        }
        finally{ReviveNativeActivation.Restore(states);}
        Saved(actor,row);
    }
}
