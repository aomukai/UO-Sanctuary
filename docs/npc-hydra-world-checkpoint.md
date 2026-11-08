# NPC-owned Hydra world and item-proc checkpoint

2026-11-08 JST. Reference: D2R Reign of the Warlock **3.3.93847**, approved Sanctuary deviations first. **208/240 handlers; no class certified complete.** Learned Shadow Warrior/Master remain gated by their remaining dependencies.

Actual NPC-owned Hydra heads now use the existing Hydra actor, AI/animation controller, cached aim, moving bolts and typed NPC damage services. Their real summon owner is the original NPC. The root player is responsible-credit/allegiance provenance; the player does not become a substitute caster or summon owner. The original NPC ID/binding and the head's captured birth survive persistence.

Ordinary NPC heads stay outside the player's18-head FIFO, as established by the existing unchanged Hydra source composition. Native tests retain21 NPC heads without eviction, independent allocations, world/private head and bolt seeds, source AI/action timing, travelling HP damage, actual-head kill attribution/root credit and final cleanup. Creation/controller/bolts do not consume the parent's attack stream; the normal equipment-proc chance still uses that stream.

The user approved **“Approve independent Hydra lifetime.”** Existing heads keep their birth stats and original250-frame/10-second expiry after the original caster dies or is removed. Already-fired bolts retain their normal lifetime and bounded emitter lease. No replacement mobile, reroll or expiry refresh is introduced. After reload a removed NPC can have no native mobile reference, while its saved owner ID remains explicit. The tested proposal became the standard rule; its temporary per-head proposal flag was removed. [Approved rule](npc-hydra-parent-removal-proposal.md).

Automatic original-item Hydra now has its current metadata, required start14 room/town check and common do144 factory consumer. The real Master allocation9 equipment registry callback creates actual heads, preserves combat targeting and costs no owner mana. Failed automatic starts return0 without allocation. Original equipment is preserved; it is not stripped to bypass the prior registration gap. Nonzero inherited immunity-pierce is explicitly refused before original-world admission or head allocation.

Validation: **C4/I6/snapshot9/native4**, zero warnings/errors; **20 native success markers, three boots, two actual reloads, three saves**. Real parent death/removal and saved missing-caster ownership pass. Existing prepared NPC birth and player-Hydra admission/payment, AI/bolts/damage, other-facet return, retirement and natural reload-resume checks pass. Source creation/AI/clock/damage evidence is reused; **no compiler matrix rerun**. [Reproducible receipt](../tools/reference/npc-hydra-world-native-evidence.json). Installed artifacts, saves and configuration are preserved.

Prototype testing exposed a fixture attempting to delete an unadmitted numeric caster from a world map; its prepared cleanup was corrected. Another fixture needed the existing400ms retirement observation before asserting actor deletion. The final automatic path activates the genuine item/defense profile and tests real world death. Earlier logs/input manifests remain available.

Remaining limits:

- `item_pierce_fire_immunity` is captured, but its nonzero damage-side interpretation remains unverified. Current Flame Rift variants store300; the table alone does not establish the consumer. The explicit gate remains, including the existing player-Hydra gate.
- Player/PvP recipients, complete modern room/target acquisition and arbitrary nested callback mutations remain outside this proof.
- Fury callback/rewind and the pending weapon-missile critical decision, remaining Master range/AI contexts and learned Shadow admission remain unfinished.
- Final heads, emergence/attack/death, bolt travel/impact/expiry and sound remain shared identified placeholders. No extra public talent/hotkey icon is introduced.
