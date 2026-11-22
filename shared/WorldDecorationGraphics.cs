// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;

public static class WorldDecorationGraphics
{
    public const ushort DragonHoard = 65520;
    public const ushort DragonHoardGroundFirst = 65521;
    public const int DragonHoardGroundCount = 12;
    public const int DragonHoardGroundDrawOffset = 112;

    public static bool IsDragonHoardGround(ushort graphic) =>
        graphic >= DragonHoardGroundFirst && graphic < DragonHoardGroundFirst + DragonHoardGroundCount;
}
