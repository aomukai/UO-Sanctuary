// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal enum DeceitRole
{
    Unassigned = -1,
    Slave, DemonicTaskmaster, WatchfulOverseer, DevoutFollower, Familiar, NecromancerApprentice,
    CultistEnslaver, DemonicTorturer, Necromancer, Seductress, InfernalMistress, Bishop, Skeleton
}

public sealed record DeceitAllySnapshot(string Species, int Level, int RaceIndex, bool Female, int Hue);

[SerializationGenerator(0)]
public partial class DeceitEncounter : EncounterCreature
{
    private const int Black = 0x0455;
    private const int SupportRange = 8;
    private const int SkeletonLimit = 5;
    private static readonly string[] _freedLines =
    [
        "Where… what have I done?", "Their voices… they’re gone!",
        "I can move… I can move!", "Please—let me leave!"
    ];

    private static readonly Dictionary<DeceitRole, DungeonProfile> _roster = new()
    {
        [DeceitRole.Slave] = Species(DeceitRole.Slave, "a slave", 35, 37, 0x190, 0),
        [DeceitRole.DemonicTaskmaster] = Species(DeceitRole.DemonicTaskmaster, "a demonic taskmaster", 37, 39, 4, 372),
        [DeceitRole.WatchfulOverseer] = Species(DeceitRole.WatchfulOverseer, "a watchful overseer", 39, 41, 22, 377, MonsterArchetype.Caster),
        [DeceitRole.DevoutFollower] = Species(DeceitRole.DevoutFollower, "a devout follower", 39, 41, 0x190, 0, MonsterArchetype.Caster),
        [DeceitRole.Familiar] = Species(DeceitRole.Familiar, "a familiar", 41, 41, 776, 357),
        [DeceitRole.NecromancerApprentice] = Species(DeceitRole.NecromancerApprentice, "a necromancer apprentice", 41, 43, 0x190, 0, MonsterArchetype.Caster),
        [DeceitRole.CultistEnslaver] = Species(DeceitRole.CultistEnslaver, "a cultist enslaver", 43, 45, 0x190, 0),
        [DeceitRole.DemonicTorturer] = Species(DeceitRole.DemonicTorturer, "a demonic torturer", 45, 47, 746, 0x165),
        [DeceitRole.Necromancer] = Species(DeceitRole.Necromancer, "a necromancer", 47, 49, 24, 0x3E9, MonsterArchetype.Caster),
        [DeceitRole.Seductress] = Species(DeceitRole.Seductress, "a seductress", 49, 51, 149, 0x4B0, MonsterArchetype.Caster),
        [DeceitRole.InfernalMistress] = Species(DeceitRole.InfernalMistress, "an infernal mistress", 51, 53, 149, 0x4B0, MonsterArchetype.Caster),
        [DeceitRole.Bishop] = Species(DeceitRole.Bishop, "a bishop", 53, 54, 0x190, 0, MonsterArchetype.Caster),
        [DeceitRole.Skeleton] = Species(DeceitRole.Skeleton, "a summoned skeleton", 25, 25, 56, 0x48D)
    };
    private static readonly Dictionary<string, DeceitRole> _roles =
        _roster.ToDictionary(entry => entry.Value.Id, entry => entry.Key, StringComparer.Ordinal);

    [SerializableField(0, setter: "private")] private List<DeceitEncounter> _skeletons = [];
    [SerializableField(1, setter: "private")] private DateTime _nextSkeleton;
    [SerializableField(2, setter: "private")] private DateTime _escapeUntil;
    [SerializableField(3, setter: "private")] private Direction _escapeHeading;

    private long _nextSpell, _nextHeal;
    private int _spellIndex;
    private TimerExecutionToken _escape;

    internal DeceitRole Role => _roles.GetValueOrDefault(Dungeon.Id, DeceitRole.Unassigned);
    internal bool Freed => _escapeUntil != default;
    private bool IsPerson => Dungeon.Body == 0x190;
    private static bool IsUndead(DeceitRole role) => role is DeceitRole.Necromancer or DeceitRole.Skeleton;

    private static DungeonProfile Species(DeceitRole role, string name, int min, int max, int body, int sound,
        MonsterArchetype kind = MonsterArchetype.Melee) =>
        new($"deceit-{role.ToString().ToLowerInvariant()}", "Deceit", name, kind, "Deceit cult",
            min, max, body, sound, new(0, 0, 0, 0, 0, IsUndead(role) ? 50 : 0));

    internal static DungeonProfile FindSpecies(string id) =>
        _roles.TryGetValue(id, out var role) ? _roster[role] : null;

    [Constructible]
    public DeceitEncounter(string role) : this(
        Enum.TryParse<DeceitRole>(role, true, out var selected) && _roster.ContainsKey(selected)
            ? selected : throw new ArgumentException("Unknown Deceit role.", nameof(role)))
    {
    }

    internal DeceitEncounter(DeceitRole role, int? level = null, Race ancestry = null, bool? female = null)
    {
        var species = _roster[role];
        var aura = role switch
        {
            DeceitRole.DemonicTaskmaster => EliteAura.Might,
            DeceitRole.CultistEnslaver => EliteAura.Vigor,
            _ => EliteAura.None
        };
        Assign(species, level ?? Utility.RandomMinMax(species.MinimumLevel, species.MaximumLevel),
            MonsterDifficulty.Nightmare, EncounterGrade.Ordinary, ChampionKind.Champion, [],
            aura, species.Name, Random.Shared);
        if (IsPerson)
        {
            Female = female ?? (role is DeceitRole.Slave or DeceitRole.DevoutFollower && Utility.RandomBool());
            Race = ancestry ?? (role is DeceitRole.Slave or DeceitRole.DevoutFollower ? RandomAncestry() : Race.Human);
            Hue = Race.RandomSkinHue();
            Dress(role);
        }
        if (role == DeceitRole.Skeleton)
        {
            NoKillAwards = true;
            Tamable = false;
        }
    }

    private static Race RandomAncestry() => Utility.Random(3) switch
    {
        0 => Race.Human,
        1 => Race.Elf,
        _ => Race.Gargoyle
    };

    private void Dress(DeceitRole role)
    {
        switch (role)
        {
            case DeceitRole.Slave:
                string ancestry = Race == Race.Human ? "human" : Race == Race.Elf ? "elven" : "gargoyle";
                Name = $"a{(Race == Race.Elf ? "n" : "")} {ancestry} slave";
                if (Race == Race.Gargoyle)
                    Wear(0x3E2, new GargishClothChestType1(), new GargishClothKiltType1());
                else
                    Wear(0x3E2, new Shirt(), new ShortPants());
                break;
            case DeceitRole.DevoutFollower when Race == Race.Gargoyle:
                Wear(Black, new GargishClothChestType1(), new GargishClothKiltType1(), new GargishClothArmsType1());
                break;
            case DeceitRole.DevoutFollower or DeceitRole.NecromancerApprentice:
                Wear(Black, new Robe());
                break;
            case DeceitRole.CultistEnslaver:
                Wear(0x455, new ChainChest(), new ChainLegs(), new RingmailArms(), new RingmailGloves(), new Broadsword());
                break;
            case DeceitRole.Bishop:
                Wear(Black, new Robe(), new QuarterStaff());
                break;
        }
    }

    private void Wear(int hue, params Item[] pieces)
    {
        foreach (var piece in pieces)
        {
            AddItem(new Item(piece.ItemID) { Layer = piece.Layer, Hue = hue, Movable = false });
            piece.Delete();
        }
    }

    public override bool AlwaysMurderer => !Freed;
    public override bool InitialInnocent => Freed;
    public override bool BleedImmune => IsUndead(Role);
    public override Poison PoisonImmune => IsUndead(Role) ? Poison.Lethal : null;
    public override OppositionGroup OppositionGroup => null;
    public override string CorpseName => $"{Name}'s corpse";
    public override bool IsEnemy(Mobile mobile) => !Freed && base.IsEnemy(mobile);
    internal override bool IsEnemyIgnoringLeash(Mobile mobile) => !Freed && base.IsEnemyIgnoringLeash(mobile);
    public override bool IsFriend(Mobile mobile) => !Freed && mobile is not DeceitEncounter { Freed: true } && base.IsFriend(mobile);
    protected override byte MeleeAnimation => IsPerson ? (byte)9 : base.MeleeAnimation;
    protected override int EncounterCastAnimation => IsPerson ? 16 : base.EncounterCastAnimation;
    protected override bool CanCastEncounterBolt => Role == DeceitRole.DemonicTorturer
        || Role != DeceitRole.DevoutFollower && base.CanCastEncounterBolt;
    protected override int EncounterBoltRange => Role == DeceitRole.DemonicTorturer ? 9 : base.EncounterBoltRange;
    protected override int AdjustEncounterLife(int life) => Role == DeceitRole.Familiar ? life / 2 : life;

    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon)
    {
        var role = _roles.GetValueOrDefault(dungeon.Id, DeceitRole.Unassigned);
        bool demon = role is DeceitRole.DemonicTaskmaster or DeceitRole.WatchfulOverseer
            or DeceitRole.Familiar or DeceitRole.DemonicTorturer or DeceitRole.Seductress or DeceitRole.InfernalMistress;
        return new(dungeon.Family, IsUndead(role), demon, false, false, true, 1, 0, 0, 100, 0);
    }

    public override bool BeginFirebolt(Mobile target)
    {
        if (ReturningHome || Core.TickCount < _nextSpell || !NamedControl.SpecialAllowed(this) || !base.BeginFirebolt(target))
            return false;
        _nextSpell = Core.TickCount + (Role == DeceitRole.DemonicTorturer ? 9000 : 3500);
        return true;
    }

    private static readonly CombatAbility[] _overseerSpells = [CombatAbility.Decrepify, CombatAbility.BoneSpear];
    private static readonly CombatAbility[] _necromancerSpells = [CombatAbility.BoneArmor, CombatAbility.Weaken, CombatAbility.LifeTap];
    private static readonly CombatAbility[] _bishopSpells = [CombatAbility.Lightning, CombatAbility.MiasmaBolt];
    private static readonly CombatAbility[] _seductressSpells = [CombatAbility.LowerResist, CombatAbility.IceBlast];
    private static readonly CombatAbility[] _mistressSpells = [CombatAbility.AmplifyDamage, CombatAbility.FireBolt];

    private CombatAbility NextSpell(CombatAbility[] spells)
    {
        var spell = spells[_spellIndex];
        _spellIndex = (_spellIndex + 1) % spells.Length;
        return spell;
    }

    internal ushort? BoltGraphic => Role switch
    {
        DeceitRole.Seductress => FlareGraphics.BlueFlight,
        DeceitRole.InfernalMistress => FlareGraphics.RedFlight,
        _ => null
    };

    protected override void ReleaseEncounterBolt(Mobile target)
    {
        var ability = Role switch
        {
            DeceitRole.WatchfulOverseer => NextSpell(_overseerSpells),
            DeceitRole.NecromancerApprentice => CombatAbility.BoneSpear,
            DeceitRole.DemonicTorturer => CombatAbility.SigilLethargy,
            DeceitRole.Necromancer => NextSpell(_necromancerSpells),
            DeceitRole.Seductress => NextSpell(_seductressSpells),
            DeceitRole.InfernalMistress => NextSpell(_mistressSpells),
            DeceitRole.Bishop => NextSpell(_bishopSpells),
            _ => CombatAbility.None
        };
        switch (ability)
        {
            case CombatAbility.None:
                break;
            case CombatAbility.Decrepify or CombatAbility.Weaken or CombatAbility.LifeTap
                or CombatAbility.LowerResist or CombatAbility.AmplifyDamage:
                NecromancerCurseCasts.CastEncounterCurse(this, target, ability);
                break;
            case CombatAbility.BoneArmor:
                NecromancerEffects.ApplyBoneArmor(this, SpellEffects(ability));
                break;
            case CombatAbility.SigilLethargy:
                SigilLethargy.CastEncounter(this, target.Location, SpellEffects(ability));
                break;
            default:
                EliteBolts.Cast(this, target, ability);
                break;
        }
    }

    public override void OnThink()
    {
        if (Freed)
            return;

        bool engaged = EliteCombat.Enemy(this, Combatant, 12);
        if (Role == DeceitRole.Necromancer)
        {
            if (!engaged || ReturningHome)
            {
                if (_nextSkeleton != default)
                    NextSkeleton = default;
            }
            else if (_nextSkeleton == default)
                NextSkeleton = Core.Now.AddSeconds(30);
        }

        if (EliteCombat.CanAct(this) && ActionReady && !SpellInProgress && !ReturningHome
            && NamedControl.SpecialAllowed(this) && CreatureHitRecovery.Remaining(this) == 0
            && Map != null && Map != Map.Internal && (Home == Point3D.Zero || Map == HomeMap && InRange(Home, HomeLeashRadius)))
        {
            if (Role == DeceitRole.Bishop && Core.TickCount >= _nextHeal && FindBishopTarget() is { } ally)
            {
                StartAction(ally, harmful: false);
                HealAlly(ally, 25);
                _nextHeal = Core.TickCount + 8000;
            }
            else if (engaged)
            {
                if (Role == DeceitRole.DemonicTorturer)
                    BeginFirebolt(Combatant);
                else if (Role == DeceitRole.Necromancer)
                    SummonSkeleton(Combatant);
            }
        }
        base.OnThink();
    }

    private DeceitEncounter FindBishopTarget()
    {
        var ally = FindWoundedAlly<DeceitEncounter>(SupportRange);
        return ally is { ReturningHome: false, Freed: false } ? ally : null;
    }

    internal DeceitAllySnapshot ResurrectionSnapshot() =>
        Freed || Raised || Controlled || Summoned || Role is DeceitRole.Skeleton or DeceitRole.Unassigned
            ? null : new(Dungeon.Id, Basis.Level, Race.RaceIndex, Female, Hue);

    private static DeceitAllySnapshot FallenAlly(Corpse corpse) =>
        CorpseSkillRules.Profiles.Find(corpse)?.DeceitAlly;

    protected override bool CanRaiseKind(Corpse corpse) => Role == DeceitRole.DevoutFollower && !Raised
        && FallenAlly(corpse) is { } ally && FindSpecies(ally.Species) is { } species
        && ally.Level >= species.MinimumLevel && ally.Level <= species.MaximumLevel && ally.RaceIndex is >= 0 and <= 2;

    protected override GraveyardUndead CreateRaisedBody(Corpse corpse)
    {
        var ally = FallenAlly(corpse);
        return new DeceitEncounter(_roles[ally.Species], ally.Level, Race.Races[ally.RaceIndex], ally.Female)
        {
            Home = corpse.Location, HomeMap = Map, RangeHome = 3, Hue = ally.Hue
        };
    }

    protected override bool RaisedBodyAwardsRewards => false;
    protected override int RaiseCooldownMs => 10000;
    protected override int RaiseAnimation => EncounterCastAnimation;

    private void SummonSkeleton(Mobile target)
    {
        if (Core.Now < _nextSkeleton)
            return;
        if (_skeletons.RemoveAll(skeleton => skeleton is not { Alive: true, Deleted: false }) > 0)
            this.MarkDirty();
        if (_skeletons.Count >= SkeletonLimit)
            return;

        for (int i = 0; i < 8; i++)
        {
            var point = Location;
            Server.Movement.Movement.Offset((Direction)i, ref point);
            if (!EncounterProfiles.Free(Map, point) || !InLOS(point))
                continue;

            var skeleton = new DeceitEncounter(DeceitRole.Skeleton)
            {
                Home = Home == Point3D.Zero ? Location : Home,
                HomeMap = Map,
                RangeHome = RangeHome
            };
            skeleton.MoveToWorld(point, Map);
            skeleton.Combatant = target;
            skeleton.FixedEffect(0x376A, 9, 20);
            skeleton.PlaySound(0x1FB);
            _skeletons.Add(skeleton);
            this.MarkDirty();
            NextSkeleton = Core.Now.AddSeconds(30);
            StartAction(target);
            return;
        }
    }

    private void RemoveSkeletons()
    {
        foreach (var skeleton in _skeletons)
            if (skeleton is { Alive: true, Deleted: false })
                skeleton.Delete();
        _skeletons.Clear();
    }

    public override void OnDeath(Container corpse)
    {
        RemoveSkeletons();
        base.OnDeath(corpse);
    }

    public override void OnDelete()
    {
        _escape.Cancel();
        RemoveSkeletons();
        base.OnDelete();
    }

    public override void Damage(int amount, Mobile from = null, bool informMount = true, bool ignoreEvilOmen = false)
    {
        if (Freed)
            return;
        if (Role != DeceitRole.Slave || !Alive || Deleted || amount <= 0)
        {
            base.Damage(amount, from, informMount, ignoreEvilOmen);
            return;
        }

        int threshold = Math.Max(1, (HitsMax + 3) / 4);
        base.Damage(Math.Min(amount, Math.Max(0, Hits - threshold)), from, informMount, ignoreEvilOmen);
        if (Alive && Hits <= threshold)
            Free(from ?? Combatant);
    }

    private void Free(Mobile attacker)
    {
        EscapeUntil = Core.Now.AddSeconds(5);
        var toward = attacker == null || attacker.Location == Location ? Direction : GetDirectionTo(attacker);
        EscapeHeading = (Direction)(((int)toward + 4) & 7);
        Blessed = true;
        FightMode = FightMode.None;
        Team = int.MinValue;
        Frozen = Paralyzed = false;
        Poison = null;
        Combatant = FocusMob = null;
        Warmode = false;
        StopCombatActions();
        AIObject?.AITimer.Stop();
        foreach (var nearby in Map.GetMobilesInRange<Mobile>(Location, 24))
        {
            if (nearby.Combatant == this)
                nearby.Combatant = null;
            if (nearby is not BaseCreature creature)
                continue;
            if (creature.FocusMob == this)
                creature.FocusMob = null;
            if (creature.ControlTarget == this)
                creature.IssueOrder(OrderType.Guard, null);
        }
        PublicOverheadMessage(MessageType.Regular, 0x59, false, _freedLines[Utility.Random(_freedLines.Length)]);
        StartEscape();
    }

    private void StartEscape()
    {
        AIObject?.AITimer.Stop();
        Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromMilliseconds(230), EscapeStep, out _escape);
    }

    private void EscapeStep()
    {
        if (Deleted || Core.Now >= _escapeUntil)
        {
            Delete();
            return;
        }
        for (int offset = 0; offset <= 2; offset++)
        {
            if (TryEscape(offset) || offset != 0 && TryEscape(-offset))
                return;
        }
    }

    private bool TryEscape(int offset) =>
        Move((Direction)(((int)_escapeHeading + offset) & 7) | Direction.Running);

    [AfterDeserialization]
    private void RestoreDeceit()
    {
        if (Role == DeceitRole.Skeleton || Raised)
            NoKillAwards = true;
        if (Freed)
            Timer.DelayCall(TimeSpan.Zero, StartEscape);
    }

    protected override void Deal(Mobile target, int damage, bool fire)
    {
        base.Deal(target, damage, fire);
        if (!fire && Role == DeceitRole.Familiar && target is { Alive: true, Deleted: false, Blessed: false }
            && Utility.Random(100) < 25)
            CombatStuns.ApplyFamiliar(target);
    }
}
