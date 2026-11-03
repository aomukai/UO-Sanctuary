// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Raw current monster-row bytes used by the validation-only request.
internal sealed record GenericPetWarpCandidateStats(byte ByteA,sbyte Byte8);
internal sealed record GenericPetWarpCandidateServices(Func<byte,int,bool> SecondaryPresent,
    Func<byte,int,GenericPetWarpCandidateStats> Primary,Func<int,int,(int X,int Y)> SpecialPoint,
    Func<int,int,int,int> RoomAt,Func<int,int,int,int?> Region,
    Func<byte,int,int,int,int,int,int> Allowed,Func<int,int,int,int,int> PointCollision,
    Func<int,int,int,int,int> TwoCollision,Func<int,int,int,int,int,int,int> BoxCollision);
internal static class GenericPetWarpCandidateCheck
{
    internal static long Check(GenericPetWarpCandidate request,byte selector,GenericPetWarpSearchRoom room,
        GenericPetWarpSearchRegion region,GenericPetWarpCandidateServices services)
    {
        // This immutable request has word44=-1(radius0) and word48=1(test
        // only). The source allocation and other radius/flag paths are not used.
        if(!services.SecondaryPresent(selector,request.ClassId)){return 0;}
        var stats=services.Primary(selector,request.ClassId);if(stats==null||room==null){return 0;}
        if(stats.ByteA==1&&unchecked((uint)(request.ClassId-258))>5&&request.ClassId!=153)
        {
            var point=services.SpecialPoint(0,room.Handle);services.RoomAt(room.Handle,point.X,point.Y);
            return point.X!=0&&point.Y!=0?1:0;
        }
        // Radius0 still consumes the original side and two sign draws. The
        // single walk step returns to the supplied point; no offsets survive.
        SourceRandom.Roll(ref room.Seed);SourceRandom.Roll(ref room.Seed);SourceRandom.Roll(ref room.Seed);
        int x=request.X,y=request.Y;
        int left=region==null?room.X:unchecked(region.MinX*5),top=region==null?room.Y:unchecked(region.MinY*5);
        int right=region==null?unchecked(room.X+room.Width):unchecked(region.MaxX*5),bottom=region==null?unchecked(room.Y+room.Height):unchecked(region.MaxY*5);
        if(x<left||x>=right||y<top||y>=bottom){return 0;}
        if(region!=null)
        {
            int handle=services.RoomAt(room.Handle,x,y);int id=handle==0?0:services.Region(handle,x/5,y/5)??-1;
            if(id!=region.Id){return 0;}
        }
        if(services.Allowed(selector,request.ClassId,room.Handle,x,y,1)==0){return 0;}
        int mask=stats.ByteA switch{1=>0x1c0,2=>0x3f11,3=>0,_=>0x3c01};
        int collision;
        switch(stats.Byte8)
        {
            case 0:case 1:collision=services.PointCollision(room.Handle,x,y,mask);break;
            case 2:collision=services.TwoCollision(room.Handle,x,y,mask);break;
            case 3:collision=services.BoxCollision(room.Handle,unchecked(x-1),unchecked(y-1),unchecked(x+1),unchecked(y+1),mask);break;
            default:return 0;
        }
        return unchecked((ushort)collision)==0&&x!=0&&y!=0?1:0;
    }
    internal static GenericPetWarpSearchServices Bind(GenericPetWarpSearchServices search,byte selector,
        Func<int,GenericPetWarpSearchRoom> room,Func<int,GenericPetWarpSearchRegion> region,GenericPetWarpCandidateServices services)
        =>search with{Candidate=request=>Check(request,selector,room(request.Room),request.Region==0?null:region(request.Region),services)};
}
