// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using static Sanctuary.PaladinAuraVisuals;

namespace Sanctuary;

internal static class PaladinHostileAuraVisuals
{
    internal static bool IsSupplier(CombatAbility ability) => ability is CombatAbility.Sanctuary or CombatAbility.Conviction;

    private static ushort SupplierGraphic(CombatAbility ability) => ability == CombatAbility.Sanctuary
        ? SpellEffectGraphics.SanctuaryAura : SpellEffectGraphics.ConvictionAura;

    internal static void ClearSupplier(Mobile owner, CombatAbility ability) => Clear(owner, SupplierGraphic(ability));

    internal static void SanctuaryPulse(Mobile owner, double radius, DateTime until, DateTime now)
    {
        if (!Attach(owner, SpellEffectGraphics.SanctuaryAura, until, now)) { return; }
        int encodedRadius = (int)Math.Clamp(Math.Round(radius * SpellEffectGraphics.HolyAuraRadiusUnitsPerTile), 1, ushort.MaxValue);
        Effects.SendLocationEffect(owner, SpellEffectGraphics.SanctuaryPulse, 8, hue: encodedRadius);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.SanctuaryPulse, owner.Location, owner.Map, now, 650);
    }

    internal static void SanctuaryContact(Mobile owner, Point3D point, Map map, DateTime now)
    {
        if (World.Loading || map == null || map == Map.Internal) { return; }
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.SanctuaryContact, 7);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.SanctuaryContact, point, map, now, 240);
    }

    internal static void SanctuaryWeapon(Mobile owner, Mobile target)
    {
        if (target is not { Alive: true, Deleted: false } || !target.CanBeDamaged() || !InWorld(owner)
            || target.Map != owner.Map || !PaladinSanctuary.Active(owner) || !GemCombat.Undead(target)
            || ClassCombat.Friendly(owner, target)) { return; }
        target.FixedEffect(SpellEffectGraphics.SanctuaryContact, 10, 7);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.SanctuaryWeapon, target.Location, target.Map, TimedEffects.Now, 180);
    }

    internal static void ConvictionSupplier(Mobile owner, DateTime until, DateTime now, bool starting)
    {
        if (Attach(owner, SpellEffectGraphics.ConvictionAura, until, now) && starting)
        {
            SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.ConvictionAura, owner.Location, owner.Map, now, 500);
        }
    }

    internal static void ConvictionRecipient(Mobile target, ConvictionLease lease, DateTime now, bool applied)
    {
        if (Attach(target, SpellEffectGraphics.ConvictionMark, lease.UntilUtc, now) && applied)
        {
            target.FixedEffect(SpellEffectGraphics.ConvictionContact, 10, 7);
            SpellAreaSounds.Pulse(lease.Source, SpellEffectSounds.ConvictionContact, target.Location, target.Map, now, 270);
        }
    }

    internal static void ClearConviction(Mobile target) => Clear(target, SpellEffectGraphics.ConvictionMark);
}
