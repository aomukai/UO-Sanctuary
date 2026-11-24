// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Regions;

namespace Sanctuary;

internal sealed class MausoleumRegion(CampaignArenas.Arena arena, Map map, Rectangle3D bounds)
    : DungeonRegion(arena.Name, map, 200, bounds)
{
    public override void AlterLightLevel(Mobile mobile, ref int global, ref int personal) => global = 12;

    public override void OnEnter(Mobile mobile)
    {
        base.OnEnter(mobile);
        if (mobile is PlayerMobile player)
            Timer.DelayCall(TimeSpan.FromMilliseconds(600), () => LazarusMausoleum.Greet(arena, player));
    }
}

internal static class LazarusMausoleum
{
    private sealed record Tier(int HalfWidth, int HalfDepth, int Chamfer, int Z);
    private static readonly Tier[] Chancel = LoadChancel();

    private static Tier[] LoadChancel()
    {
        using var stream = typeof(LazarusMausoleum).Assembly.GetManifestResourceStream("Sanctuary.Mausoleum.Chancel");
        return JsonSerializer.Deserialize<Tier[]>(stream);
    }

    internal static int Elevation(int x, int y)
    {
        // The long edge follows the chamber's east–west (X) wall. Each local tile axis
        // projects to a diagonal of length sqrt(2) in the painting's units.
        double across = Math.Sqrt(2) * Math.Abs(x - WorldDecorationGraphics.MausoleumMid);
        double along = Math.Sqrt(2) * Math.Abs(y - WorldDecorationGraphics.MausoleumMid);
        for (int i = Chancel.Length - 1; i >= 0; --i)
        {
            var tier = Chancel[i];
            if (across <= tier.HalfWidth + 0.5 && along <= tier.HalfDepth + 0.5 &&
                across + along <= tier.HalfWidth + tier.HalfDepth + 1 - tier.Chamfer) return tier.Z;
        }
        return 0;
    }

    internal static readonly (int X, int Y, ResistanceType Element, int Hue)[] Circles =
    [
        (5, 49, ResistanceType.Fire, 0x26), (12, 12, ResistanceType.Cold, 0x5),
        (48, 4, ResistanceType.Energy, 0x35), (83, 50, ResistanceType.Poison, 0x44),
        (49, 83, ResistanceType.Physical, 0x481)
    ];

    internal static (int X, int Y)[] Piers => WorldDecorationGraphics.MausoleumPiers;
    private static readonly int[] Pentagram = [0xFE7, 0xFE8, 0xFEB, 0xFE6, 0xFEA, 0xFEE, 0xFE9, 0xFEC, 0xFED];
    internal static Point3D Circle(CampaignArenas.Arena arena, int index) =>
        arena.Offset(Circles[index].X, Circles[index].Y);

    internal static void Greet(CampaignArenas.Arena arena, PlayerMobile player)
    {
        if (player.Deleted || !player.Alive || player.NetState?.Running != true
            || CampaignArenas.At(player.Map, player.Location) != arena) return;
        CampaignArenas.EnsureBoss(arena);
        if (arena.Boss is ArchbishopLazarus boss) boss.Greet(player);
    }

    // An older save can contain a full court, loose items, or an Avatar logged out
    // on the old treads. Move occupants with their individual floor surface.
    private static void MoveExistingContents(Map map, Dictionary<Point2D, (int OldZ, Point3D Top)> floors, bool moveFixtures)
    {
        foreach (var (cell, change) in floors)
        {
            var items = new List<Item>();
            foreach (var item in map.GetItemsAt(cell.X, cell.Y))
                if (item.Z == change.OldZ && (moveFixtures || item.Movable || item is Server.Items.Corpse)) items.Add(item);
            foreach (var item in items) item.MoveToWorld(change.Top, map);
        }
        foreach (var mobile in World.Mobiles.Values.ToArray())
        {
            if (mobile.Map == map && floors.TryGetValue(new(mobile.X, mobile.Y), out var change) && mobile.Z == change.OldZ)
                mobile.MoveToWorld(change.Top, map);
            else if (mobile is PlayerMobile p && p.Map == Map.Internal && p.LogoutMap == map
                && floors.TryGetValue(new(p.LogoutLocation.X, p.LogoutLocation.Y), out var login) && p.LogoutLocation.Z == login.OldZ)
                p.LogoutLocation = login.Top;
        }
    }

    internal static void Build(Map map, CampaignArenas.Arena arena)
    {
        var existing = new HashSet<(Point3D, int)>();
        var retiredAltar = new List<CampaignArenaTile>();
        var changedFloors = new Dictionary<Point2D, (int OldZ, Point3D Top)>();
        foreach (var tile in map.GetItemsInBounds<CampaignArenaTile>(arena.Bounds))
        {
            int x = tile.X - arena.Origin.X, y = tile.Y - arena.Origin.Y;
            if ((tile.ItemID == 0x2A9B && x == 45 && y == 46)
                || (tile.ItemID == 0x2A9A && x == 46 && y == 46)
                || (tile.ItemID == WorldDecorationGraphics.MausoleumAltar && (x != WorldDecorationGraphics.AltarX || y != WorldDecorationGraphics.AltarY))
                || (tile.ItemID == WorldDecorationGraphics.MausoleumAltarBlocker && !WorldDecorationGraphics.InAltarFootprint(x, y)))
            {
                retiredAltar.Add(tile);
                continue;
            }
            var surface = arena.Offset(x, y);
            if (tile.ItemID == arena.Floor && tile.Z != surface.Z)
                changedFloors[new Point2D(tile.X, tile.Y)] = (tile.Z, surface);
            existing.Add((surface, tile.ItemID));
        }
        foreach (var tile in retiredAltar) tile.Delete();
        if (changedFloors.Count > 0) MoveExistingContents(map, changedFloors, moveFixtures: true);

        bool hasSavedFloor = existing.Any(entry => entry.Item2 == arena.Floor);
        bool Place(int x, int y, int graphic, int hue = 0, bool light = false)
        {
            var point = arena.Offset(x, y);
            if (!existing.Add((point, graphic))) return false;
            var tile = new CampaignArenaTile(graphic, hue);
            if (graphic == WorldDecorationGraphics.MausoleumAltar) tile.Name = "a sacrificial altar";
            if (light) tile.Light = LightType.Circle225;
            tile.MoveToWorld(point, map);
            return true;
        }

        var clearOfObstacles = new Dictionary<Point2D, (int, Point3D)>();
        for (int y = 0; y < arena.Height; y++)
        for (int x = 0; x < arena.Width; x++)
        {
            Place(x, y, arena.Floor);
            if (WorldDecorationGraphics.InAltarFootprint(x, y))
            {
                bool added = Place(x, y, WorldDecorationGraphics.MausoleumAltarBlocker);
                if (retiredAltar.Count > 0 || (added && hasSavedFloor))
                {
                    int aisle = y + 1;
                    while (WorldDecorationGraphics.InAltarFootprint(x, aisle)) ++aisle;
                    var surface = arena.Offset(x, y);
                    clearOfObstacles[new Point2D(surface.X, surface.Y)] = (surface.Z, arena.Offset(x, aisle));
                }
            }
            if (x == 0 || x == arena.Width - 1 || y == 0 || y == arena.Height - 1)
                Place(x, y, 0x027E);
        }
        foreach (var pier in Piers)
            for (int x = -4; x <= 4; x++)
            for (int y = -4; y <= 4; y++)
            {
                if (Place(pier.X + x, pier.Y + y, 0x027E) && hasSavedFloor)
                {
                    var surface = arena.Offset(pier.X + x, pier.Y + y);
                    clearOfObstacles[new Point2D(surface.X, surface.Y)] = (surface.Z, arena.Offset(pier.X - 5, pier.Y + y));
                }
            }

        foreach (var circle in Circles)
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
                Place(circle.X + x, circle.Y + y, Pentagram[(y + 1) * 3 + x + 1], circle.Hue);

        Place(WorldDecorationGraphics.AltarX, WorldDecorationGraphics.AltarY, WorldDecorationGraphics.MausoleumAltar);
        if (clearOfObstacles.Count > 0) MoveExistingContents(map, clearOfObstacles, moveFixtures: false);
        foreach (int bay in new[] { 10, 28, 46, 64, 79 })
        {
            Place(2, bay, 0xE31, light: true);
            Place(bay, 2, 0xE31, light: true);
        }
        foreach (var pier in Piers)
            Place(pier.X + 5, pier.Y + 4, 0xE31, light: true);
    }
}
