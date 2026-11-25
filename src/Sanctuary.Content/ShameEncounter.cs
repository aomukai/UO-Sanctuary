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
    Skeleton, Daemon, Unassigned
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
    internal override int? AuraRankOverride => Role switch { ShameRole.Moissanite => 10, ShameRole.Diamond => 5, _ => null };

    private static DungeonProfile Species(ShameRole role, string name, int min, int max, int body,
        MonsterArchetype archetype = MonsterArchetype.Melee, int fire = 0, int cold = 0) =>
        new($"shame-{role.ToString().ToLowerInvariant()}", "Shame", name, archetype, "Shame excavations",
            min - BossLevelBonus(role), max - BossLevelBonus(role), body, body switch { 14 => 268, 13 => 655, 15 => 838, 16 => 278, 50 => 451, 9 => 357, _ => 0 },
            new(0, 0, fire, 0, cold, 0));

    private static int BossLevelBonus(ShameRole role) => role is >= ShameRole.BoneCaptain and <= ShameRole.FleetCaptain
        ? EncounterProfiles.Level(0, EncounterGrade.Unique) : 0;

    [Constructible]
    public ShameEncounter(string role) : this(
        Enum.TryParse<ShameRole>(role, true, out var parsed) && parsed != ShameRole.Unassigned && Enum.IsDefined(parsed)
            ? parsed : throw new ArgumentException("Unknown Shame encounter role.", nameof(role))) { }

    internal ShameEncounter(ShameRole role, ShameExpedition expedition = null, int stage = 0)
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
        Assign(species, Utility.RandomMinMax(species.MinimumLevel, species.MaximumLevel), MonsterDifficulty.Normal,
            boss ? EncounterGrade.Unique : EncounterGrade.Ordinary, ChampionKind.Champion, modifiers, aura, species.Name, Random.Shared);
        Hue = role switch
        {
            ShameRole.Silicon => 0x455, ShameRole.Chrysoberyl => 0x59A, ShameRole.Corundum => 0x489,
            ShameRole.Tungsten => 0x966, ShameRole.Titanium => 0x47E, ShameRole.Moissanite => 0x486,
            ShameRole.Stishovite => 0x47F, ShameRole.Diamond => 0x481,
            _ => Hue
        };
        if (species.Body == 400)
            Dress(this, role);
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
        creature.Hue = ghost ? 0x482 : 0x83EA;
        creature.HairItemID = 0x203C;
        creature.HairHue = ghost ? 0x482 : 0x455;
        var shirt = new FancyShirt(ghost ? 0x482 : 0x47E);
        var pants = new LongPants(ghost ? 0x482 : 0x455);
        var boots = new Boots(ghost ? 0x482 : 0x455);
        creature.AddItem(shirt);
        creature.AddItem(pants);
        creature.AddItem(boots);
        if (role is ShameRole.Researcher or ShameRole.Tidesinger or ShameRole.Stormcaller)
            creature.AddItem(new Robe(role == ShameRole.Researcher ? 0x497 : 0x59B));
        else if (role is ShameRole.BoardingCaptain or ShameRole.FleetCaptain)
            creature.AddItem(new TricorneHat(ghost ? 0x482 : 0x489));
        else
            creature.AddItem(new Bandana(ghost ? 0x482 : 0x21));
        if (role == ShameRole.Corsair)
            creature.AddItem(new Crossbow { Movable = false });
        else if (creature is ShameEncounter && role != ShameRole.Researcher)
            creature.AddItem(new Cutlass { Hue = ghost ? 0x482 : 0, Movable = false });
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

    private static readonly (CombatAbility, int)[] _tungsten = [(CombatAbility.MoltenBoulder, 10)];
    private static readonly (CombatAbility, int)[] _titanium = [(CombatAbility.Weaken, 10)];
    private static readonly (CombatAbility, int)[] _pirate = [(CombatAbility.DimVision, 10)];
    private static readonly (CombatAbility, int)[] _tidesinger = [(CombatAbility.GlacialSpike, 20), (CombatAbility.Blizzard, 20)];
    private static readonly (CombatAbility, int)[] _stishovite = [(CombatAbility.Decrepify, 10)];
    private static readonly (CombatAbility, int)[] _stormcaller = [(CombatAbility.Lightning, 15), (CombatAbility.Nova, 10)];
    private static readonly (CombatAbility, int)[] _wind = [(CombatAbility.Nova, 20)];
    private static readonly (CombatAbility, int)[] _fire = [(CombatAbility.FireWall, 20)];
    private static readonly (CombatAbility, int)[] _water = [(CombatAbility.GlacialSpike, 20)];
    private static readonly (CombatAbility, int)[] _earth = [(CombatAbility.Fissure, 20)];
    private static readonly (CombatAbility, int)[] _researcher = [(CombatAbility.Lightning, 12), (CombatAbility.Fireball, 12)];
    private static readonly (CombatAbility, int)[] _mate = [(CombatAbility.LowerResist, 10)];
    private static readonly (CombatAbility, int)[] _mage = [(CombatAbility.Volcano, 20), (CombatAbility.Armageddon, 10)];

    private static ReadOnlySpan<(CombatAbility Ability, int Rank)> Spells(ShameRole role) => role switch
    {
        ShameRole.Tungsten => _tungsten, ShameRole.Titanium => _titanium, ShameRole.Pirate => _pirate,
        ShameRole.Tidesinger => _tidesinger, ShameRole.Stishovite => _stishovite, ShameRole.Stormcaller => _stormcaller,
        ShameRole.Wind => _wind, ShameRole.Fire => _fire, ShameRole.Water => _water, ShameRole.Earth => _earth,
        ShameRole.Researcher => _researcher, ShameRole.BickeringMate => _mate, ShameRole.BickeringMage => _mage,
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
        var (ability, rank) = spells[spells.Length == 2 && _alternate ? 1 : 0];
        Alternate = !_alternate;
        int generation = _spellGeneration;
        ShameCombat.Cast(this, target, ability, rank, () => generation == _spellGeneration);
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
            ShameCombat.Cast(this, from, CombatAbility.FrostNova, 10, () => Alive && !Deleted);
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
