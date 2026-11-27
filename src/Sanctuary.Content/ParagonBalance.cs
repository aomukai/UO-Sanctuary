// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Engines.Spawners;
using Server.Mobiles;

namespace Sanctuary;

public static class ParagonBalance
{
    internal const int ExperienceMultiplier = 3;
    private const int LegacySpawnChance = 10;

    public static void Configure()
    {
        ClassMapHooks.ParagonSpawning = (creature, map) => TrySpawn(creature, map, Utility.Random(100));
        EventSink.ServerStarted += () =>
        {
            foreach (var item in World.Items.Values)
                if (item is Spawner spawner)
                    UpdateSavedPool(spawner);
        };
    }

    internal static bool TrySpawn(BaseCreature creature, Map map, int roll)
    {
        if (roll >= Paragon.SpawnChance || map == null || map == Map.Internal
            || creature is not { Deleted: false, Alive: true, Controlled: false, Summoned: false, IsAnimatedDead: false, IsParagon: false,
                NoKillAwards: false, FightMode: not FightMode.None }
            || creature is BaseVendor or BaseEscortable or BaseHire or BaseChampion or Harrower or Clone)
            return false;

        if (creature is EncounterCreature encounter)
        {
            if (encounter.Grade != EncounterGrade.Ordinary || encounter.ParagonAlreadyRolled
                || encounter is ShameEncounter { IsExpeditionActor: true })
                return false;
            encounter.PromoteSpawnToParagon();
            return true;
        }

        if (CreatureGold.Policy(creature.GetType(), MonsterVariants.Difficulty(creature)) is not { Eligible: true })
            return false;
        creature.IsParagon = true;
        return true;
    }

    internal static int SpawnChance(int chance) => chance == LegacySpawnChance ? Paragon.SpawnChance : chance;

    internal static void UpdateSavedPool(Spawner spawner)
    {
        var species = spawner switch
        {
            ShameOne or ShameTwo or ShameThree or ShameFour or ShameMage => nameof(ShameEncounter),
            WrongOne or WrongTwo or WrongDrake1 or WrongDrake2 => nameof(WrongEncounter),
            _ => null
        };
        if (species == null)
            return;

        // Placed spawners saved the original probability as a constructor argument.
        foreach (var entry in spawner.Entries)
            if (entry.SpawnedName == species && entry.Parameters is { } args && args.EndsWith(" 10", StringComparison.Ordinal))
                entry.Parameters = $"{args[..^3]} {Paragon.SpawnChance}";
    }
}
