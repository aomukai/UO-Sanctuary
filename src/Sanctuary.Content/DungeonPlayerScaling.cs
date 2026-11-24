// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Regions;

namespace Sanctuary;

internal static class DungeonPlayerScaling
{
    internal const int Players = 5;

    internal static int For(BaseCreature creature) => creature is EncounterCreature encounter
        ? encounter.EncounterPlayers
        : IsWildDungeonCreature(creature) ? Players : 1;

    internal static bool Contains(Map map, Point3D point, Region region)
    {
        if (map == null || map == Map.Internal)
            return false;
        return region?.IsPartOf<DungeonRegion>() == true || HavenCrypts.IsCryptArea(map, point)
            || map == Map.Trammel && NewHavenPopulation.MineArea.Contains(point);
    }

    internal static bool IsWildDungeonCreature(BaseCreature creature) =>
        creature is { Controlled: false, Summoned: false, FightMode: not FightMode.None }
            && creature is not (BaseVendor or BaseHire)
            && Contains(creature.Map, creature.Location, creature.Region);

    internal static int Scale(int amount, int players) => checked(amount * (players + 1) / 2);

    internal static int NoDrop(int noDrop, int weight, int players)
    {
        // Simulated extra players contribute half weight to NoDrop.
        int effectivePlayers = (players + 1) / 2;
        if (effectivePlayers <= 1 || noDrop == 0)
            return noDrop;
        double probability = Math.Pow((double)noDrop / (noDrop + weight), effectivePlayers);
        return (int)(weight * probability / (1 - probability));
    }

    public static void Initialize() => EventSink.ServerStarted += () =>
        Timer.DelayCall(TimeSpan.Zero, () =>
        {
            // Apply to persistent monsters after all dungeon regions have registered.
            foreach (var mobile in World.Mobiles.Values)
                if (mobile is BaseCreature { Alive: true, Deleted: false } creature && IsWildDungeonCreature(creature))
                    CreatureProfiles.Initialize(creature);
        });
}
