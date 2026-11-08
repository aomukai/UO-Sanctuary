// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
namespace Sanctuary;

// Approved Sanctuary adaptation. UO tile range is Chebyshev distance;
// distance-squared then coordinates order candidates within each band.
internal static class AssassinSentryPlacement
{
    private static readonly Point2D[] Offsets=Enumerable.Range(-4,9).SelectMany(x=>Enumerable.Range(-4,9).Select(y=>new Point2D(x,y)))
        .OrderBy(p=>p.X==0&&p.Y==0?0:Math.Max(Math.Abs(p.X),Math.Abs(p.Y))<=1?1:2)
        .ThenBy(p=>p.X*p.X+p.Y*p.Y).ThenBy(p=>p.X).ThenBy(p=>p.Y).ToArray();
    internal static bool Resolve(Point3D requested,Func<Point3D,bool> valid,out Point3D point)
    {
        ArgumentNullException.ThrowIfNull(valid);point=requested;
        foreach(var offset in Offsets)
        {
            var candidate=new Point3D(requested.X+offset.X,requested.Y+offset.Y,requested.Z);
            if(valid(candidate)){point=candidate;return true;}
        }
        return false;
    }
}
