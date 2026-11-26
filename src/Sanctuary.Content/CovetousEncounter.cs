// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class CovetousEncounter : EncounterCreature
{
    private const string Skeleton = "skeleton";
    private const string Zombie = "zombie";
    private const string Lich = "lich";
    private const string Corpser = "corpser";
    private const string WaterElemental = "water-elemental";
    internal const string Boss = "rimejaw";

    private static readonly Dictionary<string, DungeonProfile> _species = new(StringComparer.Ordinal)
    {
        ["harpy"] = Species("harpy", "a harpy", MonsterArchetype.Melee, 14, 22, 30, 402),
        ["gazer"] = Species("gazer", "a gazer", MonsterArchetype.Caster, 14, 22, 22, 377, new(Magic: 15)),
        ["elder-gazer"] = Species("elder-gazer", "an elder gazer", MonsterArchetype.Caster, 18, 22, 22, 377,
            new(Magic: 35, Fire: 25)),
        ["giant-rat"] = Species("giant-rat", "a giant rat", MonsterArchetype.Melee, 14, 26, 0xD7, 0x188),
        ["giant-serpent"] = Species("giant-serpent", "a giant serpent", MonsterArchetype.Melee, 14, 26, 21, 219,
            new(Poison: 50)),
        ["slime"] = Species("slime", "a slime", MonsterArchetype.Melee, 14, 22, 51, 456, new(Poison: 45)),
        ["headless"] = Species("headless", "a headless one", MonsterArchetype.Tank, 14, 22, 31, 0x39D),
        ["rat"] = Species("rat", "a rat", MonsterArchetype.Melee, 14, 18, 238, 0xCC),
        [Corpser] = Species(Corpser, "a corpser", MonsterArchetype.Tank, 14, 22, 8, 684, new(Physical: 15)),
        [WaterElemental] = Species(WaterElemental, "a water elemental", MonsterArchetype.Tank, 18, 22, 16, 278,
            new(Cold: 50, Poison: 35)),
        [Skeleton] = Species(Skeleton, "a skeleton", MonsterArchetype.Melee, 22, 26, 50, 451, new(Poison: 50)),
        [Zombie] = Species(Zombie, "a zombie", MonsterArchetype.Tank, 22, 26, 3, 471, new(Poison: 50)),
        ["spectre"] = Species("spectre", "a spectre", MonsterArchetype.Caster, 22, 26, 26, 0x482,
            new(Cold: 35, Poison: 50)),
        ["shade"] = Species("shade", "a shade", MonsterArchetype.Caster, 22, 26, 26, 0x482, new(Cold: 50, Poison: 50)),
        [Lich] = Species(Lich, "a lich", MonsterArchetype.Caster, 22, 26, 24, 0x3E9, new(Cold: 40, Poison: 75)),
        ["drake"] = Species("drake", "a drake", MonsterArchetype.Caster, 22, 26, 61, 362, new(Fire: 35)),
        ["dragon"] = Species("dragon", "a dragon", MonsterArchetype.Caster, 22, 26, 59, 362, new(Physical: 10, Fire: 50)),
        [Boss] = Species(Boss, "Rimejaw", MonsterArchetype.Tank, 27, 27, 59, 362, new(Physical: 15, Cold: 75))
    };

    private const string Prefix = "covetous-";
    internal static DungeonProfile FindSpecies(string id) => id?.StartsWith(Prefix, StringComparison.Ordinal) == true
        ? _species.GetValueOrDefault(id[Prefix.Length..])
        : null;

    private static DungeonProfile Species(string id, string name, MonsterArchetype archetype, int minimum, int maximum,
        int body, int sound, CreatureResistances resistances = null) =>
        new(Prefix + id, id, name, archetype, "Covetous", minimum, maximum, body, sound,
            new(resistances?.Physical ?? 0, resistances?.Magic ?? 0, resistances?.Fire ?? 0, 0,
                resistances?.Cold ?? 0, resistances?.Poison ?? 0));

    private string SpeciesId => Dungeon.Family;
    private bool Undead => SpeciesId is Skeleton or Zombie or "spectre" or "shade" or Lich;

    [Constructible]
    public CovetousEncounter(string species, int level)
    {
        var profile = _species.GetValueOrDefault(species)
            ?? throw new ArgumentException("Unknown Covetous species.", nameof(species));
        var grade = species == Boss ? EncounterGrade.SuperUnique : EncounterGrade.Ordinary;
        const MonsterDifficulty difficulty = MonsterDifficulty.Normal;
        int baseLevel = level - EncounterProfiles.Level(0, grade);
        var modifiers = species == Boss ? EncounterProfiles.RollModifiers(profile, difficulty, Random.Shared) : [];
        var aura = EncounterProfiles.RollAura(modifiers, level, Random.Shared);
        Assign(profile, baseLevel, difficulty, grade, ChampionKind.Champion, modifiers, aura, null, Random.Shared);
        Hue = species switch
        {
            "spectre" => 0x4001,
            "shade" => 0x04E3,
            "elder-gazer" => 0x048F,
            "giant-serpent" => Utility.RandomSnakeHue(),
            "slime" => Utility.RandomSlimeHue(),
            _ => Hue
        };
        RangePerception = 7;
    }

    protected override int[] AdjustResistances(int[] resistances)
    {
        if (SpeciesId == Boss)
            resistances[4] = Math.Min(resistances[4], 95);
        return resistances;
    }

    public override void OnAfterSpawn()
    {
        base.OnAfterSpawn();
        if (SpeciesId == Corpser)
            CantWalk = true;
    }

    internal override int HomeLeashRadius => SpeciesId == Boss ? 15 : 10;
    protected override ResistanceType MeleeElement => SpeciesId switch
    {
        "giant-serpent" or "slime" => ResistanceType.Poison,
        WaterElemental or Boss => ResistanceType.Cold,
        _ => ResistanceType.Physical
    };
    public override bool BleedImmune => Undead;
    public override Poison PoisonImmune => Undead ? Poison.Lethal : null;
    public override OppositionGroup OppositionGroup => Undead ? OppositionGroup.FeyAndUndead : null;
    public override string CorpseName => $"{Dungeon.Name}'s corpse";
    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon) =>
        new(dungeon.Family, Undead, false, Leader, false, true, 1, 0, 100, Leader ? 50 : 100, 0);
    protected override int RaiseKind => SpeciesId switch { Skeleton => 1, Zombie => 2, _ => 0 };
    protected override bool CanRaiseKind(Corpse corpse) =>
        SpeciesId == Lich && corpse.SanctuaryUndeadKind is 1 or 2
        && CorpseSkillRules.TryMonsterLevel(corpse, out int level) && level >= 22 && level <= Basis.Level;
    protected override GraveyardUndead CreateRaisedBody(Corpse corpse)
    {
        CorpseSkillRules.TryMonsterLevel(corpse, out int level);
        return new CovetousEncounter(corpse.SanctuaryUndeadKind == 1 ? Skeleton : Zombie, level);
    }

    protected override bool RaisedBodyAwardsRewards => false;
}
