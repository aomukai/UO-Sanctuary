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
    // The source probes a unit-target path, including target adjustment. The
    // existing point-target path kernel must not stand in for that service.
    internal static Func<AssassinSentryBody,Mobile,bool> PrimaryPath;
    internal static SentryTarget? Select(AssassinSentryBody body)
    {
        var state=body.Prepared;
        if(World.FindMobile((Serial)state.Owner) is not PlayerMobile owner){throw new NotSupportedException("Missing sentry allegiance supplier");}
        // Native spatial/height/LOS adapter, not D2 room-enumeration equivalence.
        // Size <=3 and source distance48 fit inside this26-tile native query.
        var targets=new List<BaseCreature>();
        foreach(var x in body.Map.GetMobilesInRange<BaseCreature>(body.Location,26))
        {if(Math.Abs(x.Z-body.Z)<16 && !SpellHelper.IsTown(x.Location,x) && body.TrapTarget(owner,x)){targets.Add(x);}}
        var candidates=targets.Select(x=>
        {
            var profile=CreatureProfiles.For(x);
            if(profile.Traits.Size is not(>=0 and <=3)){throw new NotSupportedException("Sentry target has no mapped source size: "+x.GetType().FullName);}
            return new SentryCandidate(x.Serial.Value,x.X*2,x.Y*2,profile.Traits.Size.Value,Threat:profile.Targeting.Threat);
        }).ToArray();
        var result=AssassinSentryTargets.Select(new(body.X*2,body.Y*2),candidates,_=>true,
            candidate=>!body.InLOS(World.FindMobile((Serial)candidate.Serial)),
            candidate=>(PrimaryPath??throw new NotSupportedException("Sentry low-threat alternative needs an unmapped unit-target path probe"))(body,World.FindMobile((Serial)candidate.Serial)));
        // Native sentries currently have no source attacker-override AI record.
        // The core supports an explicitly resolved remembered target; it is not
        // confused with Control.Target, which is the pending volley's aim.
        return result==null?null:new(result.Candidate.Serial,result.Distance);
    }
}
