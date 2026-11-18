// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using static Sanctuary.PaladinAuraVisuals;

namespace Sanctuary;

internal static class PaladinOffenseVisuals
{
    internal static bool Handles(CombatAbility ability) => ability is CombatAbility.Might or CombatAbility.BlessedAim
        or CombatAbility.Concentration or CombatAbility.Fanaticism;

    internal static bool Active(PaladinAuraLease lease) => lease != null && Handles(lease.Ability)
        && (lease.Percent > 0 || lease.DamagePercent > 0 || lease.AttackPercent > 0 || lease.UninterruptChance > 0);

    private static (int Graphic, int Cue) Style(CombatAbility ability) => ability switch
    {
        CombatAbility.Might => (SpellEffectGraphics.MightAura, SpellEffectSounds.MightAura),
        CombatAbility.BlessedAim => (SpellEffectGraphics.BlessedAimAura, SpellEffectSounds.BlessedAimAura),
        CombatAbility.Concentration => (SpellEffectGraphics.ConcentrationAura, SpellEffectSounds.ConcentrationAura),
        CombatAbility.Fanaticism => (SpellEffectGraphics.FanaticismAura, SpellEffectSounds.FanaticismAura),
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    internal static void Pulse(Mobile target, PaladinAuraLease lease, PaladinAuraLease previous, DateTime now)
    {
        if (!Active(lease)) { Clear(target, previous); return; }
        var style = Style(lease.Ability);
        if (Attach(target, style.Graphic, lease.UntilUtc, now) && (!Active(previous) || previous.UntilUtc <= now))
        { SpellAreaSounds.Pulse(lease.Source, style.Cue, target.Location, target.Map, now, 320); }
    }

    internal static void Clear(Mobile target, PaladinAuraLease lease)
    {
        if (Active(lease)) { PaladinAuraVisuals.Clear(target, Style(lease.Ability).Graphic); }
    }

    internal static void Guard(Mobile target)
    {
        if (!InWorld(target)) { return; }
        target.FixedEffect(SpellEffectGraphics.ConcentrationGuard, 10, 7);
        SpellAreaSounds.Pulse(target.Serial.Value, SpellEffectSounds.ConcentrationGuard,
            target.Location, target.Map, TimedEffects.Now, 200);
    }
}
