// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// One synchronous ground-placement transaction. Geometry uses the existing
// native PetGroundWorld projection; it is not a D2 map/crowd reconstruction.
// The caller supplies its actual dynamic path rather than inventing birth,
// movement, collision-pattern, mode or RNG state in this adapter.
internal sealed class GenericPetNativePlacement
{
    private const int Span=32;
    private readonly WarlockGoatmanBody Body;
    private readonly Map Map;
    private readonly GenericPetCommitPath Path;
    private readonly GenericPetMonsterPlacementUnit Unit;
    private readonly Point2D Target;
    private readonly Dictionary<int,SourceCollisionRoom> Grids=new();
    private readonly Dictionary<int,GenericPetLinkedRoom> Rooms=new();
    private readonly GenericPetRoomLinkServices Links;
    private readonly GenericPetMonsterRoomLink Member;
    private readonly GenericPetCollisionFootprintServices Footprints;
    private readonly GenericPetPathTargetView TargetMetadata;
    private readonly GenericPetPlacementWorld SharedWorld;
    private readonly long SharedRevision;
    private bool Used;
    internal int LoadedRooms=>Grids.Count;
    internal GenericPetCommitPath CommittedPath=>Path;
    internal GenericPetLinkedRoom LinkedRoom(int handle)=>Rooms[handle];
    internal GenericPetMonsterRoomLink LinkedUnit=>Member;
    internal static int RoomHandle(Map map,int x,int y)
        =>1+x/Span+(y/Span)*((map.Width*2+Span-1)/Span);

    internal static void RequireActor(WarlockGoatmanBody body,GenericPetMonsterPlacementUnit unit,GenericPetCommitPath path)
    {
        if(body is not{Alive:true,Deleted:false,Prepared:not null}||body.Map==null||body.Map==Server.Map.Internal
            ||body.ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner||owner.Map!=body.Map||!body.Permitted(owner)
            ||unit?.Pet==null||!unit.HasPath||path?.Actor is not{Kind:1}||path.Actor.Handle!=checked((int)body.Serial.Value)
            ||path.Mask60!=0x100||path.Size5c is <1 or >5)
        {throw new NotSupportedException("Native ground placement requires an owned prepared Goatman and its supplied live monster path");}
        int x=(ushort)(path.X>>16),y=(ushort)(path.Y>>16);
        if((x+1)/2!=body.X||(y+1)/2!=body.Y||path.Room!=RoomHandle(body.Map,x,y)
            ||unit.Pet.ClassId!=GenericPetSearchTables.SourceId("wargoatman"))
        {throw new NotSupportedException("Native position,room or source identity does not match the supplied path");}
    }
    internal GenericPetNativePlacement(WarlockGoatmanBody body,GenericPetMonsterPlacementUnit unit,GenericPetCommitPath path,Point2D target,
        GenericPetPathTargetView targetMetadata=null,GenericPetPlacementWorld sharedWorld=null)
    {
        RequireActor(body,unit,path);
        if(path.Target is{Kind:1} resetTarget&&(resetTarget.Flags124&4)==0)
        {
            if(targetMetadata==null){throw new NotSupportedException("Monster-target reset requires an explicit primary metadata view");}
            targetMetadata.Require(resetTarget.Variant);
        }
        TargetMetadata=targetMetadata;
        Body=body;Map=body.Map;Path=path;Unit=unit;Target=target;
        SharedWorld=sharedWorld;SharedRevision=sharedWorld?.Revision??0;
        if(sharedWorld!=null){sharedWorld.Require(body,unit,path);}
        int x=(ushort)(path.X>>16),y=(ushort)(path.Y>>16);
        if(target.X<=0||target.Y<=0||target.X>=Map.Width*2||target.Y>=Map.Height*2)
        {throw new NotSupportedException("Native placement target is outside the map");}
        var wanted=new HashSet<(int X,int Y)>();
        void Include(int cx,int cy,int radius)
        {
            for(int ry=Math.Max(0,cy-radius)/Span;ry<=Math.Min(Map.Height*2-1,cy+radius)/Span;ry++)
            for(int rx=Math.Max(0,cx-radius)/Span;rx<=Math.Min(Map.Width*2-1,cx+radius)/Span;rx++){wanted.Add((rx,ry));}
        }
        Include(x,y,2);Include(target.X,target.Y,52);
        // Allocate all neighbors before publishing grid views; recursive box
        // writes must never create/reseed a neighboring buffer mid-operation.
        foreach(var (rx,ry) in wanted)
        {
            int gx=rx*Span,gy=ry*Span,width=Math.Min(Span,Map.Width*2-gx),height=Math.Min(Span,Map.Height*2-gy);
            int handle=RoomHandle(Map,gx,gy);var neighbors=new List<int>();
            for(int ay=ry-1;ay<=ry+1;ay++)for(int ax=rx-1;ax<=rx+1;ax++)
            {if((ax!=rx||ay!=ry)&&wanted.Contains((ax,ay))){neighbors.Add(RoomHandle(Map,ax*Span,ay*Span));}}
            Grids.Add(handle,new(gx,gy,width,height,width,new ushort[width*height],neighbors));
            var bounds=new GenericPetCommitRoom(gx,gy,width,height);
            Rooms.Add(handle,sharedWorld?.EnsureRoom(handle,bounds)??new(handle,bounds,new(handle,0,0,new(0))));
        }
        var cache=new Dictionary<Point2D,ushort>();
        foreach(var grid in Grids.Values)
        {
            var cells=(ushort[])grid.Cells;
            for(int py=0;py<grid.Height;py++)for(int px=0;px<grid.Width;px++)
            {
                int cx=grid.X+px,cy=grid.Y+py;var tile=new Point2D((cx+1)/2,(cy+1)/2);
                if(!cache.TryGetValue(tile,out ushort mask))
                {mask=unchecked((ushort)PetGroundWorld.Collision(body,cx,cy));cache.Add(tile,mask);}
                cells[px+py*grid.Width]=mask;
            }
        }
        int RoomAt(int room,int px,int py)=>SourceRoomTrace.Resolve(room,px,py,id=>Grids[id]);
        SourceCollisionRoom Grid(int handle)=>Grids.GetValueOrDefault(handle);
        var unavailable=new GenericPetCollisionFootprintServices((room,px,py)=>
        {
            int next=RoomAt(room,px,py);var grid=next==0?null:Grid(next);
            return next==0?null:new(next,new(grid.X,grid.Y,grid.Width,(ushort[])grid.Cells));
        },(_,_,_,_)=>throw new InvalidOperationException(),(_,_,_,_)=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),
            (_,_,_,_)=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),
            (_,_,_,_)=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException());
        Footprints=GenericPetCollisionWrites.Bind(GenericPetCollisionReads.Bind(unavailable,new(RoomAt,Grid)),
            new(RoomAt,Grid,(room,index,value)=>((ushort[])Grids[room].Cells)[index]=value));
        // Preserve the caller's footprint even though native fit deliberately
        // excludes the traveller. BeforeSearch removes this exact occupancy.
        GenericPetCollisionFootprints.AddSize(new(path.Room,x,y,path.Size5c,path.Mask60),Footprints);
        if(sharedWorld==null)
        {
            Member=new(path.Actor.Handle,path,new(path.Actor.Handle,0,0)){Flags128=unit.Pet.Flags};
            Links=new(handle=>handle==Member.Handle?Member:throw new NotSupportedException("Unbound source room member"),
                handle=>Rooms.GetValueOrDefault(handle),_=>2);
            GenericPetRoomLinks.Add(Member.Handle,path.Room,Links);
        }
        else{Member=sharedWorld.Member(path.Actor.Handle);Member.Flags128=unit.Pet.Flags;Links=sharedWorld.Links;}
    }
    internal int Place(bool skipSearch=false)
    {
        if(Used||Body.Deleted||Body.Map!=Map){throw new NotSupportedException("Native placement snapshots are single-use and map-bound");}
        SharedWorld?.Begin(SharedRevision);
        Used=true;
        int RoomAt(int room,int x,int y)=>SourceRoomTrace.Resolve(room,x,y,id=>Grids[id]);
        var placement=new GenericPetMonsterPlacementServices(()=>Path.Room,RoomAt,_=>throw new InvalidOperationException(),
            ()=>GenericPetSecondaryTables.Table.Row(Unit.Pet.ClassId)?.SizeX??throw new NotSupportedException("Missing native source sizeX"),
            ()=>GenericPetSecondaryTables.Table.Row(Unit.Pet.ClassId)?.SizeY??throw new NotSupportedException("Missing native source sizeY"),
            _=>throw new InvalidOperationException(),()=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
            ()=>GenericPetRoomLinks.Touch(Member.Handle,Links),_=>Path.Flags&=~2u);
        var commit=GenericPetCollisionFootprints.Bind(new GenericPetPathCommitServices(RoomAt,room=>Rooms[room].Bounds,
            _=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),
            _=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),
            actor=>GenericPetRoomLinks.Remove(actor,Links),(actor,room)=>GenericPetRoomLinks.Add(actor,room,Links),
            actor=>GenericPetRoomLinks.Touch(actor,Links),(selector,id)=>TargetMetadata==null
                ?throw new NotSupportedException("Monster-target reset metadata is not bound"):TargetMetadata.Flags(selector,id),0),Footprints);
        placement=GenericPetMonsterPlacementHooks.Bind(placement,Path,Footprints);
        placement=GenericPetNearbySearch.Bind(placement,GenericPetNearbySearch.WithFootprints(RoomAt,Footprints));
        placement=placement with{UpdatePath=(room,packed)=>
        {
            int x=(ushort)packed,y=(ushort)(packed>>16);
            if(!PetGroundWorld.Point(Body,new(x,y),out var point,true))
            {throw new NotSupportedException("Selected source landing has no supported native floor/height");}
            int result=GenericPetPathCommit.Commit(Path,room,packed,commit);
            Body.MoveToWorld(point,Map);return result;
        }};
        try{return GenericPetMonsterPlacement.Place(Unit,RoomHandle(Map,Target.X,Target.Y),Target.X,Target.Y,skipSearch?1:0,placement);}
        finally{Member.Flags128=Unit.Pet.Flags;}
    }
}
