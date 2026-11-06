// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Common explicit source-frame recovery for prepared permanent summon bodies.
// The future GenericPet controller owns scheduling; this installs no timer.
internal static class PermanentSummonRecovery
{
    internal static DateTime? Advance(SkeletonGuardian body, uint ownerId, DateTime nextUtc, DateTime at, bool actions)
    {
        if (at.Kind != DateTimeKind.Utc) { throw new ArgumentException("Demon clock must be UTC"); }
        if (body.Deleted || nextUtc > at) { return null; }
        if (body.ControlMaster is not PlayerMobile { Alive: true, Deleted: false } owner || owner.Serial.Value != ownerId)
        { body.Delete(); return null; }
        long frames = (at - nextUtc).Ticks / TimeSpan.FromMilliseconds(40).Ticks + 1;
        var next = nextUtc.AddTicks(checked(frames * TimeSpan.FromMilliseconds(40).Ticks));
        if (!PersistentMinions.Active(owner, body) || !body.Permitted(owner))
        { PersistentMinions.ParkClass(owner, body.SummonClass); return next; }
        if (!actions || owner.Map == null || owner.Map == Map.Internal)
        { PersistentMinions.Suspend(owner); return next; }
        if (body.Map == Map.Internal) { PersistentMinions.Resume(owner); }
        if (body.Map != Map.Internal && body.Alive && !body.Poisoned && TimedEffects.CanRegenerateHits(body) && !SkillHealth.Full(body))
        { SkillHealth.Heal(body, (int)Math.Min(int.MaxValue, frames * BattleOrders.Regeneration256(body))); }
        body.AIObject?.AITimer.Stop(); return next;
    }
}

// Preserve the existing demon entry point while sharing its class-aware policy.
internal static class WarlockDemonRecovery
{
    internal static DateTime? Advance(SkeletonGuardian body,uint ownerId,DateTime nextUtc,DateTime at,bool actions)
        =>PermanentSummonRecovery.Advance(body,ownerId,nextUtc,at,actions);
}
