// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Collections;
using Server.Items;

namespace Sanctuary;

public partial class EncounterCreature
{
    private const int RockGraphic = 0x1362;
    protected const int RockThrowRange = 9;

    protected bool BeginRockThrow(Mobile target, ref TimerExecutionToken windup)
    {
        if (windup.Running || !ActionReady || !CanThrowAt(target))
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
        }, out windup);
        return true;
    }

    private bool CanThrowAt(Mobile target) =>
        EliteCombat.CanAct(this) && NamedControl.SpecialAllowed(this) && CreatureHitRecovery.Remaining(this) == 0
        && EliteCombat.Enemy(this, target, RockThrowRange);

    private void LandRock(Map map, Point3D origin, Point3D point)
    {
        if (Deleted || !Alive || Map != map || ReturningHome || !EncounterProfiles.ClearSight(map, origin, point))
            return;

        // Damage can remove targets or their summons from the map sector.
        using var targets = PooledRefList<Mobile>.Create();
        foreach (var target in map.GetMobilesInRange<Mobile>(point, 0))
        {
            targets.Add(target);
        }
        foreach (var target in targets)
        {
            if (target.InRange(point, 0) && Math.Abs(target.Z - point.Z) < 8
                && EliteCombat.Enemy(this, target, RockThrowRange + 2)
                && !ClassAvoidance.Missile(target, "arrow"))
                EliteCombat.Hit(this, target, Utility.RandomMinMax(CombatProfile.MinDamage, CombatProfile.MaxDamage),
                    ResistanceType.Physical, ranged: true);
        }

        if (map.CanFit(point, 5, checkBlocksFit: true, checkMobiles: false))
            EffectItem.Create(point, map, TimeSpan.FromSeconds(10)).ItemID = RockGraphic;
    }
}
