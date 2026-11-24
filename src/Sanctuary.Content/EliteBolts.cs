// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

// Missiles follow their launch vector. Moving targets, allies, walls and corners do not become homing hits.
public static class EliteBolts
{
    private sealed class Bolt
    {
        internal EncounterCreature Source;
        internal Map Map;
        internal Point3D Origin, Previous;
        internal double Dx, Dy, Distance, DistancePerFrame;
        internal SlowMissilePayload Slow;
        internal int Steps, Step, ChargedDamage;
        internal CombatAbility Ability;
        internal SkillValues RankedSpell;
        internal FrozenKind? OrbKind;
        internal HashSet<Serial> Hit;
    }
    private static readonly HashSet<Bolt> _active = new();
    private static readonly List<Bolt> _snapshot = new();
    internal static int ActiveCount => _active.Count;
    internal readonly record struct View(int Frame,double Distance,double DistancePerFrame,SlowMissilePayload Slow);
    internal static View[] Inspect(EncounterCreature source)
    {
        var result=new List<View>();foreach(var bolt in _active)
        {if(bolt.Source==source){result.Add(new(bolt.Step,bolt.Distance,bolt.DistancePerFrame,bolt.Slow));}}
        return result.ToArray();
    }
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40), Tick);
    internal static void Fan(EncounterCreature source, Mobile target, int count, bool fire)
    {
        double angle = Math.Atan2(target.Y - source.Y, target.X - source.X);
        var hit = new HashSet<Serial>();
        for (int i = 0; i < count; i++)
        {
            double offset = count == 1 ? 0 : (i - 1) * 0.22;
            Launch(source, angle + offset, fire ? CombatAbility.FireBolt : CombatAbility.None, hit);
        }
    }
    internal static void Cast(EncounterCreature source, Mobile target, CombatAbility ability, int rank = 0)
    {
        double angle = Math.Atan2(target.Y - source.Y, target.X - source.X);
        source.PlaySound(ability switch
        {
            CombatAbility.BoneSpear => SpellEffectSounds.BoneSpearRelease,
            CombatAbility.MiasmaBolt => SpellEffectSounds.MiasmaBoltCast,
            CombatAbility.Fireball => SorceressVisuals.CastSound(CombatAbility.FireBolt),
            CombatAbility.LightningBolt => SorceressVisuals.CastSound(CombatAbility.ChargedBolt),
            _ => SorceressVisuals.CastSound(ability)
        });
        var values = rank > 0 ? DeceitEncounter.RankedValues(ability, rank, source.Basis.Level) : null;
        Launch(source, angle, ability, new HashSet<Serial>(), rankedSpell: values);
    }
    internal static void CastFrom(EncounterCreature source, Point3D origin, Mobile target, CombatAbility ability)
    {
        double angle = Math.Atan2(target.Y - origin.Y, target.X - origin.X);
        Launch(source, angle, ability, [], originOverride: origin);
    }

    internal static void CastFrozenOrb(EncounterCreature source, Mobile target)
    {
        var origin = source.Location;
        double angle = Math.Atan2(target.Y - origin.Y, target.X - origin.X);
        source.PlaySound(SorceressVisuals.CastSound(CombatAbility.FrozenOrb));
        Launch(source, angle, CombatAbility.FrozenOrb, [], orbKind: FrozenKind.Orb);
    }

    internal static void Lightning(EncounterCreature source)
    {
        var hit = new HashSet<Serial>();
        for (int i = 0; i < 8; i++)
            Launch(source, i * Math.PI / 4, CombatAbility.ChargedBolt, hit, 2 * Math.Max(1, CreatureProfiles.Level(source) / 2) - 1);
        source.FixedEffect(0x37CC, 10, 15);
    }
    private static int Graphic(CombatAbility ability, double dx, double dy, int phase) => ability switch
    {
        CombatAbility.FireBolt or CombatAbility.Fireball => 0x36D4,
        CombatAbility.LightningBolt or CombatAbility.ChargedBolt => 0x3818,
        CombatAbility.Lightning => SorceressVisuals.DirectionalFrame(SpellEffectGraphics.LightningFlight, dx, dy, phase),
        CombatAbility.ChainLightning => SorceressVisuals.DirectionalFrame(SpellEffectGraphics.ChainLightningFlight, dx, dy, phase),
        CombatAbility.PoisonJavelin => SpellEffectGraphics.PoisonJavelin,
        CombatAbility.Hydra => SorceressVisuals.DirectionalFrame(SpellEffectGraphics.HydraBoltFlight, dx, dy, phase),
        CombatAbility.IceBlast => SorceressVisuals.DirectionalFrame(SpellEffectGraphics.IceBlastFlight, dx, dy, phase),
        CombatAbility.FrozenOrb => SpellEffectGraphics.FrozenOrb + phase % SpellEffectGraphics.ColdFrames,
        CombatAbility.BoneSpear => SpellEffectGraphics.BoneSpearFlight + SpellEffectGraphics.ProjectileFacing(dx, dy),
        CombatAbility.MiasmaBolt => SorceressVisuals.DirectionalFrame(SpellEffectGraphics.MiasmaBoltFlight, dx, dy, phase),
        _ => 0xF42
    };
    private static int ShotGraphic(EncounterCreature source, CombatAbility ability, double dx, double dy, int phase) =>
        source is MausoleumDemon { IsBodyguard: true } guard
            ? guard.BoltGraphic : source is DeceitEncounter { BoltGraphic: { } graphic } ? graphic
            : ability == CombatAbility.None && source is HythlothEncounter { Role: HythlothRole.RoyalArbalest }
            ? 0x1BFE : Graphic(ability, dx, dy, phase);

    private static string MissileId(CombatAbility ability) => ability switch
    {
        CombatAbility.FireBolt => "firebolt",
        CombatAbility.Fireball => "fireball",
        CombatAbility.LightningBolt or CombatAbility.Lightning or CombatAbility.ChargedBolt => "lightningbolt",
        CombatAbility.ChainLightning => "chainlightning",
        CombatAbility.IceBlast => "iceblast",
        CombatAbility.PoisonJavelin => "poisonjav",
        CombatAbility.Hydra => "hydra",
        CombatAbility.FrozenOrb => "frozenorb",
        CombatAbility.BoneSpear => "bonespear",
        CombatAbility.MiasmaBolt => "miasmabolt",
        _ => "arrow"
    };
    private static void Launch(EncounterCreature source, double angle, CombatAbility ability, HashSet<Serial> hit, int chargedDamage = 0,
        Point3D? originOverride = null, FrozenKind? orbKind = null, SkillValues rankedSpell = null)
    {
        if (_active.Count >= 4096 || (chargedDamage == 0 ? !EliteCombat.CanAct(source) : source.Deleted || !source.Alive)
            || source.Map == null || source.Map == Map.Internal) { return; }
        double dx = Math.Cos(angle), dy = Math.Sin(angle);
        var origin = originOverride ?? source.Location;
        int range = orbKind switch { FrozenKind.Orb => 6, FrozenKind.Bolt => 4, FrozenKind.Nova => 5, _ => 18 };
        var endpoint = new Point3D(origin.X + (int)Math.Round(dx * range), origin.Y + (int)Math.Round(dy * range), origin.Z);
        // Existing native encounter bolts use one tile per source frame. Keep
        // that baseline adapter, but apply the compiled factory's integer slow
        // operation before its 75% conversion. Source missile geometry is a
        // separate acceptance gap; this is not a new D2 base-velocity claim.
        string missile = orbKind switch { FrozenKind.Bolt => "frozenorbbolt", FrozenKind.Nova => "frozenorbnova", _ => MissileId(ability) };
        bool canSlow=NecromancerSummons.Source(missile,"CanSlow")!=0;
        var slow=AmazonSlowMissiles.Capture(source,canSlow);
        if (orbKind == null && slow.VelocityPercent == 100)
        {
            var destination = new Entity(Serial.Zero, endpoint, source.Map);
            int graphic = ShotGraphic(source, ability, dx, dy, 0);
            if (originOverride.HasValue)
                Effects.SendMovingEffect(new Entity(Serial.Zero, origin, source.Map), destination, graphic, 10, 0, false, false);
            else
                source.MovingEffect(destination, graphic, 10, 0, false, false);
        }
        int normal=SlowMissilePayload.VelocityFp8(0,0,0,20,256,false,100);
        int velocity=SlowMissilePayload.VelocityFp8(0,0,0,20,256,true,slow.VelocityPercent);
        _active.Add(new Bolt { Source = source, Map = source.Map, Origin = origin, Previous = origin,
            Dx = dx, Dy = dy, DistancePerFrame=(orbKind == FrozenKind.Orb ? 0.25 : 1) * velocity/normal, Slow=slow,
            Steps = orbKind == FrozenKind.Orb ? 24 : range, Ability = ability, ChargedDamage = chargedDamage, Hit = hit,
            OrbKind = orbKind, RankedSpell = rankedSpell });
    }
    internal static void Cancel(EncounterCreature source)
    {
        using var remove = Server.Collections.PooledRefList<Bolt>.Create();
        foreach (var bolt in _active) { if (bolt.Source == source) { remove.Add(bolt); } }
        foreach (var bolt in remove) { Remove(bolt); }
    }
    private static void Remove(Bolt bolt)
    { _active.Remove(bolt); bolt.Source = null; bolt.Map = null; bolt.Hit = null; }
    internal static void Tick()
    {
        _snapshot.Clear(); _snapshot.AddRange(_active);
        foreach (var bolt in _snapshot)
        {
            var source = bolt.Source;
            if (source is not { Deleted: false, Alive: true } || source.Map != bolt.Map || ++bolt.Step > bolt.Steps)
            { Remove(bolt); continue; }
            bolt.Distance+=bolt.DistancePerFrame;
            var point = new Point3D(bolt.Origin.X + (int)Math.Round(bolt.Dx * bolt.Distance),
                bolt.Origin.Y + (int)Math.Round(bolt.Dy * bolt.Distance), bolt.Origin.Z);
            if (!EncounterProfiles.ClearSight(bolt.Map, bolt.Previous, point)) { Remove(bolt); continue; }
            bolt.Previous = point;
            if (bolt.OrbKind == FrozenKind.Orb)
            {
                AdvanceFrozenOrb(bolt, point);
                continue;
            }
            if (bolt.OrbKind.HasValue && bolt.Step % 2 == 1)
            {
                int phase = (bolt.Step / 2) % SpellEffectGraphics.ColdFrames;
                int facing = SpellEffectGraphics.ProjectileFacing(bolt.Dx, bolt.Dy);
                Effects.SendLocationEffect(point, bolt.Map,
                    SpellEffectGraphics.FrozenOrbBolt + facing * SpellEffectGraphics.ColdFrames + phase, 2);
            }
            // Identified position marker for slowed native bolts, rather than
            // the ordinary client effect's unchanged travel speed. Final art pending.
            if(bolt.OrbKind == null && bolt.Slow.VelocityPercent!=100 && bolt.Step%2==0)
            {Effects.SendLocationEffect(point,bolt.Map,ShotGraphic(source, bolt.Ability, bolt.Dx, bolt.Dy, bolt.Step / 2),2);}
            Mobile victim = null;
            foreach (var mobile in bolt.Map.GetMobilesInRange<Mobile>(point, 0))
            {
                if (bolt.Ability == CombatAbility.BoneSpear && bolt.Hit.Contains(mobile.Serial))
                    continue;
                if (EliteCombat.Enemy(source, mobile, 18) && (victim == null || mobile.Serial.Value < victim.Serial.Value)) { victim = mobile; }
            }
            if (victim == null) { continue; }
            if (bolt.Hit.Add(victim.Serial))
                Hit(bolt, victim);
            if (bolt.Ability != CombatAbility.BoneSpear)
                Remove(bolt);
        }
        _snapshot.Clear();
    }

    private static void AdvanceFrozenOrb(Bolt bolt, Point3D point)
    {
        var source = bolt.Source;
        if (bolt.Step == bolt.Steps)
        {
            Effects.SendLocationEffect(point, bolt.Map, SpellEffectGraphics.FrozenOrbBurst, SpellEffectGraphics.ColdFrames);
            Effects.PlaySound(point, bolt.Map, SpellEffectSounds.FrozenOrbBurst);
            var victims = new HashSet<Serial>();
            for (int ray = 0; ray < 16; ray++)
                Launch(source, ray * Math.PI / 8, CombatAbility.FrozenOrb, victims, originOverride: point, orbKind: FrozenKind.Nova);
            Remove(bolt);
        }
        else if (bolt.Step % 2 == 1)
        {
            int phase = (bolt.Step / 2) % SpellEffectGraphics.ColdFrames;
            Effects.SendLocationEffect(point, bolt.Map, SpellEffectGraphics.FrozenOrb + phase, 2);
            Launch(source, (bolt.Step / 2) * Math.PI / 5, CombatAbility.FrozenOrb,
                bolt.Hit, originOverride: point, orbKind: FrozenKind.Bolt);
        }
    }

    private static void Hit(Bolt bolt, Mobile victim)
    {
        var source = bolt.Source;
        var ability = bolt.Ability;
        if (ability == CombatAbility.None
            ? ClassStats.CheckHit(source, victim, 0, projectile: true) != true
            : ClassAvoidance.Missile(victim, MissileId(ability)))
            return;

        if (bolt.OrbKind.HasValue)
        {
            var orbAttack = source.EncounterProfile.Attack1;
            int cold = Utility.RandomMinMax(orbAttack.MinDamage.Value, orbAttack.MaxDamage.Value);
            Effects.SendLocationEffect(victim.Location, victim.Map, SpellEffectGraphics.FrozenOrbContact, SpellEffectGraphics.ColdFrames);
            EliteCombat.Damage(source, victim, cold: bolt.Slow.Damage256(cold * 128) / 256.0);
            GearEffects.ApplyChill(victim, Core.TickCount, 2000, source);
            return;
        }
        if (ability == CombatAbility.ChargedBolt)
        {
            EliteCombat.Damage(source, victim, lightning: bolt.Slow.Damage256(bolt.ChargedDamage * 256) / 256.0);
            return;
        }
        if (bolt.RankedSpell is { } values)
        {
            double amount = bolt.Slow.Damage256(Utility.RandomMinMax(values.Damage256(false), values.Damage256(true))) / 256.0;
            switch (ability)
            {
                case CombatAbility.Lightning:
                    SorceressVisuals.Impact(victim, ability);
                    EliteCombat.Damage(source, victim, lightning: amount);
                    break;
                case CombatAbility.BoneSpear:
                    victim.FixedEffect(SpellEffectGraphics.BoneSpearContact, 10, 4);
                    victim.PlaySound(SpellEffectSounds.BoneSpearContact);
                    EliteCombat.Damage(source, victim, magic: amount);
                    break;
                case CombatAbility.MiasmaBolt:
                    EliteCombat.Damage(source, victim, magic: amount);
                    MiasmaCloud(source, victim.Location, values, bolt.Slow);
                    break;
            }
            return;
        }
        var attack = source.EncounterProfile.Attack1;
        int damage = Utility.RandomMinMax(attack.MinDamage.Value, attack.MaxDamage.Value);
        if (ability == CombatAbility.PoisonJavelin)
        {
            victim.FixedEffect(SpellEffectGraphics.PoisonAfflicted, 10, SpellEffectGraphics.PoisonFrames);
            GearEffects.ApplyPoison(source, victim, bolt.Slow.Damage256(damage * 256) / 256.0, Core.TickCount, 2400);
            return;
        }
        if (ability == CombatAbility.Hydra)
        {
            victim.FixedEffect(SpellEffectGraphics.HydraBoltContact, 10, SpellEffectGraphics.HydraPoseFrames);
            EliteCombat.Damage(source, victim, fire: bolt.Slow.Damage256(damage * 128) / 256.0);
            return;
        }
        if (ability is CombatAbility.Fireball or CombatAbility.LightningBolt or CombatAbility.Lightning
            or CombatAbility.ChainLightning or CombatAbility.IceBlast)
        {
            double scaled = bolt.Slow.Damage256(damage * 256) / 256.0;
            switch (ability)
            {
                case CombatAbility.Fireball:
                    FireballImpact(source, victim, scaled);
                    break;
                case CombatAbility.ChainLightning:
                    ChainImpact(source, victim, bolt.Hit, scaled);
                    break;
                case CombatAbility.IceBlast:
                    SorceressVisuals.Impact(victim, ability);
                    EliteCombat.Damage(source, victim, cold: scaled);
                    GearEffects.ApplyChill(victim, Core.TickCount, 2000, source);
                    break;
                default:
                    SorceressVisuals.Impact(victim, ability == CombatAbility.LightningBolt ? CombatAbility.ChargedBolt : ability);
                    EliteCombat.Damage(source, victim, lightning: scaled);
                    break;
            }
            return;
        }
        if (Utility.Random(100) < source.Basis.CriticalChance)
            damage *= 2;
        EliteCombat.Hit(source, victim, damage, ability == CombatAbility.FireBolt ? ResistanceType.Fire : ResistanceType.Physical,
            true, missile: bolt.Slow);
    }

    private static void MiasmaCloud(EncounterCreature source, Point3D center, SkillValues values, SlowMissilePayload slow)
    {
        var map = source.Map;
        var packet = MiasmaPackets.Capture(values);
        int radius = packet.CloudRadius;
        int range = (int)Math.Ceiling(SkillGeometry.Tiles(radius));
        int graphic = SpellEffectGraphics.MiasmaBoltCloud + (radius - 2) * SpellEffectGraphics.MiasmaFrames;
        var nextHit = new Dictionary<Serial, long>();
        var delay = TimeSpan.FromMilliseconds(240);
        MiasmaVisuals.Burst(map, center.X, center.Y, center.Z, chain: false);
        TimerExecutionToken pulse = default;
        Timer.StartTimer(TimeSpan.Zero, delay, (packet.CloudFrames + 5) / 6, () =>
        {
            if (source is not { Alive: true, Deleted: false, ReturningHome: false } || source.Map != map || !source.InRange(center, 18))
            {
                pulse.Cancel();
                return;
            }
            Effects.SendLocationEffect(center, map, graphic, 6);
            long now = Core.TickCount;
            foreach (var target in map.GetMobilesInRange<Mobile>(center, range))
            {
                if (nextHit.GetValueOrDefault(target.Serial) > now || Math.Abs(target.Z - center.Z) >= 16
                    || !AmazonInnerSight.InRadius(target.X - center.X, target.Y - center.Y, radius)
                    || !EliteCombat.Enemy(source, target, 18) || !EncounterProfiles.ClearSight(map, center, target.Location))
                    continue;
                nextHit[target.Serial] = now + 1000;
                double damage = slow.Damage256(Utility.RandomMinMax(packet.CloudMinimum256, packet.CloudMaximum256)) / 256.0;
                EliteCombat.Damage(source, target, magic: damage, kind: SkillDamageKind.Periodic);
            }
        }, out pulse);
    }

    private static void FireballImpact(EncounterCreature source, Mobile victim, double damage)
    {
        var center = victim.Location;
        var map = victim.Map;
        Effects.SendLocationEffect(center, map, 0x36BD, 8, 10, 0x489);
        EliteCombat.Damage(source, victim, fire: damage);
        foreach (var target in map.GetMobilesInRange<Mobile>(center, 2))
        {
            if (target != victim && EliteCombat.Enemy(source, target, 20)
                && EncounterProfiles.ClearSight(map, center, target.Location))
                EliteCombat.Damage(source, target, fire: damage);
        }
    }

    private static void ChainImpact(EncounterCreature source, Mobile victim, HashSet<Serial> hit, double damage)
    {
        var map = victim.Map;
        var origin = victim.Location;
        SorceressVisuals.Impact(victim, CombatAbility.ChainLightning);
        EliteCombat.Damage(source, victim, lightning: damage);

        for (int jump = 0; jump < 2; jump++)
        {
            Mobile next = null;
            double nearest = double.MaxValue;
            foreach (var target in map.GetMobilesInRange<Mobile>(origin, 5))
            {
                if (hit.Contains(target.Serial) || !EliteCombat.Enemy(source, target, 28)
                    || Math.Abs(target.Z - origin.Z) >= 16 || !EncounterProfiles.ClearSight(map, origin, target.Location))
                    continue;
                double distance = target.GetDistanceToSqrt(origin);
                if (distance < nearest || distance == nearest && target.Serial.Value < next.Serial.Value)
                {
                    next = target;
                    nearest = distance;
                }
            }
            if (next == null)
                return;

            var destination = next.Location;
            int graphic = Graphic(CombatAbility.ChainLightning, destination.X - origin.X, destination.Y - origin.Y, jump);
            Effects.SendMovingEffect(new Entity(Serial.Zero, origin, map), next, graphic, 10, 0, false, false);
            hit.Add(next.Serial);
            origin = destination;
            if (!ClassAvoidance.Missile(next, "chainlightning"))
            {
                SorceressVisuals.Impact(next, CombatAbility.ChainLightning);
                EliteCombat.Damage(source, next, lightning: damage);
            }
        }
    }
}
