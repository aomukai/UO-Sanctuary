// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

// Monster casts keep the encounter damage roll and source allegiance. The player
// versions use the player's learned ranks, resources, and separate saved state.
internal static class DestardSpells
{
    internal static void Cast(DestardEncounter caster, Point3D aim, CombatAbility ability, Func<bool> current)
    {
        var map = caster.Map;
        caster.PlaySound(SorceressVisuals.CastSound(ability));

        bool Active() => current() && caster is { Alive: true, Deleted: false, ReturningHome: false }
            && caster.Map == map && caster.InRange(aim, 18);

        switch (ability)
        {
            case CombatAbility.FireWall:
                var line = FireLine(caster, aim);
                foreach (var cell in line)
                    Effects.SendLocationEffect(cell, map, SpellEffectGraphics.FireWallIgnite, 8);
                Pulse(caster, line, Active, 6, 500, SpellEffectGraphics.FireWallField, cold: false);
                break;
            case CombatAbility.Meteor:
                Effects.SendLocationEffect(aim, map, SpellEffectGraphics.MeteorWarning, 20);
                Timer.DelayCall(TimeSpan.FromMilliseconds(500), () =>
                {
                    if (!Active())
                        return;
                    Effects.SendLocationEffect(aim, map, SpellEffectGraphics.MeteorFall, SpellEffectGraphics.MeteorFrames);
                    Timer.DelayCall(TimeSpan.FromMilliseconds(400), () =>
                    {
                        if (!Active())
                            return;
                        Effects.SendLocationEffect(aim, map, SpellEffectGraphics.MeteorImpact, SpellEffectGraphics.MeteorImpactDuration);
                        DamageAt(caster, aim, 2, cold: false, 1.25);
                        Pulse(caster, [aim], Active, 4, 500, SpellEffectGraphics.MeteorFire, cold: false);
                    });
                });
                break;
            case CombatAbility.Blizzard:
                var shards = new List<Point3D>();
                for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                {
                    var cell = new Point3D(aim.X + x, aim.Y + y, aim.Z);
                    if (map.CanFit(cell.X, cell.Y, cell.Z, 1, checkMobiles: false, requireSurface: true)
                        && EncounterProfiles.ClearSight(map, aim, cell))
                        shards.Add(cell);
                }
                Pulses(Active, 7, 350, () =>
                {
                    if (shards.Count == 0)
                        return;
                    var cell = shards[Utility.Random(shards.Count)];
                    Effects.SendLocationEffect(cell, map, SpellEffectGraphics.BlizzardFall, SpellEffectGraphics.BlizzardShardDuration);
                    DamageAt(caster, cell, 1, cold: true, 0.65);
                });
                break;
            case CombatAbility.Hydra:
                var heads = FireLine(caster, aim, 1);
                Pulses(Active, 7, 650, () =>
                {
                    foreach (var head in heads)
                    {
                        Effects.SendLocationEffect(head, map, SpellEffectGraphics.HydraHeadRight, 14);
                        Mobile victim = null;
                        foreach (var enemy in map.GetMobilesInRange<Mobile>(head, 5))
                            if (EliteCombat.Enemy(caster, enemy, 18) && EncounterProfiles.ClearSight(map, head, enemy.Location)
                                && (victim == null || enemy.GetDistanceToSqrt(head) < victim.GetDistanceToSqrt(head)))
                                victim = enemy;

                        if (victim != null)
                            EliteBolts.CastFrom(caster, head, victim, CombatAbility.Hydra);
                    }
                });
                break;
        }
    }

    private static List<Point3D> FireLine(DestardEncounter caster, Point3D center, int halfWidth = 2)
    {
        bool alongY = Math.Abs(center.X - caster.X) >= Math.Abs(center.Y - caster.Y);
        var result = new List<Point3D>();
        for (int i = -halfWidth; i <= halfWidth; i++)
        {
            var cell = new Point3D(center.X + (alongY ? 0 : i), center.Y + (alongY ? i : 0), center.Z);
            if (caster.Map.CanFit(cell.X, cell.Y, cell.Z, 1, checkMobiles: false, requireSurface: true)
                && EncounterProfiles.ClearSight(caster.Map, center, cell) && EncounterProfiles.ClearSight(caster.Map, caster.Location, cell))
                result.Add(cell);
        }
        return result;
    }

    private static void Pulse(DestardEncounter caster, IReadOnlyList<Point3D> cells, Func<bool> active,
        int count, int interval, int graphic, bool cold)
    {
        Pulses(active, count, interval, () =>
        {
            foreach (var cell in cells)
            {
                Effects.SendLocationEffect(cell, caster.Map, graphic, 8);
                DamageAt(caster, cell, 0, cold, 0.45);
            }
        });
    }

    private static void Pulses(Func<bool> active, int count, int interval, Action pulse)
    {
        var delay = TimeSpan.FromMilliseconds(interval);
        Timer.StartTimer(delay, delay, count, () =>
        {
            if (active())
                pulse();
        }, out _);
    }

    private static void DamageAt(DestardEncounter caster, Point3D center, int radius, bool cold, double scale)
    {
        foreach (var victim in caster.Map.GetMobilesInRange<Mobile>(center, radius))
        {
            if (!EliteCombat.Enemy(caster, victim, 18) || Math.Abs(victim.Z - center.Z) >= 16
                || !EncounterProfiles.ClearSight(caster.Map, center, victim.Location))
                continue;

            double amount = Utility.RandomMinMax(caster.Basis.MinDamage, caster.Basis.MaxDamage) * scale;
            if (cold)
            {
                if (EliteCombat.Damage(caster, victim, cold: amount) > 0)
                    GearEffects.ApplyChill(victim, Core.TickCount, 2000, caster);
            }
            else
                EliteCombat.Damage(caster, victim, fire: amount);
        }
    }
}
