// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class BarbarianCryVisuals
{
    private static (int Graphic, int Male, int Female, int Wave, int Contact) Style(CombatAbility ability) => ability switch
    {
        CombatAbility.Howl => (SpellEffectGraphics.HowlCast, SpellEffectSounds.HowlCast, SpellEffectSounds.HowlCastFemale,
            SpellEffectGraphics.HowlWave, SpellEffectSounds.HowlMark),
        CombatAbility.Taunt => (SpellEffectGraphics.TauntCast, SpellEffectSounds.TauntCast, SpellEffectSounds.TauntCastFemale,
            0, SpellEffectSounds.TauntMark),
        CombatAbility.BattleCry => (SpellEffectGraphics.BattleCryCast, SpellEffectSounds.BattleCryCast, SpellEffectSounds.BattleCryCastFemale,
            SpellEffectGraphics.BattleCryWave, SpellEffectSounds.BattleCryMark),
        CombatAbility.WarCry => (SpellEffectGraphics.WarCryCast, SpellEffectSounds.WarCryCast, SpellEffectSounds.WarCryCastFemale,
            SpellEffectGraphics.WarCryWave, SpellEffectSounds.WarCryImpact),
        CombatAbility.Shout => (SpellEffectGraphics.ShoutCast, SpellEffectSounds.ShoutCast, SpellEffectSounds.ShoutCastFemale,
            SpellEffectGraphics.ShoutWave, SpellEffectSounds.ShoutWard),
        CombatAbility.BattleOrders => (SpellEffectGraphics.BattleOrdersCast, SpellEffectSounds.BattleOrdersCast,
            SpellEffectSounds.BattleOrdersCastFemale, SpellEffectGraphics.BattleOrdersWave, SpellEffectSounds.BattleOrdersWard),
        CombatAbility.BattleCommand => (SpellEffectGraphics.BattleCommandCast, SpellEffectSounds.BattleCommandCast,
            SpellEffectSounds.BattleCommandCastFemale, SpellEffectGraphics.BattleCommandWave, SpellEffectSounds.BattleCommandWard),
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    internal static void Cast(Mobile source, CombatAbility ability, DateTime now)
    {
        if (!PaladinAuraVisuals.InWorld(source)) { return; }
        var style = Style(ability);
        source.FixedEffect(style.Graphic, 10, 8);
        SpellAreaSounds.Pulse(source.Serial.Value, source.Female ? style.Female : style.Male, source.Location, source.Map, now, 260);
    }

    internal static void Wave(Map map, ShoutRay ray, double x, double y, int step, int range)
    {
        // Sample the accepted rays in eight directions after their wall checks.
        if (World.Loading || ray.Ray % 8 != 0 || step < 2 || (ray.X == x && ray.Y == y)) { return; }
        int frames = SpellEffectGraphics.BarbarianCryFrames;
        int phase = Math.Min(frames - 1, step * frames / range);
        int direction = SpellEffectGraphics.ProjectileFacing(x - ray.X, y - ray.Y);
        int graphic = Style(ray.Ability).Wave + direction * frames + phase;
        GroundSpellVisuals.Sample(map, x, y, ray.Z, graphic, 1, lift: 14);
    }

    internal static void Contact(Mobile source, CombatAbility ability, Point3D point, Map map, DateTime now)
    {
        if (World.Loading || map == null || map == Map.Internal) { return; }
        if (ability == CombatAbility.WarCry)
        { Effects.SendLocationEffect(point, map, SpellEffectGraphics.WarCryImpact, 7); }
        SpellAreaSounds.Pulse(source.Serial.Value, Style(ability).Contact, point, map, now, 250);
    }
}
