// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Commands;
using Server.Items;
using Server.Json;
using Server.Mobiles;

namespace Sanctuary;

internal static class CampaignDungeonRoutes
{
    internal static void Install()
    {
        var map = Map.Trammel;
        var path = Path.Join(Core.BaseDirectory, "Data", "teleporters.json");
        foreach (var route in JsonConfig.Deserialize<TeleporterDefinition[]>(path))
        {
            if (route.Source.Map != map || route.Destination.Map != map)
                continue;
            var source = route.Source.Location;
            var dest = route.Destination.Location;
            string dungeon = DungeonAt(source, map) ?? DungeonAt(dest, map);
            if (dungeon == null)
                continue;

            var from = TownMarkets.Floor(map, source, [], false);
            var to = TownMarkets.Floor(map, dest, [], false);
            Place(map, from, to, dungeon);
            if (route.Back)
                Place(map, to, from, dungeon);
        }
    }

    private static string DungeonAt(Point3D point, Map map)
    {
        var region = Region.Find(point, map);
        foreach (var arena in CampaignArenas.All)
            if (region.IsPartOf(arena.Dungeon))
                return arena.Dungeon;
        return null;
    }

    private static void Place(Map map, Point3D from, Point3D to, string dungeon)
    {
        foreach (var existing in map.GetItemsAt<CampaignDungeonStair>(from))
            if (existing.Location == from && existing.PointDest == to)
                return;
        new CampaignDungeonStair(to) { Name = $"a passage through {dungeon}" }.MoveToWorld(from, map);
    }
}

[SerializationGenerator(0)]
public partial class CampaignDungeonStair : Teleporter
{
    internal CampaignDungeonStair(Point3D destination) : base(destination, Map.Trammel)
    {
        ItemID = 0x375A;
        Visible = true;
    }

    public override bool OnMoveOver(Mobile mobile) => true;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile && CanDoubleClick(from) && CanTeleport(from))
            StartTeleport(from);
    }
}
