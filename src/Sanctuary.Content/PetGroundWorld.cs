// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Movement;
namespace Sanctuary;

// Native terrain/room projection shared by source-controlled ground pets.
// It does not certify D2 room geometry, source footprints or crowd ordering.
internal static class PetGroundWorld
{
    internal static bool ZeroFootprint(Map map,Point3D p)=>map!=null&&map!=Map.Internal&&p.X>0&&p.Y>0&&p.X<map.Width&&p.Y<map.Height
        && map.CanFit(p.X,p.Y,p.Z,16,checkMobiles:false,requireSurface:true);
    internal static bool Point(BaseCreature body, Point2D cell, out Point3D point, bool zeroFootprint = false)
    {
        point = new((cell.X + 1) / 2, (cell.Y + 1) / 2, body.Z);
        var map = body.Map;
        if (map == null || map == Map.Internal || cell.X <= 0 || cell.Y <= 0 || cell.X >= map.Width * 2 || cell.Y >= map.Height * 2)
            return false;

        bool Fits(Point3D p) => zeroFootprint ? ZeroFootprint(map, p) : Teleportation.Space(map, p, body, true);
        if (Fits(point))
            return true;

        int landZ = map.GetAverageZ(point.X, point.Y);
        if (Math.Abs(landZ - body.Z) < 16)
        {
            point.Z = landZ;
            if (Fits(point))
                return true;
        }

        // Stair treads and raised walkways can be statics with a different height from the land below.
        if (!body.InRange(point, 8) || body.AtPoint(point.X, point.Y)
            || !FindStairHeight(body, point, out int stepZ))
            return false;

        point.Z = stepZ;
        return Fits(point);
    }

    private static bool FindStairHeight(BaseCreature body, Point3D target, out int z)
    {
        // Source-controlled pets can disable native AI walking while still using native terrain.
        bool cantWalk = body.CantWalk;
        body.CantWalk = false;
        try
        {
            // The source path planner also probes ahead of the next step. Resolve the
            // intervening treads so it can see the continuation on the other level.
            var from = body.Location;
            while (from.X != target.X || from.Y != target.Y)
            {
                var direction = Utility.GetDirection(from, target);
                if (!Movement.CheckMovement(body, body.Map, from, direction, out z) || Math.Abs(z - from.Z) >= 16)
                    return false;

                Movement.Offset(direction, ref from);
                from.Z = z;
            }
            z = from.Z;
            return true;
        }
        finally
        {
            body.CantWalk = cantWalk;
        }
    }
    internal static int Collision(BaseCreature body,int x,int y,bool zeroFootprint=false)=>Point(body,new(x,y),out _,zeroFootprint)?0:0x3c01;
    internal static int Room(BaseCreature body,Point2D p)=>p.X>0&&p.Y>0&&p.X<body.Map.Width*2&&p.Y<body.Map.Height*2?body.Map.MapID+1:0;
}
