// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Collections;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

internal static class ShameCombat
{
    private static readonly Dictionary<Serial, DateTime> _rootUntil = new();
    private static readonly Dictionary<Serial, DateTime> _rootImmuneUntil = new();
    private static readonly (int X, int Y)[] _directions =
        [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)];

    internal static bool Rooted(Mobile mobile) => _rootUntil.GetValueOrDefault(mobile.Serial) > Core.Now;

    internal static void PruneRoots(DateTime now)
    {
        foreach (var serial in _rootImmuneUntil.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToArray())
        {
            _rootUntil.Remove(serial);
            _rootImmuneUntil.Remove(serial);
        }
    }

    private static void Root(Mobile target)
    {
        var now = Core.Now;
        if (_rootImmuneUntil.GetValueOrDefault(target.Serial) > now)
            return;

        _rootUntil[target.Serial] = now.AddSeconds(5);
        _rootImmuneUntil[target.Serial] = now.AddSeconds(25);
        if (target is not PlayerMobile)
            target.Paralyze(TimeSpan.FromSeconds(5));
        target.FixedEffect(0x376A, 10, 20);
    }

    internal static void Knockback(ShameEncounter source, Mobile target)
    {
        if (target is BaseCreature creature)
        {
            SourceKnockbacks.Begin(source, creature);
            return;
        }
        if (target is not PlayerMobile player)
            return;
        var at = player.Location;
        Server.Movement.Movement.Offset(source.GetDirectionTo(player), ref at);
        var map = player.Map;
        if (!Teleportation.Space(map, at, player) || !EncounterProfiles.ClearSight(map, player.Location, at))
            return;
        ClassCombat.Stop(player);
        player.Location = at;
    }

    internal static void Sidestep(ShameEncounter source)
    {
        var start = source.Location;
        var map = source.Map;
        var index = Utility.Random(_directions.Length);
        for (int i = 0; i < _directions.Length; i++)
        {
            var (dx, dy) = _directions[(i + index) % _directions.Length];
            var point = new Point3D(start.X + dx * 3, start.Y + dy * 3, start.Z);
            if (!Teleportation.Space(map, point))
                point = new Point3D(point.X, point.Y, map.GetAverageZ(point.X, point.Y));
            if (Math.Abs(point.Z - start.Z) > 8 || !Teleportation.Space(map, point)
                || !EncounterProfiles.ClearSight(map, start, point)
                || source.Home != Point3D.Zero && !Utility.InRange(source.Home, point, source.HomeLeashRadius))
                continue;
            source.StopCombatActions();
            source.Location = point;
            SorceressVisuals.Teleport(source, start, map);
            return;
        }
    }

    internal static void Throw(ShameEncounter source, Mobile target)
    {
        bool crossbow = source.Role == ShameRole.Corsair;
        bool explosive = source.Role == ShameRole.Diamond;
        var map = source.Map;
        source.MovingEffect(target, crossbow ? 0x1BFE : 0x1363, 12, 0, explosive, false);
        source.PlaySound(crossbow ? 0x237 : 0x5D3);
        Timer.DelayCall(TimeSpan.FromMilliseconds(380), () =>
        {
            if (source.Map != map || !EliteCombat.Enemy(source, target, 10)
                || ClassStats.CheckHit(source, target, 0, Utility.RandomDouble, projectile: true) == false)
                return;
            int damage = Utility.RandomMinMax(source.Basis.MinDamage, source.Basis.MaxDamage);
            int before = target.Hits;
            var point = target.Location;
            EliteCombat.Hit(source, target, damage, ResistanceType.Physical, ranged: true);
            if (crossbow && target.Alive)
                Root(target);
            else if (explosive)
            {
                Effects.SendLocationEffect(point, map, 0x36BD, 15);
                Effects.PlaySound(point, map, 0x207);
                double splash = Math.Max(0, before - target.Hits) * 0.25;
                foreach (var (dx, dy) in _directions)
                {
                    var cell = new Point3D(point.X + dx, point.Y + dy, point.Z);
                    DamageAt(source, cell, 0, splash, 0, ResistanceType.Physical);
                }
            }
        });
    }

    internal static void Cast(ShameEncounter source, Mobile target, CombatAbility ability, Func<bool> current)
    {
        if (!EliteCombat.Enemy(source, target, 12))
            return;
        var map = source.Map;
        var aim = target.Location;
        if (ability is CombatAbility.Weaken or CombatAbility.Decrepify or CombatAbility.LowerResist)
        {
            NecromancerCurseCasts.CastEncounterCurse(source, target, ability);
            return;
        }
        if (ability is CombatAbility.Lightning or CombatAbility.Fireball)
        {
            EliteBolts.Cast(source, target, ability);
            return;
        }

        var values = source.SpellEffects(ability);
        if (ability == CombatAbility.DimVision)
        {
            source.PublicOverheadMessage(MessageType.Emote, 0x35, false, "*throws sand*");
            var now = TimedEffects.Now;
            var actor = TimedEffects.Actor(target, now);
            var state = actor.Control ?? new NamedControlState(default, default, default, 0, 0, 0);
            var until = now.AddMilliseconds(Math.Max(1000, values.Eval("auralencalc") * 40));
            if (until > state.BlindUntil)
                actor.Control = state with { BlindUntil = until };
            target.FixedEffect(0x375A, 10, 15, 0x455, 0);
            return;
        }
        if (ability is not (CombatAbility.Twister or CombatAbility.Tornado or CombatAbility.Hurricane or CombatAbility.Firestorm))
            source.PlaySound(SorceressVisuals.CastSound(ability));
        double damage = source.RollSpellDamage256() / 256.0;
        bool Active() => current() && source is { Alive: true, Deleted: false, ReturningHome: false } && source.Map == map;
        void Hit(Point3D at, int radius, ResistanceType type, double factor = 1.0, double physical = 0, int chill = 0) =>
            DamageAt(source, at, radius, damage * factor, physical, type, chill);
        void Delay(int milliseconds, Action action) => Timer.DelayCall(TimeSpan.FromMilliseconds(milliseconds), () =>
        {
            if (Active())
                action();
        });
        void Repeat(int count, int interval, Action action)
        {
            var delay = TimeSpan.FromMilliseconds(interval);
            Timer.StartTimer(delay, delay, count, () =>
            {
                if (Active())
                    action();
            }, out _);
        }
        void Sweep(int lanes, int interval, Action<Point3D, int, int> visit)
        {
            var origin = source.Location;
            int dx = aim.X - origin.X, dy = aim.Y - origin.Y;
            int steps = Math.Max(1, Math.Max(Math.Abs(dx), Math.Abs(dy)));
            var trail = Enumerable.Repeat(origin, lanes).ToArray();
            var blocked = new bool[lanes];
            int step = 0;
            Repeat(steps, interval, () =>
            {
                double progress = ++step / (double)steps;
                for (int lane = 0; lane < lanes; lane++)
                {
                    if (blocked[lane])
                        continue;

                    double spread = (lane - (lanes - 1) / 2.0) * Math.Sin(progress * Math.PI) * 1.5;
                    var cell = new Point3D(origin.X + (int)Math.Round(dx * progress - dy * spread / steps),
                        origin.Y + (int)Math.Round(dy * progress + dx * spread / steps),
                        origin.Z + (int)Math.Round((aim.Z - origin.Z) * progress));
                    if (!EncounterProfiles.ClearSight(map, trail[lane], cell))
                    {
                        blocked[lane] = true;
                        continue;
                    }

                    trail[lane] = cell;
                    visit(cell, step, lane);
                }
            });
        }

        switch (ability)
        {
            case CombatAbility.Nova:
            case CombatAbility.FrostNova:
                bool frost = ability == CombatAbility.FrostNova;
                source.FixedEffect(frost ? SpellEffectGraphics.FrostNovaRing : SpellEffectGraphics.NovaRing, 10, 11);
                Hit(source.Location, 4, frost ? ResistanceType.Cold : ResistanceType.Energy,
                    chill: frost ? values.DurationFrames * 40 : 0);
                break;
            case CombatAbility.GlacialSpike:
                int graphic = SorceressVisuals.DirectionalFrame(SpellEffectGraphics.GlacialSpikeFlight,
                    aim.X - source.X, aim.Y - source.Y, 0);
                Effects.SendMovingEffect(source, new Entity(Serial.Zero, aim, map), graphic, 10, 0, false, false);
                Delay(360, () =>
                {
                    Effects.SendLocationEffect(aim, map, SpellEffectGraphics.GlacialSpikeBurst, SpellEffectGraphics.GlacialSpikeBurstDuration);
                    Hit(aim, 2, ResistanceType.Cold, chill: GlacialSpikes.FreezeFrames(values) * 40);
                });
                break;
            case CombatAbility.Blizzard:
                Repeat(8, 400, () =>
                {
                    var cell = GroundNear(map, aim, 2);
                    Effects.SendLocationEffect(cell, map, SpellEffectGraphics.BlizzardFall, SpellEffectGraphics.BlizzardShardDuration);
                    Hit(cell, 1, ResistanceType.Cold, 0.35, chill: Math.Max(800, values.DurationFrames * 40));
                });
                break;
            case CombatAbility.FireWall:
                bool alongY = Math.Abs(aim.X - source.X) >= Math.Abs(aim.Y - source.Y);
                var cells = new List<Point3D>(5);
                for (int i = -2; i <= 2; i++)
                {
                    var p = new Point3D(aim.X + (alongY ? 0 : i), aim.Y + (alongY ? i : 0), aim.Z);
                    if (map.CanFit(p.X, p.Y, p.Z, 1, checkMobiles: false, requireSurface: true)
                        && EncounterProfiles.ClearSight(map, aim, p))
                    {
                        cells.Add(p);
                        Effects.SendLocationEffect(p, map, SpellEffectGraphics.FireWallIgnite, 8);
                    }
                }
                Repeat(7, 500, () =>
                {
                    foreach (var cell in cells)
                    {
                        Effects.SendLocationEffect(cell, map, SpellEffectGraphics.FireWallField, 10);
                        Hit(cell, 0, ResistanceType.Fire, 0.25);
                    }
                });
                break;
            case CombatAbility.MoltenBoulder:
                source.FixedEffect(SpellEffectGraphics.BoulderFormation, 10, 12);
                Effects.SendMovingEffect(source, new Entity(Serial.Zero, aim, map), SpellEffectGraphics.BoulderRolling, 10, 0, false, false);
                Delay(400, () =>
                {
                    Effects.SendLocationEffect(aim, map, SpellEffectGraphics.BoulderBurst, 10);
                    Hit(aim, 1, ResistanceType.Fire, 0.5, damage * 0.5);
                });
                break;
            case CombatAbility.Twister:
            case CombatAbility.Tornado:
                bool tornado = ability == CombatAbility.Tornado;
                int windArt = tornado ? SpellEffectGraphics.TornadoTravel : SpellEffectGraphics.TwisterTravel;
                int radius = tornado ? (int)Math.Ceiling(SkillGeometry.Tiles(values.Eval("aurarangecalc"))) : 1;
                var windHits = new HashSet<Serial>();
                int stun = tornado ? 0 : (int)values.Eval("calc2");
                DruidWindVisuals.Release(source, tornado ? SpellEffectSounds.TornadoRelease : SpellEffectSounds.TwisterRelease);
                Sweep(tornado ? 1 : (int)values.Eval("calc1"), tornado ? 240 : 160, (cell, step, lane) =>
                {
                    DruidWindVisuals.Travel(map, cell.X, cell.Y, cell.Z, windArt, step + lane, tornado ? 5 : 4);
                    DamageAt(source, cell, radius, damage, 0, ResistanceType.Physical,
                        targetFilter: victim => windHits.Add(victim.Serial), stunFrames: stun);
                });
                break;
            case CombatAbility.Hurricane:
                int pulse = values.Param(4) * 40;
                int duration = (int)values.Eval("auralencalc") * 40;
                int hurricaneRadius = (int)values.Eval("aurarangecalc");
                HurricaneVisuals.Release(source);
                Repeat(duration / 400, 400, () => source.FixedEffect(SpellEffectGraphics.HurricaneCore, 10, 9));
                Repeat(duration / pulse, pulse, () => DamageAt(source, source.Location,
                    (int)Math.Ceiling(SkillGeometry.Tiles(hurricaneRadius)), damage / 3, 0, ResistanceType.Cold,
                    values.DurationFrames * 40, targetFilter: victim => DruidTornado.InPulse(source.X * 2,
                        source.Y * 2, victim.X * 2, victim.Y * 2, hurricaneRadius), kind: SkillDamageKind.Periodic));
                break;
            case CombatAbility.Firestorm:
                int facing = SpellEffectGraphics.ProjectileFacing(aim.X - source.X, aim.Y - source.Y);
                int patchFrames = NecromancerSummons.Source("firestorm", "Range") - 1;
                int patchDamageRate = NecromancerSummons.Source("firestorm", "DamageRate");
                DruidFireVisuals.FirestormRelease(source);
                var ignited = new HashSet<Point3D>();
                Sweep((int)values.Eval("calc1"), 200, (cell, step, lane) =>
                {
                    int art = SpellEffectGraphics.FirestormHead + facing * SpellEffectGraphics.DruidGroundFireFrames
                        + (step + lane) % SpellEffectGraphics.DruidGroundFireFrames;
                    GroundSpellVisuals.Sample(map, cell.X, cell.Y, cell.Z, art, 4);
                    if (!ignited.Add(cell))
                        return;

                    GroundSpellVisuals.Sample(map, cell.X, cell.Y, cell.Z, SpellEffectGraphics.DruidFireBed, 12);
                    Repeat(patchFrames, 40, () => DamageAt(source, cell, 0, damage / patchFrames, 0, ResistanceType.Fire,
                        kind: SkillDamageKind.Periodic, damageRate: patchDamageRate));
                });
                break;
            case CombatAbility.Fissure:
            case CombatAbility.Volcano:
                bool fissure = ability == CombatAbility.Fissure;
                Effects.SendLocationEffect(aim, map, fissure ? SpellEffectGraphics.FissureOpening : SpellEffectGraphics.VolcanoOpening, 14);
                Repeat(fissure ? 7 : 10, 520, () =>
                {
                    var cell = GroundNear(map, aim, 2);
                    Effects.SendLocationEffect(cell, map, fissure ? SpellEffectGraphics.FissureCrack : SpellEffectGraphics.VolcanoLanding, 12);
                    Hit(cell, 1, ResistanceType.Fire, fissure ? 0.45 : 0.225, fissure ? 0 : damage * 0.225);
                });
                break;
            case CombatAbility.Armageddon:
                source.FixedEffect(SpellEffectGraphics.ArmageddonRelease, 10, 14);
                Repeat(9, 700, () =>
                {
                    var cell = GroundNear(map, source.Location, 4);
                    Effects.SendLocationEffect(cell, map, SpellEffectGraphics.ArmageddonWarning, 10);
                    Delay(420, () =>
                    {
                        Effects.SendLocationEffect(cell, map, SpellEffectGraphics.ArmageddonFall, SpellEffectGraphics.ArmageddonFallFrames);
                        Hit(cell, 1, ResistanceType.Fire, 0.3, damage * 0.3);
                    });
                });
                break;
        }
    }

    private static Point3D GroundNear(Map map, Point3D center, int range)
    {
        for (int i = 0; i < 8; i++)
        {
            var point = new Point3D(center.X + Utility.RandomMinMax(-range, range), center.Y + Utility.RandomMinMax(-range, range), center.Z);
            if (map.CanFit(point.X, point.Y, point.Z, 1, checkMobiles: false, requireSurface: true)
                && EncounterProfiles.ClearSight(map, center, point))
                return point;
        }
        return center;
    }

    private static void DamageAt(ShameEncounter source, Point3D point, int radius, double amount, double physical,
        ResistanceType type, int chill = 0, Predicate<Mobile> targetFilter = null, int stunFrames = 0,
        SkillDamageKind kind = SkillDamageKind.Direct, int damageRate = 0)
    {
        var map = source.Map;
        using var victims = PooledRefList<Mobile>.Create();
        foreach (var target in map.GetMobilesInRange<Mobile>(point, radius))
            if (Math.Abs(target.Z - point.Z) < 12 && EncounterProfiles.ClearSight(map, point, target.Location)
                && EliteCombat.Enemy(source, target, 18) && targetFilter?.Invoke(target) != false)
                victims.Add(target);
        foreach (var target in victims)
        {
            int loss = EliteCombat.Damage(source, target, physical: physical + (type == ResistanceType.Physical ? amount : 0),
                fire: type == ResistanceType.Fire ? amount : 0, cold: type == ResistanceType.Cold ? amount : 0,
                lightning: type == ResistanceType.Energy ? amount : 0, kind: kind, damageRate: damageRate);
            if (chill > 0 && loss > 0)
                GearEffects.ApplyChill(target, Core.TickCount, chill, source);
            if (stunFrames > 0 && loss > 0 && target.Alive)
                CombatStuns.ApplyEncounter(target, stunFrames);
        }
    }
}
