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

public sealed record HurricaneState(uint Owner, int Rank, ulong Seed, ushort InitParam,
    DateTime StartedUtc, DateTime NextUtc, DateTime UntilUtc);

public sealed class HurricaneProfiles : GenericPersistence
{
    public HurricaneProfiles() : base("SanctuaryHurricane", 171) { }
    public override void Serialize(IGenericWriter writer)
    { writer.Write(0); writer.Write(JsonSerializer.Serialize(DruidHurricane.Snapshot())); }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown Hurricane version"); }
        DruidHurricane.Restore(JsonSerializer.Deserialize<HurricaneState[]>(reader.ReadString()));
    }
}

// Skill250: do124 self state -> state144/server145. One fresh copied packet
// every20frames, not a travelling missile or a channel. Exact modern global
// seed/event ordering, native room/LOS and unrepresented targets remain open.
public static class DruidHurricane
{
    private static ImmutableDictionary<uint, HurricaneState> _states = ImmutableDictionary<uint, HurricaneState>.Empty;
    internal static Func<int> SeedLow = () => Utility.Random(int.MaxValue);
    public static HurricaneProfiles Profiles { get; private set; }
    public static void Configure() => Profiles = new();
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40), () => Tick(TimedEffects.Now));
    private static PlayerMobile Owner(uint serial) => World.FindMobile((Serial)serial) is PlayerMobile { Alive: true, Deleted: false } p
        && ClassCreation.Profiles.TryGet(p.Serial, out var profile) && profile.Class == DiabloClass.Druid ? p : null;
    public static HurricaneState State(Mobile owner) => owner != null && Owner(owner.Serial.Value) == owner ? _states.GetValueOrDefault(owner.Serial.Value) : null;
    public static void Clear(Mobile owner) { if (owner != null) { _states = _states.Remove(owner.Serial.Value); } }
    internal static HurricaneState[] Snapshot() => _states.Values.OrderBy(x => x.Owner).ToArray();
    internal static void Restore(HurricaneState[] rows)
    {
        if (rows == null || rows.Length > 1000000) { throw new InvalidDataException("Missing Hurricane state"); }
        var next = ImmutableDictionary<uint, HurricaneState>.Empty;
        foreach (var row in rows)
        {
            Validate(row);
            if (next.ContainsKey(row.Owner)) { throw new InvalidDataException("Duplicate Hurricane owner"); }
            next = next.Add(row.Owner, row);
        }
        _states = next;
    }
    private static void Validate(HurricaneState row)
    {
        if (row == null || !((Serial)row.Owner).IsMobile || row.Rank is < 1 or > 255
            || row.StartedUtc.Kind != DateTimeKind.Utc || row.NextUtc.Kind != DateTimeKind.Utc || row.UntilUtc.Kind != DateTimeKind.Utc
            || row.UntilUtc <= row.StartedUtc || row.UntilUtc - row.StartedUtc > TimeSpan.FromMilliseconds(1250 * 40)
            || row.NextUtc <= row.StartedUtc || (row.NextUtc.Ticks - row.StartedUtc.Ticks) % TimeSpan.FromMilliseconds(800).Ticks != 0)
        { throw new InvalidDataException("Invalid saved Hurricane state"); }
        long frames = (row.UntilUtc.Ticks - row.StartedUtc.Ticks) / TimeSpan.FromMilliseconds(40).Ticks;
        if ((row.UntilUtc.Ticks - row.StartedUtc.Ticks) % TimeSpan.FromMilliseconds(40).Ticks != 0
            || frames < 250 || (frames - 250) % 50 != 0 || row.NextUtc.Ticks - row.UntilUtc.Ticks > TimeSpan.FromMilliseconds(800).Ticks)
        { throw new InvalidDataException("Invalid Hurricane event clock"); }
    }
    private static void Routes(SkillValues values)
    {
        var row = SkillMechanics.For(CombatAbility.Hurricane);
        if (values.Ability != CombatAbility.Hurricane || row.SourceId != 250 || row.Number("srvdofunc") != 124
            || row.Text("aurastate") != "hurricane" || row.Number("aurafilter") != 42883 || row.Text("anim") != "SC"
            || row.Number("Param4") != 20 || row.Number("ResultFlags") != 1 || row.Number("HitFlags") != 2
            || row.Number("localdelay") != 0 || row.Number("SrcDam") != 0 || row.Text("EType") != "cold"
            || row.Number("intown") != 0 || values.Mana256 != 30 * 256 || values.Eval("aurarangecalc") != 9 || values.DurationFrames != 50)
        { throw new NotSupportedException("Unsupported Hurricane route"); }
    }
    public static bool Begin(PlayerMobile owner, Serial selected, Point3D point, long now)
        => SkillCombat.Begin(owner, CombatAbility.Hurricane, selected, point, now, (p, _, _, _) => DruidWeather.Outdoors(p));
    public static bool Cast(PlayerMobile owner, SkillValues values)
    {
        if (!DruidCombat.Available(owner, CombatAbility.Hurricane) || !DruidWeather.Outdoors(owner)) { return false; }
        HurricaneState state;
        try { state = Prepare(owner, values, TimedEffects.Now); }
        catch (NotSupportedException) { owner.SendMessage(0x22, "Hurricane cannot be used in this context yet."); return false; }
        if (!NecromancerEffects.Pay(owner, values.Mana256)) { return false; }
        _states = _states.SetItem(state.Owner, state);
        owner.FixedEffect(0x37CC, 10, 15, 0x480, 0); owner.PlaySound(0x655); // Identified wind-cast/audio placeholders.
        return true;
    }
    internal static HurricaneState Prepare(PlayerMobile owner, SkillValues values, DateTime at)
    {
        Routes(values);
        if (owner == null || Owner(owner.Serial.Value) != owner || values.Rank is < 1 or > 255 || at.Kind != DateTimeKind.Utc)
        { throw new NotSupportedException("Unsupported Hurricane supplier/clock"); }
        var start = DruidWeather.Prepare(owner, values, at, SeedLow);
        var state = new HurricaneState(owner.Serial.Value, values.Rank, start.Seed, start.Param, at, start.NextUtc, start.UntilUtc);
        Validate(state); return state;
    }
    internal static void Create(PlayerMobile owner, SkillValues values, DateTime at)
    { var state = Prepare(owner, values, at); _states = _states.SetItem(state.Owner, state); }
    internal static bool Recipient(PlayerMobile owner, BaseCreature target, int radius)
        => SourceDamageAreas.Recipient(owner,target,owner.Map.MapID,owner.X*2,owner.Y*2,owner.Z,radius,true);
    private static HurricaneState Pulse(PlayerMobile owner, HurricaneState state)
    {
        var values = SkillMechanics.Values(owner, CombatAbility.Hurricane, state.Rank); Routes(values);
        var mods = DruidFirestorm.Modifiers(owner);
        var ends = DruidFirestorm.Endpoints(values, mods.ColdMastery, SlowMissilePayload.Normal);
        ulong seed = state.Seed;
        int raw = WeaponMissileDamage.RollValue(ends.Min, ends.Max, 0, n => (int)SourceRandom.Limited(ref seed, n));
        var packet = DruidMoltenBoulder.Potion(mods, new(0, 0, raw / 256.0, 0, 0));
        int radius = (int)values.Eval("aurarangecalc"); var targets = new List<BaseCreature>();
        foreach (var target in owner.Map.GetMobilesInRange<BaseCreature>(owner.Location, (int)Math.Ceiling(SkillGeometry.Tiles(radius))))
        { if (Recipient(owner, target, radius)) { targets.Add(target); } }
        foreach (var target in targets)
        {
            if (!Recipient(owner, target, radius) || ClassAvoidance.Missile(target)) { continue; }
            owner.DoHarmful(target, true);
            var parts = SkillCombat.MitigatePrepared(owner, target, packet);
            ColdStates.Apply(owner, target, values.DurationFrames, TimedEffects.TickAt(state.NextUtc));
            SkillHealth.Damage(owner, target, NamedElements.To256(parts.Total));
            // ResultFlags1 has no generic GETHIT4, stun or knockback request.
            target.FixedEffect(0x37CC, 3, 4, 0x480, 0); // Identified periodic wind/cold marker.
        }
        Effects.SendLocationEffect(owner.Location, owner.Map, 0x37CC, 8, 5, 0x480, 0); // Weather marker; not its mechanical clock.
        return state with { Seed = seed };
    }
    internal static void Tick(DateTime at, bool? actions = null)
    {
        if (at.Kind != DateTimeKind.Utc) { throw new ArgumentException("Hurricane clock must be UTC"); }
        foreach (var saved in _states.Values.OrderBy(x => x.Owner).ToArray())
        {
            if (!_states.TryGetValue(saved.Owner, out var state)) { continue; }
            var owner = Owner(saved.Owner);
            // Explicit native equal-deadline policy: expiry wins. Complete
            // modern remove-state/active-event ordering remains unverified.
            if (owner == null || state.UntilUtc <= at) { _states = _states.Remove(state.Owner); continue; }
            if (at < state.NextUtc) { continue; }
            bool fighting = owner.Map != null && owner.Map != Map.Internal && (actions ?? owner.NetState is { Running: true });
            if (!fighting)
            {
                // Offline time ages the state without damage or replaying
                // missed attacks on reconnect. Lifetime is never extended.
                long missed = (at.Ticks - state.NextUtc.Ticks) / TimeSpan.FromMilliseconds(800).Ticks + 1;
                _states = _states.SetItem(state.Owner, state with { NextUtc = state.NextUtc.AddTicks(missed * TimeSpan.FromMilliseconds(800).Ticks) });
                continue;
            }
            if (!DruidWeather.Outdoors(owner)) { _states = _states.Remove(state.Owner); continue; }
            while (state.NextUtc <= at && state.NextUtc < state.UntilUtc)
            { state = Pulse(owner, state); state = state with { NextUtc = state.NextUtc.AddMilliseconds(800) }; }
            _states = _states.SetItem(state.Owner, state);
        }
    }
}
