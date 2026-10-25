// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

// Stationary monster path initialization updates tTargetCoord from a nonzero
// unit target. PATH_SetTargetUnit alone does not. A later getter prefers the
// current unit position, including a dead-but-present unit, without updating
// that cached path coordinate. Native maps supply source cells at two per tile.
internal static class AssassinSentryAim
{
    internal static Point2D? Begin(Point2D? cached,Point2D? target)
    {
        Validate(cached);Validate(target);
        // D2Common_10142 rejects a unit with either coordinate zero before
        // PATH_UpdateTargetUnit; preserve the previous path coordinate then.
        return target is{} p && p.X!=0 && p.Y!=0?p:cached;
    }
    internal static Point2D Resolve(Point2D? cached,Point2D? present)
    {
        Validate(cached);Validate(present);
        return present??cached??throw new NotSupportedException("Legacy sentry save has no retained target-path coordinate");
    }
    private static void Validate(Point2D? point)
    {
        if(point is{} p && (p.X is <0 or >65535 || p.Y is <0 or >65535))
        {throw new NotSupportedException("Sentry aim exceeds source uint16 coordinates");}
    }
}
