// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

public sealed record ArmageddonState(uint Owner, int Rank, int Memory, ulong Seed, DateTime StartedUtc, DateTime NextUtc, DateTime UntilUtc);
internal sealed record ArmageddonActor(long Id, uint Owner, int Map, int Rank, int Missile, int X, int Y, int Z,
    int Remaining, BoulderEndpoints Endpoints, FirestormModifiers Modifiers, ulong Seed, DateTime NextUtc, DateTime UntilUtc);
internal sealed record ArmageddonSave(ArmageddonState[] Weather, ArmageddonActor[] Actors, long NextId);
public sealed class ArmageddonProfiles : GenericPersistence
{
    public ArmageddonProfiles() : base("SanctuaryArmageddon", 172) { }
    public override void Serialize(IGenericWriter writer)
    { writer.Write(0); writer.Write(JsonSerializer.Serialize(DruidArmageddon.Snapshot())); }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown Armageddon version"); }
        DruidArmageddon.Restore(JsonSerializer.Deserialize<ArmageddonSave>(reader.ReadString()));
    }
}

// Skill249/state145/do146: independent6-frame emitter, stationary25-frame
// control577, copied physical/fire terminal area and37-frame ground623.
public static class DruidArmageddon
{
    private sealed record Pool(ImmutableDictionary<uint, ArmageddonState> Weather, ImmutableDictionary<long, ArmageddonActor> Actors, long NextId);
    private static Pool _pool = new(ImmutableDictionary<uint, ArmageddonState>.Empty, ImmutableDictionary<long, ArmageddonActor>.Empty, 0);
    internal static Func<int> SeedLow = () => unchecked((int)Random.Shared.NextInt64(1L << 32));
    internal static Func<PlayerMobile,int,int,bool> CanPlace = Placement;
    internal static Func<Mobile, bool> Uninterruptible = BarbarianLeap.Uninterruptible;
    public static ArmageddonProfiles Profiles { get; private set; }
    public static void Configure() => Profiles = new();
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40), () => Tick(TimedEffects.Now));
    private static PlayerMobile Owner(uint serial) => World.FindMobile((Serial)serial) is PlayerMobile { Alive: true, Deleted: false } p
        && ClassCreation.Profiles.TryGet(p.Serial, out var profile) && profile.Class == DiabloClass.Druid ? p : null;
    public static ArmageddonState State(Mobile owner) => owner != null && Owner(owner.Serial.Value) == owner ? _pool.Weather.GetValueOrDefault(owner.Serial.Value) : null;
    public static void Clear(Mobile owner)
    {
        if (owner == null) { return; }
        _pool = _pool with { Weather = _pool.Weather.Remove(owner.Serial.Value) };
        // Released impacts/fields have their own expiry; class/death owner
        // validation still removes their unsupported old-class actors.
    }
    internal static ArmageddonSave Snapshot() => new(_pool.Weather.Values.OrderBy(x => x.Owner).ToArray(), _pool.Actors.Values.OrderBy(x => x.Id).ToArray(), _pool.NextId);
    internal static void Restore(ArmageddonSave saved)
    {
        if (saved?.Weather == null || saved.Actors == null || saved.NextId < 0 || saved.Weather.Length > 1000000 || saved.Actors.Length > 1000000)
        { throw new InvalidDataException("Missing Armageddon state"); }
        var next = new Pool(ImmutableDictionary<uint, ArmageddonState>.Empty, ImmutableDictionary<long, ArmageddonActor>.Empty, saved.NextId);
        foreach (var state in saved.Weather)
        { Validate(state); if (next.Weather.ContainsKey(state.Owner)) { throw new InvalidDataException("Duplicate Armageddon weather"); } next = next with { Weather = next.Weather.Add(state.Owner, state) }; }
        foreach (var actor in saved.Actors)
        { Validate(actor); if (actor.Id > saved.NextId || next.Actors.ContainsKey(actor.Id)) { throw new InvalidDataException("Duplicate Armageddon actor"); } next = next with { Actors = next.Actors.Add(actor.Id, actor) }; }
        _pool = next;
    }
    private static void Validate(ArmageddonState state)
    {
        long frame = TimeSpan.FromMilliseconds(40).Ticks;
        if (state == null || !((Serial)state.Owner).IsMobile || state.Rank is < 1 or > 255 || state.Memory is < 0 or >= 1000000
            || state.StartedUtc.Kind != DateTimeKind.Utc || state.NextUtc.Kind != DateTimeKind.Utc || state.UntilUtc.Kind != DateTimeKind.Utc
            || state.UntilUtc <= state.StartedUtc || (state.UntilUtc - state.StartedUtc).Ticks % frame != 0
            || (state.UntilUtc - state.StartedUtc).Ticks / frame is < 250 or > 1250
            || ((state.UntilUtc - state.StartedUtc).Ticks / frame - 250) % 50 != 0 || state.NextUtc <= state.StartedUtc
            || (state.NextUtc - state.StartedUtc).Ticks % (6 * frame) != 0 || (state.NextUtc - state.UntilUtc).Ticks > 6 * frame)
        { throw new InvalidDataException("Invalid Armageddon weather clock"); }
    }
    private static void Validate(ArmageddonActor actor)
    {
        var e = actor?.Endpoints ?? default; int frames = actor?.Missile == 577 ? 25 : 37;
        if (actor == null || actor.Id <= 0 || !((Serial)actor.Owner).IsMobile || actor.Rank is < 1 or > 255 || actor.Missile is not (577 or 623)
            || actor.Map < 0 || actor.Map >= Map.Maps.Length || Map.Maps[actor.Map] == null || Map.Maps[actor.Map] == Map.Internal
            || actor.X <= 0 || actor.Y <= 0 || actor.X >= Map.Maps[actor.Map].Width * 2 || actor.Y >= Map.Maps[actor.Map].Height * 2
            || actor.Remaining < 1 || actor.Remaining > frames || actor.NextUtc.Kind != DateTimeKind.Utc || actor.UntilUtc.Kind != DateTimeKind.Utc
            || (actor.UntilUtc - actor.NextUtc).Ticks != (actor.Remaining - 1) * TimeSpan.FromMilliseconds(40).Ticks
            || actor.Modifiers == null || actor.Modifiers.Potion is < 0 or > 1000 || e.PhysicalMin < 0 || e.PhysicalMax < e.PhysicalMin || e.FireMin < 0 || e.FireMax < e.FireMin
            || actor.Missile == 623 && (e.PhysicalMin != 0 || e.PhysicalMax != 0))
        { throw new InvalidDataException("Invalid Armageddon actor"); }
    }
    private static void Routes(SkillValues values)
    {
        var row = SkillMechanics.For(CombatAbility.Armageddon);
        if (values.Ability != CombatAbility.Armageddon || row.SourceId != 249 || row.Number("srvdofunc") != 124
            || row.Text("aurastate") != "armageddon" || row.Text("srvmissilea") != "armageddoncontrol" || row.Text("EType") != "fire"
            || row.Number("Param4") != 6 || row.Number("Param3") != 8 || row.Number("localdelay") != 0 || values.Mana256 != 35 * 256)
        { throw new NotSupportedException("Unsupported Armageddon weather route"); }
        foreach (var name in new[] { "armageddoncontrol", "armageddonfire" })
        {
            bool ground = name == "armageddonfire"; int F(string key) => NecromancerSummons.Source(name, key);
            if (F("pSrvDoFunc") != 1 || F("pSrvHitFunc") != (ground ? 0 : 56) || F("Range") != (ground ? 37 : 25)
                || F("Vel") != 0 || F("CollideType") != (ground ? 8 : 0) || F("Collision") != (ground ? 1 : 0) || F("Size") != (ground ? 2 : 0)
                || F("LastCollide") != 0 || F("NextHit") != 0 || F("NextDelay") != 0 || F("ToHit") != 0 || F("CollideKill") != 0
                || F("pSrvDmgFunc") != 0 || F("Town") != 0 || F("SrcTown") != 0
                || F("ResultFlags") != (ground ? 0 : 5) || F("HitFlags") != (ground ? 0 : 2)
                || !ground && F("sHitPar1") != 4 || F("GetHit") != 0 || F("SoftHit") != 0 || F("KnockBack") != 0 || F("DamageRate") != (ground ? 41 : 0)
                || F("CanSlow") != 1 || F("SrcDamage") != 0 || F("SrcMissDmg") != 0 || F("ApplyMastery") != 0)
            { throw new NotSupportedException("Unsupported Armageddon missile route"); }
        }
    }
    public static bool Begin(PlayerMobile owner, Serial selected, Point3D point, long now)
        => SkillCombat.Begin(owner, CombatAbility.Armageddon, selected, point, now, (p, _, _, _) => DruidWeather.Outdoors(p));
    public static bool Cast(PlayerMobile owner, SkillValues values)
    {
        if (!DruidCombat.Available(owner, CombatAbility.Armageddon) || !DruidWeather.Outdoors(owner)) { return false; }
        ArmageddonState state;
        try { state = Prepare(owner, values, TimedEffects.Now); }
        catch (NotSupportedException) { owner.SendMessage(0x22, "Armageddon cannot be used in this context yet."); return false; }
        if (!NecromancerEffects.Pay(owner, values.Mana256)) { return false; }
        _pool = _pool with { Weather = _pool.Weather.SetItem(state.Owner, state) }; owner.PlaySound(0x1E5); // Identified fire-cast audio placeholder.
        return true;
    }
    internal static ArmageddonState Prepare(PlayerMobile owner, SkillValues values, DateTime at)
    {
        Routes(values); var start = DruidWeather.Prepare(owner, values, at, SeedLow);
        var state = new ArmageddonState(owner.Serial.Value, values.Rank, start.Param, start.Seed, at, start.NextUtc, start.UntilUtc); Validate(state); return state;
    }
    internal static void Create(PlayerMobile owner, SkillValues values, DateTime at)
    { var state = Prepare(owner, values, at); _pool = _pool with { Weather = _pool.Weather.SetItem(state.Owner, state) }; }
    internal static BoulderEndpoints Endpoints(SkillValues values, int missile, int mastery, SlowMissilePayload slow)
    {
        if (missile == 577)
        {
            var fire = DruidFirestorm.Endpoints(values, mastery, slow);
            return new(values.PhysicalDamage256(false), values.PhysicalDamage256(true), fire.Min, fire.Max);
        }
        if (missile != 623) { throw new NotSupportedException("Unmapped Armageddon damage supplier"); }
        int synergy = NecromancerSummons.Formula("armageddonfire", "EDmgSymPerCalc", values);
        return new(0, 0, slow.Damage256(FireGround.MissileDamage256("armageddonfire", values.Rank, synergy, false)),
            slow.Damage256(FireGround.MissileDamage256("armageddonfire", values.Rank, synergy, true)));
    }
    internal static bool Placement(PlayerMobile owner, int x, int y)
    {
        var map = owner.Map; var point = new Point3D((x + 1) / 2, (y + 1) / 2, owner.Z);
        // Declared native projection of radial barrier, room and mask1.
        return x > 0 && y > 0 && x < map.Width * 2 && y < map.Height * 2 && !SpellHelper.IsTown(point, owner)
            && map.LineOfSight(new Point3D(point.X, point.Y, point.Z + 12), new Point3D(owner.X, owner.Y, owner.Z + 12))
            && map.CanFit(point, 1, false, false, true) && !LobProjectiles.Barrier(map, point);
    }
    internal static ArmageddonState Emit(PlayerMobile owner, ArmageddonState state)
    {
        ulong seed = SourceRandom.InitLow(state.Memory); int memory = (int)(SourceRandom.Roll(ref seed) % 1000000);
        int range = (int)SkillMechanics.Values(owner, CombatAbility.Armageddon, state.Rank).Eval("aurarangecalc");
        for (int i = 0; i < 5; i++)
        {
            int x = owner.X * 2 + (int)SourceRandom.Limited(ref seed, 2 * range + 1) - range;
            int y = owner.Y * 2 + (int)SourceRandom.Limited(ref seed, 2 * range + 1) - range;
            if (!CanPlace(owner, x, y)) { continue; }
            Add(owner, 577, state.Rank, x, y, owner.Z, state.NextUtc); break;
        }
        return state with { Memory = memory, Seed = seed, NextUtc = state.NextUtc.AddMilliseconds(240) };
    }
    internal static long Add(PlayerMobile owner, int missile, int rank, int x, int y, int z, DateTime at)
    {
        var values = SkillMechanics.Values(owner, CombatAbility.Armageddon, rank); Routes(values); var mods = DruidFirestorm.Modifiers(owner);
        var ends = Endpoints(values, missile, mods.FireMastery, AmazonSlowMissiles.Capture(owner, true)); int frames = missile == 577 ? 25 : 37;
        long id = checked(_pool.NextId + 1);
        var actor = new ArmageddonActor(id, owner.Serial.Value, owner.Map.MapID, rank, missile, x, y, z, frames, ends, mods,
            SourceRandom.InitLow(SeedLow()), at.AddMilliseconds(40), at.AddMilliseconds(frames * 40)); Validate(actor);
        _pool = _pool with { Actors = _pool.Actors.Add(id, actor), NextId = id };
        Effects.SendLocationEffect(new((x + 1) / 2, (y + 1) / 2, z), owner.Map, missile == 577 ? 0x36D4 : 0x398C, 5, 10, 0x489, 0); // Identified fall/fire placeholders.
        return id;
    }
    private static GearDamage Roll(ArmageddonActor actor, ref ulong seed)
    {
        ulong next = seed;
        int physical = WeaponMissileDamage.RollValue(actor.Endpoints.PhysicalMin, actor.Endpoints.PhysicalMax, 0, n => (int)SourceRandom.Limited(ref next, n));
        int fire = WeaponMissileDamage.RollValue(actor.Endpoints.FireMin, actor.Endpoints.FireMax, 0, n => (int)SourceRandom.Limited(ref next, n)); seed = next;
        return DruidMoltenBoulder.Potion(actor.Modifiers, new(physical / 256.0, fire / 256.0, 0, 0, 0));
    }
    private static void Terminal(PlayerMobile owner, ArmageddonActor actor)
    {
        ulong seed = actor.Seed; var packet = Roll(actor, ref seed); var targets = new List<BaseCreature>();
        var point = new Point3D((actor.X + 1) / 2, (actor.Y + 1) / 2, actor.Z);
        foreach (var target in owner.Map.GetMobilesInRange<BaseCreature>(point, 3))
        { if (SourceDamageAreas.Recipient(owner, target, actor.Map, actor.X, actor.Y, actor.Z, 4)) { targets.Add(target); } }
        foreach (var target in targets)
        {
            if (!SourceDamageAreas.Recipient(owner, target, actor.Map, actor.X, actor.Y, actor.Z, 4) || ClassAvoidance.Missile(target)) { continue; }
            owner.DoHarmful(target, true); var parts = SkillCombat.MitigatePrepared(owner, target, packet);
            int loss = SkillHealth.Damage(owner, target, NamedElements.To256(parts.Total));
            if (!Uninterruptible(target)) { CreatureHitRecovery.Apply(target, owner, loss); }
        }
        // Child reads current owner synergies/gear at retained rank; parent
        // damage and child packet are not the same captured payload.
        Add(owner, 623, actor.Rank, actor.X, actor.Y, actor.Z, actor.NextUtc);
        Effects.SendLocationEffect(point, owner.Map, 0x3709, 8, 10, 0x489, 0); // Identified terminal blast placeholder.
    }
    private static void Advance(ArmageddonActor actor, bool? actions)
    {
        var owner = Owner(actor.Owner); int remaining = actor.Remaining - 1;
        if (owner == null) { _pool = _pool with { Actors = _pool.Actors.Remove(actor.Id) }; return; }
        bool fighting = owner.Map?.MapID == actor.Map && owner.Map != Map.Internal && (actions ?? owner.NetState is { Running: true });
        if (remaining <= 0)
        {
            _pool = _pool with { Actors = _pool.Actors.Remove(actor.Id) };
            if (fighting && actor.Missile == 577 && !SpellHelper.IsTown(new((actor.X + 1) / 2, (actor.Y + 1) / 2, actor.Z), owner)) { Terminal(owner, actor); }
            return;
        }
        var next = actor with { Remaining = remaining, NextUtc = actor.NextUtc.AddMilliseconds(40) };
        _pool = _pool with { Actors = _pool.Actors.SetItem(actor.Id, next) };
        if (!fighting || actor.Missile != 623) { return; }
        var point = new Point3D((actor.X + 1) / 2, (actor.Y + 1) / 2, actor.Z);
        if (SpellHelper.IsTown(point, owner) || !owner.Map.CanFit(point, 1, false, false, false) || LobProjectiles.Barrier(owner.Map, point))
        { _pool = _pool with { Actors = _pool.Actors.Remove(actor.Id) }; return; }
        var patch = new FirePatch(actor.Id, actor.Owner, CombatAbility.Armageddon, actor.Map, actor.X / 2.0, actor.Y / 2.0, actor.Z, 0, 0, 41, actor.UntilUtc, actor.NextUtc, 2);
        if (ImmolationArrows.Recipient(owner, patch) is not BaseCreature target || ClassAvoidance.Missile(target)) { return; }
        ulong seed = actor.Seed; var packet = Roll(actor, ref seed);
        _pool = _pool with { Actors = _pool.Actors.SetItem(actor.Id, next with { Seed = seed }) };
        owner.DoHarmful(target, true); var parts = SkillCombat.MitigatePrepared(owner, target, packet, damageRate: 41);
        SkillHealth.Damage(owner, target, NamedElements.To256(parts.Total)); // No GetHit, knockback or source weapon callbacks.
    }
    internal static void Tick(DateTime at, bool? actions = null)
    {
        if (at.Kind != DateTimeKind.Utc) { throw new ArgumentException("Armageddon clock must be UTC"); }
        // Retain source event order among due native clocks; expired/offline
        // weather never replays historical emissions on reconnect.
        foreach (var state in _pool.Weather.Values.Where(x => Owner(x.Owner) == null || x.UntilUtc <= at).ToArray())
        { _pool = _pool with { Weather = _pool.Weather.Remove(state.Owner) }; }
        while (true)
        {
            var due = _pool.Weather.Values.Select(x => x.NextUtc).Concat(_pool.Actors.Values.Select(x => x.NextUtc)).DefaultIfEmpty(DateTime.MaxValue).Min();
            if (due > at) { return; }
            foreach (var saved in _pool.Weather.Values.Where(x => x.NextUtc == due).OrderBy(x => x.Owner).ToArray())
            {
                var owner = Owner(saved.Owner); bool fighting = owner?.Map != null && owner.Map != Map.Internal && (actions ?? owner.NetState is { Running: true });
                if (!fighting)
                {
                    long missed = (at.Ticks - saved.NextUtc.Ticks) / TimeSpan.FromMilliseconds(240).Ticks + 1;
                    _pool = _pool with { Weather = _pool.Weather.SetItem(saved.Owner, saved with { NextUtc = saved.NextUtc.AddTicks(missed * TimeSpan.FromMilliseconds(240).Ticks) }) }; continue;
                }
                if (!DruidWeather.Outdoors(owner)) { _pool = _pool with { Weather = _pool.Weather.Remove(saved.Owner) }; continue; }
                var emitted = Emit(owner, saved);
                _pool = _pool with { Weather = _pool.Weather.SetItem(saved.Owner, emitted) };
            }
            foreach (var actor in _pool.Actors.Values.Where(x => x.NextUtc == due).OrderByDescending(x => x.Id).ToArray())
            { if (_pool.Actors.ContainsKey(actor.Id)) { Advance(actor, actions); } }
        }
    }
}
