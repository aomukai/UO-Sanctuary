// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

public sealed record CraftFacility(SkillName Skill, string Map, int X, int Y, int Z, int ItemID);

public static class CraftFacilities
{
    private static CraftFacility[] _sites = [];
    private static readonly Dictionary<(Map, Point3D), (BaseAddon Station, SkillName Skill)> _publicAddons = new();
    internal const int LegacyDesk = 0x0B4A;

    internal static int DefaultGraphic(SkillName skill) => skill switch
    {
        SkillName.Alchemy => 0x2DD4,     // Shelved alchemy table
        SkillName.Blacksmith => 0x0FAF,
        SkillName.Fletching => 0x9C2E,   // Bowyer's jig
        SkillName.Carpentry => 0x9C1A,   // Compact woodworking machine
        SkillName.Cartography => 0xA833, // Drafting board with map and instruments
        SkillName.Cooking => 0x2DDB,     // Recognized by UO as both oven and heat source
        SkillName.Inscribe => 0x9FEA,    // Writing desk with quill and open books
        SkillName.Tailoring => 0x9A48,   // Sewing machine
        SkillName.Tinkering => 0xA212,   // Tinker's worktable
        SkillName.Imbuing => 0x0974,
        _ => LegacyDesk
    };

    private static CraftFacility Resolve(CraftFacility site) => site.ItemID == LegacyDesk
        ? site with { ItemID = DefaultGraphic(site.Skill) } : site;

    public static void Load()
    {
        _sites = Array.ConvertAll(BalanceCatalog.Read<CraftFacility>("craft-facilities"), Resolve);
        foreach (var site in _sites)
        {
            if (!TradeSkills.IsCraft((int)site.Skill) || !Map.TryParse(site.Map, null, out _)
                || site.ItemID is < 1 or > 0xFFFF || site.X < 0 || site.Y < 0)
            { throw new InvalidDataException("Invalid public craft facility."); }
        }
    }
    public static void Initialize()
    {
        if (!ClassProfessions.Enabled) { return; }
        foreach (var site in _sites) EnsureStation(site);
    }

    internal static TownCraftStation Register(CraftFacility site)
    {
        site = Resolve(site);
        if (!Array.Exists(_sites, row => row == site)) { _sites = [.. _sites, site]; }
        return EnsureStation(site);
    }

    private static TownCraftStation EnsureStation(CraftFacility site)
    {
        Map.TryParse(site.Map, null, out var map);
        var point = new Point3D(site.X, site.Y, site.Z);
        foreach (var station in map.GetItemsAt<TownCraftStation>(point))
        {
            if (station.Z != site.Z || station.Movable || station.Skill != site.Skill) continue;
            if (station.ItemID == LegacyDesk && site.ItemID == DefaultGraphic(site.Skill)) station.ItemID = site.ItemID;
            if (station.ItemID == site.ItemID) return station;
        }
        var added = new TownCraftStation(site.Skill, site.ItemID);
        added.MoveToWorld(point, map);
        return added;
    }

    internal static void RegisterAddon<T>(Map map, Point3D location, SkillName skill) where T : BaseAddon, new()
    {
        foreach (var existing in map.GetItemsAt<T>(location))
        {
            if (existing.Location != location) continue;
            _publicAddons[(map, location)] = (existing, skill);
            return;
        }
        var station = new T();
        station.MoveToWorld(location, map);
        _publicAddons[(map, location)] = (station, skill);
    }
    public static bool Allows(Mobile from, SkillName skill, bool message = true)
    {
        if (!ClassProfessions.Affected(from)) { return true; }
        foreach (var site in _sites)
        {
            if (site.Skill != skill || from.Map?.Name != site.Map) { continue; }
            var location = new Point3D(site.X, site.Y, site.Z);
            if (!from.InRange(location, 2) || Math.Abs(from.Z - site.Z) > 16) { continue; }
            foreach (var station in from.Map.GetItemsAt<TownCraftStation>(location))
            {
                if (station.Skill == skill && station.ItemID == site.ItemID && !station.Movable && station.Parent == null && from.InLOS(station))
                { return true; }
            }
        }
        if (from.Map is { } map)
        {
            foreach (var part in map.GetItemsInRange<AddonComponent>(from.Location, 2))
            {
                if (part.Addon is { } addon && _publicAddons.TryGetValue((map, addon.Location), out var site)
                    && site.Station == addon && site.Skill == skill && Math.Abs(from.Z - part.Z) <= 16 && from.InLOS(part))
                    return true;
            }
        }
        if (message) { from.SendMessage($"Use a public town {ClassProfessions.Name(skill)} facility within two tiles. Station use is free."); }
        return false;
    }
    public static bool Processing(Type output) => output == typeof(Board) || output == typeof(Cloth)
        || output == typeof(BoltOfCloth) || output == typeof(LightYarn) || output == typeof(DarkYarn)
        || output == typeof(SpoolOfThread);
    public static bool AllowsRecipe(Mobile from, SkillName skill, Type output)
    {
        if (Processing(output)) { return true; }
        if (skill == SkillName.Cooking && output != null
            && (output == typeof(CookedBird) || output == typeof(ChickenLeg) || output == typeof(FishSteak)
                || output == typeof(LambLeg) || output == typeof(Ribs)))
        {
            foreach (var fire in from.Map.GetItemsInRange<Campfire>(from.Location, 2))
            {
                if (fire.Parent == null && fire.ItemID is >= 0xDE3 and <= 0xDE9
                    && Math.Abs(from.Z - fire.Z) <= 16 && from.InLOS(fire)) { return true; }
            }
        }
        return Allows(from, skill);
    }
}

[SerializationGenerator(0)]
public partial class TownCraftStation : Item
{
    [SerializableField(0)] private SkillName _skill;
    [Constructible]
    public TownCraftStation(SkillName skill, int itemID) : base(itemID == CraftFacilities.LegacyDesk ? CraftFacilities.DefaultGraphic(skill) : itemID)
    {
        _skill = skill; Movable = false;
        Name = skill == SkillName.Imbuing ? "public Imbuing cauldron" : $"public {ClassProfessions.Name(skill)} station";
    }

    [AfterDeserialization]
    private void UpgradeDesk()
    {
        if (ItemID == CraftFacilities.LegacyDesk) ItemID = CraftFacilities.DefaultGraphic(Skill);
    }
}
