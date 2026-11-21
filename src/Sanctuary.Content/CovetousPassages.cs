// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
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
            PlacePassage(map, new(5393 + lane, 1958, 0), new(2421 + lane, 885, 0));

            PlacePassage(map, new(2385, 837 + lane, 0), new(5613, 1997 + lane, 0));
            PlacePassage(map, new(5614, 1997 + lane, 0), new(2386, 837 + lane, 0));
            PlacePassage(map, new(2456 + lane, 859, 0), new(5389 + lane, 2025, 0));
            PlacePassage(map, new(5389 + lane, 2026, 0), new(2456 + lane, 860, 0));

            PlacePassage(map, new(2545 + lane, 850, 0), new(5579 + lane, 1925, 0));
            PlacePassage(map, new(5579 + lane, 1926, 0), new(2545 + lane, 849, 0));

            PlacePassage(map, new(5468, 1805 + lane, 2), new(5592, 1843, 0));
            PlacePassage(map, new(5552 + lane, 1807, 2), new(5558, 1824, 0));

            // Buccaneer's Den portal. Use the foot of the arch on both sides.
            PlacePassage(map, new(2618 + lane, 976, 2), new(2724, 2133, 0));
            PlacePassage(map, new(2618 + lane, 978, 2), new(2724, 2133, 0));
            PlacePassage(map, new(2725, 2133 + lane, 0), new(2618, 979, 0));
            PlacePassage(map, new(2729, 2133 + lane, 0), new(2618, 979, 0));
        }

        PlacePassage(map, new(2501, 917, 0), new(5456, 1862, 0));
        PlacePassage(map, new(5455, 1863, 0), new(2498, 918, 0));
        PlacePassage(map, new(5456, 1863, 0), new(2499, 918, 0));

        PlacePassage(map, new(5592, 1842, 0), new(5469, 1805, 0));
        PlacePassage(map, new(5556, 1824, 0), new(5552, 1808, 0));
        PlacePassage(map, new(5557, 1824, 0), new(5552, 1808, 0));
    }
}
