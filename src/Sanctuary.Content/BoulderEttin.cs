// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class BoulderEttin : FrontierEncounter
{
    private const int RockGraphic = 0x1362;
    private const int ThrowRange = 9;
    private TimerExecutionToken _windup;

    [Constructible]
    public BoulderEttin(int level) : base(BoulderEttinId, level, 0)
    {
        RangeFight = ThrowRange;
        Hue = 0x0455;
    }

    protected override bool Caster => true;
    protected override int EncounterMoveInterval(int interval) => interval * 3 / 2;
    public override string CorpseName => "an ettin's corpse";

    public override bool BeginFirebolt(Mobile target)
    {
        if (_windup.Running || !ActionReady || !CanThrowAt(target))
            return false;

        var origin = Location;
        var map = Map;
        StartAction(target);
        Direction = GetDirectionTo(target);
        Animate(4, 5, 1, true, false, 0);
        Timer.StartTimer(TimeSpan.FromMilliseconds(500), () =>
        {
            if (Map != map || Location != origin || !CanThrowAt(target))
                return;

            var point = target.Location;
            int distance = Math.Max(Math.Abs(point.X - X), Math.Abs(point.Y - Y));
            MovingEffect(new Entity(Serial.Zero, point, map), RockGraphic, 10, 0, false, false);
            Timer.StartTimer(TimeSpan.FromMilliseconds(150 + distance * 40), () => LandRock(map, origin, point));
        }, out _windup);
        return true;
    }

    private bool CanThrowAt(Mobile target) =>
        EliteCombat.CanAct(this) && NamedControl.SpecialAllowed(this) && CreatureHitRecovery.Remaining(this) == 0
        && EliteCombat.Enemy(this, target, ThrowRange);

    private void LandRock(Map map, Point3D origin, Point3D point)
    {
        if (Deleted || !Alive || Map != map || ReturningHome || !EncounterProfiles.ClearSight(map, origin, point))
            return;
        foreach (var target in map.GetMobilesInRange<Mobile>(point, 0))
        {
            if (Math.Abs(target.Z - point.Z) < 8 && EliteCombat.Enemy(this, target, ThrowRange + 2)
                && !ClassAvoidance.Missile(target, "arrow"))
                EliteCombat.Hit(this, target, Utility.RandomMinMax(CombatProfile.MinDamage, CombatProfile.MaxDamage),
                    ResistanceType.Physical, ranged: true);
        }

        if (map.CanFit(point, 5, checkBlocksFit: true, checkMobiles: false))
            EffectItem.Create(point, map, TimeSpan.FromSeconds(10)).ItemID = RockGraphic;
    }

    internal override void StopCombatActions()
    {
        _windup.Cancel();
        base.StopCombatActions();
    }
}
