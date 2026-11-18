// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using static Sanctuary.PaladinAuraVisuals;

namespace Sanctuary;

internal static class PaladinElementVisuals
{
    private static (int Pulse, int Loop, int Contact, int PulseCue, int ContactCue) EffectsFor(CombatAbility ability)
        => ability switch
        {
            CombatAbility.HolyFire => (SpellEffectGraphics.HolyFirePulse, SpellEffectGraphics.HolyFireAura,
                SpellEffectGraphics.HolyFireContact, SpellEffectSounds.HolyFirePulse, SpellEffectSounds.HolyFireContact),
            CombatAbility.HolyFreeze => (SpellEffectGraphics.HolyFreezePulse, SpellEffectGraphics.HolyFreezeAura,
                SpellEffectGraphics.HolyFreezeContact, SpellEffectSounds.HolyFreezePulse, SpellEffectSounds.HolyFreezeContact),
            CombatAbility.HolyShock => (SpellEffectGraphics.HolyShockPulse, SpellEffectGraphics.HolyShockAura,
                SpellEffectGraphics.HolyShockContact, SpellEffectSounds.HolyShockPulse, SpellEffectSounds.HolyShockContact),
            _ => throw new ArgumentOutOfRangeException(nameof(ability))
        };

    internal static void Active(Mobile source, CombatAbility ability, DateTime until, DateTime now)
        => Attach(source, EffectsFor(ability).Loop, until, now);

    internal static void Clear(Mobile source, CombatAbility ability)
        => PaladinAuraVisuals.Clear(source, EffectsFor(ability).Loop);

    internal static void Slow(Mobile target, DateTime until, DateTime now)
        => Attach(target, SpellEffectGraphics.HolyFreezeSlow, until, now);

    internal static bool IsWard(CombatAbility ability) => PaladinResistance.Single(ability) || ability == CombatAbility.Salvation;

    internal static bool HasWard(PaladinAuraLease lease) => lease is { SourceSkill: not 420 }
        && IsWard(lease.Ability) && (lease.Percent > 0 || lease.MaximumResistance > 0);

    private static (int Graphic, int Cue) WardStyle(CombatAbility ability) => ability switch
    {
        CombatAbility.ResistFire => (SpellEffectGraphics.ResistFireWard, SpellEffectSounds.ResistFireWard),
        CombatAbility.ResistCold => (SpellEffectGraphics.ResistColdWard, SpellEffectSounds.ResistColdWard),
        CombatAbility.ResistLightning => (SpellEffectGraphics.ResistLightningWard, SpellEffectSounds.ResistLightningWard),
        CombatAbility.Salvation => (SpellEffectGraphics.SalvationWard, SpellEffectSounds.SalvationWard),
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    internal static void WardApplied(Mobile target, PaladinAuraLease lease, PaladinAuraLease previous, DateTime now)
    {
        if (!HasWard(lease)) { ClearWard(target, previous); return; }
        var style = WardStyle(lease.Ability);
        if (Attach(target, style.Graphic, lease.UntilUtc, now) && (!HasWard(previous) || previous.UntilUtc <= now))
        {
            SpellAreaSounds.Pulse(lease.Source, style.Cue, target.Location, target.Map, now, 300);
        }
    }

    internal static void ClearWard(Mobile target, PaladinAuraLease lease)
    {
        if (HasWard(lease)) { PaladinAuraVisuals.Clear(target, WardStyle(lease.Ability).Graphic); }
    }

    internal static void Pulse(Mobile source, CombatAbility ability, double radius, DateTime now)
    {
        var style = EffectsFor(ability);
        int encodedRadius = (int)Math.Clamp(Math.Round(radius * SpellEffectGraphics.HolyAuraRadiusUnitsPerTile), 1, ushort.MaxValue);
        Effects.SendLocationEffect(source, style.Pulse, 8, hue: encodedRadius);
        SpellAreaSounds.Pulse(source.Serial.Value, style.PulseCue, source.Location, source.Map, now, 380);
    }

    internal static void Hit(Mobile source, CombatAbility ability, Point3D point, Map map, DateTime now)
    {
        var style = EffectsFor(ability);
        Effects.SendLocationEffect(point, map, style.Contact, 6);
        SpellAreaSounds.Pulse(source.Serial.Value, style.ContactCue, point, map, now, 250);
    }

    internal static void WeaponContact(Mobile source, Mobile target)
    {
        if (target is not { Alive: true, Deleted: false } || !target.CanBeDamaged()
            || !InWorld(source) || source.Map != target.Map || !PaladinElements.WeaponActive(source)) { return; }

        var now = TimedEffects.Now;
        if (PaladinAuras.Effect(source, CombatAbility.HolyFire) is { WeaponMax: > 0 } && source is not NecromancerMinion)
        { Hit(source, CombatAbility.HolyFire, target.Location, target.Map, now); }
        if (PaladinAuras.Effect(source, CombatAbility.HolyFreeze) is { WeaponMax: > 0 })
        { Hit(source, CombatAbility.HolyFreeze, target.Location, target.Map, now); }
        if (PaladinAuras.Effect(source, CombatAbility.HolyShock) is { WeaponMax: > 0 })
        { Hit(source, CombatAbility.HolyShock, target.Location, target.Map, now); }
    }
}
