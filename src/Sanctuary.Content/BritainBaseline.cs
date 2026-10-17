// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary;

public static class BritainBaseline
{
    private const string MarkerName = "Sanctuary: Britain baseline v1";
    private static readonly Point3D _markerLocation = new(1438, 1696, 0);
    private static readonly Rectangle2D _bounds = new(1400, 1620, 91, 101);
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(BritainBaseline));
    private static SpawnerDto[] _vendors;

    public static void Configure()
    {
        CommandSystem.Register("SanctuaryTest", AccessLevel.GameMaster, Travel);
        ConsoleInputHandler.RegisterCommand("sanctuary-status", "Reports the Britain baseline population",
            _ => Core.LoopContext.Post(Report));
    }

    public static void Initialize()
    {
        var folder = Path.Join(Core.BaseDirectory, "Data", "Sanctuary");
        _vendors = JsonSerializer.Deserialize<SpawnerDto[]>(
            File.ReadAllText(Path.Join(folder, "vendors.json")), SpawnerJsonSerializer.Options
        ) ?? throw new InvalidDataException("Britain vendor data is empty.");

        var installed = false;
        foreach (var item in Map.Felucca.GetItemsAt<Static>(_markerLocation))
        {
            if (item.Name == MarkerName)
            {
                installed = true;
                break;
            }
        }

        // A saved marker prevents decoration import from replacing opened or moved objects.
        if (!installed)
        {
            var decorations = 0;
            foreach (var list in DecorationList.ReadAll(Path.Join(folder, "britain.cfg")))
            {
                decorations += list.Generate([Map.Felucca]);
            }

            new Static(0x1B72) { Name = MarkerName, Visible = false }.MoveToWorld(_markerLocation, Map.Felucca);
            _logger.Information("Britain baseline: installed {Count} decorations", decorations);
        }

        var created = 0;
        foreach (var dto in _vendors)
        {
            if (FindSpawner(dto) != null)
            {
                continue;
            }

            var spawner = dto.ToSpawner();
            try
            {
                spawner.MoveToWorld(dto.Location, dto.Map);
                spawner.Respawn();
                created++;
            }
            catch
            {
                spawner.Delete();
                throw;
            }
        }

        _logger.Information("Britain baseline: created {Count} vendor spawners", created);
        Report();
    }

    private static BaseSpawner FindSpawner(SpawnerDto dto)
    {
        foreach (var item in dto.Map.GetItemsAt<BaseSpawner>(dto.Location))
        {
            // Preserve an existing town population, including a later full-world import.
            if (item.Guid == dto.Guid || item.GetType() == typeof(Spawner))
            {
                return item;
            }
        }

        return null;
    }

    private static void Travel(CommandEventArgs e)
    {
        var destination = e.Arguments.Length == 0 ? "bank" : e.Arguments[0].ToLowerInvariant();
        var point = destination switch
        {
            "bank" => _markerLocation,
            "vendor" => new Point3D(1470, 1669, 0),
            "smith" => new Point3D(1448, 1649, 10),
            _ => (Point3D?)null
        };

        if (point == null)
        {
            e.Mobile.SendMessage("Usage: [SanctuaryTest bank|vendor|smith");
            return;
        }

        e.Mobile.MoveToWorld(point.Value, Map.Felucca);
        e.Mobile.SendMessage("Britain baseline: bank doors and banker, provisioner, and smiths nearby.");
    }

    private static void Report()
    {
        var doors = 0;
        var spawners = 0;
        var residents = 0;
        var bankers = 0;
        var provisioners = 0;
        var identities = new List<uint>();
        foreach (var door in Map.Felucca.GetItemsInBounds<BaseDoor>(_bounds))
        {
            doors++;
            identities.Add(door.Serial.Value);
        }

        foreach (var dto in _vendors)
        {
            var spawner = FindSpawner(dto);
            if (spawner == null)
            {
                continue;
            }

            spawners++;
            identities.Add(spawner.Serial.Value);
            foreach (var spawn in spawner.Spawned.Keys)
            {
                if (spawn is Mobile mobile && !mobile.Deleted)
                {
                    residents++;
                    bankers += mobile is Banker ? 1 : 0;
                    provisioners += mobile is Provisioner ? 1 : 0;
                    identities.Add(mobile.Serial.Value);
                }
            }
        }

        identities.Sort();
        _logger.Information(
            "Britain baseline status: doors={Doors}, spawners={Spawners}, residents={Residents}, bankers={Bankers}, provisioners={Provisioners}; serials={Serials}",
            doors, spawners, residents, bankers, provisioners, string.Join(',', identities)
        );
    }
}
