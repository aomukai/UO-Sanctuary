// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
namespace Sanctuary;

public sealed record GenericPetSavedTarget(int Kind,int Class,byte Variant,uint Flags124);
public sealed record GenericPetSavedPath(uint X,uint Y,uint Flags,int ClientX,int ClientY,int Room,int PreviousRoom,
    int Word30,int Word34,int Type,int Pattern58,int Size5c,int Mask60,int Mask64,int Word200,ushort Collision68,uint Point204,
    ulong Raw96,int ActorHandle,int ActorKind,GenericPetSavedTarget Target)
{
    internal static GenericPetSavedPath Capture(GenericPetCommitPath p)=>new(p.X,p.Y,p.Flags,p.ClientX,p.ClientY,p.Room,p.PreviousRoom,
        p.Word30,p.Word34,p.Type,p.Pattern58,p.Size5c,p.Mask60,p.Mask64,p.Word200,p.Collision68,p.Point204,p.Raw96,p.Actor.Handle,p.Actor.Kind,
        p.Target is{} t?new(t.Kind,t.Class,t.Variant,t.Flags124):null);
    internal GenericPetCommitPath Restore()=>new(){X=X,Y=Y,Flags=Flags,ClientX=ClientX,ClientY=ClientY,Room=Room,PreviousRoom=PreviousRoom,
        Word30=Word30,Word34=Word34,Type=Type,Pattern58=Pattern58,Size5c=Size5c,Mask60=Mask60,Mask64=Mask64,Word200=Word200,
        Collision68=Collision68,Point204=Point204,Raw96=Raw96,Actor=new(ActorHandle,ActorKind),
        Target=Target is{} t?new(t.Kind,t.Class,t.Variant,t.Flags124):null};
}
public sealed record GenericPetSavedMember(int Handle,uint Owner,GenericPetSavedPath Path,int ClassId,uint PetFlags,bool HasPath,
    int Kind178,int Class17c,int Word180,int Word184,int PathWord34,int Next160,uint Flags128,byte ByteB9,uint Flags124,int Next150);
public sealed record GenericPetSavedRoom(int Handle,int Head,int FriendlyCount,byte Flags54,int UpdateHead,bool HasMarker,int MarkerValue);
public sealed record GenericPetPlacementImage(int Map,int Width,int Height,long Revision,GenericPetSavedMember[] Members,GenericPetSavedRoom[] Rooms)
{public string Profile{get;init;}="prepared-generic-pet-placement-v1";}

// Persistent source room/update graph for explicitly registered prepared pets.
// Collision buffers remain fresh native projections for each operation. This
// does not supply birth paths, automatic AI, world-wide crowds or D2 visibility.
internal sealed class GenericPetPlacementWorld
{
    private sealed record Entry(uint Owner,GenericPetMonsterPlacementUnit Unit,GenericPetMonsterRoomLink Link);
    private readonly Dictionary<int,Entry> Members=new();
    private readonly Dictionary<int,GenericPetLinkedRoom> Rooms=new();
    internal readonly Map Map;
    internal long Revision{get;private set;}
    internal int Count=>Members.Count;
    internal readonly GenericPetRoomLinkServices Links;
    internal GenericPetPlacementWorld(Map map)
    {
        if(map==null||map==Server.Map.Internal){throw new NotSupportedException("Pet placement world requires a native map");}
        Map=map;Links=new(Member,Room,_=>2);
    }
    private GenericPetCommitRoom Bounds(int handle)
    {
        int columns=(Map.Width*2+31)/32,rows=(Map.Height*2+31)/32;
        if(handle<=0||handle>columns*rows){throw new InvalidDataException("Invalid pet placement room handle");}
        int x=(handle-1)%columns*32,y=(handle-1)/columns*32;
        return new(x,y,Math.Min(32,Map.Width*2-x),Math.Min(32,Map.Height*2-y));
    }
    internal GenericPetLinkedRoom EnsureRoom(int handle,GenericPetCommitRoom bounds)
    {
        if(bounds!=Bounds(handle)){throw new NotSupportedException("Pet room bounds differ from their native map binding");}
        if(!Rooms.TryGetValue(handle,out var room))
        {room=new(handle,bounds,new(handle,0,0,new(0)));Rooms.Add(handle,room);}
        return room;
    }
    internal GenericPetLinkedRoom Room(int handle)=>Rooms.GetValueOrDefault(handle);
    internal GenericPetMonsterRoomLink Member(int handle)=>Members.TryGetValue(handle,out var e)?e.Link
        :throw new NotSupportedException("Pet source room member is not registered");
    internal GenericPetMonsterPlacementUnit Unit(int handle)=>Members.TryGetValue(handle,out var e)?e.Unit
        :throw new NotSupportedException("Pet source placement unit is not registered");
    internal void Begin(long revision)
    {
        if(revision!=Revision){throw new NotSupportedException("Pet placement context belongs to an older world revision");}
        Revision=checked(Revision+1);
    }
    internal void Require(SkeletonGuardian body,GenericPetMonsterPlacementUnit unit,GenericPetCommitPath path)
    {
        if(body.Map!=Map||!Members.TryGetValue(path.Actor.Handle,out var e)||e.Owner!=WarlockDemonPlacement.Owner(body)||body.ControlMaster?.Serial.Value!=e.Owner
            ||!ReferenceEquals(unit,e.Unit)||!ReferenceEquals(path,e.Link.Path))
        {throw new NotSupportedException("Pet body,path or placement identity differs from its registered world");}
    }
    internal void Register(SkeletonGuardian body,GenericPetMonsterPlacementUnit unit,GenericPetCommitPath path)
    {
        GenericPetNativePlacement.RequireActor(body,unit,path);Validate();
        if(body.Map!=Map||body.ControlMaster?.Serial.Value!=WarlockDemonPlacement.Owner(body)||Members.ContainsKey(path.Actor.Handle))
        {throw new NotSupportedException("Pet already registered or belongs to another map/owner");}
        int handle=path.Actor.Handle;var room=EnsureRoom(path.Room,Bounds(path.Room));
        var link=new GenericPetMonsterRoomLink(handle,path,new(handle,0,0)){Flags128=unit.Pet.Flags};
        Begin(Revision);Members.Add(handle,new(WarlockDemonPlacement.Owner(body),unit,link));
        GenericPetRoomLinks.Add(handle,room.Handle,Links);
    }
    internal void Remove(int handle)
    {
        Validate();_=Member(handle);Begin(Revision);
        GenericPetRoomLinks.Remove(handle,Links);Members.Remove(handle);
    }
    private void Validate()
    {
        var membership=new HashSet<int>();var updates=new HashSet<int>();
        foreach(var room in Rooms.Values)
        {
            int count=0;
            for(int current=room.Head;current!=0;current=Member(current).Next160)
            {
                if(!membership.Add(current)||Member(current).Path.Room!=room.Handle){throw new InvalidDataException("Cyclic,duplicate or wrong-room pet membership");}
                count++;
            }
            if(count!=room.FriendlyCount){throw new InvalidDataException("Pet friendly room count differs from its members");}
            for(int current=room.Update.Head;current!=0;current=Member(current).Update.Next150)
            {
                if(!updates.Add(current)||Member(current).Path.Room!=room.Handle||(Member(current).Update.Flags124&0x2000)==0)
                {throw new InvalidDataException("Cyclic,duplicate or wrong-room pet update membership");}
            }
        }
        foreach(var e in Members.Values)
        {
            var p=e.Link.Path;
            if(p?.Actor is not{Kind:1}||p.Actor.Handle!=e.Link.Handle||p.Mask60!=0x100||p.Size5c is <1 or >5
                ||!e.Unit.HasPath||!WarlockDemonPlacement.ClassIdAllowed(e.Unit.Pet.ClassId)||e.Link.Flags128!=e.Unit.Pet.Flags
                ||!((Serial)e.Owner).IsMobile||!((Serial)unchecked((uint)e.Link.Handle)).IsMobile||!membership.Contains(e.Link.Handle)
                ||((e.Link.Update.Flags124&0x2000)!=0)!=updates.Contains(e.Link.Handle)
                ||!updates.Contains(e.Link.Handle)&&e.Link.Update.Next150!=0)
            {throw new InvalidDataException("Invalid registered pet path or room node");}
            var b=Bounds(p.Room);int x=(ushort)(p.X>>16),y=(ushort)(p.Y>>16);
            if(x<b.X||y<b.Y||x>=b.X+b.Width||y>=b.Y+b.Height){throw new InvalidDataException("Saved pet path lies outside its room");}
        }
    }
    internal GenericPetPlacementImage Capture()
    {
        Validate();
        return new(Map.MapID,Map.Width,Map.Height,Revision,Members.OrderBy(x=>x.Key).Select(pair=>
        {
            var e=pair.Value;var u=e.Unit;var n=e.Link;
            return new GenericPetSavedMember(n.Handle,e.Owner,GenericPetSavedPath.Capture(n.Path),u.Pet.ClassId,u.Pet.Flags,u.HasPath,
                u.Kind178,u.Class17c,u.Word180,u.Word184,u.PathWord34,n.Next160,n.Flags128,n.ByteB9,n.Update.Flags124,n.Update.Next150);
        }).ToArray(),Rooms.OrderBy(x=>x.Key).Select(pair=>
        {
            var r=pair.Value;return new GenericPetSavedRoom(r.Handle,r.Head,r.FriendlyCount,r.Update.Flags54,r.Update.Head,r.Update.Marker!=null,r.Update.Marker?.Value??0);
        }).ToArray());
    }
    internal static GenericPetPlacementWorld Restore(GenericPetPlacementImage image)
    {
        if(image is not{Profile:"prepared-generic-pet-placement-v1",Members:not null,Rooms:not null,Revision:>=0 and <long.MaxValue}
            ||image.Map<0||image.Map>=Server.Map.Maps.Length||Server.Map.Maps[image.Map] is not{} map||map==Server.Map.Internal
            ||image.Width!=map.Width||image.Height!=map.Height)
        {throw new InvalidDataException("Invalid saved pet placement world");}
        var world=new GenericPetPlacementWorld(map){Revision=image.Revision};
        foreach(var r in image.Rooms)
        {
            if(r==null||world.Rooms.ContainsKey(r.Handle)){throw new InvalidDataException("Duplicate saved pet room");}
            var room=world.EnsureRoom(r.Handle,world.Bounds(r.Handle));room.Head=r.Head;room.FriendlyCount=r.FriendlyCount;
            room.Update.Flags54=r.Flags54;room.Update.Head=r.UpdateHead;room.Update.Marker=r.HasMarker?new(r.MarkerValue):null;
        }
        foreach(var m in image.Members)
        {
            if(m?.Path==null||world.Members.ContainsKey(m.Handle)){throw new InvalidDataException("Duplicate or missing saved pet path");}
            var path=m.Path.Restore();var unit=new GenericPetMonsterPlacementUnit(new(m.ClassId,m.PetFlags),m.HasPath,m.Kind178,m.Class17c,m.Word180,m.Word184,m.PathWord34);
            var link=new GenericPetMonsterRoomLink(m.Handle,path,new(m.Handle,m.Flags124,m.Next150)){Next160=m.Next160,Flags128=m.Flags128,ByteB9=m.ByteB9};
            world.Members.Add(m.Handle,new(m.Owner,unit,link));
        }
        world.Validate();return world;
    }
}
