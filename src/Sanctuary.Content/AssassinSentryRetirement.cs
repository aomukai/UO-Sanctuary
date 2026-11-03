// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace Sanctuary;

internal sealed record SentryRetirementState(DateTime EnteredUtc,DateTime CompletesUtc,string Cause,string Profile);

// Current owned LG/E9 DT records supply23/25frames at speed256 without action flags. The
// unchanged ordinary source scheduler supplies the completion calculation.
// Pool registration uses the existing completed-death boundary; its precise
// modern insertion/callback order remains an explicit acceptance limitation.
internal static class AssassinSentryRetirement
{
    internal const string Profile="owned-lg-dt23-completed-corpse-v1";
    internal const string InfernoProfile="owned-e9-dt25-completed-corpse-v1";
    internal static string For(CombatAbility ability)=>ability switch
    {CombatAbility.ChargedBoltSentry or CombatAbility.LightningSentry or CombatAbility.DeathSentry=>Profile,CombatAbility.WakeOfInferno=>InfernoProfile,_=>throw new NotSupportedException("Unsupported Sentry death profile")};
    internal static int EndDelay(int frames=23,int speed=256)
    {
        if(frames is <1 or >144 || speed is <0 or >32767){throw new ArgumentOutOfRangeException();}
        return speed==0?1:Math.Max(2,(frames*256+speed-1)/speed);
    }
    internal static SentryRetirementState Begin(DateTime at,string cause,CombatAbility ability=CombatAbility.ChargedBoltSentry)
    {
        if(at.Kind!=DateTimeKind.Utc || cause is not("source-ai" or "replacement")){throw new ArgumentException("Unsupported Sentry death entry");}
        string profile=For(ability);return new(at,at.AddMilliseconds(EndDelay(profile==InfernoProfile?25:23)*40L),cause,profile);
    }
    internal static void Validate(SentryRetirementState state,CombatAbility? ability=null)
    {
        if(state==null || state.Profile is not(Profile or InfernoProfile) || ability.HasValue&&state.Profile!=For(ability.Value) || state.EnteredUtc.Kind!=DateTimeKind.Utc || state.CompletesUtc.Kind!=DateTimeKind.Utc
            || state.CompletesUtc!=state.EnteredUtc.AddMilliseconds(EndDelay(state.Profile==InfernoProfile?25:23)*40L) || state.Cause is not("source-ai" or "replacement"))
        {throw new InvalidDataException("Invalid saved Sentry retirement");}
    }
}
