// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static partial class TownMarkets
{
    private sealed record TrainerPortrait(string Site, ushort Portrait, bool Female);
    private static Dictionary<string, TrainerPortrait> _trainerPortraits = new(StringComparer.Ordinal);

    private static void LoadTrainerPortraits()
    {
        var sites = Sites.Where(site => Skill(site.Role) != null).Select(site => site.Id).ToHashSet(StringComparer.Ordinal);
        var portraits = new HashSet<ushort>();
        var assignments = new Dictionary<string, TrainerPortrait>(StringComparer.Ordinal);
        foreach (var entry in BalanceCatalog.Read<TrainerPortrait>("trainer-portraits"))
        {
            if (entry.Site == null || !sites.Remove(entry.Site) || !PortraitProtocol.IsTrainerPortrait(entry.Portrait)
                || !portraits.Add(entry.Portrait))
                throw new InvalidDataException($"Invalid or duplicate trainer portrait assignment for {entry.Site}.");

            assignments.Add(entry.Site, entry);
        }
        if (sites.Count != 0)
            throw new InvalidDataException($"Missing trainer portraits for {string.Join(", ", sites.Order())}.");

        _trainerPortraits = assignments;
    }

    internal static ushort PortraitOf(ProfessionInstructor trainer) =>
        _trainerPortraits[Profiles.Find(trainer.Serial).Site].Portrait;

    private static void MatchTrainerPortrait(BaseVendor vendor, MarketSite site)
    {
        if (!_trainerPortraits.TryGetValue(site.Id, out var portrait) || vendor.Female == portrait.Female)
            return;

        vendor.Female = portrait.Female;
        vendor.Body = portrait.Female ? 0x191 : 0x190;
        vendor.Name = NameList.RandomName(portrait.Female ? "female" : "male");
        if (portrait.Female)
            vendor.FacialHairItemID = 0;
    }

    [CallPriority(200)]
    public static void Initialize()
    {
        var used = new HashSet<(Map, Point3D)>();
        int created = 0;
        foreach (var site in Sites)
        {
            Map.TryParse(site.Map, null, out var map);
            var vendor = Profiles.At(site.Id);
            var anchor = new Point3D(site.X, site.Y, site.Z);
            if (vendor is not { Deleted: false })
            {
                var point = site.FixedPosition ? anchor : Floor(map, anchor, used, true);
                vendor = site.Role == MarketRole.Scholar
                    ? new Scholar()
                    : new ProfessionInstructor { Title = $"the {site.Role} merchant", CantWalk = true };
                foreach (byte skill in TradeSkills.Crafting) { vendor.Skills[(SkillName)skill].Base = 0; }
                foreach (byte skill in TradeSkills.Gathering) { vendor.Skills[(SkillName)skill].Base = 0; }
                if (Skill(site.Role) is { } trained)
                {
                    vendor.SetSkill(trained, 100);
                    vendor.Title = $"the {ClassProfessions.Name(trained)} trainer";
                }
                HeartwoodTown.StyleMerchant(vendor, site);
                Profiles.Add(vendor, site.Role, site.Id);
                vendor.Home = point; vendor.RangeHome = 0;
                vendor.MoveToWorld(point, map); created++;
            }
            else if (site.FixedPosition
                ? vendor.Map != map || vendor.Location != anchor || vendor.Home != anchor
                : site.Relocate && (vendor.Map != map || !Utility.InRange(vendor.Location, anchor, 3)
                    || Math.Abs(vendor.Z - anchor.Z) >= 16))
            {
                var point = site.FixedPosition ? anchor : Floor(map, anchor, used, true);
                vendor.Home = point;
                vendor.RangeHome = 0;
                vendor.MoveToWorld(point, map);
                var market = Profiles.Find(vendor.Serial);
                if (World.FindItem(market.Station) is TownCraftStation station)
                    station.Delete();
                if (World.FindItem(market.Forge) is TownCraftStation forge)
                    forge.Delete();
                market.Station = Serial.Zero;
                market.Forge = Serial.Zero;
            }
            MatchTrainerPortrait(vendor, site);
            used.Add((map, vendor.Location));
            var state = Profiles.Find(vendor.Serial);
            if (Skill(site.Role) is { } craft && TradeSkills.IsCraft((int)craft))
            {
                var station = World.FindItem(state.Station) as TownCraftStation;
                if (station is not { Deleted: false })
                {
                    var point = Floor(map, new(vendor.X + 2, vendor.Y, vendor.Z), used, false, site.FixedPosition);
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
                        var point = Floor(map, new(station.X + 1, station.Y, station.Z), used, false, site.FixedPosition);
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
    internal static Point3D Floor(Map map, Point3D anchor, HashSet<(Map, Point3D)> used, bool mobile, bool sameLevel = false)
    {
        for (int radius = 0; radius <= 32; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) { continue; }
                    int x = anchor.X + dx, y = anchor.Y + dy;
                    var atLevel = new Point3D(x, y, anchor.Z);
                    if (!used.Contains((map, atLevel)) && map.CanFit(atLevel, 16, false, mobile, true)) { return atLevel; }
                    if (sameLevel) { continue; }

                    var atAverage = new Point3D(x, y, map.GetAverageZ(x, y));
                    if (atAverage.Z != anchor.Z && !used.Contains((map, atAverage))
                        && map.CanFit(atAverage, 16, false, mobile, true)) { return atAverage; }
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
