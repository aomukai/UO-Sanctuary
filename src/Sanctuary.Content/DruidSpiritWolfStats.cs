// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record SpiritWolfSpawn(SummonSpawnStats Stats,int Regeneration256);
internal sealed record SpiritWolfBirth(int Rank,int Level,int Life256,int BaseDefense,int BaseAttack,int Regeneration256,
    int ArmorPercent,int AttackBonus,int ColdMinimum,int ColdMaximum,int ColdFrames,int FireResistance,int ColdResistance,
    int LightningResistance,int PoisonResistance,int ColdSunder,int RequestedEquipmentLevel);
internal readonly record struct SpiritWolfCold(int Minimum,int Maximum,int Frames);

// Prepared do119 numeric composition only. Raw base stats and copied passives
// stay distinct until the actual monster defense/attack consumers are composed.
// No learned handler, body, timing or cold-sunder consumer is enabled here.
internal static class DruidSpiritWolfStats
{
    private static void Route(SkillValues values)
    {
        var row=SkillMechanics.For(CombatAbility.SummonSpiritWolf);
        if(values.Ability!=CombatAbility.SummonSpiritWolf || values.Rank is <1 or >255 || row.SourceId!=227
            || row.Number("srvdofunc")!=119 || row.Text("summon")!="spiritwolf" || row.Text("pettype")!="spiritwolf"
            || row.Text("EType")!="cold" || values.Mana256!=15*256 || row.Text("passivestat6")!="item_pierce_cold_immunity")
        {throw new NotSupportedException("Unsupported Spirit Wolf source route");}
    }
    internal static SpiritWolfSpawn Spawn(MonsterDifficulty difficulty)
    {
        if(difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell){throw new ArgumentOutOfRangeException(nameof(difficulty));}
        int F(string name)=>NecromancerSummons.Source("spiritwolf",name,difficulty);
        if(NecromancerSummons.Source("spiritwolf","noRatio")!=1 || F("minHP")!=130 || F("maxHP")!=130 || F("AC")!=67 || F("A1TH")!=50)
        {throw new NotSupportedException("Unsupported Spirit Wolf noRatio spawn row");}
        int life=checked(F("minHP")*256);
        // Initializer flags7 do not write weapon AR. The explicit-level base
        // helper and additive tohit passive are separate later operations.
        return new(new(life,F("AC"),0),checked(life*NecromancerSummons.Source("spiritwolf","DamageRegen"))>>12);
    }
    internal static SpiritWolfCold Cold(SkillValues values)
    {
        Route(values);
        // edmn/edmx use a4=0 and then >>8: captured whole-life passive stats,
        // with effective Grizzly synergy but without owner item mastery.
        return new(checked((int)values.Eval("passivecalc3")),checked((int)values.Eval("passivecalc4")),
            checked((int)values.Eval("passivecalc5")));
    }
    internal static SpiritWolfBirth Birth(SkillValues values,MonsterDifficulty difficulty,bool expansion,Func<string,int> accrued)
        =>Apply(values,difficulty,expansion,Spawn(difficulty),accrued);
    internal static SpiritWolfBirth Apply(SkillValues values,MonsterDifficulty difficulty,bool expansion,SpiritWolfSpawn spawn,Func<string,int> accrued)
    {
        Route(values);ArgumentNullException.ThrowIfNull(accrued);
        if(spawn?.Stats is not{Life256:>0,Defense:>=0,Attack:>=0} || spawn.Regeneration256<0){throw new ArgumentException("Invalid supplied Spirit Wolf spawn");}
        int ownerLevel=checked((int)values.Formula("ulvl"));if(ownerLevel is <1 or >99){throw new ArgumentOutOfRangeException(nameof(values));}
        var basis=ValkyrieCreation.BaseAtLevel(Math.Max(1,checked((int)values.Eval("calc2"))),(int)difficulty,expansion,spawn.Stats);
        int life=checked(spawn.Stats.Life256+SourcePercentage.Calculate(spawn.Stats.Life256,checked((int)values.Eval("calc1")),100));
        var cold=Cold(values);
        return new(values.Rank,basis.Level,life,basis.Defense,basis.Attack,spawn.Regeneration256,
            checked((int)values.Eval("passivecalc1")),checked((int)values.Eval("passivecalc2")),cold.Minimum,cold.Maximum,cold.Frames,
            checked((int)values.Eval("aurastatcalc1")),checked((int)values.Eval("aurastatcalc2")),
            checked((int)values.Eval("aurastatcalc3")),checked((int)values.Eval("aurastatcalc4")),
            // Current slot6 is retained as source data; the old five-slot
            // proof does not certify a modern nonzero-sunder consumer.
            checked((int)values.StatEval("passivecalc6",accrued)),Math.Clamp(checked(3*values.Rank),1,ownerLevel));
    }
    internal static int Limit(SkillValues values)
    {Route(values);return checked((int)values.Eval("petmax"));}
}
