// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal static class PetCombatRanges
{
    internal const int BonusTiles = 4;
    internal const int BonusSourceCells = 2 * BonusTiles;
    internal const int GolemAggroBonusSourceCells = 6;

    internal static int ExtendSquared(int squaredRange)
    {
        int range = (int)Math.Sqrt(squaredRange) + BonusSourceCells;
        return range * range;
    }
}
