// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

// Existing ground semantics stay in DemonGroundMovement/PetGroundWorld.
// Defiler requires an explicitly supplied flying candidate path;its artwork
// alone cannot opt an old prepared controller into new terrain semantics.
internal static class DemonPathWorld
{
    private static GenericPetCommitPath Path(SkeletonGuardian b,GenericPetCommitPath p)=>p??DeathMarkWorld.Path(b);
    private static bool Flight(SkeletonGuardian b,GenericPetCommitPath p=null)=>b is WarlockDefilerBody&&DefilerFlyingWorld.Profile(b,Path(b,p));
    internal static int Size(SkeletonGuardian b)=>Flight(b)?DefilerFlyingMovement.Species.Size:DemonGroundMovement.Size(b);
    internal static int Mask(SkeletonGuardian b)=>Flight(b)?0x1804:0x3c01;
    internal static (int Velocity,int Animation) Speed(SkeletonGuardian b,DateTime at,int extra=0)
        =>Flight(b)?DefilerFlyingMovement.Speed((WarlockDefilerBody)b,at,extra):DemonGroundMovement.Speed(b,at,extra);
    internal static int Collision(SkeletonGuardian b,int x,int y)=>Flight(b)?DefilerFlyingWorld.Collision((WarlockDefilerBody)b,x,y):PetGroundWorld.Collision(b,x,y);
    // Nearby placement has a distinct0x1c09filter,which lacks flying bit4.
    // Normalize native feasibility to the same blocked-cell projection used
    // by ground placement,not the flying movement-mask vocabulary.
    internal static int PlacementCollision(SkeletonGuardian b,int x,int y)=>Flight(b)?Collision(b,x,y)==0?0:0x3c01:PetGroundWorld.Collision(b,x,y);
    internal static int RawPlacementCollision(SkeletonGuardian b,GenericPetCommitPath path,int x,int y)
        =>Flight(b,path)?DefilerFlyingWorld.RawCollision((WarlockDefilerBody)b,x,y)==0?0:0x3c01:PetGroundWorld.Collision(b,x,y);
    internal static bool Point(SkeletonGuardian b,GenericPetCommitPath path,Point2D cell,out Point3D point,bool placement=false)
        =>Flight(b,path)?DefilerFlyingWorld.Point((WarlockDefilerBody)b,cell,out point,placement):PetGroundWorld.Point(b,cell,out point,placement);
}
