// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static partial class TownMarkets
{
    [CallPriority(200)]
    public static void Initialize()
    {
        var used = new HashSet<(Map, Point3D)>();
        int created = 0;
        foreach (var site in Sites)
        {
            Map.TryParse(site.Map, null, out var map);
            var vendor = Profiles.At(site.Id);
            if (vendor is not { Deleted: false })
            {
                var point = Floor(map, new(site.X, site.Y, site.Z), used, true);
                vendor = new ProfessionInstructor { Title = $"the {site.Role} merchant", CantWalk = true };
                foreach (byte skill in TradeSkills.Crafting) { vendor.Skills[(SkillName)skill].Base = 0; }
                foreach (byte skill in TradeSkills.Gathering) { vendor.Skills[(SkillName)skill].Base = 0; }
                if (Skill(site.Role) is { } trained)
                {
                    vendor.SetSkill(trained, 100);
                    vendor.Title = $"the {ClassProfessions.Name(trained)} trainer";
                }
                Profiles.Add(vendor, site.Role, site.Id);
                vendor.Home = point; vendor.RangeHome = 0;
                vendor.MoveToWorld(point, map); created++;
            }
            used.Add((map, vendor.Location));
            var state = Profiles.Find(vendor.Serial);
            if (Skill(site.Role) is { } craft && TradeSkills.IsCraft((int)craft))
            {
                var station = World.FindItem(state.Station) as TownCraftStation;
                if (station is not { Deleted: false })
                {
                    var point = Floor(map, new(vendor.X + 2, vendor.Y, vendor.Z), used, false);
                    int graphic = craft == SkillName.Blacksmith ? 0xFAF : craft == SkillName.Imbuing ? 0x974 : 0xB4A;
                    CraftFacilities.Register(new(craft, map.Name, point.X, point.Y, point.Z, graphic));
                    foreach (var item in map.GetItemsAt<TownCraftStation>(point))
                    {
                        if (item.Skill == craft && item.ItemID == graphic) { station = item; state.Station = item.Serial; break; }
                    }
                }
                used.Add((map, station.Location));
                CraftFacilities.Register(new(craft, map.Name, station.X, station.Y, station.Z, station.ItemID));
                if (craft == SkillName.Blacksmith)
                {
                    var forge = World.FindItem(state.Forge) as TownCraftStation;
                    if (forge is not { Deleted: false })
                    {
                        var point = Floor(map, new(station.X + 1, station.Y, station.Z), used, false);
                        CraftFacilities.Register(new(craft, map.Name, point.X, point.Y, point.Z, 0xFB1));
                        foreach (var item in map.GetItemsAt<TownCraftStation>(point))
                        {
                            if (item.Skill == craft && item.ItemID == 0xFB1) { forge = item; state.Forge = item.Serial; break; }
                        }
                    }
                    used.Add((map, forge.Location));
                    CraftFacilities.Register(new(craft, map.Name, forge.X, forge.Y, forge.Z, forge.ItemID));
                }
            }
        }
        _logger.Information("Town markets: {Sites} service sites; {Created} merchants created", Sites.Length, created);
    }
    private static Point3D Floor(Map map, Point3D anchor, HashSet<(Map, Point3D)> used, bool mobile)
    {
        for (int radius = 0; radius <= 32; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) { continue; }
                    int x = anchor.X + dx, y = anchor.Y + dy;
                    foreach (int z in new[] { anchor.Z, map.GetAverageZ(x, y) })
                    {
                        var point = new Point3D(x, y, z);
                        if (!used.Contains((map, point)) && map.CanFit(point, 16, false, mobile, true)) { return point; }
                    }
                    if (map.CanSpawnMobile(x, y, Math.Max(-128, anchor.Z - 20), Math.Min(127, anchor.Z + 60), false, false, out int floorZ))
                    {
                        var point = new Point3D(x, y, floorZ);
                        if (!used.Contains((map, point)) && map.CanFit(point, 16, false, mobile, true)) { return point; }
                    }
                }
            }
        }
        throw new InvalidOperationException($"No usable market floor near {anchor} on {map}.");
    }
}
