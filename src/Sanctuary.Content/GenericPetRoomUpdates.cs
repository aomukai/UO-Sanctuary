// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed class GenericPetRoomMarker(int value){internal int Value=value;}
internal sealed class GenericPetRoomUpdateRoom(int handle,byte flags54,int head,GenericPetRoomMarker marker)
{internal readonly int Handle=handle;internal byte Flags54=flags54;internal int Head=head;internal GenericPetRoomMarker Marker=marker;}
internal sealed class GenericPetRoomUpdateUnit(int handle,uint flags124,int next150)
{internal readonly int Handle=handle;internal uint Flags124=flags124;internal int Next150=next150;}
internal static class GenericPetRoomUpdates
{
    internal static void Touch(GenericPetRoomUpdateUnit unit,Func<int,GenericPetRoomUpdateRoom> room)
    {
        if(unit==null){throw new NotSupportedException("Current room update requires a unit");}
        var r=room(unit.Handle);if(r==null||(r.Flags54&4)!=0||(unit.Flags124&0x2000)!=0){return;}
        unit.Flags124|=0x2000;
        // Keep the source partial write if its required room marker is absent.
        if(r.Marker==null){throw new NotSupportedException("Current room update requires its marker data");}
        r.Marker.Value=1;unit.Next150=r.Head;r.Head=unit.Handle;
    }
    internal static uint PackedCoordinates(GenericPetDecisionUnit unit)
    {
        if(unit==null){throw new NotSupportedException("Current packed coordinates require a unit");}
        var(x,y)=unit.Coordinates();return unchecked((uint)(ushort)x|((uint)(ushort)y<<16));
    }
    internal static int PathWord5c(GenericPetDecisionUnit unit,int word)
    {
        if(unit==null||unit.Kind==5||!unit.HasPath){throw new NotSupportedException("Current path word5c requires a non-tile path");}
        return word;
    }
    internal static GenericPetWarpServices Bind(GenericPetWarpServices warp,Func<GenericPetDecisionUnit> owner,
        Func<GenericPetDecisionUnit> pet,Func<int> word,GenericPetRoomUpdateUnit update,Func<int,GenericPetRoomUpdateRoom> room)
        =>warp with{PackedOwnerCoordinates=()=>PackedCoordinates(owner()),UnitSize=()=>PathWord5c(pet(),word()),AfterPlacement=()=>Touch(update,room)};
}
