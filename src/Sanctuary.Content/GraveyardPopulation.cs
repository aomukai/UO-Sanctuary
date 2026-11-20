// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Commands;
using Server.Collections;
using Server.Engines.Spawners;
using Server.Logging;

namespace Sanctuary;

public static class GraveyardPopulation
{
    private static readonly Guid RetiredLichSpawn = new("06dbf592-96fb-4873-9131-2812edff87da");
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(GraveyardPopulation));
    public static void Configure() => CommandSystem.Register("Graveyard", AccessLevel.GameMaster, args =>
        args.Mobile.MoveToWorld(CombatPractice.FindFloor(Map.Felucca, 1378, 1495), Map.Felucca));
    public static void Initialize()
    {
        if (!ServerConfiguration.GetSetting("sanctuary.graveyard.enabled", true))
        {
            return;
        }
        Install();
    }
    public static void Install()
    {
        // Remove this retired saved spawner too: editing JSON alone only affects new worlds.
        using var retired = PooledRefList<BaseSpawner>.Create();
        foreach (var spawner in Map.Felucca.GetItemsInRange<BaseSpawner>(new Point3D(1393, 1494, 10), 4))
        {
            if (spawner.Guid == RetiredLichSpawn)
            {
                retired.Add(spawner);
            }
        }
        foreach (var spawner in retired)
        {
            spawner.Delete(); // Deletes its tracked lich even if it chased far away.
            _logger.Information("Graveyard: removed retired lich spawner");
        }
        int created = InstallSpawners("graveyard.json");
        _logger.Information("Graveyard: installed {Count} population spawners", created);
    }

    internal static int InstallSpawners(string fileName, bool relocate = false)
    {
        var definitions = JsonSerializer.Deserialize<SpawnerDto[]>(
            File.ReadAllText(Path.Join(Core.BaseDirectory, "Data", "Sanctuary", fileName)), SpawnerJsonSerializer.Options);
        return InstallSpawners(definitions, relocate);
    }

    internal static int InstallSpawners(IEnumerable<SpawnerDto> definitions, bool relocate = false)
    {
        var existing = new Dictionary<Guid, BaseSpawner>();
        if (relocate)
            foreach (var item in World.Items.Values)
                if (item is BaseSpawner spawner && spawner.Guid != Guid.Empty)
                    existing.TryAdd(spawner.Guid, spawner);
        int created = 0;
        foreach (var definition in definitions)
        {
            bool exists = false;
            foreach (var spawner in definition.Map.GetItemsInRange<BaseSpawner>(definition.Location, 4))
            {
                if (spawner.Guid == definition.Guid)
                {
                    exists = true;
                    break;
                }
            }
            if (exists)
            {
                continue;
            }
            var added = definition.ToSpawner();
            try
            {
                if (existing.TryGetValue(definition.Guid, out var moved))
                    moved.Delete();
                added.MoveToWorld(definition.Location, definition.Map);
                added.Respawn();
                created++;
            }
            catch
            {
                added.Delete();
                throw;
            }
        }
        return created;
    }
}
