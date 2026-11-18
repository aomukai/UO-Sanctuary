// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class PaladinSpellVisuals
{
    internal static void BoltRelease(Mobile owner)
    {
        owner.FixedEffect(SpellEffectGraphics.HolyBoltRelease, 10, 7);
        owner.PlaySound(SpellEffectSounds.HolyBoltRelease);
    }

    internal static void HammerRelease(Mobile owner)
    {
        owner.FixedEffect(SpellEffectGraphics.BlessedHammerRelease, 10, 7);
        owner.PlaySound(SpellEffectSounds.BlessedHammerRelease);
    }

    internal static void BoltTravel(Map map, HolyBoltFlight flight)
    {
        ushort group = flight.Ability == CombatAbility.HolyBolt ? SpellEffectGraphics.HolyBoltFlight : SpellEffectGraphics.HeavenBoltFlight;
        int frame = SorceressVisuals.DirectionalFrame(group, flight.Vx, flight.Vy, flight.Frame / 2);
        GroundSpellVisuals.Sample(map, flight.X, flight.Y, flight.Z, frame, 2, lift: 12);
    }

    internal static void HammerTravel(Map map, HammerFlight flight, double x, double y)
        => GroundSpellVisuals.Sample(map, x, y, flight.Z,
            SpellEffectGraphics.BlessedHammerFlight + (flight.Frame / 2) % SpellEffectGraphics.PaladinSpellFrames, 2, lift: 13);

    internal static void BoltHeal(Mobile target)
    {
        target.FixedEffect(SpellEffectGraphics.HolyBoltHeal, 10, 8);
        target.PlaySound(SpellEffectSounds.HolyBoltHeal);
    }

    internal static void BoltContact(Mobile owner, Point3D point, Map map, DateTime at)
        => Contact(owner, point, map, SpellEffectGraphics.HolyBoltContact, SpellEffectSounds.HolyBoltContact, at);

    internal static void HammerContact(Mobile owner, Point3D point, Map map, DateTime at)
        => Contact(owner, point, map, SpellEffectGraphics.BlessedHammerContact, SpellEffectSounds.BlessedHammerContact, at);

    private static void Contact(Mobile owner, Point3D point, Map map, int graphic, int cue, DateTime at)
    {
        Effects.SendLocationEffect(point, map, graphic, 7);
        SpellAreaSounds.Pulse(owner.Serial.Value, cue, point, map, at, 150);
    }

    internal static void End(Mobile owner, double x, double y, int z, bool hammer, DateTime at)
    {
        int graphic = hammer ? SpellEffectGraphics.BlessedHammerFade : SpellEffectGraphics.HolyBoltFade;
        int cue = hammer ? SpellEffectSounds.BlessedHammerFade : SpellEffectSounds.HolyBoltFade;
        var point = GroundSpellVisuals.Sample(owner.Map, x, y, z, graphic, 7, lift: 11);
        SpellAreaSounds.Pulse(owner.Serial.Value, cue, point, owner.Map, at, 160);
    }

    internal static void Warn(Mobile owner, Mobile target)
    {
        target.FixedEffect(SpellEffectGraphics.HeavenFistWarning, 10, 8);
        owner.PlaySound(SpellEffectSounds.HeavenFistWarning);
    }

    internal static void Strike(Point3D point, Map map)
    {
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.HeavenFistStrike, 9);
        Effects.PlaySound(point, map, SpellEffectSounds.HeavenFistStrike);
    }

    internal static void HeavenBurst(Point3D point, Map map)
    {
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.HeavenFistBurst, 8);
        Effects.PlaySound(point, map, SpellEffectSounds.HeavenFistBurst);
    }
}
