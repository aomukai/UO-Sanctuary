// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class HavenEncounter : EncounterCreature
{
    private const string Krulg = "haven-krulg";
    private const string Bandit = "haven-bandit";
    private const string Lizardman = "haven-lizardman";
    private static readonly Dictionary<string, DungeonProfile> _profiles = new(StringComparer.Ordinal)
    {
        ["haven-skeleton"] = Species("haven-skeleton", "a skeleton", MonsterArchetype.Melee, 2, 6, 50, 451, poison: 50),
        ["haven-zombie"] = Species("haven-zombie", "a zombie", MonsterArchetype.Tank, 1, 6, 3, 471, poison: 50),
        [Bandit] = Species(Bandit, "a farm bandit", MonsterArchetype.Melee, 4, 6, 0x190, 0),
        ["haven-elemental"] = Species("haven-elemental", "an earth elemental", MonsterArchetype.Tank, 6, 8, 14, 268, physical: 10),
        [Lizardman] = Species(Lizardman, "a lizardman", MonsterArchetype.Melee, 8, 10, 35, 417),
        // The super-unique grade adds three levels to the base profile.
        [Krulg] = Species(Krulg, "Boss Krulg", MonsterArchetype.Tank, 7, 7, 1, 427, physical: 10)
    };

    internal static DungeonProfile FindSpecies(string id) => id != null ? _profiles.GetValueOrDefault(id) : null;
    private static DungeonProfile Species(string id, string name, MonsterArchetype archetype, int min, int max,
        int body, int sound, int physical = 0, int poison = 0) =>
        new(id, id, name, archetype, "New Haven", min, max, body, sound, new(physical, 0, 0, 0, 0, poison));

    [Constructible]
    public HavenEncounter(string id, int level)
    {
        var profile = FindSpecies(id) ?? throw new ArgumentException("Unknown New Haven encounter.", nameof(id));
        const MonsterDifficulty difficulty = MonsterDifficulty.Normal;
        var grade = id == Krulg ? EncounterGrade.SuperUnique : EncounterGrade.Ordinary;
        var modifiers = id == Krulg ? EncounterProfiles.RollModifiers(profile, difficulty, Random.Shared) : [];
        var aura = EncounterProfiles.RollAura(modifiers, EncounterProfiles.Level(level, grade), Random.Shared);
        Assign(profile, level, difficulty, grade, ChampionKind.Champion, modifiers, aura, null, Random.Shared);
        if (id == Bandit)
        {
            HairItemID = 0x203B;
            HairHue = 0x0455;
            AddCostume(0x1EFD, Layer.Shirt, 0x0590);
            AddCostume(0x1539, Layer.Pants, 0x0455);
            AddCostume(0x170B, Layer.Shoes, 0x0455);
            AddCostume(0x1540, Layer.Helm, 0x0021);
        }
        else if (id == Lizardman)
        {
            RangePerception = 6;
            if (level >= 9)
                Body = 36;
        }
        else if (id == Krulg)
            RangePerception = 8;
    }

    public override int AcquireOnApproachRange => Dungeon.Id is Lizardman or Krulg
        ? RangePerception : base.AcquireOnApproachRange;

    // These pieces supply paperdoll art only. NPC combat treats normal wearable types as equipment.
    private void AddCostume(int graphic, Layer layer, int hue) =>
        AddItem(new Item(graphic) { Layer = layer, Hue = hue, Movable = false });

    internal void UpdateSavedCostume()
    {
        if (Dungeon.Id != Bandit)
            return;
        using var old = PooledRefList<Item>.Create();
        foreach (var item in Items)
            if (!item.Movable && item is FancyShirt or LongPants or Boots or Bandana)
                old.Add(item);
        foreach (var item in old)
        {
            int graphic = item.ItemID, hue = item.Hue;
            Layer layer = item.Layer;
            item.Delete();
            AddCostume(graphic, layer, hue);
        }
    }

    private static bool IsUndead(DungeonProfile profile) => profile.Id is "haven-skeleton" or "haven-zombie";
    public override string CorpseName => Dungeon.Id switch
    {
        Bandit => "a farm bandit's corpse",
        "haven-skeleton" => "a skeleton's corpse",
        "haven-zombie" => "a zombie's corpse",
        "haven-elemental" => "an earth elemental's remains",
        "haven-lizardman" => "a lizardman's corpse",
        Krulg => "Boss Krulg's corpse",
        _ => base.CorpseName
    };
    public override bool AlwaysMurderer => Dungeon.Id == Bandit;
    internal bool IsKrulg => Dungeon.Id == Krulg;
    public override bool BleedImmune => IsUndead(Dungeon) || Dungeon.Id == "haven-elemental";
    public override Poison PoisonImmune => IsUndead(Dungeon) ? Poison.Lethal : null;
    public override OppositionGroup OppositionGroup => IsUndead(Dungeon) ? OppositionGroup.FeyAndUndead : null;

    // Compress the introductory levels without changing thresholds or combat stats outside Haven.
    protected override int ExperienceMultiplier => 5;
    protected override int EncounterPlayers => Dungeon.Id is "haven-elemental" or Lizardman or Krulg ? 5 : 3;
    protected override bool RollMissingModifiersOnLoad => Dungeon.Id == Krulg;

    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon) =>
        new(dungeon.Family, IsUndead(dungeon), false, Leader, false, true,
            dungeon.Id == Krulg ? 2 : 1, 0, 100, Leader ? 50 : 100, 0);
}
