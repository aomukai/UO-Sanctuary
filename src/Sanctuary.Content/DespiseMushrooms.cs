// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Items;

namespace Sanctuary;

internal static class DespiseMushrooms
{
    private static readonly (Point3D At, ushort Hue)[] _mushrooms =
    [
        (new(5499, 570, 59), DespiseLighting.Green),
        (new(5501, 594, 45), DespiseLighting.Yellow),
        (new(5485, 565, 60), DespiseLighting.Blue),
        (new(5496, 542, 60), DespiseLighting.Red),
        (new(5484, 544, 60), DespiseLighting.Yellow),
        (new(5475, 528, 62), DespiseLighting.Blue),
        (new(5458, 548, 60), DespiseLighting.Yellow),
        (new(5448, 533, 60), DespiseLighting.Green),
        (new(5438, 543, 60), DespiseLighting.Blue),
        (new(5435, 557, 60), DespiseLighting.Red),
        (new(5421, 527, 60), DespiseLighting.Yellow),
        (new(5426, 549, 61), DespiseLighting.Green),
        (new(5421, 571, 60), DespiseLighting.Green),
        (new(5407, 575, 60), DespiseLighting.Blue),
        (new(5401, 576, 60), DespiseLighting.Yellow),
        (new(5400, 557, 60), DespiseLighting.Red),
        (new(5401, 586, 45), DespiseLighting.Green),
        (new(5393, 600, 45), DespiseLighting.Blue),
        (new(5413, 602, 45), DespiseLighting.Yellow),
        (new(5436, 588, 45), DespiseLighting.Red),
        (new(5449, 581, 47), DespiseLighting.Blue),
        (new(5466, 601, 45), DespiseLighting.Green),
        (new(5482, 600, 45), DespiseLighting.Red),
        (new(5477, 624, 26), DespiseLighting.Blue),
        (new(5501, 625, 25), DespiseLighting.Yellow)
    ];

    internal static void Install(Map map)
    {
        foreach (var (at, hue) in _mushrooms)
        {
            Static mushroom = null;
            foreach (var item in map.GetItemsAt<Static>(at))
            {
                if (item.Z == at.Z && item.ItemID == DespiseLighting.MushroomGraphic)
                {
                    mushroom = item;
                    break;
                }
            }
            if (mushroom == null)
                new Static(DespiseLighting.MushroomGraphic) { Hue = hue }.MoveToWorld(at, map);
            else if (mushroom.Hue != hue)
                mushroom.Hue = hue;
        }
    }
}
