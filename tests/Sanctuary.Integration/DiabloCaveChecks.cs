using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Commands;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class DiabloCaveChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(DiabloCaveChecks));

    public static void Configure()
    {
        ConsoleInputHandler.RegisterCommand("diablo-cave-check", "Check the installed Diablo cave and travel", _ =>
            Core.LoopContext.Post(() =>
            {
                try
                {
                    Check();
                    _logger.Information("diablo-cave-check: passed");
                }
                catch (Exception exception)
                {
                    _logger.Error(exception, "diablo-cave-check: failed");
                }
            }));
    }

    private static void Check()
    {
        var map = Map.Trammel;
        var arena = CampaignArenas.All.Single(entry => entry.IsDiablo);
        var destination = arena.Center;
        Require(arena.Center == new Point3D(6976, 3932, 0));
        Require(CampaignArenas.At(map, DiabloCave.OldEntry) == null);
        Require(CampaignArenas.At(map, arena.Entry) == arena);
        var tiles = new List<CampaignArenaTile>();
        foreach (var item in map.GetItemsInBounds(arena.Bounds))
            if (item is CampaignArenaTile tile)
                tiles.Add(tile);

        Require(tiles.All(tile => tile.GetUpdateRange(null) == WorldDecorationGraphics.DiabloTerrainRange),
            "cave terrain must use the same range on the client and server");
        var graphics = tiles.Where(tile => WorldDecorationGraphics.IsDiabloBlood((ushort)tile.ItemID)).ToArray();
        Require(graphics.Length >= 25 && graphics.Select(tile => tile.ItemID).Distinct().Count() == graphics.Length);
        var carpet = tiles.Where(tile => WorldDecorationGraphics.IsDiabloCarpet((ushort)tile.ItemID)).ToArray();
        Require(carpet.Length >= 50 && carpet.Select(tile => tile.ItemID).Distinct().Count() == carpet.Length);
        var trenches = tiles.Where(tile => tile.ItemID == WorldDecorationGraphics.DiabloLavaBarrier).ToArray();
        Require(trenches.Length >= 450 && trenches.All(tile => !map.CanFit(tile.X, tile.Y, tile.Z, 16)));
        var ground = tiles.Where(tile => tile.ItemID is >= 0x053B and <= 0x053F).Select(tile => tile.Location).ToHashSet();
        Require(trenches.All(tile => ground.Contains(tile.Location)));
        Require(tiles.All(tile => tile.ItemID is < 59960 or >= WorldDecorationGraphics.DiabloLavaBarrier));

        bool Land(Point3D p) => map.CanFit(p.X, p.Y, p.Z, 16, checkMobiles: false, requireSurface: true);
        int mid = DiabloCave.Midpoint;
        for (int x = -8; x <= 8; x++)
        for (int y = -8; y <= 8; y++)
            Require(Land(arena.Offset(mid + x, mid + y)), $"solid center at {x},{y}");

        for (int x = 5; x <= mid; x++)
        for (int y = -2; y <= 2; y++)
            Require(Land(arena.Offset(x, mid + y)), $"bridge width at {x},{y}");

        foreach (var (x, y, horizontal) in new[]
        {
            (mid, mid - 28, true), (mid, mid + 28, true), (mid + 28, mid, false), (mid - 26, mid + 7, false)
        })
        {
            var a = arena.Offset(x - (horizontal ? 0 : 2), y - (horizontal ? 2 : 0));
            var b = arena.Offset(x + (horizontal ? 0 : 3), y + (horizontal ? 3 : 0));
            Require(Land(a) && Land(b) && !EncounterProfiles.ClearSight(map, a, b), $"cover at {x},{y}");
            var clearA = arena.Offset(x + (horizontal ? 5 : -2), y + (horizontal ? -2 : 5));
            var clearB = arena.Offset(x + (horizontal ? 5 : 3), y + (horizontal ? 3 : 5));
            Require(Land(clearA) && Land(clearB) && EncounterProfiles.ClearSight(map, clearA, clearB), $"route around cover at {x},{y}");
        }

        var steps = new Queue<Point3D>();
        var visited = new HashSet<Point3D> { arena.Entry };
        steps.Enqueue(arena.Entry);
        Point2D[] directions = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];
        while (steps.TryDequeue(out var current))
            foreach (var d in directions)
            {
                var next = new Point3D(current.X + d.X, current.Y + d.Y, current.Z);
                if (arena.Bounds.Contains(next) && !visited.Contains(next) && Land(next))
                {
                    visited.Add(next);
                    steps.Enqueue(next);
                }
            }

        Require(visited.Contains(destination) && visited.Count > 3400, $"reachable cave tiles: {visited.Count}");
        bool freshEncounter = arena.Boss == null;
        CampaignArenas.EnsureBoss(arena);
        Require(arena.Boss.Home == destination && arena.Boss.HomeLeashRadius == DiabloCave.LeashRadius);
        Require(freshEncounter ? arena.Boss.Location == destination : arena.Bounds.Contains(arena.Boss.Location));
        CampaignArenaGate matchingGate = null;
        foreach (var gate in map.GetItemsAt<CampaignArenaGate>(arena.Door))
            if (gate.PointDest == arena.Entry)
                matchingGate = gate;
        Require(matchingGate != null);
        CheckMigration(map, arena, matchingGate);
    }

    private static void CheckMigration(Map map, CampaignArenas.Arena arena, CampaignArenaGate gate)
    {
        var player = new PlayerMobile { LogoutMap = map, LogoutLocation = DiabloCave.OldEntry };
        var floor = new CampaignArenaTile(0x053B, 0);
        var loot = new Gold(42);
        try
        {
            floor.MoveToWorld(DiabloCave.OldEntry, map);
            loot.MoveToWorld(DiabloCave.OldEntry, map);
            gate.PointDest = DiabloCave.OldEntry;
            DiabloCave.RetireOldArena(map, arena);
            Require(floor.Deleted && loot.Location == arena.Entry && loot.Amount == 42);
            Require(gate.PointDest == arena.Entry);
            Require(player.LogoutLocation == arena.Entry);
        }
        finally
        {
            player.Delete();
            floor.Delete();
            loot.Delete();
        }
    }

    private static void Require(bool condition,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string reason = null)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
