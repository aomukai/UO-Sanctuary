// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record TaintedFireBallFlight(long Id, uint Actor, uint Owner, int Map, int Z, int Rank,
    double X, double Y, double Vx, double Vy, int VelocityFp8, int Frame, TaintedFireBallPacket Packet,
    SlowMissilePayload Slow, ulong Seed, SourceStepTrace Steps, DateTime NextUtc, string Fault = null);
internal sealed record TaintedFireBallSave(TaintedFireBallFlight[] Flights, long NextId);
public sealed class TaintedFireBallProfiles : GenericPersistence
{
    private TaintedFireBallSave _snapshot;
    public TaintedFireBallProfiles() : base("SanctuaryTaintedFireBalls", 229) { }
    public override void Serialize() { _snapshot = TaintedFireBallMissiles.Snapshot(); base.Serialize(); }
    public override void Serialize(IGenericWriter writer) { writer.Write(0); writer.Write(JsonSerializer.Serialize(_snapshot)); }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown Tainted Fire Ball version"); }
        TaintedFireBallMissiles.Restore(JsonSerializer.Deserialize<TaintedFireBallSave>(reader.ReadString()));
    }
}

// Explicit NPC422 release, not a learned summon cast or GenericPet AI decision.
// Source room/terrain/enumeration and initial projectile seed are host adapters.
internal static class TaintedFireBallMissiles
{
    private const int Frames = 50;
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(TaintedFireBallMissiles));
    private static readonly Dictionary<long, TaintedFireBallFlight> Flights = new();
    private static long _nextId;
    internal static Func<int> SeedLow = () => unchecked((int)Random.Shared.NextInt64(1L << 32));
    private static readonly Lazy<bool> Route = new(() =>
    {
        var rows = BalanceCatalog.Read<Dictionary<string, int>>("tainted-fireball-flight");
        var expected = new Dictionary<string, int>
        {
            ["pSrvDoFunc"] = 1, ["pSrvHitFunc"] = 1, ["pSrvDmgFunc"] = 0,
            ["Vel"] = 20, ["VelLev"] = 0, ["MaxVel"] = 20, ["Accel"] = 0,
            ["Range"] = 50, ["InitSteps"] = 1, ["Activate"] = 0, ["Size"] = 1,
            ["CollideType"] = 3, ["CollideKill"] = 1, ["LastCollide"] = 1,
            ["CanSlow"] = 1, ["Collision"] = 0, ["ToHit"] = 0, ["SrcDamage"] = 0,
            ["Pierce"] = 0, ["NextHit"] = 0, ["ReturnFire"] = 1, ["GetHit"] = 1,
            ["ResultFlags"] = 5, ["HitFlags"] = 2
        };
        if (rows.Length != 1 || rows[0].Count != expected.Count || expected.Any(p => !rows[0].TryGetValue(p.Key, out int value) || value != p.Value))
        { throw new NotSupportedException("Unsupported Tainted Fire Ball flight route"); }
        return true;
    });
    public static void Configure() => _ = new TaintedFireBallProfiles();
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40), () => Tick(TimedEffects.Now));

    private static bool Attached(WarlockTaintedBody actor, PlayerMobile owner, int map) =>
        actor is { Alive: true, Deleted: false } && actor.Prepared?.Owner == owner?.Serial.Value
        && actor.ControlMaster == owner && owner is { Alive: true, Deleted: false }
        && actor.Map != null && actor.Map != Map.Internal && actor.Map.MapID == map && owner.Map == actor.Map
        && PersistentMinions.Active(owner, actor) && actor.Permitted(owner) && !SpellHelper.IsTown(owner.Location, owner);

    internal static long LaunchPrepared(WarlockTaintedBody actor, Point3D aim, int rank, TaintedFireBallPacket captured, DateTime at)
    {
        _ = Route.Value;
        var owner = actor?.ControlMaster as PlayerMobile;
        if (!Attached(actor, owner, actor?.Map?.MapID ?? -1) || rank is < 1 or > 255 || at.Kind != DateTimeKind.Utc
            || at > DateTime.MaxValue.AddMilliseconds(-Frames * 40) || aim.X <= 0 || aim.Y <= 0
            || aim.X >= actor.Map.Width || aim.Y >= actor.Map.Height)
        { throw new NotSupportedException("Tainted Fire Ball needs its original active NPC, explicit rank, aim and UTC release clock"); }
        var slow = AmazonSlowMissiles.Capture(actor, true);
        ArgumentNullException.ThrowIfNull(captured);
        var packet = captured with { Minimum256 = slow.Damage256(captured.Minimum256), Maximum256 = slow.Damage256(captured.Maximum256) };
        int velocity = SlowMissilePayload.VelocityFp8(20, 0, rank, 0x21, 0, true, slow.VelocityPercent);
        int dx = aim.X - actor.X, dy = aim.Y - actor.Y;
        if (dx == 0 && dy == 0) { dx = dy = 1; }
        var v = MissileVectors.VelocityFp8(dx, dy, velocity);
        long id = checked(_nextId + 1);
        var state = new TaintedFireBallFlight(id, actor.Serial.Value, owner.Serial.Value, actor.Map.MapID, actor.Z, rank,
            actor.X, actor.Y, v.X, v.Y, velocity, 0, packet, slow, SourceRandom.InitLow(SeedLow()),
            new([new(actor.X * 2, actor.Y * 2)]), at.AddMilliseconds(40));
        Validate(state);
        if (NpcEquipmentRandom.State(actor) == null) { NpcEquipmentRandom.Set(actor, SourceRandom.InitLow(NpcEquipmentRandom.SeedLow())); }
        Flights.Add(id, state); _nextId = id;
        return id;
    }

    internal static void Tick(DateTime at, bool? allowActions = null, long? onlyId = null)
    {
        if (at.Kind != DateTimeKind.Utc) { throw new ArgumentException("Tainted projectile clock must be UTC"); }
        while (true)
        {
            var state = Flights.Values.Where(s => s.NextUtc <= at && (!onlyId.HasValue || s.Id == onlyId.Value))
                .OrderBy(s => s.NextUtc).ThenByDescending(s => s.Id).FirstOrDefault();
            if (state == null) { return; }
            try { Advance(state, allowActions); }
            catch (Exception e)
            {
                // A partially delivered explosion is never retried. Preserve
                // its explicit fault while the original lifetime runs down.
                if (state.Frame + 1 >= Frames) { Flights.Remove(state.Id); }
                else { Flights[state.Id] = state with { Frame = state.Frame + 1, NextUtc = state.NextUtc.AddMilliseconds(40), Fault = e.Message }; }
                Log.Error(e, "Tainted Fire Ball suspended: {Id}", state.Id);
            }
        }
    }

    private static bool Candidate(WarlockTaintedBody actor, PlayerMobile owner, Mobile target, int z) =>
        target is { Alive: true, Deleted: false, Blessed: false } && target != actor && target.Map == actor.Map
        && Math.Abs(target.Z - z) < 16 && ClassCombat.IsHostile(owner, target) && !ClassCombat.Friendly(actor, target)
        && actor.CanBeHarmful(target, false) && !SpellHelper.IsTown(target.Location, owner);

    internal static void RequireOrdinaryRecipient(PlayerMobile owner, Mobile candidate)
    {
        // These NPCs have no mapped source passive Avoid/Evade/Weapon Block.
        // Shield block is intentionally absent: hit01 calls defense with1/0.
        // Class pets/hirelings/PvP need their own source-stat defense adapter.
        if (candidate is not BaseCreature target || target is SkeletonGuardian || target.Controlled || target.Summoned
            || target.ControlMaster != null || target.SummonMaster != null
            || CreatureProfiles.Definition(target).Reference is "shadowwarrior" or "shadowmaster" or "valkyrie")
        { throw new NotSupportedException("Tainted copied-area recipient needs its source passive-avoidance adapter"); }
        PreparedMonsterNativeDamage.RequireIncoming(owner, target);
        var metadata = NativeMonsterReturn.Metadata(target);
        if (!metadata.Killable || metadata.Base == 78 || MonsterUninterruptibles.Active(target) || MonsterUninterruptibles.Delayed(target))
        { throw new NotSupportedException("Tainted copied-area recipient needs its source return/mode adapter"); }
    }

    private static void Explode(WarlockTaintedBody actor, PlayerMobile owner, TaintedFireBallFlight state)
    {
        int x = (int)Math.Floor(state.X * 2 + .5), y = (int)Math.Floor(state.Y * 2 + .5);
        var center = new Point3D((x + 1) / 2, (y + 1) / 2, state.Z);
        var recipients = new List<BaseCreature>();
        foreach (var target in actor.Map.GetMobilesInRange<Mobile>(center, 3))
        {
            if (!Candidate(actor, owner, target, state.Z) || !DruidTornado.InPulse(x, y, target.X * 2, target.Y * 2, state.Packet.Radius)) { continue; }
            RequireOrdinaryRecipient(owner, target);
            if (SourceDamageAreas.Recipient(owner, (BaseCreature)target, state.Map, x, y, state.Z, state.Packet.Radius)) { recipients.Add((BaseCreature)target); }
        }
        // Roll the original missile once; each callback gets a fresh copy.
        // Preserve the native enumeration adapter's order, not serial sorting.
        ulong stream = state.Seed;
        int raw = WeaponMissileDamage.RollValue(state.Packet.Minimum256, state.Packet.Maximum256, 0, n => (int)SourceRandom.Limited(ref stream, n));
        int chance = NecromancerSummons.Source("warbighead", "Crit");
        foreach (var target in recipients)
        {
            if (!Candidate(actor, owner, target, state.Z)) { continue; }
            bool critical = NpcEquipmentCritical.Intrinsic(new(0, 0, 0, chance), () => NpcEquipmentRandom.Raw(actor));
            TaintedFireBallHit.DeliverQualified(actor, target, state.Packet, raw, critical, state.NextUtc.AddMilliseconds(-40));
        }
        Effects.SendLocationEffect(center, actor.Map, 0x36BD, 10, 10, 0, 0); // Identified interim fire explosion.
    }

    private static void Advance(TaintedFireBallFlight state, bool? allowActions)
    {
        var actor = World.FindMobile((Serial)state.Actor) as WarlockTaintedBody;
        var owner = World.FindMobile((Serial)state.Owner) as PlayerMobile;
        var next = state with { Frame = state.Frame + 1, NextUtc = state.NextUtc.AddMilliseconds(40) };
        // Explicit host policy pending current source-removal acceptance: no
        // offline/off-facet/removed-source damage or replay; expiry still runs.
        if (state.Fault != null || !Attached(actor, owner, state.Map) || !(allowActions ?? owner.NetState != null))
        { if (next.Frame >= Frames) { Flights.Remove(state.Id); } else { Flights[state.Id] = next; } return; }
        var map = actor.Map; bool removed = false;
        int Move()
        {
            next = next with { X = state.X + state.Vx, Y = state.Y + state.Vy, Steps = SourceMissileSteps.Advance(state.X, state.Y, state.Vx, state.Vy, state.Steps) };
            return next.X < 0 || next.Y < 0 || next.X >= map.Width || next.Y >= map.Height ? 2 : 1;
        }
        bool Wall(SourceMissileCell cell) => !map.LineOfSight(new Point3D((int)Math.Round(state.X), (int)Math.Round(state.Y), state.Z + 12),
            new((cell.X + 1) / 2, (cell.Y + 1) / 2, state.Z + 12));
        Mobile Find(SourceMissileCell cell)
        {
            foreach (var target in map.GetMobilesInRange<Mobile>(new Point3D((cell.X + 1) / 2, (cell.Y + 1) / 2, state.Z), 3))
            {
                if (!Candidate(actor, owner, target, state.Z)) { continue; }
                int size = target is BaseCreature creature ? CreatureProfiles.For(creature).Traits.Size ?? 2 : 2;
                if (CombatHitboxes.SourceTouches(1, size, cell.X - target.X * 2, cell.Y - target.Y * 2)) { return target; }
            }
            return null;
        }
        int Hit(uint _, bool terminal)
        {
            if (next.X >= 0 && next.Y >= 0 && next.X < map.Width && next.Y < map.Height) { Explode(actor, owner, next); }
            removed = true; return 2;
        }
        BasicMissileUpdate.Run(new(true, true, state.VelocityFp8, Frames - state.Frame, 2, Frames, true),
            new(Move, _ => { }, () => true, () => true, () => 2, () => next.Steps.Cells,
                cell => Wall(cell) ? 4 : Find(cell) != null ? 1 : 0, cell => Wall(cell) ? 0 : Find(cell)?.Serial.Value ?? 0, Hit));
        if (removed) { Flights.Remove(state.Id); return; }
        Flights[state.Id] = next;
        if (next.Frame % 2 == 0) { Effects.SendLocationEffect(new((int)Math.Round(next.X), (int)Math.Round(next.Y), next.Z), map, 0x36D4, 2, 10, 0, 0); } // Interim travel art.
    }

    private static void Validate(TaintedFireBallFlight s)
    {
        if (s == null || s.Id <= 0 || !((Serial)s.Actor).IsMobile || !((Serial)s.Owner).IsMobile || s.Actor == s.Owner
            || s.Map < 0 || s.Map >= Map.Maps.Length || Map.Maps[s.Map] == null || Map.Maps[s.Map] == Map.Internal
            || s.Rank is < 1 or > 255 || s.Z is < -128 or > 127 || s.Frame is < 0 or >= Frames
            || s.NextUtc.Kind != DateTimeKind.Utc || s.NextUtc > DateTime.MaxValue.AddMilliseconds(-Frames * 40)
            || s.Packet is not { Minimum256: >= 0, Maximum256: >= 0, Radius: 4 } || s.Packet.Maximum256 < s.Packet.Minimum256
            || s.Slow.VelocityPercent is < 0 or > 100 || s.Slow.DamagePercent is < 0 or > 100
            || s.VelocityFp8 != SlowMissilePayload.VelocityFp8(20, 0, s.Rank, 0x21, 0, true, s.Slow.VelocityPercent)
            || !double.IsFinite(s.X) || !double.IsFinite(s.Y) || !double.IsFinite(s.Vx) || !double.IsFinite(s.Vy)
            || s.X < 0 || s.Y < 0 || s.X >= Map.Maps[s.Map].Width || s.Y >= Map.Maps[s.Map].Height
            || Math.Abs(s.Vx) > s.VelocityFp8 / 8192.0 || Math.Abs(s.Vy) > s.VelocityFp8 / 8192.0
            || s.Steps?.Cells is not { Length: >= 1 and <= 10 }
            || s.Steps.Cells.Any(c => c.X < 0 || c.Y < 0 || c.X > Map.Maps[s.Map].Width * 2 || c.Y > Map.Maps[s.Map].Height * 2))
        { throw new InvalidDataException("Invalid saved Tainted Fire Ball"); }
    }
    internal static TaintedFireBallSave Snapshot() => new(Flights.Values.OrderBy(s => s.Id).ToArray(), _nextId);
    internal static void Restore(TaintedFireBallSave saved)
    {
        if (saved?.Flights == null || saved.NextId < 0) { throw new InvalidDataException("Missing Tainted Fire Ball save"); }
        var rows = new Dictionary<long, TaintedFireBallFlight>();
        foreach (var s in saved.Flights)
        { Validate(s); if (s.Id > saved.NextId || !rows.TryAdd(s.Id, s)) { throw new InvalidDataException("Duplicate Tainted Fire Ball"); } }
        Flights.Clear(); foreach (var pair in rows) { Flights.Add(pair.Key, pair.Value); } _nextId = saved.NextId;
    }
    internal static void Cancel(WarlockTaintedBody actor)
    { foreach (long id in Flights.Values.Where(s => s.Actor == actor.Serial.Value).Select(s => s.Id).ToArray()) { Flights.Remove(id); } }
}
