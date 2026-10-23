// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal readonly record struct LeapCell(int X,int Y);

// Source subtile coordinates remain integers until the native terrain adapter.
internal static class LeapGeometry
{
    internal static bool Find(LeapCell origin,LeapCell requested,int range,Func<LeapCell,LeapCell?> free,
        Func<LeapCell,bool> clear,Func<LeapCell,bool> flying,out LeapCell destination)
    {
        int dx=requested.X-origin.X,dy=requested.Y-origin.Y;
        int distance=Math.Max(1,SkillGeometry.SourceDistance(dx,dy));
        destination=distance>range?new(origin.X+(int)((long)range*dx/distance),origin.Y+(int)((long)range*dy/distance)):requested;
        int sideX=Math.Clamp(destination.Y-origin.Y,-2,2),sideY=Math.Clamp(origin.X-destination.X,-2,2);
        for(int attempt=0;attempt<3;attempt++)
        {
            int side=attempt==1?1:attempt==2?-1:0;
            var candidate=new LeapCell(destination.X+side*sideX,destination.Y+side*sideY);
            if(free(candidate) is{} cell && clear(cell) && flying(cell)
                && SkillGeometry.SourceDistance(cell.X-origin.X,cell.Y-origin.Y)<=range)
            {destination=cell;return true;}
        }
        return false;
    }
}
