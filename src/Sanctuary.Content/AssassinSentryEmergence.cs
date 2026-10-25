// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

// Ordinary source animation scheduler, with NPC frame bonus0 and no action
// events. The cached LG S1 record has15frames/speed256; acquisition provenance
// is incomplete, so the retained profile explicitly identifies that adapter.
internal static class AssassinSentryEmergence
{
    internal const string Profile="cached-lg-s1-15-v1";
    internal static int EndDelay(int speed=256)
    {
        if(speed is <0 or >32767){throw new ArgumentOutOfRangeException(nameof(speed));}
        return speed==0?1:Math.Max(2,(15*256+speed-1)/speed);
    }
    internal static int NeutralThink(int frame,int scheduled,bool stunned=false)
    {
        if(frame<0 || scheduled<0){throw new ArgumentOutOfRangeException();}
        return scheduled>frame?scheduled:checked(frame+(stunned?45:15));
    }
}
