// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Engines.Spawners;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary;

public static class CoveWorld
{
    private static readonly Guid _oldCrier = new("81d50c82-2e5a-41d8-b572-f50a66302d6e");
    private static readonly Guid _oldHealers = new("3a2fbed0-d494-4930-8238-a74b2489e23a");
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CoveWorld));
    private static readonly Point3D _bankCounter = new(2227, 1197, 0);
    private static readonly (string Name, Point3D Post, bool Female, int Hue)[] _residents =
    [
        ("fisher", new(2255, 1177, -2), false, 0x045C),
        ("peasant", new(2238, 1210, 0), true, 0x0491),
        ("hunter", new(2268, 1202, 0), true, 0x0591),
        ("trapper", new(2256, 1229, 0), false, 0x0590)
    ];
    private static readonly (int X, int Y, int Graphic)[] _oldCampProps =
    [
        (0, 0, 0x0E31),
        (-1, 0, 0x1772), (0, 1, 0x1772), (1, 0, 0x1772), (0, -1, 0x1772),
        (-2, -1, 0x11F8), (2, 1, 0x11F8), (-2, 2, 0x1079), (2, -2, 0x1079)
    ];
    private static readonly (Point3D Center, int[] OldHeights)[] _camps =
    [
        (new(2427, 1179, 0), [-5, 0, 0, 0, 0, 0, 0, 0, 0]),
        (new(2455, 1212, 7), [2, 10, 7, 5, 7, 10, 5, 10, 5]),
        (new(2553, 1240, 0), [-5, 0, 0, 0, 0, 0, 0, 0, 0]),
        (new(2635, 1238, 0), [-5, 0, 0, 0, 0, 0, 0, 2, 0])
    ];

    public static void Configure() => EventSink.ServerStarted += Install;

    internal static bool ReplacedResident(Guid id) => id == _oldCrier || id == _oldHealers;

    internal static bool MobileVisible(PlayerMobile viewer, Mobile mobile) => mobile switch
    {
        CoveGnaw => CoveGnaw.FriendlyTo(viewer),
        CoveEncounter { PentagramImp: true } => CoveGnaw.HostileTo(viewer),
        _ => true
    };

    private static void Install()
    {
        var map = Map.Trammel;
        foreach (var spawner in World.Items.Values.OfType<BaseSpawner>().Where(s => ReplacedResident(s.Guid)).ToArray())
            spawner.Delete();

        RetireTrainers(map);
        InstallCamps(map);
        GraveyardPopulation.InstallSpawners("cove-spawns.json");
        GraveyardPopulation.InstallSpawners("cove-fort-spawns.json", relocate: true);

        Place(new(2285, 1208, 0), () => new CoveGuard(true), mobile => mobile.Name == "Thomas");
        Place(new(2285, 1211, 0), () => new CoveGuard(false), mobile => mobile.Name == "Rowan");
        Place(new(2237, 1186, 0), () => new MayorJaana());
        Place(CoveFerry.Dock, () => new CoveFerry());
        Place(new(2254, 1207, 0), () => new CoveCrier());
        Place(new(2245, 1226, 0), () => new CoveHealer());
        Place(CoveGnaw.Pentagram, () => new CoveGnaw());
        Place(_bankCounter, () => new Banker { CantWalk = true, Direction = Direction.East },
            banker => banker.Map == map && banker.InRange(_bankCounter, 3) && banker.Z == _bankCounter.Z);
        foreach (var (name, post, female, hue) in _residents)
            Place(post, () => new CoveResident(name, post, female, hue), resident => resident.Name == name);

        var table = new Point3D(CoveClue.X, CoveClue.Y, CoveClue.Z);
        if (!World.Items.Values.OfType<CoveMessenger>().Any(item => !item.Deleted))
            new CoveMessenger().MoveToWorld(table, map);

        var north = EntranceDoor(new(2247, 1229, 0), DoorFacing.NorthCW);
        var south = EntranceDoor(new(2247, 1230, 0), DoorFacing.SouthCW);
        north.Link = south;
        south.Link = north;

        BaseDoor EntranceDoor(Point3D point, DoorFacing facing)
        {
            foreach (var door in map.GetItemsInRange<BaseDoor>(point, 1))
            {
                var offset = door.Open ? door.Offset : default;
                if (new Point3D(door.X - offset.X, door.Y - offset.Y, door.Z - offset.Z) == point)
                    return door;
            }
            var added = new DarkWoodDoor(facing);
            added.MoveToWorld(point, map);
            return added;
        }

        void Place<T>(Point3D point, Func<T> create, Predicate<T> match = null) where T : Mobile
        {
            if (!World.Mobiles.Values.OfType<T>().Any(mobile => !mobile.Deleted && (match?.Invoke(mobile) ?? true)))
                create().MoveToWorld(point, map);
        }
    }

    private static void RetireTrainers(Map map)
    {
        var profiles = TownMarkets.Profiles;
        foreach (var market in profiles.All.Where(market => market.Site?.StartsWith("Trammel/Cove/", StringComparison.Ordinal) == true
            && TownMarkets.Skill(market.Role).HasValue).ToArray())
        {
            if (World.FindItem(market.Station) is TownCraftStation station)
                station.Delete();
            if (World.FindItem(market.Forge) is TownCraftStation forge)
                forge.Delete();
            if (World.FindMobile(market.Vendor) is ProfessionInstructor trainer)
                trainer.Delete();
        }
        profiles.Prune();

        // Previous world layouts put this workshop in the healer's house and then east of it.
        using var leftBehind = PooledRefList<TownCraftStation>.Create();
        foreach (var station in map.GetItemsInRange<TownCraftStation>(new Point3D(2252, 1231, 0), 10))
            if (!station.Movable && station.Parent == null && station.Z == 0)
                leftBehind.Add(station);
        foreach (var station in leftBehind)
            station.Delete();
    }

    private static void InstallCamps(Map map)
    {
        var placed = World.Items.Values.OfType<OrcCamp>().Where(camp => camp.Map == map).Select(camp => camp.Location).ToHashSet();
        foreach (var (center, oldHeights) in _camps)
        {
            if (!placed.Contains(center) && !OrcCamp.TryPlace(map, center, out _, out var reason))
            {
                _logger.Warning("Could not place the Cove orc camp at {Center}: {Reason}", center, reason);
                continue;
            }

            using var retired = PooledRefList<Static>.Create();
            for (int i = 0; i < _oldCampProps.Length; i++)
            {
                var (x, y, graphic) = _oldCampProps[i];
                var point = new Point3D(center.X + x, center.Y + y, oldHeights[i]);
                foreach (var prop in map.GetItemsAt<Static>(point))
                    if (prop.GetType() == typeof(Static) && !prop.Movable && prop.Hue == 0
                        && prop.Location == point && prop.ItemID == graphic)
                        retired.Add(prop);
            }
            foreach (var prop in retired)
                prop.Delete();
        }
    }
}

[SerializationGenerator(0)]
public partial class CoveResident : BaseCreature
{
    internal CoveResident(string name, Point3D home, bool female, int hue) : base(AIType.AI_Vendor, FightMode.None, 6, 1)
    {
        Name = name;
        Female = female;
        Body = female ? 0x191 : 0x190;
        Hue = 0x83EA;
        HairItemID = female ? 0x203D : 0x203B;
        HairHue = 0x045A;
        Blessed = true;
        Home = home;
        RangeHome = 1;
        CantWalk = name == "fisher";
        AddItem(new FancyShirt(0x03E5));
        AddItem(new Tunic(hue));
        AddItem(new LongPants(0x0455));
        AddItem(new Boots(0x045B));
        if (name == "fisher")
            AddItem(new FishingPole());
    }
}

[SerializationGenerator(0)]
public partial class CoveHealer : Healer
{
    [Constructible]
    public CoveHealer()
    {
        Name = "Brencis";
        CantWalk = true;
        Direction = Direction.East;
    }
}
