// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace Sanctuary;

internal sealed record SentryRetirementState(DateTime EnteredUtc,DateTime CompletesUtc,string Cause,string Profile);

// Current owned LGDTHTH supplies23frames/speed256, no action flags. The
// unchanged ordinary source scheduler supplies the completion calculation.
// Pool registration uses the existing completed-death boundary; its precise
// modern insertion/callback order remains an explicit acceptance limitation.
internal static class AssassinSentryRetirement
{
    internal const string Profile="owned-lg-dt23-completed-corpse-v1";
    internal static int EndDelay(int frames=23,int speed=256)
    {
        if(frames is <1 or >144 || speed is <0 or >32767){throw new ArgumentOutOfRangeException();}
        return speed==0?1:Math.Max(2,(frames*256+speed-1)/speed);
    }
    internal static SentryRetirementState Begin(DateTime at,string cause)
    {
        if(at.Kind!=DateTimeKind.Utc || cause is not("source-ai" or "replacement")){throw new ArgumentException("Unsupported Sentry death entry");}
        return new(at,at.AddMilliseconds(EndDelay()*40L),cause,Profile);
    }
    internal static void Validate(SentryRetirementState state)
    {
        if(state is not{Profile:Profile} || state.EnteredUtc.Kind!=DateTimeKind.Utc || state.CompletesUtc.Kind!=DateTimeKind.Utc
            || state.CompletesUtc!=state.EnteredUtc.AddMilliseconds(EndDelay()*40L) || state.Cause is not("source-ai" or "replacement"))
        {throw new InvalidDataException("Invalid saved Sentry retirement");}
    }
}
