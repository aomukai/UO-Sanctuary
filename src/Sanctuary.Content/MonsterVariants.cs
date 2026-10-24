// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Commands;
using Server.Mobiles;
using Server.Targeting;

namespace Sanctuary;

// Spawn identity is independent of facet, location, combat target and loot ownership.
public static class MonsterVariants
{
    internal static MonsterVariantProfiles Profiles { get; private set; }
    public static MonsterDifficulty Difficulty(BaseCreature creature) => creature is SkeletonGuardian summon ? SummonDifficulties.For(summon)
        : creature is EncounterCreature encounter ? encounter.Difficulty : Profiles?.Find(creature.Serial) ?? MonsterDifficulty.Normal;
    internal static NormalMonsterStats Melee(GraveyardUndead creature, NormalMonsterStats normal)
    {
        if (normal == null || Difficulty(creature) == MonsterDifficulty.Normal) { return normal; }
        var state = CreatureProfiles.Session(creature);
        if (state.DifficultyMelee == null)
        {
            var row = CreatureProfiles.For(creature);
            state.DifficultyMelee = normal with
            {
                MaxLife = row.Life ?? normal.MaxLife,
                Attack1 = normal.Attack1 with { MinDamage = row.Attack1.MinDamage ?? normal.Attack1.MinDamage, MaxDamage = row.Attack1.MaxDamage ?? normal.Attack1.MaxDamage },
                Attack2 = normal.Attack2 with { MinDamage = row.Attack2.MinDamage ?? normal.Attack2.MinDamage, MaxDamage = row.Attack2.MaxDamage ?? normal.Attack2.MaxDamage },
                Experience = row.Rewards.Experience ?? normal.Experience
            };
        }
        return state.DifficultyMelee;
    }
    public static void Configure()
    {
        Profiles = new MonsterVariantProfiles();
        EventSink.WorldSave += Profiles.Prune;
        ClassCombatHooks.CreatureResistedDamage = ResistedDamage;
        CommandSystem.Register("SpawnVariant", AccessLevel.GameMaster, args =>
        {
            if (args.Length != 2 || !Enum.TryParse<MonsterDifficulty>(args.GetString(0), true, out var difficulty)
                || !Enum.IsDefined(difficulty))
            { args.Mobile.SendMessage("Usage: [SpawnVariant Normal|Nightmare|Hell CreatureType"); return; }
            string name = args.GetString(1);
            var type = typeof(BaseCreature).Assembly.GetType(name) ?? typeof(BaseCreature).Assembly.GetType("Server.Mobiles." + name)
                ?? typeof(MonsterVariants).Assembly.GetType(name) ?? typeof(MonsterVariants).Assembly.GetType("Sanctuary." + name);
            if (!CanSpawn(type, difficulty))
            { args.Mobile.SendMessage("That creature has no supported difficulty variant."); return; }
            args.Mobile.Target = new SpawnTarget(type, difficulty);
        });
    }
    private static int? ResistedDamage(BaseCreature creature, int damage, int physical, int fire, int cold, int poison, int lightning)
    {
        if (creature is not EncounterCreature && Difficulty(creature) == MonsterDifficulty.Normal || !CreatureProfiles.For(creature).ApplyStats) { return null; }
        // Clamp each component before summing: an immune channel cannot cancel another channel or force one point through.
        long weighted = (long)physical * Math.Clamp(100 - creature.PhysicalResistance, 0, 200)
            + (long)fire * Math.Clamp(100 - creature.FireResistance, 0, 200)
            + (long)cold * Math.Clamp(100 - creature.ColdResistance, 0, 200)
            + (long)poison * Math.Clamp(100 - creature.PoisonResistance, 0, 200)
            + (long)lightning * Math.Clamp(100 - creature.EnergyResistance, 0, 200);
        return (int)Math.Min(int.MaxValue, weighted * damage / 10000);
    }
    public static bool Supported(Type type, MonsterDifficulty difficulty) => type != null && !type.IsAbstract
        && typeof(BaseCreature).IsAssignableFrom(type) && Enum.IsDefined(difficulty)
        && CreatureGold.Policy(type, difficulty) is { Eligible: true }
        && (difficulty == MonsterDifficulty.Normal || CreatureProfiles.ActiveVariant(type, difficulty));
    public static bool CanSpawn(Type type, MonsterDifficulty difficulty) => Supported(type, difficulty)
        && type.GetConstructor(Type.EmptyTypes) != null;
    // Assignment is one-time and only before entering the world; it cannot refresh a fought creature or change paid loot.
    public static void Assign(BaseCreature creature, MonsterDifficulty difficulty)
    {
        if (creature is not { Deleted: false, Alive: true, Controlled: false, Summoned: false }
            || creature is BaseVendor || creature.Map != Map.Internal || creature.DamageEntries.Count != 0
            || Difficulty(creature) != MonsterDifficulty.Normal || !Supported(creature.GetType(), difficulty)
            || difficulty != MonsterDifficulty.Normal && CreatureProfiles.Variant(creature, difficulty) is not { Enabled: true, ApplyStats: true })
        { throw new InvalidOperationException("Difficulty requires a fresh supported wild creature."); }
        if (difficulty == MonsterDifficulty.Normal) { return; }
        Profiles.Store(creature, difficulty);
        CreatureProfiles.Session(creature).Initialized = false;
        CreatureProfiles.Initialize(creature);
        creature.Name = $"{difficulty} {creature.Name}";
    }
    public static BaseCreature Spawn(Type type, MonsterDifficulty difficulty, Point3D location, Map map)
    {
        if (!CanSpawn(type, difficulty) || map == null || map == Map.Internal || !map.CanSpawnMobile(location))
        { throw new InvalidOperationException("Choose a supported creature and a walkable spawn point."); }
        var creature = type.CreateInstance<BaseCreature>();
        try
        {
            Assign(creature, difficulty);
            creature.Home = location;
            creature.MoveToWorld(location, map);
            return creature;
        }
        catch { creature.Delete(); throw; }
    }
    private sealed class SpawnTarget(Type type, MonsterDifficulty difficulty) : Target(12, true, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from.AccessLevel < AccessLevel.GameMaster || from.Map == null || from.Map == Map.Internal
                || targeted is not IPoint3D point || !from.InRange(point, 12)) { return; }
            var location = new Point3D(point);
            if (!from.Map.CanSpawnMobile(location)) { from.SendMessage("That point cannot hold a creature."); return; }
            try { Spawn(type, difficulty, location, from.Map); }
            catch (InvalidOperationException) { from.SendMessage("That native creature cannot be created as a fresh wild variant."); }
        }
    }
}

public sealed class MonsterVariantProfiles : GenericPersistence
{
    private readonly Dictionary<Serial, MonsterDifficulty> _creatures = new();
    public MonsterVariantProfiles() : base("SanctuaryMonsterVariants", 124) { }
    public MonsterDifficulty Find(Serial serial) => _creatures.GetValueOrDefault(serial);
    internal void Store(BaseCreature creature, MonsterDifficulty difficulty) => _creatures.Add(creature.Serial, difficulty);
    public void Prune()
    {
        var removed = new List<Serial>();
        foreach (var serial in _creatures.Keys)
        { if (World.FindMobile(serial) is not BaseCreature { Deleted: false }) { removed.Add(serial); } }
        foreach (var serial in removed) { _creatures.Remove(serial); }
    }
    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(0); writer.Write(_creatures.Count);
        foreach (var (serial, difficulty) in _creatures) { writer.Write(serial.Value); writer.Write((byte)difficulty); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown monster-variant save version."); }
        _creatures.Clear();
        int count = reader.ReadInt();
        if (count is < 0 or > 10000000) { throw new InvalidDataException("Invalid monster-variant count."); }
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt(); var difficulty = (MonsterDifficulty)reader.ReadByte();
            if (difficulty is not (MonsterDifficulty.Nightmare or MonsterDifficulty.Hell))
            { throw new InvalidDataException("Invalid saved monster difficulty."); }
            _creatures.Add(serial, difficulty);
        }
    }
    public override void PostDeserialize()
    {
        Prune();
        foreach (var (serial, difficulty) in _creatures)
        {
            var creature = (BaseCreature)World.FindMobile(serial);
            if (!MonsterVariants.Supported(creature.GetType(), difficulty))
            { throw new InvalidDataException("Saved monster variant no longer has a reward policy."); }
            CreatureProfiles.Initialize(creature);
        }
    }
}
