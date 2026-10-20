// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public enum TimedEffectCategory : byte { Potion, Food, Map }
public sealed record TimedBuff(TimedEffectCategory Category, string Id, int Magnitude, DateTime ExpiresUtc, string Context);
public sealed record RestorationState(long Units, long Remainder, int Duration);
public sealed record TimedEffectState(uint Owner, DateTime UpdatedUtc, DateTime ChillUntilUtc, int ChillPercent,
    uint PoisonSource, int PoisonMs, double PoisonRate, double PoisonFraction,
    RestorationState Life, RestorationState Mana, TimedBuff[] Buffs);

internal sealed class RestorationLane
{
    internal long Units, Remainder;
    internal int Duration;
    internal void Add(int amount, int duration) { Units += amount * 1000000L; Duration += duration; }
    internal int Advance(int elapsed)
    {
        if (Duration == 0) { return 0; }
        int step = Math.Min(Duration, elapsed); long value = step == Duration ? Units : Units * step / Duration;
        Units -= value; Duration -= step; Remainder += value;
        int result = (int)(Remainder / 1000000); Remainder %= 1000000; return result;
    }
    internal void Clear() { Units = Remainder = 0; Duration = 0; }
    internal RestorationState Snapshot() => new(Units, Remainder, Duration);
    internal void Restore(RestorationState row) { Units = row.Units; Remainder = row.Remainder; Duration = row.Duration; }
}
internal sealed class TimedActor(DateTime now)
{
    internal DateTime Updated = now, ChillUntil = now;
    internal int ChillPercent, PoisonMs;
    internal Serial PoisonSource;
    internal double PoisonRate, PoisonFraction;
    internal readonly RestorationLane Life = new(), Mana = new();
    internal readonly Dictionary<TimedEffectCategory, TimedBuff> Buffs = new();
}

public static class TimedEffects
{
    private static readonly Dictionary<Serial, TimedActor> _actors = new();
    private static readonly List<Serial> _keys = new();
    private static readonly long _anchorTick = Core.TickCount;
    private static readonly DateTime _anchorUtc = DateTime.UtcNow;
    public static TimedEffectProfiles Profiles { get; private set; }
    public static DateTime TimeAt(long tick) => _anchorUtc.AddMilliseconds(tick - _anchorTick);
    public static DateTime Now => TimeAt(Core.TickCount);
    internal static DateTime? DamageTime { get; private set; }
    public static void Configure()
    {
        Profiles = new TimedEffectProfiles();
        EventSink.Connected += mobile => Settle(mobile, Now);
        EventSink.Disconnected += mobile => Settle(mobile, Now);
        EventSink.Logout += mobile => Settle(mobile, Now);
    }
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100), () => Tick(Now));
    internal static TimedActor Actor(Mobile owner, DateTime now)
    {
        if (!_actors.TryGetValue(owner.Serial, out var actor)) { _actors.Add(owner.Serial, actor = new(now)); }
        return actor;
    }
    public static void Clear(Mobile owner)
    {
        if (owner != null && _actors.Remove(owner.Serial, out var actor) && actor.Buffs.ContainsKey(TimedEffectCategory.Food)
            && owner is PlayerMobile p) { FoodEffects.Refresh(p); }
    }
    public static void ClearRestoration(Mobile owner)
    { if (owner != null && _actors.TryGetValue(owner.Serial, out var a)) { a.Life.Clear(); a.Mana.Clear(); } }
    public static void RemoveBuff(Mobile owner, TimedEffectCategory category)
    {
        if (owner != null && _actors.TryGetValue(owner.Serial, out var a) && a.Buffs.Remove(category)
            && category == TimedEffectCategory.Food && owner is PlayerMobile p) { FoodEffects.Refresh(p); }
    }
    internal static void DiscardFullRestoration(PlayerMobile owner)
    {
        if (_actors.TryGetValue(owner.Serial, out var a))
        {
            if (owner.Hits >= owner.HitsMax) { a.Life.Clear(); }
            if (owner.Mana >= owner.ManaMax) { a.Mana.Clear(); }
        }
    }
    public static void ClearGear(Mobile owner)
    {
        if (owner != null && _actors.TryGetValue(owner.Serial, out var a))
        {
            a.ChillUntil = a.Updated; a.ChillPercent = a.PoisonMs = 0; a.PoisonRate = a.PoisonFraction = 0;
            if (a.Buffs.Count == 0 && a.Life.Duration == 0 && a.Mana.Duration == 0) { _actors.Remove(owner.Serial); }
        }
    }
    public static TimedBuff Buff(Mobile owner, TimedEffectCategory category, DateTime? at = null) => owner is { Alive: true, Deleted: false }
        && _actors.TryGetValue(owner.Serial, out var a) && a.Buffs.TryGetValue(category, out var b) && b.ExpiresUtc > (at ?? Now) ? b : null;
    public static bool ApplyBuff(PlayerMobile owner, TimedEffectCategory category, string id, int magnitude, string context = null, DateTime? at = null)
    {
        if (owner is not { Alive: true, Deleted: false } || !ClassCombat.Enabled(owner) || !Enum.IsDefined(category)
            || !Identifier(id) || magnitude is < -10000 or > 10000 || context?.Length > 256) { return false; }
        DateTime now = at ?? Now; Settle(owner, now); if (!owner.Alive) { return false; }
        var a = Actor(owner, now); int seconds = category switch { TimedEffectCategory.Potion => 30, TimedEffectCategory.Food => 3600, _ => 1800 };
        DateTime until = now.AddSeconds(seconds);
        if (category == TimedEffectCategory.Potion && Buff(owner, category, now) is { } old && old.Id == id)
        {
            // A repeated dose extends one frozen magnitude; it never grows or carries time into another buff.
            if (old.Magnitude != magnitude || old.Context != context) { return false; }
            until = new DateTime(Math.Min(now.AddSeconds(90).Ticks, old.ExpiresUtc.AddSeconds(30).Ticks), DateTimeKind.Utc);
            if (until <= old.ExpiresUtc) { return false; }
        }
        a.Buffs[category] = new(category, id, magnitude, until, context); return true;
    }
    internal static bool Identifier(string id)
    {
        if (id is not { Length: > 0 and <= 64 }) { return false; }
        foreach (char c in id) { if (!(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')) { return false; } }
        return true;
    }
    public static bool AddRestoration(PlayerMobile owner, bool life, int amount, int duration, DateTime? at = null)
    {
        DateTime now = at ?? Now; Settle(owner, now);
        if (owner is not { Alive: true, Deleted: false } || amount is < 1 or > 1000000 || duration is < 1 or > 600000
            || (life ? owner.Hits >= owner.HitsMax : owner.Mana >= owner.ManaMax)) { return false; }
        var a = Actor(owner, now); var lane = life ? a.Life : a.Mana;
        if (lane.Duration > 600000 - duration) { return false; }
        if (lane.Units > 10000000000000L - amount * 1000000L) { return false; }
        lane.Add(amount, duration); return true;
    }
    public static void Settle(Mobile owner, DateTime now)
    {
        if (owner == null || !_actors.TryGetValue(owner.Serial, out var a)) { return; }
        if (owner.Deleted || !owner.Alive) { Clear(owner); return; }
        if (now <= a.Updated) { return; }
        int elapsed = (int)Math.Clamp((now - a.Updated).TotalMilliseconds, 0, 600000);
        DateTime cursor = a.Updated;
        // Catch-up follows the same short periodic steps as live play. Poison settles before healing in a step;
        // lethal damage cannot be undone by a later restorative payment from the same offline interval.
        while (elapsed > 0 && owner.Alive && !owner.Deleted && (a.PoisonMs > 0 || a.Life.Duration > 0 || a.Mana.Duration > 0))
        {
            int step = a.PoisonMs > 0 ? Math.Min(100, elapsed) : elapsed; elapsed -= step; cursor = cursor.AddMilliseconds(step);
            if (a.PoisonMs > 0)
            {
                int poisonStep = Math.Min(step, a.PoisonMs); a.PoisonMs -= poisonStep;
                a.PoisonFraction += poisonStep * a.PoisonRate;
                int damage = (int)Math.Floor(a.PoisonFraction + 1e-9); a.PoisonFraction -= damage;
                if (damage > 0)
                {
                    var source = World.FindMobile(a.PoisonSource);
                    // Damage-time consumers must see the historical tick during offline catch-up.
                    var previous = DamageTime; DamageTime = cursor;
                    try { owner.Damage(damage, source); CombatDamage.Finish(owner, source); }
                    finally { DamageTime = previous; }
                }
            }
            if (!owner.Alive || owner.Deleted) { Clear(owner); return; }
            if (owner is PlayerMobile player)
            {
                Restore(player, a.Life, true, step); Restore(player, a.Mana, false, step);
            }
        }
        a.Updated = now;
        foreach (var category in new[] { TimedEffectCategory.Potion, TimedEffectCategory.Food, TimedEffectCategory.Map })
        {
            if (a.Buffs.TryGetValue(category, out var b) && b.ExpiresUtc <= now)
            { a.Buffs.Remove(category); if (category == TimedEffectCategory.Food && owner is PlayerMobile p) { FoodEffects.Refresh(p); } }
        }
        if (a.ChillUntil <= now) { a.ChillPercent = 0; }
        if (a.Buffs.Count == 0 && a.ChillPercent == 0 && a.PoisonMs == 0 && a.PoisonFraction < 1e-12
            && a.Life.Duration == 0 && a.Mana.Duration == 0) { _actors.Remove(owner.Serial); }
    }
    private static void Restore(PlayerMobile player, RestorationLane lane, bool life, int step)
    {
        if (life ? player.Hits >= player.HitsMax : player.Mana >= player.ManaMax) { lane.Clear(); return; }
        int points = lane.Advance(step);
        if (life) { player.Hits = Math.Min(player.HitsMax, player.Hits + points); }
        else { player.Mana = Math.Min(player.ManaMax, player.Mana + points); }
        if (life ? player.Hits >= player.HitsMax : player.Mana >= player.ManaMax) { lane.Clear(); }
    }
    public static void Tick(DateTime now)
    {
        _keys.Clear(); foreach (var key in _actors.Keys) { _keys.Add(key); }
        foreach (var key in _keys)
        {
            if (World.FindMobile(key) is { Deleted: false } owner) { Settle(owner, now); }
            else { _actors.Remove(key); }
        }
    }
    internal static int Chill(Mobile owner, DateTime now) => owner is { Alive: true, Deleted: false }
        && _actors.TryGetValue(owner.Serial, out var a) && now < a.ChillUntil ? a.ChillPercent : 0;
    internal static void Chill(Mobile owner, int percent, int duration, DateTime now)
    {
        Settle(owner, now); if (!owner.Alive || owner.Deleted) { return; }
        var a = Actor(owner, now); a.ChillPercent = percent;
        a.ChillUntil = new DateTime(Math.Max(a.ChillUntil.Ticks, now.AddMilliseconds(duration).Ticks), DateTimeKind.Utc);
    }
    internal static bool Poison(Mobile source, Mobile target, double total, int duration, DateTime now)
    {
        Settle(target, now); if (!target.Alive || target.Deleted) { return false; }
        var a = Actor(target, now); double rate = total / duration;
        if (a.PoisonMs > 0 && rate + 1e-12 < a.PoisonRate) { return false; }
        a.PoisonRate = rate; a.PoisonMs = duration; a.PoisonSource = source.Serial; return true;
    }
    public static TimedEffectState[] Snapshot()
    {
        var rows = new List<TimedEffectState>();
        foreach (var (serial, a) in _actors)
        {
            if (World.FindMobile(serial) is not { Alive: true, Deleted: false }) { continue; }
            var buffs = new TimedBuff[a.Buffs.Count]; a.Buffs.Values.CopyTo(buffs, 0);
            rows.Add(new(serial.Value, a.Updated, a.ChillUntil, a.ChillPercent, a.PoisonSource.Value, a.PoisonMs,
                a.PoisonRate, a.PoisonFraction, a.Life.Snapshot(), a.Mana.Snapshot(), buffs));
        }
        return rows.ToArray();
    }
    internal static void Load(TimedEffectState[] rows)
    {
        var restored = new Dictionary<Serial, TimedActor>();
        foreach (var r in rows)
        {
            TimedEffectProfiles.Validate(r);
            var a = new TimedActor(r.UpdatedUtc) { ChillUntil = r.ChillUntilUtc, ChillPercent = r.ChillPercent,
                PoisonSource = (Serial)r.PoisonSource, PoisonMs = r.PoisonMs, PoisonRate = r.PoisonRate, PoisonFraction = r.PoisonFraction };
            a.Life.Restore(r.Life); a.Mana.Restore(r.Mana);
            foreach (var b in r.Buffs) { a.Buffs.Add(b.Category, b); }
            restored.Add((Serial)r.Owner, a);
        }
        _actors.Clear(); foreach (var row in restored) { _actors.Add(row.Key, row.Value); }
    }
}

public sealed class TimedEffectProfiles : GenericPersistence
{
    private TimedEffectState[] _snapshot = [];
    private static readonly JsonSerializerOptions _json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public TimedEffectProfiles() : base("SanctuaryTimedEffects", 125) { }
    // Capture value records on the game loop. Save workers only encode this immutable snapshot;
    // settling effects inside a serializer would desynchronize saved health and remaining damage.
    public override void Serialize() { Capture(); base.Serialize(); }
    internal void Capture() => _snapshot = TimedEffects.Snapshot();
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_snapshot.Length);
        foreach (var row in _snapshot) { writer.Write(JsonSerializer.Serialize(row)); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unsupported timed-effect version."); }
        int count = reader.ReadInt(); if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid timed-effect count."); }
        var rows = new TimedEffectState[count];
        for (int i = 0; i < count; i++)
        {
            string json = reader.ReadString(); if (json is not { Length: > 0 and <= 16000 }) { throw new InvalidDataException("Invalid timed-effect payload."); }
            rows[i] = JsonSerializer.Deserialize<TimedEffectState>(json, _json);
        }
        TimedEffects.Load(rows);
    }
    internal static void Validate(TimedEffectState r)
    {
        static bool Lane(RestorationState l) => l != null && l.Duration is >= 0 and <= 600000 && l.Units is >= 0 and <= 10000000000000L
            && l.Remainder is >= 0 and < 1000000 && (l.Duration > 0 || l.Units == 0);
        if (r == null || r.Owner == 0 || r.UpdatedUtc.Kind != DateTimeKind.Utc || r.ChillUntilUtc.Kind != DateTimeKind.Utc
            || r.ChillPercent is < 0 or > 50 || r.PoisonMs is < 0 or > 60000 || !double.IsFinite(r.PoisonRate)
            || r.PoisonRate is < 0 or > 1000000 || !double.IsFinite(r.PoisonFraction) || r.PoisonFraction is < -1e-8 or >= 1.00000001
            || !Lane(r.Life) || !Lane(r.Mana) || r.Buffs is not { Length: <= 3 }) { throw new InvalidDataException("Invalid timed effect."); }
        var categories = new HashSet<TimedEffectCategory>();
        foreach (var b in r.Buffs)
        {
            if (b == null || !Enum.IsDefined(b.Category) || !categories.Add(b.Category) || !TimedEffects.Identifier(b.Id)
                || b.Magnitude is < -10000 or > 10000 || b.ExpiresUtc.Kind != DateTimeKind.Utc || b.Context?.Length > 256)
            { throw new InvalidDataException("Invalid timed buff."); }
        }
    }
}
