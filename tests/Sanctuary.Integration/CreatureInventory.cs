using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CreatureInventory
{
    public static void Configure()
    {
        if (Environment.GetEnvironmentVariable("SANCTUARY_TEST_CREATURE_INVENTORY") is { } path)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(typeof(BaseCreature).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(BaseCreature).IsAssignableFrom(type))
                .OrderBy(type => type.FullName)
                .Select(type => new { type = type.FullName, parent = type.BaseType.FullName,
                    role = typeof(BaseVendor).IsAssignableFrom(type) ? "Vendor" : "Native" }),
                new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
