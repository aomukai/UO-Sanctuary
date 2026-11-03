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
    {
        if (fire256 < 0 || hitFlags is not (0 or 2) || hitClass is not (0 or 16)
            || at.Kind != DateTimeKind.Utc || emitter.Map != target.Map || !target.CanBeDamaged())
        { throw new NotSupportedException("Unqualified NPC fire spell recipient"); }
        PreparedMonsterNativeDamage.RequireIncoming(owner, target, emitter);
        if (MonsterUninterruptibles.Active(target) || MonsterUninterruptibles.Delayed(target))
        { throw new NotSupportedException(kind + " uninterruptible/death-delay recipient needs its actor adapter"); }
        var metadata = NativeMonsterReturn.Metadata(target);
        if (!metadata.Killable || metadata.Base == 78) { throw new NotSupportedException(kind + " recipient killability/sand-leaper completion is unsupported"); }
        PreparedMonsterDamagePacket Mitigate(PreparedMonsterDamagePacket packet)
        {
            var parts = SkillCombat.MitigatePrepared(emitter, target, new(0, packet.Fire256 / 256.0, 0, 0, 0), out int absorb);
            return packet with { Total256 = NamedElements.To256(parts.Total), Fire256 = NamedElements.To256(parts.Fire), AbsorbedLife256 = absorb };
        }
        NativeMonsterReturn.Session(target);
        var services = new InstantSpellNpcDamage(owner, target, metadata, null, at, kind, () => TelekinesisCombat.DefenderRoll(), emitter: emitter, npcMitigation: Mitigate);
        return PreparedMonsterDamage.ReleaseMissile(emitter, target, new(0, fire256, 0, 0, 5, HitClass: hitClass, Fire256: fire256, HitFlags: hitFlags),
            new(NamedCombat.Elite(target), () => false, () => metadata.Velocity, () => false),
            _ => throw new InvalidOperationException(kind + " has no stun"), services, at);
    }
}
