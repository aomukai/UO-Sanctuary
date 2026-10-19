// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server;

namespace Sanctuary;

// Catalogs are read once during Configure; combat uses validated in-memory records.
public static class BalanceCatalog
{
    private static readonly JsonSerializerOptions _options = CreateOptions();
    [CallPriority(-100)]
    public static void Configure()
    {
        EquipmentBalance.Load();
        PotionBeltCatalog.Load();
        EquipmentEconomy.Load();
        AffixBalance.Load();
        MonsterBalance.Load();
        CreatureProfiles.Load();
    }
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
    public static T[] Read<T>(string name) => JsonSerializer.Deserialize<T[]>(
        File.ReadAllText(Path.Join(Core.BaseDirectory, "Data", "Sanctuary", "Balance", name + ".json")), _options)
        ?? throw new InvalidDataException($"Empty balance catalog: {name}");
}
