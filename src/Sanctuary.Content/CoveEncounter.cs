// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class CoveEncounter : EncounterCreature
{
    private const string Skeleton = "cove-skeleton", Zombie = "cove-zombie", Mummy = "cove-mummy", LichLord = "cove-lich-lord";
    private const string Warrior = "cove-orc-warrior", Scout = "cove-orc-scout", Shaman = "cove-orc-shaman", Chief = "cove-orc-chief";
    internal const string Imp = "cove-imp";
    private static readonly Dictionary<string, DungeonProfile> _species = new(StringComparer.Ordinal)
    {
        [Skeleton] = Species(Skeleton, "a skeleton", MonsterArchetype.Melee, 20, 20, 50, 451, poison: 50),
        [Zombie] = Species(Zombie, "a zombie", MonsterArchetype.Tank, 20, 20, 3, 471, poison: 50),
        [Mummy] = Species(Mummy, "a mummy", MonsterArchetype.Tank, 20, 20, 154, 471, poison: 65),
        [LichLord] = Species(LichLord, "a lich lord", MonsterArchetype.Caster, 25, 25, 79, 412, cold: 55, poison: 75),
        [Warrior] = Species(Warrior, "an orc warrior", MonsterArchetype.Melee, 18, 24, 17, 0x45A),
        [Scout] = Species(Scout, "an orc scout", MonsterArchetype.Ranged, 18, 24, 17, 0x45A),
        [Shaman] = Species(Shaman, "an orc shaman", MonsterArchetype.Caster, 18, 24, 140, 0x45A, fire: 25),
        [Chief] = Species(Chief, "an orc chief", MonsterArchetype.Tank, 22, 22, 138, 0x45A, physical: 15),
        [Imp] = Species(Imp, "an imp", MonsterArchetype.Melee, 20, 20, 39, 422, fire: 35)
    };

    private TimerExecutionToken _heal;
    private long _nextHeal;

    internal static DungeonProfile FindSpecies(string id) => id == null ? null : _species.GetValueOrDefault(id);
    private static DungeonProfile Species(string id, string name, MonsterArchetype archetype, int min, int max,
        int body, int sound, int physical = 0, int fire = 0, int cold = 0, int poison = 0) =>
        new(id, id, name, archetype, "Cove", min, max, body, sound, new(physical, 0, fire, 0, cold, poison));

    [Constructible]
    public CoveEncounter(string id, int level)
    {
        var profile = FindSpecies(id) ?? throw new ArgumentException("Unknown Cove species.", nameof(id));
        var grade = id == Chief ? EncounterGrade.SuperUnique : id == Imp ? EncounterGrade.Champion : EncounterGrade.Ordinary;
        var modifiers = id == Chief ? EncounterProfiles.RollModifiers(profile, MonsterDifficulty.Normal, Random.Shared) : [];
        string name = id == Chief ? "Ghurk the Siege-Chief" : id == Imp ? "an imp" : null;
        Assign(profile, level - EncounterProfiles.Level(0, grade), MonsterDifficulty.Normal, grade,
            ChampionKind.Champion, modifiers, EncounterProfiles.RollAura(modifiers, level, Random.Shared), name, Random.Shared);
        RangePerception = id == LichLord ? 10 : id is Shaman or Scout ? 8 : 6;
    }

    internal bool Orc => Dungeon.Id is Warrior or Scout or Shaman or Chief;
    internal bool OrcChief => Dungeon.Id == Chief;
    internal bool PentagramImp => Dungeon.Id == Imp;
    private bool Undead => Dungeon.Id is Skeleton or Zombie or Mummy or LichLord;
    protected override int RegionalEncounterPlayers => 3;
    internal override int HomeLeashRadius => Dungeon.Id == LichLord ? 12 : PentagramImp ? 8 : 10;
    public override int AcquireOnApproachRange => Math.Min(base.AcquireOnApproachRange, RangePerception);
    public override bool BleedImmune => Undead;
    public override Poison PoisonImmune => Undead ? Poison.Lethal : null;
    public override OppositionGroup OppositionGroup => Undead ? OppositionGroup.FeyAndUndead : null;
    public override string CorpseName => $"{Name}'s corpse";
    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon) =>
        new(dungeon.Family, Undead, PentagramImp, Leader, false, true, 1, 0, 100, Leader ? 50 : 100, 0);

    internal override bool IsEnemyIgnoringLeash(Mobile target) =>
        (!PentagramImp || CoveGnaw.HostileTo(target)) && base.IsEnemyIgnoringLeash(target);

    public override bool CanBeHarmful(Mobile target, bool message, bool ignoreOurBlessedness) =>
        (!PentagramImp || CoveGnaw.HostileTo(target)) && base.CanBeHarmful(target, message, ignoreOurBlessedness);

    protected override int RaiseKind => Dungeon.Id switch { Skeleton => 1, Zombie or Mummy => 2, _ => 0 };
    protected override bool CanRaiseKind(Corpse corpse) =>
        Dungeon.Id == LichLord && corpse.Owner is CoveEncounter { Undead: true }
        && corpse.SanctuaryUndeadKind is 1 or 2;
    protected override GraveyardUndead CreateRaisedBody(Corpse corpse) =>
        new CoveEncounter(((CoveEncounter)corpse.Owner).Dungeon.Id, 20);
    protected override bool RaisedBodyAwardsRewards => false;

    public override void OnThink()
    {
        if (_heal.Running)
            return;

        if (Dungeon.Id == LichLord && !Controlled && !Summoned && !ReturningHome && ActionReady
            && EliteCombat.CanAct(this) && NamedControl.SpecialAllowed(this) && CreatureHitRecovery.Remaining(this) == 0
            && ((FindRaiseTarget() is { } corpse && BeginRaise(corpse)) || TryMend()))
            return;

        base.OnThink();
    }

    public override bool BeginFirebolt(Mobile target) => !_heal.Running && base.BeginFirebolt(target);

    private bool TryMend()
    {
        if (Core.TickCount < _nextHeal)
            return false;

        CoveEncounter wounded = null;
        foreach (var ally in Map.GetMobilesInRange<CoveEncounter>(Location, 8))
        {
            if (CanMend(ally) && (wounded == null || (long)ally.Hits * wounded.HitsMax < (long)wounded.Hits * ally.HitsMax))
                wounded = ally;
        }
        if (wounded == null)
            return false;

        var map = Map;
        var origin = Location;
        _nextHeal = Core.TickCount + 9000;
        Direction = GetDirectionTo(wounded);
        Animate(4, 5, 1, true, false, 0);
        wounded.FixedParticles(0x376A, 9, 24, 5024, EffectLayer.Waist);
        Timer.StartTimer(TimeSpan.FromMilliseconds(850), () =>
        {
            if (Map != map || Location != origin || ReturningHome || !EliteCombat.CanAct(this)
                || !NamedControl.SpecialAllowed(this) || CreatureHitRecovery.Remaining(this) > 0 || !CanMend(wounded))
                return;
            SkillHealth.SetBalance256(wounded, wounded.HitsMax * 256);
            wounded.FixedEffect(0x376A, 9, 32);
            wounded.PlaySound(0x1F2);
        }, out _heal);
        return true;
    }

    private bool CanMend(CoveEncounter ally) =>
        !Controlled && !Summoned && ally != this && ally is { Undead: true, Alive: true, Deleted: false, Controlled: false, Summoned: false }
        && ally.Dungeon.Id != LichLord && ally.Map == Map && Math.Abs(ally.Z - Z) < 16 && InRange(ally, 8)
        && ally.Hits * 3L < ally.HitsMax * 2L && CanSee(ally) && InLOS(ally);

    internal override void StopCombatActions()
    {
        _heal.Cancel();
        base.StopCombatActions();
    }
}

[SerializationGenerator(0)]
public partial class OrcEar : Item
{
    [Constructible]
    public OrcEar() : base(0x1CE3)
    {
        Stackable = true;
        Weight = 0.1;
        Hue = 0x0591;
    }

    public override string DefaultName => "orc ear";
}
