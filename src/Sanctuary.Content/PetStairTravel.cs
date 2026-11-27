// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.CompilerServices;
using Server;
using Server.Collections;
using Server.Mobiles;

namespace Sanctuary;

internal static class PetStairTravel
{
    private sealed class Crossing
    {
        internal TimerExecutionToken Timer;
    }

    private static readonly ConditionalWeakTable<PlayerMobile, Crossing> _pending = new();

    internal static void Walked(PlayerMobile owner, Map map, Point3D from)
    {
        var to = owner.Location;
        if (owner.AllFollowers is not { Count: > 0 } || owner.Map != map || !IsStairStep(map, from, to))
            return;

        var crossing = _pending.GetOrCreateValue(owner);
        crossing.Timer.Cancel();
        Timer.StartTimer(TimeSpan.FromSeconds(2), () => CatchUp(owner, map, from, to), out crossing.Timer);
    }

    internal static void CatchUp(PlayerMobile owner, Map map, Point3D from, Point3D to)
    {
        if (owner is not { Alive: true, Deleted: false, AllFollowers: { Count: > 0 } followers }
            || owner.Map != map || !owner.InRange(to, 10) || Math.Abs(owner.Z - to.Z) > 3 || !IsStairStep(map, from, to))
            return;

        int direction = Math.Sign(to.Z - from.Z);
        using var stranded = PooledRefList<BaseCreature>.Create();
        foreach (var follower in followers)
            if (follower is BaseCreature { Deleted: false, Alive: true, IsStabled: false, Combatant: null } pet
                && pet.GetMaster() == owner && pet.Map == map && !MountTravel.Managed(pet)
                && (pet.ControlOrder is OrderType.Guard or OrderType.Come
                    || pet.ControlOrder == OrderType.Follow && pet.ControlTarget == owner)
                && pet.InRange(from, 10) && Math.Abs(pet.Z - from.Z) <= 24 && (owner.Z - pet.Z) * direction >= 3)
                stranded.Add(pet);

        foreach (var pet in stranded)
            if (PersistentMinions.FindPosition(map, owner.Location,
                    spot => Math.Abs(spot.Z - owner.Z) < 3 && owner.InLOS(spot), out var landing))
                PersistentMinions.MoveFollower(pet, landing, map);
    }

    private static bool IsStairStep(Map map, Point3D from, Point3D to) =>
        map != null && map != Map.Internal && (from.X != to.X || from.Y != to.Y) && Utility.InRange(from, to, 1)
        && Math.Abs(to.Z - from.Z) is > 0 and <= 8 && (HasTread(map, from) || HasTread(map, to));

    private static bool HasTread(Map map, Point3D point)
    {
        foreach (var tile in map.Tiles.GetStaticAndMultiTiles(point.X, point.Y))
        {
            var data = TileData.ItemTable[tile.ID & TileData.MaxItemValue];
            if (data.Bridge && data.Surface && !data.Impassable && tile.Z + data.CalcHeight == point.Z)
                return true;
        }

        foreach (var item in map.GetItemsInRange<Item>(point, 0))
        {
            var data = item.ItemData;
            if (!item.Movable && data.Bridge && data.Surface && !data.Impassable && item.Z + data.CalcHeight == point.Z)
                return true;
        }
        return false;
    }
}
