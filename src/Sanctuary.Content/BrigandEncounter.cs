// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class BrigandEncounter : EncounterCreature
{
    private const string Melee = "camp-brigand-melee", Archer = "camp-brigand-archer", Healer = "camp-brigand-healer";
    private const int HealRange = 8, HealPercent = 15, CastIntervalMs = 3000, LightningCooldownMs = 9000;
    private static readonly DungeonProfile _melee = Species(Melee, MonsterArchetype.Melee);
    private static readonly DungeonProfile _archer = Species(Archer, MonsterArchetype.Ranged);
    private static readonly DungeonProfile _healer = Species(Healer, MonsterArchetype.Caster);

    private long _nextCast, _nextLightning;

    internal static DungeonProfile FindSpecies(string id) => id switch
    {
        Melee => _melee,
        Archer => _archer,
        Healer => _healer,
        _ => null
    };

    private static DungeonProfile Species(string id, MonsterArchetype archetype) =>
        new(id, "Brigand", "a brigand", archetype, "Yew–Britain brigand camp", 28, 32, 0x190, 0,
            new(0, 0, 0, 0, 0, 0));

    [Constructible]
    public BrigandEncounter()
    {
        var profile = Utility.Random(100) switch
        {
            < 70 => _melee,
            < 80 => _archer,
            _ => _healer
        };
        int level = Utility.RandomMinMax(profile.MinimumLevel, profile.MaximumLevel);
        Assign(profile, level, MonsterDifficulty.Normal, EncounterGrade.Ordinary, ChampionKind.Champion,
            [], EliteAura.None, null, Random.Shared);

        Female = Utility.RandomBool();
        Body = Female ? 0x191 : 0x190;
        Hue = Race.Human.RandomSkinHue();
        Utility.AssignRandomHair(this);
        AddCostume(0x1EFD, Layer.Shirt, Utility.RandomNeutralHue());
        AddCostume(0x1539, Layer.Pants, Utility.RandomNeutralHue());
        AddCostume(0x170B, Layer.Shoes, 0x0455);
        AddCostume(0x1540, Layer.Helm, profile == _healer ? 0x0026 : Utility.RandomNeutralHue());
        // The weapon art identifies roles. Encounter profiles drive their attacks.
        if (profile == _melee)
            AddCostume(0x0F5E, Layer.OneHanded, 0);
        else
            AddCostume(profile == _archer ? 0x13B2 : 0x13F8, Layer.TwoHanded, 0);
    }

    private void AddCostume(int graphic, Layer layer, int hue) =>
        AddItem(new Item(graphic) { Layer = layer, Hue = hue, Movable = false });

    public override bool AlwaysMurderer => true;
    public override string CorpseName => "a brigand's corpse";
    protected override int RegionalEncounterPlayers => 3;
    internal override int HomeLeashRadius => 10;
    protected override CreatureTraits EncounterTraits(DungeonProfile dungeon) =>
        new(dungeon.Family, false, false, false, false, true, 1, 0, 100, 100, 0);

    // Healers choose their own spells at a slower cadence than ordinary casters.
    public override bool BeginFirebolt(Mobile target) => Dungeon.Id != Healer && base.BeginFirebolt(target);

    protected override void ReleaseEncounterBolt(Mobile target)
    {
        if (Dungeon.Id == Healer)
            EliteBolts.Cast(this, target, CombatAbility.Lightning);
        else
            base.ReleaseEncounterBolt(target);
    }

    public override void OnThink()
    {
        if (Dungeon.Id == Healer && Map != null && Map != Map.Internal && !ReturningHome && !Controlled && !Summoned
            && Core.TickCount >= _nextCast && ActionReady && !SpellInProgress && EliteCombat.CanAct(this)
            && NamedControl.SpecialAllowed(this) && CreatureHitRecovery.Remaining(this) == 0 && TrySupport())
            return;
        base.OnThink();
    }

    private bool TrySupport()
    {
        long now = Core.TickCount;
        _nextCast = now + CastIntervalMs;

        if (now >= _nextLightning && Utility.Random(100) < 20 && base.BeginFirebolt(Combatant))
        {
            _nextLightning = now + LightningCooldownMs;
            return true;
        }
        var ally = FindWoundedAlly<BrigandEncounter>(HealRange);
        if (ally == null)
            return false;

        HealAlly(ally, HealPercent);
        return true;
    }
}
