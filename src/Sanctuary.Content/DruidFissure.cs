// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal sealed record FissureCenter(long Id, uint Owner, int Map, int Rank, int X, int Y, int Z,
    int Frame, ulong Seed, DateTime NextUtc, DateTime UntilUtc);
internal sealed record FissureCrack(long Id, long Cast, uint Owner, int Map, int Rank, int X, int Y, int Z,
    int Frame, int Min256, int Max256, FirestormModifiers Modifiers, ulong Seed, uint LastTarget,
    DateTime NextUtc, DateTime UntilUtc);
internal sealed record FissureSave(FissureCenter[] Centers, FissureCrack[] Cracks, WeaponFlightLease[] Leases, long NextId)
{
    public string DelayScope { get; init; } = "cast-target-provisional-2.7";
}

public sealed class FissureProfiles : GenericPersistence
{
    public FissureProfiles() : base("SanctuaryFissure", 167) { }
    public override void Serialize(IGenericWriter writer)
    { writer.Write(0); writer.Write(JsonSerializer.Serialize(DruidFissure.Snapshot())); }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown Fissure version"); }
        DruidFissure.Restore(JsonSerializer.Deserialize<FissureSave>(reader.ReadString()));
    }
}

// Prepared source234 center/cracks. No learned handler is enabled here.
// Positive-range placement, fresh endpoints, lifetime and LastCollide follow
// the pinned source proofs. Modern delay grouping and cross-producer missile
// footprints remain explicit integration limits before learned installation.
public static class DruidFissure
{
    private sealed record Pool(ImmutableDictionary<long, FissureCenter> Centers,
        ImmutableDictionary<long, FissureCrack> Cracks,
        ImmutableDictionary<(long, uint), WeaponFlightLease> Leases, long NextId);
    private static Pool _pool = new(ImmutableDictionary<long, FissureCenter>.Empty,
        ImmutableDictionary<long, FissureCrack>.Empty,
        ImmutableDictionary<(long, uint), WeaponFlightLease>.Empty, 0);
    internal static Func<int> SeedLow = () => unchecked((int)Random.Shared.NextInt64(1L << 32));
    // Explicit adapter boundary: own crack footprints are implemented below.
    // Other producers must supply their Collision1 footprints before this
    // component is represented as a complete shared missile-mask consumer.
    internal static Func<int, int, int, int, DateTime, bool> OtherMissileFootprint = (_, _, _, _, _) => false;
    public static FissureProfiles Profiles { get; private set; }
    public static void Configure() => Profiles = new();
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40), () => Tick(TimedEffects.Now));
    internal static FissureSave Snapshot()
    {
        var pool = _pool;
        return new(pool.Centers.Values.OrderBy(x => x.Id).ToArray(), pool.Cracks.Values.OrderBy(x => x.Id).ToArray(),
            pool.Leases.Values.OrderBy(x => x.Cast).ThenBy(x => x.Target).ToArray(), pool.NextId);
    }
    internal static void Cancel(Mobile owner)
    {
        if (owner == null) { return; }
        var pool = _pool;
        var centers = pool.Centers.RemoveRange(pool.Centers.Where(x => x.Value.Owner == owner.Serial.Value).Select(x => x.Key));
        var cracks = pool.Cracks.RemoveRange(pool.Cracks.Where(x => x.Value.Owner == owner.Serial.Value).Select(x => x.Key));
        _pool = pool with { Centers = centers, Cracks = cracks };
        PruneLeases(DateTime.MinValue);
    }
    private static PlayerMobile Owner(uint serial) => World.FindMobile((Serial)serial) is PlayerMobile { Alive: true, Deleted: false } p
        && ClassCreation.Profiles.TryGet(p.Serial, out var profile) && profile.Class == DiabloClass.Druid ? p : null;
    private static bool Fighting(PlayerMobile owner, int map, bool? actions) => owner?.Map != null && owner.Map != Map.Internal
        && owner.Map.MapID == map && (actions ?? owner.NetState is { Running: true });
    private static Point3D Location(int x, int y, int z) => new((x + 1) / 2, (y + 1) / 2, z);
    private static void Routes()
    {
        foreach (string key in new[] { "erruption center", "erruption crack 1" })
        {
            bool center = key == "erruption center";
            int F(string field) => NecromancerSummons.Source(key, field);
            if (F("pSrvDoFunc") != (center ? 25 : 1) || F("pSrvDmgFunc") != (center ? 0 : 8) || F("pSrvHitFunc") != 0
                || F("Range") != (center ? 80 : 84) || F("Size") != (center ? 1 : 3) || F("Vel") != 0
                || F("CollideType") != (center ? 0 : 3) || F("Collision") != (center ? 0 : 1)
                || F("LastCollide") != 1 || F("NextHit") != (center ? 0 : 1) || F("NextDelay") != (center ? 0 : 5)
                || F("ToHit") != 0 || F("CollideKill") != 0 || F("AlwaysExplode") != 0 || F("DamageRate") != 0
                || F("Town") != 0 || F("SrcTown") != 0)
            { throw new NotSupportedException("Unsupported Fissure missile row"); }
        }
    }
    internal static long Create(PlayerMobile owner, Point3D aim, int rank, DateTime at)
    {
        Routes();
        if (owner == null || Owner(owner.Serial.Value) != owner || owner.Map == null || owner.Map == Map.Internal
            || rank is < 1 or > 255 || at.Kind != DateTimeKind.Utc)
        { throw new NotSupportedException("Unsupported Fissure supplier"); }
        int x = checked(aim.X * 2), y = checked(aim.Y * 2);
        if (x == 0 || y == 0 || SkillGeometry.SourceDistance(x - owner.X * 2, y - owner.Y * 2) > 100 || !Terrain(owner.Map, x, y, aim.Z))
        { throw new NotSupportedException("Unsupported Fissure landing"); }
        long id = checked(_pool.NextId + 1);
        // Native epoch: first source body is on the next40ms update. Elapsed
        // source frame0 is distinct from the decrement performed in that body.
        var center = new FissureCenter(id, owner.Serial.Value, owner.Map.MapID, rank, x, y, aim.Z,
            0, SourceRandom.InitLow(SeedLow()), at.AddMilliseconds(40), at.AddMilliseconds(80 * 40));
        Validate(center);
        _pool = _pool with { NextId = id, Centers = _pool.Centers.Add(id, center) };
        return id;
    }
    private static bool Terrain(Map map, int x, int y, int z) => map != null && map != Map.Internal
        && x > 0 && y > 0 && x < map.Width * 2 && y < map.Height * 2
        && map.CanFit(Location(x, y, z), 1, false, false, true);
    internal static bool LandingBlocked(int map, int x, int y, int z, DateTime at)
    {
        if (map < 0 || map >= Map.Maps.Length || !Terrain(Map.Maps[map], x, y, z)) { return true; }
        // CheckMask at the candidate point queries the source Size3 square
        // footprint of existing cracks. No radial LOS or target occupancy test.
        foreach (var crack in _pool.Cracks.Values)
        {
            if (crack.Map == map && Math.Abs(crack.Z - z) < 16 && crack.UntilUtc > at
                && CombatHitboxes.SourceTouches(1, 3, x - crack.X, y - crack.Y)) { return true; }
        }
        return OtherMissileFootprint(map, x, y, z, at);
    }
    private static void AddCrack(PlayerMobile owner, FissureCenter center, int x, int y)
    {
        var values = SkillMechanics.Values(owner, CombatAbility.Fissure, center.Rank);
        var modifiers = DruidFirestorm.Modifiers(owner);
        var ends = DruidFirestorm.Endpoints(values, modifiers.FireMastery, AmazonSlowMissiles.Capture(owner, true));
        long id = checked(_pool.NextId + 1);
        var crack = new FissureCrack(id, center.Id, center.Owner, center.Map, center.Rank, x, y, center.Z,
            0, ends.Min, ends.Max, modifiers, SourceRandom.InitLow(SeedLow()), center.Owner,
            center.NextUtc.AddMilliseconds(40), center.NextUtc.AddMilliseconds(84 * 40));
        Validate(crack);
        _pool = _pool with { NextId = id, Cracks = _pool.Cracks.Add(id, crack) };
        Effects.SendLocationEffect(Location(x, y, center.Z), Map.Maps[center.Map], 0x398C, 3, 10, 0x489, 0); // Identified crack placeholder.
    }
    internal static bool Ready(long cast, uint target, DateTime at) => !_pool.Leases.TryGetValue((cast, target), out var lease) || lease.UntilUtc <= at;
    internal static Mobile Recipient(PlayerMobile owner, FissureCrack crack)
    {
        foreach (var target in owner.Map.GetMobilesInRange<Mobile>(Location(crack.X, crack.Y, crack.Z), 2))
        {
            int size = target is BaseCreature creature ? CreatureProfiles.For(creature).Traits.Size ?? 2 : 2;
            if (target.Serial.Value != crack.LastTarget && Math.Abs(target.Z - crack.Z) < 16
                && CombatHitboxes.SourceTouches(3, size, crack.X - target.X * 2, crack.Y - target.Y * 2)
                && Ready(crack.Cast, target.Serial.Value, crack.NextUtc) && ClassCombat.IsHostile(owner, target)
                && !SpellHelper.IsTown(target.Location, owner)) { return target; }
        }
        return null;
    }
    private static void Advance(FissureCenter center, bool? actions)
    {
        var owner = Owner(center.Owner);
        if (owner == null) { _pool = _pool with { Centers = _pool.Centers.Remove(center.Id) }; return; }
        if (Fighting(owner, center.Map, actions))
        {
            if (SpellHelper.IsTown(Location(center.X, center.Y, center.Z), owner))
            { _pool = _pool with { Centers = _pool.Centers.Remove(center.Id) }; return; }
            var values = SkillMechanics.Values(owner, CombatAbility.Fissure, center.Rank);
            int interval = (int)values.Eval("calc2"), range = (int)values.Eval("calc1");
            if (interval < 1 || range is < 1 or > 32767) { throw new NotSupportedException("Unmapped Fissure emitter bounds"); }
            if (center.Frame % interval == 0)
            {
                var sample = GroundMissileScatter.Sample(center.X, center.Frame, range);
                center = center with { Seed = sample.Seed };
                int x = checked(center.X + sample.X), y = checked(center.Y + sample.Y);
                if (!LandingBlocked(center.Map, x, y, center.Z, center.NextUtc)) { AddCrack(owner, center, x, y); }
            }
        }
        // Child emission precedes the generic center lifetime decrement.
        int frame = center.Frame + 1;
        _pool = _pool with { Centers = frame == 80 ? _pool.Centers.Remove(center.Id)
            : _pool.Centers.SetItem(center.Id, center with { Frame = frame, NextUtc = center.NextUtc.AddMilliseconds(40) }) };
    }
    private static void Burn(FissureCrack crack, bool? actions)
    {
        var owner = Owner(crack.Owner);
        if (owner == null || crack.NextUtc >= crack.UntilUtc)
        { _pool = _pool with { Cracks = _pool.Cracks.Remove(crack.Id) }; return; }
        var next = crack with { Frame = crack.Frame + 1, NextUtc = crack.NextUtc.AddMilliseconds(40) };
        _pool = _pool with { Cracks = _pool.Cracks.SetItem(crack.Id, next) };
        if (!Fighting(owner, crack.Map, actions)) { return; }
        var point = Location(crack.X, crack.Y, crack.Z);
        if (SpellHelper.IsTown(point, owner) || !Terrain(owner.Map, crack.X, crack.Y, crack.Z))
        { _pool = _pool with { Cracks = _pool.Cracks.Remove(crack.Id) }; return; }
        if (Recipient(owner, crack) is { } target)
        {
            // Reuse the existing cast/target lease representation. Its exact
            // modern Fissure grouping remains explicitly provisional; it is
            // separate from this crack's source LastCollide identity.
            var lease = new WeaponFlightLease(crack.Cast, target.Serial.Value, crack.NextUtc.AddMilliseconds(5 * 40));
            ulong seed = crack.Seed;
            int raw = WeaponMissileDamage.RollValue(crack.Min256, crack.Max256, 0, bound => (int)SourceRandom.Limited(ref seed, bound));
            next = next with { LastTarget = target.Serial.Value, Seed = seed };
            _pool = _pool with { Cracks = _pool.Cracks.SetItem(crack.Id, next), Leases = _pool.Leases.SetItem((crack.Cast, target.Serial.Value), lease) };
            target.FixedEffect(0x3709, 3, 4, 0x489, 0); // Identified fire_hit placeholder, before defense/health.
            if (!ClassAvoidance.Missile(target, "erruption crack 1"))
            {
                owner.DoHarmful(target, true);
                var parts = SkillCombat.MitigatePrepared(owner, target, DruidFirestorm.Packet(crack.Modifiers, raw));
                int loss = SkillHealth.Damage(owner, target, NamedElements.To256(parts.Total));
                if (!BarbarianLeap.Uninterruptible(target) && target is BaseCreature survivor)
                { CreatureHitRecovery.Apply(survivor, owner, loss); }
            }
        }
        if (next.Frame % 5 == 0) { Effects.SendLocationEffect(point, owner.Map, 0x398C, 3, 10, 0x489, 0); } // Identified active-crack placeholder.
    }
    internal static void Tick(DateTime at, bool? actions = null)
    {
        if (at.Kind != DateTimeKind.Utc) { throw new ArgumentException("Fissure clock must be UTC"); }
        while (true)
        {
            var pool = _pool;
            DateTime next = pool.Centers.Values.Select(x => x.NextUtc).Concat(pool.Cracks.Values.Select(x => x.NextUtc)).DefaultIfEmpty(DateTime.MaxValue).Min();
            if (next > at) { PruneLeases(at); return; }
            // Native projection of newest-first unit lists across both actors.
            var due = pool.Centers.Values.Where(x => x.NextUtc == next).Select(x => (x.Id, Center: true))
                .Concat(pool.Cracks.Values.Where(x => x.NextUtc == next).Select(x => (x.Id, Center: false))).OrderByDescending(x => x.Id).ToArray();
            foreach (var item in due)
            {
                if (item.Center && _pool.Centers.TryGetValue(item.Id, out var center)) { Advance(center, actions); }
                else if (!item.Center && _pool.Cracks.TryGetValue(item.Id, out var crack)) { Burn(crack, actions); }
            }
        }
    }
    private static void PruneLeases(DateTime at)
    {
        var pool = _pool;
        var casts = pool.Centers.Keys.Concat(pool.Cracks.Values.Select(x => x.Cast)).ToHashSet();
        _pool = pool with { Leases = pool.Leases.RemoveRange(pool.Leases.Where(x => x.Value.UntilUtc <= at || !casts.Contains(x.Value.Cast)).Select(x => x.Key)) };
    }
    private static void Common(long id, uint owner, int map, int rank, int x, int y, int z, DateTime next, DateTime until)
    {
        if (id <= 0 || !((Serial)owner).IsMobile || rank is < 1 or > 255 || map < 0 || map >= Map.Maps.Length || Map.Maps[map] == null || Map.Maps[map] == Map.Internal
            || x <= 0 || y <= 0 || x >= Map.Maps[map].Width * 2 || y >= Map.Maps[map].Height * 2 || z is < -128 or > 127
            || next.Kind != DateTimeKind.Utc || until.Kind != DateTimeKind.Utc || next > until)
        { throw new InvalidDataException("Invalid Fissure identity/clock"); }
    }
    private static void Validate(FissureCenter center)
    {
        if (center == null) { throw new InvalidDataException("Missing Fissure center"); }
        Common(center.Id, center.Owner, center.Map, center.Rank, center.X, center.Y, center.Z, center.NextUtc, center.UntilUtc);
        if (center.Frame is < 0 or >= 80 || center.UntilUtc - center.NextUtc != TimeSpan.FromMilliseconds((79 - center.Frame) * 40))
        { throw new InvalidDataException("Invalid Fissure center lifetime"); }
    }
    private static void Validate(FissureCrack crack)
    {
        if (crack == null) { throw new InvalidDataException("Missing Fissure crack"); }
        Common(crack.Id, crack.Owner, crack.Map, crack.Rank, crack.X, crack.Y, crack.Z, crack.NextUtc, crack.UntilUtc);
        if (crack.Cast <= 0 || crack.Cast >= crack.Id || crack.Frame is < 0 or >= 84 || crack.Min256 < 0 || crack.Max256 < crack.Min256
            || crack.Modifiers == null || crack.Modifiers.Potion < 0 || crack.LastTarget != 0 && !((Serial)crack.LastTarget).IsMobile
            || crack.UntilUtc - crack.NextUtc != TimeSpan.FromMilliseconds((83 - crack.Frame) * 40))
        { throw new InvalidDataException("Invalid Fissure crack damage/lifetime"); }
    }
    internal static void Restore(FissureSave saved)
    {
        if (saved?.Centers == null || saved.Cracks == null || saved.Leases == null || saved.NextId < 0 || saved.DelayScope != "cast-target-provisional-2.7")
        { throw new InvalidDataException("Missing/unsupported Fissure state"); }
        var next = new Pool(ImmutableDictionary<long, FissureCenter>.Empty, ImmutableDictionary<long, FissureCrack>.Empty,
            ImmutableDictionary<(long, uint), WeaponFlightLease>.Empty, saved.NextId);
        foreach (var center in saved.Centers)
        {
            Validate(center);
            if (center.Id > saved.NextId || next.Centers.ContainsKey(center.Id)) { throw new InvalidDataException("Duplicate Fissure center"); }
            next = next with { Centers = next.Centers.Add(center.Id, center) };
        }
        foreach (var crack in saved.Cracks)
        {
            Validate(crack);
            if (crack.Id > saved.NextId || next.Centers.ContainsKey(crack.Id) || next.Cracks.ContainsKey(crack.Id)
                || next.Centers.TryGetValue(crack.Cast, out var parent) && (parent.Owner != crack.Owner || parent.Map != crack.Map || parent.Rank != crack.Rank))
            { throw new InvalidDataException("Duplicate/mismatched Fissure crack"); }
            next = next with { Cracks = next.Cracks.Add(crack.Id, crack) };
        }
        var casts = next.Centers.Keys.Concat(next.Cracks.Values.Select(x => x.Cast)).ToHashSet();
        foreach (var lease in saved.Leases)
        {
            if (lease == null || !casts.Contains(lease.Cast) || !((Serial)lease.Target).IsMobile || lease.UntilUtc.Kind != DateTimeKind.Utc
                || lease.RetainWithoutFlight || next.Leases.ContainsKey((lease.Cast, lease.Target)))
            { throw new InvalidDataException("Invalid Fissure hit-delay lease"); }
            next = next with { Leases = next.Leases.Add((lease.Cast, lease.Target), lease) };
        }
        _pool = next;
    }
}
