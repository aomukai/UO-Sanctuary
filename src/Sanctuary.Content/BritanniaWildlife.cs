// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Engines.Spawners;
using Server.Logging;

namespace Sanctuary;

public static class BritanniaWildlife
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(BritanniaWildlife));
    private static readonly HashSet<string> _animals = new(StringComparer.OrdinalIgnoreCase)
    {
        "Alligator", "Bird", "BlackBear", "Boar", "BrownBear", "Bull", "Cat", "Chicken", "Cougar", "Cow", "Dog",
        "Eagle", "Goat", "GreatHart", "GreyWolf", "GrizzlyBear", "Hind", "Horse", "JackRabbit", "Llama", "Panther",
        "Pig", "PolarBear", "Rabbit", "Rat", "Sheep", "Snake", "TimberWolf"
    };

    public static void Configure() => EventSink.ServerStarted += Install;

    private static void Install()
    {
        int created = GraveyardPopulation.InstallSpawners(Definitions());
        _logger.Information("Britannia wildlife: installed {Count} animal spawners", created);
    }

    internal static IEnumerable<SpawnerDto> Definitions()
    {
        string root = Path.Join(Core.BaseDirectory, "Data", "Spawns");
        foreach (string layer in new[] { "shared", Core.Expansion >= Expansion.SA ? "post-uoml" : "uoml" })
        foreach (string file in new[] { "WildLife.json", "TownsLife.json", "Outdoors.json" })
        {
            string path = Path.Join(root, layer, "trammel", file);
            var spawners = JsonSerializer.Deserialize<SpawnerDto[]>(File.ReadAllText(path), SpawnerJsonSerializer.Options);
            foreach (var definition in spawners.OfType<SpawnerDataDto>())
            {
                // New Haven has its own encounter and wildlife population.
                if (definition.Map != Map.Trammel || !BritanniaWorld.Include(definition.Location))
                    continue;

                var animals = definition.Entries.Where(entry => entry.Enabled && _animals.Contains(entry.SpawnedName)
                    && string.IsNullOrWhiteSpace(entry.Parameters) && string.IsNullOrWhiteSpace(entry.Properties)).ToList();
                if (animals.Count == 0)
                    continue;

                yield return definition with
                {
                    Name = "Britannia Wildlife",
                    Count = Math.Min(definition.Count, animals.Sum(entry => entry.SpawnedMaxCount)),
                    Entries = animals
                };
            }
        }
    }
}
