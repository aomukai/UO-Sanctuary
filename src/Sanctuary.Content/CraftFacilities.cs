// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

public sealed record CraftFacility(SkillName Skill, string Map, int X, int Y, int Z, int ItemID);

public static class CraftFacilities
{
    private static CraftFacility[] _sites = [];
    public static void Load()
    {
        _sites = BalanceCatalog.Read<CraftFacility>("craft-facilities");
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
        foreach (var site in _sites)
        {
            Map.TryParse(site.Map, null, out var map);
            var location = new Point3D(site.X, site.Y, site.Z);
            bool found = false;
            foreach (var station in map.GetItemsAt<TownCraftStation>(location))
            {
                if (station.Skill == site.Skill && station.ItemID == site.ItemID && !station.Movable)
                { found = true; break; }
            }
            if (!found)
            {
                new TownCraftStation(site.Skill, site.ItemID).MoveToWorld(location, map);
            }
        }
    }
    internal static void Register(CraftFacility site)
    {
        if (!Array.Exists(_sites, row => row == site)) { _sites = [.. _sites, site]; }
        Map.TryParse(site.Map, null, out var map);
        var point = new Point3D(site.X, site.Y, site.Z);
        foreach (var station in map.GetItemsAt<TownCraftStation>(point))
        {
            if (station.Skill == site.Skill && station.ItemID == site.ItemID) { return; }
        }
        new TownCraftStation(site.Skill, site.ItemID).MoveToWorld(point, map);
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
    public TownCraftStation(SkillName skill, int itemID) : base(itemID)
    {
        _skill = skill; Movable = false;
        Name = skill == SkillName.Imbuing ? "public Imbuing cauldron" : $"public {ClassProfessions.Name(skill)} station";
    }
}
