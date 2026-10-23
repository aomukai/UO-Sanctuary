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
        internal int Steps, Step, Lightning;
        internal bool Fire;
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
            Launch(source, angle + offset, fire, 0, hit);
        }
    }
    internal static void Lightning(EncounterCreature source)
    {
        var hit = new HashSet<Serial>();
        for (int i = 0; i < 8; i++) { Launch(source, i * Math.PI / 4, false, 2 * Math.Max(1, CreatureProfiles.Level(source) / 2) - 1, hit); }
        source.FixedEffect(0x37CC, 10, 15);
    }
    private static void Launch(EncounterCreature source, double angle, bool fire, int lightning, HashSet<Serial> hit)
    {
        if (_active.Count >= 4096 || (lightning == 0 ? !EliteCombat.CanAct(source) : source.Deleted || !source.Alive) || source.Map == null || source.Map == Map.Internal) { return; }
        var endpoint = new Point3D(source.X + (int)Math.Round(Math.Cos(angle) * 18), source.Y + (int)Math.Round(Math.Sin(angle) * 18), source.Z);
        // Existing native encounter bolts use one tile per source frame. Keep
        // that baseline adapter, but apply the compiled factory's integer slow
        // operation before its 75% conversion. Source missile geometry is a
        // separate acceptance gap; this is not a new D2 base-velocity claim.
        bool canSlow=NecromancerSummons.Source(fire?"firebolt":lightning>0?"lightningbolt":"arrow","CanSlow")!=0;
        var slow=AmazonSlowMissiles.Capture(source,canSlow);
        if(slow.VelocityPercent==100)
        {source.MovingEffect(new Entity(Serial.Zero, endpoint, source.Map), fire ? 0x36D4 : lightning > 0 ? 0x3818 : 0xF42, 10, 0, false, false);}
        int normal=SlowMissilePayload.VelocityFp8(0,0,0,20,256,false,100);
        int velocity=SlowMissilePayload.VelocityFp8(0,0,0,20,256,true,slow.VelocityPercent);
        _active.Add(new Bolt { Source = source, Map = source.Map, Origin = source.Location, Previous = source.Location,
            Dx = Math.Cos(angle), Dy = Math.Sin(angle), DistancePerFrame=velocity/(double)normal, Slow=slow,
            Steps = 18, Fire = fire, Lightning = lightning, Hit = hit });
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
            {Effects.SendLocationEffect(point,bolt.Map,bolt.Fire?0x36D4:bolt.Lightning>0?0x3818:0xF42,2);}
            Mobile victim = null;
            foreach (var mobile in bolt.Map.GetMobilesInRange<Mobile>(point, 0))
            {
                if (EliteCombat.Enemy(source, mobile, 18) && (victim == null || mobile.Serial.Value < victim.Serial.Value)) { victim = mobile; }
            }
            if (victim == null) { continue; }
            if (bolt.Hit.Add(victim.Serial))
            {
                if (bolt.Lightning > 0) { if(!AmazonAvoidance.Missile(victim)){EliteCombat.Damage(source, victim, lightning: bolt.Slow.Damage256(bolt.Lightning*256)/256.0);} }
                else if (bolt.Fire || ClassStats.CheckHit(source, victim,0,projectile:true) == true)
                {
                    var attack = source.EncounterProfile.Attack1;
                    int damage = Utility.RandomMinMax(attack.MinDamage.Value, attack.MaxDamage.Value);
                    if(bolt.Fire && AmazonAvoidance.Missile(victim)){Remove(bolt);continue;}
                    if (Utility.Random(100) < source.Basis.CriticalChance) { damage *= 2; }
                    EliteCombat.Hit(source, victim, damage, bolt.Fire ? ResistanceType.Fire : ResistanceType.Physical, true, missile:bolt.Slow);
                }
            }
            Remove(bolt);
        }
        _snapshot.Clear();
    }
}
