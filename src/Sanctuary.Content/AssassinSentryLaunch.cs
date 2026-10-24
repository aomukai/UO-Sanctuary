// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal sealed record SentryBoltSpawn(int Ordinal,Point2D Aim,uint Seed,int Frames,int Distance);

// Complete do17 dispatch and charged-bolt initialization contract. Coordinates
// are source cells, matching the existing ChargedBolts.SeedSubtile helper.
internal static class AssassinSentryLaunch
{
    internal static bool Dispatch(bool skillRow,bool validMissile,int count,int range,
        Func<int,Point2D?> target,Func<SentryBoltSpawn,bool> create)
    {
        ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(create);
        if(!skillRow || !validMissile){return false;}
        if(count<0 || range is <0 or >256){throw new ArgumentOutOfRangeException();}
        for(int ordinal=0;ordinal<count;ordinal++)
        {
            var aim=target(ordinal);if(aim is not{} point || point.X==0 || point.Y==0){continue;}
            int frames=Math.Min(range,77); // The source updates its local count before passing it to the byte-distance setter.
            var spawn=new SentryBoltSpawn(ordinal,point,ChargedBolts.SeedSubtile(point.X,ordinal),frames,frames);
            // The source ignores individual factory allocation failure.
            create(spawn);
        }
        return true;
    }
}
