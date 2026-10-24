// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record SourcePoisonClock(int Rate256, int ElapsedMs);

// Poison is negative HP regeneration. Ordinary Damage would add hit recovery,
// damage-triggered items, spell interruption and a different lethal threshold.
public static class PoisonStates
{
    internal static int BalanceAfter(int before256, long loss256, bool player, bool town)
    {
        if (loss256 <= 0 || !player && town && before256 < 256) { return before256; }
        return (int)Math.Max(player ? 256L : 0L, before256 - loss256);
    }

    internal static int Drain(Mobile source, Mobile target, long loss256)
    {
        if (loss256 <= 0 || target is not { Alive: true, Deleted: false } || target.IsDeadBondedPet || !target.CanBeDamaged()) { return 0; }
        // Sanctuary T29 explicitly protects against poison ticks as well as
        // ordinary hits. Keep that approved deviation after mitigation.
        if (target is PlayerMobile player) { loss256 = Math.Max(0, loss256 - (long)CombatPotions.Bonus(player, "protection") * 256); }
        int before = SkillHealth.Balance256(target), displayed = target.Hits;
        int after = BalanceAfter(before, loss256, target is PlayerMobile, SpellHelper.IsTown(target.Location, target));
        if (after == before) { return 0; }
        SkillHealth.SetBalance256(target, after);
        // Keep native corpse/loot damage attribution without dispatching a hit.
        // Register after the health setter, which clears entries at displayed full HP.
        int whole = displayed - target.Hits;
        if (whole > 0 && source != null) { target.RegisterDamage(whole, source); }
        if (after == 0) { CombatDamage.Finish(target, source); }
        return Math.Max(0, before - after);
    }

    internal static void Advance(Mobile target, TimedActor actor, int elapsed)
    {
        // Legacy rounding allowed a tiny negative HP remainder. It represents
        // numerical zero, rather than a credit against the next integer frame.
        actor.PoisonFraction = Math.Max(0, actor.PoisonFraction);
        if (actor.PoisonClock is { } clock)
        {
            int total = clock.ElapsedMs + elapsed, frames = total / 40;
            actor.PoisonClock = clock with { ElapsedMs = total % 40 };
            actor.PoisonFraction += frames * (long)clock.Rate256 / 256.0;
        }
        else { actor.PoisonFraction += elapsed * actor.PoisonRate; }
        // The saved remainder remains measured in HP. Old whole-HP remainders
        // enter the shared FP256 balance exactly once at the next settlement.
        long raw = (long)Math.Floor(actor.PoisonFraction * 256 + 1e-9);
        actor.PoisonFraction -= raw / 256.0;
        if (raw > 0) { Drain(World.FindMobile(actor.PoisonSource), target, raw); }
        if (actor.PoisonMs == 0) { actor.PoisonClock = null; }
    }
}
