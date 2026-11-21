// SPDX-License-Identifier: GPL-3.0-or-later
using ModernUO.Serialization;
using Server;
using Server.Commands;
using Server.Items;
using Server.Targeting;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class OrcCamp : BaseAddon
{
    private readonly record struct Piece(int Graphic, int X, int Y, int Rise = 0, int Hue = 0, LightType? Light = null);

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

    public static void Configure() => CommandSystem.Register("OrcCamp", AccessLevel.GameMaster, PlaceCommand);

    [Usage("OrcCamp [x y [z]]"), Description("Places the Cove-style orc camp scenery on the current facet.")]
    private static void PlaceCommand(CommandEventArgs args)
    {
        var gm = args.Mobile;
        var map = gm.Map;
        if (map == null || map == Map.Internal)
            return;

        if (args.Length == 0)
        {
            gm.Target = new CampTarget(map);
            gm.SendMessage("Target the ground at the center of the orc camp.");
            return;
        }

        int z = 0;
        if (args.Length is not (2 or 3) || !int.TryParse(args.GetString(0), out int x)
            || !int.TryParse(args.GetString(1), out int y) || args.Length == 3 && !int.TryParse(args.GetString(2), out z))
        {
            gm.SendMessage("Use [OrcCamp to target the ground, or [OrcCamp x y with an optional z.");
            return;
        }

        if (!OnMap(map, x, y))
        {
            gm.SendMessage("That camp center lies outside this facet.");
            return;
        }
        PlaceFor(gm, map, new(x, y, args.Length == 2 ? map.GetAverageZ(x, y) : z));
    }

    private sealed class CampTarget(Map map) : Target(-1, true, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from.AccessLevel < AccessLevel.GameMaster || from.Map != map)
                return;

            if (targeted is LandTarget or StaticTarget && targeted is IPoint3D spot && OnMap(map, spot.X, spot.Y))
                PlaceFor(from, map, new(spot.X, spot.Y, map.GetAverageZ(spot.X, spot.Y)));
            else
                from.SendMessage("Target a ground tile or use [OrcCamp x y.");
        }
    }

    private static void PlaceFor(Mobile gm, Map map, Point3D center)
    {
        gm.SendMessage(TryPlace(map, center, out var camp, out string reason)
            ? $"Placed an orc camp with {camp.Components.Count} props at {center}."
            : reason);
    }

    internal static bool TryPlace(Map map, Point3D center, out OrcCamp camp, out string reason)
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
        }
        catch
        {
            camp.Delete();
            throw;
        }

        reason = null;
        return true;
    }

    private static bool OnMap(Map map, int x, int y) => (uint)x < map.Width && (uint)y < map.Height;

    private static bool DryGround(Map map, int x, int y)
    {
        int id = map.Tiles.GetLandTile(x, y).ID & TileData.MaxLandValue;
        return (TileData.LandTable[id].Flags & TileFlag.Wet) == 0;
    }
}
