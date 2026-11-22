// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Targeting;

namespace Sanctuary;

[SerializationGenerator(1)]
public partial class EttinCamp : BaseAddon
{
    private static readonly (int Graphic, int X, int Y)[] _props =
    [
        (CampGraphics.Bonfire, 0, 0),
        (0x1E91, 1, -1), // Skinned deer
        (0x1079, -1, 1) // Hides
    ];

    private static readonly (int X, int Y)[] _posts =
    [
        (-2, -2), (2, -2), (-2, 2), (2, 2), (0, 3), (0, -3)
    ];

    private static readonly int[] _ettinLevels = [15, 16, 18, 20, 18];

    [SerializableField(0, getter: "private", setter: "private")]
    private List<Spawner> _spawners = [];

    private EttinCamp() { }

    public override string DefaultName => "an ettin camp";
    public override bool ShareHue => false;

    public static void Configure()
    {
        CommandSystem.Register("EttinCamp", AccessLevel.GameMaster, PlaceEttinCommand);
        CommandSystem.Register("OgreCamp", AccessLevel.GameMaster, PlaceOgreCommand);
    }

    public static void Initialize()
    {
        if (CampGraphics.Bonfire <= TileData.MaxItemValue)
            TileData.ItemTable[CampGraphics.Bonfire] = new ItemData(
                "a bonfire", TileFlag.Impassable, 0, 0, 0, 0, 0, CampGraphics.BonfireLogHeight);
    }

    [Usage("EttinCamp [x y [z]]"), Description("Places a compact hunting camp with five ettin spawners.")]
    private static void PlaceEttinCommand(CommandEventArgs args) => PlaceCommand(args, ogres: false);

    [Usage("OgreCamp [x y [z]]"), Description("Places a compact hunting camp with four to six ogre spawners.")]
    private static void PlaceOgreCommand(CommandEventArgs args) => PlaceCommand(args, ogres: true);

    private static void PlaceCommand(CommandEventArgs args, bool ogres)
    {
        var gm = args.Mobile;
        var map = gm.Map;
        if (map == null || map == Map.Internal)
            return;

        if (args.Length == 0)
        {
            gm.Target = new CampTarget(map, ogres);
            gm.SendMessage("Target the ground where the bonfire should stand.");
            return;
        }

        int z = 0;
        if (args.Length is not (2 or 3) || !int.TryParse(args.GetString(0), out int x)
            || !int.TryParse(args.GetString(1), out int y) || args.Length == 3 && !int.TryParse(args.GetString(2), out z))
        {
            gm.SendMessage($"Use [{(ogres ? "OgreCamp" : "EttinCamp")} x y with an optional z, or omit the coordinates to target the ground.");
            return;
        }

        if (!OnMap(map, x, y))
        {
            gm.SendMessage("That ground lies outside this facet.");
            return;
        }
        PlaceFor(gm, map, new(x, y, args.Length == 2 ? map.GetAverageZ(x, y) : z), ogres);
    }

    private sealed class CampTarget(Map map, bool ogres) : Target(-1, true, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from.AccessLevel < AccessLevel.GameMaster || from.Map != map)
                return;
            if (targeted is LandTarget or StaticTarget && targeted is IPoint3D spot && OnMap(map, spot.X, spot.Y))
                PlaceFor(from, map, new(spot.X, spot.Y, map.GetAverageZ(spot.X, spot.Y)), ogres);
            else
                from.SendMessage("Target a ground tile where the bonfire should stand.");
        }
    }

    private static void PlaceFor(Mobile gm, Map map, Point3D center, bool ogres) =>
        gm.SendMessage(TryPlace(map, center, ogres, out var camp)
            ? $"Placed {camp.Name} with {camp._spawners.Count} spawners at {camp.Location}."
            : "The camp needs clear, dry ground for its props and spawners.");

    internal static bool TryPlace(Map map, Point3D center, out EttinCamp camp) =>
        TryPlace(map, center, ogres: false, out camp);

    private static bool TryPlace(Map map, Point3D center, bool ogres, out EttinCamp camp)
    {
        camp = null;
        if (map == null || map == Map.Internal || !OnMap(map, center.X, center.Y))
            return false;

        int ground = map.GetAverageZ(center.X, center.Y);
        var offsets = new int[_props.Length];
        for (int i = 0; i < _props.Length; i++)
        {
            var (_, dx, dy) = _props[i];
            int x = center.X + dx, y = center.Y + dy;
            if (!OnMap(map, x, y))
                return false;

            int offset = map.GetAverageZ(x, y) - ground;
            int z = center.Z + offset;
            int land = map.Tiles.GetLandTile(x, y).ID & TileData.MaxLandValue;
            if (z is < sbyte.MinValue or > sbyte.MaxValue || (TileData.LandTable[land].Flags & TileFlag.Wet) != 0
                || !map.CanFit(x, y, z, 8, checkMobiles: false))
                return false;
            offsets[i] = offset;
        }

        camp = new EttinCamp();
        if (ogres)
            camp.Name = "an ogre camp";

        try
        {
            for (int i = 0; i < _props.Length; i++)
            {
                var (graphic, x, y) = _props[i];
                camp.AddComponent(new AddonComponent(graphic), x, y, offsets[i]);
            }
            camp.MoveToWorld(center, map);
            if (!camp.Populate(center.Z - ground, ogres))
            {
                camp.Delete();
                camp = null;
                return false;
            }
            return true;
        }
        catch
        {
            camp.Delete();
            throw;
        }
    }

    private bool Populate(int elevation, bool ogres)
    {
        int count = ogres ? Utility.RandomMinMax(4, 6) : _ettinLevels.Length;
        var locations = new List<Point3D>(count);
        for (int i = 0; i < count; i++)
        {
            var (x, y) = _posts[i];
            if (!FindPost(x, y, elevation, locations, out var spot))
                return false;
            locations.Add(spot);
        }

        for (int i = 0; i < locations.Count; i++)
        {
            int level = ogres ? Utility.RandomMinMax(24, 28) : _ettinLevels[i];
            var spawner = new Spawner
            {
                Name = ogres ? "Ogre camp spawner" : "Ettin camp spawner",
                MinDelay = TimeSpan.FromSeconds(90),
                MaxDelay = TimeSpan.FromSeconds(150),
                WalkingRange = 1
            };
            _spawners.Add(spawner);
            spawner.MoveToWorld(locations[i], Map);
            spawner.HomeRange = 0;
            if (!ogres && i == _ettinLevels.Length - 1)
                spawner.AddEntry(nameof(BoulderEttin), parameters: level.ToString());
            else
                spawner.AddEntry(nameof(FrontierEncounter),
                    parameters: $"{(ogres ? FrontierEncounter.CampOgreId : "frontier-ettin")} {level} 0");
            spawner.Respawn();
            if (spawner.IsEmpty)
                return false;
        }
        this.MarkDirty();
        return true;
    }

    private bool FindPost(int x, int y, int elevation, List<Point3D> locations, out Point3D spot)
    {
        for (int radius = 0; radius <= 1; radius++)
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            int nx = X + x + dx, ny = Y + y + dy;
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius || !OnMap(Map, nx, ny)
                || Math.Max(Math.Abs(x + dx), Math.Abs(y + dy)) > 3
                || Components.Exists(prop => prop.X == nx && prop.Y == ny))
                continue;
            int z = Map.GetAverageZ(nx, ny) + elevation;
            var candidate = new Point3D(nx, ny, z);
            if (z is < sbyte.MinValue or > sbyte.MaxValue || !Map.CanSpawnMobile(candidate)
                || locations.Exists(other => Utility.InRange(other, candidate, 1)))
                continue;
            spot = candidate;
            return true;
        }
        spot = default;
        return false;
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        foreach (var spawner in _spawners)
            spawner?.Delete();
    }

    // Version 0 camps only contain scenery.
    private void Deserialize(IGenericReader reader, int version) => _spawners = [];

    private static bool OnMap(Map map, int x, int y) => (uint)x < map.Width && (uint)y < map.Height;
}
