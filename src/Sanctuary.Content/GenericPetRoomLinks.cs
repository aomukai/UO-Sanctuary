// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal sealed class GenericPetMonsterRoomLink(int handle,GenericPetCommitPath path,GenericPetRoomUpdateUnit update)
{
    internal readonly int Handle=handle;
    internal readonly GenericPetCommitPath Path=path;
    internal readonly GenericPetRoomUpdateUnit Update=update;
    internal int Next160;
    internal uint Flags128;
    internal byte ByteB9;
}
internal sealed class GenericPetLinkedRoom(int handle,GenericPetCommitRoom bounds,GenericPetRoomUpdateRoom update)
{
    internal readonly int Handle=handle;
    internal readonly GenericPetCommitRoom Bounds=bounds;
    internal readonly GenericPetRoomUpdateRoom Update=update;
    internal int Head,FriendlyCount;
}
internal sealed record GenericPetRoomLinkServices(Func<int,GenericPetMonsterRoomLink> Unit,
    Func<int,GenericPetLinkedRoom> Room,Func<int,int> Alignment);

// Current dynamic monster branch. Native UO sector membership is transported
// separately by MoveToWorld; these are the source room/update linked lists.
internal static class GenericPetRoomLinks
{
    private static GenericPetMonsterRoomLink Require(int handle,GenericPetRoomLinkServices s)
        =>handle!=0&&s.Unit(handle) is{} unit?unit:throw new NotSupportedException("Room link requires a supplied monster unit");
    private static GenericPetLinkedRoom Room(GenericPetMonsterRoomLink unit,GenericPetRoomLinkServices s)
        =>unit.Path?.Room is not(0 or null)?s.Room(unit.Path.Room):null;
    internal static void Touch(int handle,GenericPetRoomLinkServices s)
    {var unit=Require(handle,s);GenericPetRoomUpdates.Touch(unit.Update,_=>Room(unit,s)?.Update);}
    internal static void RemoveUpdate(int handle,GenericPetRoomLinkServices s)
    {
        var unit=Require(handle,s);var room=Room(unit,s);
        if(room==null)
        {
            if(unit.Update.Next150!=0){throw new NotSupportedException("Detached update node retains its next pointer");}
            return;
        }
        int previous=0,current=room.Update.Head;var seen=new HashSet<int>();
        while(current!=0)
        {
            if(!seen.Add(current)){throw new NotSupportedException("Cyclic source update list");}
            var node=Require(current,s);
            if(current==handle)
            {
                if(previous==0){room.Update.Head=node.Update.Next150;}else{Require(previous,s).Update.Next150=node.Update.Next150;}
                node.Update.Next150=0;return;
            }
            previous=current;current=node.Update.Next150;
        }
    }
    internal static int Add(int handle,int roomHandle,GenericPetRoomLinkServices s)
    {
        var unit=Require(handle,s);var room=roomHandle!=0?s.Room(roomHandle):null;
        if(room==null){throw new NotSupportedException("Room link requires a supplied destination room");}
        int x=unit.Path==null?0:(ushort)(unit.Path.X>>16),y=unit.Path==null?0:(ushort)(unit.Path.Y>>16);var bounds=room.Bounds;
        if(x<bounds.X||x>=unchecked(bounds.X+bounds.Width)||y<bounds.Y||y>=unchecked(bounds.Y+bounds.Height))
        {throw new NotSupportedException("Source unit does not lie inside its destination room");}
        var seen=new HashSet<int>();
        for(int node=room.Head;node!=0;node=Require(node,s).Next160)
        {
            if(node==handle||!seen.Add(node)){throw new NotSupportedException("Duplicate or cyclic source room membership");}
        }
        unit.Next160=room.Head;room.Head=handle;Touch(handle,s);
        if(s.Alignment(handle)==2){room.FriendlyCount=unchecked(room.FriendlyCount+1);}
        return 1;
    }
    internal static void Remove(int handle,GenericPetRoomLinkServices s)
    {
        var unit=Require(handle,s);var room=Room(unit,s);
        if(room==null)
        {
            if(unit.Next160!=0){throw new NotSupportedException("Detached room node retains its next pointer");}
            return;
        }
        int previous=0,current=room.Head;bool found=false;var seen=new HashSet<int>();
        while(current!=0)
        {
            if(!seen.Add(current)){throw new NotSupportedException("Cyclic source room membership");}
            var node=Require(current,s);
            if(current==handle)
            {
                if(previous==0){room.Head=node.Next160;}else{Require(previous,s).Next160=node.Next160;}
                node.Next160=0;found=true;break;
            }
            previous=current;current=node.Next160;
        }
        if(found&&s.Alignment(handle)==2)
        {
            if(room.FriendlyCount<=0){throw new NotSupportedException("Friendly source room count underflow after unlink");}
            room.FriendlyCount--;
        }
        RemoveUpdate(handle,s);unit.Update.Flags124&=~0x2000u;
        if(!found){return;}
        if((unit.Flags128&0x4000000)==0){unit.ByteB9=0;}
        if(unit.Path!=null){unit.Path.Room=0;}
    }
}
