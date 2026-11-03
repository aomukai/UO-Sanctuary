// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record GenericPetCollisionBox(int Left,int Top,int Right,int Bottom);
internal sealed class GenericPetCollisionGrid(int x,int y,int width,ushort[] cells)
{internal readonly int X=x,Y=y,Width=width;internal readonly ushort[] Cells=cells;}
internal sealed record GenericPetCollisionRoom(int Handle,GenericPetCollisionGrid Grid);
internal sealed record GenericPetCollisionFootprintServices(Func<int,int,int,GenericPetCollisionRoom> RoomAt,
    Func<int,int,int,int,int> CheckPoint,Func<int,int,int,int,int> CheckAdjacent,Func<int,GenericPetCollisionBox,int,int> CheckBox,
    Action<int,int,int,int> AddAdjacent,Action<int,GenericPetCollisionBox,int> AddBox,
    Action<int,int,int,int> RemoveAdjacent,Action<int,GenericPetCollisionBox,int> RemoveBox);

// Current pattern0..3 and size0..5 dispatch. Adjacent/box kernels, source room
// lookup and native collision ownership remain separate world dependencies.
internal static class GenericPetCollisionFootprints
{
    private static GenericPetCollisionBox Box(GenericPetFootprintRequest r)=>new(unchecked(r.X-1),unchecked(r.Y-1),unchecked(r.X+1),unchecked(r.Y+1));
    private static void Cell(GenericPetFootprintRequest r,int mask,bool add,GenericPetCollisionFootprintServices s)
    {
        var grid=s.RoomAt(r.Room,r.X,r.Y)?.Grid;if(grid?.Cells==null){return;}
        // The source wraps the two int32 subtractions and multiplication
        // before sign-extending each component for pointer arithmetic.
        long index=(long)unchecked(r.X-grid.X)+(long)unchecked(unchecked(r.Y-grid.Y)*grid.Width);
        if(index<0||index>=grid.Cells.Length){throw new NotSupportedException("Current footprint cell lies outside its supplied source grid");}
        ushort bits=unchecked((ushort)mask);
        grid.Cells[index]=add?(ushort)(grid.Cells[index]|bits):(ushort)(grid.Cells[index]&~bits);
    }
    internal static int CheckPattern(GenericPetFootprintRequest r,GenericPetCollisionFootprintServices s)=>r.Pattern switch
    {
        0 or 1=>s.CheckPoint(r.Room,r.X,r.Y,r.Mask),2=>s.CheckAdjacent(r.Room,r.X,r.Y,r.Mask),
        3=>s.CheckBox(r.Room,Box(r),r.Mask),_=>0xffff
    };
    internal static void AddPattern(GenericPetFootprintRequest r,GenericPetCollisionFootprintServices s)
    {
        switch(r.Pattern)
        {case 1:Cell(r,r.Mask,true,s);break;case 2:s.AddAdjacent(r.Room,r.X,r.Y,r.Mask);break;case 3:s.AddBox(r.Room,Box(r),r.Mask);break;}
    }
    internal static void RemovePattern(GenericPetFootprintRequest r,GenericPetCollisionFootprintServices s)
    {
        if(r.Room==0){return;}
        switch(r.Pattern)
        {case 1:Cell(r,r.Mask,false,s);break;case 2:s.RemoveAdjacent(r.Room,r.X,r.Y,r.Mask);break;case 3:s.RemoveBox(r.Room,Box(r),r.Mask);break;}
    }
    private static void Size(GenericPetFootprintRequest r,GenericPetCollisionFootprintServices s,bool add)
    {
        if(!add&&r.Room==0||unchecked((uint)r.Pattern)>5||r.Pattern==0){return;}
        var adjacent=add?s.AddAdjacent:s.RemoveAdjacent;var box=add?s.AddBox:s.RemoveBox;
        if(r.Pattern is 2 or 4){box(r.Room,Box(r),r.Mask);}else{adjacent(r.Room,r.X,r.Y,r.Mask);}
        // Test the full mask, not its low16 bits, before adding/removing the
        // center/adjacent occupancy lane for size1..4.
        if(r.Mask==0||r.Pattern==5){return;}
        int extra=r.Pattern<=2?0x1000:0x2000;
        if(r.Pattern is 1 or 3){Cell(r,extra,add,s);}else{adjacent(r.Room,r.X,r.Y,extra);}
    }
    internal static void AddSize(GenericPetFootprintRequest r,GenericPetCollisionFootprintServices s)=>Size(r,s,true);
    internal static void RemoveSize(GenericPetFootprintRequest r,GenericPetCollisionFootprintServices s)=>Size(r,s,false);
    internal static GenericPetPathCommitServices Bind(GenericPetPathCommitServices path,GenericPetCollisionFootprintServices s)
        =>path with{RemovePattern=r=>RemovePattern(r,s),CheckPattern=r=>CheckPattern(r,s),AddPattern=r=>AddPattern(r,s),RemoveSize=r=>RemoveSize(r,s),AddSize=r=>AddSize(r,s)};
}
