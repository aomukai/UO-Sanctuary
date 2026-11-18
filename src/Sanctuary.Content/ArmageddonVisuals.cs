// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using static Sanctuary.GroundSpellVisuals;

namespace Sanctuary;

internal static class ArmageddonVisuals
{
    internal static void Release(Mobile owner)
    {
        owner.FixedEffect(SpellEffectGraphics.ArmageddonRelease, 10, 9);
        owner.PlaySound(SpellEffectSounds.ArmageddonRelease);
    }

    internal static void Sustain(Mobile owner, ArmageddonState state)
    {
        int duration = (int)Math.Ceiling(Math.Min(360, (state.UntilUtc - state.NextUtc).TotalMilliseconds) / 50);
        if (duration > 0) { owner.FixedEffect(SpellEffectGraphics.ArmageddonAura, 10, duration); }
    }

    internal static void Clear(Mobile owner)
    {
        if (!World.Loading && owner is { Deleted: false } && owner.Map != null && owner.Map != Map.Internal)
        { owner.FixedEffect(SpellEffectGraphics.ArmageddonAura, 10, 0); }
    }

    internal static void Approach(Map map, ArmageddonActor actor, DateTime at)
    {
        double x = actor.X / 2.0, y = actor.Y / 2.0;
        int remaining = actor.Remaining;
        if ((remaining & 1) == 0)
        {
            int warning = SpellEffectGraphics.ArmageddonWarning + ((25 - remaining) / 3) % SpellEffectGraphics.DruidEruptionFrames;
            Sample(map, x, y, actor.Z, warning, remaining >= 3 ? 2 : 1);
        }
        if (remaining > SpellEffectGraphics.ArmageddonFallFrames) { return; }

        int fall = SpellEffectGraphics.ArmageddonFall + SpellEffectGraphics.ArmageddonFallFrames - remaining;
        var point = Sample(map, x, y, actor.Z, fall, 1);
        if (remaining == SpellEffectGraphics.ArmageddonFallFrames)
        { SpellAreaSounds.Pulse(actor.Owner, SpellEffectSounds.ArmageddonDescent, point, map, at, 220); }
    }

    internal static void Impact(Map map, ArmageddonActor actor)
    {
        var point = Sample(map, actor.X / 2.0, actor.Y / 2.0, actor.Z, SpellEffectGraphics.ArmageddonImpact, 8);
        SpellAreaSounds.Pulse(actor.Owner, SpellEffectSounds.ArmageddonImpact, point, map, actor.NextUtc, 220);
    }
}
