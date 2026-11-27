// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Collections;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary;

public static class NewHavenPopulation
{
    internal static readonly Point3D MineDoor = new(3579, 2603, 0);
    internal static readonly Point3D MineArrival = new(5993, 341, 0);
    internal static readonly Point3D MineExit = new(5993, 343, 0);
    internal static readonly Point3D MineReturn = new(3579, 2605, 0);
    internal static readonly Rectangle2D MineShaft = new(3576, 2596, 4, 4);
    internal static readonly Point3D MineShaftRope = new(3578, 2599, 0);
    internal static readonly Point3D MountainMineMouth = new(3508, 2777, 26);
    internal static readonly Point3D MountainMineArrival = new(5912, 355, 0);
    internal static readonly Point3D MountainCamp = new(3508, 2742, 0);
    internal static readonly Point3D RopeDown = new(5994, 319, 0);
    internal static readonly Point3D RopeUp = new(5953, 319, 0);
    internal static readonly Point3D IslandGate = new(3450, 2677, 25);
    internal static readonly Point3D BankCounter = new(3484, 2570, 20);
    internal static readonly Rectangle2D MineArea = new(5903, 268, 100, 110);
    // Fixed encounter spawners in existing worlds, including both levels of the mine.
    private static readonly HashSet<Guid> _retiredEncounters =
    [
        new("155b8aa5-0ee7-43c1-9359-d2b060968856"),
        new("ce99dc95-aa4b-4931-a1d6-00b0f8b77339"),
        new("a1f9d010-fcfc-5f35-b021-95bfcb671328"),
        new("94641133-a96a-50c5-b7b0-5a718014fa43"),
        new("491ef282-24bc-5912-88dc-ccd35a2b9db2"),
        new("cf0f9847-c10b-500c-8998-93b7c8fdb978"),
        new("826eb7a6-3c48-5f12-aced-b57357c999c4"),
        new("68002c83-a051-515a-b5fb-4850c65cc839"),
        new("aa3b3a89-d677-5123-b34f-13eaa6bdd80d"),
        new("411f77fd-bbc9-596f-b14e-31587ec650f2"),
        new("567ec576-7949-5571-a961-f01bdf1457d0"),
        new("5591ad6e-5c72-519d-b312-ab6bb7e2f99b"),
        new("abfdebb1-45f4-5968-a88a-4bf2fd11a3a1"),
        new("9c691ae5-1bbb-5514-a2aa-0d1deaa47bcc"),
        new("71373eac-f629-5af4-99c0-a0f65367b038"),
        new("f77dd3f7-437d-592f-9178-462089f598d1"),
        new("99effd66-ab6f-5bb4-8dfb-34ac4557033d"),
        new("4b0dab98-658c-50e4-9907-1f5a3e086cb6"),
        new("78b66db0-f0ad-5b98-a8b4-2b2a01ad92c4"),
        new("21619f7b-cfac-56ef-b2c9-32b5747e5ae5"),
        new("b18601f3-32cc-5378-be21-b77045b7bf9c"),
        new("f3736ef1-7ec1-5225-84e9-4c931475d434"),
        new("d853152d-3970-5cab-b2a8-8ef6e51e0dcd"),
        new("064d6448-494a-55f1-b9f5-bf326fcb9278"),
        new("e42339ac-8f2b-54e4-a70e-b3c6e332e3f2"),
    ];

    private const int MineLandingGraphic = 0x051B;
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(NewHavenPopulation));
    private static PMList[] _publicWorlds;
    private static PMList[] PublicWorlds => _publicWorlds ??=
        [new(PMList.Trammel.Number, PMList.Trammel.SelNumber, Map.Trammel, PMList.Trammel.Entries, "Britannia")];
    private static PMList[] _unlockedWorlds;
    private static PMList[] UnlockedWorlds => _unlockedWorlds ??=
        [PublicWorlds[0], PMList.Ilshenar, PMList.Malas, PMList.Tokuno, PMList.TerMur];

    public static void Configure()
    {
        EventSink.ServerStarted += Install;
        ClassMapHooks.LocationChanged += RecordMineVisit;
        ClassMapHooks.PublicGateAllowed = CanUseGate;
        ClassMapHooks.PublicGateWorlds = from => from is PlayerMobile player && BritanniaCampaign.Complete(player)
            ? UnlockedWorlds : PublicWorlds;
        ClassMapHooks.PublicGateTraveled = (gate, mobile) =>
        {
            if (IsIslandGate(gate) && mobile is PlayerMobile player && ClassCreation.Profiles.RecordNewHavenDeparture(player.Serial))
                Iolo.OnNewHavenDeparture(player);
        };
    }

    private static void RecordMineVisit(Mobile mobile)
    {
        if (mobile is PlayerMobile { Alive: true } && mobile.Map == Map.Trammel && MineArea.Contains(mobile.Location))
            ClassCreation.Profiles.RecordHavenMineVisit(mobile.Serial);
    }

    internal static void Install()
    {
        var map = Map.Trammel;
        InstallDecorations(map, "haven-decor.cfg", "Haven decorations");
        InstallDecorations(map, "haven-doors.cfg", "Haven doors");
        int retired = RetireEncounters();
        int created = GraveyardPopulation.InstallSpawners("new-haven-encounters.json", relocate: true);
        foreach (var mobile in World.Mobiles.Values)
            if (mobile is HavenEncounter encounter)
                encounter.UpdateSavedCostume();
        // Keep the signed road entrance available from outside the stock wall.
        for (int offset = -1; offset <= 1; offset++)
        {
            var tile = new Point3D(MineDoor.X + offset, MineDoor.Y, MineDoor.Z);
            PlaceStatic(map, tile, MineLandingGraphic);
            PlacePassage(map, tile, MineArrival);
        }
        // The black shaft itself is impassable. Use the walkable cave floor along its rim.
        InstallMineShaft(map);
        InstallMountainMine(map);
        PlaceCampMiners(map);
        PlaceStatic(map, MineExit, MineLandingGraphic);
        PlacePassage(map, MineExit, MineReturn);
        PlaceRope(map, RopeDown, RopeUp, "a rope leading down");
        PlaceRope(map, RopeUp, RopeDown, "a rope leading up");

        PlaceSign(map, new Point3D(3578, 2603, 0), "New Haven Mine");
        PlaceSign(map, new Point3D(5994, 343, 0), "Exit to New Haven");
        PlaceSign(map, new Point3D(3598, 2463, 6), "New Haven Graveyard");
        PlaceSign(map, new Point3D(3460, 2558, 36), "New Haven Healer");
        PlaceHealer(map);
        PlaceBanker(map);
        RemoveOldGraveyardProps(map);
        PlaceStatic(map, new Point3D(3721, 2618, 20), 0x0FAC);
        PlaceStatic(map, new Point3D(3720, 2618, 20), 0x0E3C);
        InstallPublicGates();
        _logger.Information("New Haven: installed {Count} population spawners and retired {Retired} encounter spawners", created, retired);
    }

    private static int RetireEncounters()
    {
        using var retired = PooledRefList<BaseSpawner>.Create();
        foreach (var item in World.Items.Values)
            if (item is BaseSpawner spawner && _retiredEncounters.Contains(spawner.Guid))
                retired.Add(spawner);
        foreach (var spawner in retired)
            spawner.Delete();
        return retired.Count;
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

    private static void PlaceBanker(Map map)
    {
        foreach (var banker in map.GetMobilesInRange<Banker>(BankCounter, 4))
            if (banker is { Alive: true, Deleted: false })
                return;

        new Banker { CantWalk = true, Direction = Direction.East }.MoveToWorld(BankCounter, map);
    }

    private static void PlaceCampMiners(Map map)
    {
        for (int i = 0; i < 2; i++)
        {
            bool second = i == 1;
            bool present = false;
            foreach (var miner in map.GetMobilesInRange<MineCampMiner>(MountainCamp, 8))
                if (!miner.Deleted && miner.Female == second)
                {
                    present = true;
                    break;
                }
            if (present)
                continue;

            var seat = second ? new Point3D(3509, 2744, 0) : new Point3D(3506, 2743, 0);
            var point = TownMarkets.Floor(map, seat, [], true);
            new MineCampMiner(second).MoveToWorld(point, map);
        }
    }

    private static bool IsIslandGate(PublicMoongate gate) =>
        gate.Map == Map.Trammel && gate.X == IslandGate.X && gate.Y == IslandGate.Y;

    private static bool CanUseGate(PublicMoongate gate, Mobile from)
    {
        if (!IsIslandGate(gate)
            || from is not PlayerMobile { AccessLevel: AccessLevel.Player } player
            || !ClassCreation.Profiles.TryGet(player.Serial, out _)
            || ClassCreation.Profiles.NewHavenGateUnlocked(player.Serial)
            || BritanniaCampaign.Complete(player))
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
        foreach (var list in PublicWorlds)
        {
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
            if (item is not InteractionTeleporter && item.Z == at.Z && item.PointDest == destination
                && (item.MapDest == null || item.MapDest == map) && item.Active)
                return true;
        return false;
    }

    internal static void PlacePassage(Map map, Point3D at, Point3D destination)
    {
        if (!HasPassage(map, at, destination))
            new Teleporter(destination, map).MoveToWorld(at, map);
    }

    private static void InstallMineShaft(Map map)
    {
        for (int x = MineShaft.X; x < MineShaft.End.X; x++)
        {
            PlacePassage(map, new Point3D(x, MineShaft.Y - 1, 0), MineArrival);
            PlacePassage(map, new Point3D(x, MineShaft.End.Y, 0), MineArrival);
        }
        for (int y = MineShaft.Y; y < MineShaft.End.Y; y++)
        {
            PlacePassage(map, new Point3D(MineShaft.X - 1, y, 0), MineArrival);
            PlacePassage(map, new Point3D(MineShaft.End.X, y, 0), MineArrival);
        }
        PlaceRope(map, MineShaftRope, MineArrival, "a rope leading into New Haven Mine");
    }

    private static void InstallMountainMine(Map map)
    {
        // Restore the stock Trammel passage without requiring the shard-wide TelGen command.
        PlacePassage(map, MountainMineMouth, MountainMineArrival);
        int adjacentY = MountainMineMouth.Y + 1;
        var adjacent = new Point3D(MountainMineMouth.X, adjacentY, map.GetAverageZ(MountainMineMouth.X, adjacentY));
        PlacePassage(map, adjacent, MountainMineArrival);
        PlacePassage(map, MountainMineArrival, MountainMineMouth);
        PlaceStatic(map, MountainMineArrival, MineLandingGraphic);
        PlaceSign(map, new Point3D(5912, 354, 0), "Exit to the mountain trail");
    }

    internal static void PlaceRope(Map map, Point3D at, Point3D destination, string name)
    {
        // Keep the usable end above ground clutter while both landings remain at floor height.
        var handle = new Point3D(at.X, at.Y, at.Z + 12);
        foreach (var rope in map.GetItemsInRange<InteractionTeleporter>(at, 0))
        {
            if (rope.PointDest != destination || rope.Name != name)
                continue;
            if (rope.Z != handle.Z)
                rope.Location = handle;
            return;
        }
        new InteractionTeleporter(destination, map) { Name = name, ItemID = 0x14FA }.MoveToWorld(handle, map);
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
