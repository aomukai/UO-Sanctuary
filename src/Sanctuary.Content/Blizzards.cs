using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record BlizzardStorm(long Id, uint Owner, int Rank, int Map, int X, int Y, int Z,
    int Frame, DateTime UntilUtc, DateTime NextUtc);
public sealed record BlizzardShard(long Id, uint Owner, int Map, double X, double Y, int Z, int Min256, int Max256,
    int ColdFrames, int Remaining, uint LastCollision, DateTime UntilUtc, DateTime NextUtc);
public sealed record BlizzardSave(BlizzardStorm[] Storms, BlizzardShard[] Shards);

public sealed class BlizzardProfiles : GenericPersistence
{
    public BlizzardProfiles() : base("SanctuaryBlizzard", 116) { }
    public override void Serialize(IGenericWriter writer) { writer.Write(0); writer.Write(JsonSerializer.Serialize(Blizzards.Snapshot())); }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown Blizzard version"); }
        Blizzards.Restore(JsonSerializer.Deserialize<BlizzardSave>(reader.ReadString()));
    }
}

public static class Blizzards
{
    private static readonly Dictionary<long, BlizzardStorm> _storms = new();
    private static readonly Dictionary<long, BlizzardShard> _shards = new();
    private static readonly List<long> _keys = new();
    private static long _nextId;
    public static BlizzardProfiles Profiles { get; private set; }
    public static void Configure() => Profiles = new();
    public static void Initialize() => Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromMilliseconds(40), Tick);
    public static Point2D Sample(int sourceX, int elapsedFrame, int range)
    {
        // The legacy GetRemainingFrames helper returns total - current: elapsed frames.
        // InitLowSeed resets BOTH halves; do not retain carry between separate falls.
        ulong seed = ((ulong)666 << 32) | unchecked((uint)(sourceX + elapsedFrame));
        int radius = Math.Max(0, range - 1), bound = radius * 2;
        return new(Next() - radius, Next() - radius);
        int Next()
        {
            if (bound <= 0) { return 0; }
            seed = 0x6AC690C5UL * (seed & uint.MaxValue) + (seed >> 32);
            return (int)((uint)seed % (uint)bound);
        }
    }
    public static void Cast(PlayerMobile owner, Point3D point, SkillValues values)
    {
        var now = TimedEffects.Now; long id = ++_nextId;
        _storms.Add(id, new(id, owner.Serial.Value, values.Rank, owner.Map.MapID, point.X, point.Y, point.Z, 0,
            now.AddMilliseconds(NecromancerSummons.Source("blizzardcenter", "Range") * 40), now));
    }
    private static PlayerMobile Owner(uint serial, int map) => World.FindMobile((Serial)serial) is PlayerMobile { Alive: true, Deleted: false } player
        && ClassCreation.Profiles.TryGet(player.Serial, out var profile) && profile.Class == DiabloClass.Sorceress
        && (player.Map == Map.Internal || player.Map.MapID == map && !SpellHelper.IsTown(player.Location, player)) ? player : null;
    public static void Cancel(Mobile owner)
    {
        var ids = new List<long>();
        foreach (var (id, storm) in _storms) { if (storm.Owner == owner.Serial.Value) { ids.Add(id); } }
        foreach (long id in ids) { _storms.Remove(id); } ids.Clear();
        foreach (var (id, shard) in _shards) { if (shard.Owner == owner.Serial.Value) { ids.Add(id); } }
        foreach (long id in ids) { _shards.Remove(id); }
    }
    private static void Spawn(PlayerMobile owner, BlizzardStorm storm, SkillValues values, DateTime now)
    {
        var offset = Sample((int)(storm.X * SkillGeometry.SubtilesPerTile), storm.Frame, (int)values.Eval("calc1"));
        double x = storm.X + SkillGeometry.Tiles(offset.X), y = storm.Y + SkillGeometry.Tiles(offset.Y);
        var point = new Point3D((int)Math.Round(x), (int)Math.Round(y), storm.Z);
        // Source mask5 tests the landing cell; there is no radial LOS ray from the caster/center to each fall.
        if (point.X < 0 || point.Y < 0 || point.X >= owner.Map.Width || point.Y >= owner.Map.Height
            || !owner.Map.CanFit(point, 1, false, false, true)) { return; }
        int potion = CombatPotions.Bonus(owner, "magical-penetration");
        int Scale(int value) => (int)Math.Clamp(NamedElements.Enhance(owner, value * (100 + potion) / 100.0, "cold"), 0, int.MaxValue);
        long id = ++_nextId; int frames = NecromancerSummons.Source("blizzard1", "Range");
        _shards.Add(id, new(id, owner.Serial.Value, storm.Map, x, y, storm.Z, Scale(values.Damage256(false)), Scale(values.Damage256(true)),
            values.DurationFrames, frames, 0, now.AddMilliseconds(frames * 40), now));
        Effects.SendLocationEffect(point, owner.Map, 0x376A, 9, 10, 0x480, 0); // Placeholder falling ice.
    }
    private static void Tick()
    {
        var now = TimedEffects.Now; _keys.Clear(); _keys.AddRange(_storms.Keys);
        foreach (long id in _keys)
        {
            if (!_storms.TryGetValue(id, out var storm)) { continue; }
            var owner = Owner(storm.Owner, storm.Map);
            if (owner == null || storm.UntilUtc <= now || storm.Frame >= NecromancerSummons.Source("blizzardcenter", "Range"))
            { _storms.Remove(id); continue; }
            if (now < storm.NextUtc || owner.Map == Map.Internal) { continue; }
            var values = SkillMechanics.Values(owner, CombatAbility.Blizzard, storm.Rank);
            int interval = (int)values.Eval("calc2");
            if (interval > 0 && storm.Frame % interval == 0) { Spawn(owner, storm, values, now); }
            _storms[id] = storm with { Frame = storm.Frame + 1, NextUtc = storm.NextUtc.AddMilliseconds(40) };
        }
        _keys.Clear(); _keys.AddRange(_shards.Keys);
        foreach (long id in _keys)
        {
            if (!_shards.TryGetValue(id, out var shard)) { continue; }
            var owner = Owner(shard.Owner, shard.Map);
            // Collision handler decrements first and expires before testing the zero frame.
            if (owner == null || shard.UntilUtc <= now || shard.Remaining <= 1) { _shards.Remove(id); continue; }
            if (now < shard.NextUtc || owner.Map == Map.Internal) { continue; }
            uint last = shard.LastCollision;
            var point = new Point3D((int)Math.Round(shard.X), (int)Math.Round(shard.Y), shard.Z);
            double radius = 0.5 + SkillGeometry.Tiles(NecromancerSummons.Source("blizzard1", "Size")) / 2;
            foreach (var target in SkillCombat.Targets(owner, point, 2))
            {
                double dx = target.X - shard.X, dy = target.Y - shard.Y;
                if (target.Serial.Value == last || dx * dx + dy * dy > radius * radius) { continue; }
                last = target.Serial.Value;
                int damage = shard.Max256 <= shard.Min256 ? shard.Min256 : Utility.RandomMinMax(shard.Min256, shard.Max256 - 1);
                SorceressEffects.MissileHit(owner, target, "blizzard1");
                SkillCombat.ColdImpact(owner, target, damage, shard.ColdFrames);
                target.FixedEffect(0x374A, 10, 10, 0x480, 0); // Placeholder ice impact.
                break; // One collision per shard/frame; another overlapped unit can replace LastCollide next frame.
            }
            if (_shards.ContainsKey(id))
            { _shards[id] = shard with { LastCollision = last, Remaining = shard.Remaining - 1, NextUtc = shard.NextUtc.AddMilliseconds(40) }; }
        }
    }
    public static BlizzardSave Snapshot() => new(new List<BlizzardStorm>(_storms.Values).ToArray(), new List<BlizzardShard>(_shards.Values).ToArray());
    public static void Restore(BlizzardSave save)
    {
        if (save?.Storms == null || save.Shards == null) { throw new InvalidDataException("Missing Blizzard state"); }
        _storms.Clear(); _shards.Clear(); _nextId = 0;
        foreach (var storm in save.Storms)
        {
            if (storm.Id <= 0 || storm.Rank <= 0 || storm.Frame is < 0 or >= 100 || !_storms.TryAdd(storm.Id, storm))
            { throw new InvalidDataException("Invalid Blizzard storm"); }
            _nextId = Math.Max(_nextId, storm.Id);
        }
        foreach (var shard in save.Shards)
        {
            if (shard.Id <= 0 || shard.Min256 < 0 || shard.Max256 < shard.Min256 || shard.ColdFrames < 0 || shard.Remaining is < 1 or > 9
                || !double.IsFinite(shard.X + shard.Y) || _storms.ContainsKey(shard.Id) || !_shards.TryAdd(shard.Id, shard))
            { throw new InvalidDataException("Invalid Blizzard shard"); }
            _nextId = Math.Max(_nextId, shard.Id);
        }
    }
}
