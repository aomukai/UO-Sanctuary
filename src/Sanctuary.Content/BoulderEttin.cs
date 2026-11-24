// SPDX-License-Identifier: GPL-3.0-or-later
using ModernUO.Serialization;
using Server;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class BoulderEttin : FrontierEncounter
{
    private TimerExecutionToken _windup;

    [Constructible]
    public BoulderEttin(int level) : base(BoulderEttinId, level, 0)
    {
        RangeFight = RockThrowRange;
    }

    protected override bool Caster => true;
    protected override int EncounterMoveInterval(int interval) => interval * 3 / 2;
    public override string CorpseName => "an ettin's corpse";

    [AfterDeserialization]
    private void RestoreAppearance()
    {
        if (Name == "a boulder-throwing ettin")
            Name = Dungeon.Name;
        if (Hue == 0x0455)
            Hue = 0;
    }

    public override bool BeginFirebolt(Mobile target) => BeginRockThrow(target, ref _windup);

    internal override void StopCombatActions()
    {
        _windup.Cancel();
        base.StopCombatActions();
    }
}
