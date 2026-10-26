// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Native terrain/room projection shared by source-controlled ground pets.
// It does not certify D2 room geometry, source footprints or crowd ordering.
internal static class PetGroundWorld
{
    internal static bool Point(BaseCreature body,Point2D cell,out Point3D point,bool zeroFootprint=false)
    {
        point=new((cell.X+1)/2,(cell.Y+1)/2,body.Z);var map=body.Map;
        if(map==null || map==Map.Internal || cell.X<=0 || cell.Y<=0 || cell.X>=map.Width*2 || cell.Y>=map.Height*2){return false;}
        bool Fits(Point3D p)=>zeroFootprint?map.CanFit(p.X,p.Y,p.Z,16,checkMobiles:false,requireSurface:true):Teleportation.Space(map,p,body,true);
        if(Fits(point)){return true;}
        int z=map.GetAverageZ(point.X,point.Y);if(Math.Abs(z-body.Z)>=16){return false;}
        point=new(point.X,point.Y,z);return Fits(point);
    }
    internal static int Collision(BaseCreature body,int x,int y,bool zeroFootprint=false)=>Point(body,new(x,y),out _,zeroFootprint)?0:0x3c01;
    internal static int Room(BaseCreature body,Point2D p)=>p.X>0&&p.Y>0&&p.X<body.Map.Width*2&&p.Y<body.Map.Height*2?body.Map.MapID+1:0;
}
