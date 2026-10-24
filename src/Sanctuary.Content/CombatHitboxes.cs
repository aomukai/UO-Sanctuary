// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

public static class CombatHitboxes
{
    public const double BodyRadius = 0.5;
    public const double TeethRadius = 0.25;
    public const int BodyHeight = 16;

    // D2Common_10407_Impl combines missile size with the actor footprint.
    internal static bool SourceTouches(int missileSize,int targetSize,int dx,int dy)
    {
        if(missileSize is <1 or >3 || targetSize<1){return false;}
        int key=Math.Min(targetSize,3)+3*(missileSize-1),x=Math.Abs(dx),y=Math.Abs(dy);
        return key switch{1=>x==0&&y==0,2 or 4=>x+y<=1,5=>x+y<=2,3 or 7=>x<=1&&y<=1,
            6 or 8=>x<=2&&y<=1||y<=2&&x<=1,9=>x<=2&&y<=2,_=>false};
    }

    // A swept circle against a body footprint, measured in world tiles. Screen
    // pixels, animation gaps, and the mouse selection rectangle never enter this test.
    public static bool Intersect(Point3D start, Point3D end, Point3D body, out double entry)
        => Intersect(start.X, start.Y, end.X, end.Y, start.Z, body, out entry);
    public static bool Intersect(double startX, double startY, double endX, double endY, int z, Point3D body, out double entry)
    {
        entry = 0;
        if (Math.Abs(body.Z - z) >= BodyHeight)
        {
            return false;
        }
        double dx = endX - startX, dy = endY - startY;
        var length = Math.Sqrt(dx * dx + dy * dy);
        double x = body.X - startX, y = body.Y - startY;
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
