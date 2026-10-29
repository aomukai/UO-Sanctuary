// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal static class ItemDamageOperations
{
    // Source op5 truncates level*coefficient/8 to an integer percent first,
    // then applies it to the original local maximum separately from op13
    // ED and op4 flat maximum. The caller supplies that integer percent.
    internal static int LevelMaximum(int originalMaximum, int percent)
    {
        if (originalMaximum < 0 || percent < 0) { throw new NotSupportedException("Negative local maximum scaling is not mapped"); }
        return (int)(checked(originalMaximum * percent) / 100.0);
    }
}
