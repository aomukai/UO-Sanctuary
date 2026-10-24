// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sanctuary;

public readonly record struct SourceMissileCell(int X,int Y);
public sealed record SourceStepTrace(SourceMissileCell[] Cells)
{
    public bool Equals(SourceStepTrace other)=>other!=null && Cells.SequenceEqual(other.Cells);
    public override int GetHashCode(){var h=new HashCode();foreach(var c in Cells){h.Add(c);}return h.ToHashCode();}
}
internal static class SourceMissileSteps
{
    // Complete stable-velocity path stepping uses vector downshifts followed
    // by ordered integer-subtile crossings; retained steps handle subcell moves.
    internal static SourceStepTrace Crossings(long x,long y,int vx,int vy)
    {
        long endX=x+vx,endY=y+vy;int sx=vx,sy=vy;
        while(sx < -65536 || sx>65536){sx>>=1;sy>>=1;}
        while(sy < -65536 || sy>65536){sx>>=1;sy>>=1;}
        var cells=new List<SourceMissileCell>();int cx=(int)(x>>16),cy=(int)(y>>16);
        while(cx!=(int)(endX>>16) || cy!=(int)(endY>>16))
        {
            x+=sx;y+=sy;int nx=(int)(x>>16),ny=(int)(y>>16);
            if(nx!=cx || ny!=cy){cells.Add(new(nx,ny));if(cells.Count==10){break;}}
            cx=nx;cy=ny;
        }
        return new(cells.ToArray());
    }
    internal static SourceStepTrace Advance(WeaponFlight flight)
    {
        var trace=Crossings((long)Math.Round(flight.X*131072+32768),(long)Math.Round(flight.Y*131072+32768),
            (int)Math.Round(flight.Vx*131072),(int)Math.Round(flight.Vy*131072));
        return trace.Cells.Length>0?trace:flight.SourceSteps;
    }
}
