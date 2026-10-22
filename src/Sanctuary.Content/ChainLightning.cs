// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record ChainDelay(uint Target, DateTime UntilUtc);
public sealed record ChainFlight(long Cast, uint Owner, int Map, int Rank, int Remaining, uint AimTarget,
    double X, double Y, int Z, double Vx, double Vy, int Frame, int Min256, int Max256,
    ChainDelay[] Delays, DateTime UntilUtc, DateTime NextUtc);

public sealed class ChainLightningProfiles : GenericPersistence
{
    public ChainLightningProfiles() : base("SanctuaryChainLightning", 120) { }
    public override void Serialize(IGenericWriter writer) { writer.Write(0); writer.Write(JsonSerializer.Serialize(ChainLightning.Snapshot())); }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown Chain Lightning version"); }
        ChainLightning.Restore(JsonSerializer.Deserialize<ChainFlight[]>(reader.ReadString()));
    }
}

public static class ChainLightning
{
    private static readonly Dictionary<long, ChainFlight> _flights = new();
    private static readonly List<long> _keys = new();
    private static long _nextCast;
    public static ChainLightningProfiles Profiles { get; private set; }
    public static int Frames => NecromancerSummons.Source("chainlightning", "Range");
    public static int DelayFrames => NecromancerSummons.Source("chainlightning", "NextDelay");
    public static void Configure() => Profiles = new();
    public static void Initialize() => Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromMilliseconds(40), Tick);
    private static (int Min, int Max) Damage(PlayerMobile owner, SkillValues values)
    {
        int potion = CombatPotions.Bonus(owner, "magical-penetration");
        int Scale(int damage) => (int)Math.Clamp(NamedElements.Enhance(owner, damage * (100 + potion) / 100.0, "ltng"), 0, int.MaxValue);
        return (Scale(values.Damage256(false)), Scale(values.Damage256(true)));
    }
    public static void Cast(PlayerMobile owner, Point3D aim, SkillValues values)
    {
        int dx = aim.X - owner.X, dy = aim.Y - owner.Y;
        if (dx == 0 && dy == 0) { dx = dy = 1; }
        var velocity = MissileVectors.Velocity(dx, dy, NecromancerSummons.Source("chainlightning", "Vel"));
        var damage = Damage(owner, values); var now = TimedEffects.Now; long id = ++_nextCast;
        _flights.Add(id, new(id, owner.Serial.Value, owner.Map.MapID, values.Rank, Math.Max(1, (int)values.Eval("calc1")), 0,
            owner.X, owner.Y, owner.Z, velocity.X, velocity.Y, 0, damage.Min, damage.Max, [], now.AddMilliseconds(Frames * 40), now));
    }
    private static PlayerMobile Owner(ChainFlight flight) => World.FindMobile((Serial)flight.Owner) is PlayerMobile { Alive: true, Deleted: false } player
        && ClassCreation.Profiles.TryGet(player.Serial, out var profile) && profile.Class == DiabloClass.Sorceress
        && (player.Map == Map.Internal || player.Map.MapID == flight.Map && !SpellHelper.IsTown(player.Location, player)) ? player : null;
    public static bool Delayed(ChainFlight flight, uint target, DateTime at)
    {
        foreach (var delay in flight.Delays) { if (delay.Target == target && delay.UntilUtc > at) { return true; } }
        return false;
    }
    private static Mobile NextTarget(PlayerMobile owner, ChainFlight flight, Mobile hit, double x, double y, DateTime at)
    {
        double radius = SkillGeometry.Tiles(SkillMechanics.For(CombatAbility.ChainLightning).Parameter(1));
        var point = new Point3D((int)Math.Round(x), (int)Math.Round(y), flight.Z);
        Mobile smallest = null, greater = null;
        foreach (var target in owner.Map.GetMobilesInRange<Mobile>(point, (int)Math.Ceiling(radius)))
        {
            double dx = target.X - x, dy = target.Y - y;
            // Aura filter 0x8A783 traces from the caster. Segment terrain is checked during travel.
            if (target == hit || dx * dx + dy * dy > radius * radius || Math.Abs(target.Z - flight.Z) >= 16
                || Delayed(flight, target.Serial.Value, at) || !ClassCombat.IsHostile(owner, target)
                || SpellHelper.IsTown(target.Location, owner) || !owner.Map.LineOfSight(new Point3D(owner.X, owner.Y, owner.Z + 12), new Point3D(target.X, target.Y, target.Z + 12))) { continue; }
            if (smallest == null || target.Serial.Value < smallest.Serial.Value) { smallest = target; }
            if (target.Serial.Value > hit.Serial.Value && (greater == null || target.Serial.Value < greater.Serial.Value)) { greater = target; }
        }
        return greater ?? smallest;
    }
    public static void Cancel(Mobile owner)
    {
        var ids = new List<long>(); foreach (var flight in _flights.Values) { if (flight.Owner == owner.Serial.Value) { ids.Add(flight.Cast); } }
        foreach (long id in ids) { _flights.Remove(id); }
    }
    private static void Tick()
    {
        var now = TimedEffects.Now; _keys.Clear(); _keys.AddRange(_flights.Keys);
        foreach (long id in _keys)
        {
            if (!_flights.TryGetValue(id, out var flight)) { continue; }
            var owner = Owner(flight);
            if (owner == null || flight.UntilUtc <= now) { _flights.Remove(id); continue; }
            if (owner.Map == Map.Internal || flight.NextUtc > now) { continue; }
            double x = flight.X + flight.Vx, y = flight.Y + flight.Vy;
            var from = new Point3D((int)Math.Round(flight.X), (int)Math.Round(flight.Y), flight.Z + 12);
            var to = new Point3D((int)Math.Round(x), (int)Math.Round(y), flight.Z + 12);
            if (x < 0 || y < 0 || x >= owner.Map.Width || y >= owner.Map.Height || !owner.Map.LineOfSight(from, to) || flight.Frame + 1 >= Frames)
            { _flights.Remove(id); continue; }
            Mobile first = null; double nearest = double.MaxValue;
            foreach (var target in SkillCombat.Targets(owner, new(to.X, to.Y, flight.Z), 2))
            {
                if (!Delayed(flight, target.Serial.Value, flight.NextUtc)
                    && CombatHitboxes.Intersect(flight.X, flight.Y, x, y, flight.Z, target.Location, out double entry) && entry < nearest)
                { first = target; nearest = entry; }
            }
            Effects.SendLocationEffect(new(to.X, to.Y, flight.Z), owner.Map, 0x3818, 3, 10, 0x482, 0); // Placeholder chain segment.
            if (first == null)
            { _flights[id] = flight with { X = x, Y = y, Frame = flight.Frame + 1, NextUtc = flight.NextUtc.AddMilliseconds(40) }; continue; }
            var delays = new List<ChainDelay>();
            foreach (var delay in flight.Delays) { if (delay.UntilUtc > flight.NextUtc) { delays.Add(delay); } }
            delays.Add(new(first.Serial.Value, flight.NextUtc.AddMilliseconds(DelayFrames * 40)));
            flight = flight with { Delays = delays.ToArray() };
            var next = flight.Remaining > 1 ? NextTarget(owner, flight, first, x, y, flight.NextUtc) : null;
            if (next == null) { _flights.Remove(id); }
            else
            {
                int dx = (int)Math.Round((next.X - x) * 2), dy = (int)Math.Round((next.Y - y) * 2);
                if (dx == 0 && dy == 0) { dx = dy = 1; }
                var velocity = MissileVectors.Velocity(dx, dy, NecromancerSummons.Source("chainlightning", "Vel"));
                // Legacy child factory recalculates damage with the original cast rank and current synergies/mastery.
                var damage = Damage(owner, SkillMechanics.Values(owner, CombatAbility.ChainLightning, flight.Rank));
                _flights[id] = flight with { Remaining = flight.Remaining - 1, AimTarget = next.Serial.Value, X = x, Y = y,
                    Vx = velocity.X, Vy = velocity.Y, Frame = 0, Min256 = damage.Min, Max256 = damage.Max,
                    UntilUtc = flight.NextUtc.AddMilliseconds(Frames * 40), NextUtc = flight.NextUtc.AddMilliseconds(40) };
            }
            SorceressEffects.MissileHit(owner, first, "chainlightning");
            int rolled = flight.Max256 <= flight.Min256 ? flight.Min256 : Utility.RandomMinMax(flight.Min256, flight.Max256 - 1);
            SkillCombat.LightningImpact(owner, first, rolled);
        }
    }
    public static ChainFlight[] Snapshot() => new List<ChainFlight>(_flights.Values).ToArray();
    public static void Restore(ChainFlight[] saved)
    {
        if (saved == null) { throw new InvalidDataException("Missing Chain Lightning flights"); }
        _flights.Clear(); _nextCast = 0;
        foreach (var flight in saved)
        {
            if (flight.Cast <= 0 || flight.Rank <= 0 || flight.Remaining <= 0 || flight.Frame < 0 || flight.Frame >= Frames
                || flight.Min256 < 0 || flight.Max256 < flight.Min256 || flight.Delays == null
                || !double.IsFinite(flight.X + flight.Y + flight.Vx + flight.Vy) || !_flights.TryAdd(flight.Cast, flight))
            { throw new InvalidDataException("Invalid Chain Lightning flight"); }
            var targets = new HashSet<uint>();
            foreach (var delay in flight.Delays)
            { if (delay.Target == 0 || !targets.Add(delay.Target)) { throw new InvalidDataException("Invalid Chain Lightning delay"); } }
            _nextCast = Math.Max(_nextCast, flight.Cast);
        }
    }
}
