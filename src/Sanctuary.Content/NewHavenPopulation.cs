// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using Server;
using Server.Commands;
using Server.Items;
using Server.Logging;
using Server.Maps;
using Server.Mobiles;

namespace Sanctuary;

public static class NewHavenPopulation
{
    internal static readonly Point3D MineDoor = new(3579, 2603, 0);
    internal static readonly Point3D MineArrival = new(5993, 341, 0);
    internal static readonly Point3D MineExit = new(5993, 343, 0);
    internal static readonly Point3D MineReturn = new(3579, 2605, 0);
    internal static readonly Point3D RopeDown = new(5994, 319, 0);
    internal static readonly Point3D RopeUp = new(5953, 319, 0);
    internal static readonly Point3D IslandGate = new(3450, 2677, 25);
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(NewHavenPopulation));

    public static void Configure()
    {
        EventSink.ServerStarted += Install;
        ClassMapHooks.PublicGateAllowed = CanUseGate;
        ClassMapHooks.PublicGateTraveled = (gate, mobile) =>
        {
            if (IsIslandGate(gate) && mobile is PlayerMobile player)
                ClassCreation.Profiles.RecordNewHavenDeparture(player.Serial);
        };
    }

    internal static void Install()
    {
        var map = Map.Trammel;
        InstallDecorations(map, "haven-decor.cfg", "Haven decorations");
        InstallDecorations(map, "haven-doors.cfg", "Haven doors");
        int created = GraveyardPopulation.InstallSpawners("new-haven-encounters.json", relocate: true);
        foreach (var mobile in World.Mobiles.Values)
            if (mobile is HavenEncounter encounter)
                encounter.UpdateSavedCostume();
        PlacePassage(map, MineDoor, MineArrival);
        PlacePassage(map, MineExit, MineReturn);
        PlaceRope(map, RopeDown, RopeUp, "a rope leading down");
        PlaceRope(map, RopeUp, RopeDown, "a rope leading up");

        PlaceSign(map, new Point3D(3578, 2603, 0), "New Haven Mine");
        PlaceSign(map, new Point3D(5994, 343, 0), "Exit to New Haven");
        PlaceSign(map, new Point3D(3598, 2463, 6), "New Haven Graveyard");
        PlaceSign(map, new Point3D(3460, 2558, 36), "New Haven Healer");
        PlaceHealer(map);
        RemoveOldGraveyardProps(map);
        PlaceStatic(map, new Point3D(3721, 2618, 20), 0x0FAC);
        PlaceStatic(map, new Point3D(3720, 2618, 20), 0x0E3C);
        InstallPublicGates();
        _logger.Information("New Haven: installed {Count} population spawners", created);
    }

    private static void InstallDecorations(Map map, string file, string decoration)
    {
        string marker = $"Sanctuary: {decoration} v1";
        foreach (var item in map.GetItemsAt<Static>(ClassCreation.Arrival))
            if (item.Name == marker)
                return;

        int count = 0;
        string path = Path.Join(Core.BaseDirectory, "Data", "Sanctuary", file);
        foreach (var list in DecorationList.ReadAll(path))
            count += list.Generate([map]);
        new Static(0x1B72) { Name = marker, Visible = false }.MoveToWorld(ClassCreation.Arrival, map);
        _logger.Information("New Haven: installed {Count} objects from {File}", count, file);
    }

    private static void PlaceHealer(Map map)
    {
        foreach (var healer in map.GetMobilesInRange<BaseHealer>(NewHavenHealer.House, 3))
            if (healer.Alive && !healer.Deleted)
                return;
        new NewHavenHealer().MoveToWorld(NewHavenHealer.House, map);
    }

    private static bool IsIslandGate(PublicMoongate gate) =>
        gate.Map == Map.Trammel && gate.X == IslandGate.X && gate.Y == IslandGate.Y;

    private static bool CanUseGate(PublicMoongate gate, Mobile from)
    {
        if (!IsIslandGate(gate)
            || from is not PlayerMobile { AccessLevel: AccessLevel.Player } player
            || !ClassCreation.Profiles.TryGet(player.Serial, out _)
            || ClassCreation.Profiles.NewHavenGateUnlocked(player.Serial))
            return true;

        player.SendMessage("You’re not sure it’s wise to leave yet. Perhaps you should speak with Iolo at the inn.");
        return false;
    }

    private static void RemoveOldGraveyardProps(Map map)
    {
        foreach (var item in map.GetItemsAt<BaseSign>(new Point3D(3424, 2651, 47)))
            if (item.Name == "New Haven Graveyard")
            {
                item.Delete();
                break;
            }
        foreach (var (location, graphic) in new (Point3D, int)[]
        {
            (new(3416, 2650, 48), 0x0ED4), (new(3414, 2653, 50), 0x0ED5), (new(3417, 2656, 48), 0x0ED7)
        })
            foreach (var item in map.GetItemsAt<Static>(location))
                if (item.ItemID == graphic)
                {
                    item.Delete();
                    break;
                }
    }

    private static void InstallPublicGates()
    {
        // The native destination menu assumes each listed gate has a reciprocal gate at its destination.
        var enabled = ExpansionInfo.CoreExpansion.MapSelectionFlags;
        foreach (var list in new[] { PMList.Trammel, PMList.Felucca, PMList.Ilshenar, PMList.Malas, PMList.Tokuno })
        {
            if (!enabled.Includes(list.Map.ToSelectionFlag()))
                continue;
            foreach (var entry in list.Entries)
            {
                bool exists = false;
                foreach (var gate in list.Map.GetItemsInRange<PublicMoongate>(entry.Location, 0))
                {
                    exists = true;
                    break;
                }
                if (!exists)
                    new PublicMoongate { Hue = entry.Number == 1060642 ? 0x497 : 0 }.MoveToWorld(entry.Location, list.Map);
            }
        }
    }

    private static bool HasPassage(Map map, Point3D at, Point3D destination)
    {
        foreach (var item in map.GetItemsInRange<Teleporter>(at, 0))
            if (item.Z == at.Z && item.PointDest == destination)
                return true;
        return false;
    }

    private static void PlacePassage(Map map, Point3D at, Point3D destination)
    {
        if (!HasPassage(map, at, destination))
            new Teleporter(destination, map).MoveToWorld(at, map);
    }

    private static void PlaceRope(Map map, Point3D at, Point3D destination, string name)
    {
        if (!HasPassage(map, at, destination))
            new InteractionTeleporter(destination, map) { Name = name, ItemID = 0x14FA }.MoveToWorld(at, map);
    }

    private static void PlaceSign(Map map, Point3D at, string name)
    {
        foreach (var sign in map.GetItemsInRange<BaseSign>(at, 1))
            if (sign.Name == name)
                return;
        new Sign(SignType.LightWoodenPost, SignFacing.North) { Name = name }.MoveToWorld(at, map);
    }

    private static void PlaceStatic(Map map, Point3D at, int itemId)
    {
        foreach (var item in map.GetItemsInRange<Item>(at, 0))
            if (item.Z == at.Z && item.ItemID == itemId)
                return;
        new Static(itemId).MoveToWorld(at, map);
    }
}
