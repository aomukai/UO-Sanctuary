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
    internal static void Cast(EncounterCreature source, Mobile target, CombatAbility ability)
    {
        double angle = Math.Atan2(target.Y - source.Y, target.X - source.X);
        source.PlaySound(SorceressVisuals.CastSound(ability switch
        {
            CombatAbility.Fireball => CombatAbility.FireBolt,
            CombatAbility.LightningBolt => CombatAbility.ChargedBolt,
            _ => ability
        }));
        Launch(source, angle, ability, new HashSet<Serial>());
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
        CombatAbility.IceBlast => SorceressVisuals.DirectionalFrame(SpellEffectGraphics.IceBlastFlight, dx, dy, phase),
        _ => 0xF42
    };
    private static string MissileId(CombatAbility ability) => ability switch
    {
        CombatAbility.FireBolt => "firebolt",
        CombatAbility.Fireball => "fireball",
        CombatAbility.LightningBolt or CombatAbility.Lightning or CombatAbility.ChargedBolt => "lightningbolt",
        CombatAbility.ChainLightning => "chainlightning",
        CombatAbility.IceBlast => "iceblast",
        _ => "arrow"
    };
    private static void Launch(EncounterCreature source, double angle, CombatAbility ability, HashSet<Serial> hit, int chargedDamage = 0)
    {
        if (_active.Count >= 4096 || (chargedDamage == 0 ? !EliteCombat.CanAct(source) : source.Deleted || !source.Alive)
            || source.Map == null || source.Map == Map.Internal) { return; }
        double dx = Math.Cos(angle), dy = Math.Sin(angle);
        var endpoint = new Point3D(source.X + (int)Math.Round(dx * 18), source.Y + (int)Math.Round(dy * 18), source.Z);
        // Existing native encounter bolts use one tile per source frame. Keep
        // that baseline adapter, but apply the compiled factory's integer slow
        // operation before its 75% conversion. Source missile geometry is a
        // separate acceptance gap; this is not a new D2 base-velocity claim.
        bool canSlow=NecromancerSummons.Source(MissileId(ability),"CanSlow")!=0;
        var slow=AmazonSlowMissiles.Capture(source,canSlow);
        if(slow.VelocityPercent==100)
        {source.MovingEffect(new Entity(Serial.Zero, endpoint, source.Map), Graphic(ability, dx, dy, 0), 10, 0, false, false);}
        int normal=SlowMissilePayload.VelocityFp8(0,0,0,20,256,false,100);
        int velocity=SlowMissilePayload.VelocityFp8(0,0,0,20,256,true,slow.VelocityPercent);
        _active.Add(new Bolt { Source = source, Map = source.Map, Origin = source.Location, Previous = source.Location,
            Dx = dx, Dy = dy, DistancePerFrame=velocity/(double)normal, Slow=slow,
            Steps = 18, Ability = ability, ChargedDamage = chargedDamage, Hit = hit });
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
            // Identified position marker for slowed native bolts, rather than
            // the ordinary client effect's unchanged travel speed. Final art pending.
            if(bolt.Slow.VelocityPercent!=100 && bolt.Step%2==0)
            {Effects.SendLocationEffect(point,bolt.Map,Graphic(bolt.Ability, bolt.Dx, bolt.Dy, bolt.Step / 2),2);}
            Mobile victim = null;
            foreach (var mobile in bolt.Map.GetMobilesInRange<Mobile>(point, 0))
            {
                if (EliteCombat.Enemy(source, mobile, 18) && (victim == null || mobile.Serial.Value < victim.Serial.Value)) { victim = mobile; }
            }
            if (victim == null) { continue; }
            if (bolt.Hit.Add(victim.Serial))
                Hit(bolt, victim);
            Remove(bolt);
        }
        _snapshot.Clear();
    }

    private static void Hit(Bolt bolt, Mobile victim)
    {
        var source = bolt.Source;
        var ability = bolt.Ability;
        if (ability == CombatAbility.None
            ? ClassStats.CheckHit(source, victim, 0, projectile: true) != true
            : ClassAvoidance.Missile(victim, MissileId(ability)))
            return;

        if (ability == CombatAbility.ChargedBolt)
        {
            EliteCombat.Damage(source, victim, lightning: bolt.Slow.Damage256(bolt.ChargedDamage * 256) / 256.0);
            return;
        }
        var attack = source.EncounterProfile.Attack1;
        int damage = Utility.RandomMinMax(attack.MinDamage.Value, attack.MaxDamage.Value);
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
