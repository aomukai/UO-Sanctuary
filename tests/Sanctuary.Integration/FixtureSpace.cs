using System;
using Server;
using Server.Mobiles;

namespace Sanctuary.Integration;

internal static class FixtureSpace
{
    // Park inspected state only after its actual timing/expiry checks. The
    // full retained suite and two reloads now exceed the old ten-minute hold.
    // Natural expiry controls keep their original short deadlines.
    internal const int RetentionMilliseconds=3_600_000;
    internal static TimeSpan Retention=>TimeSpan.FromMilliseconds(RetentionMilliseconds);
    internal static bool ClearOfAllies(PlayerMobile p,Point3D point,Map map)
    {
        foreach(var other in map.GetMobilesInRange<Mobile>(point,0))
        { if(AlliedOverlap.PassThrough(p,other) && point.Z+16>other.Z && other.Z+16>point.Z) { return false; } }
        return true;
    }
    internal static void Place(PlayerMobile p, Point3D anchor, Map map)
    {
        for (int r=0;r<=30;r++)
        {
            for (int dx=-r;dx<=r;dx++)
            {
                for(int dy=-r;dy<=r;dy++)
                {
                    if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=r) { continue; }
                    var point=new Point3D(anchor.X+dx,anchor.Y+dy,anchor.Z);
                    if (Teleportation.Space(map,point,p)) { p.MoveToWorld(point,map); return; }
                }
            }
        }
        throw new InvalidOperationException("No independent fixture position near "+anchor);
    }
}
