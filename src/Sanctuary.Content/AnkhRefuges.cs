// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;

namespace Sanctuary;

internal static class AnkhRefuges
{
    internal const int Radius = 5;

    internal static bool Contains(Map map, Point3D location, int radius = Radius)
    {
        if (map == null || map == Map.Internal)
            return false;

        foreach (var item in map.GetItemsInRange<Item>(location, radius))
            if (item is AnkhNorth or AnkhWest or RejuvinationAddonComponent
                && Math.Abs(item.Z - location.Z) < 16)
                return true;
        return false;
    }
}
