// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Engines.Spawners;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class DespiseTroll : Spawner
{
    [Constructible]
    public DespiseTroll() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2))
    {
        Name = nameof(DespiseTroll);
        DeepDespiseSpawns.Configure(this, FrontierEncounter.DespiseTroll, FrontierEncounter.DespiseHarpy, 24);
    }
}

[SerializationGenerator(0)]
public partial class DespiseOgre : Spawner
{
    [Constructible]
    public DespiseOgre() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2))
    {
        Name = nameof(DespiseOgre);
        DeepDespiseSpawns.Configure(this, FrontierEncounter.DespiseOgre, FrontierEncounter.DespiseStoneHarpy, 26);
    }
}

[SerializationGenerator(0)]
public partial class DespiseOgreLord : Spawner
{
    private const int LordWeight = 80, MageWeight = 20;

    [Constructible]
    public DespiseOgreLord() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2))
    {
        Name = nameof(DespiseOgreLord);
        DeepDespiseSpawns.Configure(this, FrontierEncounter.DespiseOgreLord, FrontierEncounter.DespiseOgreMage, 28, LordWeight, MageWeight);
    }

    [AfterDeserialization]
    private void UpdateSavedEntries()
    {
        foreach (var entry in Entries)
        {
            if (entry.SpawnedName != nameof(FrontierEncounter) || entry.Parameters is not { } parameters)
                continue;

            var args = parameters.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (args.Length < 2 || args[0] is not (FrontierEncounter.DespiseOgreLord or FrontierEncounter.DespiseOgreMage))
                continue;

            if (int.TryParse(args[1], out int level) && level > DeepDespiseSpawns.MaximumLevel)
                entry.Enabled = false;

            if (entry.SpawnedProbability != 100)
                continue;
            if (args[0] == FrontierEncounter.DespiseOgreLord)
                entry.SpawnedProbability = LordWeight;
            else
                entry.SpawnedProbability = MageWeight;
        }
    }
}

internal static class DeepDespiseSpawns
{
    internal const int MaximumLevel = 30;

    private static readonly HashSet<Guid> _retiredPresets =
    [
        new("06aceaf6-1a2d-5816-b6de-affe59edc760"),
        new("6cc25282-0be7-5bc4-908e-95cd3766bea9"),
        new("27be51c6-61e4-5ec4-a0f7-c9c3de209693"),
        new("752d6276-902a-50b7-9c61-ec9845dae388"),
        new("db3ade21-2f0c-52c6-a4d8-b3e1b2355f87"),
        new("2e799959-7d9d-57dd-b434-08c97297fbce"),
        new("804c9800-c6ad-5e44-ae9e-f606e3394636"),
        new("40a04400-f2c7-5a28-9c54-81013b3de54e"),
        new("4a91a803-8000-5073-9898-171015be884d"),
        new("a577a2b2-6e15-5099-bef1-dbc883f778d2"),
        new("41e8f029-a15c-5b45-a8c1-2cbcc3ee1baf"),
        new("c39088f9-373e-573f-9fe1-aa194b1bacc8"),
        new("c3766b7c-7cc0-53d1-b966-b20cce2479f5"),
        new("80b66f0a-e665-5386-8ff9-5508066ebe42"),
        new("18e8581f-d7e0-5895-ab96-0b6445235b2b"),
        new("2d3024cc-1899-513b-8715-134e46f354da"),
        new("3cfa26b1-9971-53d0-a8d1-b687d8af31a0"),
        new("38d0446d-e155-580e-b0f0-390c4b2ea8ee"),
        new("7138de24-3f7c-51ab-9791-7b0506d3021a"),
        new("7fada9ff-140e-5259-834e-eb76665eb3ab"),
        new("bb159c01-f152-5115-bbd4-66f5ccff0bc2"),
        new("37178f9b-c254-5553-a3f2-539f928177cf"),
        new("52e1c171-81b3-5063-98a9-0db869b742b3"),
    ];

    internal static void Configure(Spawner spawner, string first, string second, int minimumLevel, int firstWeight = 100, int secondWeight = 100)
    {
        spawner.SpawnBounds = default;
        spawner.WalkingRange = 2;
        for (int level = minimumLevel; level <= Math.Min(minimumLevel + 4, MaximumLevel); level++)
        {
            spawner.AddEntry(nameof(FrontierEncounter), firstWeight, dotimer: false, parameters: $"{first} {level} 0");
            spawner.AddEntry(nameof(FrontierEncounter), secondWeight, dotimer: false, parameters: $"{second} {level} 0");
        }
    }

    internal static int RetirePresets()
    {
        using var retired = PooledRefList<BaseSpawner>.Create();
        foreach (var item in World.Items.Values)
            if (item is BaseSpawner spawner && _retiredPresets.Contains(spawner.Guid))
                retired.Add(spawner);
        foreach (var spawner in retired)
            spawner.Delete();
        return retired.Count;
    }
}
