// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

// Complete source Other-mode writer shares the15..175scalar clamp with the
// nonweapon attack writer. This collector uses only actual Other stat fields;
// ownerFCR,masteryIAS,Decrepify and attack-only sigils are not Other modifiers.
internal static class SourceOtherAnimation
{
    internal static int Speed(int basis,int totalRate)=>SourceAttackRate.Speed(basis,0,totalRate);
    internal static int Rate(Mobile actor)
    {
        if(actor is not{Alive:true,Deleted:false}){throw new NotSupportedException("Other animation requires a living source actor");}
        int cold=ItemDurationDefenses.CannotFreeze(actor)?0:TimedEffects.Chill(actor,TimedEffects.Now);
        return checked(100-cold-PaladinHolyFreeze.OtherSlow(actor)-ImpaleSlow.Other(actor)+GrimWardCurseTransport.Other(actor));
    }
}
