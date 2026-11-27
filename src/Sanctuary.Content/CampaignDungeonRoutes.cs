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
    internal static readonly Point3D HythlothSurfaceLanding = new(4721, 3816, 0);

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
        InstallHythlothSurfaceRoutes(map);
        var optionalSources = new HashSet<Point3D>();
        foreach (var route in routes)
        {
            if (route.Source.Map != map || route.Destination.Map != map)
                continue;
            var source = route.Source.Location;
            var dest = route.Destination.Location;
            if (IsShameSurfaceRoute(source, dest) || IsShameSurfaceRoute(dest, source)
                || IsStockHythlothSurfaceRoute(source, dest) || IsStockHythlothSurfaceRoute(dest, source))
                continue;

            string dungeon = DungeonAt(source, map) ?? DungeonAt(dest, map);
            if (dungeon == null)
                continue;

            var from = TownMarkets.Floor(map, source, [], false);
            // Leave room for the named encounter at the stock court landing.
            var to = TownMarkets.Floor(map, dungeon == "Wrong" && dest == _wrongMage ? _wrongMageApproach : dest, [], false);
            bool fromDeceitEntrance = IsDeceitSurfaceRoute(source, dest);
            bool toDeceitEntrance = IsDeceitSurfaceRoute(dest, source);
            if (fromDeceitEntrance || toDeceitEntrance)
            {
                RemoveLegacyDeceitRoute(map, from, to);
                if (route.Back)
                    RemoveLegacyDeceitRoute(map, to, from);
                if (fromDeceitEntrance)
                    from = new Point3D(source.X, source.Y + 1, source.Z);
                if (toDeceitEntrance)
                    to = new Point3D(dest.X, dest.Y + 1, dest.Z);
            }
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

    private static bool IsStockHythlothSurfaceRoute(Point3D outside, Point3D inside) =>
        outside.Y == 3813 && outside.X is >= 4721 and <= 4723 && inside.Y == 16 && inside.X is >= 5904 and <= 5906;

    private static bool IsDeceitSurfaceRoute(Point3D outside, Point3D inside) =>
        outside is { X: >= 4110 and <= 4112, Y: 430, Z: 5 }
        && inside is { X: >= 5186 and <= 5189, Y: 639, Z: 0 };

    private static void RemoveLegacyDeceitRoute(Map map, Point3D from, Point3D to)
    {
        using var obsolete = PooledRefList<Teleporter>.Create();
        foreach (var item in map.GetItemsAt<Teleporter>(from))
            if ((item is CampaignDungeonStair || item.GetType() == typeof(Teleporter))
                && item.Location == from && item.PointDest == to && (item.MapDest == map || item.MapDest == null))
                obsolete.Add(item);
        foreach (var item in obsolete)
            item.Delete();
    }

    private static bool NearHythlothSurface(Point3D point) =>
        point.X is >= 4716 and <= 4728 && point.Y is >= 3808 and <= 3818;

    private static bool NearHythlothInterior(Point3D point) =>
        point.X is >= 5902 and <= 5908 && point.Y is >= 13 and <= 21;

    private static bool GroundHythlothEntrance(Point3D point) =>
        point.X is >= 4720 and <= 4722 && point.Y == 3814 && point.Z == 0;

    private static Point3D HythlothInteriorLanding(int lane) => new(5904 + lane, 21, 44);

    private static Point3D HythlothInteriorExit(int lane) => new(5904 + lane, 20, 46);

    private static void InstallHythlothSurfaceRoutes(Map map)
    {
        using var obsolete = PooledRefList<Teleporter>.Create();
        foreach (var item in World.Items.Values)
        {
            if (item is not Teleporter route || route.Map != map || route.MapDest != map
                || route is not CampaignDungeonStair && route.GetType() != typeof(Teleporter))
                continue;
            var from = route.Location;
            var to = route.PointDest;
            if (NearHythlothSurface(from) && NearHythlothInterior(to)
                && (!GroundHythlothEntrance(from) || to != HythlothInteriorLanding(from.X - 4720))
                || NearHythlothInterior(from) && NearHythlothSurface(to)
                && (to != HythlothSurfaceLanding || from.X is < 5904 or > 5906 || from != HythlothInteriorExit(from.X - 5904)))
                obsolete.Add(route);
        }
        foreach (var route in obsolete)
            route.Delete();

        for (int lane = 0; lane < 3; lane++)
        {
            // Leave the first step north of the arrival tiles for the return passage.
            PlaceSurfaceRoute(map, new(4720 + lane, 3814, 0), HythlothInteriorLanding(lane), "Hythloth");
            PlaceSurfaceRoute(map, HythlothInteriorExit(lane), HythlothSurfaceLanding, "Hythloth");
        }
    }

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
            PlaceSurfaceRoute(map, outside, inside, "Shame");
            PlaceSurfaceRoute(map, inside, ShameSurfaceLanding, "Shame");
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

    private static void PlaceSurfaceRoute(Map map, Point3D from, Point3D to, string dungeon)
    {
        foreach (var item in map.GetItemsAt<Teleporter>(from))
        {
            if (item.GetType() != typeof(Teleporter) || item.Location != from || item.PointDest != to || item.MapDest != map)
                continue;
            SetSurfacePassageArt(item, dungeon);
            return;
        }

        var passage = new Teleporter(to, map);
        SetSurfacePassageArt(passage, dungeon);
        passage.MoveToWorld(from, map);
    }

    private static void SetSurfacePassageArt(Teleporter passage, string dungeon)
    {
        passage.Name = $"a passage through {dungeon}";
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
