// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

internal enum ShameRole
{
    Silicon, Chrysoberyl, Corundum, Tungsten,
    Titanium, Moissanite, Pirate, Tidesinger,
    Stishovite, Diamond, Stormcaller, Corsair,
    Wind, Fire, Water, Earth,
    Researcher, Deckhand, Shipboy, BoardingCaptain,
    BoneCaptain, DaemonCaptain, BickeringMate, BickeringMage, FleetCaptain,
    Skeleton, Daemon, Unassigned,
    CrazedResearcher, FanaticExperimenter
}

[SerializationGenerator(0)]
public partial class ShameEncounter : EncounterCreature
{
    private static readonly Dictionary<ShameRole, DungeonProfile> _roster = new()
    {
        [ShameRole.Silicon] = Species(ShameRole.Silicon, "a silicon elemental", 20, 22, 14),
        [ShameRole.Chrysoberyl] = Species(ShameRole.Chrysoberyl, "a chrysoberyl elemental", 21, 23, 14),
        [ShameRole.Corundum] = Species(ShameRole.Corundum, "a corundum elemental", 22, 24, 14),
        [ShameRole.Tungsten] = Species(ShameRole.Tungsten, "a tungsten elemental", 23, 25, 14),
        [ShameRole.Titanium] = Species(ShameRole.Titanium, "a titanium elemental", 24, 26, 14),
        [ShameRole.Moissanite] = Species(ShameRole.Moissanite, "a moissanite elemental", 25, 27, 14),
        [ShameRole.Pirate] = Species(ShameRole.Pirate, "a pirate", 26, 28, 400),
        [ShameRole.Tidesinger] = Species(ShameRole.Tidesinger, "a pirate tidesinger", 27, 29, 400, MonsterArchetype.Caster),
        [ShameRole.Stishovite] = Species(ShameRole.Stishovite, "a stishovite elemental", 28, 30, 14),
        [ShameRole.Diamond] = Species(ShameRole.Diamond, "a diamond elemental", 29, 31, 14),
        [ShameRole.Stormcaller] = Species(ShameRole.Stormcaller, "a pirate stormcaller", 30, 32, 400, MonsterArchetype.Caster),
        [ShameRole.Corsair] = Species(ShameRole.Corsair, "a corsair", 31, 33, 400, MonsterArchetype.Ranged),
        [ShameRole.Wind] = Species(ShameRole.Wind, "a furious wind elemental", 38, 40, 13),
        [ShameRole.Fire] = Species(ShameRole.Fire, "an incensed fire elemental", 38, 40, 15, fire: 75),
        [ShameRole.Water] = Species(ShameRole.Water, "a frenzied water elemental", 38, 40, 16, cold: 75),
        [ShameRole.Earth] = Species(ShameRole.Earth, "an enraged earth elemental", 38, 40, 14),
        [ShameRole.Researcher] = Species(ShameRole.Researcher, "a mineral researcher", 30, 33, 400, MonsterArchetype.Caster),
        [ShameRole.CrazedResearcher] = Species(ShameRole.CrazedResearcher, "a crazed researcher", 33, 33, 400, MonsterArchetype.Caster),
        [ShameRole.FanaticExperimenter] = Species(ShameRole.FanaticExperimenter, "a fanatic experimenter", 35, 35, 400, MonsterArchetype.Caster),
        [ShameRole.Deckhand] = Species(ShameRole.Deckhand, "a pirate deckhand", 35, 35, 400),
        [ShameRole.Shipboy] = Species(ShameRole.Shipboy, "a shipboy", 35, 35, 400),
        [ShameRole.BoardingCaptain] = Species(ShameRole.BoardingCaptain, "the pirate captain", 35, 35, 400),
        [ShameRole.BoneCaptain] = Species(ShameRole.BoneCaptain, "Captain Keelbone", 34, 34, 400),
        [ShameRole.DaemonCaptain] = Species(ShameRole.DaemonCaptain, "Captain Brine", 35, 35, 400),
        [ShameRole.BickeringMate] = Species(ShameRole.BickeringMate, "Captain Cutwater", 36, 36, 400),
        [ShameRole.BickeringMage] = Species(ShameRole.BickeringMage, "Captain Cinders", 36, 36, 400, MonsterArchetype.Caster),
        [ShameRole.FleetCaptain] = Species(ShameRole.FleetCaptain, "the fleet captain", 38, 38, 400),
        [ShameRole.Skeleton] = Species(ShameRole.Skeleton, "a drowned skeleton", 25, 25, 50),
        [ShameRole.Daemon] = Species(ShameRole.Daemon, "a bound daemon", 35, 35, 9)
    };
    private static readonly Dictionary<string, ShameRole> _roles =
        _roster.ToDictionary(entry => entry.Value.Id, entry => entry.Key, StringComparer.Ordinal);

    [SerializableField(0)] private Serial _expedition;
    [SerializableField(1)] private int _stage;
    [SerializableField(2)] private DateTime _summonAfter;
    [SerializableField(3)] private bool _alternate;

    private const int PirateSkin = 0x83EA, PirateHair = 0x455, PirateShirt = 0x47E;
    private const int PirateTrousers = 0x455, PirateBoots = 0x455, PirateBandana = 0x21;
    private const int GhostCaptainHat = 0x497;

    private long _nextSpell;
    private int _spellGeneration;

    internal static DungeonProfile FindSpecies(string id) => _roles.TryGetValue(id, out var role) ? _roster[role] : null;
    internal ShameRole Role => _roles.GetValueOrDefault(Dungeon.Id, ShameRole.Unassigned);
    internal bool IsExpeditionActor => _expedition != Serial.Zero;
    internal bool IsSummonedCrew => Role is ShameRole.Skeleton or ShameRole.Daemon;
    private bool CastingRole => Spells(Role).Length != 0 || Role is ShameRole.Chrysoberyl or ShameRole.Diamond or ShameRole.Corsair;
    protected override bool CanCastEncounterBolt => CastingRole;
    protected override bool BusyWithSpecial => SpellInProgress;
    protected override int EncounterBoltRange => 9;
    protected override int EncounterCastAnimation => Body == 400 ? 16 : base.EncounterCastAnimation;
    public override Poison PoisonImmune => null;
    public override bool BleedImmune => Role is >= ShameRole.BoneCaptain and <= ShameRole.Skeleton;
    public override OppositionGroup OppositionGroup => null;
    public override bool AlwaysMurderer => true;
    public override string CorpseName => $"{Name}'s corpse";

    private static DungeonProfile Species(ShameRole role, string name, int min, int max, int body,
        MonsterArchetype archetype = MonsterArchetype.Melee, int fire = 0, int cold = 0) =>
        new($"shame-{role.ToString().ToLowerInvariant()}", "Shame", name, archetype, "Shame excavations",
            min - BossLevelBonus(role), max - BossLevelBonus(role), body, body switch { 14 => 268, 13 => 655, 15 => 838, 16 => 278, 50 => 451, 9 => 357, _ => 0 },
            new(0, 0, fire, 0, cold, 0));

    private static int BossLevelBonus(ShameRole role) => role is >= ShameRole.BoneCaptain and <= ShameRole.FleetCaptain
        ? EncounterProfiles.Level(0, EncounterGrade.Unique) : 0;

    [Constructible]
    public ShameEncounter(string role) : this(ParseRole(role)) { }

    [Constructible]
    public ShameEncounter(string role, int paragonChance)
        : this(ParseRole(role), paragon: RollParagon(paragonChance)) => ParagonAlreadyRolled = true;

    private static ShameRole ParseRole(string role) =>
        Enum.TryParse<ShameRole>(role, true, out var parsed) && parsed != ShameRole.Unassigned && Enum.IsDefined(parsed)
            ? parsed : throw new ArgumentException("Unknown Shame encounter role.", nameof(role));

    private static bool RollParagon(int chance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(chance);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(chance, 100);
        return Utility.Random(100) < ParagonBalance.SpawnChance(chance);
    }

    private static bool LabMage(ShameRole role) => role is ShameRole.Researcher or ShameRole.CrazedResearcher
        or ShameRole.FanaticExperimenter;

    // Paragons keep the same level band as their normal counterparts.
    protected override int EncounterLevel(int baseLevel) => Grade == EncounterGrade.Champion
        ? baseLevel : base.EncounterLevel(baseLevel);

    internal ShameEncounter(ShameRole role, ShameExpedition expedition = null, int stage = 0, bool paragon = false)
    {
        var species = _roster[role];
        var modifiers = role switch
        {
            ShameRole.Wind => new[] { EliteModifier.ExtraFast },
            ShameRole.Fire => [EliteModifier.ExtraStrong],
            ShameRole.Earth or ShameRole.BoneCaptain => [EliteModifier.StoneSkin],
            _ => Array.Empty<EliteModifier>()
        };
        var aura = role switch { ShameRole.Moissanite => EliteAura.Might, ShameRole.Diamond => EliteAura.Vigor, _ => EliteAura.None };
        bool boss = role is >= ShameRole.BoneCaptain and <= ShameRole.FleetCaptain;
        bool canBeParagon = role is <= ShameRole.Earth or ShameRole.CrazedResearcher or ShameRole.FanaticExperimenter;
        var grade = boss ? EncounterGrade.Unique : paragon && canBeParagon
            ? EncounterGrade.Champion : EncounterGrade.Ordinary;
        Assign(species, Utility.RandomMinMax(species.MinimumLevel, species.MaximumLevel), MonsterDifficulty.Normal,
            grade, ChampionKind.Champion, modifiers, aura, species.Name, Random.Shared);
        Hue = role switch
        {
            ShameRole.Silicon => 0x455, ShameRole.Chrysoberyl => 0x59A, ShameRole.Corundum => 0x489,
            ShameRole.Tungsten => 0x966, ShameRole.Titanium => 0x47E, ShameRole.Moissanite => 0x486,
            ShameRole.Stishovite => 0x47F, ShameRole.Diamond => 0x481,
            _ => Hue
        };
        if (species.Body == 400)
            Dress(this, role);
        if (grade == EncounterGrade.Champion)
            Hue = ParagonBodyHue;
        if (expedition != null)
        {
            Expedition = expedition.Serial;
            Stage = stage;
        }

        NoKillAwards = IsSummonedCrew;
    }

    internal static void Dress(BaseCreature creature, ShameRole role)
    {
        bool ghost = role is >= ShameRole.BoneCaptain and <= ShameRole.FleetCaptain;
        creature.Body = 400;
        creature.Hue = PirateSkin;
        creature.HairItemID = 0x203C;
        creature.HairHue = PirateHair;
        var shirt = new FancyShirt(PirateShirt);
        var pants = new LongPants(PirateTrousers);
        var boots = new Boots(PirateBoots);
        creature.AddItem(shirt);
        creature.AddItem(pants);
        creature.AddItem(boots);
        if (LabMage(role) || role is ShameRole.Tidesinger or ShameRole.Stormcaller)
            creature.AddItem(new Robe(role switch { ShameRole.FanaticExperimenter => 0x21, _ when LabMage(role) => 0x497, _ => 0x59B }));
        else if (role is ShameRole.BoardingCaptain or ShameRole.FleetCaptain)
            creature.AddItem(new TricorneHat(ghost ? GhostCaptainHat : 0x489));
        else
            creature.AddItem(new Bandana(PirateBandana));
        if (role == ShameRole.Corsair)
            creature.AddItem(new Crossbow { Movable = false });
        else if (creature is ShameEncounter && !LabMage(role))
            creature.AddItem(new Cutlass { Movable = false });
    }

    [AfterDeserialization]
    private void RestoreBossCostume()
    {
        if (Role is >= ShameRole.BoneCaptain and <= ShameRole.FleetCaptain)
            RestoreGhostCostumeAfterLoad(this);
    }

    internal static void RestoreGhostCostumeAfterLoad(BaseCreature creature) =>
        Timer.DelayCall(() => RestoreGhostCostume(creature));

    private static void RestoreGhostCostume(BaseCreature creature)
    {
        if (creature.Deleted)
            return;
        if (creature.HairHue == ParagonBodyHue)
        {
            creature.Hue = PirateSkin;
            creature.HairHue = PirateHair;
        }
        foreach (var item in creature.Items)
        {
            if (item.Hue != ParagonBodyHue)
                continue;
            item.Hue = item switch
            {
                FancyShirt => PirateShirt, LongPants => PirateTrousers, Boots => PirateBoots,
                TricorneHat => GhostCaptainHat, Bandana => PirateBandana, Cutlass => 0, _ => item.Hue
            };
        }
    }

    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon)
    {
        var role = _roles.GetValueOrDefault(dungeon.Id);
        return new(dungeon.Family, role is >= ShameRole.BoneCaptain and <= ShameRole.Skeleton,
            role == ShameRole.Daemon, false, role is >= ShameRole.BoneCaptain and <= ShameRole.FleetCaptain,
            true, 1, 0, 0, 100, 0);
    }

    protected override int[] AdjustResistances(int[] resistances)
    {
        if (Role == ShameRole.BoneCaptain)
            Array.Fill(resistances, 75);
        return resistances;
    }

    internal override bool IsEnemyIgnoringLeash(Mobile target) => base.IsEnemyIgnoringLeash(target)
        && (!IsExpeditionActor || ShameExpedition.AllowsCombat(this, target));

    public override void Damage(int amount, Mobile from = null, bool informMount = true, bool ignoreEvilOmen = false)
    {
        if (!IsExpeditionActor || from == null || ShameExpedition.AllowsCombat(this, from))
            base.Damage(amount, from, informMount, ignoreEvilOmen);
    }

    public override bool Move(Direction direction)
    {
        if (IsExpeditionActor && Stage == 0)
        {
            var end = Location;
            Server.Movement.Movement.Offset(direction, ref end);
            if (!ShamePopulation.OnMainDeck(end, Map) || !Map.CanFit(end, 16, checkMobiles: false))
                return false;
        }
        return base.Move(direction);
    }

    private static ReadOnlySpan<CombatAbility> Spells(ShameRole role) => role switch
    {
        ShameRole.Tungsten => [CombatAbility.MoltenBoulder],
        ShameRole.Titanium => [CombatAbility.Weaken],
        ShameRole.Pirate => [CombatAbility.DimVision],
        ShameRole.Tidesinger => [CombatAbility.GlacialSpike, CombatAbility.Blizzard],
        ShameRole.Stishovite => [CombatAbility.Decrepify],
        ShameRole.Stormcaller => [CombatAbility.Lightning, CombatAbility.Nova],
        ShameRole.Wind => [CombatAbility.Nova],
        ShameRole.Fire => [CombatAbility.FireWall],
        ShameRole.Water => [CombatAbility.GlacialSpike],
        ShameRole.Earth => [CombatAbility.Fissure],
        ShameRole.Researcher => [CombatAbility.Lightning, CombatAbility.Fireball],
        ShameRole.BickeringMate => [CombatAbility.LowerResist],
        ShameRole.BickeringMage => [CombatAbility.Volcano, CombatAbility.Armageddon],
        ShameRole.CrazedResearcher => [CombatAbility.Twister, CombatAbility.Tornado],
        ShameRole.FanaticExperimenter => [CombatAbility.Hurricane, CombatAbility.Firestorm],
        _ => []
    };

    public override bool BeginFirebolt(Mobile target)
    {
        if (ReturningHome || Core.TickCount < _nextSpell || !NamedControl.SpecialAllowed(this)
            || !base.BeginFirebolt(target))
            return false;
        _nextSpell = Core.TickCount + (Role == ShameRole.Corsair ? 2700 : 6000);
        return true;
    }

    public override void OnThink()
    {
        if (Combatant is { } target && EliteCombat.Enemy(this, target, EncounterBoltRange))
        {
            if (!Caster && CastingRole)
                BeginFirebolt(target);
            if (IsExpeditionActor && Role is ShameRole.BoneCaptain or ShameRole.DaemonCaptain
                && Core.Now >= _summonAfter && EliteCombat.CanAct(this))
            {
                SummonAfter = Core.Now.AddSeconds(10);
                ShameExpedition.Summon(this);
            }
        }
        base.OnThink();
    }

    protected override void ReleaseEncounterBolt(Mobile target)
    {
        if (Role is ShameRole.Chrysoberyl or ShameRole.Diamond or ShameRole.Corsair)
        {
            ShameCombat.Throw(this, target);
            return;
        }

        var spells = Spells(Role);
        if (spells.IsEmpty)
            return;
        var ability = spells[spells.Length == 2 && _alternate ? 1 : 0];
        Alternate = !_alternate;
        int generation = _spellGeneration;
        ShameCombat.Cast(this, target, ability, () => generation == _spellGeneration);
    }

    protected override void Deal(Mobile target, int damage, bool fire)
    {
        base.Deal(target, damage, fire);
        if (!target.Alive || !EliteCombat.Enemy(this, target, 2))
            return;
        if (Role == ShameRole.Tungsten)
        {
            ShameCombat.Knockback(this, target);
            CombatStuns.ApplyFamiliar(target);
        }
        else if (Role == ShameRole.FleetCaptain)
        {
            if (Utility.Random(100) < 10)
                NamedCombat.ApplyCrushing(target, false);
            TimedEffects.Bleed(this, target, NamedCombat.OpenWoundsDamage(Basis.Level, target, false), TimedEffects.Now);
            ShameCombat.Knockback(this, target);
        }
    }

    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        base.OnDamage(amount, from, willKill);
        if (willKill || amount <= 0 || !EliteCombat.Enemy(this, from, 12))
            return;
        if (Role == ShameRole.Water && Utility.Random(100) < 20)
            ShameCombat.Sidestep(this);
        else if (Role == ShameRole.FleetCaptain && Utility.Random(100) < 10)
            ShameCombat.Cast(this, from, CombatAbility.FrostNova, () => Alive && !Deleted);
    }

    public override void OnGotMeleeAttack(Mobile attacker, int damage)
    {
        base.OnGotMeleeAttack(attacker, damage);
        if (Role == ShameRole.Corundum && damage > 0)
            ClayGolemSlow.Incoming(attacker, this, ranged: false, TimedEffects.Now);
    }

    public override void SendAppearanceTo(NetState state)
    {
        if (Role is >= ShameRole.BoneCaptain and <= ShameRole.FleetCaptain)
            SkeletonAppearancePackets.Send(state, Serial,
                Role == ShameRole.FleetCaptain ? SkeletonAppearance.FleetCaptain : SkeletonAppearance.ShameGhost);
    }

    internal override void StopCombatActions()
    {
        _spellGeneration++;
        base.StopCombatActions();
    }

    public override void OnDeath(Container corpse)
    {
        var credited = Role == ShameRole.FleetCaptain ? ClassLoot.Killer(this) : null;
        if (IsExpeditionActor && corpse is Corpse expeditionCorpse)
            expeditionCorpse.SanctuaryRestInPeace = true;
        base.OnDeath(corpse);
        if (Role == ShameRole.Shipboy && corpse is { Deleted: false })
            corpse.DropItem(new ShameClue(ShameClueKind.Manifest));
        if (IsExpeditionActor)
            ShameExpedition.Died(this, corpse, credited);
    }
}
