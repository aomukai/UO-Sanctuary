// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class MausoleumDemon : EncounterCreature
{
    private static readonly DungeonProfile[] Roster =
    [
        Species("red-vex", "Red Vex", 149, true), Species("blackjade", "Blackjade", 149, true),
        Species("cinder-fiend", "a cinder fiend", 74), Species("ice-fiend", "an ice fiend", 43),
        Species("storm-fiend", "a storm fiend", 9), Species("venom-fiend", "a venom fiend", 776),
        Species("iron-fiend", "an iron fiend", 22)
    ];

    [SerializableField(0)] private ArchbishopLazarus _archbishop;
    internal int Role => Array.IndexOf(Roster, Dungeon);
    internal bool IsBodyguard => Role is 0 or 1;
    internal ushort BoltGraphic => Role == 0 ? FlareGraphics.RedFlight : FlareGraphics.BlueFlight;

    private static DungeonProfile Species(string id, string name, int body, bool guard = false) =>
        new("mausoleum-" + id, "Deceit", name, guard ? MonsterArchetype.Caster : MonsterArchetype.Melee,
            "Lazarus's court", 50, 50, body, guard ? 0x4B0 : 357, new(20, 10, 20, 20, 20, 20));

    internal static DungeonProfile FindSpecies(string id) => Array.Find(Roster, p => p.Id == id);

    internal MausoleumDemon(ArchbishopLazarus boss, int role)
    {
        Archbishop = boss;
        var profile = Roster[role];
        Assign(profile, 50, MonsterDifficulty.Nightmare, role < 2 ? EncounterGrade.SuperUnique : EncounterGrade.Ordinary,
            ChampionKind.Champion, [], EliteAura.None, profile.Name, Random.Shared);
        if (!IsBodyguard)
            Hue = LazarusMausoleum.Circles[role - 2].Hue;
    }

    internal override int HomeLeashRadius => WorldDecorationGraphics.MausoleumSize;
    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon) =>
        new("Deceit", false, true, Leader, false, true, 2, 0, 0, 100, 0);
    public override OppositionGroup OppositionGroup => null;
    public override bool AlwaysMurderer => true;
    internal override bool IsEnemyIgnoringLeash(Mobile mobile) => !Blessed && mobile != null
        && ArchbishopLazarus.Hall.Bounds.Contains(mobile.Location) && base.IsEnemyIgnoringLeash(mobile);

    protected override bool OnMove(Direction direction) =>
        !(IsBodyguard && _archbishop is { Challenged: false }) && base.OnMove(direction);

    protected override void ReleaseEncounterBolt(Mobile target) =>
        EliteBolts.Cast(this, target, Role == 0 ? CombatAbility.FireBolt : CombatAbility.IceBlast);

    protected override void Deal(Mobile target, int damage, bool fire)
    {
        switch (Role)
        {
            case 2: EliteCombat.Damage(this, target, fire: damage); break;
            case 3: EliteCombat.Damage(this, target, cold: damage); break;
            case 4: EliteCombat.Damage(this, target, lightning: damage); break;
            case 5: EliteCombat.Hit(this, target, damage, ResistanceType.Poison); break;
            default: base.Deal(target, damage, fire); break;
        }
    }

    public override void OnDeath(Container corpse)
    {
        _archbishop?.Lost(this, true);
        base.OnDeath(corpse);
    }

    public override void OnDelete()
    {
        _archbishop?.Lost(this, false);
        base.OnDelete();
    }
}
