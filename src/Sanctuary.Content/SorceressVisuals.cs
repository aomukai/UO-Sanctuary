// SPDX-License-Identifier: GPL-3.0-or-later
using Server;

namespace Sanctuary;

internal static class SorceressVisuals
{
    internal static int CastSound(CombatAbility ability) => ability switch
    {
        CombatAbility.FireBolt => SpellEffectSounds.FireBoltRelease,
        CombatAbility.IceBolt => SpellEffectSounds.IceBoltRelease,
        CombatAbility.IceBlast => SpellEffectSounds.IceBlastRelease,
        CombatAbility.GlacialSpike => SpellEffectSounds.GlacialSpikeRelease,
        CombatAbility.FrostNova => SpellEffectSounds.FrostNovaRelease,
        CombatAbility.ChargedBolt => SpellEffectSounds.ChargedBoltRelease,
        CombatAbility.Lightning => SpellEffectSounds.LightningRelease,
        CombatAbility.ChainLightning => SpellEffectSounds.ChainLightningRelease,
        CombatAbility.StaticField => SpellEffectSounds.StaticFieldRelease,
        CombatAbility.ThunderStorm => SpellEffectSounds.ThunderStormCharge,
        CombatAbility.Nova => SpellEffectSounds.NovaRelease,
        CombatAbility.Blaze => SpellEffectSounds.BlazeRelease,
        CombatAbility.FireWall => SpellEffectSounds.FireWallRelease,
        CombatAbility.Enchant => SpellEffectSounds.EnchantRaise,
        CombatAbility.FrozenArmor => SpellEffectSounds.FrozenArmorRaise,
        CombatAbility.ShiverArmor => SpellEffectSounds.ShiverArmorRaise,
        CombatAbility.ChillingArmor => SpellEffectSounds.ChillingArmorRaise,
        CombatAbility.EnergyShield => SpellEffectSounds.EnergyShieldRaise,
        CombatAbility.Telekinesis => SpellEffectSounds.TelekinesisRelease,
        _ => 0x1E5
    };

    internal static int DirectionalFrame(ushort first, double vx, double vy, int phase) =>
        first + 4 * SpellEffectGraphics.ProjectileFacing(vx, vy) + phase % 4;

    internal static int? ProjectileGraphic(CombatAbility ability, double vx, double vy, int phase)
    {
        ushort first = ability switch
        {
            CombatAbility.IceBolt or CombatAbility.ChillingArmor => SpellEffectGraphics.IceBoltFlight,
            CombatAbility.IceBlast => SpellEffectGraphics.IceBlastFlight,
            CombatAbility.GlacialSpike => SpellEffectGraphics.GlacialSpikeFlight,
            CombatAbility.FireBolt => SpellEffectGraphics.FireBoltFlight,
            CombatAbility.Inferno => SpellEffectGraphics.InfernoFlight,
            _ => 0
        };
        return first == 0 ? null : DirectionalFrame(first, vx, vy, phase);
    }

    internal static void Armor(Mobile owner, CombatAbility ability)
    {
        int graphic = ability switch
        {
            CombatAbility.FrozenArmor => SpellEffectGraphics.FrozenArmorCast,
            CombatAbility.ShiverArmor => SpellEffectGraphics.ShiverArmorCast,
            _ => SpellEffectGraphics.ChillingArmorCast
        };
        owner.FixedEffect(graphic, 10, SpellEffectGraphics.WardOneShot);
    }

    internal static void Telekinesis(Mobile owner)
    {
        owner.FixedEffect(SpellEffectGraphics.TelekinesisCast, 10, SpellEffectGraphics.UtilityDuration);
        owner.PlaySound(CastSound(CombatAbility.Telekinesis));
    }

    internal static void Teleport(Mobile owner, Point3D origin, Map map)
    {
        Effects.SendLocationEffect(origin, map, SpellEffectGraphics.TeleportDeparture, SpellEffectGraphics.UtilityDuration);
        Effects.SendLocationEffect(owner.Location, owner.Map, SpellEffectGraphics.TeleportArrival, SpellEffectGraphics.UtilityDuration);
        owner.PlaySound(SpellEffectSounds.TeleportTransfer);
    }

    internal static void Impact(Mobile target, CombatAbility ability)
    {
        if (target.Deleted)
        {
            return;
        }

        switch (ability)
        {
            case CombatAbility.FireBolt:
                target.FixedEffect(SpellEffectGraphics.FireBoltContact, 10, SpellEffectGraphics.FireFrames);
                target.PlaySound(SpellEffectSounds.FireBoltContact);
                break;
            case CombatAbility.Inferno:
                target.FixedEffect(SpellEffectGraphics.InfernoContact, 10, SpellEffectGraphics.FireFrames);
                break;
            case CombatAbility.Lightning:
                target.FixedEffect(SpellEffectGraphics.LightningContact, 10, SpellEffectGraphics.LightningFrames);
                target.PlaySound(SpellEffectSounds.LightningContact);
                break;
            case CombatAbility.LightningStrike:
            case CombatAbility.ChainLightning:
                target.FixedEffect(SpellEffectGraphics.ChainLightningContact, 10, SpellEffectGraphics.LightningFrames);
                target.PlaySound(SpellEffectSounds.ChainLightningContact);
                break;
            case CombatAbility.ChargedBolt:
                target.FixedEffect(SpellEffectGraphics.ChargedBoltContact, 10, SpellEffectGraphics.LightningFrames);
                target.PlaySound(SpellEffectSounds.ChargedBoltContact);
                break;
            case CombatAbility.Nova:
                target.FixedEffect(SpellEffectGraphics.NovaContact, 10, SpellEffectGraphics.UtilityFrames);
                break;
            case CombatAbility.IceBolt:
            case CombatAbility.ChillingArmor:
                target.FixedEffect(SpellEffectGraphics.IceBoltContact, 10, SpellEffectGraphics.ColdFrames);
                target.PlaySound(SpellEffectSounds.IceBoltContact);
                break;
            case CombatAbility.IceBlast:
                target.FixedEffect(SpellEffectGraphics.IceBlastContact, 10, SpellEffectGraphics.ColdFrames);
                target.PlaySound(SpellEffectSounds.IceBlastContact);
                break;
            case CombatAbility.FrostNova:
                target.FixedEffect(SpellEffectGraphics.FrostNovaContact, 10, SpellEffectGraphics.ColdFrames);
                break;
        }
    }
}
