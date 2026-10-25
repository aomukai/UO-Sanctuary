// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;

namespace Sanctuary;

// One native source-frame conductor: every trap missile phase precedes every
// trap monster phase. Old explicitly scoped fixture Tick methods remain usable.
public static class AssassinTrapClock
{
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static void Tick(DateTime at)
    {
        AssassinTrapRoster.SweepOwners();BladeSentinelMissiles.PruneFaults();
        // Unlimited shot-based actors must not replay millions of empty offline
        // frames after a long pause; their adapter ages that interval in bulk.
        foreach(var body in AssassinSentryActors.Actors())
        {if(body.Prepared.NextFrameUtc<=at && (!AssassinTrapRoster.Fighting(body.Prepared.Owner)||body.Prepared.ControllerFault!=null)){AssassinSentryActors.AdvanceAt(body,at);}}
        while(true)
        {
            var blades=BladeSentinelActors.Actors();var sentries=AssassinSentryActors.Actors();
            DateTime next=blades.Select(x=>x.Prepared.NextFrameUtc).Concat(sentries.Select(x=>x.Prepared.NextFrameUtc))
                .Append(BladeSentinelMissiles.NextFrame).Append(AssassinSentryBolts.NextFrame).Min();
            if(next>at){return;}
            BladeSentinelMissiles.TickFrame(next);AssassinSentryBolts.TickFrame(next);
            var actors=blades.Where(x=>x.Prepared.NextFrameUtc==next).Cast<Mobile>()
                .Concat(sentries.Where(x=>x.Prepared.NextFrameUtc==next)).OrderByDescending(x=>x.Serial.Value).ToArray();
            foreach(var actor in actors)
            {
                if(actor.Deleted){continue;}
                if(actor is BladeSentinelBody blade){BladeSentinelActors.AdvanceAt(blade,next);}
                else{AssassinSentryActors.AdvanceAt((AssassinSentryBody)actor,next);}
            }
        }
    }
}
