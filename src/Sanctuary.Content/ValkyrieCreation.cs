// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sanctuary;

public sealed record SummonLevelStats(int Level,int[] Defense,int[] Attack,int[] ExpansionDefense,int[] ExpansionAttack);
public sealed record SummonSpawnStats(int Life256,int Defense,int Attack);
public sealed record SummonStat(string Name,int Value);
public sealed record ValkyrieCreationStats(int Rank,int Level,int Life256,int Defense,int Attack,int EquipmentItemLevel,
    SummonStat[] Passives,SummonStat[] Auras,SummonStat[] Skills);

// Creation component only. The caller must supply the actual spawned monster's
// stats and accrued owner stat snapshot. This does not replace the spawn HP roll,
// requested-quality equipment generator, pet AI, damage or lifecycle consumers.
public static class ValkyrieCreation
{
    private static SummonLevelStats[] _levels;
    public static void Configure()
    {
        _levels=BalanceCatalog.Read<SummonLevelStats>("summon-level-stats");
        if(_levels.Length!=111 || _levels.Where((row,i)=>row.Level!=i ||
            new[]{row.Defense,row.Attack,row.ExpansionDefense,row.ExpansionAttack}.Any(x=>x is not{Length:3} || x.Any(v=>v<0))).Any())
        {throw new InvalidDataException("Invalid pinned summon level stats");}
    }
    internal static (int Level,int Defense,int Attack) Base(int casterLevel,int rank,int difficulty,bool expansion,SummonSpawnStats spawn)
    {
        if(casterLevel is <1 or >99 || rank is <1 or >255 || difficulty is <0 or >2 || spawn is not{Life256:>0,Defense:>=0,Attack:>=0})
        {throw new ArgumentOutOfRangeException();}
        return BaseAtLevel(AmazonDecoys.Level(casterLevel,rank),difficulty,expansion,spawn);
    }
    // Raven supplies calc2 explicitly; the source retains that level on the
    // pet while clamping only the monlvl lookup, unlike the default pet level.
    internal static (int Level,int Defense,int Attack) BaseAtLevel(int level,int difficulty,bool expansion,SummonSpawnStats spawn)
    {
        if(level<1 || difficulty is <0 or >2 || spawn is not{Life256:>0,Defense:>=0,Attack:>=0})
        {throw new ArgumentOutOfRangeException();}
        var row=_levels[Math.Min(level,_levels.Length-1)];
        return (level,checked(spawn.Defense+(expansion?row.ExpansionDefense:row.Defense)[difficulty]),
            checked(spawn.Attack+(expansion?row.ExpansionAttack:row.Attack)[difficulty]));
    }
    internal static ValkyrieCreationStats Prepare(SkillValues values,int casterLevel,int difficulty,bool expansion,
        SummonSpawnStats spawn,Func<string,int> accrued)
    {
        ArgumentNullException.ThrowIfNull(accrued);
        if(values.Ability!=CombatAbility.Valkyrie){throw new ArgumentException("Expected Valkyrie creation values",nameof(values));}
        var basis=Base(casterLevel,values.Rank,difficulty,expansion,spawn);
        var definition=SkillMechanics.For(CombatAbility.Valkyrie);
        var passives=new List<SummonStat>();var auras=new List<SummonStat>();var skills=new List<SummonStat>();
        // Current pinned row has seven passive slots; the supplementary old
        // helper loops only five. Retain all current data, explicitly without
        // claiming that the modern consumer has been executed or verified.
        for(int i=1;i<=7;i++)
        {string name=definition.Text("passivestat"+i);if(name!="0"){passives.Add(new(name,checked((int)values.StatEval("passivecalc"+i,accrued))));}}
        for(int i=1;i<=6;i++)
        {string name=definition.Text("aurastat"+i);if(name!="0"){int amount=checked((int)values.Eval("aurastatcalc"+i));if(amount!=0){auras.Add(new(name,amount));}}}
        for(int i=1;i<=5;i++)
        {string name=definition.Text("sumskill"+i);if(name!="0"){int rank=checked((int)values.Eval("sumsk"+i+"calc"));if(rank>0){skills.Add(new(name,rank));}}}
        int life=checked(spawn.Life256+SourcePercentage.Calculate(spawn.Life256,checked((int)values.Eval("calc1")),100));
        return new(values.Rank,basis.Level,life,basis.Defense,basis.Attack,checked((int)values.Eval("calc2")),passives.ToArray(),auras.ToArray(),skills.ToArray());
    }
}
