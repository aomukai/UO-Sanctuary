// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.CompilerServices;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Approved Sanctuary adaptation, not an inferred D2R chance. The existing
// corpse record owns the once-only result. Overrides only supply test rolls;
// every ordinary actor uses the same rule with the native RNG by default.
internal static class ColdCorpseRules
{
    private sealed record RollOverride(Func<int> Roll);
    private static readonly ConditionalWeakTable<BaseCreature,RollOverride> Overrides=new();
    internal static void SetRoll(BaseCreature actor,Func<int> roll)
    {
        ArgumentNullException.ThrowIfNull(actor);ArgumentNullException.ThrowIfNull(roll);
        Overrides.Remove(actor);Overrides.Add(actor,new(roll));
    }
    internal static bool Resolve(BaseCreature actor,bool alreadyShattered)
    {
        Overrides.TryGetValue(actor,out var supplied);Overrides.Remove(actor);
        if(alreadyShattered||!TimedEffects.TryActor(actor,out var state)||state.Cold==null
            ||state.ChillUntil<=TimedEffects.Now||state.ChillPercent==0){return alreadyShattered;}
        int roll=supplied?.Roll()??Utility.Random(100);
        if(roll is<0 or>=100){throw new ArgumentOutOfRangeException(nameof(roll),"Cold corpse roll must be 0..99");}
        return roll<20;
    }
}
