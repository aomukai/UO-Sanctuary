// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary;

public static class BritanniaWorld
{
    private const string Installed = "Sanctuary: Britannia scenery v1";
    private static readonly Point3D _marker = new(1438, 1696, 0);
    private static readonly Rectangle2D _newHaven = new(3350, 2400, 500, 475);
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(BritanniaWorld));

    [CallPriority(150)]
    public static void Initialize()
    {
        var map = Map.Trammel;
        string data = Path.Join(Core.BaseDirectory, "Data");
        if (!HasMarker(map))
        {
            int props = 0;
            foreach (string facet in new[] { "Britannia", "Trammel" })
                foreach (string path in Directory.EnumerateFiles(Path.Join(data, "Decoration", facet), "*.cfg").Order(StringComparer.Ordinal))
                    foreach (var list in DecorationList.ReadAll(path))
                        props += list.Generate([map], (item, point) => IncludeDecoration(map, item, point));

            int doors = DoorGenerator.GenerateBritannia(map, point => Include(point) && !HasDoor(map, point));
            int signs = InstallSigns(map, Path.Join(data, "signs.cfg"));
            new Static(0x1B72) { Name = Installed, Visible = false }.MoveToWorld(_marker, map);
            _logger.Information("Britannia scenery: installed {Props} decorations, {Doors} additional doors and {Signs} signs", props, doors, signs);
        }

        int residents = GraveyardPopulation.InstallSpawners(Residents(data, map));
        _logger.Information("Britannia town life: installed {Count} civilian and vendor spawners", residents);
    }

    internal static bool Include(Point3D point) => !_newHaven.Contains(point);

    private static bool HasMarker(Map map)
    {
        foreach (var item in map.GetItemsAt<Static>(_marker))
            if (item.Name == Installed)
                return true;
        return false;
    }

    private static bool IncludeDecoration(Map map, Item item, Point3D point) =>
        Include(point) && item is not (Teleporter or BaseSpawner or MarkContainer or SerpentPillar or SerpentPillarAddon)
        && (item is not BaseDoor || !HasDoor(map, point));

    private static bool HasDoor(Map map, Point3D point)
    {
        foreach (var door in map.GetItemsInRange<BaseDoor>(point, 1))
        {
            var offset = door.Open ? door.Offset : default;
            var closed = new Point3D(door.X - offset.X, door.Y - offset.Y, door.Z - offset.Z);
            if (closed.X == point.X && closed.Y == point.Y && Math.Abs(closed.Z - point.Z) < 8)
                return true;
        }
        return false;
    }

    private static int InstallSigns(Map map, string path)
    {
        int count = 0;
        foreach (string line in File.ReadLines(path))
        {
            string[] fields = line.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 6 || fields[0] is not ("0" or "2"))
                continue;
            var point = new Point3D(Utility.ToInt32(fields[2]), Utility.ToInt32(fields[3]), Utility.ToInt32(fields[4]));
            int graphic = Utility.ToInt32(fields[1]);
            if (!Include(point) || HasSign(map, point, graphic))
                continue;
            SignParser.Add_Static(graphic, point, map, fields[5]);
            count++;
        }
        return count;
    }

    private static bool HasSign(Map map, Point3D point, int graphic)
    {
        foreach (var sign in map.GetItemsAt<BaseSign>(point))
            if (sign.Z == point.Z && sign.ItemID == graphic)
                return true;
        return false;
    }

    private static IEnumerable<SpawnerDto> Residents(string data, Map map)
    {
        string era = Core.Expansion >= Expansion.SA ? "post-uoml" : "uoml";
        foreach (string layer in new[] { "shared", era })
            foreach (string file in new[] { "Vendors.json", "TownsPeople.json" })
            {
                string path = Path.Join(data, "Spawns", layer, "trammel", file);
                if (!File.Exists(path))
                    continue;
                var definitions = JsonSerializer.Deserialize<SpawnerDto[]>(File.ReadAllText(path), SpawnerJsonSerializer.Options);
                foreach (var definition in definitions.OfType<SpawnerDataDto>())
                {
                    if (definition.Map != map || !Include(definition.Location))
                        continue;
                    var civilians = definition.Entries.Where(entry => entry.Enabled
                        && AssemblyHandler.FindTypeByName(entry.SpawnedName) is { } type
                        && (typeof(BaseVendor).IsAssignableFrom(type) || type == typeof(TownCrier) || type == typeof(HarborMaster))).ToList();
                    if (civilians.Count == 0)
                        continue;

                    yield return definition with
                    {
                        Name = "Britannia town resident",
                        Count = Math.Min(definition.Count, civilians.Sum(entry => entry.SpawnedMaxCount)),
                        Entries = civilians
                    };
                }
            }
    }
}
