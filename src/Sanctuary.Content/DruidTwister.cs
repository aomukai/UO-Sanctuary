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

internal sealed record TwisterMissile(long Id, long Cast, uint Owner, int Map, int Rank, int Z,
    int OriginX, int OriginY, double X, double Y, Point2D[] Path, int Next, int Frame, int Velocity256,
    int Min256, int Max256, FirestormModifiers Modifiers, ulong Seed, DateTime NextUtc, DateTime UntilUtc)
{
    public string MotionAdapter { get; init; } = "native-shared-q12-charged-v1";
    public SourceStepTrace Steps { get; init; }
}
internal sealed record TwisterSave(TwisterMissile[] Missiles, WeaponFlightLease[] Leases, long NextId)
{
    public string DelayScope { get; init; } = "cast-target-provisional-2.7";
}
public sealed class TwisterProfiles : GenericPersistence
{
    public TwisterProfiles() : base("SanctuaryTwister", 168) { }
    public override void Serialize(IGenericWriter writer)
    { writer.Write(0); writer.Write(JsonSerializer.Serialize(DruidTwister.Snapshot())); }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown Twister version"); }
        DruidTwister.Restore(JsonSerializer.Deserialize<TwisterSave>(reader.ReadString()));
    }
}

// Source240/477 charged missiles. Source-backed launch, physical endpoints and
// common reaction/stun consumers are separate from the explicitly provisional
// modern cast/target NHD key and native motion/terrain/room-order adapters.
public static class DruidTwister
{
    private sealed record Pool(ImmutableDictionary<long, TwisterMissile> Missiles,
        ImmutableDictionary<(long, uint), WeaponFlightLease> Leases, long NextId);
    private static Pool _pool = new(ImmutableDictionary<long, TwisterMissile>.Empty,
        ImmutableDictionary<(long, uint), WeaponFlightLease>.Empty, 0);
    internal static Func<Mobile, bool> Uninterruptible = BarbarianLeap.Uninterruptible;
    public static TwisterProfiles Profiles { get; private set; }
    public static void Configure() => Profiles = new();
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40), () => Tick(TimedEffects.Now));
    private static TwisterMissile Copy(TwisterMissile missile) => missile with { Path = missile.Path.ToArray(), Steps = new(missile.Steps.Cells.ToArray()) };
    internal static TwisterSave Snapshot()
    {
        var pool = _pool;
        return new(pool.Missiles.Values.OrderBy(x => x.Id).Select(Copy).ToArray(),
            pool.Leases.Values.OrderBy(x => x.Cast).ThenBy(x => x.Target).ToArray(), pool.NextId);
    }
    internal static void Cancel(Mobile owner)
    {
        if (owner == null) { return; }
        _pool = _pool with { Missiles = _pool.Missiles.RemoveRange(_pool.Missiles.Where(x => x.Value.Owner == owner.Serial.Value).Select(x => x.Key)) };
        Prune(TimedEffects.Now);
    }
    private static PlayerMobile Owner(uint serial) => World.FindMobile((Serial)serial) is PlayerMobile { Alive: true, Deleted: false } p
        && ClassCreation.Profiles.TryGet(p.Serial, out var profile) && profile.Class == DiabloClass.Druid ? p : null;
    private static bool Fighting(PlayerMobile owner, int map, bool? actions) => owner.Map != null && owner.Map != Map.Internal
        && owner.Map.MapID == map && (actions ?? owner.NetState is { Running: true });
    private static void Routes()
    {
        var skill = SkillMechanics.For(CombatAbility.Twister);
        int F(string name) => NecromancerSummons.Source("twister", name);
        if (skill.Number("srvdofunc") != 118 || skill.Number("Param1") != 3 || skill.Number("Param2") != 10
            || skill.Number("Param7") != 2 || skill.Number("SrcDam") != 0 || skill.Text("anim") != "SC"
            || F("pSrvDoFunc") != 1 || F("pSrvHitFunc") != 0 || F("pSrvDmgFunc") != 9
            || F("Vel") != 10 || F("VelLev") != 0 || F("MaxVel") != 10 || F("Range") != 50
            || F("CollideType") != 3 || F("Size") != 2 || F("Collision") != 0 || F("LastCollide") != 0
            || F("NextHit") != 1 || F("NextDelay") != 25 || F("ToHit") != 0 || F("CollideKill") != 0
            || F("KnockBack") != 1 || F("GetHit") != 1 || F("ReturnFire") != 1 || F("CanSlow") != 1
            || F("Town") != 0 || F("SrcTown") != 0 || F("SrcDamage") != 0 || F("SrcMissDmg") != 0)
        { throw new NotSupportedException("Unsupported Twister route"); }
    }
    public static bool Begin(PlayerMobile owner, Serial selected, Point3D point, long now)
    {
        if (!DruidGroundCasting.Aim(owner, selected, ref point, false)) { return false; }
        return SkillCombat.Begin(owner, CombatAbility.Twister, selected, point, now, (p, _, _, aim) => DruidGroundCasting.Ground(p, aim));
    }
    public static bool Release(PlayerMobile owner)
    {
        var session = ClassCombat.For(owner); var point = session.PendingPoint;
        if (!DruidCombat.Available(owner, CombatAbility.Twister)
            || !DruidGroundCasting.Aim(owner, session.PendingTarget, ref point, true)) { return false; }
        var values = SkillMechanics.Values(owner, CombatAbility.Twister); TwisterMissile[] prepared;
        try { prepared = Prepare(owner, point, values, TimedEffects.Now); }
        catch (NotSupportedException) { owner.SendMessage(0x22, "Twister cannot be used in this context yet."); return false; }
        if (prepared.Length != 3 || !NecromancerEffects.Pay(owner, values.Mana256)) { return false; }
        Commit(prepared); owner.PlaySound(0x655); // Identified wind-cast audio placeholder.
        return true;
    }
    internal static long[] Create(PlayerMobile owner, Point3D aim, SkillValues values, DateTime at) => Commit(Prepare(owner, aim, values, at));
    private static long[] Commit(TwisterMissile[] prepared)
    {
        if (prepared.Length == 0) { return []; }
        var next = _pool.Missiles;
        foreach (var missile in prepared) { next = next.Add(missile.Id, missile); }
        _pool = _pool with { Missiles = next, NextId = prepared[^1].Id };
        return prepared.Select(x => x.Id).ToArray();
    }
    private static TwisterMissile[] Prepare(PlayerMobile owner, Point3D aim, SkillValues values, DateTime at)
    {
        Routes();
        if (owner == null || Owner(owner.Serial.Value) != owner || owner.Map == null || owner.Map == Map.Internal
            || at.Kind != DateTimeKind.Utc || values.Ability != CombatAbility.Twister || values.Rank is < 1 or > 255 || values.Eval("calc1") != 3)
        { throw new NotSupportedException("Unsupported Twister supplier"); }
        int ax = aim.X * 2, ay = aim.Y * 2, dx = ax - owner.X * 2, dy = ay - owner.Y * 2;
        if (ax <= 0 || ay <= 0 || ax > 65535 || ay > 65535 || Math.Abs(dx) >= 100 || Math.Abs(dy) >= 100) { return []; }
        if (dx == 0 && dy == 0) { ax++; ay++; dx = dy = 1; }
        var slow = AmazonSlowMissiles.Capture(owner, true);
        int velocity = SlowMissilePayload.VelocityFp8(10, 0, values.Rank, 33, 0, true, slow.VelocityPercent);
        if (velocity == 0) { throw new NotSupportedException("Unmapped zero-velocity Twister path"); }
        var plans = DruidEmitterPatterns.Launch(3, false, new(owner.X * 2, owner.Y * 2, ax, ay, true), 477, values.Rank);
        var result = new TwisterMissile[3]; var mods = DruidFirestorm.Modifiers(owner); long cast = checked(_pool.NextId + 1);
        for (int i = 0; i < plans.Length; i++)
        {
            var init = DruidEmitterPatterns.InitializeCharged(50, ax, i);
            var path = ChargedBolts.Path(init.Seed, ChargedBolts.Heading(dx, dy), init.Distance);
            ulong seed = SourceRandom.InitLow(unchecked((int)init.Seed));
            // Existing sentry/martial callers retain the same path-building
            // RNG draws before damage starts. Do not restart this seed at impact.
            for (int point = 1; point < path.Length; point++) { SourceRandom.Roll(ref seed); }
            var missile = new TwisterMissile(cast + i, cast, owner.Serial.Value, owner.Map.MapID, values.Rank, owner.Z,
                owner.X, owner.Y, owner.X, owner.Y, path, 1, 0, velocity,
                values.PhysicalDamage256(false), values.PhysicalDamage256(true), mods, seed,
                at.AddMilliseconds(40), at.AddMilliseconds(50 * 40)) { Steps = new([new(owner.X * 2, owner.Y * 2)]) };
            Validate(missile); result[i] = missile;
        }
        return result;
    }
    internal static BoulderKnockbackPlan ReactionFlags(bool uninterruptible, ulong seed)
    {
        int flags = 1;
        if (!uninterruptible) { flags |= 4; if (SourceRandom.Roll(ref seed) % 100 < 1) { flags |= 8; } }
        return new(flags, seed);
    }
    internal static bool Ready(long cast, uint target, DateTime at) => !_pool.Leases.TryGetValue((cast, target), out var lease) || lease.UntilUtc <= at;
    internal static BaseCreature Recipient(PlayerMobile owner, TwisterMissile missile, SourceStepTrace steps)
    {
        foreach (var cell in steps.Cells)
        {
            foreach (var target in owner.Map.GetMobilesInRange<BaseCreature>(new Point3D((cell.X + 1) / 2, (cell.Y + 1) / 2, missile.Z), 2))
            {
                int size = CreatureProfiles.For(target).Traits.Size ?? 2;
                if (Math.Abs(target.Z - missile.Z) < 16 && CombatHitboxes.SourceTouches(2, size, cell.X - target.X * 2, cell.Y - target.Y * 2)
                    && Ready(missile.Cast, target.Serial.Value, missile.NextUtc) && ClassCombat.IsHostile(owner, target)
                    && !SpellHelper.IsTown(target.Location, owner)) { return target; }
            }
        }
        return null;
    }
    private static TwisterMissile Impact(PlayerMobile owner, BaseCreature target, TwisterMissile missile)
    {
        if (!SourceBlocking.TryRate(target, out _, out _) || !CombatStuns.Rules(target, out _, out _)) { return missile; }
        // Lease precedes events/defense, as in the generic router. It is a
        // declared modern grouping adapter, not the old global JUSTHIT state.
        var lease = new WeaponFlightLease(missile.Cast, target.Serial.Value, missile.NextUtc.AddMilliseconds(25 * 40));
        _pool = _pool with { Leases = _pool.Leases.SetItem((lease.Cast, lease.Target), lease) };
        ulong seed = missile.Seed;
        int raw = WeaponMissileDamage.RollValue(missile.Min256, missile.Max256, 0, n => (int)SourceRandom.Limited(ref seed, n));
        bool guard = Uninterruptible(target); var flags = ReactionFlags(guard, seed); seed = flags.Seed;
        if (!SourceBlocking.TryBlock(target, raw != 0, out bool blocked)) { return missile with { Seed = seed }; }
        var plan = MoltenBoulderPackets.ResolveImpact(BoulderImpactKind.Rolling, flags, blocked ? 1 : 0,
            SourceKnockbacks.Rule(target)?.Knockback == true, guard);
        owner.DoHarmful(target, true); int loss = 0;
        if (plan.Damage)
        {
            var damage = DruidMoltenBoulder.Potion(missile.Modifiers, new(raw / 256.0, 0, 0, 0, 0));
            var parts = SkillCombat.MitigatePrepared(owner, target, damage);
            // The released2.4 change and current calc2 supersede the legacy
            // damage9 raw Param2 fallback. Read live hard Arctic Blast points
            // at retained missile rank, separately from captured physical damage.
            CombatStuns.Apply(target, checked((int)SkillMechanics.Values(owner, CombatAbility.Twister, missile.Rank).Eval("calc2")), missile.NextUtc);
            loss = SkillHealth.Damage(owner, target, NamedElements.To256(parts.Total));
        }
        if (!guard && target is { Alive: true, Deleted: false })
        {
            if (!(plan.Push && SourceKnockbacks.Begin(owner, target)) && plan.Damage) { CreatureHitRecovery.Apply(target, owner, loss); }
        }
        target.FixedEffect(0x37CC, 3, 4, 0x481, 0); // Identified wind-contact placeholder.
        return missile with { Seed = seed };
    }
    private static void Advance(TwisterMissile missile, bool? actions)
    {
        var owner = Owner(missile.Owner);
        if (owner == null || missile.NextUtc >= missile.UntilUtc) { _pool = _pool with { Missiles = _pool.Missiles.Remove(missile.Id) }; return; }
        var next = missile with { Frame = missile.Frame + 1, NextUtc = missile.NextUtc.AddMilliseconds(40) };
        if (!Fighting(owner, missile.Map, actions)) { _pool = _pool with { Missiles = _pool.Missiles.SetItem(missile.Id, next) }; return; }
        if (SpellHelper.IsTown(new Point3D((int)Math.Round(missile.X), (int)Math.Round(missile.Y), missile.Z), owner))
        { _pool = _pool with { Missiles = _pool.Missiles.Remove(missile.Id) }; return; }
        var moved = ChargedBolts.Advance(missile.Path, missile.OriginX, missile.OriginY, missile.X, missile.Y, missile.Next,
            SkillGeometry.Tiles(missile.Velocity256 / 4096.0));
        var steps = SourceMissileSteps.Advance(missile.X, missile.Y, moved.X - missile.X, moved.Y - missile.Y, missile.Steps);
        if (moved.Next >= missile.Path.Length || moved.X <= 0 || moved.Y <= 0 || moved.X >= owner.Map.Width || moved.Y >= owner.Map.Height)
        { _pool = _pool with { Missiles = _pool.Missiles.Remove(missile.Id) }; return; }
        foreach (var cell in steps.Cells)
        {
            if (Recipient(owner, missile, new([cell])) is { } target)
            { next = Impact(owner, target, next with { NextUtc = missile.NextUtc }); break; }
            if (DruidFirestorm.CellBlocked(owner.Map, missile.Z, missile.X, missile.Y, cell.X, cell.Y))
            { _pool = _pool with { Missiles = _pool.Missiles.Remove(missile.Id) }; return; }
        }
        next = next with { X = moved.X, Y = moved.Y, Next = moved.Next, Steps = steps, NextUtc = missile.NextUtc.AddMilliseconds(40) };
        _pool = _pool with { Missiles = _pool.Missiles.SetItem(missile.Id, next) };
        if (next.Frame % 3 == 0) { Effects.SendLocationEffect(new((int)Math.Round(next.X), (int)Math.Round(next.Y), missile.Z), owner.Map, 0x37CC, 3, 8, 0x481, 0); } // Identified flight placeholder.
    }
    internal static void Tick(DateTime at, bool? actions = null)
    {
        if (at.Kind != DateTimeKind.Utc) { throw new ArgumentException("Twister clock must be UTC"); }
        while (true)
        {
            var due = _pool.Missiles.Values.Select(x => x.NextUtc).DefaultIfEmpty(DateTime.MaxValue).Min();
            if (due > at) { Prune(at); return; }
            foreach (var missile in _pool.Missiles.Values.Where(x => x.NextUtc == due).OrderByDescending(x => x.Id).ToArray())
            { if (_pool.Missiles.ContainsKey(missile.Id)) { Advance(missile, actions); } }
        }
    }
    private static void Prune(DateTime at)
    {
        var casts = _pool.Missiles.Values.Select(x => x.Cast).ToHashSet();
        _pool = _pool with { Leases = _pool.Leases.RemoveRange(_pool.Leases.Where(x => x.Value.UntilUtc <= at || !casts.Contains(x.Key.Item1)).Select(x => x.Key)) };
    }
    private static void Validate(TwisterMissile missile)
    {
        if (missile == null || missile.Id <= 0 || missile.Cast <= 0 || missile.Cast > missile.Id || !((Serial)missile.Owner).IsMobile
            || missile.Rank is < 1 or > 255 || missile.Map < 0 || missile.Map >= Map.Maps.Length || Map.Maps[missile.Map] == null || Map.Maps[missile.Map] == Map.Internal
            || missile.Z is < -128 or > 127 || missile.OriginX <= 0 || missile.OriginY <= 0 || missile.OriginX >= Map.Maps[missile.Map].Width || missile.OriginY >= Map.Maps[missile.Map].Height
            || !double.IsFinite(missile.X + missile.Y) || missile.X <= 0 || missile.Y <= 0 || missile.X >= Map.Maps[missile.Map].Width || missile.Y >= Map.Maps[missile.Map].Height
            || missile.Path is not { Length: 26 } || missile.Path[0] != new Point2D(0, 0) || missile.Next < 1 || missile.Next >= missile.Path.Length
            || missile.Path.Zip(missile.Path.Skip(1)).Any(x => Math.Abs(x.First.X - x.Second.X) > 1 || Math.Abs(x.First.Y - x.Second.Y) > 1 || x.First == x.Second)
            || missile.Frame is < 0 or >= 50 || missile.Velocity256 is < 1 or > 1920 || missile.Min256 < 0 || missile.Max256 < missile.Min256
            || missile.Modifiers == null || missile.Modifiers.Potion < 0 || missile.NextUtc.Kind != DateTimeKind.Utc || missile.UntilUtc.Kind != DateTimeKind.Utc
            || missile.UntilUtc - missile.NextUtc != TimeSpan.FromMilliseconds((49 - missile.Frame) * 40)
            || missile.MotionAdapter != "native-shared-q12-charged-v1" || missile.Steps?.Cells is not { Length: >= 1 and <= 10 }
            || missile.Steps.Cells.Any(c => c.X <= 0 || c.Y <= 0 || c.X >= Map.Maps[missile.Map].Width * 2 || c.Y >= Map.Maps[missile.Map].Height * 2))
        { throw new InvalidDataException("Invalid Twister missile"); }
    }
    internal static void Restore(TwisterSave saved)
    {
        if (saved?.Missiles == null || saved.Leases == null || saved.NextId < 0 || saved.DelayScope != "cast-target-provisional-2.7")
        { throw new InvalidDataException("Missing/unsupported Twister state"); }
        var next = new Pool(ImmutableDictionary<long, TwisterMissile>.Empty, ImmutableDictionary<(long, uint), WeaponFlightLease>.Empty, saved.NextId);
        foreach (var missile in saved.Missiles)
        {
            Validate(missile);
            if (missile.Id > saved.NextId || next.Missiles.ContainsKey(missile.Id)) { throw new InvalidDataException("Duplicate Twister identity"); }
            next = next with { Missiles = next.Missiles.Add(missile.Id, Copy(missile)) };
        }
        foreach (var group in next.Missiles.Values.GroupBy(x => x.Cast))
        {
            var first = group.First();
            if (group.Any(x => x.Owner != first.Owner || x.Map != first.Map || x.Rank != first.Rank || x.UntilUtc != first.UntilUtc))
            { throw new InvalidDataException("Mismatched Twister cast"); }
        }
        var casts = next.Missiles.Values.Select(x => x.Cast).ToHashSet();
        foreach (var lease in saved.Leases)
        {
            if (lease == null || !casts.Contains(lease.Cast) || !((Serial)lease.Target).IsMobile || lease.UntilUtc.Kind != DateTimeKind.Utc
                || lease.RetainWithoutFlight || next.Leases.ContainsKey((lease.Cast, lease.Target))) { throw new InvalidDataException("Invalid Twister hit-delay lease"); }
            next = next with { Leases = next.Leases.Add((lease.Cast, lease.Target), lease) };
        }
        _pool = next;
    }
}
