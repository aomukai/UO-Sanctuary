// SPDX-License-Identifier: GPL-3.0-or-later
using Server;

namespace Sanctuary;

internal static class UoSkillVisuals
{
    internal static (int Graphic, int Hue) WeaponFlight(WeaponFlight flight)
    {
        if (flight.HostileSpell)
        {
            return (0x36D4, 0);
        }
        if (AmazonJavelins.Handles(flight.Ability))
        {
            return (AmazonJavelinEffects.Flight(flight), 0);
        }

        bool crossbow = flight.CrossbowVisual || flight.Missile is "multipleshotbolt" or "strafebolt";
        if (flight.Ability is CombatAbility.Strafe or CombatAbility.MultipleShot)
        {
            int graphic = crossbow ? SpellEffectGraphics.StrafeBolt : SpellEffectGraphics.StrafeArrow;
            return (graphic + SpellEffectGraphics.ProjectileFacing(flight.Vx, flight.Vy), 0);
        }
        if (flight.Ability is CombatAbility.MagicArrow or CombatAbility.GuidedArrow)
        {
            ushort graphic = (flight.Ability, crossbow) switch
            {
                (CombatAbility.MagicArrow, true) => SpellEffectGraphics.MagicArrowBolt,
                (CombatAbility.MagicArrow, false) => SpellEffectGraphics.MagicArrowFlight,
                (CombatAbility.GuidedArrow, true) => SpellEffectGraphics.GuidedArrowBolt,
                _ => SpellEffectGraphics.GuidedArrowFlight
            };
            return (SorceressVisuals.DirectionalFrame(graphic, flight.Vx, flight.Vy, flight.Frame / 2), 0);
        }

        int elemental = flight.Ability switch
        {
            CombatAbility.FireArrow => SpellEffectGraphics.FireArrow,
            CombatAbility.ExplodingArrow => SpellEffectGraphics.ExplodingArrow,
            CombatAbility.ImmolationArrow => SpellEffectGraphics.ImmolationArrow,
            CombatAbility.ColdArrow => SpellEffectGraphics.ColdArrow,
            CombatAbility.IceArrow => SpellEffectGraphics.IceArrow,
            CombatAbility.FreezingArrow => SpellEffectGraphics.FreezingArrow,
            _ => 0
        };
        if (elemental != 0)
        {
            int offset = crossbow ? SpellEffectGraphics.ElementalArrowBoltOffset : 0;
            return (elemental + offset + SpellEffectGraphics.ProjectileFacing(flight.Vx, flight.Vy), 0);
        }

        return (0x0F42, 0);
    }

    internal static (int Graphic, int Hue, int Sound)? Curse(CombatAbility ability) => ability switch
    {
        CombatAbility.AmplifyDamage => (0x374A, 0x21, 0x1E1),
        CombatAbility.DimVision => (0x3779, 0x455, 0x1E6),
        CombatAbility.Weaken => (0x3779, 0, 0x1E6),
        CombatAbility.IronMaiden => (0x375A, 0x497, 0x1E1),
        CombatAbility.Terror => (0x374A, 0x455, 0x1FB),
        CombatAbility.Confuse => (0x3779, 0x481, 0x1FB),
        CombatAbility.LifeTap => (0x374A, 0x26, 0x1E1),
        CombatAbility.Attract => (0x375A, 0x59, 0x1FB),
        CombatAbility.Decrepify => (0x3779, 0x3F, 0x1E6),
        CombatAbility.LowerResist => (0x374A, 0x482, 0x1E1),
        _ => null
    };

    internal static int CurseSound(CombatAbility ability) => Curse(ability)?.Sound ?? 0x1E5;

    internal static void Summon(Mobile body, int sound, int hue = 0)
    {
        body.FixedEffect(0x376A, 10, 12, hue, 0);
        body.PlaySound(sound);
    }
}
