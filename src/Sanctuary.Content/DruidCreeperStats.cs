// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record CreeperSpawn(SummonSpawnStats Stats,int Regeneration256);
internal sealed record CreeperBirth(int Rank,int Level,int Life256,int Defense,int Attack,int Regeneration256,
    int Pierce,int VineAttackRank,int RequestedEquipmentLevel);
internal readonly record struct CreeperPoison(int Minimum256,int Maximum256,int Frames,int Count=0);

// Prepared numeric components. No Creeper actor or learned handler is enabled.
// In particular, supplied SkillValues/mastery are not proof of actual NPC-to-
// owner stat transport, and birth level does not imply Raven's base-stat route.
internal static class DruidCreeperStats
{
    private static void Route(SkillValues values)
    {
        var row=SkillMechanics.For(CombatAbility.PoisonCreeper);
        if(values.Ability!=CombatAbility.PoisonCreeper || values.Rank is <1 or >255 || row.SourceId!=222
            || row.Number("srvdofunc")!=115 || row.Text("summon")!="plaguepoppy" || row.Text("pettype")!="vine"
            || values.Eval("petmax")!=1 || values.Mana256!=8*256 || row.Text("sumskill1")!="Vine Attack"
            || row.Text("passivestat1")!="item_pierce_poison_immunity")
        {throw new NotSupportedException("Unsupported Creeper source route");}
    }
    internal static CreeperSpawn Spawn(MonsterDifficulty difficulty)
    {
        if(difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell){throw new ArgumentOutOfRangeException(nameof(difficulty));}
        int F(string name)=>NecromancerSummons.Source("plaguepoppy",name,difficulty);
        if(NecromancerSummons.Source("plaguepoppy","noRatio")!=1 || NecromancerSummons.Source("plaguepoppy","Align")!=1
            || F("minHP")<=0 || F("minHP")!=F("maxHP") || F("A1TH")!=0)
        {throw new NotSupportedException("Unsupported Creeper noRatio spawn");}
        int life=checked(F("minHP")*256);
        // Initializer flags7 populate life/defense/XP, not weapon attack rating.
        // Regeneration is seeded before the passive life multiplier.
        return new(new(life,F("AC"),0),checked(life*NecromancerSummons.Source("plaguepoppy","DamageRegen"))>>12);
    }
    internal static CreeperBirth Birth(SkillValues values,MonsterDifficulty difficulty,Func<string,int> accrued)
        =>Apply(values,Spawn(difficulty),accrued);
    internal static CreeperBirth Apply(SkillValues values,CreeperSpawn spawn,Func<string,int> accrued)
    {
        Route(values);ArgumentNullException.ThrowIfNull(accrued);
        if(spawn?.Stats is not{Life256:>0,Defense:>=0,Attack:>=0} || spawn.Regeneration256<0){throw new ArgumentException("Invalid supplied Creeper spawn");}
        var basis=spawn.Stats;
        int life=checked(basis.Life256+SourcePercentage.Calculate(basis.Life256,checked((int)values.Eval("calc1")),100));
        // do115 overwrites explicit level, but does not invoke Raven's helper
        // that would add level-based defense/AR or clamp its lookup index.
        return new(values.Rank,Math.Max(1,checked((int)values.Eval("calc2"))),life,basis.Defense,basis.Attack,spawn.Regeneration256,
            checked((int)values.StatEval("passivecalc1",accrued)),checked((int)values.Eval("sumsk1calc")),
            Math.Clamp(checked(3*values.Rank),1,checked((int)values.Formula("ulvl"))));
    }
    internal static CreeperPoison Poison(SkillValues values,int mastery)
    {
        Route(values);
        // Shared elemental getter/mastery arithmetic has no fire-only branch.
        // No slow or owner-stat transport policy is selected by this component.
        var ends=DruidFirestorm.Endpoints(values,mastery,SlowMissilePayload.Normal);
        return new(ends.Min,ends.Max,values.DurationFrames);
    }
}
