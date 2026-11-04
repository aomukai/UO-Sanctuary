// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

// Field-null,size2,max50,increment1 source search. The caller supplies actual
// room/footprint clearance. This flag controls final blocked-origin fallback;
// it does not prohibit candidate checks across source room boundaries.
internal static class SourceFreeCoordinates
{
    internal static LeapCell? Find(LeapCell requested,Func<LeapCell,bool> clear,bool allowBlockedOriginFallback=false)
    {
        if(clear(requested)){return requested;}
        for(int radius=1;radius<50;radius++)
        {
            LeapCell? best=null;int score=int.MaxValue;
            void Consider(int dx,int dy)
            {
                var cell=new LeapCell(requested.X+dx,requested.Y+dy);int distance=Math.Abs(dx)+Math.Abs(dy);
                if(clear(cell)&&distance<score){score=distance;best=cell;}
            }
            for(int y=-radius;y<=radius;y++){Consider(-radius,y);Consider(radius,y);}
            for(int x=1-radius;x<radius;x++){Consider(x,-radius);Consider(x,radius);}
            if(best is{} found){return found;}
        }
        return allowBlockedOriginFallback?requested:null;
    }
}
