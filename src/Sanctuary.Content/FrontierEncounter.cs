// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class FrontierEncounter : EncounterCreature
{
    private const string Skeleton = "frontier-skeleton", Zombie = "frontier-zombie", Lich = "frontier-lich";
    private const string VesperUndead = "vesper-undead", Spectre = "vesper-spectre", Wraith = "vesper-wraith",
        Ghoul = "vesper-ghoul", SkeletalKnight = "vesper-skeletal-knight", Mummy = "vesper-mummy";
    private static readonly string[] _vesperGraveyardUndead = [Skeleton, Zombie, Spectre, Wraith, Ghoul];
    private const string Ettin = "frontier-ettin", Drake = "frontier-drake", Wyvern = "frontier-wyvern", Dragon = "frontier-dragon";
    internal const string BoulderEttinId = "boulder-ettin";
    private const string BridgeTroll = "bridge-troll";
    private const string Lizardman = "fens-lizardman", Alligator = "fens-alligator", Slime = "fens-slime",
        Bogling = "fens-bogling", BogThing = "fens-bog-thing", PlagueBeast = "fens-plague-beast", SwampDragon = "fens-swamp-dragon";
    internal const string DespiseEttin = "despise-ettin", DespiseRatman = "despise-ratman";
    internal const string DespiseTroll = "despise-troll", DespiseOgre = "despise-ogre", DespiseOgreLord = "despise-ogre-lord";
    private static readonly Dictionary<string, DungeonProfile> _species = new(StringComparer.Ordinal)
    {
        [Skeleton] = Species(Skeleton, "a skeleton", MonsterArchetype.Melee, 12, 20, 50, 451, new(Poison: 50)),
        [Zombie] = Species(Zombie, "a zombie", MonsterArchetype.Tank, 12, 20, 3, 471, new(Poison: 50)),
        [Lich] = Species(Lich, "a cemetery lich", MonsterArchetype.Caster, 16, 20, 24, 0x3E9, new(Cold: 40, Poison: 75)),
        [Spectre] = Species(Spectre, "a spectre", MonsterArchetype.Caster, 12, 16, 26, 0x482, new(Cold: 35, Poison: 50)),
        [Wraith] = Species(Wraith, "a wraith", MonsterArchetype.Caster, 12, 16, 26, 0x482, new(Cold: 50, Poison: 50)),
        [Ghoul] = Species(Ghoul, "a ghoul", MonsterArchetype.Melee, 12, 16, 153, 0x482, new(Poison: 50)),
        [SkeletalKnight] = Species(SkeletalKnight, "a skeletal knight", MonsterArchetype.Tank, 15, 18, 147, 451,
            new(Physical: 15, Poison: 50)),
        [Mummy] = Species(Mummy, "a mummy", MonsterArchetype.Tank, 15, 18, 154, 471, new(Poison: 50)),
        [Ettin] = Species(Ettin, "an ettin", MonsterArchetype.Tank, 14, 20, 18, 367, new(Physical: 10)),
        [BoulderEttinId] = Species(BoulderEttinId, "a boulder-throwing ettin", MonsterArchetype.Tank, 14, 18, 18, 367,
            new(Physical: 10)),
        [BridgeTroll] = Species(BridgeTroll, "a troll", MonsterArchetype.Melee, 15, 15, 53, 461, new(Physical: 10)),
        [DespiseEttin] = Species(DespiseEttin, "an ettin", MonsterArchetype.Tank, 18, 22, 18, 367, new(Physical: 10)),
        [DespiseRatman] = Species(DespiseRatman, "a ratman", MonsterArchetype.Melee, 12, 16, 42, 437, new()),
        [DespiseTroll] = Species(DespiseTroll, "a troll", MonsterArchetype.Melee, 20, 25, 53, 461, new(Physical: 10)),
        [DespiseOgre] = Species(DespiseOgre, "an ogre", MonsterArchetype.Tank, 20, 25, 1, 427, new(Physical: 15)),
        [DespiseOgreLord] = Species(DespiseOgreLord, "an ogre lord", MonsterArchetype.Tank, 28, 28, 83, 427, new(Physical: 20)),
        [Drake] = Species(Drake, "a drake", MonsterArchetype.Caster, 16, 22, 61, 362, new(Fire: 20)),
        [Wyvern] = Species(Wyvern, "a wyvern", MonsterArchetype.Melee, 16, 22, 62, 362, new(Poison: 70)),
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
    private TimerExecutionToken _inferno;

    internal static DungeonProfile FindSpecies(string id) => id == null ? null : _species.GetValueOrDefault(id);
    private static DungeonProfile Species(string id, string name, MonsterArchetype archetype, int min, int max, int body, int sound,
        CreatureResistances resistances) => new(id, id, name, archetype, "Britannia frontier", min, max, body, sound,
        new(resistances.Physical ?? 0, 0, resistances.Fire ?? 0, 0, resistances.Cold ?? 0, resistances.Poison ?? 0));

    [Constructible]
    public FrontierEncounter(string id, int level, int modifierCount) : this(id, level, modifierCount, null)
    {
    }

    // Vesper's spawners roll a fresh species and grade for each vacancy.
    [Constructible]
    public FrontierEncounter(string id, int level) : this(
        id == VesperUndead ? _vesperGraveyardUndead[Utility.Random(_vesperGraveyardUndead.Length)] : id,
        level, 0, RollVesperGrade())
    {
    }

    private FrontierEncounter(string id, int level, int modifierCount, EncounterGrade? rolledGrade)
    {
        var profile = FindSpecies(id) ?? throw new ArgumentException("Unknown frontier species.", nameof(id));
        if (id == Dragon ? modifierCount != 2 : id == DespiseOgreLord ? modifierCount != 0 : modifierCount is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(modifierCount));

        const MonsterDifficulty difficulty = MonsterDifficulty.Normal;
        var grade = rolledGrade ?? (id == Dragon ? EncounterGrade.SuperUnique : id == DespiseOgreLord ? EncounterGrade.Champion
            : modifierCount > 0 ? EncounterGrade.Unique : EncounterGrade.Ordinary);
        int baseLevel = level - EncounterProfiles.Level(0, grade);
        if (rolledGrade.HasValue)
            baseLevel = Math.Max(baseLevel, profile.MinimumLevel);
        int maxLevel = id is Dragon or DespiseOgreLord ? 30 : profile.MaximumLevel;
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
            DespiseOgreLord => "an ogre lord champion",
            _ => null
        };
        Assign(profile, baseLevel, difficulty, grade, ChampionKind.Champion, modifiers, aura, name, Random.Shared);
        if (grade == EncounterGrade.Ordinary)
        {
            if (id == Spectre)
                Hue = 0x4001;
            else if (id == Wraith)
                Hue = 0x04E3;
        }
        if (id == Lich)
            CantWalk = true;
        ApplyPerception();
    }

    private static EncounterGrade RollVesperGrade()
    {
        int roll = Utility.Random(100);
        if (roll >= 10 || World.Mobiles.Values.OfType<FrontierEncounter>().Any(mob =>
            !mob.Deleted && mob.Alive && mob.Grade is (EncounterGrade.Champion or EncounterGrade.Unique) && mob.VesperGraveyardDead))
            return EncounterGrade.Ordinary;
        return roll < 3 ? EncounterGrade.Unique : EncounterGrade.Champion;
    }

    private bool PlayersFive => Dungeon.Id is Drake or Wyvern or Dragon or DespiseEttin or DespiseRatman
        or DespiseTroll or DespiseOgre or DespiseOgreLord;
    private bool RestrictedRange => (PlayersFive && Dungeon.Id != Dragon) || Dungeon.Id == BridgeTroll
        || Dungeon.Id.StartsWith("fens-", StringComparison.Ordinal);
    private bool BritainCemeteryDead => (Dungeon.Id is Skeleton or Zombie) && HomeMap == Map.Trammel
        && Home.X is >= 1373 and <= 1409 && Home.Y is >= 1476 and <= 1505;
    private bool VesperGraveyardDead => Undead && HomeMap == Map.Trammel
        && Home.X is >= 2742 and <= 2782 && Home.Y is >= 840 and <= 894;
    internal override int EncounterPlayers => PlayersFive ? 5 : 3;
    internal override int HomeLeashRadius => BritainCemeteryDead || VesperGraveyardDead ? 7 : RestrictedRange ? 10 : base.HomeLeashRadius;
    public override int AcquireOnApproachRange => Math.Min(base.AcquireOnApproachRange, RangePerception);

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
        else if (RestrictedRange)
            RangePerception = 7;
    }
    protected override ResistanceType MeleeElement => Dungeon.Id is Wyvern or Slime or BogThing or PlagueBeast
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
        DespiseOgre => "an ogre's corpse",
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
        if (_inferno.Running)
            return false;
        if (Dungeon.Id is not (Drake or Dragon) || Core.TickCount < _infernoAfter || !ActionReady
            || !EliteCombat.CanAct(this) || CreatureHitRecovery.Remaining(this) > 0
            || !EliteCombat.Enemy(this, target, 5) || target.X == X && target.Y == Y)
            return base.BeginFirebolt(target);

        var origin = Location;
        var map = Map;
        int dx = target.X - X, dy = target.Y - Y;
        int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        double stepX = dx / (double)distance, stepY = dy / (double)distance;
        _infernoAfter = Core.TickCount + 7500;
        StartAction(target);
        Direction = GetDirectionTo(target);
        Animate(4, 5, 1, true, false, 0);
        int phase = 0;
        Timer.StartTimer(TimeSpan.FromMilliseconds(520), TimeSpan.FromMilliseconds(180), 4,
            () => InfernoPulse(map, origin, stepX, stepY, phase++), out _inferno);
        return true;
    }

    private void InfernoPulse(Map map, Point3D origin, double stepX, double stepY, int phase)
    {
        if (Map != map || Location != origin || ReturningHome || !EliteCombat.CanAct(this)
            || !NamedControl.SpecialAllowed(this) || CreatureHitRecovery.Remaining(this) > 0)
        {
            _inferno.Cancel();
            return;
        }
        if (phase == 0)
            PlaySound(SpellEffectSounds.InfernoJet);

        var previous = origin;
        var attack = EncounterProfile.Attack1;
        int graphic = SorceressVisuals.DirectionalFrame(SpellEffectGraphics.InfernoFlight, stepX, stepY, phase);
        for (int i = 1; i <= 5; i++)
        {
            var point = new Point3D(origin.X + (int)Math.Round(stepX * i), origin.Y + (int)Math.Round(stepY * i), origin.Z);
            if (!EncounterProfiles.ClearSight(map, previous, point))
                break;

            Effects.SendLocationEffect(point, map, graphic, SpellEffectGraphics.FireFrames);
            foreach (var target in map.GetMobilesInRange<Mobile>(point, 0))
            {
                if (EliteCombat.Enemy(this, target, 5))
                    EliteCombat.Damage(this, target, fire: Utility.RandomMinMax(attack.MinDamage.Value, attack.MaxDamage.Value) / 3.0);
            }
            previous = point;
        }
    }

    internal override void StopCombatActions()
    {
        _inferno.Cancel();
        base.StopCombatActions();
    }
}
