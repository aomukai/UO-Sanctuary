// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Engines.Spawners;
using Server.Mobiles;

namespace Sanctuary;

internal static class WorldChestSpawnLevels
{
    internal static int Level(BaseSpawner spawner)
    {
        var difficulty = BritanniaCampaign.DungeonDifficulty(spawner.Map, spawner.Location,
            Region.Find(spawner.Location, spawner.Map)) ?? MonsterDifficulty.Normal;
        long weightedLevels = 0;
        long totalWeight = 0;

        foreach (var entry in spawner.Entries)
        {
            if (entry.Disabled || entry.SpawnedProbability <= 0)
            {
                continue;
            }

            var type = AssemblyHandler.FindTypeByName(entry.SpawnedName);
            if (type == null || !typeof(BaseCreature).IsAssignableFrom(type))
            {
                continue;
            }

            int level = typeof(EncounterCreature).IsAssignableFrom(type)
                ? EncounterLevel(type, entry.Parameters)
                : CreatureProfiles.WorldChestSpawnLevel(type, difficulty);
            if (level > 0)
            {
                weightedLevels += (long)level * entry.SpawnedProbability;
                totalWeight += entry.SpawnedProbability;
            }
        }

        // Use a stable estimate while the spawner is waiting to respawn.
        return totalWeight == 0 ? 0 : (int)(weightedLevels / totalWeight);
    }

    private static int EncounterLevel(Type type, string parameters)
    {
        var arguments = parameters?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (arguments.Length == 0)
        {
            // These spawners randomize their role in a parameterless constructor.
            var representative = type == typeof(YewEncounter) ? YewEncounter.FindSpecies(YewEncounter.BlackCultist)
                : type == typeof(HedgeEncounter) ? HedgeEncounter.FindSpecies("hedge-gazer")
                : type == typeof(BrigandEncounter) ? BrigandEncounter.FindSpecies("camp-brigand-melee")
                : null;
            return representative == null ? 0 : Midpoint(representative);
        }

        string role = arguments[0];
        if (type == typeof(BoulderEttin))
        {
            return int.TryParse(role, out int level) ? level : 0;
        }
        string normalized = role.ToLowerInvariant();
        var species = type == typeof(ShameEncounter) ? ShameEncounter.FindSpecies("shame-" + normalized)
            : type == typeof(WrongEncounter) ? WrongEncounter.FindSpecies("wrong-" + normalized)
            : type == typeof(DeceitEncounter) ? DeceitEncounter.FindSpecies("deceit-" + normalized)
            : type == typeof(DestardEncounter) ? DestardEncounter.FindSpecies("destard-" + normalized)
            : type == typeof(HythlothEncounter) ? HythlothEncounter.FindSpecies("hythloth-" + normalized)
            : type == typeof(OrcFortEncounter) ? OrcFortEncounter.FindSpecies("west-orc-" + normalized)
            : type == typeof(CovetousEncounter) ? CovetousEncounter.FindSpecies("covetous-" + role)
            : type == typeof(HavenEncounter) ? HavenEncounter.FindSpecies(role)
            : type == typeof(FrontierEncounter) ? FrontierEncounter.FindSpecies(role)
            : type == typeof(CoveEncounter) ? CoveEncounter.FindSpecies(role)
            : type == typeof(YewEncounter) ? YewEncounter.FindSpecies(role)
            : type == typeof(HedgeEncounter) ? HedgeEncounter.FindSpecies(role)
            : null;

        if (species == null)
        {
            return 0;
        }

        if ((type == typeof(CovetousEncounter) || type == typeof(CoveEncounter)
                || type == typeof(FrontierEncounter) || type == typeof(HavenEncounter))
            && arguments.Length >= 2 && int.TryParse(arguments[1], out int configured))
        {
            return type == typeof(HavenEncounter) && arguments.Length >= 3 && int.TryParse(arguments[2], out int maximum)
                ? (configured + maximum) / 2
                : configured;
        }

        return Midpoint(species);
    }

    private static int Midpoint(DungeonProfile species) => (species.MinimumLevel + species.MaximumLevel) / 2;
}
