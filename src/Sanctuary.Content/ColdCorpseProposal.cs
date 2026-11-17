// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.CompilerServices;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Explicit candidate, not an inferred D2R chance. Ordinary actors are not
// enrolled. The existing corpse record owns the once-only persisted result.
internal static class ColdCorpseProposal
{
    private sealed record Admission(Func<int> Roll);
    private static readonly ConditionalWeakTable<BaseCreature,Admission> Candidates=new();
    internal static void Enroll(BaseCreature actor,Func<int> roll=null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        Candidates.Remove(actor);Candidates.Add(actor,new(roll??(()=>Utility.Random(100))));
    }
    internal static bool Resolve(BaseCreature actor,bool alreadyShattered)
    {
        if(!Candidates.TryGetValue(actor,out var admission)){return alreadyShattered;}
        Candidates.Remove(actor);
        if(alreadyShattered||!TimedEffects.TryActor(actor,out var state)||state.Cold==null
            ||state.ChillUntil<=TimedEffects.Now||state.ChillPercent==0){return alreadyShattered;}
        int roll=admission.Roll();
        if(roll is<0 or>=100){throw new ArgumentOutOfRangeException(nameof(roll),"Cold corpse roll must be 0..99");}
        return roll<20;
    }
}
