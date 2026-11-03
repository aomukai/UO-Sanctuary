// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record TaintedFireBallRoute(int Skill, int Missile, int HitFunction, int ResultFlags, uint HitFlags, int Radius);
internal static class TaintedFireBallHit
{
    private static readonly Lazy<TaintedFireBallRoute> Route = new(() =>
    {
        var rows = BalanceCatalog.Read<TaintedFireBallRoute>("tainted-fireball-hit");
        if (rows.Length != 1 || rows[0] != new TaintedFireBallRoute(422, 734, 1, 5, 2, 4))
        { throw new NotSupportedException("Unsupported pinned Tainted Fire Ball hit route"); }
        return rows[0];
    });
    // Explicit successful copy of hit01 after target selection/defense and
    // intrinsic critical. This is not an area finder or a projectile launch.
    internal static PreparedMonsterDamageResult DeliverQualified(WarlockTaintedBody body, BaseCreature target,
        TaintedFireBallPacket captured, int copiedRaw256, bool intrinsicCritical, DateTime at)
    {
        var route = Route.Value;
        if (body is not { Alive: true, Deleted: false, ControlMaster: PlayerMobile { Alive: true, Deleted: false } owner }
            || body.Prepared is not { } state || state.Owner != owner.Serial.Value || owner.Map != body.Map
            || body.Map == null || body.Map == Server.Map.Internal || !PersistentMinions.Active(owner, body) || !body.Permitted(owner)
            || target is not { Alive: true, Deleted: false } || target.Map != body.Map || !body.CanBeHarmful(target, false)
            || captured is not { Minimum256: >= 0 } || captured.Radius != route.Radius || captured.Maximum256 < captured.Minimum256
            || copiedRaw256 < captured.Minimum256 || copiedRaw256 > captured.Maximum256)
        { throw new NotSupportedException("Detached Tainted or unqualified Fire Ball copy"); }
        // The source packet can predate later gear/rank changes. Its endpoints
        // are supplied by the eventual saved missile, never recaptured here.
        var damage = NpcEquipmentCritical.ApplyIntrinsic(0, new(0, copiedRaw256 / 256.0, 0, 0, 0), new(0, 0), intrinsicCritical);
        return NpcFireSpellHit.Deliver(body, owner, target, NamedElements.To256(damage.Elements.Fire), at, "Tainted Fire Ball", route.HitFlags, damage.HitClass);
    }
}
