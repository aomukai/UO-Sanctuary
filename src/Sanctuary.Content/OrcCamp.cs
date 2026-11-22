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
public partial class OrcCamp : BaseAddon
{
    private readonly record struct Piece(int Graphic, int X, int Y, int Rise = 0, int Hue = 0, LightType? Light = null);
    private static readonly (int X, int Y, string Role, int Level)[] _posts =
    [
        (-6, -5, "warrior", 18), (0, -7, "scout", 19), (7, -6, "warrior", 19),
        (8, 3, "warrior", 20), (2, 7, "shaman", 20), (-6, 4, "warrior", 19)
    ];

    [SerializableField(0, getter: "private", setter: "private")]
    private List<Spawner> _orcSpawners = [];

    // Offsets from the clearing at 2337, 1222 in Cove. Rise is measured from the ground under each prop.
    private static readonly Piece[] _pieces =
    [
        new(0x02EB, -4, -2, Hue: 0x33), new(0x02EC, -3, -3, Hue: 0x33),
        new(0x02EB,  0,  7, Hue: 0x33), new(0x02EC,  1,  6, Hue: 0x33),
        new(0x02EB,  5, -8, Hue: 0x33), new(0x02EC,  6, -9, Hue: 0x33),
        new(0x02EB,  9, -2, Hue: 0x33), new(0x02EC, 10, -3, Hue: 0x33),

        new(0x0974,  0, -1), new(0x0DE3,  0, -1, Light: LightType.Circle225),
        new(0x0FAC,  0, -1, Light: LightType.Circle225),
        new(0x041F,  1, -8), new(0x0420, 10, -5), new(0x0428, -8, -1),
        new(0x0E59, -7, -2), new(0x0F46, -6, -3),

        new(0x10EE, -5, -1), new(0x10EE, -4,  1), new(0x10EE, -1, -1, Rise: 1),
        new(0x10EE, -1,  0), new(0x10EE,  1, -8), new(0x10EE,  1, -4),
        new(0x10EE,  1,  2), new(0x10EE,  2, -1, Rise: 2), new(0x10EE,  3, -2),
        new(0x10EE,  3,  2),
        new(0x1E88, -8,  3), new(0x1E89,  0,  1), new(0x1E90,  5, -1),
        new(0x1E91,  1, -3), new(0x1E91,  2, -3)
    ];

    private OrcCamp() { }

    public override string DefaultName => "an orc camp";
    public override bool ShareHue => false;

    public static void Configure()
    {
        CommandSystem.Register("OrcCamp", AccessLevel.GameMaster, PlaceCampCommand);
        CommandSystem.Register("OrcCampDeco", AccessLevel.GameMaster, PlaceDecoCommand);
    }

    [Usage("OrcCamp [x y [z]]"), Description("Places an orc camp with six Cove tribe spawners on the current facet.")]
    private static void PlaceCampCommand(CommandEventArgs args) => PlaceCommand(args, populate: true);

    [Usage("OrcCampDeco [x y [z]]"), Description("Places orc camp scenery on the current facet.")]
    private static void PlaceDecoCommand(CommandEventArgs args) => PlaceCommand(args, populate: false);

    private static void PlaceCommand(CommandEventArgs args, bool populate)
    {
        var gm = args.Mobile;
        var map = gm.Map;
        if (map == null || map == Map.Internal)
            return;

        string command = populate ? "[OrcCamp" : "[OrcCampDeco";
        if (args.Length == 0)
        {
            gm.Target = new CampTarget(map, populate);
            gm.SendMessage("Target the ground at the center of the orc camp.");
            return;
        }

        int z = 0;
        if (args.Length is not (2 or 3) || !int.TryParse(args.GetString(0), out int x)
            || !int.TryParse(args.GetString(1), out int y) || args.Length == 3 && !int.TryParse(args.GetString(2), out z))
        {
            gm.SendMessage($"Use {command} x y with an optional z, or omit the coordinates to target the ground.");
            return;
        }

        if (!OnMap(map, x, y))
        {
            gm.SendMessage("That camp center lies outside this facet.");
            return;
        }
        PlaceFor(gm, map, new(x, y, args.Length == 2 ? map.GetAverageZ(x, y) : z), populate);
    }

    private sealed class CampTarget(Map map, bool populate) : Target(-1, true, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from.AccessLevel < AccessLevel.GameMaster || from.Map != map)
                return;

            if (targeted is LandTarget or StaticTarget && targeted is IPoint3D spot && OnMap(map, spot.X, spot.Y))
                PlaceFor(from, map, new(spot.X, spot.Y, map.GetAverageZ(spot.X, spot.Y)), populate);
            else
                from.SendMessage("Target a ground tile at the center of the camp.");
        }
    }

    private static void PlaceFor(Mobile gm, Map map, Point3D center, bool populate)
    {
        if (!TryPlace(map, center, out var camp, out string reason, populate))
        {
            gm.SendMessage(reason);
            return;
        }

        gm.SendMessage(populate
            ? $"Placed an orc camp with {camp._orcSpawners.Count} orc spawners at {center}."
            : $"Placed an orc camp with {camp.Components.Count} props at {center}.");
    }

    internal static bool TryPlace(Map map, Point3D center, out OrcCamp camp, out string reason, bool populate = false)
    {
        camp = null;
        if (map == null || map == Map.Internal || !OnMap(map, center.X, center.Y)
            || center.Z is < sbyte.MinValue or > sbyte.MaxValue)
        {
            reason = "That camp center lies outside this facet.";
            return false;
        }

        foreach (var existing in map.GetItemsAt<OrcCamp>(center))
        {
            if (existing.Location == center)
            {
                reason = "An orc camp is already centered there.";
                return false;
            }
        }

        int centerGround = map.GetAverageZ(center.X, center.Y);
        int fireY = center.Y - 1;
        var offsets = new int[_pieces.Length];
        for (int i = 0; i < _pieces.Length; i++)
        {
            var piece = _pieces[i];
            int x = center.X + piece.X, y = center.Y + piece.Y;
            if (!OnMap(map, x, y) || !DryGround(map, x, y))
            {
                reason = "The full camp footprint needs dry ground within this facet.";
                return false;
            }

            int offset = map.GetAverageZ(x, y) - centerGround + piece.Rise;
            if (center.Z + offset is < sbyte.MinValue or > sbyte.MaxValue)
            {
                reason = "One of the camp props would be outside the supported height range.";
                return false;
            }
            offsets[i] = offset;
        }

        int fireZ = center.Z + map.GetAverageZ(center.X, fireY) - centerGround;
        foreach (var item in map.GetItemsAt(new Point2D(center.X, fireY)))
        {
            if (item.Z == fireZ && item.ItemID == 0x0FAC)
            {
                reason = "A campfire pit is already in place there.";
                return false;
            }
        }

        camp = new OrcCamp();
        try
        {
            for (int i = 0; i < _pieces.Length; i++)
            {
                var piece = _pieces[i];
                var prop = new AddonComponent(piece.Graphic) { Hue = piece.Hue };
                if (piece.Light is { } light)
                    prop.Light = light;
                camp.AddComponent(prop, piece.X, piece.Y, offsets[i]);
            }
            camp.MoveToWorld(center, map);
            if (populate && !camp.Populate(center.Z - centerGround))
            {
                camp.Delete();
                camp = null;
                reason = "The camp needs six separated, walkable tiles for the orcs.";
                return false;
            }
        }
        catch
        {
            camp.Delete();
            throw;
        }

        reason = null;
        return true;
    }

    private bool Populate(int elevation)
    {
        var posts = new List<Point3D>(_posts.Length);
        foreach (var (x, y, _, _) in _posts)
        {
            if (!FindPost(X + x, Y + y, elevation, posts, out var spot))
                return false;
            posts.Add(spot);
        }

        for (int i = 0; i < posts.Count; i++)
        {
            var (_, _, role, level) = _posts[i];
            var spawner = new Spawner
            {
                Name = $"Orc camp {role}",
                MinDelay = TimeSpan.FromSeconds(90),
                MaxDelay = TimeSpan.FromSeconds(150),
                WalkingRange = 2
            };
            _orcSpawners.Add(spawner);
            spawner.MoveToWorld(posts[i], Map);
            spawner.HomeRange = 0;
            spawner.AddEntry(nameof(CoveEncounter), 100, 1, parameters: $"cove-orc-{role} {level}");
            spawner.Respawn();
            if (spawner.IsEmpty)
                return false;
        }
        this.MarkDirty();
        return true;
    }

    private bool FindPost(int x, int y, int elevation, List<Point3D> posts, out Point3D spot)
    {
        for (int radius = 0; radius <= 2; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius || !OnMap(Map, nx, ny) || !DryGround(Map, nx, ny))
                    continue;
                int z = Map.GetAverageZ(nx, ny) + elevation;
                var candidate = new Point3D(nx, ny, z);
                if (z is < sbyte.MinValue or > sbyte.MaxValue || !Map.CanSpawnMobile(candidate)
                    || posts.Exists(p => Utility.InRange(p, candidate, 4)))
                    continue;
                spot = candidate;
                return true;
            }
        }
        spot = default;
        return false;
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        foreach (var spawner in _orcSpawners)
            spawner?.Delete();
    }

    // Version 0 camps only contain scenery.
    private void Deserialize(IGenericReader reader, int version) => _orcSpawners = [];

    private static bool OnMap(Map map, int x, int y) => (uint)x < map.Width && (uint)y < map.Height;

    private static bool DryGround(Map map, int x, int y)
    {
        int id = map.Tiles.GetLandTile(x, y).ID & TileData.MaxLandValue;
        return (TileData.LandTable[id].Flags & TileFlag.Wet) == 0;
    }
}
