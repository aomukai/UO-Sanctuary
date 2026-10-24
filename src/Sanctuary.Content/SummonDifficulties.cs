// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record FacetDifficulty(int Map, MonsterDifficulty Difficulty);
public sealed record SummonDifficultyState(uint Owner, MonsterDifficulty Difficulty);

// Load this ledger before mobiles: their existing deserializers recalculate
// derived summon stats. No change to their serialized inheritance is needed.
public sealed class SummonDifficultyProfiles : GenericPersistence
{
    internal readonly Dictionary<Serial, SummonDifficultyState> States = new();
    public SummonDifficultyProfiles() : base("SanctuarySummonDifficulties", -1) { }
    public override void Serialize(IGenericWriter writer)
    {
        Prune(); writer.Write(0); writer.Write(States.Count);
        foreach (var (serial, state) in States)
        { writer.Write(serial.Value); writer.Write(state.Owner); writer.Write((byte)state.Difficulty); }
    }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown summon difficulty version"); }
        int count = reader.ReadInt();
        if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid summon difficulty count"); }
        States.Clear();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt(); uint owner = reader.ReadUInt();
            var difficulty = (MonsterDifficulty)reader.ReadByte();
            if (!serial.IsMobile || !((Serial)owner).IsMobile || difficulty is < MonsterDifficulty.Normal or > MonsterDifficulty.Hell
                || !States.TryAdd(serial, new(owner, difficulty)))
            { throw new InvalidDataException("Invalid summon difficulty identity"); }
        }
    }
    internal void Prune()
    {
        var remove = new List<Serial>();
        foreach (var (serial, state) in States)
        {
            if (World.FindMobile(serial) is not SkeletonGuardian { Deleted: false } pet
                || pet.ControlMaster?.Serial.Value != state.Owner) { remove.Add(serial); }
        }
        foreach (var serial in remove) { States.Remove(serial); }
    }
    public override void PostDeserialize() => Prune();
}

public static class SummonDifficulties
{
    private static Dictionary<int, MonsterDifficulty> _facets = new();
    public static SummonDifficultyProfiles Profiles { get; private set; }
    public static void Configure()
    {
        Profiles = new();
        SetFacets(BalanceCatalog.Read<FacetDifficulty>("facet-difficulties"));
    }
    public static void Initialize()
    {
        // Subscribe after every Configure assignment, retaining map-survey work.
        // PlayerMobile calls this on both map changes and ordinary movement.
        ClassMapHooks.LocationChanged += LocationChanged;
    }
    internal static FacetDifficulty[] Facets()
    {
        var result = new List<FacetDifficulty>();
        foreach (var (map, difficulty) in _facets) { result.Add(new(map, difficulty)); }
        return result.ToArray();
    }
    internal static void SetFacets(FacetDifficulty[] definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var facets = new Dictionary<int, MonsterDifficulty>();
        foreach (var definition in definitions)
        {
            if (definition == null || definition.Map < 0 || definition.Map >= Map.Maps.Length
                || Map.Maps[definition.Map] == null || Map.Maps[definition.Map] == Map.Internal
                || definition.Difficulty is < MonsterDifficulty.Normal or > MonsterDifficulty.Hell
                || !facets.TryAdd(definition.Map, definition.Difficulty))
            { throw new InvalidDataException("Invalid or duplicate facet difficulty designation"); }
        }
        _facets = facets;
    }
    public static MonsterDifficulty For(Map map) => map == null || map == Map.Internal
        ? MonsterDifficulty.Normal : _facets.GetValueOrDefault(map.MapID);
    public static MonsterDifficulty For(SkeletonGuardian pet) => Profiles?.States.GetValueOrDefault(pet.Serial)?.Difficulty ?? MonsterDifficulty.Normal;
    internal static void Created(SkeletonGuardian pet, PlayerMobile owner)
        => Profiles.States[pet.Serial] = new(owner.Serial.Value, For(owner.Map));
    private static void LocationChanged(Mobile mobile)
    { if (!World.Loading && mobile is PlayerMobile owner) { Sync(owner); } }
    // Quantize down once to FP256. Retain a living fractional point at the lower
    // boundary; repeated notification in the same difficulty never rounds again.
    internal static int Life256(int current256, int oldMaximum256, int newMaximum256)
    {
        if (current256 < 0 || oldMaximum256 <= 0 || newMaximum256 <= 0) { throw new ArgumentOutOfRangeException(); }
        return current256 == 0 ? 0 : (int)Math.Clamp((long)Math.Min(current256, oldMaximum256) * newMaximum256 / oldMaximum256, 1, newMaximum256);
    }
    internal static void Sync(PlayerMobile owner)
    {
        if (owner is not { Alive: true, Deleted: false } || owner.Map == null || owner.Map == Map.Internal || World.Loading) { return; }
        var difficulty = For(owner.Map);
        foreach (var follower in owner.AllFollowers ?? [])
        {
            if (follower is not SkeletonGuardian { Deleted: false, Alive: true } pet || pet.ControlMaster != owner) { continue; }
            var previous = Profiles.States.GetValueOrDefault(pet.Serial);
            if (previous != null && previous.Owner != owner.Serial.Value) { throw new InvalidDataException("Summon difficulty owner changed"); }
            if (For(pet) == difficulty)
            { Profiles.States[pet.Serial] = new(owner.Serial.Value, difficulty); continue; }
            var resources = BattleOrders.Capture(pet);
            Profiles.States[pet.Serial] = new(owner.Serial.Value, difficulty);
            pet.RefreshDifficulty();
            BattleOrders.Change(pet, resources, preserveSummonLifeRatio: true);
        }
        BladeSentinelSummons.RescaleFor(owner);
    }
}
