// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// Shared Hydra/Tainted post-defense fire packet receiver. The caller owns
// missile/area selection, block/dodge, intrinsic critical, RNG and scheduling.
internal static class NpcFireSpellHit
{
    internal static PreparedMonsterDamageResult Deliver(BaseCreature emitter, PlayerMobile owner, BaseCreature target,
        int fire256, DateTime at, string kind, uint hitFlags = 0, int hitClass = 0)
        => NpcElementSpellHit.Deliver(emitter,owner,target,"fire",fire256,0,at,kind,hitFlags,hitClass);
}
