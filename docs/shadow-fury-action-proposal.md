# Shadow Blade Fury action proposal

Status: **approved by user: “Approve the single-sequence Shadow Fury rule”**. Reference: D2R Reign of the Warlock **3.3.93847**. This decision concerns original Shadow Warrior/Master NPC actions, not the player's approved held-input skill.

Both actual original Shadows retain their monster slot2 mode14,17-point sequence. The tested current-row/older-clock composition gives one release at frame10 and an unrewound end at frame18, at25frames/second. The separately composed old rewind callback gives different recovery at different game epochs. Current executable behavior remains unavailable, so neither the epoch-dependent rewind nor a guessed modern correction is established.

Approved Sanctuary action:

1. Each AI selection runs one complete original sequence: one blade at frame10 (0.40seconds), recovery ends at frame18 (0.72seconds). Use the own original clock, not the player input clock. Do not rewind or create an indefinite NPC channel.
2. Reuse the existing startup26/do48 state component inside that action, with the already approved zero NPC mana threshold and no pet/player payment. Its timed state belongs to this action and is discarded when the action ends or is interrupted; a later AI choice starts a fresh sequence without carrying a lease or an immediate re-entry shot.
3. Use the enemy's current location at release while it remains valid on the same facet; retain the original aim when no live target was supplied. Invalid/dead/removed target or invalid release geometry produces no new blade. Ordinary interruption, movement, control loss, owner inactivity and reload cancel the pending action; committed blades retain the existing qualified projectile lifecycle.
4. Capture damage and own original equipment/rank at release. Keep the actual Shadow as attacker and the player as responsible credit. Unsigned critical selection was separately approved: [existing unsigned proposal](shadow-missile-critical-proposal.md). Actor critical RNG, world allocation and private projectile damage RNG stay separate.
5. Resume the existing AI after recovery. Do not add a new NPC cooldown or force repeated selections. All ordinary target, LOS, town, range and skill eligibility checks remain.

The clock, free-resource component, own equipment packet, travelling projectile/damage host and persistence are already separately tested. The proposed full action requires integration verification after the design decision. Approval defines these unresolved NPC timing/re-entry boundaries; it does not claim exact current-executable parity or broaden unsupported recipient/property contexts.
