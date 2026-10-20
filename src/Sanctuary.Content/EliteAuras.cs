// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class EliteAuras
{
    private sealed class Debuff(Mobile owner)
    {
        internal Mobile Owner = owner;
        internal ResistanceMod Physical, Fire, Cold, Lightning;
        internal int Amp, Conviction;
    }
    private static readonly HashSet<EncounterCreature> _emitters = new();
    private static readonly Dictionary<Serial, Debuff> _debuffs = new();
    private static readonly List<Serial> _keys = new();
    private static readonly List<EncounterCreature> _sources = new();
    private static long _nextPulse = Core.TickCount;
    internal static EliteCurseProfiles Curses { get; private set; }
    [CallPriority(100)]
    public static void Configure()
    {
        Curses = new EliteCurseProfiles();
        ClassCombatHooks.PlayerDied += (player, _) => { Curses.Clear(player.Serial); Refresh(player); };
    }
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200), Tick);
    internal static void Observe(EncounterCreature source)
    { if (source is { Deleted: false, Alive: true } && source.Aura != EliteAura.None) { _emitters.Add(source); } }
    internal static void Forget(EncounterCreature source)
    {
        _emitters.Remove(source);
        // Remove an aura immediately; the next pulse must not retain a dead or deleted emitter.
        _keys.Clear(); _keys.AddRange(_debuffs.Keys);
        foreach (var serial in _keys) { if (_debuffs.TryGetValue(serial, out var state)) { Refresh(state.Owner); } }
    }
    internal static int SkillLevel(EncounterCreature source) => Math.Max(1, CreatureProfiles.Level(source) / (source.Aura switch
    { EliteAura.Might or EliteAura.HolyFire => 6, EliteAura.BlessedAim => 5, EliteAura.HolyFreeze => 7, _ => 8 }));
    internal static int Radius(EliteAura aura, int level)
    {
        // LoD subtiles are mapped to three per UO tile, bounded by the project's sight range.
        int range = aura switch { EliteAura.Might or EliteAura.BlessedAim => 16 + 2 * (level - 1),
            EliteAura.Conviction => 20, EliteAura.Fanaticism => 11 + level - 1, _ => 6 + level - 1 };
        return Math.Clamp((range + 2) / 3, 1, 18);
    }
    internal static int Level(Mobile target, EliteAura aura, bool hostile)
    {
        if (target?.Map == null || target.Map == Map.Internal || target.Deleted || !target.Alive) { return 0; }
        int result = 0;
        foreach (var source in target.Map.GetMobilesInRange<EncounterCreature>(target.Location, 18))
        {
            if (source is not { Deleted: false, Alive: true } || source.Controlled || source.Summoned || source.ReturningHome || source.Aura != aura) { continue; }
            bool enemy = target is PlayerMobile || target is BaseCreature { Controlled: true } or BaseCreature { Summoned: true };
            if (enemy != hostile || !enemy && target is not EncounterCreature || Math.Abs(source.Z - target.Z) >= 16) { continue; }
            int level = SkillLevel(source);
            if (source.InRange(target, Radius(aura, level)) && source.InLOS(target) && source.CanSee(target)) { result = Math.Max(result, level); }
        }
        return result;
    }
    internal static int Diminish(int level, int minimum, int maximum) => minimum + 110 * level * (maximum - minimum) / (100 * (level + 6));
    internal static int Speed(Mobile target)
    { int level = Level(target, EliteAura.Fanaticism, false); return level == 0 ? 0 : Diminish(level, 10, 40); }
    internal static int Slow(Mobile target)
    { int level = Level(target, EliteAura.HolyFreeze, true); return level == 0 ? 0 : Diminish(level, 25, 60); }
    internal static int DamageBonus(Mobile target)
    {
        int might = Level(target, EliteAura.Might, false), fanaticism = Level(target, EliteAura.Fanaticism, false);
        return (might == 0 ? 0 : 40 + 10 * (might - 1)) + (fanaticism == 0 ? 0 : (50 + 17 * (fanaticism - 1)) / 2);
    }
    internal static int AttackRating(Mobile target, int rating)
    {
        int aim = Level(target, EliteAura.BlessedAim, false), fanaticism = Level(target, EliteAura.Fanaticism, false);
        int bonus = (aim == 0 ? 0 : 75 + 15 * (aim - 1)) + (fanaticism == 0 ? 0 : 40 + 5 * (fanaticism - 1));
        return checked(rating * (100 + bonus) / 100);
    }
    internal static int Defense(Mobile target, int defense)
    { int level = Level(target, EliteAura.Conviction, true); return level == 0 ? defense : defense * Math.Max(0, 100 - Diminish(level, 40, 100)) / 100; }
    internal static int SkillDamage(int basis, int[] increments, int level)
    {
        int result = basis;
        for (int l = 2; l <= level; l++) { result += increments[l <= 8 ? 0 : l <= 16 ? 1 : l <= 22 ? 2 : l <= 28 ? 3 : 4]; }
        return result;
    }
    internal static int ElementDamage(EliteAura aura, int level, Random random, int multiplier = 1) => aura switch
    {
        EliteAura.HolyFire => EliteCombat.Roll(SkillDamage(2, [1, 2, 3, 5, 7], level) * multiplier / 2, SkillDamage(6, [1, 2, 3, 5, 7], level) * multiplier / 2, random),
        EliteAura.HolyFreeze => EliteCombat.Roll(SkillDamage(2, [1, 2, 3, 4, 5], level) * multiplier, SkillDamage(3, [1, 2, 3, 4, 5], level) * multiplier, random),
        EliteAura.HolyShock => EliteCombat.Roll(multiplier, SkillDamage(10, [6, 8, 10, 12, 15], level) * multiplier, random),
        _ => 0
    };
    internal static int PlayerResistance(PlayerMobile target, ResistanceType type, int basis)
    {
        if (!_debuffs.TryGetValue(target.Serial, out var state)) { return basis; }
        int offset = type switch { ResistanceType.Physical => state.Physical?.Offset ?? 0,
            ResistanceType.Fire => state.Fire?.Offset ?? 0, ResistanceType.Cold => state.Cold?.Offset ?? 0,
            ResistanceType.Energy => state.Lightning?.Offset ?? 0, _ => 0 };
        return Math.Clamp(basis + offset, -100, 75);
    }
    internal static void Amplify(Mobile target, TimeSpan duration)
    {
        if (target is not { Deleted: false, Alive: true } || target is EncounterCreature { Grade: EncounterGrade.Champion, Champion: ChampionKind.Possessed }) { return; }
        Curses.Apply(target.Serial, TimedEffects.Now.Add(duration)); Refresh(target);
        target.FixedParticles(0x374A, 10, 15, 5028, EffectLayer.Waist);
    }
    private static void Remove(Mobile target, ref ResistanceMod mod)
    { if (mod != null) { target.RemoveResistanceMod(mod); mod = null; } }
    internal static void Refresh(Mobile target)
    {
        if (target == null) { return; }
        _debuffs.TryGetValue(target.Serial, out var state);
        bool alive = !target.Deleted && target.Alive;
        int amp = alive && Curses.Active(target.Serial, TimedEffects.Now) ? 100 : 0;
        int conviction = alive ? Level(target, EliteAura.Conviction, true) : 0;
        if (state != null && state.Amp == amp && state.Conviction == conviction) { return; }
        if (state != null)
        {
            Remove(target, ref state.Physical); Remove(target, ref state.Fire); Remove(target, ref state.Cold); Remove(target, ref state.Lightning);
            _debuffs.Remove(target.Serial); state.Owner = null;
        }
        if (!alive) { Curses.Clear(target.Serial); return; }
        if (amp == 0 && conviction == 0) { return; }
        state = new Debuff(target) { Amp = amp, Conviction = conviction };
        if (amp > 0)
        {
            state.Physical = new ResistanceMod(ResistanceType.Physical, "SanctuaryAmplifyDamage", target.PhysicalResistance >= 100 ? -amp / 5 : -amp);
            target.AddResistanceMod(state.Physical);
        }
        if (conviction > 0)
        {
            int amount = Math.Min(150, 30 + 5 * (conviction - 1));
            state.Fire = new ResistanceMod(ResistanceType.Fire, "SanctuaryConvictionFire", target.FireResistance >= 100 ? -amount / 5 : -amount);
            state.Cold = new ResistanceMod(ResistanceType.Cold, "SanctuaryConvictionCold", target.ColdResistance >= 100 ? -amount / 5 : -amount);
            state.Lightning = new ResistanceMod(ResistanceType.Energy, "SanctuaryConvictionLightning", target.EnergyResistance >= 100 ? -amount / 5 : -amount);
            target.AddResistanceMod(state.Fire); target.AddResistanceMod(state.Cold); target.AddResistanceMod(state.Lightning);
        }
        _debuffs.Add(target.Serial, state);
    }
    internal static void Tick() => Tick(Core.TickCount);
    internal static void Tick(long now)
    {
        _sources.Clear(); _sources.AddRange(_emitters);
        bool pulse = now - _nextPulse >= 0;
        if (pulse) { _nextPulse = now + EncounterProfiles.Settings.AuraPulseMs; }
        foreach (var source in _sources)
        {
            if (source is not { Deleted: false, Alive: true } || source.Map == null || source.Map == Map.Internal)
            { _emitters.Remove(source); continue; }
            int level = SkillLevel(source), radius = Radius(source.Aura, level);
            foreach (var target in source.Map.GetMobilesInRange<Mobile>(source.Location, radius))
            {
                if (!EliteCombat.Enemy(source, target, radius)) { continue; }
                if (source.Aura == EliteAura.Conviction) { Refresh(target); }
                if (!pulse || source.Aura is not (EliteAura.HolyFire or EliteAura.HolyFreeze or EliteAura.HolyShock)
                    || Level(target, source.Aura, true) != level || !HighestEmitter(source, target, source.Aura, level)) { continue; }
                int amount = ElementDamage(source.Aura, level, Random.Shared);
                EliteCombat.Damage(source, target, fire: source.Aura == EliteAura.HolyFire ? amount : 0,
                    cold: source.Aura == EliteAura.HolyFreeze ? amount : 0, lightning: source.Aura == EliteAura.HolyShock ? amount : 0);
            }
        }
        _sources.Clear();
        _keys.Clear(); _keys.AddRange(_debuffs.Keys);
        foreach (var serial in _keys) { if (_debuffs.TryGetValue(serial, out var state)) { Refresh(state.Owner); } }
        _keys.Clear();
    }
    private static bool HighestEmitter(EncounterCreature source, Mobile target, EliteAura aura, int level)
    {
        foreach (var other in target.Map.GetMobilesInRange<EncounterCreature>(target.Location, 18))
        {
            if (other == source || other.Deleted || !other.Alive || other.Aura != aura || other.ReturningHome
                || other.Controlled || other.Summoned || !other.InRange(target, Radius(aura, SkillLevel(other))) || !other.InLOS(target)) { continue; }
            int candidate = SkillLevel(other);
            if (candidate > level || candidate == level && other.Serial.Value < source.Serial.Value) { return false; }
        }
        return true;
    }
}

public sealed class EliteCurseProfiles : GenericPersistence
{
    private readonly Dictionary<Serial, DateTime> _curses = new();
    private KeyValuePair<Serial, DateTime>[] _snapshot = [];
    public EliteCurseProfiles() : base("SanctuaryEliteCurses", 126) { }
    internal void Apply(Serial serial, DateTime until) => _curses[serial] = until;
    internal bool Active(Serial serial, DateTime now)
    {
        if (!_curses.TryGetValue(serial, out var until)) { return false; }
        if (until > now) { return true; }
        _curses.Remove(serial); return false;
    }
    internal void Clear(Serial serial) => _curses.Remove(serial);
    internal DateTime Expires(Serial serial) => _curses.GetValueOrDefault(serial);
    public override void Serialize()
    {
        var rows = new List<KeyValuePair<Serial, DateTime>>();
        foreach (var row in _curses)
        { if (row.Value > TimedEffects.Now && World.FindMobile(row.Key) is { Deleted: false, Alive: true }) { rows.Add(row); } }
        _snapshot = rows.ToArray(); base.Serialize();
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_snapshot.Length);
        foreach (var row in _snapshot) { writer.Write(row.Key.Value); writer.Write(row.Value); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown elite curse version."); }
        int count = reader.ReadInt(); if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid elite curse count."); }
        _curses.Clear();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt(); var until = reader.ReadDateTime();
            if (serial.Value == 0) { throw new InvalidDataException("Invalid curse owner."); }
            _curses.Add(serial, until);
        }
    }
    public override void PostDeserialize()
    {
        foreach (var row in new List<KeyValuePair<Serial, DateTime>>(_curses))
        { if (World.FindMobile(row.Key) is { Deleted: false, Alive: true } mobile) { EliteAuras.Refresh(mobile); } }
    }
}
