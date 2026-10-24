// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public enum MonsterDifficulty { Normal, Nightmare, Hell }
public sealed record CreatureResistances(int? Physical = null, int? Magic = null, int? Fire = null,
    int? Lightning = null, int? Cold = null, int? Poison = null);
public sealed record CreatureAttack(int? MinDamage = null, int? MaxDamage = null, int? AttackRating = null,
    int? Range = null, int? CycleMs = null, int? ImpactMs = null, int? AfterAttackMs = null,
    int? Physical = null, int? Fire = null, int? Lightning = null, int? Cold = null, int? Poison = null,
    int? Magic = null, int? CriticalChance = null, string Skill = null, int? SkillLevel = null, string Missile = null);
public sealed record CreatureRecovery(bool Enabled = true, int Frames = 4, int Speed = 256, int Fhr = 0);
public sealed record CreatureMovement(int? WalkMs = null, int? RunMs = null, int? IdleMs = null);
public sealed record CreatureReaction(string Behavior = "Native", int? NoticeMs = null, int? ScanMs = null,
    int? ApproachChance = null, int? AwarenessRange = null, int? RetryMs = null, bool? Wander = null,
    int? AttackChance = null, int? Attack1Chance = null, int? AiDelayMs = null);
public sealed record CreatureRewards(int? Experience = null, string TreasureClass = null,
    bool Enabled = false, int? Level = null, string Reference = null, string Rationale = null);
public sealed record CreatureTargeting(string Policy = "Native", int? Threat = null, bool RetainValidTarget = true);
public sealed record CreatureTraits(string Family = "Native", bool? Undead = null, bool? Demon = null,
    bool? Boss = null, bool? Revivable = null, bool? LeavesCorpse = null, int? Size = null,
    int? Regeneration = null, int? DrainEffectiveness = null, int? ChillEffectiveness = null, int? BlockChance = null);

// Null means delegate to the creature's existing implementation, never zero the stat.
public sealed record CreatureVariant(bool Enabled, bool ApplyStats, int? Level, int? Life, int? Defense,
    CreatureResistances Resistances, CreatureAttack Attack1, CreatureAttack Attack2, CreatureAttack Spell,
    CreatureRecovery HitRecovery, CreatureMovement Movement, CreatureReaction Reaction,
    CreatureRewards Rewards, CreatureTraits Traits, CreatureTargeting Targeting,
    IReadOnlyDictionary<string, string> SourceStats, IReadOnlyDictionary<string, string> SourceModes);
public sealed record CreatureDefinition(string Type, string Parent, string Role, string Source,
    string Template, string Reference, string Status);
public sealed record CreatureSnapshot(string Type, MonsterDifficulty Difficulty, string Reference,
    string Status, int Level, int Life, int AttackRating, int Defense, CreatureResistances Resistances,
    CreatureVariant Profile);

public static class CreatureProfiles
{
    private static readonly Dictionary<string, CreatureDefinition> _definitions = new(StringComparer.Ordinal);
    private static readonly Dictionary<(string, MonsterDifficulty), CreatureVariant> _variants = new();
    private static CreatureVariant _native;
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static IReadOnlyCollection<CreatureDefinition> All => _definitions.Values;
    internal sealed class State
    {
        internal long RecoveryUntil = Core.TickCount;
        internal bool Initialized;
        internal NormalMonsterStats DifficultyMelee;
    }
    private static readonly ConditionalWeakTable<BaseCreature, State> _states = new();
    internal static State Session(BaseCreature creature) => _states.GetValue(creature, static _ => new State());

    public static void Load()
    {
        _definitions.Clear();
        _variants.Clear();
        string folder = Path.Join(Core.BaseDirectory, "Data", "Sanctuary", "Balance");
        var templates = JsonNode.Parse(File.ReadAllText(Path.Join(folder, "creature-templates.json"))).AsObject();
        var rewards = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var group in JsonNode.Parse(File.ReadAllText(Path.Join(folder, "creature-rewards.json")))["groups"].AsArray())
        {
            foreach (var type in group["types"].AsArray())
            {
                rewards.Add(type.GetValue<string>(), group["variants"]);
            }
        }
        var higher = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        var higherCatalog = JsonNode.Parse(File.ReadAllText(Path.Join(folder, "creature-difficulties.json")));
        if (higherCatalog["sourceCommit"].GetValue<string>() != "45112569deb9384738ccafe5c24ebbb71f41c7c9"
            || higherCatalog["players"].GetValue<int>() != 1 || higherCatalog["lifePolicy"].GetValue<string>() != "SourceMaximum")
        { throw new InvalidDataException("Unsupported higher-difficulty source rules."); }
        foreach (var group in higherCatalog["groups"].AsArray())
        {
            foreach (var type in group["types"].AsArray()) { higher.Add(type.GetValue<string>(), group["variants"]); }
        }
        _native = templates["defaults"]["Normal"].Deserialize<CreatureVariant>(_options);
        foreach (var node in JsonNode.Parse(File.ReadAllText(Path.Join(folder, "uo-creatures.json"))).AsArray())
        {
            var row = node.AsObject();
            // Variant overrides are separate from the identity record.
            var identity = row.DeepClone().AsObject();
            identity.Remove("variants");
            var definition = identity.Deserialize<CreatureDefinition>(_options);
            _definitions.Add(definition.Type, definition);
            var template = templates[definition.Template] ?? throw new InvalidDataException("Unknown creature template");
            if (!rewards.Remove(definition.Type, out var reward))
            {
                throw new InvalidDataException($"Missing creature reward policy: {definition.Type}");
            }
            foreach (var difficulty in Enum.GetValues<MonsterDifficulty>())
            {
                string key = difficulty.ToString();
                var merged = Merge(templates["defaults"]["Normal"], template["Normal"]);
                merged["rewards"] = Merge(merged["rewards"], reward["Normal"]);
                merged = Merge(merged, row["variants"]?["Normal"]);
                if (difficulty != MonsterDifficulty.Normal)
                {
                    merged = Merge(merged, templates["defaults"][key]);
                    merged = Merge(merged, template[key]);
                    merged["rewards"] = Merge(merged["rewards"], reward[key]);
                    if (higher.TryGetValue(definition.Type, out var numeric)) { merged = Merge(merged, numeric[key]); }
                    merged = Merge(merged, row["variants"]?[key]);
                }
                var variant = merged.Deserialize<CreatureVariant>(_options);
                Validate(variant);
                _variants.Add((definition.Type, difficulty), variant);
            }
        }
        if (rewards.Count != 0)
        {
            throw new InvalidDataException("Reward policy names an unknown creature type.");
        }
        // Fail on incomplete upstream coverage, without constructing NPCs or running their scripts.
        foreach (var type in typeof(BaseCreature).Assembly.GetTypes())
        {
            if (!type.IsAbstract && typeof(BaseCreature).IsAssignableFrom(type) && !_definitions.ContainsKey(type.FullName))
            {
                throw new InvalidDataException($"Missing creature profile: {type.FullName}");
            }
        }
    }
    private static JsonNode Merge(JsonNode basis, JsonNode patch)
    {
        var result = basis.DeepClone();
        if (patch is JsonObject fields)
        {
            foreach (var field in fields)
            {
                result[field.Key] = field.Value is JsonObject && result[field.Key] is JsonObject
                    ? Merge(result[field.Key], field.Value) : field.Value?.DeepClone();
            }
        }
        return result;
    }
    private static void Validate(CreatureVariant row)
    {
        if (row.Level is < 1 or > 99 || row.Life is < 1 or > 65000 || row.Defense is < 0
            || row.Rewards == null || row.Rewards.Experience is < 0
            || row.Rewards.Level is < 1 or > 99
            || row.Rewards.Enabled && (row.Rewards.Level == null || row.Rewards.Experience == null
                || string.IsNullOrWhiteSpace(row.Rewards.Reference) || string.IsNullOrWhiteSpace(row.Rewards.Rationale))
            || row.Enabled && row.ApplyStats && row.Rewards.Enabled && row.Level.HasValue && row.Level != row.Rewards.Level
            || row.HitRecovery == null || row.HitRecovery.Frames is < 1 or > 100
            || row.HitRecovery.Speed is < 1 or > 512 || row.HitRecovery.Fhr is < 0 or > 10000
            || CreatureHitRecovery.Frames(row.HitRecovery) * ActionTiming.FrameMs > 10000
            || row.Attack1.MinDamage is < 0 || row.Attack1.MaxDamage < row.Attack1.MinDamage
            || row.ApplyStats && row.Attack1.MinDamage.HasValue != row.Attack1.MaxDamage.HasValue
            || row.Attack1.AttackRating is < 1 || row.Attack1.Range is < 1 or > 18
            || row.Movement.WalkMs is < 40 || row.Movement.RunMs is < 40 || row.Movement.IdleMs is < 40
            || row.Reaction.NoticeMs is < 0 || row.Reaction.ScanMs is < 40
            || row.Reaction.ApproachChance is < 0 or > 100 || row.Reaction.RetryMs is < 40
            || row.Reaction.Behavior == "Species" && (row.Reaction.ApproachChance == null || row.Reaction.AwarenessRange == null
                || row.Reaction.RetryMs == null || row.Reaction.Wander == null)
            || row.Resistances.Physical is < -100 or > 200 || row.Resistances.Magic is < -100 or > 200
            || row.Resistances.Fire is < -100 or > 200 || row.Resistances.Cold is < -100 or > 200
            || row.Resistances.Lightning is < -100 or > 200 || row.Resistances.Poison is < -100 or > 200)
        {
            throw new InvalidDataException("Invalid common creature profile.");
        }
    }
    public static CreatureDefinition Definition(BaseCreature creature)
    {
        if(creature is AmazonDecoy){return new(creature.GetType().FullName,typeof(BaseCreature).FullName,"ClassSummon","Pinned Dopplezon entity","native","dopplezon","Explicit Decoy snapshot; full casting and source/native acceptance pending");}
        for (var type = creature.GetType(); type != null; type = type.BaseType)
        {
            if (_definitions.TryGetValue(type.FullName, out var row))
            {
                return row;
            }
        }
        return new(creature.GetType().FullName, creature.GetType().BaseType.FullName, "Native", "Custom subclass",
            "native", null, "Unmapped custom type; native values retained");
    }
    public static CreatureVariant Variant(BaseCreature creature, MonsterDifficulty? difficulty = null)
        => creature is EncounterCreature encounter ? encounter.EncounterProfile
            : _variants.TryGetValue((Definition(creature).Type, difficulty ?? MonsterVariants.Difficulty(creature)), out var row) ? row : _native;
    internal static bool ActiveVariant(Type type, MonsterDifficulty difficulty) =>
        _variants.TryGetValue((type.FullName, difficulty), out var row) && row is { Enabled: true, ApplyStats: true };
    public static CreatureVariant For(BaseCreature creature)
    {
        var row = Variant(creature);
        return row.Enabled ? row : _native;
    }

    // Explicit arguments preview variants; omitted arguments use the persisted spawn identity.
    public static CreatureSnapshot Snapshot(BaseCreature creature, MonsterDifficulty? difficulty = null)
    {
        difficulty ??= MonsterVariants.Difficulty(creature);
        var identity = Definition(creature);
        var row = Variant(creature, difficulty);
        var res = row.Resistances;
        return new(identity.Type, difficulty.Value, row.Rewards.Reference ?? identity.Reference, identity.Status,
            Level(creature, difficulty), row.Life ?? creature.HitsMax,
            row.Attack1.AttackRating ?? NativeAttack(creature), row.Defense ?? NativeDefense(creature),
            new(res.Physical ?? creature.PhysicalResistance, res.Magic ?? 0, res.Fire ?? creature.FireResistance,
                res.Lightning ?? creature.EnergyResistance, res.Cold ?? creature.ColdResistance,
                res.Poison ?? creature.PoisonResistance), row);
    }
    internal static int NativeLevel(BaseCreature mob) => mob is GraveyardUndead undead ? undead.CombatProfile.Level
        : mob is SkeletonGuardian pet ? pet.CombatProfile.Level : mob is BoneBarrierSegment barrier ? barrier.SourceLevel : mob is AmazonDecoy decoy ? decoy.Snapshot?.Level??1 : 1;
    internal static int NativeAttack(BaseCreature mob) => mob is GraveyardUndead undead ? undead.CombatProfile.AttackRating
        : mob is SkeletonGuardian pet ? pet.CombatProfile.AttackRating : Math.Max(5, mob.Dex * 2);
    internal static int NativeDefense(BaseCreature mob) => mob is GraveyardUndead undead ? undead.CombatProfile.Defense
        : mob is SkeletonGuardian pet ? pet.CombatProfile.Defense : mob is BoneBarrierSegment barrier ? barrier.SourceDefense : mob is AmazonDecoy decoy ? decoy.Snapshot?.Defense??0 : Math.Max(0, mob.Dex / 4 + (int)mob.ArmorRating);
    public static int Level(BaseCreature mob, MonsterDifficulty? difficulty = null)
    {
        if(PaladinConversion.Life(mob) is{} conversion){return conversion.Level;}
        var row = Variant(mob, difficulty);
        return row is { Enabled: true, ApplyStats: true, Level: { } level } ? level
            : row.Rewards is { Enabled: true, Level: { } rewardLevel } ? rewardLevel : NativeLevel(mob);
    }
    // Reward policy has its own opt-in: a reviewed level/XP mapping does not
    // activate an unrelated combat proposal.
    public static int Experience(BaseCreature creature, MonsterDifficulty? difficulty = null)
        => Variant(creature, difficulty).Rewards is { Enabled: true, Experience: { } xp } ? xp : 0;
    public static int AttackRating(BaseCreature mob) => For(mob) is { ApplyStats: true, Attack1.AttackRating: { } rating }
        ? rating : NativeAttack(mob);
    public static int Defense(BaseCreature mob) => For(mob) is { ApplyStats: true, Defense: { } defense } ? defense : NativeDefense(mob);

    // Explicit opt-in prevents an unreviewed mapping from silently rebalancing a native creature.
    internal static void Initialize(BaseCreature creature)
    {
        var state = Session(creature);
        if (state.Initialized)
        {
            return;
        }
        state.Initialized = true;
        var row = For(creature);
        if (!row.Enabled || !row.ApplyStats || creature.Controlled || creature.Summoned || creature is BaseVendor)
        {
            return;
        }
        if (row.Life is { } life)
        {
            int before = creature.Hits, maximum = creature.HitsMax;
            creature.SetHits(life);
            creature.Hits = Math.Clamp((int)((long)before * life / Math.Max(1, maximum)), 0, life);
        }
        if (row.Attack1.MinDamage is { } min && row.Attack1.MaxDamage is { } max)
        {
            creature.SetDamage(min, max);
        }
        var res = row.Resistances;
        SetResistance(creature, ResistanceType.Physical, res.Physical);
        SetResistance(creature, ResistanceType.Fire, res.Fire);
        SetResistance(creature, ResistanceType.Cold, res.Cold);
        SetResistance(creature, ResistanceType.Energy, res.Lightning);
        SetResistance(creature, ResistanceType.Poison, res.Poison);
        if (row.Movement.WalkMs is { } walk)
        {
            creature.ActiveMoveSpeed = walk / 1000.0;
        }
        if (row.Movement.IdleMs is { } idle)
        {
            creature.PassiveMoveSpeed = idle / 1000.0;
        }
        if (row.Attack1.Physical is { } physical && row.Attack1.Fire is { } fire)
        {
            creature.SetDamageType(ResistanceType.Physical, physical);
            creature.SetDamageType(ResistanceType.Fire, fire);
            creature.SetDamageType(ResistanceType.Energy, row.Attack1.Lightning ?? 0);
            creature.SetDamageType(ResistanceType.Cold, row.Attack1.Cold ?? 0);
            creature.SetDamageType(ResistanceType.Poison, row.Attack1.Poison ?? 0);
        }
    }
    private static void SetResistance(BaseCreature creature, ResistanceType type, int? value)
    {
        if (value is { } resistance)
        {
            creature.SetResistance(type, resistance);
        }
    }
}
