using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class MeleeReachChecks
{
    private static void Require(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Check()
    {
        var player = new PlayerMobile { Body = 0x190, Name = "ReachCheck", Player = true };
        var enemy = new Skeleton { Frozen = true };
        var dagger = new Dagger();
        var halberd = new Halberd();
        var wall = new Static(0x80);
        var door = new MetalDoor(DoorFacing.NorthCW);
        ClassGear.Profiles.MarkStarter(dagger);
        try
        {
            ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
            player.Str = player.Dex = player.Int = 100;
            var point = CombatPractice.FindFloor(Map.Trammel, 5450, 1150);
            player.MoveToWorld(point, Map.Trammel);
            enemy.SetHits(10000);
            enemy.Hits = 10000;
            Require(player.EquipItem(dagger), "Cannot equip reach dagger");
            Require(CombatTiming.Range(player, CombatAbility.BasicAttack) == 1, "Dagger reach changed");
            long now = Core.TickCount;
            var session = ClassCombat.For(player);

            void Start(int dx, int dy, bool allowed)
            {
                now = Math.Max(now + 2000, session.NextAction);
                enemy.MoveToWorld(new Point3D(point.X + dx, point.Y + dy, point.Z), player.Map);
                Require(ClassCombat.Begin(player, enemy), "Could not select reach enemy");
                ClassCombat.UpdateCombat(player, now);
                Require((session.PendingAbility == CombatAbility.BasicAttack) == allowed, $"Wrong admission at {dx},{dy}");
            }
            void Impact(bool hit)
            {
                int hp = enemy.Hits;
                long deadline = session.NextAction;
                ClassCombat.ResolveAction(player, session.EffectAt - 1);
                Require(enemy.Hits == hp, "Reach comparison hit before effect frame");
                ClassCombat.ResolveAction(player, session.EffectAt);
                Require((enemy.Hits < hp) == hit && session.NextAction == deadline, "Wrong impact result or refunded cycle");
                hp = enemy.Hits;
                ClassCombat.ResolveAction(player, session.EffectAt);
                Require(enemy.Hits == hp, "Reach comparison resolved twice");
            }

            Start(2, 0, false);
            Start(1, 1, true);
            Impact(true);
            Start(1, 0, true);
            enemy.MoveToWorld(new Point3D(point.X + 2, point.Y, point.Z), player.Map);
            Impact(false);

            ClassCombat.Stop(player);
            player.AddToBackpack(dagger);
            Require(player.EquipItem(halberd), "Cannot equip reach halberd");
            Require(halberd.MaxRange == 1 && CombatTiming.Range(player, CombatAbility.BasicAttack) == 2,
                "Halberd comparison changed native UO reach or failed to apply class reach");
            Start(3, 0, false);
            Start(2, 0, true);
            Require(session.Timing.DurationMs == 800 && session.Timing.EffectMs == 400, "Reach changed halberd timing");
            Impact(true);
            Start(2, 2, true);
            Impact(true);
            Start(2, 0, true);
            enemy.MoveToWorld(new Point3D(point.X + 3, point.Y, point.Z), player.Map);
            Impact(false);
            Start(2, 0, true);
            enemy.MoveToWorld(new Point3D(point.X + 2, point.Y, point.Z + 16), player.Map);
            Impact(false);
            Start(2, 0, true);
            wall.MoveToWorld(new Point3D(point.X + 1, point.Y, point.Z), player.Map);
            Require(!player.InLOS(enemy), "Wall fixture does not obstruct melee LOS");
            Impact(false);
            Start(2, 0, false);
            wall.Internalize();
            door.MoveToWorld(new Point3D(point.X + 1, point.Y, point.Z), player.Map);
            Require(!player.InLOS(enemy), "Closed door fixture does not obstruct melee LOS");
            Start(2, 0, false);
            door.Open = true;
            Start(2, 0, true);
            Impact(true);
            door.Internalize();
            Start(2, 0, true);
            player.AddToBackpack(halberd);
            player.EquipItem(dagger);
            Impact(false);
        }
        finally
        {
            player.Delete();
            enemy.Delete();
            dagger.Delete();
            halberd.Delete();
            wall.Delete();
            door.Delete();
            ClassCreation.Profiles.PruneDeleted();
        }
    }
}
