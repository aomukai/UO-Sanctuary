using System;
using Server;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class StationaryAttackChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
    private static bool Attack(PlayerMobile from, CombatAbility ability, Mobile target, Point3D point, long now)
    {
        bool started = ClassCombat.ContinueAttack(from, ability, target, point, now);
        ClassCombat.ResolveAction(from, ClassCombat.For(from).EffectAt);
        return started;
    }
    public static void Check()
    {
        var player = new PlayerMobile { Body = 0x190, Name = "StationaryAttack", Player = true, Str = 100 };
        var enemy = new Skeleton { Frozen = true };
        var second = new Skeleton { Frozen = true };
        var vendor = new Blacksmith();
        try
        {
            ClassCreation.Profiles.Assign(player, DiabloClass.Barbarian);
            var point = CombatPractice.FindFloor(Map.Felucca, 1382, 1495);
            foreach (var mobile in new Mobile[] { player, enemy, second, vendor })
            {
                mobile.MoveToWorld(point, Map.Felucca);
            }
            enemy.SetHits(200);
            second.SetHits(200);
            enemy.Hits = second.Hits = 200;
            var hitHp = 200 - CombatTestActions.HitDamage(player, enemy);
            var session = ClassCombat.For(player);
            var start = Core.TickCount;
            var cycle = CombatTiming.For(player, CombatAbility.BasicAttack).DurationMs;
            var aim = new Point3D(point.X + 8, point.Y, point.Z);
            // Repeated empty swings are real actions, even with enemies beside us.
            for (var elapsed = 0; elapsed < (cycle * 3); elapsed += CombatProtocol.CombatTickMs)
            {
                var now = start + elapsed;
                if (elapsed % CombatProtocol.HoldHeartbeatMs == 0)
                {
                    Require(Attack(player, session.Left, null, aim, now), "Empty swing rejected");
                }
                ClassCombat.UpdateCombat(player, now);
            }
            Require(session.NextAction == start + (cycle * 3) && enemy.Hits == 200 && second.Hits == 200,
                "Empty swings bypassed cadence or acquired an unselected creature");
            Require(player.Location == point && session.Target == Serial.Zero, "Stationary attack started pursuit");
            var time = start + (cycle * 3);
            Require(Attack(player, session.Left, enemy, enemy.Location, time)
                && enemy.Hits == hitHp && second.Hits == 200, "Selected in-range creature was not the only melee victim");
            ClassCombat.ReleaseCast(player);
            Attack(player, session.Left, enemy, enemy.Location, time);
            Require(enemy.Hits == hitHp, "Release/restart bypassed melee cooldown");
            time += cycle;
            enemy.MoveToWorld(aim, Map.Felucca);
            Attack(player, session.Left, enemy, aim, time);
            Require(enemy.Hits == hitHp && session.NextAction == time + cycle, "Out-of-range aim did not swing harmlessly");
            time += cycle;
            enemy.MoveToWorld(new Point3D(point.X, point.Y, point.Z + 20), Map.Felucca);
            Attack(player, session.Left, enemy, enemy.Location, time);
            Require(enemy.Hits == hitHp && session.NextAction == time + cycle, "Melee hit another elevation");
            time += cycle;
            Require(!Attack(player, session.Left, vendor, vendor.Location, time), "Force attack accepted a friendly victim");
            Require(!Attack(player, CombatAbility.Teeth, null, aim, time), "Left hold ignored its binding");
            player.Frozen = true;
            Attack(player, session.Left, null, aim, time);
            Require(session.NextAction == time, "Frozen player swung");
            player.Frozen = false;
            Attack(player, session.Left, null, aim, time);
            ClassCombat.UpdateCombat(player, time + CombatProtocol.HoldLeaseMs);
            Require(session.HeldAbility == CombatAbility.None && session.NextAction == time + cycle,
                "Abandoned left hold did not expire");
            time += cycle;
            Attack(player, session.Left, null, aim, time);
            session.Left = CombatAbility.None;
            ClassCombat.UpdateCombat(player, time + 40);
            Require(session.HeldAbility == CombatAbility.None, "Changed binding retained its held attack");
            session.Left = CombatAbility.BasicAttack;
            time += cycle;
            enemy.MoveToWorld(point, Map.Felucca);
            enemy.Hits = 1;
            Attack(player, session.Left, enemy, point, time);
            Require(!enemy.Alive && session.HeldAbility == CombatAbility.None, "Death retained held victim");
            ClassCombat.UpdateCombat(player, time + 5000);
            Require(second.Hits == 200 && session.Target == Serial.Zero, "Stationary kill acquired a replacement");
            Require(!player.Warmode && player.Combatant == null, "Stationary swing enabled legacy combat");
            session.Right = CombatAbility.BasicAttack;
            time += cycle * 10;
            Require(ClassCombat.ContinueCast(player, session.Right, null, aim, time), "Right-bound melee rejected an empty swing");
            ClassCombat.ResolveAction(player, session.EffectAt);
            Require(session.NextAction == time + cycle && second.Hits == 200 && player.Location == point,
                "Right-bound empty melee moved, selected a victim or bypassed swing speed");
        }
        finally
        {
            player.Delete();
            enemy.Delete();
            second.Delete();
            vendor.Delete();
            ClassCreation.Profiles.PruneDeleted();
        }
    }
}
