// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Raw current dynamic-path fields shared by coordinate commitment and reset.
// Handles preserve identity; geometry, collision footprints and room ownership
// are explicit services until a source-compatible native world adapter exists.
internal sealed class GenericPetCommitTarget(int kind,int @class,byte variant,uint flags124)
{internal int Kind=kind,Class=@class;internal byte Variant=variant;internal uint Flags124=flags124;}
internal sealed class GenericPetCommitActor(int handle,int kind)
{internal readonly int Handle=handle;internal int Kind=kind;}
internal sealed class GenericPetCommitPath
{
    internal uint X,Y,Flags;
    internal int ClientX,ClientY,Room,PreviousRoom,Word30,Word34,Type,Pattern58,Size5c,Mask60,Mask64,Word200;
    internal ushort Collision68;
    internal uint Point204;
    internal ulong Raw96;
    internal GenericPetCommitActor Actor;
    internal GenericPetCommitTarget Target;
}
internal sealed record GenericPetCommitRoom(int X,int Y,int Width,int Height);
internal sealed record GenericPetFootprintRequest(int Room,int X,int Y,int Pattern,int Mask);
internal sealed record GenericPetPathCommitServices(Func<int,int,int,int> RoomAt,Func<int,GenericPetCommitRoom> Bounds,
    Action<GenericPetFootprintRequest> RemovePattern,Func<GenericPetFootprintRequest,int> CheckPattern,Action<GenericPetFootprintRequest> AddPattern,
    Action<GenericPetFootprintRequest> RemoveSize,Action<GenericPetFootprintRequest> AddSize,
    Action<int> RemoveRoomLink,Action<int,int> AddRoomLink,Action<int> TouchRoom,Func<byte,int,byte?> TargetMetadata,ulong Reset96);

internal static class GenericPetPathCommit
{
    private static uint Center(ushort cell)=>((uint)cell<<16)|0x8000;
    internal static void Coordinates(GenericPetCommitPath p,uint x,uint y,int fallbackRoom,GenericPetPathCommitServices s)
    {
        if((p.Flags&0x40000)!=0&&s.RoomAt(p.Room,(int)(x>>16),(int)(y>>16))==0){p.Word34=0;return;}
        uint px=x>>11,py=y>>11;p.X=x;p.Y=y;
        p.ClientX=unchecked((int)(px-py))>>1;p.ClientY=unchecked((int)((px+py)>>2));
        if(p.Actor==null||(p.Flags&1)==0){return;}
        int cx=(int)(x>>16),cy=(int)(y>>16),room=p.Room;
        if(room!=0)
        {
            var bounds=s.Bounds(room);
            if(cx>=bounds.X&&cx<unchecked(bounds.X+bounds.Width)&&cy>=bounds.Y&&cy<unchecked(bounds.Y+bounds.Height)){return;}
        }
        int next=s.RoomAt(room,cx,cy);
        if(next==0&&fallbackRoom!=0){next=s.RoomAt(fallbackRoom,cx,cy);}
        if(next==0&&(p.Flags&0x40000)!=0){p.Word34=0;return;}
        var actor=p.Actor;p.PreviousRoom=p.Room;if(p.Room!=0){s.RemoveRoomLink(actor.Handle);}
        p.Flags|=2;p.Room=next;
        if(next!=0){s.AddRoomLink(actor.Handle,next);s.TouchRoom(actor.Handle);}
    }
    internal static void Reset(GenericPetCommitPath p,GenericPetPathCommitServices s)
    {
        bool special=false;
        if(p.Target is{Kind:1} t&&(t.Flags124&4)==0)
        {byte? row=s.TargetMetadata(t.Variant,t.Class);special=row.HasValue&&(row.Value&2)!=0;}
        if(p.Type!=17)
        {
            if(p.Actor==null){throw new NotSupportedException("Current path reset requires its actor");}
            if(p.Actor.Kind!=0||!special){Coordinates(p,(p.X&0xffff0000)|0x8000,(p.Y&0xffff0000)|0x8000,0,s);}
        }
        p.Flags&=~0x20u;p.Word30=0;p.Word34=0;p.Raw96=s.Reset96;
    }
    internal static int Commit(GenericPetCommitPath p,int room,uint packed,GenericPetPathCommitServices s)
    {
        ArgumentNullException.ThrowIfNull(p);ArgumentNullException.ThrowIfNull(s);
        ushort x=(ushort)packed,y=(ushort)(packed>>16),oldX=(ushort)(p.X>>16),oldY=(ushort)(p.Y>>16);
        if(p.Actor is{Kind:3})
        {
            if(packed==0)
            {s.RemovePattern(new(p.Room,oldX,oldY,p.Pattern58,p.Mask60));p.Collision68=0;}
            else
            {
                if(oldX==x&&oldY==y){p.Flags&=~8u;}else{p.Flags|=8;}
                int mask=p.Mask60,pattern=p.Pattern58,check=p.Mask64,old=p.Room;
                if(old!=0){s.RemovePattern(new(old,oldX,oldY,pattern,mask));}
                ushort collision=unchecked((ushort)s.CheckPattern(new(room,x,y,pattern,check)));
                s.AddPattern(new(room,x,y,pattern,mask));p.Collision68=collision;p.Word200=1;p.Point204=packed;
            }
        }
        else if(packed==0){s.RemoveSize(new(p.Room,oldX,oldY,p.Size5c,p.Mask60));}
        else
        {
            int old=p.Room,mask=p.Mask60,size=p.Size5c;
            if(old!=0){s.RemoveSize(new(old,oldX,oldY,size,mask));s.AddSize(new(room,x,y,size,mask));}
        }
        if(p.Room!=room){p.Flags|=1;}
        Coordinates(p,Center(x),Center(y),room,s);
        if(packed!=0&&s.RoomAt(room,x,y)==0){throw new NotSupportedException("Current committed coordinate has no source room");}
        Reset(p,s);return 1;
    }
    internal static GenericPetMonsterPlacementServices Bind(GenericPetMonsterPlacementServices placement,GenericPetCommitPath path,GenericPetPathCommitServices services)
        =>placement with{UpdatePath=(room,point)=>Commit(path,room,point,services)};
}
