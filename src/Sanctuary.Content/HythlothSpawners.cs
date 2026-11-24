// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Engines.Spawners;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class HythLow : Spawner
{
    private static readonly (HythlothRole Role, int Weight)[] _pool =
    [
        (HythlothRole.Acolyte, 34),
        (HythlothRole.RoyalSoldier, 33),
        (HythlothRole.UndeadServant, 33)
    ];

    [Constructible]
    public HythLow() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2)) =>
        HythlothSpawns.Configure(this, _pool);

    [AfterDeserialization]
    private void RestorePool() => HythlothSpawns.RestorePool(this, _pool);
}

[SerializationGenerator(0)]
public partial class HythMed : Spawner
{
    private static readonly (HythlothRole Role, int Weight)[] _pool =
    [
        (HythlothRole.TempleGuard, 25),
        (HythlothRole.RoyalArbalest, 25),
        (HythlothRole.Apprentice, 25),
        (HythlothRole.UndeadCleric, 25)
    ];

    [Constructible]
    public HythMed() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2)) =>
        HythlothSpawns.Configure(this, _pool);

    [AfterDeserialization]
    private void RestorePool() => HythlothSpawns.RestorePool(this, _pool);
}

[SerializationGenerator(0)]
public partial class HythHigh : Spawner
{
    private static readonly (HythlothRole Role, int Weight)[] _pool =
    [
        (HythlothRole.RoyalKnight, 50),
        (HythlothRole.SkeletalMage, 50)
    ];

    [Constructible]
    public HythHigh() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2)) =>
        HythlothSpawns.Configure(this, _pool);

    [AfterDeserialization]
    private void RestorePool() => HythlothSpawns.RestorePool(this, _pool);
}

[SerializationGenerator(0)]
public partial class HythBoss : Spawner
{
    [Constructible]
    public HythBoss(string boss) : base(1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(7))
    {
        var role = boss.ToLowerInvariant() switch
        {
            "herald" => HythlothRole.Herald,
            "jester" => HythlothRole.Jester,
            "chamberlain" => HythlothRole.Chamberlain,
            "executioner" => HythlothRole.Executioner,
            _ => throw new ArgumentException("Choose Herald, Jester, Chamberlain, or Executioner. King Leoric spawns in the campaign arena.", nameof(boss))
        };
        HythlothSpawns.Configure(this, (role, 100));
        Name = $"HythBoss {role}";
    }
}

internal static class HythlothSpawns
{
    internal static void Configure(Spawner spawner, params (HythlothRole Role, int Weight)[] pool)
    {
        spawner.Name = spawner.GetType().Name;
        spawner.HomeRange = 3;
        spawner.WalkingRange = 3;
        spawner.SpawnLocationIsHome = true;
        foreach (var (role, weight) in pool)
            spawner.AddEntry(nameof(HythlothEncounter), probability: weight, dotimer: false, parameters: role.ToString());
    }

    internal static void RestorePool(Spawner spawner, (HythlothRole Role, int Weight)[] pool)
    {
        foreach (var entry in spawner.Entries)
        {
            if (entry.SpawnedName != nameof(HythlothEncounter))
                continue;

            foreach (var (role, weight) in pool)
                if (string.Equals(entry.Parameters, role.ToString(), StringComparison.OrdinalIgnoreCase)
                    && entry.SpawnedProbability != weight)
                    entry.SpawnedProbability = weight;
        }

        if (spawner.Count == 1 && spawner.Spawned.Count <= 1)
            return;

        // Apply the single-creature cap while loading, then trim old packs after
        // their entities have finished deserializing. Keep an existing survivor.
        spawner.Count = 1;
        Timer.DelayCall(TimeSpan.Zero, () =>
        {
            if (spawner.Deleted)
                return;

            var excess = spawner.Spawned.Keys.Where(spawn => !spawn.Deleted)
                .OrderBy(spawn => spawn.Serial.Value).Skip(1).ToArray();
            foreach (var spawn in excess)
            {
                spawner.Remove(spawn);
                spawn.Delete();
            }
        });
    }
}
