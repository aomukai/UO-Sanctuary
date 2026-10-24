// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal sealed record SentryCandidate(uint Serial,int X,int Y,int Size,bool Player=false,int? Threat=null);
internal sealed record SentryChoice(SentryCandidate Candidate,int Distance);
internal sealed record SentryScan(SentryChoice Primary=null,SentryChoice Alternative=null);

// Complete callback6/callback7 and alternative-target choice, with eligibility,
// missile-barrier visibility, room enumeration and unit-target path as services.
internal static class AssassinSentryTargets
{
    internal static int Distance(Point2D origin,SentryCandidate candidate)
    {
        if(candidate==null || candidate.Serial==0 || candidate.Size is <0 or >8){throw new ArgumentException("Invalid sentry target metadata");}
        int x=Math.Abs(Math.Abs(candidate.X-origin.X)-candidate.Size),y=Math.Abs(Math.Abs(candidate.Y-origin.Y)-candidate.Size);
        return checked(Math.Min(x,y)+2*Math.Max(x,y))/2;
    }
    internal static SentryChoice Select(Point2D origin,IEnumerable<SentryCandidate> candidates,
        Func<SentryCandidate,bool> eligible,Func<SentryCandidate,bool> barrier,
        Func<SentryCandidate,bool> primaryPath,SentryChoice remembered=null)
    {
        ArgumentNullException.ThrowIfNull(candidates);ArgumentNullException.ThrowIfNull(eligible);ArgumentNullException.ThrowIfNull(barrier);ArgumentNullException.ThrowIfNull(primaryPath);
        if(remembered!=null){return remembered;}
        var scan=new SentryScan();
        foreach(var candidate in candidates)
        {
            if(!eligible(candidate)){continue;}
            int distance=Distance(origin,candidate);if(distance>48){continue;}
            bool primary=candidate.Player || candidate.Threat.GetValueOrDefault()>1;
            var old=primary?scan.Primary:scan.Alternative;
            if(distance>=(old?.Distance??int.MaxValue) || barrier(candidate)){continue;}
            var choice=new SentryChoice(candidate,distance);scan=primary?scan with{Primary=choice}:scan with{Alternative=choice};
        }
        if(scan.Alternative==null){return scan.Primary;}
        if(scan.Primary==null){return scan.Alternative;}
        if(scan.Alternative.Distance>5 || primaryPath(scan.Primary.Candidate)){return scan.Primary;}
        // Callback7 excludes the blocked primary and all low-threat monsters.
        SentryChoice fallback=null;
        foreach(var candidate in candidates)
        {
            if(candidate.Serial==scan.Primary.Candidate.Serial || !eligible(candidate)){continue;}
            int distance=Distance(origin,candidate);if(distance>48){continue;}
            if(!candidate.Player && candidate.Threat==null){throw new NotSupportedException("Source callback7 has no missing-monster-row fallback");}
            if(!(candidate.Player || candidate.Threat.GetValueOrDefault()>1)
                || distance>=(fallback?.Distance??int.MaxValue) || barrier(candidate)){continue;}
            fallback=new(candidate,distance);
        }
        return fallback==null || fallback.Distance>=20?scan.Alternative:fallback;
    }
}

// Shared native trap allegiance/visibility adapter. The caller retains the
// protected BaseCreature harmful-region check at the actor's own map/position.
internal static class AssassinTrapTargets
{
    internal static bool Eligible(BaseCreature actor,PlayerMobile owner,Mobile target,Func<bool> harmful)=>target is BaseCreature creature && target!=owner
        && target.Alive && !target.Deleted && target.Map==actor.Map && !target.Blessed && !target.IsDeadBondedPet
        && !PaladinConversion.Active(target) && creature is not BaseVendor
        && !creature.Controlled && !creature.Summoned && creature.GetMaster()==null && !creature.InitialInnocent
        && creature.FightMode is not(FightMode.None or FightMode.Aggressor or FightMode.Evil)
        && creature.IsEnemy(owner) && actor.CanSee(target) && harmful();
}
