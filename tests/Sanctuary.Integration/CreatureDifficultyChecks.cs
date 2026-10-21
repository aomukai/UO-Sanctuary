using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Commands;
using Server.Targeting;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CreatureDifficultyChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CreatureDifficultyChecks));
    private static readonly Point3D _point = new(5580, 1150, 0);
    private static string Filepath => Path.Join(Core.BaseDirectory, "difficulty-checks.json");
    private sealed record Saved(uint Creature, MonsterDifficulty Difficulty, int Hits, int Maximum, int Level, int Experience, int Fire, int Damage);
    private sealed record Manifest(Saved[] Creatures, uint Corpse, uint Loot, string Gear);
    public static void Configure()
    { Register("difficulty-check", Check); Register("difficulty-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Disposable difficulty identity, combat, treasure and persistence checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", name); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    private static void Require(bool value, string message) => GearDropChecks.Require(value, message);
    private static Type Resolve(string type) => typeof(BaseCreature).Assembly.GetType(type) ?? typeof(GraveyardUndead).Assembly.GetType(type);
    private static BaseCreature Create(Type type, MonsterDifficulty difficulty)
    {
        var creature = type.CreateInstance<BaseCreature>();
        try { MonsterVariants.Assign(creature, difficulty); return creature; }
        catch (Exception error) { throw new InvalidOperationException($"{type.FullName}: map={creature.Map}, alive={creature.Alive}, controlled={creature.Controlled}, summoned={creature.Summoned}, noAwards={creature.NoKillAwards}", error); }
    }
    private static void Reject(BaseCreature creature, MonsterDifficulty difficulty)
    {
        bool rejected = false;
        try { MonsterVariants.Assign(creature, difficulty); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Invalid difficulty reassignment was accepted");
    }
    internal static int GearSeed(MonsterDifficulty difficulty, int find, GearTier tier)
    {
        string[] allowed = ["Hatchet", "Dagger", "Longsword", "Broadsword", "Axe", "TwoHandedAxe", "WarAxe", "Scimitar", "Cutlass"];
        int level = CreatureProfilesLevel(difficulty);
        for (int seed = 0; seed < 100000; seed++)
        {
            var random = new Random(seed);
            var basis = GearDrops.SelectBase(CreatureGold.Select(typeof(Skeleton), random, difficulty), random);
            if (basis == null || basis.Tier != tier || !basis.Variants.Any(v => allowed.Contains(v.Type))) { continue; }
            var gear = GearDrops.Generate(basis, level, find, random);
            if (gear == null) { continue; }
            var state = ClassGear.State(gear);
            bool good = allowed.Contains(gear.GetType().Name) && state.Roll.Rarity == GearRarity.Magic
                && !state.Roll.Affixes.Any(a => a.Modifiers.Any(m => m.Code == "mag%"));
            gear.Delete();
            if (good) { return seed; }
        }
        throw new InvalidOperationException($"No deterministic supported {difficulty}/{tier} equipment seed");
    }
    private static int CreatureProfilesLevel(MonsterDifficulty difficulty)
    { var creature = new Skeleton(); int level = CreatureProfiles.Level(creature, difficulty); creature.Delete(); return level; }
    private static Saved Snapshot(BaseCreature creature) => new(creature.Serial.Value, MonsterVariants.Difficulty(creature), creature.Hits,
        creature.HitsMax, CreatureProfiles.Level(creature), CreatureProfiles.Experience(creature), creature.FireResistance, creature.DamageMin);
    private static void Check()
    {
        var saved = new List<Saved>();
        var player = GearDropChecks.Player(); player.MoveToWorld(_point, Map.Trammel);
        foreach (var difficulty in new[] { MonsterDifficulty.Nightmare, MonsterDifficulty.Hell })
        {
            int covered = 0;
            foreach (var definition in CreatureProfiles.All)
            {
                var type = Resolve(definition.Type);
                if (CreatureGold.Policy(type, difficulty) is not { Eligible: true })
                { Require(!MonsterVariants.Supported(type, difficulty), "Excluded variant became spawnable"); continue; }
                covered++;
                if (!MonsterVariants.CanSpawn(type, difficulty)) { continue; }
                var creature = Create(type, difficulty);
                var row = CreatureProfiles.For(creature);
                Require(MonsterVariants.Difficulty(creature) == difficulty && row.Enabled && row.ApplyStats
                    && creature.HitsMax == row.Life && CreatureProfiles.Level(creature) == row.Rewards.Level
                    && CreatureProfiles.Experience(creature) == row.Rewards.Experience
                    && CreatureProfiles.AttackRating(creature) == row.Attack1.AttackRating
                    && CreatureProfiles.Defense(creature) == row.Defense && creature.DamageMin == row.Attack1.MinDamage
                    && creature.BaseFireResistance == row.Resistances.Fire && creature.BasePhysicalResistance == row.Resistances.Physical,
                    $"Difficulty combat/reward identity differs: {definition.Type}; life {creature.HitsMax}/{row.Life}, AR {CreatureProfiles.AttackRating(creature)}/{row.Attack1.AttackRating}, damage {creature.DamageMin}/{row.Attack1.MinDamage}, fire {creature.BaseFireResistance}/{row.Resistances.Fire}, physical {creature.BasePhysicalResistance}/{row.Resistances.Physical}");
                Reject(creature, MonsterDifficulty.Hell);
                creature.Delete();
            }
            Require(covered == 324, "Difficulty roster coverage changed");
            Rewards(difficulty);
            var keeper = (GraveyardSkeleton)Create(typeof(GraveyardSkeleton), difficulty);
            keeper.Frozen = keeper.CantWalk = true; keeper.MoveToWorld(_point, Map.Trammel); keeper.AIObject.AITimer.Stop();
            keeper.Hits = keeper.HitsMax - 37; saved.Add(Snapshot(keeper));
            Reject(keeper, MonsterDifficulty.Normal);
            var undead = (GraveyardSkeleton)Create(typeof(GraveyardSkeleton), difficulty);
            Require(undead.NormalStats.Attack1.MinDamage == CreatureProfiles.For(undead).Attack1.MinDamage,
                "Project undead still used Normal melee damage"); undead.Delete();
            var dragon = (Dragon)Create(typeof(Dragon), difficulty);
            if (difficulty == MonsterDifficulty.Hell)
            {
                Require(dragon.FireResistance >= 100, "Native resistance cap erased Hell immunity");
                dragon.Frozen = true; dragon.MoveToWorld(new Point3D(_point.X + 3, _point.Y, 0), Map.Trammel);
                dragon.AIObject.AITimer.Stop(); int before = dragon.Hits;
                AOS.Damage(dragon, player, 50, 0, 100, 0, 0, 0);
                Require(dragon.Hits == before, "Real fire damage bypassed Hell immunity");
                AOS.Damage(dragon, player, 20, 50, 50, 0, 0, 0);
                Require(dragon.Hits == before - 20 * 50 * Math.Clamp(100 - dragon.PhysicalResistance, 0, 200) / 10000,
                    "Immune fire component cancelled physical damage");
            }
            dragon.Delete();
            int seed = GearSeed(difficulty, 0, difficulty == MonsterDifficulty.Hell ? GearTier.Elite : GearTier.Exceptional);
            var victim = (Skeleton)Create(typeof(Skeleton), difficulty);
            var corpse = GearDropChecks.Body(victim, player, _point);
            RewardLife.Process(victim, corpse, new Random(seed));
            Require(corpse.Items.Count == 1 && ClassGear.State(corpse.Items[0]) is { Identified: false } state
                && state.Tier == (difficulty == MonsterDifficulty.Hell ? GearTier.Elite : GearTier.Exceptional)
                && state.Roll.ItemLevel == CreatureProfiles.Level(victim), "Higher loot used Normal base/ilvl/table");
            var loot = corpse.Items[0]; string original = JsonSerializer.Serialize(ClassGear.State(loot));
            RewardLife.Process(victim, corpse, new Random(seed));
            Require(corpse.Items.Count == 1 && original == JsonSerializer.Serialize(ClassGear.State(loot)), "Repeated higher callback paid twice");
            if (difficulty == MonsterDifficulty.Hell)
            {
                File.WriteAllText(Filepath, JsonSerializer.Serialize(new Manifest(saved.ToArray(), corpse.Serial.Value, loot.Serial.Value, original)));
            }
            else { corpse.Delete(); }
            victim.Delete();
        }
        var normal = new Skeleton(); Require(MonsterVariants.Difficulty(normal) == MonsterDifficulty.Normal
            && !CreatureProfiles.For(normal).ApplyStats, "Unassigned creature changed difficulty");
        normal.MoveToWorld(_point, Map.Trammel); Reject(normal, MonsterDifficulty.Hell); normal.Delete();
        var vendor = new Provisioner(); Reject(vendor, MonsterDifficulty.Hell); vendor.Delete();
        var pet = new Skeleton { Controlled = true }; Reject(pet, MonsterDifficulty.Hell); pet.Delete();
        NativeDeath(player); SpawnCommand(player);
    }
    private static void SpawnCommand(PlayerMobile player)
    {
        var point = CombatPractice.FindFloor(Map.Felucca, 1387, 1495);
        player.MoveToWorld(new Point3D(point.X - 2, point.Y, point.Z), Map.Felucca);
        Require(MonsterVariants.CanSpawn(typeof(Skeleton), MonsterDifficulty.Hell), "Supported native factory missing");
        player.AccessLevel = AccessLevel.GameMaster;
        CommandSystem.Handle(player, "[SpawnVariant Hell Skeleton");
        Require(player.Target != null, "Staff spawn command did not request a target");
        player.Target.Invoke(player, new LandTarget(point, Map.Felucca));
        Skeleton spawned = null;
        foreach (var creature in Map.Felucca.GetMobilesInRange<Skeleton>(point, 0))
        { if (MonsterVariants.Difficulty(creature) == MonsterDifficulty.Hell) { spawned = creature; } }
        Require(spawned != null && spawned.HitsMax == CreatureProfiles.For(spawned).Life, "Native spawn target did not create a Hell creature");
        spawned.Delete(); player.AccessLevel = AccessLevel.Player;
    }
    private static long Xp(PlayerMobile player)
    { ClassCreation.Profiles.TryGet(player.Serial, out var row); return row.Experience; }
    private static void Rewards(MonsterDifficulty difficulty)
    {
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = "DifficultyXp" }; player.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer); ClassCreation.Profiles.AdvanceLevel(player, 50);
        player.MoveToWorld(_point, Map.Trammel);
        var pet = new Skeleton(); pet.SetControlMaster(player); pet.Controlled = true;
        var victim = (Skeleton)Create(typeof(Skeleton), difficulty);
        int seed = 0;
        while (CreatureGold.Select(typeof(Skeleton), new Random(seed), difficulty) != "gld") { seed++; }
        int level = CreatureProfiles.Level(victim), expected = MonsterExperience.Award(CreatureProfiles.Experience(victim), level, 50);
        long before = Xp(player);
        var corpse = GearDropChecks.Body(victim, pet, _point);
        RewardLife.Process(victim, corpse, new Random(seed));
        Require(Xp(player) == before + expected && corpse.Items.OfType<Gold>().Sum(g => g.Amount)
            == CreatureGold.Roll(typeof(Skeleton), level, 0, new Random(seed), difficulty), "Pet kill lost higher XP/gold or selected Normal rules");
        corpse.Delete(); victim.Delete();
        foreach (var quality in new[] { DropQuality.Unique, DropQuality.Set })
        {
            int chosen = -1;
            for (int candidate = 0; candidate < 100000 && chosen < 0; candidate++)
            {
                var random = new Random(candidate);
                var basis = GearDrops.SelectBase(CreatureGold.Select(typeof(Skeleton), random, difficulty), random);
                if (basis == null || basis.Variants.Length == 0) { continue; }
                random.Next(basis.Variants.Length);
                if (GearDrops.Quality(basis, level, 0, random) == quality) { chosen = candidate; }
            }
            Require(chosen >= 0, "Reserved source quality was unreachable");
            victim = (Skeleton)Create(typeof(Skeleton), difficulty); corpse = GearDropChecks.Body(victim, player, _point);
            RewardLife.Process(victim, corpse, new Random(chosen));
            var rarity = quality == DropQuality.Unique ? GearRarity.Unique : GearRarity.Set;
            Require(corpse.Items.Count <= 1 && corpse.SanctuaryNoRewards
                && corpse.Items.All(i => ClassGear.State(i) is { NamedItemId: not null } state
                    && state.Roll.Rarity == rarity && NamedGear.Available(state.NamedItemId)),
                "Named higher quality rerolled or fell back to another rarity");
            corpse.Delete(); victim.Delete();
        }
        victim = (Skeleton)Create(typeof(Skeleton), difficulty); victim.NoKillAwards = true; corpse = GearDropChecks.Body(victim, pet, _point);
        before = Xp(player); RewardLife.Process(victim, corpse, new Random(seed));
        Require(Xp(player) == before && corpse.Items.Count == 0 && corpse.SanctuaryNoRewards, "Higher no-awards victim paid rewards");
        corpse.Delete(); victim.Delete(); pet.Delete(); player.Delete();
    }
    private static void NativeDeath(PlayerMobile player)
    {
        var victim = (Skeleton)Create(typeof(Skeleton), MonsterDifficulty.Hell);
        int seed = GearSeed(MonsterDifficulty.Hell, 0, GearTier.Elite);
        var original = ClassLootHooks.CreatureDied;
        try
        {
            ClassLootHooks.CreatureDied = (creature, body) =>
            { if (creature == victim) { RewardLife.Process(creature, body, new Random(seed)); } else { original(creature, body); } };
            victim.Frozen = true; victim.MoveToWorld(_point, Map.Trammel); victim.AIObject.AITimer.Stop(); victim.Damage(100000, player);
            Require(victim.Corpse is Corpse { SanctuaryNoRewards: true } corpse && corpse.Items.Count == 1
                && ClassGear.State(corpse.Items[0]).Tier == GearTier.Elite, "Native Hell death hook lost difficulty loot");
        }
        finally { ClassLootHooks.CreatureDied = original; victim.Corpse?.Delete(); victim.Delete(); }
    }
    private static void Reload()
    {
        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Filepath));
        foreach (var row in manifest.Creatures)
        {
            var creature = World.FindMobile((Serial)row.Creature) as BaseCreature;
            Require(creature != null && Snapshot(creature) == row, $"Difficulty/stats/injury/XP changed across reload: saved={JsonSerializer.Serialize(row)} actual={(creature == null ? "missing" : JsonSerializer.Serialize(Snapshot(creature)))}");
            Reject(creature, MonsterDifficulty.Normal);
        }
        var corpse = World.FindItem((Serial)manifest.Corpse) as Corpse; var loot = World.FindItem((Serial)manifest.Loot);
        Require(corpse is { SanctuaryNoRewards: true } && loot?.Parent == corpse
            && JsonSerializer.Serialize(ClassGear.State(loot)) == manifest.Gear, "Saved Hell loot or paid claim changed");
    }
}
