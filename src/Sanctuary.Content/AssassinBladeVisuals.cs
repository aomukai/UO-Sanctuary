// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class AssassinBladeVisuals
{
    internal static void SentinelRise(BladeSentinelBody body)
    {
        if (PaladinAuraVisuals.InWorld(body)) { body.PlaySound(SpellEffectSounds.BladeSentinelRise); }
    }

    internal static void SentinelFade(BladeSentinelBody body)
    {
        Burst(body.Location, body.Map, SpellEffectGraphics.BladeSentinelFade, SpellEffectSounds.BladeSentinelFade);
    }

    internal static void FuryRelease(Mobile owner, WeaponFlight flight)
    {
        Attached(owner, SpellEffectGraphics.BladeFuryRelease, SpellEffectSounds.BladeFuryRelease);
        FuryTravel(owner.Map, flight);
    }

    internal static void FuryTravel(Map map, WeaponFlight flight)
    {
        int facing = SpellEffectGraphics.ProjectileFacing(flight.Vx, flight.Vy);
        int phase = flight.Frame / 2 % SpellEffectGraphics.AssassinBladeFrames;
        int graphic = SpellEffectGraphics.BladeFuryFlight + facing * SpellEffectGraphics.AssassinBladeFrames + phase;
        GroundSpellVisuals.Sample(map, flight.X, flight.Y, flight.Z, graphic, 2, 31);
    }

    internal static void FuryFade(Mobile owner, WeaponFlight flight, Point3D? contact = null)
    {
        var point = contact ?? new Point3D((int)Math.Round(flight.X), (int)Math.Round(flight.Y), flight.Z);
        Effects.SendLocationEffect(point, owner.Map, SpellEffectGraphics.BladeFuryFade, 7);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.BladeFuryFade, point, owner.Map, flight.NextUtc, 180);
    }

    internal static void Impact(CombatAbility ability, Point3D point, Map map, bool blocked = false)
    {
        var (graphic, sound) = blocked ? (SpellEffectGraphics.DragonDeflect, SpellEffectSounds.DragonDeflect) : ability switch
        {
            CombatAbility.BladeSentinel => (SpellEffectGraphics.BladeSentinelHit, SpellEffectSounds.BladeSentinelHit),
            CombatAbility.BladeFury => (SpellEffectGraphics.BladeFuryHit, SpellEffectSounds.BladeFuryHit),
            CombatAbility.BladeShield => (SpellEffectGraphics.BladeShieldHit, SpellEffectSounds.BladeShieldHit),
            _ => throw new ArgumentOutOfRangeException(nameof(ability))
        };
        Burst(point, map, graphic, sound);
    }

    internal static void ShieldRaise(Mobile owner)
    {
        Attached(owner, SpellEffectGraphics.BladeShieldRaise, SpellEffectSounds.BladeShieldRaise);
    }

    internal static void ShieldRefresh(Mobile owner, DateTime until, DateTime at)
    {
        if (owner.Hidden) { return; }
        var refreshLimit = at.AddMilliseconds(1250);
        PaladinAuraVisuals.Attach(owner, SpellEffectGraphics.BladeShieldRing, until < refreshLimit ? until : refreshLimit, at);
    }

    internal static void ShieldClear(Mobile owner, bool expired = false)
    {
        PaladinAuraVisuals.Clear(owner, SpellEffectGraphics.BladeShieldRing);
        if (expired && owner is { Alive: true })
        {
            Attached(owner, SpellEffectGraphics.BladeShieldFade, SpellEffectSounds.BladeShieldFade);
        }
    }

    private static void Attached(Mobile owner, int graphic, int sound)
    {
        if (!PaladinAuraVisuals.InWorld(owner) || owner.Hidden) { return; }
        owner.FixedEffect(graphic, 8, 7);
        owner.PlaySound(sound);
    }

    private static void Burst(Point3D point, Map map, int graphic, int sound)
    {
        if (World.Loading || map == null || map == Map.Internal) { return; }
        Effects.SendLocationEffect(point, map, graphic, 7);
        Effects.PlaySound(point, map, sound);
    }
}
