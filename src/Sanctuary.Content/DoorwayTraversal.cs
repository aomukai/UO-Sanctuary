// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using CalcMoves = Server.Movement.Movement;

namespace Sanctuary;

internal static class DoorwayTraversal
{
    private const int OutdoorApproachTiles = 4;

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
        if (player is not { Alive: true, Deleted: false, Frozen: false, Paralyzed: false, DesignContext: null }
            || player.Map is not { } map || map == Map.Internal || ClassCombatHooks.MovementAllowed?.Invoke(player) == false
            || door is not { Deleted: false, Parent: null } || door.Map != map
            || !doubleClick && door is BaseHouseDoor
            || door is BaseHouseDoor house && !house.CheckAccess(player))
        {
            return false;
        }

        if (BlocksMountedTravel(player, door))
            return false;

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

    internal static bool BlocksMountedTravel(Mobile from, BaseDoor door) =>
        from is PlayerMobile { Mounted: true } && !OpensOutdoors(door) && MountTravel.BlocksTravel(from);

    private static bool OpensOutdoors(BaseDoor door)
    {
        if (!HasOutdoorSides(door))
            return false;

        if (door.UseChainedFunctionality)
        {
            foreach (var leaf in door.GetChain())
                if (leaf != door && !HasOutdoorSides(leaf))
                    return false;
        }
        else if (!door.Open && door.Link is { Deleted: false, Open: false } linked && !HasOutdoorSides(linked))
        {
            return false;
        }

        return true;
    }

    private static bool HasOutdoorSides(BaseDoor door)
    {
        if (door.Map is not { } map || map == Map.Internal || door.Parent != null)
            return false;

        var threshold = Threshold(door);
        int facing = DoorwayArt.Facing(door.ClosedId, out _);
        return (facing >= 4 || LeadsOutdoors(map, threshold, Direction.North) && LeadsOutdoors(map, threshold, Direction.South))
            && (facing is >= 0 and < 4 || LeadsOutdoors(map, threshold, Direction.East) && LeadsOutdoors(map, threshold, Direction.West));
    }

    private static bool LeadsOutdoors(Map map, Point3D point, Direction direction)
    {
        // A short roofed passage can connect outdoor areas, as at Cove's guardhouse.
        for (int distance = 1; distance <= OutdoorApproachTiles; distance++)
        {
            CalcMoves.Offset(direction, ref point);
            if (point.X < 0 || point.Y < 0 || point.X >= map.Width || point.Y >= map.Height)
                return false;

            int floor = map.GetTopSurfaceZ(new(point.X, point.Y, point.Z + 2));
            if (Math.Abs(floor - point.Z) > 4)
                return false;
            point = new(point.X, point.Y, floor);

            if (Region.Find(point, map).IsPartOf<DungeonRegion>())
                return false;

            bool covered = HasOverheadCover(map, point);
            if (distance == 1 && !covered)
                return true;
            if (!map.CanFit(point, 16, checkMobiles: false))
                return false;
            if (!covered)
                return true;
        }
        return false;
    }

    private static bool HasOverheadCover(Map map, Point3D point)
    {
        int headHeight = point.Z + 16;
        foreach (var tile in map.Tiles.GetStaticAndMultiTiles(point.X, point.Y))
        {
            var data = TileData.ItemTable[tile.ID & TileData.MaxItemValue];
            if (tile.Z >= headHeight && (data.Roof || data.Surface))
                return true;
        }

        foreach (var item in map.GetItemsAt<Item>(point))
            if (!item.Movable && item.Z >= headHeight && (item.ItemData.Roof || item.ItemData.Surface))
                return true;

        return false;
    }

    private static Point3D Threshold(BaseDoor door)
    {
        var offset = door.Open ? door.Offset : default;
        return new(door.X - offset.X, door.Y - offset.Y, door.Z - offset.Z);
    }
}
