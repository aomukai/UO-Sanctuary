// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal sealed record SentrySequenceState(int Point=0,int Remaining=15*256,int Action=0);

// Pinned seq_chargesentry: fifteen S2 entries, one event1 at entry2.
// Source mode14 consumes the current action before StopSequence advances it.
internal static class AssassinSentrySequence
{
    internal const int Points=15*256;
    internal static bool Complete(SentrySequenceState state)=>state.Remaining<=0;
    internal static SentrySequenceState Advance(SentrySequenceState state,int speed=256)
        =>AdvanceIn(state,Points,2,speed);
    // Both pinned stationary sequences consist solely of S2 rows and one
    // action1 row. Keep their point count and action index explicit.
    internal static SentrySequenceState AdvanceIn(SentrySequenceState state,int points,int actionIndex,int speed=256)
    {
        ArgumentNullException.ThrowIfNull(state);
        if(points<=0 || points%256!=0 || actionIndex<0 || actionIndex>=points/256
            || state.Point<0 || state.Point>=points || state.Remaining<=0 || speed<0 || speed>points || state.Action is <0 or >1)
        {throw new ArgumentOutOfRangeException();}
        int next=state.Point+speed;if(next>=points){next-=points;}
        // Complete ComputeSequenceAnimation returns the last crossed event.
        // On wrapping to a smaller index its forward-only loop finds none.
        int action=next==state.Point?(next>>8)==actionIndex?1:0
            :((state.Point>>8)+1<=actionIndex && (next>>8)>=actionIndex?1:0);
        return new(next,checked(state.Remaining-speed),action);
    }
}
