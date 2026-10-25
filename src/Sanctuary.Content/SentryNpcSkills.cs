// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;

namespace Sanctuary;

// NPC312's percentages/radius/shot count are distinct from player276's
// lightning damage parameters. Keep the complete pinned rows and reuse the
// existing formula evaluator instead of silently borrowing the player row.
internal static class SentryNpcSkills
{
    private static readonly Dictionary<int,SkillDefinition> Rows=new();
    internal static void Load()
    {
        Rows.Clear();
        foreach(var row in BalanceCatalog.Read<SkillDefinition>("sentry-npc-skills"))
        {
            if(row.SourceId is not(312 or 324) || row.Ability!=CombatAbility.DeathSentry || row.CharacterClass!=DiabloClass.Assassin
                || row.SourceKey!=(row.SourceId==312?"mon death sentry":"death sentry ltng") || !Rows.TryAdd(row.SourceId,row))
            {throw new InvalidDataException("Invalid Death Sentry NPC source row");}
        }
        if(Rows.Count!=2){throw new InvalidDataException("Incomplete Death Sentry NPC rows");}
    }
    internal static SkillDefinition For(int id)=>Rows.TryGetValue(id,out var row)?row:throw new NotSupportedException("Unmapped sentry NPC skill");
    internal static SkillValues Values(int id,int rank,int level,Func<string,int> hard)
    {
        var row=For(id);
        return new(row,key=>string.Equals(key,row.SourceKey,StringComparison.OrdinalIgnoreCase)?rank:hard(key),hard,level);
    }
}
