// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Native enumeration/height/LOS adapters around the existing complete source
// selector. Caller supplies actor pose, eligibility and its path-query service.
internal static class NpcRangedTargets
{
    internal static SentryChoice Select(BaseCreature body,Point2D origin,Func<Mobile,bool> eligible,
        Func<Mobile,bool> primaryPath,Func<BaseCreature,int> size=null)
    {
        var candidates=new List<SentryCandidate>();
        foreach(var target in body.Map.GetMobilesInRange<BaseCreature>(body.Location,26))
        {
            if(Math.Abs(target.Z-body.Z)>=16||SpellHelper.IsTown(target.Location,target)||!eligible(target)){continue;}
            var profile=CreatureProfiles.For(target);
            int footprint=size?.Invoke(target)??(profile.Traits.Size is >=0 and <=3 and var mapped?mapped
                :throw new NotSupportedException("Ranged target has no mapped source size: "+target.GetType().FullName));
            candidates.Add(new(target.Serial.Value,target.X*2,target.Y*2,footprint,Threat:profile.Targeting.Threat));
        }
        return AssassinSentryTargets.Select(origin,candidates,_=>true,
            candidate=>!body.InLOS(World.FindMobile((Serial)candidate.Serial)),
            candidate=>primaryPath(World.FindMobile((Serial)candidate.Serial)));
    }
}
