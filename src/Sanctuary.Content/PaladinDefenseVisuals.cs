// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using static Sanctuary.PaladinAuraVisuals;

namespace Sanctuary;

internal static class PaladinDefenseVisuals
{
    internal static bool Handles(CombatAbility ability) =>
        ability is CombatAbility.Defiance or CombatAbility.Vigor or CombatAbility.Thorns;

    internal static bool Active(PaladinAuraLease lease) => lease != null && Handles(lease.Ability)
        && (lease.Percent > 0 || lease.VelocityPercent > 0 || lease.StaminaPercent > 0 || lease.DamagePercent > 0);

    private static (int Graphic, int Cue) Style(CombatAbility ability) => ability switch
    {
        CombatAbility.Defiance => (SpellEffectGraphics.DefianceAura, SpellEffectSounds.DefianceAura),
        CombatAbility.Vigor => (SpellEffectGraphics.VigorAura, SpellEffectSounds.VigorAura),
        CombatAbility.Thorns => (SpellEffectGraphics.ThornsAura, SpellEffectSounds.ThornsAura),
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    internal static void Pulse(Mobile target, PaladinAuraLease lease, PaladinAuraLease previous, DateTime now)
    {
        if (!Active(lease)) { Clear(target, previous); return; }
        var style = Style(lease.Ability);
        if (Attach(target, style.Graphic, lease.UntilUtc, now) && (!Active(previous) || previous.UntilUtc <= now))
        {
            SpellAreaSounds.Pulse(lease.Source, style.Cue, target.Location, target.Map, now, 320);
        }
    }

    internal static void Clear(Mobile target, PaladinAuraLease lease)
    {
        if (Active(lease)) { PaladinAuraVisuals.Clear(target, Style(lease.Ability).Graphic); }
    }

    internal static void ThornsContact(Mobile defender, Point3D point, Map map)
    {
        if (World.Loading || map == null || map == Map.Internal) { return; }
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.ThornsContact, 7);
        SpellAreaSounds.Pulse(defender.Serial.Value, SpellEffectSounds.ThornsContact, point, map, TimedEffects.Now, 200);
    }

    internal static void ShieldMoment(Mobile owner, int graphic, int cue, int duration, DateTime now)
    {
        if (!InWorld(owner)) { return; }
        owner.FixedEffect(graphic, 10, duration);
        SpellAreaSounds.Pulse(owner.Serial.Value, cue, owner.Location, owner.Map, now, 180);
    }
}
