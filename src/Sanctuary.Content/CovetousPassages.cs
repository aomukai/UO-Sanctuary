// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Items;
using Server.Mobiles;
using static Sanctuary.NewHavenPopulation;

namespace Sanctuary;

internal static class CovetousPassages
{
    internal static void Install(Map map)
    {
        for (int lane = -1; lane <= 1; lane++)
        {
            // Each level has a mountainside mouth. Leave a tile between each arrival and its return trigger.
            PlacePassage(map, new(2499 + lane, 917, 0), new(5456, 1862, 0));
            PlacePassage(map, new(2421 + lane, 884, 0), new(5393 + lane, 1957, 0));
            PlaceExit(new(5393 + lane, 1958, 0), new(2421 + lane, 885, 0));

            PlacePassage(map, new(2385, 837 + lane, 0), new(5613, 1997 + lane, 0));
            PlaceExit(new(5614, 1997 + lane, 0), new(2386, 837 + lane, 0));
            PlacePassage(map, new(2456 + lane, 859, 0), new(5389 + lane, 2025, 0));
            PlaceExit(new(5389 + lane, 2026, 0), new(2456 + lane, 860, 0));

            PlacePassage(map, new(2545 + lane, 850, 0), new(5579 + lane, 1925, 0));
            PlaceExit(new(5579 + lane, 1926, 0), new(2545 + lane, 849, 0));

            GuardStockExit(new(5393 + lane, 1959, 0));
            GuardStockExit(new(5615, 1997 + lane, 0));
            GuardStockExit(new(5389 + lane, 2027, 0));
            GuardStockExit(new(5579 + lane, 1927, 0));
            GuardStockExit(new(5456 + lane, 1864, 0));

            // Buccaneer's Den portal. Use the foot of the arch on both sides.
            PlacePassage(map, new(2618 + lane, 976, 2), new(2724, 2133, 0));
            PlacePassage(map, new(2618 + lane, 978, 2), new(2724, 2133, 0));
            PlacePassage(map, new(2725, 2133 + lane, 0), new(2618, 979, 0));
            PlacePassage(map, new(2729, 2133 + lane, 0), new(2618, 979, 0));
        }

        PlacePassage(map, new(2501, 917, 0), new(5456, 1862, 0));
        PlaceExit(new(5455, 1863, 0), new(2498, 918, 0));
        PlaceExit(new(5456, 1863, 0), new(2499, 918, 0));

        void PlaceExit(Point3D at, Point3D destination)
        {
            using var old = PooledRefList<Teleporter>.Create();
            foreach (var portal in map.GetItemsAt<Teleporter>(at))
            {
                if (portal is CovetousExit && portal.Location == at && portal.PointDest == destination)
                    return;
                if (portal.GetType() == typeof(Teleporter) && Math.Abs(portal.Z - at.Z) <= 4
                    && Utility.InRange(portal.PointDest, destination, 4))
                    old.Add(portal);
            }
            foreach (var portal in old)
                portal.Delete();
            new CovetousExit(destination, map).MoveToWorld(at, map);
        }

        void GuardStockExit(Point3D at)
        {
            using var old = PooledRefList<Teleporter>.Create();
            foreach (var portal in map.GetItemsAt<Teleporter>(at))
                if (portal.GetType() == typeof(Teleporter) && portal.Location == at
                    && !Region.Find(portal.PointDest, portal.MapDest ?? map).IsPartOf("Covetous"))
                    old.Add(portal);
            foreach (var portal in old)
            {
                var replacement = new CovetousExit(portal.PointDest, portal.MapDest ?? map);
                portal.Delete();
                replacement.MoveToWorld(at, map);
            }
        }

        ReplaceStairs(new(5468, 1805, 2), new(5592, 1842, 0),
            deeper: new(5469, 1805, 0), deeperLanding: new(5592, 1844, 0),
            back: new(5592, 1843, 0), backLanding: new(5470, 1805, 0));
        ReplaceStairs(new(5552, 1807, 2), new(5557, 1824, 0),
            deeper: new(5552, 1808, 0), deeperLanding: new(5559, 1825, 0),
            back: new(5557, 1824, 0), backLanding: new(5552, 1809, 0));

        void ReplaceStairs(Point3D oldTop, Point3D oldBottom, Point3D deeper, Point3D deeperLanding,
            Point3D back, Point3D backLanding)
        {
            RemoveWalkTriggers(oldTop, oldBottom);
            RemoveWalkTriggers(oldBottom, oldTop);
            PlaceStairs(deeper, deeperLanding, "stairs deeper into Covetous");
            PlaceStairs(back, backLanding, "stairs back through Covetous");
        }

        void RemoveWalkTriggers(Point3D from, Point3D to)
        {
            using var obsolete = PooledRefList<Teleporter>.Create();
            foreach (var portal in map.GetItemsInRange<Teleporter>(from, 3))
                if (portal.GetType() == typeof(Teleporter) && Utility.InRange(portal.PointDest, to, 3)
                    && Math.Abs(portal.Z - from.Z) <= 15 && (portal.MapDest == null || portal.MapDest == map))
                    obsolete.Add(portal);
            foreach (var portal in obsolete)
                portal.Delete();
        }

        void PlaceStairs(Point3D at, Point3D destination, string name)
        {
            foreach (var portal in map.GetItemsAt<CovetousStairs>(at))
                if (portal.Location == at && portal.PointDest == destination)
                    return;
            new CovetousStairs(destination, map) { Name = name }.MoveToWorld(at, map);
        }
    }
}

[SerializationGenerator(0)]
public partial class CovetousExit : Teleporter
{
    internal CovetousExit(Point3D destination, Map map) : base(destination, map) { }

    internal static bool WaitingForCeleste(Teleporter portal, Mobile mobile) => mobile is PlayerMobile player
        && Celeste.StopDeparture(player, () => !portal.Deleted && portal.Active && player.Map == portal.Map
            && player.InRange(portal, 2) && Math.Abs(player.Z - portal.Z) < 16,
            () =>
            {
                if (portal.CanTeleport(player))
                    portal.StartTeleport(player);
            });

    public override bool OnMoveOver(Mobile mobile)
    {
        if (!Active || !CanTeleport(mobile))
            return true;
        if (!WaitingForCeleste(this, mobile))
            StartTeleport(mobile);
        return false;
    }
}

[SerializationGenerator(0)]
public partial class CovetousStairs : Teleporter
{
    internal CovetousStairs(Point3D destination, Map map) : base(destination, map)
    {
        Visible = true;
        ItemID = 0x375A;
    }

    public override bool OnMoveOver(Mobile mobile) => true;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile && CanDoubleClick(from) && CanTeleport(from) && !CovetousExit.WaitingForCeleste(this, from))
            StartTeleport(from);
    }
}
