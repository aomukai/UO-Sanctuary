// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal sealed record GenericPetCollisionReadServices(Func<int,int,int,int> RoomAt,Func<int,SourceCollisionRoom> Grid);

// Current point/cardinal/recursive-box reads. Room topology and grid ownership
// are supplied by the world; use the same buffers as footprint writes.
internal static class GenericPetCollisionReads
{
    private static ushort Cell(SourceCollisionRoom grid,long index)
    {
        if(grid.Cells==null||index<0||index>=grid.Cells.Count)
        {throw new NotSupportedException("Current collision read has no supplied cell at its raw index");}
        return grid.Cells[(int)index];
    }
    private static long Index(SourceCollisionRoom grid,int x,int y)
        =>(long)unchecked(x-grid.X)+(long)unchecked(unchecked(y-grid.Y)*grid.Width);

    internal static int Point(int room,int x,int y,int mask,GenericPetCollisionReadServices s)
    {
        int next=s.RoomAt(room,x,y);var grid=next==0?null:s.Grid(next);
        int value=grid?.Cells==null?0x27:Cell(grid,Index(grid,x,y));
        return unchecked((ushort)(value&mask));
    }
    internal static int Adjacent(int room,int x,int y,int mask,GenericPetCollisionReadServices s)
    {
        int next=s.RoomAt(room,x,y);var grid=next==0?null:s.Grid(next);
        if(grid?.Cells==null){return unchecked((ushort)mask);}
        int edges=(grid.X>=x?1:0)+(grid.Y>=y?2:0)
            +(unchecked(grid.X+grid.Width)<=unchecked(x+1)?4:0)
            +(unchecked(grid.Y+grid.Height)<=unchecked(y+1)?8:0);
        if(edges is not(0 or 1 or 2 or 3 or 4 or 6 or 8 or 9 or 12)){return unchecked((ushort)mask);}
        int value=0;
        // Edge helpers run before direct reads. Keep vertical before horizontal
        // at corners, matching the current routine rather than sorting cells.
        if((edges&2)!=0){value|=Point(next,x,unchecked(y-1),mask,s);}
        if((edges&8)!=0){value|=Point(next,x,unchecked(y+1),mask,s);}
        if((edges&1)!=0){value|=Point(next,unchecked(x-1),y,mask,s);}
        if((edges&4)!=0){value|=Point(next,unchecked(x+1),y,mask,s);}
        long index=Index(grid,x,y),width=grid.Width;int direct=0;
        if((edges&2)==0){direct|=Cell(grid,index-width);}
        if((edges&8)==0){direct|=Cell(grid,index+width);}
        if((edges&1)==0){direct|=Cell(grid,index-1);}
        if((edges&4)==0){direct|=Cell(grid,index+1);}
        direct|=Cell(grid,index);
        return unchecked((ushort)(value|(direct&mask)));
    }
    internal static GenericPetCollisionBox[] Split(GenericPetCollisionBox box,SourceCollisionRoom grid)
    {
        if(box.Top>box.Bottom||box.Left>box.Right||grid==null){return [];}
        var first=box;var pending=new List<GenericPetCollisionBox>();
        int endX=unchecked(grid.X+grid.Width),endY=unchecked(grid.Y+grid.Height);
        if(box.Right>=endX)
        {
            first=first with{Right=unchecked(endX-1)};
            pending.Add(new(endX,box.Top,box.Right,box.Bottom));
        }
        if(box.Bottom>=endY)
        {
            first=first with{Bottom=unchecked(endY-1)};
            pending.Add(new(box.Left,endY,first.Right,box.Bottom));
        }
        pending.Insert(0,first);return pending.ToArray();
    }
    private static int GridBox(SourceCollisionRoom grid,GenericPetCollisionBox box,int mask)
    {
        int left=unchecked(box.Left-grid.X),right=unchecked(box.Right-grid.X),top=unchecked(box.Top-grid.Y),bottom=unchecked(box.Bottom-grid.Y);
        if(top>bottom){return 0;}
        long index=(long)unchecked(top*grid.Width)+left;
        long stride=unchecked(unchecked(left-right)-1+grid.Width);int value=0,reads=0;
        for(int y=top;;y=unchecked(y+1))
        {
            if(left<=right)
            {
                for(int x=left;;x=unchecked(x+1))
                {
                    if(++reads>10000){throw new NotSupportedException("Current collision box exceeds its supplied read budget");}
                    value|=Cell(grid,index++)&mask;if(x==right){break;}
                }
            }
            index+=stride;if(y==bottom){break;}
        }
        return unchecked((ushort)value);
    }
    internal static int Box(int room,GenericPetCollisionBox box,int mask,GenericPetCollisionReadServices s)
    {
        int visits=0;
        int Run(int current,GenericPetCollisionBox area)
        {
            if(++visits>10000){throw new NotSupportedException("Current recursive collision box exceeds its supplied room budget");}
            int next=s.RoomAt(current,area.Left,area.Top);var grid=next==0?null:s.Grid(next);
            if(grid==null){return 0x27;}
            var parts=Split(area,grid);if(parts.Length==0){return 0x27;}
            int value=GridBox(grid,parts[0],mask);
            for(int i=1;i<parts.Length;i++){value|=Run(next,parts[i]);}
            return unchecked((ushort)value);
        }
        return Run(room,box);
    }
    internal static GenericPetCollisionFootprintServices Bind(GenericPetCollisionFootprintServices footprints,GenericPetCollisionReadServices s)
        =>footprints with{CheckPoint=(room,x,y,mask)=>Point(room,x,y,mask,s),CheckAdjacent=(room,x,y,mask)=>Adjacent(room,x,y,mask,s),CheckBox=(room,box,mask)=>Box(room,box,mask,s)};
}
