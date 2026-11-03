// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Stable monster identity with the current dynamic-path/cache fields. The
// actual pet recovery caller supplies last argument0; player packet/history
// and nonzero-last-argument branches are not exposed by this interface.
internal sealed class GenericPetMonsterPlacementUnit(GenericPetWarpUnit pet,bool hasPath,int kind178,int class17c,int word180,int word184,int pathWord34)
{
    internal readonly GenericPetWarpUnit Pet=pet;
    internal bool HasPath=hasPath;
    internal int Kind178=kind178,Class17c=class17c,Word180=word180,Word184=word184,PathWord34=pathWord34;
}
internal sealed record GenericPetNearbyRequest(int Room,int X,int Y,int Zero,int Word180,int Mask1,int Mask2,int Zero2,int Distance,int One);
internal sealed record GenericPetNearbyResult(int Room,int X,int Y);
internal sealed record GenericPetMonsterPlacementServices(Func<int> Room,Func<int,int,int,int> RoomAt,
    Action<int> BeforeSearch,Func<int> Word180,Func<int> Word184,
    Func<GenericPetNearbyRequest,GenericPetNearbyResult> Nearby,Action AfterSearch,
    Func<int,uint,int> UpdatePath,Action AfterPlacement,Action<int> Synchronize);
internal static class GenericPetMonsterPlacement
{
    internal static int Place(GenericPetMonsterPlacementUnit unit,int room,int x,int y,int skipSearch,GenericPetMonsterPlacementServices services)
    {
        if(unit?.Pet==null){throw new NotSupportedException("Current monster placement requires a stable monster unit");}
        int oldRoom=services.Room();if(!unit.HasPath){throw new NotSupportedException("Current monster placement requires a dynamic path");}
        if(room==0)
        {
            room=services.RoomAt(0,x,y);if(room==0){room=services.RoomAt(oldRoom,x,y);}
            if(room==0){return 0;}
        }
        if(skipSearch==0)
        {
            services.BeforeSearch(1);
            if(unit.Kind178!=1||unit.Class17c!=unit.Pet.ClassId)
            {
                unit.Kind178=1;unit.Class17c=unit.Pet.ClassId;
                unit.Word180=services.Word180();unit.Word184=services.Word184();
            }
            var result=services.Nearby(new(room,x,y,0,unit.Word180,0x1c09,0x1c09,0,50,1));
            room=result.Room;x=result.X;y=result.Y;services.AfterSearch();
            if(room==0){return 0;}
        }
        uint packed=unchecked((uint)(ushort)x|((uint)(ushort)y<<16));
        if(services.UpdatePath(room,packed)==0){return 0;}
        unit.PathWord34=0;services.AfterPlacement();unit.Pet.Flags|=0x10000;services.Synchronize(0);return 1;
    }
    internal static GenericPetWarpServices Bind(GenericPetWarpServices warp,GenericPetMonsterPlacementUnit unit,GenericPetMonsterPlacementServices services)
        =>warp with{Place=(room,x,y,skip,last)=>last==0?Place(unit,room,x,y,skip,services)
            :throw new NotSupportedException("Current pet relocation last argument must be0")};
}
