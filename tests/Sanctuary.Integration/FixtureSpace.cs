using System;
using Server;
using Server.Mobiles;

namespace Sanctuary.Integration;

internal static class FixtureSpace
{
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
