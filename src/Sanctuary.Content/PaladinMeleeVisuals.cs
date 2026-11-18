// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class PaladinMeleeVisuals
{
    private static (int Swing, int SwingCue, int Hit, int HitCue) EffectsFor(CombatAbility ability) => ability switch
    {
        CombatAbility.Sacrifice => (SpellEffectGraphics.SacrificeSwing, SpellEffectSounds.SacrificeSwing,
            SpellEffectGraphics.SacrificeImpact, SpellEffectSounds.SacrificeImpact),
        CombatAbility.Smite => (SpellEffectGraphics.SmiteSwing, SpellEffectSounds.SmiteSwing,
            SpellEffectGraphics.SmiteImpact, SpellEffectSounds.SmiteImpact),
        CombatAbility.Vengeance => (SpellEffectGraphics.VengeanceSwing, SpellEffectSounds.VengeanceSwing,
            SpellEffectGraphics.VengeanceImpact, SpellEffectSounds.VengeanceImpact),
        CombatAbility.Zeal => (SpellEffectGraphics.ZealSwing, SpellEffectSounds.ZealSwing,
            SpellEffectGraphics.ZealImpact, SpellEffectSounds.ZealImpact),
        CombatAbility.Charge => (SpellEffectGraphics.ChargeLaunch, SpellEffectSounds.ChargeLaunch,
            SpellEffectGraphics.ChargeImpact, SpellEffectSounds.ChargeImpact),
        CombatAbility.Conversion => (SpellEffectGraphics.ConversionSwing, SpellEffectSounds.ConversionSwing,
            SpellEffectGraphics.ZealImpact, SpellEffectSounds.ZealImpact),
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    internal static void Swing(Mobile owner, CombatAbility ability)
    {
        if (!PaladinAuraVisuals.InWorld(owner)) { return; }
        var style = EffectsFor(ability);
        owner.FixedEffect(style.Swing, 10, ability == CombatAbility.Zeal ? 4 : 6);
        SpellAreaSounds.Pulse(owner.Serial.Value, style.SwingCue, owner.Location, owner.Map, TimedEffects.Now, 90);
    }

    internal static void SacrificeCost(Mobile owner, Point3D point, Map map)
        => Burst(owner, point, map, SpellEffectGraphics.SacrificeCost, SpellEffectSounds.SacrificeCost);

    internal static void ChargeStep(Mobile owner, Point3D point)
    {
        if (!PaladinAuraVisuals.InWorld(owner)) { return; }
        Effects.SendLocationEffect(point, owner.Map, SpellEffectGraphics.ChargeTrail, 4);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.ChargeTrail, point, owner.Map, TimedEffects.Now, 200);
    }

    internal static void Land(Mobile owner, Point3D point, Map map, CombatAbility ability, MonsterContactResult outcome)
    {
        if (outcome.Commit is { Packet.ReactionSettled: false }) { Land(owner, point, map, ability); }
    }

    internal static void Land(Mobile owner, Point3D point, Map map, CombatAbility ability)
    {
        var style = EffectsFor(ability);
        Burst(owner, point, map, style.Hit, style.HitCue);
    }

    private static void Burst(Mobile owner, Point3D point, Map map, int graphic, int cue)
    {
        if (World.Loading || map == null || map == Map.Internal) { return; }
        Effects.SendLocationEffect(point, map, graphic, graphic == SpellEffectGraphics.ZealImpact ? 4 : 7);
        SpellAreaSounds.Pulse(owner.Serial.Value, cue, point, map, TimedEffects.Now, 100);
    }
}
