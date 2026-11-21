// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal static class AmazonBowEffects
{
    internal static void Release(PlayerMobile owner, CombatAbility ability)
    {
        bool crossbow = BowAmmunition.AmmoType(owner, CombatTiming.Equipped(owner)) == typeof(Bolt);
        owner.PlaySound(crossbow ? SpellEffectSounds.AmazonCrossbowRelease : SpellEffectSounds.AmazonBowRelease);
        int accent = ability switch
        {
            CombatAbility.MagicArrow => SpellEffectSounds.MagicArrowRelease,
            CombatAbility.GuidedArrow => SpellEffectSounds.GuidedArrowRelease,
            CombatAbility.FireArrow or CombatAbility.ExplodingArrow or CombatAbility.ImmolationArrow => SpellEffectSounds.FireBoltRelease,
            CombatAbility.ColdArrow => SpellEffectSounds.IceBoltRelease,
            CombatAbility.IceArrow or CombatAbility.FreezingArrow => SpellEffectSounds.IceBlastRelease,
            _ => 0
        };
        if (accent != 0) { owner.PlaySound(accent); }
    }

    internal static void Contact(Mobile owner, Mobile target, CombatAbility ability)
    {
        (int graphic, int sound) = ability switch
        {
            CombatAbility.Strafe or CombatAbility.MultipleShot => (SpellEffectGraphics.ArrowContact, SpellEffectSounds.AmazonArrowContact),
            CombatAbility.MagicArrow => (SpellEffectGraphics.MagicArrowContact, SpellEffectSounds.MagicArrowContact),
            CombatAbility.GuidedArrow => (SpellEffectGraphics.GuidedArrowContact, SpellEffectSounds.GuidedArrowContact),
            CombatAbility.FireArrow => (SpellEffectGraphics.FireArrowContact, SpellEffectSounds.FireBoltContact),
            CombatAbility.ColdArrow => (SpellEffectGraphics.ColdArrowContact, SpellEffectSounds.IceBoltContact),
            CombatAbility.IceArrow => (SpellEffectGraphics.IceArrowContact, SpellEffectSounds.IceBlastContact),
            _ => (0, 0)
        };
        if (graphic == 0) { return; }
        var point = target.Location;
        Effects.SendLocationEffect(point, target.Map, graphic, SpellEffectGraphics.ArrowContactFrames);
        SpellAreaSounds.Pulse(owner.Serial.Value, sound, point, target.Map, TimedEffects.Now, 80);
    }

    internal static void Burst(PlayerMobile owner, CombatAbility ability, Point3D point)
    {
        (int graphic, int sound) = ability switch
        {
            CombatAbility.ExplodingArrow => (SpellEffectGraphics.ExplodingArrowBurst, SpellEffectSounds.ExplodingArrowBurst),
            CombatAbility.FreezingArrow => (SpellEffectGraphics.FreezingArrowBurst, SpellEffectSounds.GlacialSpikeBurst),
            CombatAbility.ImmolationArrow => (SpellEffectGraphics.ImmolationArrowIgnition, SpellEffectSounds.ImmolationArrowIgnition),
            _ => (0, 0)
        };
        if (graphic == 0) { return; }
        Effects.SendLocationEffect(point, owner.Map, graphic, SpellEffectGraphics.ArrowBurstDuration);
        SpellAreaSounds.Pulse(owner.Serial.Value, sound, point, owner.Map, TimedEffects.Now, 80);
    }
}
