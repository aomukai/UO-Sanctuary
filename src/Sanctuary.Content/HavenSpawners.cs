// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Engines.Spawners;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class HavenBandit : Spawner
{
    [Constructible]
    public HavenBandit() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2))
    {
        Name = nameof(HavenBandit);
        HavenSpawnEntries.Configure(this, (HavenEncounter.Bandit, 4, 6, 90), (HavenEncounter.Thief, 4, 6, 10));
    }
}

[SerializationGenerator(0)]
public partial class HavenUndead : Spawner
{
    [Constructible]
    public HavenUndead() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2))
    {
        Name = nameof(HavenUndead);
        HavenSpawnEntries.Configure(this, (HavenEncounter.Skeleton, 4, 6, 50), (HavenEncounter.Zombie, 4, 6, 50));
    }
}

[SerializationGenerator(0)]
public partial class HavenGhost : Spawner
{
    [Constructible]
    public HavenGhost() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2))
    {
        Name = nameof(HavenGhost);
        HavenSpawnEntries.Configure(this, (HavenEncounter.Spectre, 6, 8, 10), (HavenEncounter.Wraith, 6, 8, 40),
            (HavenEncounter.SkeletonKnight, 8, 10, 20), (HavenEncounter.Ghoul, 9, 11, 30));
    }
}

[SerializationGenerator(0)]
public partial class HavenMines : Spawner
{
    [Constructible]
    public HavenMines() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2))
    {
        Name = nameof(HavenMines);
        HavenSpawnEntries.Configure(this, (HavenEncounter.EarthElemental, 6, 8, 90), (HavenEncounter.GraniteElemental, 7, 9, 10));
    }
}

[SerializationGenerator(0)]
public partial class HavenBogling : Spawner
{
    private const int MinimumLevel = 1, MaximumLevel = 3;
    private const string OldParameters = HavenEncounter.Bogling + " 2 6";

    [Constructible]
    public HavenBogling() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2))
    {
        Name = nameof(HavenBogling);
        HavenSpawnEntries.Configure(this, (HavenEncounter.Bogling, MinimumLevel, MaximumLevel, 100));
    }

    public static void Configure() => EventSink.ServerStarted += UpgradePlacedSpawners;

    private static void UpgradePlacedSpawners()
    {
        foreach (var spawner in World.Items.Values.OfType<HavenBogling>().ToArray())
            spawner.UpgradeRange();
    }

    private void UpgradeRange()
    {
        bool removed = false;
        foreach (var entry in Entries)
        {
            if (entry.SpawnedName != nameof(HavenEncounter) || entry.Parameters != OldParameters)
                continue;

            entry.Parameters = $"{HavenEncounter.Bogling} {MinimumLevel} {MaximumLevel}";
            foreach (var bogling in entry.Spawned.OfType<HavenEncounter>().Where(mob =>
                         !mob.Deleted && !mob.Controlled && !mob.Summoned && mob.Dungeon.Id == HavenEncounter.Bogling
                         && mob.Basis.Level > MaximumLevel).ToArray())
            {
                Remove(bogling);
                bogling.Delete();
                removed = true;
            }
        }
        if (removed && Running)
            Spawn();
    }
}

internal static class HavenSpawnEntries
{
    internal static void Configure(Spawner spawner, params (string Species, int Min, int Max, int Weight)[] entries)
    {
        spawner.SpawnBounds = default;
        spawner.WalkingRange = 2;
        foreach (var (species, min, max, weight) in entries)
            spawner.AddEntry(nameof(HavenEncounter), weight, dotimer: false, parameters: $"{species} {min} {max}");
    }
}
