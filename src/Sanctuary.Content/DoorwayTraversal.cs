// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using CalcMoves = Server.Movement.Movement;

namespace Sanctuary;

internal static class DoorwayTraversal
{
    internal static bool TryDoubleClick(PlayerMobile player, BaseDoor door)
    {
        var threshold = Threshold(door);
        var direction = (threshold.X - player.X, threshold.Y - player.Y) switch
        {
            (0, -1) => Direction.North,
            (0, 1) => Direction.South,
            (-1, 0) => Direction.West,
            (1, 0) => Direction.East,
            (0, 0) when door.Open => player.Direction & Direction.Mask,
            _ => (Direction)byte.MaxValue
        };
        return TryCross(player, door, direction, doubleClick: true);
    }

    internal static bool TryCross(PlayerMobile player, BaseDoor door, Direction direction, bool doubleClick = false)
    {
        if (MountTravel.BlocksTravel(player))
            return false;
        if (player is not { Alive: true, Deleted: false, Frozen: false, Paralyzed: false, DesignContext: null }
            || player.Map is not { } map || map == Map.Internal || ClassCombatHooks.MovementAllowed?.Invoke(player) == false
            || door is not { Deleted: false, Parent: null } || door.Map != map
            || !doubleClick && door is BaseHouseDoor
            || door is BaseHouseDoor house && !house.CheckAccess(player))
        {
            return false;
        }

        if ((door.Locked || !doubleClick && door.KeyValue != 0) && !AssassinLockpicking.CanPick(player, door))
        {
            return false;
        }

        int facing = DoorwayArt.Facing(door.ClosedId, out _);
        bool northSouth = direction is Direction.North or Direction.South;
        if (!northSouth && direction is not (Direction.East or Direction.West)
            || (facing < 0 ? !doubleClick : (facing < 4) != northSouth))
        {
            return false;
        }

        var threshold = Threshold(door);
        var origin = player.Location;
        var ahead = origin;
        CalcMoves.Offset(direction, ref ahead);
        bool onThreshold = doubleClick && door.Open && origin.X == threshold.X && origin.Y == threshold.Y
            && Math.Abs(origin.Z - threshold.Z) <= 3;
        if (!onThreshold && (ahead.X != threshold.X || ahead.Y != threshold.Y) || Math.Abs(origin.Z - threshold.Z) >= 16
            || !player.CanSee(door) || !door.IsAccessibleTo(player))
        {
            return false;
        }

        if (!door.Open)
        {
            if (!player.InLOS(door) || door.UseChainedFunctionality
                || door.Link is { Deleted: false } linked
                && ((linked.Locked || !doubleClick && linked.KeyValue != 0) && !AssassinLockpicking.CanPick(player, linked)
                    || linked is BaseHouseDoor linkedHouse && (!doubleClick || !linkedHouse.CheckAccess(player))))
            {
                return false;
            }
            player.Use(door);
        }
        int stepZ = origin.Z;
        if (!door.Open || door.Deleted || player.Map != map || player.Location != origin
            || !onThreshold && (!CalcMoves.CheckMovement(player, map, origin, direction, out stepZ) || Math.Abs(stepZ - threshold.Z) > 3)
            || !CalcMoves.CheckMovement(player, map, new(threshold.X, threshold.Y, stepZ), direction, out _))
        {
            return false;
        }

        var exit = threshold;
        CalcMoves.Offset(direction, ref exit);
        var connection = player.NetState;
        var move = ClassCombat.For(player).Running ? direction | Direction.Running : direction;
        // Run both native steps so region checks, tile triggers and follower routing still see the doorway.
        connection?.ResetMovementState();
        player.Direction = direction;
        try
        {
            return (onThreshold || player.Move(move) && player.Map == map && player.Location == new Point3D(threshold.X, threshold.Y, stepZ))
                && player.Move(move) && player.Map == map && player.X == exit.X && player.Y == exit.Y;
        }
        finally
        {
            if (connection != null && !player.Deleted && player.NetState == connection)
            {
                connection.ResetMovementState();
                connection.SendMobileUpdate(player);
            }
        }
    }

    private static Point3D Threshold(BaseDoor door)
    {
        var offset = door.Open ? door.Offset : default;
        return new(door.X - offset.X, door.Y - offset.Y, door.Z - offset.Z);
    }
}
