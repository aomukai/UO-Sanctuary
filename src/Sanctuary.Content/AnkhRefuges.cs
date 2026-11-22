// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;

namespace Sanctuary;

internal static class AnkhRefuges
{
    internal const int Radius = 5;
    private static readonly Rectangle2D _compassionApproach = new(1852, 860, 29, 24);

    internal static bool Contains(Map map, Point3D location, int radius = Radius)
    {
        if (map == null || map == Map.Internal)
            return false;

        // The path from the desert to the Compassion ankh starts beyond its usual radius.
        if (map == Map.Trammel && Math.Abs(location.Z + 1) < 16 && _compassionApproach.Contains(location))
            return true;

        foreach (var item in map.GetItemsInRange<Item>(location, radius))
            if (item is AnkhNorth or AnkhWest or RejuvinationAddonComponent
                && Math.Abs(item.Z - location.Z) < 16)
                return true;
        return false;
    }
}
