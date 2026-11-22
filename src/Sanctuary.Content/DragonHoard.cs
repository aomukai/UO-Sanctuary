// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Commands;
using Server.Items;
using Server.Targeting;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class DragonHoard : BaseAddon
{
    private readonly record struct Piece(ushort Graphic, int X, int Y);

    private static readonly Lazy<Piece[]> _template = new(LoadTemplate);

    private DragonHoard() { }

    public override string DefaultName => "a dragon's hoard";

    public static void Configure() => CommandSystem.Register("DragonHoard", AccessLevel.GameMaster, Place);

    private static Piece[] LoadTemplate()
    {
        string path = Path.Join(Core.BaseDirectory, "Data", "Sanctuary", "dragon-hoard-template.json");
        var pieces = JsonSerializer.Deserialize<Piece[]>(File.ReadAllText(path), JsonSerializerOptions.Web);
        if (pieces is not { Length: > 1 } || pieces[0] != new Piece(WorldDecorationGraphics.DragonHoard, 0, 0)
            || pieces.Skip(1).Any(p => !WorldDecorationGraphics.IsDragonHoardGround(p.Graphic))
            || pieces.Select(p => p.Graphic).Distinct().Count() != pieces.Length)
        {
            throw new InvalidDataException("Invalid dragon hoard template.");
        }
        return pieces;
    }

    // Existing worlds may have the same graphics saved as individual statics.
    internal static int Install(Map map, Point3D center)
    {
        var placed = TryPlace(map, center);
        if (placed == null && Find(map, center) == null) { return 0; }

        using var old = PooledRefList<Static>.Create();
        foreach (var piece in _template.Value)
        {
            var location = new Point3D(center.X + piece.X, center.Y + piece.Y, center.Z);
            foreach (var item in map.GetItemsAt<Static>(location))
            {
                if (item.GetType() == typeof(Static) && item.Location == location && item.ItemID == piece.Graphic)
                    old.Add(item);
            }
        }
        foreach (var item in old) { item.Delete(); }
        return placed?.Components.Count ?? 0;
    }

    private static DragonHoard Find(Map map, Point3D center)
    {
        foreach (var hoard in map.GetItemsAt<DragonHoard>(center))
            if (hoard.Location == center) { return hoard; }
        return null;
    }

    private static bool OnMap(Map map, int x, int y) => x >= 0 && x < map.Width && y >= 0 && y < map.Height;

    internal static DragonHoard TryPlace(Map map, Point3D center)
    {
        if (map == null || map == Map.Internal || !OnMap(map, center.X, center.Y)
            || center.Z is < sbyte.MinValue or > sbyte.MaxValue || Find(map, center) != null
            || !map.CanFit(center.X, center.Y, center.Z, 1, checkMobiles: false, requireSurface: true)
            || _template.Value.Any(p => !OnMap(map, center.X + p.X, center.Y + p.Y)))
        {
            return null;
        }

        var hoard = new DragonHoard();
        try
        {
            hoard.MoveToWorld(center, map);
            foreach (var piece in _template.Value)
                hoard.AddComponent(new AddonComponent(piece.Graphic), piece.X, piece.Y, 0);
            return hoard;
        }
        catch
        {
            hoard.Delete();
            throw;
        }
    }

    [Usage("DragonHoard [x y [z]]"), Description("Places a dragon's nest and its ground scatter as one decoration.")]
    private static void Place(CommandEventArgs args)
    {
        var gm = args.Mobile;
        var map = gm.Map;
        if (map == null || map == Map.Internal) { return; }

        if (args.Length == 0)
        {
            gm.Target = new HoardTarget(map);
            gm.SendMessage("Target the ground where the nest should sit.");
            return;
        }

        int z = 0;
        if (args.Length is not (2 or 3) || !int.TryParse(args.GetString(0), out int x)
            || !int.TryParse(args.GetString(1), out int y) || !OnMap(map, x, y)
            || args.Length == 3 && !int.TryParse(args.GetString(2), out z))
        {
            gm.SendMessage("Use [DragonHoard x y with an optional z, or omit coordinates to target the ground.");
            return;
        }

        PlaceFor(gm, map, new(x, y, args.Length == 2 ? map.GetAverageZ(x, y) : z));
    }

    private static void PlaceFor(Mobile gm, Map map, Point3D center)
    {
        gm.SendMessage(TryPlace(map, center) != null
            ? $"Placed a dragon's hoard at {center}."
            : "The hoard needs a clear ground anchor and space within the facet. A hoard cannot share the same anchor.");
    }

    private sealed class HoardTarget(Map map) : Target(-1, true, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from.AccessLevel < AccessLevel.GameMaster || from.Map != map) { return; }

            if (targeted is LandTarget or StaticTarget && targeted is IPoint3D spot && OnMap(map, spot.X, spot.Y))
                PlaceFor(from, map, new(spot.X, spot.Y, map.GetAverageZ(spot.X, spot.Y)));
            else
                from.SendMessage("Target the ground where the nest should sit.");
        }
    }
}
