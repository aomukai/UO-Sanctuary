// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal enum BlindAiAction { Attack, WalkNear, Idle }
internal readonly record struct BlindAiRequest(BlindAiAction Action,int Value=0);

// Special-state10's mode1 target wrapper and shared10/17 decision/tactics.
// Target acquisition, mode entry, path completion and event timing are services;
// returning a request does not grant native movement or an immediate attack.
internal static class BlindAiPolicy
{
    internal static BlindAiRequest Decide(bool target,bool combat,bool validClass,bool hasMonster,
        bool interact,bool hasModes,bool attack1,bool walk,bool escape,bool collision,int distance,Func<int> percentage)
    {
        ArgumentNullException.ThrowIfNull(percentage);
        if(!target)
        {
            // sub_6FCCF9D0 returns before the blind tick on this branch.
            if((escape||collision)&&hasModes&&walk){return new(BlindAiAction.WalkNear,5);}
            return new(BlindAiAction.Idle,distance>=35?25:distance>=25?distance-10:10);
        }
        if(combat&&validClass&&(!hasMonster||!interact)&&hasModes&&attack1)
        {return new(BlindAiAction.Attack);}
        int roll=percentage();
        if(roll is <0 or >99){throw new ArgumentOutOfRangeException(nameof(percentage));}
        return roll<20?new(BlindAiAction.WalkNear,3):new(BlindAiAction.Idle,10);
    }

    internal static Point2D Offset(int distance,Func<uint> random,Func<int,int> limited)
    {
        ArgumentNullException.ThrowIfNull(random);ArgumentNullException.ThrowIfNull(limited);
        if(distance<=0){throw new NotSupportedException("Blind wandering requires a positive source distance");}
        bool first=(random()&1)!=0;
        int small=limited(distance);
        if(small<0||small>=distance){throw new ArgumentOutOfRangeException(nameof(limited));}
        int x=first?distance:small,y=first?small:distance;
        if((random()&1)!=0){x=-x;}
        if((random()&1)!=0){y=-y;}
        return new(x,y);
    }
}
