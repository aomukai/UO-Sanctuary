# NPC Hydra parent-removal rule

Status: **approved by user: “Approve independent Hydra lifetime”.** Reference: D2R Reign of the Warlock3.3.93847. This explicit Sanctuary adaptation covers the NPC caster's removal, separately from the approved summon-birth snapshot and offline lifetime rules.

The unchanged older Hydra creation composition keeps ordinary NPC ownership, own inherited stats and alignment, and avoids the player18-head FIFO. Its isolated AI routine checks expiry/target/chance without querying the parent. That does not establish broader current-engine cleanup after the caster dies or is removed; exact current cleanup remains unverified.

Approved rule:

1. Existing heads keep their captured birth stats, assigned spell ranks and original expiry when their NPC caster dies or is removed. No reroll, recovery, duration refresh or replacement caster.
2. The original NPC owner ID stays in saved provenance. A deleted owner can have no remaining native mobile reference; the player does not become the summon owner. The actual existing head remains the damage attacker, with the root player retained for responsible credit/allegiance.
3. Heads continue normal AI and shooting until their original250-frame/10-second expiry. New shots then stop. Already-fired bolts retain their normal projectile lifetime; the existing bounded emitter/death-presentation lease lets them finish before cleanup.
4. Existing root-player lifecycle rules still apply: offline combat stops while lifetimes run, no catch-up damage, and root death/removal follows the existing Hydra controller.

The prepared proposal was tested before approval. The approved behavior is now standard NPC Hydra behavior; the temporary per-head proposal flag was removed. Native tests cover real NPC-owner death/removal, actual-head/root credit and saved owner graphs with a missing caster across reloads, without creating a replacement mobile or player-caster proxy. Automatic proc integration is verified in the [world checkpoint](npc-hydra-world-checkpoint.md).

This approval does not establish current-executable parity, add PvP support or define the nonzero inherited immunity-pierce stat's damage-side interpretation. That consumer remains explicitly gated rather than guessed.
