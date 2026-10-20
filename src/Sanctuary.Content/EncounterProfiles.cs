// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server;
using Server.Commands;
using Server.Mobiles;
using Server.Targeting;

namespace Sanctuary;

public enum EncounterGrade { Ordinary, Champion, Unique, SuperUnique, Minion }
public enum ChampionKind { Champion, Ghostly, Fanatic, Berserker, Possessed }
public enum EliteModifier { AuraEnchanted, Cursed, ColdEnchanted, ExtraFast, FireEnchanted, LightningEnchanted,
    MagicResistant, ManaBurn, Multishot, SpectralHit, StoneSkin, ExtraStrong, Teleport }
public enum EliteAura { None, Might, HolyFire, BlessedAim, HolyFreeze, HolyShock, Conviction, Fanaticism }
public sealed record DungeonProfile(string Id, string Family, string Name, MonsterArchetype Archetype, string Reference,
    int MinimumLevel, int MaximumLevel, int Body, int Sound, CreatureResistances Resistances);
public sealed record NamedEncounter(string Id, string Name, string Profile, int BaseLevel, EliteModifier[] Modifiers, EliteAura Aura);
public sealed record EncounterSettings(int SchemaVersion, DungeonProfile[] Profiles, NamedEncounter[] Named,
    int TeleportCooldownMs, int TeleportRadius, int TeleportHealPercent, int AuraPulseMs, int MaximumAddedImmunities);

public static class EncounterProfiles
{
    private static readonly Dictionary<string, DungeonProfile> _profiles = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, NamedEncounter> _named = new(StringComparer.Ordinal);
    public static EncounterSettings Settings { get; private set; }
    public static IReadOnlyCollection<DungeonProfile> All => _profiles.Values;
    public static IReadOnlyCollection<NamedEncounter> Named => _named.Values;
    public static DungeonProfile Profile(string id) => _profiles.TryGetValue(id ?? "", out var row) ? row
        : TreasureHunts.Profile(id) ?? throw new ArgumentException("Unknown dungeon encounter profile.", nameof(id));
    public static void Load()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new JsonStringEnumConverter());
        Settings = JsonSerializer.Deserialize<EncounterSettings>(File.ReadAllText(Path.Join(Core.BaseDirectory,
            "Data", "Sanctuary", "Balance", "encounters.json")), options);
        if (Settings is not { SchemaVersion: 1, Profiles.Length: 16, Named.Length: 3, MaximumAddedImmunities: 2 }
            || Settings.TeleportCooldownMs is < 1000 or > 60000 || Settings.TeleportRadius is < 2 or > 12
            || Settings.TeleportHealPercent is < 0 or > 50 || Settings.AuraPulseMs is < 1000 or > 10000)
        { throw new InvalidDataException("Invalid dungeon encounter settings."); }
        _profiles.Clear(); _named.Clear();
        foreach (var row in Settings.Profiles)
        {
            if (row.MinimumLevel is < 60 or > 90 || row.MinimumLevel % 10 != 0 || row.MaximumLevel != row.MinimumLevel + 9
                || !Enum.IsDefined(row.Archetype) || string.IsNullOrWhiteSpace(row.Name) || string.IsNullOrWhiteSpace(row.Reference))
            { throw new InvalidDataException("Invalid bounded dungeon profile."); }
            foreach (int? resistance in new[] { row.Resistances.Physical, row.Resistances.Magic, row.Resistances.Fire,
                row.Resistances.Cold, row.Resistances.Lightning, row.Resistances.Poison })
            { if (resistance is null or < -100 or > 200) { throw new InvalidDataException("Missing dungeon resistance."); } }
            _profiles.Add(row.Id, row);
        }
        foreach (var row in Settings.Named)
        {
            var profile = Profile(row.Profile);
            if (row.BaseLevel < profile.MinimumLevel || row.BaseLevel > profile.MaximumLevel || row.BaseLevel + 3 > 99
                || row.Modifiers is not { Length: >= 1 and <= 3 } || !Legal(row.Modifiers, profile, MonsterDifficulty.Hell)
                || row.Modifiers.Contains(EliteModifier.AuraEnchanted) != (row.Aura != EliteAura.None) || !Enum.IsDefined(row.Aura))
            { throw new InvalidDataException("Invalid named dungeon encounter."); }
            _named.Add(row.Id, row);
        }
    }
    private static bool Contains<T>(this T[] values, T value) => Array.IndexOf(values, value) >= 0;
    internal static bool Legal(EliteModifier[] modifiers, DungeonProfile profile, MonsterDifficulty difficulty)
    {
        var seen = new HashSet<EliteModifier>();
        foreach (var modifier in modifiers)
        {
            if (!Enum.IsDefined(modifier) || !seen.Add(modifier)
                || modifier == EliteModifier.MagicResistant && difficulty == MonsterDifficulty.Normal
                || modifier == EliteModifier.Multishot && profile.Archetype is not (MonsterArchetype.Ranged or MonsterArchetype.Caster))
            { return false; }
        }
        return true;
    }
    internal static EliteModifier[] RollModifiers(DungeonProfile profile, MonsterDifficulty difficulty, Random random)
    {
        var pool = new List<EliteModifier>();
        foreach (var modifier in Enum.GetValues<EliteModifier>())
        { if (Legal([modifier], profile, difficulty)) { pool.Add(modifier); } }
        var result = new EliteModifier[1 + (int)difficulty];
        for (int i = 0; i < result.Length; i++)
        { int pick = random.Next(pool.Count); result[i] = pool[pick]; pool.RemoveAt(pick); }
        return result;
    }
    internal static EliteAura RollAura(int level, Random random) => (EliteAura)random.Next(1, level < 20 ? 7 : 8) switch
    { EliteAura.HolyShock when level < 20 => EliteAura.Fanaticism, var aura => aura };
    public static int Level(int baseLevel, EncounterGrade grade) => Math.Min(99, baseLevel + (grade switch
    { EncounterGrade.Champion => 2, EncounterGrade.Unique or EncounterGrade.SuperUnique or EncounterGrade.Minion => 3, _ => 0 }));
    public static MouldGrade MouldGrade(EncounterGrade grade) => grade switch
    { EncounterGrade.Champion => Sanctuary.MouldGrade.Champion, EncounterGrade.Unique => Sanctuary.MouldGrade.Unique,
        EncounterGrade.SuperUnique => Sanctuary.MouldGrade.SuperUnique, _ => Sanctuary.MouldGrade.Ordinary };
    internal static int LifePermille(EncounterGrade grade, ChampionKind champion, MonsterDifficulty difficulty)
    {
        int index = (int)difficulty;
        return grade switch
        {
            EncounterGrade.Ordinary => 1000,
            EncounterGrade.Minion => 2000 - index * 250,
            EncounterGrade.Unique or EncounterGrade.SuperUnique => new[] { 4000, 3000, 2000 }[index],
            _ => champion switch { ChampionKind.Berserker => new[] { 750, 625, 500 }[index],
                ChampionKind.Possessed => 6000 - index * 1000, _ => 3000 - index * 500 }
        };
    }
    // Resist bonuses are evaluated in the saved modifier order, including all six channels.
    internal static int[] Resistances(CreatureResistances source, ChampionKind? champion, EliteModifier[] modifiers)
    {
        int[] values = [source.Physical.Value, source.Magic.Value, source.Fire.Value, source.Lightning.Value, source.Cold.Value, source.Poison.Value];
        if (champion == ChampionKind.Ghostly) { values[0] = 80; }
        void Add(int channel, int amount)
        {
            int immune = 0; foreach (int value in values) { if (value >= 100) { immune++; } }
            if (immune >= 2 && (values[channel] >= 100 || values[channel] + amount >= 100)) { return; }
            values[channel] = Math.Min(200, values[channel] + amount);
        }
        foreach (var modifier in modifiers)
        {
            switch (modifier)
            {
                case EliteModifier.ColdEnchanted: Add(4, 75); break;
                case EliteModifier.FireEnchanted: Add(2, 75); break;
                case EliteModifier.LightningEnchanted: Add(3, 75); break;
                case EliteModifier.MagicResistant:
                    foreach (int channel in new[] { 4, 2, 3 }) { if (values[channel] < 100) { Add(channel, 40); } } break;
                case EliteModifier.ManaBurn: Add(1, 20); break;
                case EliteModifier.SpectralHit:
                    foreach (int channel in new[] { 4, 2, 3 }) { if (values[channel] < 75) { Add(channel, 40); } } break;
                case EliteModifier.StoneSkin: Add(0, 50); break;
            }
        }
        return values;
    }
    public static IReadOnlyList<EncounterCreature> SpawnRandomElite(string id, MonsterDifficulty difficulty, Point3D location, Map map, Random random = null)
    {
        random ??= Random.Shared;
        return Spawn(id, difficulty, random.Next(5) == 0 ? EncounterGrade.Champion : EncounterGrade.Unique, location, map, random);
    }
    public static IReadOnlyList<EncounterCreature> Spawn(string id, MonsterDifficulty difficulty, EncounterGrade grade,
        Point3D location, Map map, Random random = null)
    {
        random ??= Random.Shared;
        if (!Enum.IsDefined(difficulty) || !Enum.IsDefined(grade) || grade == EncounterGrade.Minion || map == null || map == Map.Internal)
        { throw new ArgumentException("Choose a supported encounter grade, difficulty and map."); }
        _named.TryGetValue(id, out var named);
        var profile = named != null ? Profile(named.Profile) : Profile(id);
        if (named != null) { grade = EncounterGrade.SuperUnique; }
        if (grade == EncounterGrade.SuperUnique && named == null) { throw new ArgumentException("Super-uniques require a named profile."); }
        int count = grade switch { EncounterGrade.Unique => 6, EncounterGrade.Champion => random.Next(2, 5), _ => 1 };
        var points = new List<Point3D>();
        for (int radius = 0; radius <= 6 && points.Count < count; radius++)
        {
            for (int x = -radius; x <= radius && points.Count < count; x++)
            {
                for (int y = -radius; y <= radius && points.Count < count; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius) { continue; }
                    var point = new Point3D(location.X + x, location.Y + y, location.Z);
                    if (Free(map, point) && ClearSight(map, location, point)) { points.Add(point); }
                }
            }
        }
        if (points.Count != count) { throw new InvalidOperationException("The complete encounter needs more clear, connected ground."); }
        int baseLevel = named?.BaseLevel ?? random.Next(profile.MinimumLevel, profile.MaximumLevel + 1);
        var modifiers = named?.Modifiers ?? (grade == EncounterGrade.Unique ? RollModifiers(profile, difficulty, random) : []);
        var aura = named?.Aura ?? (Array.IndexOf(modifiers, EliteModifier.AuraEnchanted) >= 0 ? RollAura(Level(baseLevel, grade), random) : EliteAura.None);
        var result = new List<EncounterCreature>();
        try
        {
            for (int i = 0; i < count; i++)
            {
                var memberGrade = grade == EncounterGrade.Unique && i > 0 ? EncounterGrade.Minion : grade;
                var champion = grade == EncounterGrade.Champion && i == count - 1 ? (ChampionKind)random.Next(5) : ChampionKind.Champion;
                var creature = new EncounterCreature(); result.Add(creature);
                creature.Assign(profile, baseLevel, difficulty, memberGrade, champion, modifiers,
                    memberGrade == EncounterGrade.Minion ? EliteAura.None : aura, named?.Name, random);
                creature.GroupLeader = i == 0 ? creature.Serial.Value : result[0].Serial.Value;
                creature.Home = points[i]; creature.HomeMap = map;
                creature.MoveToWorld(points[i], map); EliteAuras.Observe(creature);
            }
            return result;
        }
        catch { foreach (var creature in result) { creature.Delete(); } throw; }
    }
    internal static bool ClearSight(Map map, Point3D origin, Point3D target) => map.LineOfSight(
        new Point3D(origin.X, origin.Y, origin.Z + 14), new Point3D(target.X, target.Y, target.Z + 14));
    internal static bool Free(Map map, Point3D point)
    {
        if (!map.CanSpawnMobile(point)) { return false; }
        foreach (var mobile in map.GetMobilesInRange<Mobile>(point, 0))
        { if (!mobile.Deleted && mobile.Alive && Math.Abs(mobile.Z - point.Z) < 16) { return false; } }
        return true;
    }
    public static void Configure()
    {
        CommandSystem.Register("SpawnEncounter", AccessLevel.GameMaster, args =>
        {
            if (args.Length != 3 || !Enum.TryParse<MonsterDifficulty>(args.GetString(1), true, out var difficulty)
                || !Enum.TryParse<EncounterGrade>(args.GetString(2), true, out var grade)
                || !Enum.IsDefined(difficulty) || !Enum.IsDefined(grade) || grade == EncounterGrade.Minion
                || !_profiles.ContainsKey(args.GetString(0)) && !_named.ContainsKey(args.GetString(0)))
            { args.Mobile.SendMessage("Usage: [SpawnEncounter profile-or-name Normal|Nightmare|Hell Ordinary|Champion|Unique|SuperUnique"); return; }
            args.Mobile.Target = new EncounterTarget(args.GetString(0), difficulty, grade);
        });
    }
    private sealed class EncounterTarget(string id, MonsterDifficulty difficulty, EncounterGrade grade) : Target(12, true, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from.AccessLevel < AccessLevel.GameMaster || targeted is not IPoint3D point || !from.InRange(point, 12)
                || !ClearSight(from.Map, from.Location, new Point3D(point))) { return; }
            try { Spawn(id, difficulty, grade, new Point3D(point), from.Map); }
            catch (ArgumentException) { from.SendMessage("That encounter profile and grade do not match."); }
            catch (InvalidOperationException) { from.SendMessage("There is not enough clear ground for that encounter."); }
        }
    }
}
