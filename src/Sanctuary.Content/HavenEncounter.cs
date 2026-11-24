// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(1)]
public partial class HavenEncounter : EncounterCreature
{
    private const string Krulg = "haven-krulg";
    internal const string Bandit = "haven-bandit", Thief = "haven-thief";
    internal const string Skeleton = "haven-skeleton", Zombie = "haven-zombie";
    internal const string Spectre = "haven-spectre", Wraith = "haven-wraith",
        SkeletonKnight = "haven-skeleton-knight", Ghoul = "haven-ghoul";
    internal const string EarthElemental = "haven-elemental", GraniteElemental = "haven-granite-elemental";
    internal const string Bogling = "haven-bogling";
    private const string Lizardman = "haven-lizardman";
    [SerializableField(0, getter: "private", setter: "private")] private bool _thiefRolled;
    [SerializableField(1, getter: "private", setter: "private")] private DateTime _thiefHiddenUntil;
    [SerializableField(2, getter: "private", setter: "private")] private DateTime _devourReadyAt;

    // Version 0 did not save any fields on HavenEncounter itself.
    private void Deserialize(IGenericReader reader, int version) { }

    [AfterDeserialization]
    private void AfterLoad()
    {
        if (_thiefHiddenUntil != default)
            Timer.StartTimer(TimeSpan.Zero, RestoreThiefVisibility, out _reveal);
    }

    private static readonly Dictionary<string, DungeonProfile> _profiles = new(StringComparer.Ordinal)
    {
        [Skeleton] = Species(Skeleton, "a skeleton", MonsterArchetype.Melee, 2, 6, 50, 451, poison: 50),
        [Zombie] = Species(Zombie, "a zombie", MonsterArchetype.Tank, 1, 6, 3, 471, poison: 50),
        [Bandit] = Species(Bandit, "a farm bandit", MonsterArchetype.Melee, 4, 6, 0x190, 0),
        [Thief] = Species(Thief, "a Haven thief", MonsterArchetype.Melee, 4, 6, 0x190, 0),
        [Spectre] = Species(Spectre, "a spectre", MonsterArchetype.Caster, 6, 8, 26, 0x482),
        [Wraith] = Species(Wraith, "a wraith", MonsterArchetype.Melee, 6, 8, 26, 0x482),
        [SkeletonKnight] = Species(SkeletonKnight, "a skeleton knight", MonsterArchetype.Melee, 8, 10, 147, 451),
        [Ghoul] = Species(Ghoul, "a ghoul", MonsterArchetype.Melee, 9, 11, 153, 0x482),
        [EarthElemental] = Species(EarthElemental, "an earth elemental", MonsterArchetype.Tank, 6, 8, 14, 268, physical: 10),
        [GraniteElemental] = Species(GraniteElemental, "a granite elemental", MonsterArchetype.Tank, 7, 9, 14, 268, physical: 10),
        // Keep the old upper end readable so placed spawners can migrate saved creatures.
        [Bogling] = Species(Bogling, "a bogling", MonsterArchetype.Melee, 1, 6, 779, 422),
        [Lizardman] = Species(Lizardman, "a lizardman", MonsterArchetype.Melee, 8, 10, 35, 417),
        // The super-unique grade adds three levels to the base profile.
        [Krulg] = Species(Krulg, "Boss Krulg", MonsterArchetype.Tank, 7, 7, 1, 427, physical: 10)
    };

    internal static DungeonProfile FindSpecies(string id) => id != null ? _profiles.GetValueOrDefault(id) : null;
    private static DungeonProfile Species(string id, string name, MonsterArchetype archetype, int min, int max,
        int body, int sound, int physical = 0, int poison = 0) =>
        new(id, id, name, archetype, "New Haven", min, max, body, sound, new(physical, 0, 0, 0, 0, poison));

    [Constructible]
    public HavenEncounter(string id, int minimumLevel, int maximumLevel)
        : this(id, Utility.RandomMinMax(minimumLevel, maximumLevel))
    {
    }

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
        else if (id == Thief)
        {
            HairItemID = 0x203B;
            HairHue = 0x0455;
            AddCostume(0x1F03, Layer.OuterTorso, 0x0455);
            AddCostume(0x170B, Layer.Shoes, 0x0455);
        }
        else if (id is Spectre or Wraith)
        {
            Hue = id == Spectre ? 0x4001 : 0x4000;
        }
        else if (id == GraniteElemental)
        {
            Hue = 0x0455;
            RangeFight = RockThrowRange;
        }
        else if (id == Lizardman)
        {
            RangePerception = 6;
            if (level >= 9)
                Body = 36;
        }
        else if (id == Krulg)
            RangePerception = 8;
        else if (id == Bogling && Utility.Random(4) == 0)
        {
            // This is carried from spawn and follows the native corpse transfer.
            var pack = Backpack;
            if (pack == null)
            {
                pack = new Backpack { Movable = false };
                AddItem(pack);
            }
            pack.DropItem(ItemOrigins.Create(ItemOrigin.Drop, () => new Log(1)));
        }
    }

    public override int AcquireOnApproachRange => Dungeon.Id is Lizardman or Krulg
        ? RangePerception : base.AcquireOnApproachRange;

    protected override int EncounterMoveInterval(int interval) => Dungeon.Id is Bogling or Skeleton or Zombie
        ? Math.Max(640, interval) : interval;

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

    private static bool IsUndead(DungeonProfile profile) =>
        profile.Id is Skeleton or Zombie or Spectre or Wraith or SkeletonKnight or Ghoul;
    public override string CorpseName => Dungeon.Id switch
    {
        Bandit => "a farm bandit's corpse",
        Thief => "a Haven thief's corpse",
        Skeleton => "a skeleton's corpse",
        Zombie => "a zombie's corpse",
        Spectre or Wraith => "a ghostly corpse",
        SkeletonKnight => "a skeleton knight's corpse",
        Ghoul => "a ghoul's corpse",
        EarthElemental => "an earth elemental's remains",
        GraniteElemental => "a granite elemental's remains",
        Bogling => "a plant corpse",
        "haven-lizardman" => "a lizardman's corpse",
        Krulg => "Boss Krulg's corpse",
        _ => base.CorpseName
    };
    public override bool AlwaysMurderer => Dungeon.Id is Bandit or Thief;
    internal bool IsKrulg => Dungeon.Id == Krulg;
    public override bool BleedImmune => IsUndead(Dungeon) || Dungeon.Id is EarthElemental or GraniteElemental;
    public override Poison PoisonImmune => IsUndead(Dungeon) ? Poison.Lethal : null;
    public override OppositionGroup OppositionGroup => IsUndead(Dungeon) ? OppositionGroup.FeyAndUndead : null;

    protected override int RegionalEncounterPlayers => Dungeon.Id is EarthElemental or GraniteElemental or Lizardman or Krulg ? 5 : 3;
    protected override bool RollMissingModifiersOnLoad => Dungeon.Id == Krulg;

    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon) =>
        new(dungeon.Family, IsUndead(dungeon), false, Leader, false, true,
            dungeon.Id == Krulg ? 2 : 1, 0, 100, Leader ? 50 : 100, 0);
}
