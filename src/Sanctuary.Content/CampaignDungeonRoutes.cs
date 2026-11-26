// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Commands;
using Server.Collections;
using Server.Items;
using Server.Json;
using Server.Mobiles;

namespace Sanctuary;

internal static class CampaignDungeonRoutes
{
    internal static readonly Point3D ShameSurfaceLanding = new(512, 1564, 0);

    private static readonly HashSet<Point3D> _shameMoongates =
    [
        new(5490, 19, -25), new(5514, 10, 5), new(5604, 102, 5), new(5514, 147, 25),
        new(5538, 170, 5), new(5513, 176, 5), new(5507, 162, 5), new(5875, 19, -5)
    ];

    private static readonly Point3D _wrongMage = new(5703, 639, 0);
    private static readonly Point3D _wrongMageApproach = new(5703, 641, 0);
    // Wrong's legacy decoration places inert sparkles both above triggers and at arrival points.
    private static readonly Point3D[] _wrongDecoration =
    [
        new(5703, 639, 0), new(5706, 625, 0), new(5698, 662, 0), new(5732, 554, 24),
        new(5792, 525, 10), new(5827, 593, 0), new(5829, 593, 0), new(5690, 569, 25),
        new(5867, 528, 15), new(5872, 532, 24)
    ];

    internal static void Install()
    {
        var map = Map.Trammel;
        var path = Path.Join(Core.BaseDirectory, "Data", "teleporters.json");
        var routes = JsonConfig.Deserialize<TeleporterDefinition[]>(path);
        RemoveWrongDecoration(map);
        InstallShameSurfaceRoutes(map);
        var optionalSources = new HashSet<Point3D>();
        foreach (var route in routes)
        {
            if (route.Source.Map != map || route.Destination.Map != map)
                continue;
            var source = route.Source.Location;
            var dest = route.Destination.Location;
            if (IsShameSurfaceRoute(source, dest) || IsShameSurfaceRoute(dest, source))
                continue;

            string dungeon = DungeonAt(source, map) ?? DungeonAt(dest, map);
            if (dungeon == null)
                continue;

            var from = TownMarkets.Floor(map, source, [], false);
            // Leave room for the named encounter at the stock court landing.
            var to = TownMarkets.Floor(map, dungeon == "Wrong" && dest == _wrongMage ? _wrongMageApproach : dest, [], false);
            PlaceRoute(from, to, dungeon);
            if (route.Back)
                PlaceRoute(to, from, dungeon);
        }

        void PlaceRoute(Point3D from, Point3D to, string dungeon)
        {
            // Keep each surface access lane. Two stock Wrong lanes share one inside exit.
            if (dungeon is "Shame" or "Wrong" && !optionalSources.Add(from))
                return;
            Place(map, from, to, dungeon);
        }
    }

    private static bool IsShameSurfaceRoute(Point3D outside, Point3D inside) =>
        inside.X is >= 5394 and <= 5396 && inside.Y == 127
        && (outside.X is >= 512 and <= 514 && outside.Y is 1559 or 1560 || outside == ShameSurfaceLanding);

    private static void InstallShameSurfaceRoutes(Map map)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            var oldOutside = new Point3D(512 + lane, 1559, 0);
            var oldInside = new Point3D(5394 + lane, 127, 0);
            var inside = TownMarkets.Floor(map, oldInside, [], false);
            var outside = new Point3D(512 + lane, 1560, 0);
            var oldFloor = TownMarkets.Floor(map, oldOutside, [], false);
            RemoveLegacyShameRoute(map, oldOutside, oldInside, includeStock: true);
            RemoveLegacyShameRoute(map, oldInside, oldOutside, includeStock: true);
            RemoveLegacyShameRoute(map, oldFloor, inside, includeStock: false);
            RemoveLegacyShameRoute(map, inside, oldFloor, includeStock: false);
            PlaceShameSurfaceRoute(map, outside, inside);
            PlaceShameSurfaceRoute(map, inside, ShameSurfaceLanding);
        }
    }

    private static void RemoveLegacyShameRoute(Map map, Point3D from, Point3D to, bool includeStock)
    {
        using var obsolete = PooledRefList<Teleporter>.Create();
        foreach (var item in map.GetItemsAt<Teleporter>(from))
            if ((item is CampaignDungeonStair || includeStock && item.GetType() == typeof(Teleporter))
                && item.Location == from && item.PointDest == to && item.MapDest == map)
                obsolete.Add(item);
        foreach (var item in obsolete)
            item.Delete();
    }

    private static void PlaceShameSurfaceRoute(Map map, Point3D from, Point3D to)
    {
        foreach (var item in map.GetItemsAt<Teleporter>(from))
        {
            if (item.GetType() != typeof(Teleporter) || item.Location != from || item.PointDest != to || item.MapDest != map)
                continue;
            SetShameSurfaceArt(item);
            return;
        }

        var passage = new Teleporter(to, map);
        SetShameSurfaceArt(passage);
        passage.MoveToWorld(from, map);
    }

    private static void SetShameSurfaceArt(Teleporter passage)
    {
        passage.Name = "a passage through Shame";
        passage.ItemID = 0x375A;
        passage.Visible = true;
    }

    private static void RemoveWrongDecoration(Map map)
    {
        using var old = PooledRefList<Static>.Create();
        foreach (var point in _wrongDecoration)
            foreach (var item in map.GetItemsAt<Static>(point))
                if (item.GetType() == typeof(Static) && item.Location == point && item.ItemID == 0x375A && !item.Movable)
                    old.Add(item);
        foreach (var item in old)
            item.Delete();
    }

    private static string DungeonAt(Point3D point, Map map)
    {
        var region = Region.Find(point, map);
        if (region.IsPartOf("Shame"))
            return "Shame";
        if (region.IsPartOf("Wrong"))
            return "Wrong";
        foreach (var arena in CampaignArenas.All)
            if (region.IsPartOf(arena.Dungeon))
                return arena.Dungeon;
        return null;
    }

    private static void Place(Map map, Point3D from, Point3D to, string dungeon)
    {
        bool moongate = dungeon == "Shame" && _shameMoongates.Contains(from);
        if (moongate)
            RemoveShameMoongateDecoration(map, from);

        foreach (var existing in map.GetItemsAt<CampaignDungeonStair>(from))
        {
            if (existing.Location != from || existing.PointDest != to)
                continue;
            if (moongate)
                existing.UseBlackMoongateArt();
            return;
        }

        var passage = new CampaignDungeonStair(to) { Name = $"a passage through {dungeon}" };
        if (moongate)
            passage.UseBlackMoongateArt();
        passage.MoveToWorld(from, map);
    }

    private static void RemoveShameMoongateDecoration(Map map, Point3D floor)
    {
        // The stock black gate is scenery one Z above the walk trigger.
        using var decoration = PooledRefList<Static>.Create();
        foreach (var item in map.GetItemsAt<Static>(floor))
            if (item.GetType() == typeof(Static) && item.Z == floor.Z + 1 && !item.Movable
                && item.ItemID == CampaignDungeonStair.BlackMoongateArt)
                decoration.Add(item);
        foreach (var item in decoration)
            item.Delete();
    }
}

[SerializationGenerator(0)]
public partial class CampaignDungeonStair : Teleporter
{
    internal const int BlackMoongateArt = 0x1FD4;

    internal CampaignDungeonStair(Point3D destination) : base(destination, Map.Trammel)
    {
        ItemID = 0x375A;
        Visible = true;
    }

    internal void UseBlackMoongateArt()
    {
        ItemID = BlackMoongateArt;
        Light = LightType.Circle300;
    }

    public override bool OnMoveOver(Mobile mobile) =>
        ItemID != BlackMoongateArt || mobile is not PlayerMobile || Math.Abs(mobile.Z - Z) >= 16
        || base.OnMoveOver(mobile);

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile && CanDoubleClick(from) && CanTeleport(from))
            StartTeleport(from);
    }
}
