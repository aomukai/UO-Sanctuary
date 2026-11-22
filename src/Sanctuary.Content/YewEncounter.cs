// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Engines.Spawners;
using Server.Items;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class YewCultist : Spawner
{
    [Constructible]
    public YewCultist() : base(1, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4))
    {
        Name = "YewCultist";
        HomeRange = 0;
        WalkingRange = 2;
        AddEntry(nameof(YewEncounter), dotimer: false);
    }
}

[SerializationGenerator(0)]
public partial class YewEncounter : EncounterCreature
{
    internal const string BlackCultist = "yew-cultist-black", RedCultist = "yew-cultist-red";
    private const string Imp = "yew-cultist-imp", HellHound = "yew-cultist-hell-hound", HellCat = "yew-cultist-hell-cat",
        Gargoyle = "yew-cultist-gargoyle", Daemon = "yew-cultist-daemon", Marbas = "yew-daemon-marbas";
    private const int BlackHue = 0x0455, DarkRedHue = 0x0026, HealingRange = 8;
    private static readonly Dictionary<string, DungeonProfile> _species = new(StringComparer.Ordinal)
    {
        [BlackCultist] = Species(BlackCultist, "a cultist", MonsterArchetype.Caster, 28, 34, 0x190, 0),
        [RedCultist] = Species(RedCultist, "a cultist", MonsterArchetype.Caster, 25, 25, 0x190, 0),
        [Imp] = Species(Imp, "an imp", MonsterArchetype.Melee, 28, 34, 74, 422),
        [HellHound] = Species(HellHound, "a hell hound", MonsterArchetype.Caster, 28, 34, 98, 229),
        [HellCat] = Species(HellCat, "a hell cat", MonsterArchetype.Caster, 28, 34, 0xC9, 0x69),
        [Gargoyle] = Species(Gargoyle, "a gargoyle", MonsterArchetype.Melee, 28, 34, 4, 372),
        [Daemon] = Species(Daemon, "a daemon", MonsterArchetype.Melee, 25, 25, 9, 357),
        [Marbas] = Species(Marbas, "Marbas", MonsterArchetype.Caster, 33, 33, 9, 357)
    };

    [SerializableField(0)]
    private DateTime _nextHeal;
    private bool _castIceNext;

    internal bool HumanForm => Dungeon.Id is BlackCultist or RedCultist;
    internal override int EncounterPlayers => 3;
    public override bool AlwaysAttackable => HumanForm && !PaladinConversion.Active(this);
    public override string CorpseName => $"{Name}'s corpse";

    internal static DungeonProfile FindSpecies(string id) => id == null ? null : _species.GetValueOrDefault(id);
    private static DungeonProfile Species(string id, string name, MonsterArchetype archetype,
        int min, int max, int body, int sound) =>
        new(id, "yew-cult", name, archetype, "Yew", min, max, body, sound, new(0, 0, 0, 0, 0, 0));

    [Constructible]
    public YewEncounter() : this(Utility.Random(100) < 75 ? BlackCultist : RedCultist) { }

    [Constructible]
    public YewEncounter(string id)
    {
        if (id is not (BlackCultist or RedCultist or Marbas))
            throw new ArgumentException("Unknown Yew spawn.", nameof(id));

        var profile = _species[id];
        var grade = id == Marbas ? EncounterGrade.Unique : EncounterGrade.Ordinary;
        const MonsterDifficulty difficulty = MonsterDifficulty.Normal;
        var mods = grade == EncounterGrade.Unique ? EncounterProfiles.RollModifiers(profile, difficulty, Random.Shared) : [];
        int level = Utility.RandomMinMax(profile.MinimumLevel, profile.MaximumLevel);
        Assign(profile, level, difficulty, grade, ChampionKind.Champion, mods,
            EncounterProfiles.RollAura(mods, EncounterProfiles.Level(level, grade), Random.Shared), profile.Name, Random.Shared);

        if (HumanForm)
        {
            // Costume items provide the native appearance without adding UO equipment stats.
            int hue = id == RedCultist ? DarkRedHue : BlackHue;
            AddItem(new Item(0x1F03) { Layer = Layer.OuterTorso, Hue = hue, Movable = false });
            AddItem(new Item(0x1718) { Layer = Layer.Helm, Hue = hue, Movable = false });
            AddItem(new Item(0x0DF0) { Layer = Layer.TwoHanded, Hue = BlackHue, Movable = false });
        }
    }

    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon) =>
        new(dungeon.Family, false, dungeon.Body != 0x190, Leader, false, true, 1, 0, 0, Leader ? 50 : 100, 0);

    [AfterDeserialization]
    private void RestoreCultistName()
    {
        if (HumanForm && Name is ("a black-robed cultist" or "a red-robed cultist"))
            Name = Dungeon.Name;
    }

    public override void Damage(int amount, Mobile from = null, bool informMount = true, bool ignoreEvilOmen = false)
    {
        base.Damage(amount, from, informMount, ignoreEvilOmen);
        if (!Deleted && HumanForm && 4L * Hits < HitsMax)
            Transform();
    }

    public override bool OnBeforeDeath()
    {
        if (!HumanForm)
            return base.OnBeforeDeath();
        Transform();
        return false;
    }

    internal void TryTransform()
    {
        if (!Deleted && HumanForm && 4L * SkillHealth.Balance256(this) < SkillHealth.Maximum256(this))
            Transform();
    }

    private void Transform()
    {
        string form = Dungeon.Id == RedCultist ? Daemon : Utility.Random(100) switch
        {
            < 70 => Imp,
            < 80 => HellHound,
            < 90 => HellCat,
            _ => Gargoyle
        };
        FindItemOnLayer(Layer.OuterTorso)?.Delete();
        FindItemOnLayer(Layer.Helm)?.Delete();
        FindItemOnLayer(Layer.TwoHanded)?.Delete();
        var profile = _species[form];
        ChangeForm(profile, form == Daemon ? profile.MinimumLevel : Basis.Level);
        if (form == HellHound)
            RangeFight = InfernoRange;
        else if (form == HellCat)
            Hue = 0x0662;
        FixedEffect(0x3728, 10, 15);
        PlaySound(profile.Sound);
    }

    public override bool BeginFirebolt(Mobile target) =>
        Dungeon.Id == HellHound ? BeginInferno(target) : base.BeginFirebolt(target);

    protected override void ReleaseEncounterBolt(Mobile target)
    {
        switch (Dungeon.Id)
        {
            case BlackCultist:
            case RedCultist:
            case Marbas:
                EliteBolts.Cast(this, target, _castIceNext ? CombatAbility.IceBlast : CombatAbility.Lightning);
                _castIceNext = Dungeon.Id == Marbas && !_castIceNext;
                break;
            default:
                base.ReleaseEncounterBolt(target);
                break;
        }
    }

    public override void OnThink()
    {
        TryTransform();
        if (Dungeon.Id == RedCultist && Map != null && Map != Map.Internal && Core.Now >= _nextHeal && ActionReady && !SpellInProgress
            && !Controlled && !Summoned && !ReturningHome && EliteCombat.CanAct(this)
            && NamedControl.SpecialAllowed(this) && CreatureHitRecovery.Remaining(this) == 0 && TryHealAlly())
            return;
        base.OnThink();
    }

    private bool TryHealAlly()
    {
        var wounded = FindWoundedAlly<YewEncounter>(HealingRange);
        if (wounded == null)
            return false;

        NextHeal = Core.Now.AddSeconds(10);
        HealAlly(wounded, 30);
        return true;
    }
}
