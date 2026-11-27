// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Logging;

namespace Sanctuary;

public static class BritanniaFrontier
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(BritanniaFrontier));

    public static void Configure() => EventSink.ServerStarted += Install;

    public static void Initialize()
    {
        int end = WorldDecorationGraphics.DragonHoardGroundFirst + WorldDecorationGraphics.DragonHoardGroundCount;
        var hoard = new ItemData("a dragon's hoard", TileFlag.Background, 255, 0, 0, 0, 0, 0);
        for (int graphic = WorldDecorationGraphics.DragonHoard; graphic < end && graphic <= TileData.MaxItemValue; graphic++)
            TileData.ItemTable[graphic] = hoard;
    }

    private static void Install()
    {
        var map = Map.Trammel;
        int scenery = 0;
        string path = Path.Join(Core.BaseDirectory, "Data", "Sanctuary", "britannia-frontier-decor.cfg");
        foreach (var list in DecorationList.ReadAll(path))
            scenery += list.Generate([map]);
        scenery += DragonHoard.Install(map, new(1445, 902, 0));
        RetireOldHoard(map);

        InstallDespise(map);
        DespiseMushrooms.Install(map);
        DespiseStory.EnsureClue(map);
        int retiredDespiseSpawns = DeepDespiseSpawns.RetirePresets();
        int despiseSpawns = GraveyardPopulation.InstallSpawners("despise-spawns.json");
        EnsureRoamingEttinUnique(map);
        _logger.Information("Despise: installed {Count} population spawners and retired {Retired} deep spawners", despiseSpawns, retiredDespiseSpawns);

        InstallSewerLadders(map);

        // The old Magery pad is on the mountainside. Trigger on the walkable edge of the approach.
        for (int offset = -1; offset <= 1; offset++)
        {
            NewHavenPopulation.PlacePassage(map, new(1361 + offset, 886, 0), new(5166, 244, 15));
            NewHavenPopulation.PlacePassage(map, new(5166 + offset, 246, 15), new(1361, 888, 0));
        }

        int retiredWindSpawns = WindSpawns.RetirePresets();
        WindTown.Install();
        _logger.Information("Wind: retired {Retired} preset monster spawners", retiredWindSpawns);
        int spawners = GraveyardPopulation.InstallSpawners("britannia-frontier-spawns.json", relocate: true, refreshCounts: true);
        spawners += GraveyardPopulation.InstallSpawners("southern-britannia-spawns.json", relocate: true, refreshCounts: true);
        spawners += GraveyardPopulation.InstallSpawners("compassion-frontier-spawns.json");
        spawners += GraveyardPopulation.InstallSpawners("vesper-graveyard-spawns.json");
        spawners += GraveyardPopulation.InstallSpawners("vesper-coast-spawns.json");
        spawners += GraveyardPopulation.InstallSpawners("west-orc-fort-spawns.json", relocate: true);
        CovetousPassages.Install(map);
        Celeste.Install(map);
        spawners += GraveyardPopulation.InstallSpawners("covetous-spawns.json");
        HedgeMazeStory.Install(map);
        _logger.Information("Britannia frontier: installed {Scenery} decorations and {Spawners} spawners", scenery, spawners);
    }

    private static void RetireOldHoard(Map map)
    {
        // Replace the stock piles and the previous anchor for the smaller hoard.
        (Point3D Location, int Graphic)[] placements =
        [
            (new(1439, 898, 0), 0x0EEF), (new(1440, 899, 0), 0x0EEF), (new(1441, 898, 0), 0x0EEF),
            (new(1440, 898, 0), 0x1BEB), (new(1441, 899, 0), 0x1BEB),
            (new(1442, 898, 0), WorldDecorationGraphics.DragonHoard)
        ];
        using var old = PooledRefList<Static>.Create();
        foreach (var (location, graphic) in placements)
            foreach (var item in map.GetItemsAt<Static>(location))
                if (item.GetType() == typeof(Static) && item.Location == location && item.ItemID == graphic)
                    old.Add(item);
        foreach (var item in old)
            item.Delete();
    }

    private static void InstallSewerLadders(Map map)
    {
        for (int lane = 0; lane < 2; lane++)
        {
            var britain = new Point3D(1491, 1640 + lane * 2, 24);
            var stockLanding = new Point3D(6032, 1499 + lane * 2, 31);
            RetireStockPassage(map, britain, stockLanding);
            RetireStockPassage(map, stockLanding, britain);

            // The tunnel floor below the steps is clear of the ladder and stair collision.
            int x = 6031 + lane;
            PlaceSewerLadder(map, britain, new(x, 1494, 5), "ladder to the Britain sewers");
            PlaceSewerLadder(map, new(x, 1495, 7), britain, "ladder to Britain");
        }
    }

    private static void PlaceSewerLadder(Map map, Point3D from, Point3D to, string name)
    {
        foreach (var ladder in map.GetItemsAt<BritainSewerLadder>(from))
            if (ladder.Location == from && ladder.PointDest == to && ladder.MapDest == map && ladder.Active)
                return;

        new BritainSewerLadder(to, map) { Name = name }.MoveToWorld(from, map);
    }

    private static void InstallDespise(Map map)
    {
        var deepLanding = new Point3D(5407, 859, 45);
        var middleLanding = new Point3D(5475, 750, 5);

        // The three lanes of each staircase reach the actual stair surface at different heights from the entry hall.
        for (int offset = 0; offset < 3; offset++)
        {
            int outsideY = 1080 + offset, caveY = 630 + offset;
            var threshold = new Point3D(1296, outsideY, 0);
            var exit = new Point3D(5588, caveY, 30);
            RetireStockPassage(map, threshold, exit);
            RetireStockPassage(map, exit, threshold);
            NewHavenPopulation.PlacePassage(map, threshold, new(5586, caveY, 30));
            NewHavenPopulation.PlacePassage(map, new(1297, outsideY, 0), new(5586, caveY, 30));
            NewHavenPopulation.PlacePassage(map, exit, new(1299, outsideY, 0));

            int upperY = 569 + offset, northStairY = 628 + offset;
            int lowerY = 672 + offset, southStairY = 632 + offset;
            var upper = new Point3D(5501, upperY, 59);
            var lower = new Point3D(5518, lowerY, 20);
            var northFoyer = new Point3D(5577, northStairY, 30);
            var southFoyer = new Point3D(5578, 633 + offset, 30);

            RetireStockPassage(map, new(5573, northStairY, 42), new(5503, upperY, 51));
            RetireStockPassage(map, new(5504, upperY, 46), new(5574, northStairY, 37));
            RetireStockPassage(map, new(5572, southStairY, 17), new(5521, lowerY, 27));
            RetireStockPassage(map, new(5522, lowerY, 32), new(5573, southStairY, 22));

            NewHavenPopulation.PlacePassage(map, new(5575, northStairY, 32), upper);
            NewHavenPopulation.PlacePassage(map, new(5502, upperY, 56), northFoyer);
            NewHavenPopulation.PlacePassage(map, new(5504, upperY, 46), northFoyer);
            NewHavenPopulation.PlacePassage(map, new(5575, southStairY, 30), lower);
            NewHavenPopulation.PlacePassage(map, new(5574, southStairY, 27), lower);
            NewHavenPopulation.PlacePassage(map, new(5520, lowerY, 22), southFoyer);
            NewHavenPopulation.PlacePassage(map, new(5522, lowerY, 32), southFoyer);

            NewHavenPopulation.PlacePassage(map, new(5472 + offset, 753, 10), deepLanding);
            RetireStockPassage(map, new(5408, 858 + offset, 45), middleLanding);
        }
        NewHavenPopulation.PlacePassage(map, new(5475, 752, 5), deepLanding);
        NewHavenPopulation.PlacePassage(map, new(5474, 752, 10), deepLanding);
        NewHavenPopulation.PlacePassage(map, new(5472, 752, 20), deepLanding);

        var sparkleLocation = new Point3D(5408, 859, 47);
        foreach (var passage in map.GetItemsAt<InteractionTeleporter>(sparkleLocation))
            if (passage.Location == sparkleLocation && passage.PointDest == middleLanding && passage.MapDest == map)
                return;

        new InteractionTeleporter(middleLanding, map)
        {
            ItemID = 0x375A,
            Name = "a sparkle to Despise level 2"
        }.MoveToWorld(sparkleLocation, map);
    }

    private static void EnsureRoamingEttinUnique(Map map)
    {
        var location = new Point3D(5487, 689, 20);
        foreach (var spawner in map.GetItemsAt<DespiseEttinSpawner>(location))
            if (spawner.Location == location)
                return;
        var roaming = new DespiseEttinSpawner();
        roaming.MoveToWorld(location, map);
        roaming.Respawn();
    }

    private static void RetireStockPassage(Map map, Point3D from, Point3D to)
    {
        using var old = PooledRefList<Teleporter>.Create();
        foreach (var teleporter in map.GetItemsAt<Teleporter>(from))
            if (teleporter.GetType() == typeof(Teleporter) && teleporter.Z == from.Z && teleporter.PointDest == to
                && (teleporter.MapDest == null || teleporter.MapDest == map))
                old.Add(teleporter);
        foreach (var teleporter in old)
            teleporter.Delete();
    }
}

[SerializationGenerator(0)]
public partial class DespiseEttinSpawner : Spawner
{
    private static readonly Point3D[] _positions =
    [
        new(5505, 662, 21), new(5487, 689, 20), new(5464, 708, 17),
        new(5455, 686, 21), new(5415, 684, 20), new(5394, 695, 15),
        new(5422, 730, 7), new(5468, 741, 5), new(5503, 711, 15)
    ];

    [Constructible]
    public DespiseEttinSpawner() : base(1, System.TimeSpan.FromMinutes(5), System.TimeSpan.FromMinutes(7))
    {
        Name = "Despise wandering ettin warlord";
        SpawnLocationIsHome = true;
        WalkingRange = 3;
        AddEntry(nameof(FrontierEncounter), dotimer: false, parameters: "despise-ettin 22 1");
    }

    public override Point3D GetSpawnPosition(ISpawnable spawned, Map map)
    {
        int start = Utility.Random(_positions.Length);
        for (int offset = 0; offset < _positions.Length; offset++)
        {
            var point = _positions[(start + offset) % _positions.Length];
            if (map.CanSpawnMobile(point))
                return point;
        }
        return base.GetSpawnPosition(spawned, map);
    }

    protected override void OnConfigureSpawned(SpawnerEntry entry, ISpawnable spawned)
    {
        if (spawned is FrontierEncounter encounter)
            encounter.Name = "a roaming ettin warlord";
    }
}

[SerializationGenerator(0)]
public partial class FrontierSentry : ConversationNpc
{
    private static readonly ScriptedDialogue.Node[] _warning =
    [
        new("Keep thy wits about thee, traveller. Ettins have made camp along the roads, and there are drakes in the mountain passes. Give their fires a wide berth.", [])
    ];

    [Constructible]
    public FrontierSentry(bool female)
    {
        Female = female;
        Body = female ? 0x191 : 0x190;
        Name = female ? "Mara" : "Duncan";
        Title = "the watch guard";
        Hue = 0x83EF;
        CantWalk = true;
        Direction = Direction.South;
        AddItem(new RingmailChest());
        AddItem(new Bascinet());
        AddItem(new Halberd());
        AddItem(new Boots());
    }

    internal override ushort Portrait => (byte)(Female ? NpcPortrait.WatchGuardMara : NpcPortrait.WatchGuardDuncan);
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _warning;
}
