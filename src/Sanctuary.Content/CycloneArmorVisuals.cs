// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class CycloneArmorVisuals
{
    private static bool CanDraw(Mobile owner) => !World.Loading && owner is { Deleted: false }
        && owner.Map != null && owner.Map != Map.Internal;

    internal static void Raise(Mobile owner)
    {
        if (!CanDraw(owner)) { return; }

        owner.FixedEffect(SpellEffectGraphics.CycloneArmorRaise, 10, 8);
        owner.PlaySound(SpellEffectSounds.CycloneArmorRaise);
        Refresh(owner);
    }

    internal static void Refresh(Mobile owner)
    {
        if (CanDraw(owner)) { owner.FixedEffect(SpellEffectGraphics.CycloneArmorWard, 10, 10); }
    }

    internal static void Catch(Mobile owner)
    {
        if (!CanDraw(owner)) { return; }

        owner.FixedEffect(SpellEffectGraphics.CycloneArmorCatch, 10, 6);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.CycloneArmorCatch, owner.Location, owner.Map, TimedEffects.Now, 160);
    }

    internal static void Break(Mobile owner)
    {
        if (!CanDraw(owner)) { return; }

        owner.FixedEffect(SpellEffectGraphics.CycloneArmorBreak, 10, 9);
        owner.PlaySound(SpellEffectSounds.CycloneArmorBreak);
    }

    internal static void Clear(Mobile owner)
    {
        if (CanDraw(owner)) { owner.FixedEffect(SpellEffectGraphics.CycloneArmorWard, 10, 0); }
    }
}
