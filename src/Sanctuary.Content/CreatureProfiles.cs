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
    private const int DestardResistanceCap = 85;
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
        internal int InitializedPlayers;
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
        if(creature is EncounterCreature encounter){return new(creature.GetType().FullName,typeof(GraveyardUndead).FullName,"DungeonEncounter",
            "Saved dungeon encounter profile","native",encounter.Dungeon.Reference,"Explicit encounter source identity; native archetype combat retained");}
        if(creature is AssassinShadowBody shadow){return new(creature.GetType().FullName,typeof(BaseCreature).FullName,"ClassSummon","Pinned Shadow resource body","native",shadow.SourceMonsterId,"Parked original initialization/equipment/resources;NPC skills/events,AI and learned casting remain gated");}
        if(creature is AmazonValkyrieBody){return new(creature.GetType().FullName,typeof(BaseCreature).FullName,"ClassSummon","Pinned Valkyrie supplied-birth body","native","valkyrie","Prepared saved supplied life/stats/ownership; equipment,passive skills,AI and learned casting remain gated");}
        if(creature is WarlockDefilerBody){return new(creature.GetType().FullName,typeof(BaseCreature).FullName,"ClassSummon","Pinned warputriddefiler vital entity","native","warputriddefiler","Prepared Gazer life/ownership; offense,Health Link,GenericPet and learned casting remain gated");}
        if(creature is WarlockTaintedBody){return new(creature.GetType().FullName,typeof(BaseCreature).FullName,"ClassSummon","Pinned warbighead entity","native","warbighead","Prepared saved vulnerable Tainted life/ownership and qualified fire hit; GenericPet controller,actual skill release and learned casting remain gated");}
        if(creature is WarlockGoatmanBody){return new(creature.GetType().FullName,typeof(BaseCreature).FullName,"ClassSummon","Pinned wargoatman entity","native","wargoatman","Prepared saved vulnerable Goatman life/ownership; GenericPet world/combat and learned casting remain gated");}
        if(creature is IronGolemBody){return new(creature.GetType().FullName,typeof(BaseCreature).FullName,"ClassSummon","Pinned Iron Golem stat body","native","irongolem","Prepared saved numeric birth/body; source initializer,AI,attack/events and learned casting remain unfinished");}
        if(creature is DruidOakBody spirit){return new(creature.GetType().FullName,typeof(BaseCreature).FullName,"ClassSummon","Pinned spirit entity","native",spirit.SourceMonsterId,"Saved vulnerable Totem body; individual aura/casting and exact-current acceptance scope is recorded separately");}
        if(creature is DruidCreeperBody){return new(creature.GetType().FullName,typeof(BaseCreature).FullName,"ClassSummon","Pinned plaguepoppy entity","native","plaguepoppy","Prepared saved noRatio life/ownership only; world state, mats and learned casting pending");}
        if(creature is DruidRecyclerBody recycler){return new(creature.GetType().FullName,typeof(BaseCreature).FullName,"ClassSummon","Pinned recycler vine entity","native",recycler.SourceMonsterId,"Prepared saved recycler life/ownership; corpse controller,exposure and learned acceptance remain separate");}
        if(creature is DruidSpiritWolfBody){return new(creature.GetType().FullName,typeof(BaseCreature).FullName,"ClassSummon","Pinned Spirit Wolf entity","native","spiritwolf","Saved life/movement/cold hit and learned casting; modern full parity pending");}
        if(creature is DruidGrizzlyBody){return new(creature.GetType().FullName,typeof(BaseCreature).FullName,"ClassSummon","Pinned druidbear entity","native","druidbear","Prepared saved vulnerable Grizzly life/ownership; AI112world,BearSmite and learned casting pending");}
        if(creature is DruidDireWolfBody){return new(creature.GetType().FullName,typeof(BaseCreature).FullName,"ClassSummon","Pinned Fenris entity","native","fenris","Prepared saved vulnerable Fenris world/rage/physical combat; learned casting and full current parity pending");}
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
    private static CreatureVariant SummonVariant(SkeletonGuardian body,string key,CreatureResistances resistances,MonsterDifficulty? difficulty,bool demon=true,string family=null)
        => _native with{Enabled=true,ApplyStats=false,Resistances=resistances,
            Traits=new(family??(demon?"Demon":"Valkyrie"),false,demon,false,false,false,
                GenericPetSecondaryTables.Table.Row(GenericPetSearchTables.SourceId(key)??throw new NotSupportedException("Missing demon source identity"))?.SizeX
                    ??throw new NotSupportedException("Missing demon model footprint"),
                NecromancerSummons.Source(key,"DamageRegen"),NecromancerSummons.Source(key,"Drain",difficulty??body.SummonDifficulty),
                Math.Abs(NecromancerSummons.Source(key,"coldeffect",difficulty??body.SummonDifficulty)),NecromancerSummons.Source(key,"ToBlock",difficulty??body.SummonDifficulty)),
            SourceStats=new Dictionary<string,string>
            {
                {"hcIdx",(GenericPetSearchTables.SourceId(key)??throw new NotSupportedException("Missing demon source identity")).ToString()},
                {"Velocity",NecromancerSummons.Source(key,"Velocity").ToString()}
            },
            // Reuse the pinned current return identity instead of inheriting
            // the UO appearance's recovery/block/knockback capabilities.
            SourceModes=new Dictionary<string,string>
            {
                {"mGH",((NativeMonsterReturn.Identity(key).ModeFlags>>3)&1).ToString()},
                {"mBL",((NativeMonsterReturn.Identity(key).ModeFlags>>6)&1).ToString()},
                {"mKB",((NativeMonsterReturn.Identity(key).ModeFlags>>13)&1).ToString()}
            },HitRecovery=new(false)};
    public static CreatureVariant Variant(BaseCreature creature, MonsterDifficulty? difficulty = null)
    {
        var variant = ResolveVariant(creature, difficulty);
        if (creature is not EncounterCreature && DungeonPlayerScaling.IsWildDungeonCreature(creature))
        {
            const int players = DungeonPlayerScaling.Players;
            variant = variant with
            {
                Life = variant.Life is { } life ? Math.Min(65000, DungeonPlayerScaling.Scale(life, players)) : null,
                Rewards = variant.Rewards.Experience is { } xp
                    ? variant.Rewards with { Experience = DungeonPlayerScaling.Scale(xp, players) } : variant.Rewards
            };
        }
        if (creature.Controlled || creature.Summoned
            || creature.Map != Map.Trammel || !creature.Region.IsPartOf("Destard"))
            return variant;

        var res = variant.Resistances;
        if (res.Physical.GetValueOrDefault() <= DestardResistanceCap && res.Magic.GetValueOrDefault() <= DestardResistanceCap
            && res.Fire.GetValueOrDefault() <= DestardResistanceCap && res.Lightning.GetValueOrDefault() <= DestardResistanceCap
            && res.Cold.GetValueOrDefault() <= DestardResistanceCap && res.Poison.GetValueOrDefault() <= DestardResistanceCap)
            return variant;
        return variant with { Resistances = CapDestardResistances(res) };
    }

    private static CreatureResistances CapDestardResistances(CreatureResistances res)
    {
        static int? Cap(int? value) => value > DestardResistanceCap ? DestardResistanceCap : value;
        return new(Cap(res.Physical), Cap(res.Magic), Cap(res.Fire), Cap(res.Lightning), Cap(res.Cold), Cap(res.Poison));
    }

    internal static void ApplyDestardResistances(BaseCreature creature)
    {
        // Native spawners call this on the internal map, before the Destard region can affect Variant.
        var profile = ResolveVariant(creature, null);
        if (profile.ApplyStats && !creature.Controlled && !creature.Summoned)
            ApplyResistances(creature, CapDestardResistances(profile.Resistances));
    }

    private static CreatureVariant ResolveVariant(BaseCreature creature, MonsterDifficulty? difficulty)
        => creature is AssassinShadowBody shadow ? SummonVariant(shadow,shadow.SourceMonsterId,shadow.SourceResistances(difficulty),difficulty,false,"Shadow")
            : creature is AmazonValkyrieBody valkyrie ? SummonVariant(valkyrie,"valkyrie",valkyrie.SourceResistances(difficulty),difficulty,false)
            : creature is WarlockDefilerBody defiler ? SummonVariant(defiler,"warputriddefiler",defiler.SourceResistances(difficulty),difficulty)
            : creature is WarlockTaintedBody tainted ? SummonVariant(tainted,"warbighead",tainted.SourceResistances(difficulty),difficulty)
            : creature is WarlockGoatmanBody goat ? SummonVariant(goat,"wargoatman",goat.SourceResistances(difficulty),difficulty)
            : creature is IronGolemBody iron ? _native with{Enabled=true,ApplyStats=false,
            Resistances=new(Magic:NecromancerSummons.Source("irongolem","ResMa",difficulty??iron.SummonDifficulty)),
            Traits=new("Golem",false,false,false,false,false,2,3,
                NecromancerSummons.Source("irongolem","Drain",difficulty??iron.SummonDifficulty),
                Math.Abs(NecromancerSummons.Source("irongolem","coldeffect",difficulty??iron.SummonDifficulty)),
                NecromancerSummons.Source("irongolem","ToBlock",difficulty??iron.SummonDifficulty)),
            HitRecovery=new(false)}
            : creature is DruidOakBody oak ? _native with{Enabled=true,ApplyStats=false,
            Resistances=new(Magic:NecromancerSummons.Source(oak.SourceMonsterId,"ResMa",difficulty??oak.SummonDifficulty)),
            Traits=new("Spirit",false,false,false,false,false,2,3,
                NecromancerSummons.Source(oak.SourceMonsterId,"Drain",difficulty??oak.SummonDifficulty),
                Math.Abs(NecromancerSummons.Source(oak.SourceMonsterId,"coldeffect",difficulty??oak.SummonDifficulty)),0),
            HitRecovery=new(false)}
            : creature is DruidCreeperBody creeper ? _native with{Enabled=true,ApplyStats=false,
            Resistances=new(0,0,0,0,0,0),
            Traits=new("Vine",false,false,false,false,false,0,3,
                NecromancerSummons.Source("plaguepoppy","Drain",difficulty??creeper.SummonDifficulty),
                Math.Abs(NecromancerSummons.Source("plaguepoppy","coldeffect",difficulty??creeper.SummonDifficulty)),0),
            HitRecovery=new(false)}
            : creature is DruidRecyclerBody recycler ? _native with{Enabled=true,ApplyStats=false,
            Resistances=new(0,0,0,0,0,0),
            Traits=new("Vine",false,false,false,false,false,0,3,
                NecromancerSummons.Source(recycler.SourceMonsterId,"Drain",difficulty??recycler.SummonDifficulty),
                Math.Abs(NecromancerSummons.Source(recycler.SourceMonsterId,"coldeffect",difficulty??recycler.SummonDifficulty)),0),
            HitRecovery=new(false)}
            : creature is DruidSpiritWolfBody wolf ? _native with{Enabled=true,ApplyStats=false,
            Traits=new("Wolf",false,false,false,false,false,2,4,
                NecromancerSummons.Source("spiritwolf","Drain",difficulty??wolf.SummonDifficulty),
                Math.Abs(NecromancerSummons.Source("spiritwolf","coldeffect",difficulty??wolf.SummonDifficulty)),0),
            HitRecovery=new(true,7,256)}
            : creature is DruidGrizzlyBody bear ? _native with{Enabled=true,ApplyStats=false,
            Traits=new("Bear",false,false,false,false,false,NecromancerSummons.Source("druidbear-model","SizeX"),
                NecromancerSummons.Source("druidbear","DamageRegen"),NecromancerSummons.Source("druidbear","Drain",difficulty??bear.SummonDifficulty),
                Math.Abs(NecromancerSummons.Source("druidbear","coldeffect",difficulty??bear.SummonDifficulty)),0),
            HitRecovery=new(true,NecromancerSummons.Source("druidbear-animation","GHFrames"),NecromancerSummons.Source("druidbear-animation","GHSpeed"))}
            : creature is DruidDireWolfBody dire ? _native with{Enabled=true,ApplyStats=false,
            Traits=new("Wolf",false,false,false,false,false,2,4,
                NecromancerSummons.Source("fenris","Drain",difficulty??dire.SummonDifficulty),
                Math.Abs(NecromancerSummons.Source("fenris","coldeffect",difficulty??dire.SummonDifficulty)),0),
            HitRecovery=new(true,7,256)}
            : creature is EncounterCreature encounter ? encounter.ProfileFor(difficulty)
            : _variants.TryGetValue((Definition(creature).Type, difficulty ?? MonsterVariants.Difficulty(creature)), out var row) ? row : _native;
    internal static int WorldChestSpawnLevel(Type type, MonsterDifficulty difficulty)
    {
        if (CreatureGold.Policy(type, difficulty) is not { Eligible: true })
        {
            return 0;
        }

        return _variants.TryGetValue((type.FullName, difficulty), out var row)
            ? ConfiguredLevel(row) ?? 1
            : 1;
    }

    internal static bool ActiveVariant(Type type, MonsterDifficulty difficulty) =>
        _variants.TryGetValue((type.FullName, difficulty), out var row) && row is { Enabled: true, ApplyStats: true };
    internal static bool TryVariant(string definition,MonsterDifficulty difficulty,out CreatureVariant profile)
        =>_variants.TryGetValue((definition,difficulty),out profile);
    public static CreatureVariant For(BaseCreature creature)
    {
        var row = Variant(creature);
        return row.Enabled ? row : _native;
    }

    internal static void AddImmunityProperties(BaseCreature creature, IPropertyList list)
    {
        if (creature.Controlled || creature.Summoned || creature.IsInvulnerable || creature.FightMode == FightMode.None)
            return;

        var res = For(creature).Resistances;
        if ((res.Physical ?? creature.PhysicalResistance) >= 100) list.Add("Immune to Physical");
        if (NamedElements.MagicResistance(creature) >= 100) list.Add("Immune to Magic");
        if ((res.Fire ?? creature.FireResistance) >= 100) list.Add("Immune to Fire");
        if ((res.Lightning ?? creature.EnergyResistance) >= 100) list.Add("Immune to Lightning");
        if ((res.Cold ?? creature.ColdResistance) >= 100) list.Add("Immune to Cold");
        if ((res.Poison ?? creature.PoisonResistance) >= 100) list.Add("Immune to Poison");
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
        : mob is AssassinSentryBody sentry ? sentry.Prepared?.Creation?.Level??1
        : mob is BladeSentinelBody blade ? blade.Prepared?.Creation?.Level??1 : mob is SkeletonGuardian pet ? pet.CombatProfile.Level : mob is BoneBarrierSegment barrier ? barrier.SourceLevel : mob is AmazonDecoy decoy ? decoy.Snapshot?.Level??1 : 1;
    internal static int NativeAttack(BaseCreature mob) => mob is GraveyardUndead undead ? undead.CombatProfile.AttackRating
        : mob is AssassinSentryBody sentry ? sentry.Prepared?.Creation?.Attack??0
        : mob is BladeSentinelBody blade ? blade.Prepared?.Creation?.Attack??0 : mob is SkeletonGuardian pet ? pet.CombatProfile.AttackRating
        : mob is IClassCompanion build && MercenaryCare.CompanionLevel(mob).HasValue ? CompanionBuild.AttackRating(mob, build.CombatClass) : Math.Max(5, mob.Dex * 2);
    internal static int NativeDefense(BaseCreature mob) => mob is GraveyardUndead undead ? undead.CombatProfile.Defense
        : mob is AssassinSentryBody sentry ? sentry.Prepared?.Creation?.Defense??0
        : mob is BladeSentinelBody blade ? blade.Prepared?.Creation?.Defense??0 : mob is SkeletonGuardian pet ? pet.CombatProfile.Defense : mob is BoneBarrierSegment barrier ? barrier.SourceDefense : mob is AmazonDecoy decoy ? decoy.Snapshot?.Defense??0
        : MercenaryCare.CompanionLevel(mob).HasValue ? CompanionBuild.Defense(mob) : Math.Max(0, mob.Dex / 4 + (int)mob.ArmorRating);
    public static int Level(BaseCreature mob, MonsterDifficulty? difficulty = null)
    {
        if (MercenaryCare.CompanionLevel(mob) is { } companionLevel) { return companionLevel; }
        if(PaladinConversion.Life(mob) is{} conversion){return conversion.Level;}
        if(ReviveCombat.Level(mob) is{} revived){return revived;}
        return ConfiguredLevel(Variant(mob, difficulty)) ?? NativeLevel(mob);
    }

    private static int? ConfiguredLevel(CreatureVariant row) =>
        row is { Enabled: true, ApplyStats: true, Level: { } level } ? level
            : row.Rewards is { Enabled: true } ? row.Rewards.Level : null;

    // Reward policy has its own opt-in: a reviewed level/XP mapping does not
    // activate an unrelated combat proposal.
    public static int Experience(BaseCreature creature, MonsterDifficulty? difficulty = null)
        => Variant(creature, difficulty).Rewards is { Enabled: true, Experience: { } xp } ? xp : 0;
    public static int AttackRating(BaseCreature mob) => (ReviveEquipmentCombat.AttackBasis(mob)??(For(mob) is { ApplyStats: true, Attack1.AttackRating: { } rating }
        ? rating : NativeAttack(mob)))+BoundDemonHosts.Attack(mob);
    public static int Defense(BaseCreature mob) => checked((ReviveEquipmentCombat.DefenseBasis(mob)??(For(mob) is { ApplyStats: true, Defense: { } defense } ? defense : NativeDefense(mob)))+EngorgeEffects.Armor(mob)+BoundDemonHosts.Armor(mob));

    // Explicit opt-in prevents an unreviewed mapping from silently rebalancing a native creature.
    internal static void Initialize(BaseCreature creature,bool preserveLoadedLife=false)
    {
        var state = Session(creature);
        int players = DungeonPlayerScaling.For(creature);
        if (state.Initialized && state.InitializedPlayers == players)
        {
            return;
        }
        state.Initialized = true;
        state.InitializedPlayers = players;
        var row = For(creature);
        if (!row.Enabled || !row.ApplyStats || creature.Controlled || creature.Summoned || creature is BaseVendor)
        {
            return;
        }
        if (row.Life is { } life)
        {
            int before = creature.Hits, maximum = World.Loading && players == DungeonPlayerScaling.Players
                && creature.HitsMaxSeed > 0 ? creature.HitsMaxSeed : creature.HitsMax;
            creature.SetHits(life);
            // During world load the resource hook intentionally exposes a
            // temporary 65000 maximum. A saved encounter already contains its
            // injured life; do not treat that load guard as a prior stat pool.
            // Let the restored resource states establish their real maximum
            // before ordinary native clamps, including an active life buff.
            creature.Hits = preserveLoadedLife?before:Math.Clamp((int)((long)before * life / Math.Max(1, maximum)), 0, life);
        }
        if (row.Attack1.MinDamage is { } min && row.Attack1.MaxDamage is { } max)
        {
            creature.SetDamage(min, max);
        }
        ApplyResistances(creature, row.Resistances);
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

    private static void ApplyResistances(BaseCreature creature, CreatureResistances res)
    {
        SetResistance(creature, ResistanceType.Physical, res.Physical);
        SetResistance(creature, ResistanceType.Fire, res.Fire);
        SetResistance(creature, ResistanceType.Cold, res.Cold);
        SetResistance(creature, ResistanceType.Energy, res.Lightning);
        SetResistance(creature, ResistanceType.Poison, res.Poison);
    }

    internal static void RestoreTamedMountLife(BaseMount mount)
    {
        var state = Session(mount);
        if (!state.Initialized || state.InitializedPlayers != DungeonPlayerScaling.Players
            || ResolveVariant(mount, null) is not { ApplyStats: true, Life: { } life })
            return;

        int before = mount.Hits, maximum = mount.HitsMax;
        mount.SetHits(life);
        mount.Hits = Math.Clamp((int)((long)before * mount.HitsMax / Math.Max(1, maximum)), 1, mount.HitsMax);
        state.InitializedPlayers = 1;
    }

    private static void SetResistance(BaseCreature creature, ResistanceType type, int? value)
    {
        if (value is { } resistance)
        {
            creature.SetResistance(type, resistance);
        }
    }
}
