// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class GroundSpellVisuals
{
    // World fractions and visual lift are projected into screen pixels without changing the world position.
    internal static Point3D Sample(Map map, double x, double y, int z, int graphic, int duration, int lift = 0)
    {
        var point = new Point3D((int)Math.Round(x), (int)Math.Round(y), z);
        double dx = x - point.X, dy = y - point.Y;
        Effects.SendLocationEffect(point, map, graphic, duration,
            speed: (int)Math.Round((dx - dy) * 22) + SpellEffectGraphics.GroundEffectPositionBias,
            hue: (int)Math.Round((dx + dy) * 22) + SpellEffectGraphics.GroundEffectPositionBias - lift);
        return point;
    }
}
