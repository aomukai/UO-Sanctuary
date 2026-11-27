// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class FrontierEncounter : EncounterCreature
{
    private const string Skeleton = "frontier-skeleton", Zombie = "frontier-zombie", Lich = "frontier-lich";
    private const string VesperUndead = "vesper-undead", Spectre = "vesper-spectre", Wraith = "vesper-wraith",
        Ghoul = "vesper-ghoul", SkeletalKnight = "vesper-skeletal-knight", Mummy = "vesper-mummy";
    private static readonly string[] _vesperGraveyardUndead = [Skeleton, Zombie, Spectre, Wraith, Ghoul];
    private const string Ettin = "frontier-ettin", Dragon = "frontier-dragon";
    internal const string Drake = "frontier-drake", Wyvern = "frontier-wyvern";
    internal const string BoulderEttinId = "boulder-ettin";
    private const string BridgeTroll = "bridge-troll";
    private const string VesperSeaSerpent = "vesper-sea-serpent", VesperKraken = "vesper-kraken";
    private const string Desert = "compassion-desert-";
    private const string DesertCopper = Desert + "dull-copper-elemental", DesertEarth = Desert + "earth-elemental",
        DesertHarpy = Desert + "harpy", DesertVortex = Desert + "sand-vortex",
        DesertScorpion = Desert + "scorpion", DesertSnake = Desert + "snake";
    private const string Lizardman = "fens-lizardman", Alligator = "fens-alligator", Slime = "fens-slime",
        Bogling = "fens-bogling", BogThing = "fens-bog-thing", PlagueBeast = "fens-plague-beast", SwampDragon = "fens-swamp-dragon";
    internal const string DespiseEttin = "despise-ettin", DespiseRatman = "despise-ratman";
    internal const string DespiseTroll = "despise-troll", DespiseOgre = "despise-ogre", DespiseOgreLord = "despise-ogre-lord";
    internal const string DespiseHarpy = "despise-harpy", DespiseStoneHarpy = "despise-stone-harpy", DespiseOgreMage = "despise-ogre-mage";
    internal const string CampOgreId = "camp-ogre";
    private static readonly DungeonProfile _ogre =
        Species(DespiseOgre, "an ogre", MonsterArchetype.Tank, 20, 25, 1, 427, new(Physical: 15));

    private static readonly Dictionary<string, DungeonProfile> _species = new(StringComparer.Ordinal)
    {
        [Skeleton] = Species(Skeleton, "a skeleton", MonsterArchetype.Melee, 12, 20, 50, 451, new(Poison: 50)),
        [Zombie] = Species(Zombie, "a zombie", MonsterArchetype.Tank, 12, 20, 3, 471, new(Poison: 50)),
        [Lich] = Species(Lich, "a cemetery lich", MonsterArchetype.Caster, 16, 20, 24, 0x3E9, new(Cold: 40, Poison: 75)),
        [Spectre] = Species(Spectre, "a spectre", MonsterArchetype.Caster, 12, 16, 26, 0x482, new(Cold: 35, Poison: 50)),
        [Wraith] = Species(Wraith, "a wraith", MonsterArchetype.Caster, 12, 16, 26, 0x482, new(Cold: 50, Poison: 50)),
        [Ghoul] = Species(Ghoul, "a ghoul", MonsterArchetype.Melee, 12, 16, 153, 0x482, new(Poison: 50)),
        [SkeletalKnight] = Species(SkeletalKnight, "a skeletal knight", MonsterArchetype.Tank, 15, 18, 147, 451,
            new(Physical: 15, Poison: 50)) with { SkeletalRole = SkeletalMonsterRole.Warrior },
        [Mummy] = Species(Mummy, "a mummy", MonsterArchetype.Tank, 15, 18, 154, 471, new(Poison: 50)),
        [Ettin] = Species(Ettin, "an ettin", MonsterArchetype.Tank, 14, 20, 18, 367, new(Physical: 10)),
        [BoulderEttinId] = Species(BoulderEttinId, "an ettin", MonsterArchetype.Tank, 14, 18, 18, 367, new(Physical: 10)),
        [BridgeTroll] = Species(BridgeTroll, "a troll", MonsterArchetype.Melee, 15, 15, 53, 461, new(Physical: 10)),
        [VesperSeaSerpent] = Species(VesperSeaSerpent, "a sea serpent", MonsterArchetype.Caster, 15, 27, 150, 447,
            new(Physical: 25, Fire: 50, Cold: 35, Poison: 30)),
        [VesperKraken] = Species(VesperKraken, "a kraken", MonsterArchetype.Tank, 20, 30, 77, 353,
            new(Physical: 45, Fire: 30, Cold: 35, Poison: 20)),
        [DesertCopper] = Species(DesertCopper, "a dull copper elemental", MonsterArchetype.Tank, 20, 25, 110, 268,
            new(Physical: 35, Fire: 35)),
        [DesertEarth] = Species(DesertEarth, "an earth elemental", MonsterArchetype.Tank, 20, 25, 14, 268,
            new(Physical: 20)),
        [DesertHarpy] = Species(DesertHarpy, "a harpy", MonsterArchetype.Melee, 20, 25, 30, 402, new()),
        [DesertVortex] = Species(DesertVortex, "a sand vortex", MonsterArchetype.Melee, 20, 25, 790, 263,
            new(Physical: 70, Fire: 60, Cold: 60, Poison: 60)),
        [DesertScorpion] = Species(DesertScorpion, "a scorpion", MonsterArchetype.Melee, 20, 25, 48, 397, new(Poison: 45)),
        [DesertSnake] = Species(DesertSnake, "a snake", MonsterArchetype.Melee, 20, 25, 52, 0xDB, new(Poison: 35)),
        [DespiseEttin] = Species(DespiseEttin, "an ettin", MonsterArchetype.Tank, 18, 22, 18, 367, new(Physical: 10)),
        [DespiseRatman] = Species(DespiseRatman, "a ratman", MonsterArchetype.Melee, 12, 16, 42, 437, new()),
        [DespiseTroll] = Species(DespiseTroll, "a troll", MonsterArchetype.Melee, 20, 28, 53, 461, new(Physical: 10)),
        [DespiseHarpy] = Species(DespiseHarpy, "a harpy", MonsterArchetype.Melee, 24, 28, 30, 402, new()),
        [DespiseOgre] = _ogre with { MaximumLevel = 30 },
        [DespiseStoneHarpy] = Species(DespiseStoneHarpy, "a stone harpy", MonsterArchetype.Tank, 26, 30, 73, 402, new(Physical: 45)),
        [CampOgreId] = _ogre with { Id = CampOgreId, MinimumLevel = 24, MaximumLevel = 28 },
        // Paragons at level 28 still need a base profile at level 26.
        [DespiseOgreLord] = Species(DespiseOgreLord, "an ogre lord", MonsterArchetype.Tank, 26, 32, 83, 427, new(Physical: 20)),
        [DespiseOgreMage] = Species(DespiseOgreMage, "an ogre mage", MonsterArchetype.Caster, 28, 32, 83, 427, new(Physical: 15)),
        [Drake] = Species(Drake, "a drake", MonsterArchetype.Caster, 16, 28, 61, 362, new(Fire: 20)),
        [Wyvern] = Species(Wyvern, "a wyvern", MonsterArchetype.Melee, 16, 28, 62, 362, new(Poison: 70)),
        [Lizardman] = Species(Lizardman, "a lizardman", MonsterArchetype.Melee, 15, 25, 35, 417, new(Poison: 20)),
        [Alligator] = Species(Alligator, "an alligator", MonsterArchetype.Tank, 15, 19, 0xCA, 660, new(Physical: 10)),
        [Slime] = Species(Slime, "a slime", MonsterArchetype.Melee, 15, 19, 51, 456, new(Poison: 45)),
        [Bogling] = Species(Bogling, "a bogling", MonsterArchetype.Melee, 16, 21, 779, 422, new(Poison: 25)),
        [BogThing] = Species(BogThing, "a bog thing", MonsterArchetype.Tank, 19, 24, 780, 422, new(Physical: 20, Poison: 45)),
        [PlagueBeast] = Species(PlagueBeast, "a plague beast", MonsterArchetype.Tank, 22, 25, 775, 422, new(Physical: 20, Poison: 65)),
        [SwampDragon] = Species(SwampDragon, "a swamp dragon", MonsterArchetype.Tank, 23, 25, 0x31A, 0x16A,
            new(Physical: 20, Poison: 50)),
        // The named grade adds three levels to the base profile.
        [Dragon] = Species(Dragon, "a dragon", MonsterArchetype.Caster, 27, 27, 59, 362, new(Physical: 10, Fire: 70))
    };

    private long _infernoAfter;

    internal static DungeonProfile FindSpecies(string id) => id == null ? null : _species.GetValueOrDefault(id);
    internal static EncounterGrade OgreLordGrade(int roll) => roll < Paragon.SpawnChance ? EncounterGrade.Champion : EncounterGrade.Ordinary;
    private static DungeonProfile Species(string id, string name, MonsterArchetype archetype, int min, int max, int body, int sound,
        CreatureResistances resistances) => new(id, id, name, archetype, "Britannia frontier", min, max, body, sound,
        new(resistances.Physical ?? 0, 0, resistances.Fire ?? 0, 0, resistances.Cold ?? 0, resistances.Poison ?? 0));

    [Constructible]
    public FrontierEncounter(string id, int level, int modifierCount) : this(id, level, modifierCount, null)
    {
    }

    // These spawners roll a new grade for each vacancy and share one Unique slot per area.
    [Constructible]
    public FrontierEncounter(string id, int level) : this(
        id == VesperUndead ? _vesperGraveyardUndead[Utility.Random(_vesperGraveyardUndead.Length)] : id,
        level, 0, RollPopulationGrade(id))
    {
    }

    private FrontierEncounter(string id, int level, int modifierCount, EncounterGrade? rolledGrade)
    {
        var profile = FindSpecies(id) ?? throw new ArgumentException("Unknown frontier species.", nameof(id));
        if (id == Dragon ? modifierCount != 2 : id == DespiseOgreLord ? modifierCount != 0 : modifierCount is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(modifierCount));

        const MonsterDifficulty difficulty = MonsterDifficulty.Normal;
        ParagonAlreadyRolled = rolledGrade.HasValue || id == DespiseOgreLord;
        var grade = rolledGrade ?? (id == Dragon ? EncounterGrade.SuperUnique : id == DespiseOgreLord ? OgreLordGrade(Utility.Random(100))
            : modifierCount > 0 ? EncounterGrade.Unique : EncounterGrade.Ordinary);
        int baseLevel = level - EncounterProfiles.Level(0, grade);
        if (rolledGrade.HasValue)
            baseLevel = Math.Max(baseLevel, profile.MinimumLevel);
        int maxLevel = id == Dragon ? EncounterProfiles.Level(profile.MaximumLevel, grade) : profile.MaximumLevel;
        int encounterLevel = EncounterProfiles.Level(baseLevel, grade);
        if (baseLevel < profile.MinimumLevel || baseLevel > profile.MaximumLevel || encounterLevel > maxLevel)
            throw new ArgumentOutOfRangeException(nameof(level));
        EliteModifier[] modifiers = id == Dragon ? [EliteModifier.ExtraStrong, EliteModifier.StoneSkin]
            : grade == EncounterGrade.Unique ? EncounterProfiles.RollModifiers(profile, difficulty, Random.Shared) : [];
        var aura = EncounterProfiles.RollAura(modifiers, encounterLevel, Random.Shared);
        string name = id switch
        {
            Dragon => "Cindermaw",
            Drake when modifierCount > 0 => "the drake broodkeeper",
            Wyvern when modifierCount > 0 => "the wyvern broodkeeper",
            DespiseEttin when modifierCount > 0 => "an ettin warlord",
            _ => null
        };
        Assign(profile, baseLevel, difficulty, grade, ChampionKind.Champion, modifiers, aura, name, Random.Shared);
        if (VesperSeaCreature)
        {
            CanSwim = true;
            CantWalk = true;
        }
        if (grade == EncounterGrade.Ordinary)
        {
            if (id == Spectre)
                Hue = 0x4001;
            else if (id == Wraith)
                Hue = 0x04E3;
            else if (id == DesertSnake)
                Hue = Utility.RandomSnakeHue();
            else if (id == VesperSeaSerpent)
                Hue = Utility.Random(0x530, 9);
            else if (id == DespiseOgreMage)
                Hue = 0x048D;
        }
        if (id == Lich)
            CantWalk = true;
        ApplyPerception();
    }

    private static EncounterGrade RollPopulationGrade(string species)
    {
        var grade = PopulationGrade(Utility.Random(100));
        if (grade != EncounterGrade.Unique)
            return grade;

        bool desert = IsDesert(species);
        if (World.Mobiles.Values.OfType<FrontierEncounter>().Any(mob =>
            !mob.Deleted && mob.Alive && mob.Grade == EncounterGrade.Unique
            && (desert ? IsDesert(mob.Dungeon.Id) : mob.VesperGraveyardDead)))
            return EncounterGrade.Ordinary;
        return grade;
    }

    internal static EncounterGrade PopulationGrade(int roll)
    {
        const int uniqueChance = 3;
        if (roll < uniqueChance)
            return EncounterGrade.Unique;
        return roll < uniqueChance + Paragon.SpawnChance ? EncounterGrade.Champion : EncounterGrade.Ordinary;
    }

    private static bool IsDesert(string id) => id?.StartsWith(Desert, StringComparison.Ordinal) == true;
    private bool VesperSeaCreature => Dungeon.Id is VesperSeaSerpent or VesperKraken;

    private bool PlayersFive => Dungeon.Id is Drake or Wyvern or Dragon or DespiseEttin or DespiseRatman
        or DespiseTroll or DespiseHarpy or DespiseOgre or DespiseStoneHarpy or DespiseOgreLord or DespiseOgreMage;
    private bool RestrictedRange => (PlayersFive && Dungeon.Id != Dragon) || Dungeon.Id == BridgeTroll
        || Dungeon.Id.StartsWith("fens-", StringComparison.Ordinal) || IsDesert(Dungeon.Id);
    private bool BritainCemeteryDead => (Dungeon.Id is Skeleton or Zombie) && HomeMap == Map.Trammel
        && Home.X is >= 1373 and <= 1409 && Home.Y is >= 1476 and <= 1505;
    private bool VesperGraveyardDead => Undead && HomeMap == Map.Trammel
        && Home.X is >= 2742 and <= 2782 && Home.Y is >= 840 and <= 894;
    protected override int RegionalEncounterPlayers => PlayersFive ? 5 : 3;
    internal override int HomeLeashRadius => VesperSeaCreature ? 12 : BritainCemeteryDead || VesperGraveyardDead ? 7
        : RestrictedRange ? 10 : base.HomeLeashRadius;
    public override int AcquireOnApproachRange => Math.Min(base.AcquireOnApproachRange, RangePerception);

    internal override bool IsEnemyIgnoringLeash(Mobile mobile) => mobile != null
        && (!VesperSeaCreature || Controlled || Summoned || Utility.InRange(Home, mobile.Location, HomeLeashRadius))
        && base.IsEnemyIgnoringLeash(mobile);

    public override void OnAfterSpawn()
    {
        base.OnAfterSpawn();
        ApplyPerception();
    }

    [AfterDeserialization]
    private void ApplyPerception()
    {
        if (BritainCemeteryDead || VesperGraveyardDead)
            RangePerception = 5;
        else if (VesperSeaCreature)
            RangePerception = 10;
        else if (RestrictedRange)
            RangePerception = 7;
    }
    protected override ResistanceType MeleeElement => Dungeon.Id is Wyvern or Slime or BogThing or PlagueBeast or DesertScorpion or DesertSnake
        ? ResistanceType.Poison : base.MeleeElement;
    private bool Undead => Dungeon.Id is Skeleton or Zombie or Lich or Spectre or Wraith or Ghoul or SkeletalKnight or Mummy;
    public override bool BleedImmune => Undead;
    public override Poison PoisonImmune => Undead ? Poison.Lethal : null;
    public override OppositionGroup OppositionGroup => Undead ? OppositionGroup.FeyAndUndead : null;
    public override string CorpseName => Dungeon.Id switch
    {
        Skeleton => "a skeleton's corpse",
        Zombie => "a zombie's corpse",
        Lich => "a lich's corpse",
        Ettin or DespiseEttin => "an ettin's corpse",
        DespiseRatman => "a ratman's corpse",
        BridgeTroll or DespiseTroll => "a troll's corpse",
        DespiseOgre or CampOgreId => "an ogre's corpse",
        DespiseOgreLord => "an ogre lord's corpse",
        Drake => "a drake's corpse",
        Wyvern => "a wyvern's corpse",
        Dragon => "Cindermaw's corpse",
        _ => $"{Dungeon.Name}'s corpse"
    };

    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon) =>
        new(dungeon.Family, Undead, false, Leader, false, true, 1, 0, 100, Leader ? 50 : 100, 0);

    protected override int RaiseKind => Dungeon.Id switch { Skeleton => 1, Zombie => 2, _ => 0 };
    protected override bool CanRaiseKind(Corpse corpse) =>
        Dungeon.Id == Lich && (corpse.SanctuaryUndeadKind is 1 or 2)
        && CorpseSkillRules.TryMonsterLevel(corpse, out int level) && level >= 12 && level <= Basis.Level;
    protected override GraveyardUndead CreateRaisedBody(Corpse corpse)
    {
        CorpseSkillRules.TryMonsterLevel(corpse, out int level);
        return new FrontierEncounter(corpse.SanctuaryUndeadKind == 1 ? Skeleton : Zombie, level, 0);
    }
    protected override bool RaisedBodyAwardsRewards => false;

    public override bool BeginFirebolt(Mobile target)
    {
        if (Dungeon.Id is (Drake or Dragon) && Core.TickCount >= _infernoAfter && BeginInferno(target))
        {
            _infernoAfter = Core.TickCount + 7500;
            return true;
        }
        return base.BeginFirebolt(target);
    }
}
