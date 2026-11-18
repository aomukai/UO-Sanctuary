// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class DruidSpiritVisuals
{
    private static int AuraGraphic(CombatAbility ability) => ability switch
    {
        CombatAbility.OakSage => SpellEffectGraphics.OakSageAura,
        CombatAbility.HeartOfWolverine => SpellEffectGraphics.WolverineAura,
        CombatAbility.SpiritOfBarbs => SpellEffectGraphics.BarbsAura,
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    private static bool OnMap(Mobile target) => !World.Loading && target is { Deleted: false }
        && target.Map != null && target.Map != Map.Internal;

    internal static void Aura(Mobile target, CombatAbility ability, DateTime until)
    {
        if (!OnMap(target)) { return; }

        int duration = (int)Math.Ceiling(Math.Min(DruidOakAuras.LeaseFrames * 40, (until - TimedEffects.Now).TotalMilliseconds) / 50);
        if (duration > 0) { target.FixedEffect(AuraGraphic(ability), 10, duration); }
    }

    internal static void ClearAura(Mobile target, CombatAbility ability)
    {
        if (OnMap(target)) { target.FixedEffect(AuraGraphic(ability), 10, 0); }
    }

    internal static void BarbsContact(Mobile defender, Point3D point, Map map)
    {
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.BarbsRetaliation, 6);
        SpellAreaSounds.Pulse(defender.Serial.Value, SpellEffectSounds.BarbsRetaliation, point, map, TimedEffects.Now, 140);
    }
}
