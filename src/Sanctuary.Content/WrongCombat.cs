// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Collections;
using Server.Mobiles;

namespace Sanctuary;

internal static class WrongCombat
{
    internal static void Charge(WrongEncounter source, Mobile target)
    {
        source.Direction = source.GetDirectionTo(target);
        source.PlaySound(0x235);
        for (int i = 0; i < 4 && EliteCombat.Enemy(source, target, 6) && !source.InRange(target, 1); i++)
        {
            if (!CombatDisplacement.TryStep(source, source.GetDirectionTo(target)))
                break;
        }
        if (EliteCombat.Enemy(source, target, 1))
        {
            source.SpecialHit(target, 0.65);
            CombatStuns.ApplyEncounter(target, 15);
        }
    }

    internal static void Zeal(WrongEncounter source, Mobile target, Map map, Point3D origin, int generation)
    {
        source.PlaySound(0x23B);
        int strikes = Math.Clamp((int)source.SpellEffects(CombatAbility.Zeal).Eval("calc1"), 1, 5);
        TimerExecutionToken sequence = default;
        Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromMilliseconds(270), strikes, () =>
        {
            if (!source.Active(map, generation) || source.Location != origin || !EliteCombat.Enemy(source, target, 1)
                || !EliteCombat.CanAct(source) || CreatureHitRecovery.Remaining(source) > 0)
            {
                sequence.Cancel();
                return;
            }
            source.Direction = source.GetDirectionTo(target);
            source.Animate(4, 5, 1, true, false, 0);
            source.SpecialHit(target, 0.55);
        }, out sequence);
    }

    internal static void Knockback(WrongEncounter source, Mobile target)
    {
        if (target is BaseCreature creature)
        {
            SourceKnockbacks.Begin(source, creature);
            return;
        }
        PushPlayer(target, source.GetDirectionTo(target));
    }

    private static void PushPlayer(Mobile target, Direction direction)
    {
        if (target is not PlayerMobile player)
            return;
        var point = player.Location;
        Server.Movement.Movement.Offset(direction, ref point);
        if (Teleportation.Space(player.Map, point, player) && EncounterProfiles.ClearSight(player.Map, player.Location, point))
        {
            ClassCombat.Stop(player);
            player.Location = point;
        }
    }

    internal static void Crossbow(WrongEncounter source, Mobile target, bool guided, int generation)
    {
        if (!guided)
        {
            source.PlaySound(0x237);
            EliteBolts.Cast(source, target, CombatAbility.None);
            return;
        }

        var map = source.Map;
        var origin = source.Location;
        var aim = target.Location;
        source.PlaySound(SpellEffectSounds.HexBaneCast);
        source.FixedEffect(SpellEffectGraphics.HexBaneCast, 10, 5);
        source.MovingEffect(target, SorceressVisuals.DirectionalFrame(SpellEffectGraphics.GuidedArrowBolt,
            aim.X - origin.X, aim.Y - origin.Y, 0), 12, 0, false, false);
        Timer.StartTimer(TimeSpan.FromMilliseconds(160 + (int)source.GetDistanceToSqrt(aim) * 40), () =>
        {
            if (!source.Active(map, generation) || !EliteCombat.Enemy(source, target, 12)
                || !EncounterProfiles.ClearSight(map, origin, target.Location) || ClassAvoidance.Missile(target, "guidedarrow"))
                return;

            var hex = HexBaneDamageProposal.Capture(source.SpellEffects(CombatAbility.HexBane), 0).Values;
            double amount = source.RollSpellDamage256() / 256.0;
            target.FixedEffect(SpellEffectGraphics.GuidedArrowContact, 10, 5);
            EliteCombat.Damage(source, target, physical: amount, magic: amount * 0.35, weapon: true, ranged: true);
            if (target.Alive)
                HexBane.ApplyEncounter(source, target, hex);
        });
    }

    internal static void Cast(WrongEncounter source, Mobile target, CombatAbility ability, int generation)
    {
        var map = source.Map;
        var origin = source.Location;
        var aim = target.Location;
        var values = source.SpellEffects(ability);
        double power = source.RollSpellDamage256() / 256.0;
        switch (ability)
        {
            case CombatAbility.MiasmaBolt:
                EliteBolts.Cast(source, target, ability);
                break;
            case CombatAbility.MiasmaChain:
                MiasmaChain(source, target, map, origin, generation, power);
                break;
            case CombatAbility.SigilLethargy:
                SigilLethargy.CastEncounter(source, aim, values);
                break;
            case CombatAbility.EldritchBlast:
                source.FixedEffect(SpellEffectGraphics.EldritchBlastCast, 10, 8);
                source.PlaySound(SpellEffectSounds.EldritchBlastCast);
                int range = Math.Clamp((int)Math.Ceiling(SkillGeometry.Tiles((int)values.Eval("aurarangecalc"))), 2, 6);
                Repeat(source, map, generation, 4, 700, _ =>
                {
                    Effects.SendLocationEffect(source.Location, map, SpellEffectGraphics.EldritchBlastRing, 8);
                    source.PlaySound(SpellEffectSounds.EldritchBlastPulse);
                    foreach (var enemy in Recipients(source, source.Location, range))
                    {
                        enemy.FixedEffect(SpellEffectGraphics.EldritchBlastContact, 10, 4);
                        EliteCombat.Damage(source, enemy, magic: power * 0.45, kind: SkillDamageKind.Periodic);
                    }
                });
                break;
            case CombatAbility.FlameWave:
                FlameWave(source, map, origin, aim, generation, power);
                break;
            case CombatAbility.Abyss:
                Effects.SendLocationEffect(aim, map, SpellEffectGraphics.AbyssOpen, 10);
                source.PlaySound(SpellEffectSounds.AbyssOpen);
                Repeat(source, map, generation, 5, SpellEffectGraphics.AbyssPulseMilliseconds, index =>
                {
                    bool collapse = index == 5;
                    Effects.SendLocationEffect(aim, map, collapse ? SpellEffectGraphics.AbyssCollapse : SpellEffectGraphics.AbyssField, 10);
                    Effects.PlaySound(aim, map, collapse ? SpellEffectSounds.AbyssCollapse : SpellEffectSounds.AbyssPull);
                    foreach (var enemy in Recipients(source, aim, 2))
                    {
                        if (!collapse && (enemy.X != aim.X || enemy.Y != aim.Y))
                        {
                            if (enemy is BaseCreature pet)
                                CombatDisplacement.TryStep(pet, pet.GetDirectionTo(aim));
                            else
                                PushPlayer(enemy, enemy.GetDirectionTo(aim));
                        }
                        EliteCombat.Damage(source, enemy, magic: power * (collapse ? 0.8 : 0.3));
                    }
                });
                break;
            case CombatAbility.Apocalypse:
                Effects.SendLocationEffect(aim, map, SpellEffectGraphics.ApocalypseWarning, 20);
                Effects.PlaySound(aim, map, SpellEffectSounds.ApocalypseWarning);
                Repeat(source, map, generation, 1, 1050, _ =>
                {
                    Effects.SendLocationEffect(aim, map, SpellEffectGraphics.ApocalypseEruption, 12);
                    Effects.PlaySound(aim, map, SpellEffectSounds.ApocalypseEruption);
                    foreach (var enemy in Recipients(source, aim, 3))
                    {
                        enemy.FixedEffect(SpellEffectGraphics.ApocalypseContact, 10, 6);
                        EliteCombat.Damage(source, enemy, fire: power * 1.6);
                    }
                });
                break;
        }
    }

    private static void MiasmaChain(WrongEncounter source, Mobile target, Map map, Point3D origin, int generation, double power)
    {
        source.FixedEffect(SpellEffectGraphics.MiasmaChainCast, 10, 8);
        source.PlaySound(SpellEffectSounds.MiasmaChainCast);
        var aim = target.Location;
        source.MovingEffect(target, SorceressVisuals.DirectionalFrame(SpellEffectGraphics.MiasmaChainLink,
            aim.X - origin.X, aim.Y - origin.Y, 0), 10, 0, false, false);
        Repeat(source, map, generation, 1, 300, _ =>
        {
            if (!EliteCombat.Enemy(source, target, 12) || !EncounterProfiles.ClearSight(map, origin, target.Location))
                return;
            MiasmaVisuals.Burst(map, target.X, target.Y, target.Z, chain: true);
            if (!ClassAvoidance.Missile(target, reaction: true))
                EliteCombat.Damage(source, target, magic: power);
            if (!target.Alive)
                return;

            Repeat(source, map, generation, 5, 520, _ =>
            {
                if (!EliteCombat.Enemy(source, target, 12))
                    return;
                var from = source.Location;
                var to = target.Location;
                int steps = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));
                if (steps == 0)
                    return;
                using var hit = PooledRefList<Mobile>.Create();
                for (int i = 1; i <= steps; i++)
                {
                    var cell = Interpolate(from, to, i, steps);
                    Effects.SendLocationEffect(cell, map, SpellEffectGraphics.MiasmaChainCloud, 10);
                    foreach (var enemy in Recipients(source, cell, 1))
                    {
                        if (!hit.Contains(enemy))
                        {
                            hit.Add(enemy);
                            EliteCombat.Damage(source, enemy, magic: power * 0.23, kind: SkillDamageKind.Periodic);
                        }
                    }
                }
            });
        });
    }

    private static void FlameWave(WrongEncounter source, Map map, Point3D origin, Point3D aim, int generation, double power)
    {
        source.FixedEffect(SpellEffectGraphics.FlameWaveCast, 10, 6);
        source.PlaySound(SpellEffectSounds.FlameWaveLaunch);
        int dx = aim.X - origin.X, dy = aim.Y - origin.Y;
        if (dx == 0 && dy == 0)
            return;
        int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        var tip = new Point3D(origin.X + (int)Math.Round(dx * 9.0 / distance),
            origin.Y + (int)Math.Round(dy * 9.0 / distance), origin.Z);
        var previous = origin;
        var hit = new HashSet<Serial>();
        bool blocked = false;
        Repeat(source, map, generation, 9, 100, index =>
        {
            var center = Interpolate(origin, tip, index, 9);
            if (blocked || !EncounterProfiles.ClearSight(map, previous, center))
            {
                blocked = true;
                return;
            }
            previous = center;
            Effects.SendLocationEffect(center, map, SpellEffectGraphics.FlameWaveField, 8);
            foreach (var enemy in Recipients(source, center, 1))
                if (hit.Add(enemy.Serial))
                    EliteCombat.Damage(source, enemy, fire: power);
        });
    }

    private static Point3D Interpolate(Point3D from, Point3D to, int step, int length) =>
        new(from.X + (int)Math.Round((to.X - from.X) * (double)step / length),
            from.Y + (int)Math.Round((to.Y - from.Y) * (double)step / length), from.Z);

    private static Mobile[] Recipients(WrongEncounter source, Point3D center, int range)
    {
        using var targets = PooledRefList<Mobile>.Create();
        foreach (var target in source.Map.GetMobilesInRange<Mobile>(center, range))
        {
            if (Math.Abs(target.Z - center.Z) < 16 && EliteCombat.Enemy(source, target, 18)
                && EncounterProfiles.ClearSight(source.Map, center, target.Location))
                targets.Add(target);
        }
        return targets.ToArray();
    }

    private static void Repeat(WrongEncounter source, Map map, int generation, int count, int milliseconds, Action<int> pulse)
    {
        var delay = TimeSpan.FromMilliseconds(milliseconds);
        TimerExecutionToken timer = default;
        int index = 0;
        Timer.StartTimer(delay, delay, count, () =>
        {
            if (source.Active(map, generation))
                pulse(++index);
            else
                timer.Cancel();
        }, out timer);
    }
}
