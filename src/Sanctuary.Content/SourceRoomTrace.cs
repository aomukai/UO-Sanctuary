// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

// Source error-floor stepping,shared by wall-follow paths and collision rays.
internal struct SourceLineCursor
{
    internal readonly int DX,DY,SX,SY;
    private int Error;
    internal SourceLineCursor(int x,int y,int tx,int ty)
    {
        int dx=unchecked(tx-x),dy=unchecked(ty-y);DX=dx<0?unchecked(-dx):dx;DY=dy<0?unchecked(-dy):dy;
        SX=dx<0?-1:1;SY=dy<0?-1:1;Error=0;
    }
    internal bool XMajor=>DX!=0&&(DY==0||DX>=DY);
    internal void Advance(ref int x,ref int y,out int stepX,out int stepY)
    {
        stepX=stepY=0;
        unchecked
        {
            if(DX==0){y+=SY;stepY=SY;return;}if(DY==0){x+=SX;stepX=SX;return;}
            if(XMajor){x+=SX;stepX=SX;Error+=DY;if(Error>=DX){Error-=DX;y+=SY;stepY=SY;}}
            else{y+=SY;stepY=SY;Error+=DX;if(Error>=DY){Error-=DY;x+=SX;stepX=SX;}}
        }
    }
}
internal sealed class SourceGridPoint(int x,int y)
{internal int X=x,Y=y;}
internal sealed record SourceCollisionRoom(int X,int Y,int Width,int Height,int Pitch,IReadOnlyList<ushort> Cells,IReadOnlyList<int> Adjacent);
internal static class SourceRoomTrace
{
    private static bool Inside(SourceCollisionRoom r,int x,int y)=>x>=r.X&&x<unchecked(r.X+r.Width)&&y>=r.Y&&y<unchecked(r.Y+r.Height);
    internal static int Resolve(int room,int x,int y,Func<int,SourceCollisionRoom> get)
    {
        if(room==0){return 0;}var current=get(room);if(Inside(current,x,y)){return room;}
        foreach(int next in current.Adjacent){if(next!=0&&Inside(get(next),x,y)){return next;}}return 0;
    }
    internal static int Run(int room,SourceGridPoint origin,SourceGridPoint target,int mask,Func<int,SourceCollisionRoom> get)
    {
        ArgumentNullException.ThrowIfNull(get);
        if(origin==null||target==null){throw new NotSupportedException("Current trace dereferences both point records");}
        int x=origin.X,y=origin.Y,tx=target.X,ty=target.Y;var line=new SourceLineCursor(x,y,tx,ty);long index=0;bool reset=true;
        int Blocked(){target.X=x;target.Y=y;return 1;}
        if(room==0){return Blocked();}
        while(true)
        {
            var r=get(room);
            if(!Inside(r,x,y))
            {
                room=Resolve(room,x,y,get);if(room==0){return Blocked();}r=get(room);
                if(!Inside(r,x,y)){return Blocked();}reset=true;
            }
            if(reset){index=unchecked((int)(unchecked(y-r.Y)*r.Pitch))+(long)unchecked(x-r.X);reset=false;}
            if(r.Cells==null||index<0||index>=r.Cells.Count){throw new NotSupportedException("Trace collision buffer is missing or out of range");}
            if((r.Cells[(int)index]&mask)!=0){return Blocked();}
            if(line.DX==0&&line.DY==0){return 0;}
            line.Advance(ref x,ref y,out int stepX,out int stepY);
            index=unchecked(index+stepX+(long)unchecked(r.Width*stepY));
            // The source tests the one-past major coordinate before resolving
            // another room,including after reading the destination cell.
            if(line.XMajor?x==unchecked(tx+line.SX):y==unchecked(ty+line.SY)){return 0;}
        }
    }
}
