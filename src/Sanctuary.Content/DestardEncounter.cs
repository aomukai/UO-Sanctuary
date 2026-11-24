// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Network;

namespace Sanctuary;

internal enum DestardRole
{
    Wyvern, EnslavedDrake, LesserDaemon, Daemon, EnslavedDragon, Wyrm, Archdaemon, CorruptedHatchling, DaemonSpawn
}

[SerializationGenerator(0)]
public partial class DestardEncounter : EncounterCreature
{
    private const int BlackHue = 0x455;
    private static readonly Dictionary<DestardRole, DungeonProfile> _roster = new()
    {
        [DestardRole.Wyvern] = Species(DestardRole.Wyvern, "a wyvern", 55, 57, 62, poison: 75),
        [DestardRole.EnslavedDrake] = Species(DestardRole.EnslavedDrake, "an enslaved drake", 56, 58, 60, fire: 75),
        [DestardRole.LesserDaemon] = Species(DestardRole.LesserDaemon, "a lesser daemon", 57, 59, 74, MonsterArchetype.Caster),
        [DestardRole.Daemon] = Species(DestardRole.Daemon, "a daemon", 59, 60, 9),
        [DestardRole.EnslavedDragon] = Species(DestardRole.EnslavedDragon, "an enslaved dragon", 60, 62, 12, MonsterArchetype.Tank, fire: 75),
        [DestardRole.Wyrm] = Species(DestardRole.Wyrm, "a wyrm", 61, 63, 49, MonsterArchetype.Tank, cold: 75),
        [DestardRole.Archdaemon] = Species(DestardRole.Archdaemon, "an archdaemon", 62, 64, 9, MonsterArchetype.Caster),
        [DestardRole.CorruptedHatchling] = Species(DestardRole.CorruptedHatchling, "a corrupted hatchling", 20, 20, 12),
        [DestardRole.DaemonSpawn] = Species(DestardRole.DaemonSpawn, "a daemon spawn", 20, 20, 776)
    };
    private static readonly Dictionary<string, DestardRole> _roles =
        _roster.ToDictionary(entry => entry.Value.Id, entry => entry.Key, StringComparer.Ordinal);

    [SerializableField(0)] private bool _alternate;
    private long _nextSpell;
    private int _spellGeneration;

    internal static DungeonProfile FindSpecies(string id) => _roles.TryGetValue(id, out var role) ? _roster[role] : null;
    internal DestardRole Role => _roles[Dungeon.Id];
    internal bool DropsHorn => Role is DestardRole.LesserDaemon or DestardRole.Daemon or DestardRole.Archdaemon;
    internal override int? AuraRankOverride => Role is DestardRole.Archdaemon or DestardRole.CorruptedHatchling ? 10 : null;
    protected override ResistanceType MeleeElement => Dungeon.Id == _roster[DestardRole.Wyvern].Id ? ResistanceType.Poison : ResistanceType.Physical;
    protected override bool BusyWithSpecial => SpellInProgress;
    protected override bool CanCastEncounterBolt => Role != DestardRole.CorruptedHatchling;
    protected override int EncounterBoltRange => 9;
    public override bool BleedImmune => false;
    public override Poison PoisonImmune => null;
    public override OppositionGroup OppositionGroup => null;
    public override bool AlwaysMurderer => true;
    public override string CorpseName => $"{Name}'s corpse";

    private static DungeonProfile Species(DestardRole role, string name, int min, int max, int body,
        MonsterArchetype kind = MonsterArchetype.Melee, int fire = 0, int cold = 0, int poison = 0)
    {
        int sound = body switch { 74 => 422, 9 or 776 => 357, _ => 362 };
        return new($"destard-{role.ToString().ToLowerInvariant()}", "Destard", name, kind, "Destard invasion",
            min, max, body, sound, new(0, 0, fire, 0, cold, poison));
    }

    [Constructible]
    public DestardEncounter(string role) : this(
        Enum.TryParse<DestardRole>(role, true, out var selected) && Enum.IsDefined(selected)
            ? selected : throw new ArgumentException("Unknown Destard role.", nameof(role)))
    {
    }

    internal DestardEncounter(DestardRole role)
    {
        var species = _roster[role];
        var aura = role is DestardRole.Archdaemon or DestardRole.CorruptedHatchling ? EliteAura.Conviction : EliteAura.None;
        Assign(species, Utility.RandomMinMax(species.MinimumLevel, species.MaximumLevel), MonsterDifficulty.Hell,
            EncounterGrade.Ordinary, ChampionKind.Champion, [], aura, species.Name, Random.Shared);
        if (role is DestardRole.Wyrm or DestardRole.Archdaemon or DestardRole.CorruptedHatchling)
            Hue = BlackHue;
    }

    [AfterDeserialization]
    private void RestoreDefaultHue()
    {
        if ((Role, Hue) is (DestardRole.Wyrm, 0) or (DestardRole.Archdaemon, 0x489))
            Hue = BlackHue;
    }

    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon)
    {
        bool demon = dungeon.Body is 9 or 74 or 776;
        return new(dungeon.Family, false, demon, false, false, true, dungeon.Body is 12 or 49 ? 3 : 1, 0, 0, 100, 0);
    }

    protected override int[] AdjustResistances(int[] resistances)
    {
        for (int i = 0; i < resistances.Length; i++)
            resistances[i] = Math.Min(85, resistances[i]);
        return resistances;
    }

    public override void SendAppearanceTo(NetState state)
    {
        if (Role == DestardRole.CorruptedHatchling)
            SkeletonAppearancePackets.Send(state, Serial, SkeletonAppearance.CorruptedHatchling);
    }

    public override bool BeginFirebolt(Mobile target)
    {
        if (Core.TickCount < _nextSpell || ReturningHome || !NamedControl.SpecialAllowed(this))
            return false;

        bool began = Role == DestardRole.EnslavedDrake ? BeginInferno(target) : base.BeginFirebolt(target);
        if (began)
            _nextSpell = Core.TickCount + (Role is DestardRole.LesserDaemon or DestardRole.Archdaemon ? 3200 : 5200);
        return began;
    }

    public override void OnThink()
    {
        if (!Caster && Role != DestardRole.CorruptedHatchling && (Home == Point3D.Zero || Map == HomeMap && InRange(Home, HomeLeashRadius))
            && Combatant is { } enemy)
            BeginFirebolt(enemy);
        base.OnThink();
    }

    protected override void ReleaseEncounterBolt(Mobile target)
    {
        var ability = Role switch
        {
            DestardRole.Wyvern => CombatAbility.PoisonJavelin,
            DestardRole.LesserDaemon => _alternate ? CombatAbility.Fireball : CombatAbility.Lightning,
            DestardRole.EnslavedDragon => _alternate ? CombatAbility.Meteor : CombatAbility.Fireball,
            DestardRole.Daemon => CombatAbility.FireWall,
            DestardRole.Wyrm => _alternate ? CombatAbility.FrozenOrb : CombatAbility.Blizzard,
            DestardRole.Archdaemon => CombatAbility.Hydra,
            DestardRole.DaemonSpawn => CombatAbility.LowerResist,
            _ => CombatAbility.None
        };
        Alternate = !_alternate;
        switch (ability)
        {
            case CombatAbility.FrozenOrb:
                EliteBolts.CastFrozenOrb(this, target);
                break;
            case CombatAbility.LowerResist:
                NecromancerCurseCasts.NpcDamageCurse(this, target, ability, 10);
                break;
            case CombatAbility.FireWall or CombatAbility.Meteor or CombatAbility.Blizzard or CombatAbility.Hydra:
                int generation = _spellGeneration;
                DestardSpells.Cast(this, target.Location, ability, () => generation == _spellGeneration);
                break;
            case CombatAbility.None:
                break;
            default:
                EliteBolts.Cast(this, target, ability);
                break;
        }
    }

    internal override void StopCombatActions()
    {
        _spellGeneration++;
        base.StopCombatActions();
    }

    public override void OnDeath(Container corpse)
    {
        if (Role == DestardRole.EnslavedDrake)
            PublicOverheadMessage(MessageType.Regular, 0x59, false, "Free… at last.");
        if (Role == DestardRole.EnslavedDragon)
            PublicOverheadMessage(MessageType.Regular, 0x59, false, "The voice… is gone.");
        base.OnDeath(corpse);
    }
}

[SerializationGenerator(0)]
public partial class DaemonHorn : Item
{
    [Constructible]
    public DaemonHorn() : base(0x0FC7)
    {
        Stackable = true;
        Amount = 1;
        Weight = 1.0;
        Hue = 0x489;
    }

    public override string DefaultName => "daemon horn";
}
