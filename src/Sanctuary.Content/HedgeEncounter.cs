// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Engines.Spawners;
using Server.Items;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class HedgeGazer : Spawner
{
    [Constructible]
    public HedgeGazer() : base(1, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4))
    {
        Name = "HedgeGazer";
        HomeRange = 0;
        WalkingRange = 2;
        AddEntry(nameof(HedgeEncounter), dotimer: false);
    }
}

[SerializationGenerator(0)]
public partial class HedgeEncounter : EncounterCreature
{
    private const string Larva = "hedge-gazer-larva";
    private const string Gazer = "hedge-gazer";
    private const string Elder = "hedge-elder-gazer";
    private const string Imp = "hedge-imp";
    private const string Gargoyle = "hedge-gargoyle";
    private const string StoneGargoyle = "hedge-stone-gargoyle";
    private const string Mage = "hedge-evil-mage";
    private const string NamedGazer = "hedge-champion-gazer";
    internal const string WatchDaemon = "hedge-watch-daemon";
    private const int CurseRange = 6, CurseCooldownMs = 12000;

    private static readonly Dictionary<string, DungeonProfile> _species = new(StringComparer.Ordinal)
    {
        [Larva] = Species(Larva, "a gazer larva", 30, 35, 778, 377),
        [Gazer] = Species(Gazer, "a gazer", 33, 38, 22, 377, magic: 15),
        [Elder] = Species(Elder, "an elder gazer", 38, 42, 22, 377, magic: 35, fire: 25),
        [Imp] = Species(Imp, "an imp", 30, 35, 74, 422, archetype: MonsterArchetype.Melee),
        [Gargoyle] = Species(Gargoyle, "a gargoyle", 33, 38, 4, 372, archetype: MonsterArchetype.Melee),
        [StoneGargoyle] = Species(StoneGargoyle, "a stone gargoyle", 38, 42, 67, 372, archetype: MonsterArchetype.Tank),
        // Champion and SuperUnique add two and three levels respectively.
        [Mage] = Species(Mage, "an evil mage", 42, 42, 0x190, 0),
        [NamedGazer] = Species(NamedGazer, "Veyr the Many-Eyed", 40, 40, 22, 377, magic: 15),
        [WatchDaemon] = Species(WatchDaemon, "Orvath", 47, 47, 9, 357, archetype: MonsterArchetype.Melee)
    };

    private static readonly CombatAbility[] _championSpells =
        [CombatAbility.LightningBolt, CombatAbility.Lightning, CombatAbility.ChainLightning];
    private int _nextChampionSpell;
    private long _curseAfter = Core.TickCount;
    private TimerExecutionToken _curseCast;

    public override bool AlwaysAttackable => Dungeon.Id == Mage && !PaladinConversion.Active(this);
    public override string CorpseName => $"{Name}'s corpse";

    internal static DungeonProfile FindSpecies(string id) => id == null ? null : _species.GetValueOrDefault(id);
    private static DungeonProfile Species(string id, string name, int min, int max, int body, int sound,
        int magic = 0, int fire = 0, MonsterArchetype archetype = MonsterArchetype.Caster) =>
        new(id, "hedge-maze", name, archetype, "Hedge Maze", min, max, body, sound, new(0, magic, fire, 0, 0, 0));

    [Constructible]
    public HedgeEncounter() : this(Utility.Random(100) switch
    {
        < 30 => Larva,
        < 60 => Imp,
        < 75 => Gazer,
        < 90 => Gargoyle,
        < 95 => Elder,
        _ => StoneGargoyle
    }) { }

    [Constructible]
    public HedgeEncounter(string id)
    {
        var profile = FindSpecies(id) ?? throw new ArgumentException("Unknown Hedge Maze spawn.", nameof(id));
        var grade = id switch
        {
            Mage => EncounterGrade.SuperUnique,
            NamedGazer => EncounterGrade.Champion,
            WatchDaemon => EncounterGrade.Unique,
            _ => EncounterGrade.Ordinary
        };
        EliteModifier[] modifiers = id switch
        {
            Mage => [EliteModifier.StoneSkin, EliteModifier.Teleport],
            NamedGazer => [EliteModifier.ExtraFast, EliteModifier.Teleport],
            _ => []
        };
        int level = Utility.RandomMinMax(profile.MinimumLevel, profile.MaximumLevel);
        Assign(profile, level, MonsterDifficulty.Normal, grade, ChampionKind.Champion, modifiers,
            EliteAura.None, profile.Name, Random.Shared);

        if (id == Elder)
            Hue = 0x048F;
        else if (id == Mage)
        {
            AddItem(new Item(0x1F03) { Layer = Layer.OuterTorso, Hue = 0x0455, Movable = false });
            AddItem(new Item(0x1718) { Layer = Layer.Helm, Hue = 0x0455, Movable = false });
            AddItem(new Item(0x1711) { Layer = Layer.Shoes, Movable = false });
        }
    }

    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon) =>
        new(dungeon.Family, false, dungeon.Id != Mage, Leader, false, true, 1, 0, 0,
            Leader || dungeon.Id == NamedGazer ? 50 : 100, 0);

    public override void OnThink()
    {
        base.OnThink();
        var ability = Dungeon.Id switch
        {
            Imp => CombatAbility.LowerResist,
            Gargoyle => CombatAbility.Decrepify,
            StoneGargoyle => CombatAbility.AmplifyDamage,
            _ => CombatAbility.None
        };
        var target = Combatant;
        if (ability == CombatAbility.None || Core.TickCount < _curseAfter || !ActionReady || _curseCast.Running
            || !CanCurse(target))
            return;

        var map = Map;
        var origin = Location;
        _curseAfter = Core.TickCount + CurseCooldownMs;
        StartAction(target);
        Direction = GetDirectionTo(target);
        Animate(4, 5, 1, true, false, 0);
        NecromancerCurseVisuals.Cast(this);
        Timer.StartTimer(TimeSpan.FromMilliseconds(Basis.EffectMs), () =>
        {
            if (Map == map && Location == origin && Combatant == target && CanCurse(target))
                NecromancerCurseCasts.NpcDamageCurse(this, target, ability, rank: 1);
        }, out _curseCast);
    }

    private bool CanCurse(Mobile target) =>
        !Controlled && !Summoned && !ReturningHome && EliteCombat.CanAct(this)
        && NamedControl.SpecialAllowed(this) && CreatureHitRecovery.Remaining(this) == 0
        && EliteCombat.Enemy(this, target, CurseRange);

    internal override void StopCombatActions()
    {
        _curseCast.Cancel();
        base.StopCombatActions();
    }

    protected override void ReleaseEncounterBolt(Mobile target)
    {
        var ability = Dungeon.Id switch
        {
            Mage => CombatAbility.Fireball,
            Larva => CombatAbility.LightningBolt,
            Gazer => CombatAbility.Lightning,
            Elder => CombatAbility.ChainLightning,
            _ => _championSpells[_nextChampionSpell]
        };
        if (Dungeon.Id == NamedGazer)
            _nextChampionSpell = (_nextChampionSpell + 1) % _championSpells.Length;
        EliteBolts.Cast(this, target, ability);
    }
}
