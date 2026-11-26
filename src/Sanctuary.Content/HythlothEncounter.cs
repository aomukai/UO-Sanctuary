// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Spells;

namespace Sanctuary;

internal enum HythlothRole
{
    Acolyte, RoyalSoldier, UndeadServant, TempleGuard, RoyalArbalest, Apprentice, UndeadCleric,
    RoyalKnight, SkeletalMage, Herald, Jester, Chamberlain, Executioner
}

[SerializationGenerator(0)]
public partial class HythlothEncounter : EncounterCreature
{
    private const int Black = 0x0455;
    private const int SupportRange = 8;

    private static readonly Dictionary<HythlothRole, DungeonProfile> _roster = new()
    {
        [HythlothRole.Acolyte] = Species(HythlothRole.Acolyte, "an acolyte", MonsterArchetype.Melee, 30, 32, 0x190),
        [HythlothRole.RoyalSoldier] = Species(HythlothRole.RoyalSoldier, "a royal soldier", MonsterArchetype.Melee, 31, 33, 147),
        [HythlothRole.UndeadServant] = Species(HythlothRole.UndeadServant, "an undead servant", MonsterArchetype.Caster, 32, 34, 56),
        [HythlothRole.TempleGuard] = Species(HythlothRole.TempleGuard, "a temple guard", MonsterArchetype.Tank, 33, 35, 0x190),
        [HythlothRole.RoyalArbalest] = Species(HythlothRole.RoyalArbalest, "a royal arbalest", MonsterArchetype.Ranged, 33, 35, 50),
        [HythlothRole.Apprentice] = Species(HythlothRole.Apprentice, "an apprentice", MonsterArchetype.Melee, 34, 36, 0x190),
        [HythlothRole.UndeadCleric] = Species(HythlothRole.UndeadCleric, "an undead cleric", MonsterArchetype.Caster, 35, 37, 56),
        [HythlothRole.RoyalKnight] = Species(HythlothRole.RoyalKnight, "a royal knight", MonsterArchetype.Tank, 36, 38, 0x190),
        [HythlothRole.SkeletalMage] = Species(HythlothRole.SkeletalMage, "a skeletal mage", MonsterArchetype.Caster, 37, 39, 56),
        [HythlothRole.Herald] = Species(HythlothRole.Herald, "the Herald", MonsterArchetype.Ranged, 32, 32, 50, true),
        [HythlothRole.Jester] = Species(HythlothRole.Jester, "the Jester", MonsterArchetype.Caster, 35, 35, 56, true),
        [HythlothRole.Chamberlain] = Species(HythlothRole.Chamberlain, "the Chamberlain", MonsterArchetype.Tank, 36, 36, 50, true),
        [HythlothRole.Executioner] = Species(HythlothRole.Executioner, "the Executioner", MonsterArchetype.Tank, 38, 38, 50, true),
    };
    private static readonly Dictionary<string, HythlothRole> _roles =
        _roster.ToDictionary(entry => entry.Value.Id, entry => entry.Key, StringComparer.Ordinal);

    [SerializableField(0)] private bool _announced;
    [SerializableField(1)] private int _soldiersCalled;
    [SerializableField(2)] private DateTime _nextBark;
    [SerializableField(3, setter: "private")] private List<HythlothEncounter> _escort = [];
    private long _nextSupport, _nextLightning, _nextOrders, _nextWhirlwind;
    private TimerExecutionToken _whirlwind;

    internal static DungeonProfile FindSpecies(string id) =>
        _roles.TryGetValue(id, out var role) ? _roster[role] : null;

    private static DungeonProfile Species(HythlothRole role, string name, MonsterArchetype kind,
        int min, int max, int body, bool boss = false)
    {
        int bonus = boss ? EncounterProfiles.Level(0, EncounterGrade.SuperUnique) : 0;
        bool human = body == 0x190;
        return new($"hythloth-{role.ToString().ToLowerInvariant()}", "Hythloth", name, kind, "Hythloth royal court",
            min - bonus, max - bonus, body, human ? 0 : 0x48D, new(0, 0, 0, 0, 0, human ? 0 : 50))
        {
            SkeletalRole = role switch
            {
                HythlothRole.RoyalSoldier => SkeletalMonsterRole.Warrior,
                HythlothRole.UndeadServant or HythlothRole.UndeadCleric or HythlothRole.SkeletalMage => SkeletalMonsterRole.Mage,
                _ => SkeletalMonsterRole.None
            }
        };
    }

    [Constructible]
    public HythlothEncounter(string role) : this(
        Enum.TryParse<HythlothRole>(role, true, out var selected) && Enum.IsDefined(selected)
            ? selected : throw new ArgumentException("Unknown Hythloth role.", nameof(role)))
    {
    }

    internal HythlothEncounter(HythlothRole role)
    {
        var species = _roster[role];
        var grade = role >= HythlothRole.Herald ? EncounterGrade.SuperUnique : EncounterGrade.Ordinary;
        int level = Utility.RandomMinMax(species.MinimumLevel, species.MaximumLevel);
        var aura = role == HythlothRole.TempleGuard ? EncounterProfiles.RollAura(level, Random.Shared) : EliteAura.None;
        var modifiers = role == HythlothRole.Jester ? new[] { EliteModifier.Teleport } : [];
        Assign(species, level, MonsterDifficulty.Normal, grade, ChampionKind.Champion, modifiers, aura, species.Name, Random.Shared);
        Hue = IsHuman(species) ? Race.Human.RandomSkinHue() : 0;
        Dress(role);
    }

    internal HythlothRole Role => _roles[Dungeon.Id];
    private static bool IsHuman(DungeonProfile profile) => profile.Body == 0x190;
    private bool Human => IsHuman(Dungeon);
    protected override byte MeleeAnimation => Role switch
    {
        HythlothRole.RoyalKnight => 14,
        HythlothRole.Acolyte or HythlothRole.TempleGuard or HythlothRole.Apprentice => 9,
        _ => base.MeleeAnimation
    };
    protected override int EncounterCastAnimation => Human ? 16 : base.EncounterCastAnimation;
    protected override bool CanCastEncounterBolt => Role == HythlothRole.Apprentice || base.CanCastEncounterBolt;
    protected override int EncounterBoltRange => Role == HythlothRole.Apprentice ? 9 : base.EncounterBoltRange;
    public override bool AlwaysMurderer => true;
    public override bool BleedImmune => !Human;
    public override Poison PoisonImmune => Human ? null : Poison.Lethal;
    public override OppositionGroup OppositionGroup => Human ? null : base.OppositionGroup;
    public override string CorpseName => $"{Name}'s corpse";
    protected override bool BusyWithSpecial => _whirlwind.Running || SpellInProgress;
    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon) =>
        new(dungeon.Family, !IsHuman(dungeon), false, Leader, false, true,
            dungeon.Id == _roster[HythlothRole.Executioner].Id ? 3 : 1, 0, 0, Leader ? 50 : 100, 0);

    private void Dress(HythlothRole role)
    {
        switch (role)
        {
            case HythlothRole.Acolyte:
                Wear(new Robe(Black), new Dagger());
                break;
            case HythlothRole.Apprentice:
                Utility.AssignRandomHair(this);
                Wear(new Robe(0x0491), new GnarledStaff());
                break;
            case HythlothRole.TempleGuard:
                Wear(new ChainCoif { Resource = CraftResource.ShadowIron }, new ChainChest { Resource = CraftResource.ShadowIron },
                    new ChainLegs { Resource = CraftResource.ShadowIron }, new RingmailGloves { Resource = CraftResource.ShadowIron },
                    new ChaosShield(), new Broadsword());
                break;
            case HythlothRole.RoyalKnight:
                Wear(new PlateHelm { Resource = CraftResource.ShadowIron }, new PlateChest { Resource = CraftResource.ShadowIron },
                    new PlateArms { Resource = CraftResource.ShadowIron }, new PlateLegs { Resource = CraftResource.ShadowIron },
                    new PlateGloves { Resource = CraftResource.ShadowIron }, new PlateGorget { Resource = CraftResource.ShadowIron },
                    new Halberd());
                break;
        }
    }

    private void Wear(params Item[] pieces)
    {
        foreach (var piece in pieces)
        {
            // Encounter profiles supply combat stats, while human equipment supplies the visible costume.
            AddItem(new Item(piece.ItemID) { Layer = piece.Layer, Hue = piece.Hue, Movable = false });
            piece.Delete();
        }
    }

    public override void SendAppearanceTo(NetState state)
    {
        var outfit = Role switch
        {
            HythlothRole.Herald => SkeletonAppearance.Bow,
            HythlothRole.RoyalArbalest => SkeletonAppearance.Crossbow,
            HythlothRole.Jester => SkeletonAppearance.Jester,
            HythlothRole.Chamberlain => SkeletonAppearance.Mace,
            HythlothRole.Executioner => SkeletonAppearance.Executioner,
            _ => SkeletonAppearance.None
        };
        if (outfit != SkeletonAppearance.None)
            SkeletonAppearancePackets.Send(state, Serial, outfit);
        else
            base.SendAppearanceTo(state);
    }

    public override void OnThink()
    {
        TryCourtAction();
        base.OnThink();
    }

    private void TryCourtAction()
    {
        if (!EliteCombat.CanAct(this) || Map == null || Map == Map.Internal || ReturningHome || Home != Point3D.Zero && (Map != HomeMap || !InRange(Home, HomeLeashRadius))
            || Controlled || Summoned || !NamedControl.SpecialAllowed(this) || CreatureHitRecovery.Remaining(this) != 0)
            return;

        bool engaged = EliteCombat.Enemy(this, Combatant, 12);
        if (Role == HythlothRole.Herald && engaged)
            CallEscort(Combatant);
        if (Role == HythlothRole.Jester && engaged)
            Bark(_jesterBarks);
        if (Role == HythlothRole.Chamberlain && (engaged || NearbyServant()))
            Bark(_chamberlainBarks);

        if (!ActionReady || BusyWithSpecial)
            return;

        long now = Core.TickCount;
        if (Role is HythlothRole.UndeadServant or HythlothRole.UndeadCleric && now >= _nextSupport)
        {
            _nextSupport = now + 250;
            foreach (var ally in Map.GetMobilesInRange<EncounterCreature>(Location, SupportRange))
            {
                if (CanSupport(ally) && ally.EncounterProfile.Traits.Undead == true && !SkillHealth.Full(ally))
                {
                    HealAlly(ally, 25);
                    _nextSupport = now + 8000;
                    if (Role == HythlothRole.UndeadServant)
                        break;
                }
            }
        }
        if (!engaged)
            return;

        if (Role == HythlothRole.Apprentice && now >= _nextLightning && BeginFirebolt(Combatant))
            _nextLightning = now + 4500;
        if (Role == HythlothRole.RoyalKnight)
            KnightActions(Combatant, now);
    }

    private bool CanSupport(EncounterCreature ally) =>
        ally != this && ally is { Alive: true, Deleted: false, Controlled: false, Summoned: false }
        && !ally.ReturningHome && Math.Abs(ally.Z - Z) < 16 && IsFriend(ally) && CanSee(ally) && InLOS(ally);

    private bool NearbyServant()
    {
        foreach (var ally in Map.GetMobilesInRange<HythlothEncounter>(Location, SupportRange))
            if (CanSupport(ally) && ally.Role == HythlothRole.UndeadServant)
                return true;
        return false;
    }

    protected override bool CanRaiseKind(Corpse corpse) => Role == HythlothRole.UndeadCleric
        && corpse.Owner is HythlothEncounter { Human: true, Controlled: false, Summoned: false };

    protected override GraveyardUndead CreateRaisedBody(Corpse corpse) => new HythlothEncounter(HythlothRole.RoyalSoldier)
    {
        Home = corpse.Location, HomeMap = Map, RangeHome = 3
    };

    protected override bool RaisedBodyAwardsRewards => false;

    protected override void ReleaseEncounterBolt(Mobile target)
    {
        switch (Role)
        {
            case HythlothRole.Apprentice:
                EliteBolts.Cast(this, target, CombatAbility.Lightning);
                break;
            case HythlothRole.UndeadServant:
                EliteBolts.Cast(this, target, CombatAbility.IceBlast);
                break;
            case HythlothRole.UndeadCleric:
                EliteBolts.Cast(this, target, CombatAbility.Fireball);
                break;
            case HythlothRole.Jester:
                EliteBolts.CastFrozenOrb(this, target);
                break;
            default:
                base.ReleaseEncounterBolt(target);
                break;
        }
    }

    protected override void Deal(Mobile target, int damage, bool fire)
    {
        if (!fire && Role == HythlothRole.Executioner && Utility.Random(100) < 10)
            NamedCombat.ApplyCrushing(target, ranged: false);

        base.Deal(target, damage, fire);
        if (fire || !target.Alive || target.Deleted)
            return;

        if (Role == HythlothRole.Executioner)
            TimedEffects.Bleed(this, target, NamedCombat.OpenWoundsDamage(Basis.Level, target, ranged: false), TimedEffects.Now);
        if (Role == HythlothRole.Chamberlain)
            HythlothStun.TryApply(target);
    }

    private void CallEscort(Mobile target)
    {
        if (!_announced)
        {
            Announced = true;
            string guest = target is BaseCreature creature && creature.GetMaster() is Mobile owner ? owner.Name : target.Name;
            PublicOverheadMessage(MessageType.Regular, 0x59, false, $"Hear ye! {guest}, an unwelcome guest of His Majesty!");
        }
        if (_soldiersCalled >= 4)
            return;

        for (int radius = 1; radius <= 3 && _soldiersCalled < 4; radius++)
        {
            for (int y = -radius; y <= radius && _soldiersCalled < 4; y++)
            for (int x = -radius; x <= radius && _soldiersCalled < 4; x++)
            {
                if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                    continue;

                var point = new Point3D(X + x, Y + y, Z);
                if (RepelledByRefuge(point) || !EncounterProfiles.Free(Map, point)
                    || !EncounterProfiles.ClearSight(Map, Location, point))
                    continue;

                var soldier = new HythlothEncounter(HythlothRole.RoyalSoldier)
                    { Home = point, HomeMap = Map, RangeHome = 3 };
                soldier.MoveToWorld(point, Map);
                soldier.Combatant = target;
                _escort.Add(soldier);
                SoldiersCalled++;
                this.MarkDirty();
            }
        }
    }

    public override void OnDelete()
    {
        foreach (var soldier in _escort)
            soldier?.Delete();
        base.OnDelete();
    }

    private void KnightActions(Mobile target, long now)
    {
        if (now >= _nextOrders)
        {
            _nextOrders = now + 30000;
            StartAction(target);
            Animate(16, 5, 1, true, false, 0);
            BattleOrders.ApplyEncounter(this, this, 3, TimedEffects.Now);
            foreach (var ally in Map.GetMobilesInRange<HythlothEncounter>(Location, SupportRange))
                if (CanSupport(ally))
                    BattleOrders.ApplyEncounter(this, ally, 3, TimedEffects.Now);
            return;
        }
        if (now < _nextWhirlwind || !InRange(target, 3))
            return;

        _nextWhirlwind = now + 9000;
        StartAction(target);
        var direction = GetDirectionTo(target);
        var map = Map;
        var hit = new HashSet<Serial>();
        Timer.StartTimer(TimeSpan.FromMilliseconds(160), TimeSpan.FromMilliseconds(160), 4, () =>
        {
            if (Map != map || !EliteCombat.CanAct(this) || ReturningHome || !NamedControl.SpecialAllowed(this)
                || CreatureHitRecovery.Remaining(this) != 0)
            {
                _whirlwind.Cancel();
                return;
            }
            Direction = (Direction)(((int)Direction + 2) & 7);
            Animate(13, 4, 1, true, false, 0);
            foreach (var enemy in map.GetMobilesInRange<Mobile>(Location, 1))
            {
                if (!EliteCombat.Enemy(this, enemy, 1) || !hit.Add(enemy.Serial))
                    continue;
                using var contact = PaladinThorns.Attempt(this, enemy, false);
                if (ClassStats.CheckHit(this, enemy, 0) == true)
                    EliteCombat.Hit(this, enemy, Utility.RandomMinMax(Basis.MinDamage, Basis.MaxDamage), ResistanceType.Physical);
            }
            var next = Location;
            Server.Movement.Movement.Offset(direction, ref next);
            if (Utility.InRange(next, Home, HomeLeashRadius))
                CombatDisplacement.TryStep(this, direction);
        }, out _whirlwind);
    }

    internal override void StopCombatActions()
    {
        _whirlwind.Cancel();
        base.StopCombatActions();
    }

    private void Bark(string[] lines)
    {
        if (Core.Now < _nextBark)
            return;

        NextBark = Core.Now.AddSeconds(30);
        PublicOverheadMessage(MessageType.Regular, 0x59, false, lines[Utility.Random(lines.Length)]);
    }

    private static readonly string[] _jesterBarks =
    [
        "Do try to die amusingly. His Majesty tires of the usual.",
        "Ah, a warm audience! We so rarely get those.",
        "Hold still! This next trick requires a volunteer.",
        "Nothing up my sleeves! Nothing in them, either.",
        "Thou hast a lovely skeleton. Why hide it?",
        "A little frost to preserve thee for the encore!",
        "Missed me! In fairness, there’s less of me to hit.",
        "The last fellow died laughing. Well… I was laughing.",
        "No applause? A tough crowd. Usually ours falls apart.",
        "If thou must heckle, kindly wait until I’ve finished killing thee!"
    ];

    private static readonly string[] _chamberlainBarks =
    [
        "That dust is older than the king! Remove it!",
        "We lay places for twelve. We have always laid places for twelve.",
        "Whose finger is in the soup? Account for yourselves!",
        "Straighten thy livery. Death is no excuse for slovenliness.",
        "The west corridor smells of intruders. Fetch a mop.",
        "His Majesty’s wine has turned to vinegar. Again.",
        "Stop polishing that skull. It belongs to the ambassador.",
        "The silver is for our guests. Put thy teeth away.",
        "I requested fresh candles. These were burnt out when we were alive.",
        "If thy arm falls off once more, carry the tray with the other!"
    ];
}

internal static class HythlothStun
{
    private static readonly TimeSpan _duration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _immunity = TimeSpan.FromSeconds(5);
    private static readonly Dictionary<Serial, DateTime> _immuneUntil = new();

    internal static bool TryApply(Mobile target)
    {
        if (target.Frozen || target.Paralyzed || _immuneUntil.GetValueOrDefault(target.Serial) > Core.Now
            || Utility.Random(100) >= 25)
            return false;

        var deadline = Core.Now + _duration + _immunity;
        _immuneUntil[target.Serial] = deadline;
        Timer.DelayCall(_duration + _immunity, () =>
        {
            if (_immuneUntil.GetValueOrDefault(target.Serial) == deadline)
                _immuneUntil.Remove(target.Serial);
        });
        if (target is PlayerMobile player)
            ClassCombat.Stop(player);
        if (target.Spell is Spell spell)
            spell.Disturb(DisturbType.Hurt);
        target.Freeze(_duration);
        target.FixedEffect(SpellEffectGraphics.CombatStun, 10, 30);
        return true;
    }
}
