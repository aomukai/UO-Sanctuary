// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed class GenericPetWarpSearchRoom(int handle,int x,int y,int width,int height,ulong seed)
{
    internal readonly int Handle=handle,X=x,Y=y,Width=width,Height=height;
    internal ulong Seed=seed;
}
internal sealed record GenericPetWarpSearchRegion(int Handle,int Id,int MinX,int MinY,int MaxX,int MaxY);
internal sealed record GenericPetWarpCandidate(int Game,int Room,int Region,int ClassId,int X,int Y)
{
    internal int Word28=>1;internal int Word32=>0;internal int Word44=>-1;internal ushort Word48=>1;internal long Pointer56=>0;
}
internal sealed record GenericPetWarpSearchServices(Func<int,int,int,int> RoomAt,
    Func<int,int,int,int?> Region,Func<GenericPetWarpCandidate,long> Candidate);
internal static class GenericPetWarpSearch
{
    // Actual warp caller supplies arg7=0. The unrelated nonzero restriction
    // path is not selected by this interface or guessed here.
    internal static bool Find(int game,GenericPetWarpSearchRoom room,GenericPetWarpSearchRegion region,int classId,
        GenericPetWarpSearchServices services,ref int outX,ref int outY)
    {
        if(room==null){throw new NotSupportedException("Current warp search requires its room-owned RNG");}
        int x,y,width,height;
        if(region==null)
        {x=unchecked(room.X+1);y=unchecked(room.Y+1);width=unchecked(room.Width-1);height=unchecked(room.Height-1);}
        else
        {x=unchecked(region.MinX*5+1);y=unchecked(region.MinY*5+1);width=unchecked(region.MaxX*5-x);height=unchecked(region.MaxY*5-y);}
        for(int attempt=0;attempt<20;attempt++)
        {
            int px=unchecked(x+(int)SourceRandom.Limited(ref room.Seed,width));
            int py=unchecked(y+(int)SourceRandom.Limited(ref room.Seed,height));
            if(region!=null)
            {
                int candidateRoom=services.RoomAt(room.Handle,px,py);
                int id=candidateRoom==0?0:services.Region(candidateRoom,px/5,py/5)??-1;
                if(id!=region.Id){continue;}
            }
            var candidate=new GenericPetWarpCandidate(game,room.Handle,region?.Handle??0,classId,px,py);
            if(services.Candidate(candidate)==0){continue;}
            outX=px;outY=py;return true;
        }
        return false;
    }
    internal static GenericPetWarpServices Bind(GenericPetWarpServices warp,int game,
        Func<int,GenericPetWarpSearchRoom> room,Func<int,GenericPetWarpSearchRegion> region,GenericPetWarpSearchServices services)
        =>warp with{Find=(roomId,regionId,classId)=>
        {
            var r=room(roomId)??throw new NotSupportedException("Missing source search room");
            var area=regionId==0?null:region(regionId)??throw new NotSupportedException("Missing source search region");
            int x=0,y=0;bool found=Find(game,r,area,classId,services,ref x,ref y);return new(found?1:0,x,y);
        }};
}
