// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal static class AssassinSentryWorldTargets
{
    // The complete type2 setter/initializer composition bounds reuse of the
    // toward kernel for this stationary zero-lead unit-target query. Other
    // unit-path contexts and the legacy pointer-clear quirk remain separate.
    internal static Func<AssassinSentryBody,Mobile,bool> PrimaryPath;
    internal static SentryTarget? Select(AssassinSentryBody body)
    {
        var state=body.Prepared;
        if(World.FindMobile((Serial)state.Owner) is not PlayerMobile owner){throw new NotSupportedException("Missing sentry allegiance supplier");}
        // Native spatial/height/LOS adapter, not D2 room-enumeration equivalence.
        // Size <=3 and source distance48 fit inside this26-tile native query.
        var result=NpcRangedTargets.Select(body,new(body.X*2,body.Y*2),target=>body.TrapTarget(owner,target),
            target=>(PrimaryPath??throw new NotSupportedException("Sentry stationary unit-target path service is not connected"))(body,target));
        // Native sentries currently have no source attacker-override AI record.
        // The core supports an explicitly resolved remembered target; it is not
        // confused with Control.Target, which is the pending volley's aim.
        return result==null?null:new(result.Candidate.Serial,result.Distance);
    }
}
