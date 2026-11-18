// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class BarbarianMotionVisuals
{
    internal static void Takeoff(Mobile owner) => Burst(owner.Location, owner.Map,
        SpellEffectGraphics.LeapTakeoff, SpellEffectSounds.LeapTakeoff);

    internal static void Air(Mobile owner, LeapFlight flight)
    {
        var position = flight.Position;
        var from = flight.Origin;
        var to = flight.Destination;
        int travelFrames = (int)Math.Ceiling(flight.Length16 / (flight.Velocity256 * 8.0));
        int length = Math.Max(flight.MinimumAirFrames, travelFrames);
        var progress = Math.Clamp(flight.AirFrames / (double)length, 0, 1);
        int phase = Math.Min(SpellEffectGraphics.BarbarianMotionFrames - 1,
            (int)(progress * SpellEffectGraphics.BarbarianMotionFrames));
        int facing = SpellEffectGraphics.ProjectileFacing(to.X - from.X, to.Y - from.Y);
        int lift = 9 + (int)Math.Round(Math.Sin(Math.PI * progress) * 26);
        int graphic = SpellEffectGraphics.LeapAirTrail + facing * SpellEffectGraphics.BarbarianMotionFrames + phase;
        GroundSpellVisuals.Sample(owner.Map, position.X, position.Y, position.Z, graphic, 1, lift);
    }

    internal static void LeapSettled(Mobile owner)
        => Burst(owner.Location, owner.Map, SpellEffectGraphics.LeapSettle, SpellEffectSounds.LeapSettle);

    internal static void Landing(Mobile owner, double radius, bool attack)
    {
        int encoded = (int)Math.Clamp(Math.Round(radius * SpellEffectGraphics.HolyAuraRadiusUnitsPerTile), 1, ushort.MaxValue);
        Effects.SendLocationEffect(owner, attack ? SpellEffectGraphics.LeapAttackLanding : SpellEffectGraphics.LeapLanding,
            8, hue: encoded);
        owner.PlaySound(attack ? SpellEffectSounds.LeapAttackLanding : SpellEffectSounds.LeapLanding);
    }

    internal static void LeapAreaContact(Point3D point, Map map)
        => Effects.SendLocationEffect(point, map, SpellEffectGraphics.LeapAttackAreaContact, 6);

    internal static void LeapStrike(Mobile owner)
    {
        owner.FixedEffect(SpellEffectGraphics.LeapAttackSwing, 10, 6);
        owner.PlaySound(SpellEffectSounds.LeapAttackSwing);
    }

    internal static void LeapWeaponImpact(Point3D point, Map map, PreparedMonsterCommitResult commit)
    {
        if (commit is not { Packet.ReactionSettled: false }) { return; }
        Burst(point, map, SpellEffectGraphics.LeapAttackImpact, SpellEffectSounds.LeapAttackImpact);
    }

    internal static void WhirlwindSpin(Mobile owner, int frame)
    {
        if (frame % 12 != 0 || !PaladinAuraVisuals.InWorld(owner)) { return; }
        owner.FixedEffect(SpellEffectGraphics.WhirlwindSpin, 10, 22);
        if (frame == 0) { owner.PlaySound(SpellEffectSounds.WhirlwindStart); }
    }

    internal static void WhirlwindWake(Mobile owner, Point3D from, DateTime now)
    {
        Effects.SendLocationEffect(from, owner.Map, SpellEffectGraphics.WhirlwindWake, 6);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.WhirlwindWake, from, owner.Map, now, 290);
    }

    internal static void WhirlwindImpact(Mobile owner, Point3D point, Map map, DateTime now)
    {
        if (World.Loading || map == null || map == Map.Internal) { return; }
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.WhirlwindImpact, 6);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.WhirlwindImpact, point, map, now, 85);
    }

    internal static void WhirlwindClear(Mobile owner)
        => PaladinAuraVisuals.Clear(owner, SpellEffectGraphics.WhirlwindSpin);

    internal static void WhirlwindEnd(Mobile owner, WhirlwindFlight flight)
    {
        if (owner is not { Alive: true } || !PaladinAuraVisuals.InWorld(owner)
            || owner.Map.MapID != flight.Map || owner.Location != flight.Position) { return; }
        Burst(flight.Position, owner.Map, SpellEffectGraphics.WhirlwindFade, SpellEffectSounds.WhirlwindFade);
    }

    private static void Burst(Point3D point, Map map, int graphic, int sound)
    {
        if (World.Loading || map == null || map == Map.Internal) { return; }
        Effects.SendLocationEffect(point, map, graphic, 7);
        Effects.PlaySound(point, map, sound);
    }
}
