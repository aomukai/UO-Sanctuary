// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class OrcFortEncounter : EncounterCreature
{
    private const string Guard = "west-orc-guard", Shaman = "west-orc-shaman",
        Trapper = "west-orc-trapper", Archer = "west-orc-trapper-archer";
    protected const string Scout = "west-orc-scout";
    private static readonly DungeonProfile _guard = Species(Guard, "an orc guard", MonsterArchetype.Melee, 25, 138);
    private static readonly DungeonProfile _shaman = Species(Shaman, "an orc shaman", MonsterArchetype.Caster, 23, 140);
    private static readonly DungeonProfile _trapper = Species(Trapper, "an orc trapper", MonsterArchetype.Melee, 24, 17);
    private static readonly DungeonProfile _archer = Species(Archer, "an orc trapper", MonsterArchetype.Ranged, 24, 17);
    private static readonly DungeonProfile _scout = Species(Scout, "an orc scout", MonsterArchetype.Ranged, 23, 17);
    private static readonly string[] _rally = ["To da shaman!", "Guard da shaman!", "Keep 'em off da shaman!"];

    private bool _inCombat;
    private long _nextCry;

    internal bool FortDefender => Dungeon.Id is Guard or Shaman;
    protected override int RegionalEncounterPlayers => 3;
    internal override int HomeLeashRadius => FortDefender ? 20 : 10;
    public override string CorpseName => $"{Name}'s corpse";
    private bool Alerted => AIObject is OrcFortAI { Alerted: true };
    public override bool PlayerRangeSensitive => !Alerted && base.PlayerRangeSensitive;
    public override bool ReturnsToHome => !Alerted && base.ReturnsToHome;
    protected override BaseAI ForcedAI => new OrcFortAI(this);

    internal static DungeonProfile FindSpecies(string id) => id switch
    {
        Guard => _guard,
        Shaman => _shaman,
        Trapper => _trapper,
        Archer => _archer,
        Scout => _scout,
        _ => null
    };

    private static DungeonProfile Species(string id, string name, MonsterArchetype archetype, int level, int body) =>
        new(id, "Orc", name, archetype, "Western orc fort", level, level, body, 0x45A, new(0, 0, 0, 0, 0, 0));

    [Constructible]
    public OrcFortEncounter(string role) : this(role switch
    {
        "guard" => _guard,
        "shaman" => _shaman,
        "trapper" => Utility.RandomBool() ? _trapper : _archer,
        _ => throw new ArgumentException("Unknown fort orc role.", nameof(role))
    }) { }

    protected OrcFortEncounter() : this(_scout) { }

    private OrcFortEncounter(DungeonProfile profile)
    {
        var grade = profile == _shaman ? EncounterGrade.Champion : EncounterGrade.Ordinary;
        Assign(profile, profile.MinimumLevel, MonsterDifficulty.Normal, grade, ChampionKind.Champion,
            [], EliteAura.None, profile.Name, Random.Shared);
    }

    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon) =>
        new(dungeon.Family, false, false, false, false, true, 1, 0, 100, 100, 0);

    public override void OnCombatantChange()
    {
        base.OnCombatantChange();
        if (Dungeon.Id != Guard)
            return;

        bool engaged = Combatant != null;
        if (engaged && !_inCombat && !Controlled && !Summoned && !PaladinConversion.Active(this)
            && Core.TickCount >= _nextCry && Utility.Random(10) == 0)
        {
            _nextCry = Core.TickCount + 8000;
            Bark(Utility.RandomList(_rally));
        }
        _inCombat = engaged;
    }

    internal void Bark(string line) => PublicOverheadMessage(MessageType.Regular, 0x0021, false, line);
}

internal sealed class OrcFortAI(OrcFortEncounter defender) : LeashedMeleeAI(defender)
{
    private long _alertUntil = Core.TickCount;

    internal bool Alerted => Core.TickCount < _alertUntil;

    internal void ReceiveWarning()
    {
        _alertUntil = Core.TickCount + 12000;
        defender.ForceReacquire();
        AITimer.Prod();
    }

    protected override bool HandleIdleMovement()
    {
        if (!Alerted)
            return false;

        // The raised guards stay with the shaman while the courtyard guards check the gate.
        var gate = OrcFortScoutAI.GatePassage;
        if (Math.Abs(defender.Home.Z - gate.Z) < 8 && !PathFollower.Check(defender.Location, new Point3D(gate), 2))
            MoveToPoint(gate, 2);
        else
        {
            ClearMoveIntent();
            defender.Direction = defender.GetDirectionTo(OrcFortScoutAI.GateApproach);
        }
        return true;
    }
}
