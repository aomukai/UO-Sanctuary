// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Items;
using Server.Json;
using Server.Mobiles;
using Server.Regions;

namespace Sanctuary;

internal sealed class DiabloCaveRegion(string name, Map map, Rectangle3D bounds) : DungeonRegion(name, map, 200, bounds)
{
    public override void AlterLightLevel(Mobile mobile, ref int global, ref int personal) => global = 13;
}

public static class DiabloCave
{
    internal const int Size = WorldDecorationGraphics.DiabloCaveSize;
    internal const int Midpoint = Size / 2;
    internal const int LeashRadius = 39;
    internal static readonly Point3D Origin = new(WorldDecorationGraphics.DiabloCaveX, WorldDecorationGraphics.DiabloCaveY, 0);
    internal static readonly Point3D OldEntry = new(6984, 3812, 0);
    internal static readonly Rectangle2D RetiredBounds = new(6980, 3800, 33, 25);
    private static readonly int[] _ribs = [0x025D, 0x0263, 0x0265, 0x0267, 0x027D, 0x027E];

    private const int PlatformRadius = 25;
    private const int LegacyStoneFirst = 59960;
    private sealed record ArtSection(ushort Graphic, int X, int Y);
    private sealed record RitualPlan(ArtSection[] Sections, int[][] Stone, int[][] Lava);

    public static void Initialize()
    {
        for (int id = WorldDecorationGraphics.DiabloCarpetFirst; id < WorldDecorationGraphics.DiabloBloodEnd; id++)
            TileData.ItemTable[id] = new ItemData("ritual floor art", TileFlag.Background, 255, 0, 0, 0, 0, 0);

        TileData.ItemTable[WorldDecorationGraphics.DiabloLavaBarrier] =
            new ItemData("lava", TileFlag.Impassable, 255, 0, 0, 0, 0, 20);
    }

    internal static void Build(Map map, CampaignArenas.Arena arena)
    {
        var ritual = JsonConfig.Deserialize<RitualPlan>(Path.Combine(Core.BaseDirectory, "Data", "Sanctuary", "diablo-ritual.json"));
        if (ritual?.Sections is not { Length: > 0 } || ritual.Lava is not { Length: > 0 } || ritual.Stone is not { Length: > 0 }
            || ritual.Sections.Any(section => !WorldDecorationGraphics.IsDiabloFloorArt(section.Graphic)
                || Math.Abs(section.X) >= Midpoint || Math.Abs(section.Y) >= Midpoint)
            || ritual.Sections.Select(section => section.Graphic).Distinct().Count() != ritual.Sections.Length
            || ritual.Lava.Concat(ritual.Stone).Any(cell => cell.Length != 2 || cell.Any(axis => Math.Abs(axis) > PlatformRadius)))
            throw new InvalidDataException("Invalid Diablo ritual floor layout.");

        var stone = ritual.Stone.Select(cell => (cell[0] + Midpoint, cell[1] + Midpoint)).ToHashSet();
        var lava = ritual.Lava.Select(cell => (cell[0] + Midpoint, cell[1] + Midpoint)).ToHashSet();
        if (stone.Overlaps(lava) || !stone.Contains((Midpoint, Midpoint)))
            throw new InvalidDataException("Diablo's platform and lava overlap.");

        var existing = new HashSet<(Point3D Location, int Graphic)>();
        var overlays = ritual.Sections.ToDictionary(section => (int)section.Graphic,
            section => arena.Offset(Midpoint + section.X, Midpoint + section.Y));
        var retiredArt = new List<CampaignArenaTile>();
        foreach (var item in map.GetItemsInBounds(arena.Bounds))
        {
            if (item is not CampaignArenaTile tile)
                continue;
            if (tile.ItemID is >= LegacyStoneFirst and < WorldDecorationGraphics.DiabloLavaBarrier
                || (WorldDecorationGraphics.IsDiabloFloorArt((ushort)tile.ItemID)
                    && (!overlays.TryGetValue(tile.ItemID, out var at) || at != tile.Location)))
                retiredArt.Add(tile);
            else
                existing.Add((tile.Location, tile.ItemID));
        }
        foreach (var tile in retiredArt)
            tile.Delete();

        void Tile(int x, int y, int graphic, int z = 0)
        {
            var point = new Point3D(arena.Origin.X + x, arena.Origin.Y + y, arena.Origin.Z + z);
            if (existing.Add((point, graphic)))
                new CampaignArenaTile(graphic, 0).MoveToWorld(point, map);
        }

        bool LavaTrench(int x, int y) => x is >= 8 and <= 11 && Math.Abs(y - Midpoint) is >= 3 and <= 5;
        bool Arrival(int x, int y) => x is >= 3 and <= 7 && Math.Abs(y - Midpoint) <= 4;
        bool Bridge(int x, int y) => x is >= 8 and <= 12 && Math.Abs(y - Midpoint) <= 2;
        bool Cave(int x, int y) =>
            x is >= 10 and <= 76 && y is >= 4 and <= 76
            && Math.Abs(x - Midpoint) + Math.Abs(y - Midpoint) <= 61 + (x * 7 + y * 11) % 4
            && (x >= 12 || Math.Abs(y - Midpoint) > 5);
        bool Interior(int x, int y) => Arrival(x, y) || Bridge(x, y) || Cave(x, y) || LavaTrench(x, y);
        bool Border(int x, int y) => Interior(x - 1, y) || Interior(x + 1, y) || Interior(x, y - 1) || Interior(x, y + 1);

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            if (Interior(x, y))
            {
                Tile(x, y, Bridge(x, y) ? 0x051E : 0x053B + (x * 11 + y * 7 + x * y) % 5);
                if (lava.Contains((x, y)) || LavaTrench(x, y))
                    Tile(x, y, WorldDecorationGraphics.DiabloLavaBarrier);
            }
            else if (Border(x, y))
                Tile(x, y, _ribs[(x * 13 + y * 3) % _ribs.Length]);
        }

        // Four separate shelves of native rock. Each has open ground on both
        // sides and at each end, so it can break sight without ending a chase.
        Ridge(Midpoint, Midpoint - 28, horizontal: true);
        Ridge(Midpoint, Midpoint + 28, horizontal: true);
        Ridge(Midpoint + 28, Midpoint, horizontal: false);
        Ridge(Midpoint - 26, Midpoint + 7, horizontal: false);

        void Ridge(int x, int y, bool horizontal)
        {
            for (int step = -3; step <= 3; step++)
            {
                int rx = x + (horizontal ? step : 0), ry = y + (horizontal ? 0 : step);
                Tile(rx, ry, _ribs[(step + 3 + (horizontal ? 1 : 0)) % _ribs.Length]);
                if (Math.Abs(step) <= 1)
                    Tile(rx + (horizontal ? 0 : 1), ry + (horizontal ? 1 : 0), 0x027E);
            }
        }

        foreach (var section in ritual.Sections)
            Tile(Midpoint + section.X, Midpoint + section.Y, section.Graphic);
        foreach (var (x, y) in new[] { (-10, -10), (9, -15), (15, 0), (0, 15), (-15, 9) })
            Tile(Midpoint + x, Midpoint + y, 0x1B10);

        foreach (var (x, y, graphic) in new (int, int, int)[]
        {
            (18, 15, 0x08E8), (33, 8, 0x08E1), (51, 7, 0x1B0E), (64, 15, 0x08E4),
            (74, 28, 0x08E8), (75, 51, 0x1B10), (64, 67, 0x08E6), (48, 74, 0x08E4),
            (31, 73, 0x1B0B), (17, 63, 0x08E8), (12, 51, 0x0ED0), (13, 24, 0x08E4)
        })
            Tile(x, y, graphic);
        Tile(33, 7, 0x1A07, 8);
        Tile(75, 45, 0x1A08, 8);
        Tile(31, 74, 0x1A07, 8);
    }

    internal static void RetireOldArena(Map map, CampaignArenas.Arena arena) =>
        RetireArena(map, arena, RetiredBounds, OldEntry);

    internal static void RetireArena(Map map, CampaignArenas.Arena arena, Rectangle2D retired, Point3D entry)
    {
        bool Old(Point3D point) => retired.Contains(point) && Math.Abs(point.Z) < 16;

        var oldTiles = new List<CampaignArenaTile>();
        var looseItems = new List<Item>();
        foreach (var item in map.GetItemsInBounds(retired))
        {
            if (!Old(item.Location))
                continue;
            if (item is CampaignArenaTile tile)
                oldTiles.Add(tile);
            else if (item.Movable || item is Corpse)
                looseItems.Add(item);
        }

        CampaignArenaGate oldGate = null;
        foreach (var gate in map.GetItemsAt<CampaignArenaGate>(arena.Door))
            if (gate.PointDest == entry && gate.MapDest == map)
                oldGate = gate;
        if (oldTiles.Count == 0 && oldGate == null)
            return;

        foreach (var mobile in World.Mobiles.Values.ToArray())
        {
            if (mobile.Map == map && Old(mobile.Location))
            {
                var destination = mobile switch
                {
                    CampaignBoss boss when boss.Dungeon.Id == arena.Profile.Id => arena.BossPost,
                    DestardChampion => arena.Offset(Midpoint + 1, Midpoint),
                    _ => arena.Entry
                };
                mobile.MoveToWorld(destination, map);
                if (mobile is CampaignBoss migrated)
                {
                    migrated.Home = arena.BossPost;
                    migrated.HomeMap = map;
                    if (arena.IsDiablo) migrated.RangeHome = LeashRadius;
                }
            }
            else if (mobile is PlayerMobile player && player.Map == Map.Internal
                && player.LogoutMap == map && Old(player.LogoutLocation))
                player.LogoutLocation = arena.Entry;
        }

        foreach (var item in looseItems)
            item.MoveToWorld(arena.Entry, map);
        foreach (var tile in oldTiles)
            tile.Delete();
        if (oldGate != null)
            oldGate.PointDest = arena.Entry;
    }
}
