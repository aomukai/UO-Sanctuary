// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed class GenericPetNearbyPoint(int x,int y){internal int X=x,Y=y;}
internal sealed record GenericPetNearbyServices(Func<int,int,int,int> RoomAt,Func<GenericPetFootprintRequest,int> Check);

// Current no-field branch used by real pet relocation. The unrelated optional
// field allocator/line validator is deliberately not substituted by this API.
internal static class GenericPetNearbySearch
{
    private static int Abs(int n){int sign=n>>31;return unchecked((n^sign)-sign);}
    internal static int Find(int room,GenericPetNearbyPoint point,int pattern,int mask,int fallback,int distance,int increment,GenericPetNearbyServices s)
    {
        if(point==null){throw new NotSupportedException("Current nearby search requires its coordinate pointer");}
        int originX=point.X,originY=point.Y,queries=0;
        int At(int current,int x,int y)
        {if(++queries>10000){throw new NotSupportedException("Current nearby search exceeded its verified query bound");}return s.RoomAt(current,x,y);}
        room=At(room,originX,originY);
        if(room!=0&&unchecked((ushort)s.Check(new(room,point.X,point.Y,pattern,mask)))==0){return room;}
        int best=-1;
        if(distance>1)
        {
            if(distance>50||increment is <1 or >50||originX<int.MinValue+200||originX>int.MaxValue-200||originY<int.MinValue+200||originY>int.MaxValue-200)
            {throw new NotSupportedException("Unverified nearby-search range,increment or extreme coordinate arithmetic");}
            void Candidate(int x,int y)
            {
                int found=At(room,x,y);if(found==0){return;}room=found;
                if(unchecked((ushort)s.Check(new(room,x,y,pattern,mask)))!=0){return;}
                int score=unchecked(Abs(unchecked(x-originX))+Abs(unchecked(y-originY)));
                if(best==-1||score<best){best=score;point.X=x;point.Y=y;}
            }
            int left=originX,bottom=unchecked(originY+1),right=originX,top=unchecked(originY-1),span=2;
            do
            {
                best=-1;
                for(int y=top;y<=bottom;y=unchecked(y+increment))
                {for(int x=unchecked(left-1);x<=unchecked(right+1);x=unchecked(x+span)){Candidate(x,y);}}
                for(int x=left;x<=right;x=unchecked(x+increment))
                {for(int y=top;y<=bottom;y=unchecked(y+span)){Candidate(x,y);}}
                if(best!=-1){break;}
                left=unchecked(left-increment);top=unchecked(top-increment);right=unchecked(right+increment);
                span=unchecked(span+2*increment);bottom=unchecked(bottom+increment);
            }while(unchecked(right+1-originX)<distance);
        }
        return best==-1&&fallback==0?0:At(room,point.X,point.Y);
    }
    internal static GenericPetMonsterPlacementServices Bind(GenericPetMonsterPlacementServices placement,GenericPetNearbyServices search)
        =>placement with{Nearby=r=>
        {
            if(r.Zero!=0){throw new NotSupportedException("Optional field-constrained nearby placement remains unsupported");}
            var point=new GenericPetNearbyPoint(r.X,r.Y);int room=Find(r.Room,point,r.Word180,r.Mask1,r.Zero2,r.Distance,r.One,search);
            return new(room,point.X,point.Y);
        }};
    internal static GenericPetNearbyServices WithFootprints(Func<int,int,int,int> roomAt,GenericPetCollisionFootprintServices footprints)
        =>new(roomAt,r=>GenericPetCollisionFootprints.CheckPattern(r,footprints));
}
