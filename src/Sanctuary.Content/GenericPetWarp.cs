// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed class GenericPetWarpUnit(int classId,uint flags)
{internal readonly int ClassId=classId;internal uint Flags=flags;}
internal sealed record GenericPetWarpContext(GenericPetDecisionUnit Owner,GenericPetWarpUnit Pet,byte TableSelector);
internal sealed record GenericPetWarpFind(int Result,int X,int Y);
// Room/region handles and placement services remain explicit native-world
// dependencies. This policy does not invent room geometry or placement order.
internal sealed record GenericPetWarpServices(Func<int> OwnerRoom,Func<int,int,int,int> Region,
    Func<byte,int> TableCount,Func<int,int,int,GenericPetWarpFind> Find,
    Func<int,int,int,int> RoomAt,Func<int,int,int,int,int,int> Place,
    Func<uint> PackedOwnerCoordinates,Func<int> UnitSize,Func<int,int,int,int,int,int> Collision,
    Action AfterPlacement,Action<int> Idle);
internal static class GenericPetWarp
{
    internal static int Execute(GenericPetWarpContext c,GenericPetWarpServices s)
    {
        int room=s.OwnerRoom();if(room==0){return 0;}
        if(c.Owner==null){throw new NotSupportedException("Current warp with a room requires its owner");}
        var(x,y)=c.Owner.Coordinates();int region=s.Region(room,x/5,y/5);
        int index=c.Pet==null?-1:unchecked((short)c.Pet.ClassId);
        if(index>=0)
        {
            if(c.TableSelector is <1 or >3){throw new NotSupportedException("Current warp table selector requires1..3");}
            if(index>=s.TableCount(c.TableSelector)){index=-1;}
        }
        else{index=-1;}
        bool success=false;var found=s.Find(room,region,index);
        if(found.Result!=0)
        {
            room=s.RoomAt(room,found.X,found.Y);
            if(room!=0){success=s.Place(room,found.X,found.Y,0,0)!=0;}
        }
        if(!success)
        {
            uint packed=s.PackedOwnerCoordinates();x=(ushort)packed;y=(ushort)(packed>>16);
            // RoomAt receives the current room, even when a failed initial
            // lookup replaced it with null. Do not restore the original room.
            room=s.RoomAt(room,x,y);if(room==0){return 0;}
            success=s.Place(room,x,y,0,0)!=0;
            if(!success&&s.Collision(room,x,y,s.UnitSize(),0xc01)==0)
            {success=s.Place(room,x,y,1,0)!=0;}
        }
        if(!success){return 0;}
        s.AfterPlacement();if(c.Pet!=null){c.Pet.Flags|=0x10000;}
        s.Idle(5);return 1;
    }
    internal static GenericPetFollowExecutionServices Bind(GenericPetFollowExecutionServices follow,GenericPetWarpContext context,GenericPetWarpServices services)
        =>follow with{Warp=()=>Execute(context,services)};
}
