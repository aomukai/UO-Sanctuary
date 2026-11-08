// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal static class NpcMissileCritical
{
    // Concrete proposed policy only. Intrinsic Shadow AI does not call this
    // until the legacy signed-roll discrepancy has an approved resolution.
    internal static bool ProposedUnsigned(int passive,int deadly,int mastery,Func<uint> raw)
    {
        if(passive<0||deadly<0||mastery<0){throw new NotSupportedException("Negative NPC missile critical chances need a source policy");}
        ArgumentNullException.ThrowIfNull(raw);
        if(raw()%100<(uint)passive){return true;}
        if(deadly!=0&&raw()%100<(uint)deadly){return true;}
        return mastery!=0&&raw()%100<(uint)mastery;
    }
}
