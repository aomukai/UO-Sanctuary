// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Only the synchronous forced step bypasses voluntary hit-recovery movement.
// Terrain, doors, occupancy, regions and movement events still use Mobile.Move.
internal static class CombatDisplacement
{
    private static Mobile _moving;
    internal static bool Active(Mobile mobile) => ReferenceEquals(_moving, mobile);
    internal static bool TryStep(Mobile target, Direction direction)
    {
        if (target is not BaseCreature || target.Deleted || !target.Alive || target.Frozen || target.Paralyzed
            || target.Map == null || target.Map == Map.Internal)
        { return false; }
        var previous = _moving; var facing = target.Direction; var origin = target.Location;
        _moving = target;
        try
        {
            // Mobile.Move otherwise consumes the first request as a turn.
            target.Direction = direction;
            return target.Move(direction) && target.Location != origin;
        }
        finally { target.Direction = facing; _moving = previous; }
    }
}
