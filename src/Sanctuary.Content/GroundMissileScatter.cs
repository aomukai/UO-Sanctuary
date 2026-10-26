// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal readonly record struct GroundMissileSample(int X, int Y, ulong Seed);

// Blizzard and Fissure share MISSMODE_CreateMissileWithCollisionCheck's
// positive-range sampling. Landing masks, emission gates, factory requests
// and damage remain the callers' responsibility.
internal static class GroundMissileScatter
{
    internal static GroundMissileSample Sample(int sourceX, int elapsedFrame, int range)
    {
        ulong seed = SourceRandom.InitLow(unchecked(sourceX + elapsedFrame));
        // Preserve Blizzard's existing clamped behavior outside positive ranges.
        // Those inputs are not claimed to match the source's negative bounds.
        int radius = Math.Max(0, range - 1), bound = radius * 2;
        int x = (int)SourceRandom.Limited(ref seed, bound) - radius;
        int y = (int)SourceRandom.Limited(ref seed, bound) - radius;
        return new(x, y, seed);
    }
}
