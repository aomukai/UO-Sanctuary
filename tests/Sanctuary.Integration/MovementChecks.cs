using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class MovementChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Check()
    {
        var player = new PlayerMobile { Body = 0x190, Name = "MovementCheck", Player = true };
        var legacy = new PlayerMobile { Body = 0x190, Name = "LegacyMovement" };
        var enemy = new Skeleton { Frozen = true };
        var dagger = new Dagger();
        var shield = new WoodenShield();
        try
        {
            ClassCreation.Profiles.Assign(player, DiabloClass.Paladin);
            player.Str = player.Dex = 100;
            player.Hits = player.Stam = 100;
            var point = CombatPractice.FindFloor(Map.Felucca, 1382, 1495);
            player.MoveToWorld(point, Map.Felucca);
            legacy.MoveToWorld(point, Map.Felucca);
            enemy.MoveToWorld(point, Map.Felucca);
            player.EquipItem(dagger);
            player.EquipItem(shield);
            Require(ClassCombat.For(player).Running && !ClassMovement.IsRunning(player, Core.TickCount),
                "Selecting Run removed stationary defense");
            player.Direction = Direction.East;
            Require(player.Move(Direction.East | Direction.Running) && player.Location != point,
                "Running fixture could not take a step");
            var now = Core.TickCount;
            Require(ClassMovement.IsRunning(player, now), "Accepted running step did not suppress defense");
            Require(!BaseWeapon.CheckParry(player), "Running player could parry");
            for (var i = 0; i < 20; i++)
            {
                Require(dagger.CheckHit(enemy, player), "Running player evaded a weapon attack");
            }
            Require(!ClassMovement.IsRunning(player, now + player.ComputeMovementSpeed(Direction.East | Direction.Running, false) + 1),
                "Stationary defense never recovered after a running step");
            var beforeWalk = player.Location;
            Require(player.Move(Direction.East) && player.Location != beforeWalk, "Walking fixture could not take a step");
            Require(!ClassMovement.IsRunning(player, Core.TickCount), "Walking retained the running penalty");
            player.Direction = Direction.North;
            Require(player.Move(Direction.West | Direction.Running) && player.Location != point,
                "Direction-only turn failed");
            Require(!ClassMovement.IsRunning(player, Core.TickCount), "Turning in Run mode removed defense");
            player.Frozen = true;
            var beforeBlocked = player.Location;
            Require(!player.Move(Direction.West | Direction.Running) && player.Location == beforeBlocked,
                "Frozen player unexpectedly moved");
            Require(!ClassMovement.IsRunning(player, Core.TickCount), "Failed movement removed defense");
            legacy.Direction = Direction.East;
            Require(legacy.Move(Direction.East | Direction.Running) && !ClassMovement.IsRunning(legacy, Core.TickCount),
                "Legacy movement received class running rules");
            Require(!player.Warmode && player.Combatant == null, "Movement activated war mode");
        }
        finally
        {
            player.Delete();
            legacy.Delete();
            enemy.Delete();
            dagger.Delete();
            shield.Delete();
            ClassCreation.Profiles.PruneDeleted();
        }
    }
}
