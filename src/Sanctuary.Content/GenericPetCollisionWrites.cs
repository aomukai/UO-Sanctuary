// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Write must update the same backing buffer returned by Grid. The explicit
// endpoint lets the native grid owner maintain its own storage/lifecycle.
internal sealed record GenericPetCollisionWriteServices(Func<int,int,int,int> RoomAt,
    Func<int,SourceCollisionRoom> Grid,Action<int,long,ushort> Write);

internal static class GenericPetCollisionWrites
{
    private static void Cell(int room,SourceCollisionRoom grid,long index,int mask,bool add,GenericPetCollisionWriteServices s)
    {
        if(grid.Cells==null||index<0||index>=grid.Cells.Count)
        {throw new NotSupportedException("Current collision write has no supplied cell at its raw index");}
        int old=grid.Cells[(int)index];
        s.Write(room,index,unchecked((ushort)(add?old|mask:old&~mask)));
    }
    private static void Point(int room,int x,int y,int mask,bool add,GenericPetCollisionWriteServices s)
    {
        int next=s.RoomAt(room,x,y);var grid=next==0?null:s.Grid(next);
        if(grid?.Cells==null){return;}
        long index=(long)unchecked(x-grid.X)+(long)unchecked(unchecked(y-grid.Y)*grid.Width);
        Cell(next,grid,index,mask,add,s);
    }
    internal static void Adjacent(int room,int x,int y,int mask,bool add,GenericPetCollisionWriteServices s)
    {
        // Every lookup starts with the original room, including after failure.
        Point(room,unchecked(x-1),y,mask,add,s);Point(room,x,y,mask,add,s);
        Point(room,unchecked(x+1),y,mask,add,s);Point(room,x,unchecked(y-1),mask,add,s);
        Point(room,x,unchecked(y+1),mask,add,s);
    }
    private static void GridBox(int room,SourceCollisionRoom grid,GenericPetCollisionBox box,int mask,bool add,GenericPetCollisionWriteServices s)
    {
        int left=unchecked(box.Left-grid.X),right=unchecked(box.Right-grid.X),top=unchecked(box.Top-grid.Y),bottom=unchecked(box.Bottom-grid.Y);
        if(top>bottom){return;}
        long index=(long)unchecked(top*grid.Width)+left;
        long stride=unchecked(unchecked(left-right)-1+grid.Width);int writes=0;
        for(int y=top;;y=unchecked(y+1))
        {
            if(left<=right)
            {
                for(int x=left;;x=unchecked(x+1))
                {
                    if(++writes>10000){throw new NotSupportedException("Current collision box exceeds its supplied write budget");}
                    Cell(room,grid,index++,mask,add,s);if(x==right){break;}
                }
            }
            index+=stride;if(y==bottom){break;}
        }
    }
    internal static void Box(int room,GenericPetCollisionBox box,int mask,bool add,GenericPetCollisionWriteServices s)
    {
        int visits=0;
        void Run(int current,GenericPetCollisionBox area)
        {
            if(++visits>10000){throw new NotSupportedException("Current recursive collision write exceeds its supplied room budget");}
            int next=s.RoomAt(current,area.Left,area.Top);var grid=next==0?null:s.Grid(next);
            if(grid==null){return;}
            var parts=GenericPetCollisionReads.Split(area,grid);if(parts.Length==0){return;}
            GridBox(next,grid,parts[0],mask,add,s);
            for(int i=1;i<parts.Length;i++){Run(next,parts[i]);}
        }
        Run(room,box);
    }
    internal static GenericPetCollisionFootprintServices Bind(GenericPetCollisionFootprintServices footprints,GenericPetCollisionWriteServices s)
        =>footprints with{AddAdjacent=(room,x,y,mask)=>Adjacent(room,x,y,mask,true,s),RemoveAdjacent=(room,x,y,mask)=>Adjacent(room,x,y,mask,false,s),
            AddBox=(room,box,mask)=>Box(room,box,mask,true,s),RemoveBox=(room,box,mask)=>Box(room,box,mask,false,s)};
}
