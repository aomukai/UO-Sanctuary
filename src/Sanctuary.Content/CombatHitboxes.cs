// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

public static class CombatHitboxes
{
    public const double BodyRadius = 0.5;
    public const double TeethRadius = 0.25;
    public const int BodyHeight = 16;

    // A swept circle against a body footprint, measured in world tiles. Screen
    // pixels, animation gaps, and the mouse selection rectangle never enter this test.
    public static bool Intersect(Point3D start, Point3D end, Point3D body, out double entry)
    {
        entry = 0;
        if (Math.Abs(body.Z - start.Z) >= BodyHeight)
        {
            return false;
        }
        double dx = end.X - start.X, dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        double x = body.X - start.X, y = body.Y - start.Y;
        var radius = BodyRadius + TeethRadius;
        if (length == 0)
        {
            return x * x + y * y <= radius * radius;
        }
        var along = (x * dx + y * dy) / length;
        var perpendicularSquared = Math.Max(0, x * x + y * y - along * along);
        if (perpendicularSquared > radius * radius)
        {
            return false;
        }
        var halfChord = Math.Sqrt(radius * radius - perpendicularSquared);
        if (along + halfChord < 0 || along - halfChord > length)
        {
            return false;
        }
        entry = Math.Max(0, along - halfChord);
        return true;
    }
}
