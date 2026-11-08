# Shadow weapon-missile critical proposal

Status: **proposed; not approved or connected to automatic AI.** Pinned reference: D2R Reign of the Warlock3.3.93847. This decision is separate from the approved free NPC Fury resource rule.

The older bridge's complete `MISSILE_HasBonusStats` casts each32-bit random value to signed `int` before `%100`. With a sufficiently high-bit roll, the remainder is negative and can pass a zero-percent critical threshold. The current executable's equivalent routine is not established; treating this bridge behavior as modern D2R fact would be unwarranted.

Sixteen bounded comparisons compile that complete unchanged routine beside a separately named variant changing only those signed casts. The source evidence identifies the changed variant as a proposal, not unchanged source: [evidence](../tools/reference/shadow-fury-action-source-evidence.json). The discrepancy is observable in these controls; it is not a claim of an in-game observation.

Proposed Sanctuary rule for these original NPC weapon missiles:

1. Read the actual NPC's passive critical, deadly strike and selected-weapon mastery critical chance. Never inherit the controlling player's chances.
2. Test them in that order using ordinary **unsigned**32-bit source rolls modulo100. The passive test always draws, including at zero chance. Deadly strike and mastery draw only for a nonzero chance after preceding tests fail. Any successful test stops further draws.
3. Double physical damage once through the existing saved critical flag. Typed damage and poison are not doubled. Do not add a second intrinsic monster critical roll for Shadows, whose current monster rows have Crit0.
4. Keep actor critical RNG separate from the world allocation stream and each projectile's private damage stream. Continue using the existing persisted source-algorithm allocation services; this does not claim the original D2 game seed.

The concrete arithmetic is implemented as `NpcMissileCritical.ProposedUnsigned` and compared with the explicitly modified complete routine. Prepared Fury projectiles already support supplied creation-critical and child-seed inputs. The actual initialized mode14/17-point cast remains separate. Automatic creation/AI remains gated pending a decision, so unapproved policy cannot leak into gameplay.

Approval would resolve only this critical selection discrepancy. Actual creation ordering, selected original weapon collection, world/private seed allocation and AI still require their normal integration checks.
