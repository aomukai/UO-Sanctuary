// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Engines.Spawners;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class WindOutside : Spawner
{
    [Constructible]
    public WindOutside() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2))
    {
        Name = nameof(WindOutside);
        WindSpawns.Configure(this, 16, 20);
    }
}

[SerializationGenerator(0)]
public partial class WindInside : Spawner
{
    [Constructible]
    public WindInside() : base(1, TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(2))
    {
        Name = nameof(WindInside);
        WindSpawns.Configure(this, 22, 28);
    }
}

internal static class WindSpawns
{
    private static readonly HashSet<Guid> _retiredPresets =
    [
        new("49655931-0567-5125-831d-10e1d43ea50c"),
        new("d2ca5f26-ced7-55b7-a9a5-addc2b1fa198"),
        new("e1f14d6d-b7f9-543b-aa5c-2b4f5417e237"),
        new("a0942037-41c8-5a01-ac9b-6ab1d43405b2"),
        new("fecbba3e-c83a-5078-b2e4-2f40b61a6cc0"),
        new("496f9c5b-f51e-576c-85a0-0f1d0afbe782"),
        new("69b3b5bd-ffa1-5d02-a240-90fca80c6a77"),
        new("53138a47-3c90-5f1e-b9f7-286e201c5bb9"),
        new("8666254f-6d76-5211-98a9-44e63685176c"),
        new("edd648f6-4b54-55e9-93a0-4a8bb420dca8"),
        new("646fa2c5-28a8-5138-a5e9-76ef99cfa4c6"),
        new("543fbbf5-d72c-5822-9abc-380f462ba959"),
        new("8a0ce075-d1c0-5940-88a0-6531d6c073f4"),
        new("6afbe082-b362-5a15-b452-d48ccc1b73c2"),
        new("598eb233-38f5-545e-a646-005715f08e2a"),
        new("80118a93-6542-5c21-9ea7-520157a9e059"),
        new("82162737-207b-5316-ab7c-15c3b9ce4bf2"),
        new("be874e95-2334-5be4-b1eb-0aa62ca64b0f"),
        new("1951e746-3d82-5e31-99f1-facea6a2cf98")
    ];

    internal static void Configure(Spawner spawner, int minimumLevel, int maximumLevel)
    {
        spawner.SpawnBounds = default;
        spawner.WalkingRange = 2;
        for (int level = minimumLevel; level <= maximumLevel; level++)
        {
            spawner.AddEntry(nameof(FrontierEncounter), 100, dotimer: false, parameters: $"{FrontierEncounter.Drake} {level} 0");
            spawner.AddEntry(nameof(FrontierEncounter), 100, dotimer: false, parameters: $"{FrontierEncounter.Wyvern} {level} 0");
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
