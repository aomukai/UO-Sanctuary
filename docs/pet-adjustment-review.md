# Necromancer and Druid pet adjustment review

Reference baseline: **D2R Reign of the Warlock 3.3.93847**. This document records Sanctuary design decisions separately from baseline mechanical verification. User requested the design discussion now, while remaining class skills are being implemented. No new adjustment is implemented by this document.

Approved scope: review **all Necromancer and Druid pets**. Warlock, Assassin and Amazon pets are satisfactory and remain outside this adjustment pass. Reviewing a pet does not imply it needs a change. The user’s balance aim is **viable pure pet builds**.

Selected pets may gain extra abilities unlocked by **hard points** invested in their summon skill. Exact pets, unlock thresholds, ability ranks/scaling, cooldowns/resources and AI priorities remain undecided unless recorded below.

| Necromancer pet | Review state |
| --- | --- |
| Skeleton Warriors (Raise Skeleton) | Straight melee. At 10 hard points gain the existing item-style flat physical ATD property, with **34 as a provisional starting amount**. At 20 gain the existing monster Extra Fast affix. Earlier movement+attack intent versus the currently movement-only implementation needs reconciliation. |
| Skeletal Mages | Three final types: fire, lightning and ice; poison is removed. Confirmed rank 1/5/10/20 spell progression and chances are specified below and in the [Mage plan](skeletal-mage-upgrade-plan.md). |
| Clay Golem | Same damage and damage scaling as one summoned Skeleton Warrior. At 10 hard points gain Stone Skin; at 20 gain Magic Resistant. |
| Blood Golem | 150% of Clay Golem damage. At 10 hard points gain Open Wounds; at 20 gain a 5% chance to proc level-5 Life Tap. Open Wounds percentage remains open. |
| Iron Golem | Same damage as Clay Golem. At 10 hard points gain Extra Strong; at 20 gain Spectral Hit. Preserve original-item identity/effects and item safety rules; exact composition with the new baseline remains to be specified. |
| Fire Golem | 200% of Clay Golem damage (twice Clay, not +200%). Holy Fire uses Paladin scaling. At 10 hard points gain Cannot Be Frozen; at 20 gain cold immunity. Exact aura-rank mapping and base/innate-fire composition remain open. |
| Revived monsters | Pending additional review. Existing approved rules remain: no expiration timer; last until death, Necromancer death or replacement; retain original living abilities; corpse loot stays lootable while combat effects are preserved separately. Global monster spell conversion remains separate work. |

| Druid pet | Review state |
| --- | --- |
| Ravens | Pending role/ability review. |
| Spirit Wolves | At 10 hard points gain Open Wounds; at 20 gain cold immunity. Preserve baseline cold attacks/unchillability. Open Wounds chance and immunity representation remain open. |
| Dire Wolves | At 10 hard points gain Extra Fast; at 20 gain Extra Strong. Reuse the shared affix decisions made for Necromancer pets. |
| Grizzly | At 10 hard points gain Stun; at 20 gain **10% Crushing Blow**. Stun chance/duration/delivery remain open. This is an approved addition beyond the baseline knockback/no-stun observation. |
| Poison Creeper | Improve damage scaling, targeting **about 2,000 total poison damage over four seconds at rank 20**, not 2,000 per second. Exact curve, range and synergy context remain open. |
| Carrion Vine | User says it is fine; retain its baseline behavior. |
| Solar Creeper | User says it is fine; retain its baseline behavior. |
| Oak Sage | User says spirits are fine; retain baseline behavior. |
| Heart of Wolverine | User says spirits are fine; retain baseline behavior. |
| Spirit of Barbs | User says spirits are fine; retain baseline behavior. |

Implementation should reuse existing player/NPC spells, projectiles, statuses, movement, combat and summon persistence services. Proposed automatic casting, soft-rank spell scaling, skill selection, targeting and control rules have not yet been approved. Record confirmed choices here; leave unanswered questions nonblocking for unaffected baseline work.

Track every extra cast/projectile/impact/status/body/audio requirement in [skill effects](skill-effects.md), and tree/hotkey icon requirements separately in [skill icons](skill-icons.md). Autonomous pet abilities do not imply a new player binding. Final assets must not block mechanics; label placeholders clearly. Preserve installed builds and saved actors; migration of any future enhancement needs its own verified rule.

## Confirmed Skeletal Mage progression — 2026-11-01 JST

All breakpoints below refer to hard points in Raise Skeletal Mage, following the established unlock rule. Basic spell rank/scaling beyond the entry rank and the role of soft ranks still need an explicit mapping. This is a **three-type roster**, consolidating ice/cold and removing poison; existing saved poison Mage migration/type selection must be designed rather than silently rerolled.

| Type | Rank 1 | Rank 5 | Rank 10 | Rank 20 |
| --- | --- | --- | --- | --- |
| Fire | Fire Bolt, scaling like Sorceress Fire Bolt. | Multishot for its basic bolt only. | 5% chance to cast level-10 Meteor. | Cast level-20 Enchant on allies. |
| Lightning | Charged Bolt, scaling like Sorceress Charged Bolt. | Multishot for its basic bolts only. | 5% chance to cast level-10 Chain Lightning. | 5% chance to cast level-5 Static Field. |
| Ice | Ice Bolt. | Multishot for its basic bolt only. | 5% chance to cast level-10 Glacial Spike. | Gain level-20 Frozen Armor. |

Fire Mages have **no Fire Mastery**. At combat start they scan allied units in range, prioritize units lacking Enchant, and continue enchanting until all allies in range are enchanted. Preserve this requested priority instead of replacing it with a random Enchant proc. Refresh/expiry behavior during an ongoing fight, ally range, overlapping supplier strength and cadence remain unspecified.

The user explicitly accepts corpse shattering: Necromancers can choose a corpse-explosion build or an army build. Reuse existing cold/freeze/death/corpse consumers; do not manufacture a blanket shatter rule for every cold hit.

The earlier suggested Fire Ball/Blizzard/poison-cloud package is **superseded** by this explicit user design. Only the three basic bolt attacks gain multishot; Meteor, Enchant, Chain Lightning, Static Field, Glacial Spike and Frozen Armor do not gain extra copies from that unlock.

## Open details to resolve without blocking baseline work

- Reconcile Extra Fast: the user previously selected movement **and** attack speed, and now requests the existing monster affix. The current encounter implementation adds movement speed only. Do not silently choose between those instructions or change the shared affix without resolving the intended behavior.
- Treat ATD34 as provisional (the user compared it to rank-10 Spirit of Barbs), not as an independently verified current reference value or final balance number. Reuse the existing item property/event semantics. Growth beyond the unlock remains open.
- Basic spell levels versus summon hard/effective rank; inherited mastery/synergy/gear context; multishot count/spread and how it applies to already multi-projectile Charged Bolt.
- Proc roll opportunity/cadence and competing 5% rolls, spell priority, resources/cooldowns, ranges, and timing. Record each stated 5% as exact requested chance, without silently inventing “per second” or independent simultaneous casts.
- Frozen Armor activation/refresh behavior; Enchant combat maintenance and eligible allies/range; saved actor upgrades and unlock changes after training/respec.
- Which Skeleton damage formula/rank/mastery context feeds Clay; distinguish damage endpoints from DPS/attack-rate changes. Blood uses1.5×, Iron1× and Fire2× that approved Clay baseline; rounding, innate elemental additions, weapon/item enhancement and Holy Fire rank mapping remain explicit design work.
- Affix grade/difficulty context for Stone Skin, Magic Resistant, Extra Strong and Spectral Hit; Open Wounds chance; cold-immunity resistance/curse interaction. Prefer existing shared consumers and avoid pet-specific duplicate formulas.

These are approved Sanctuary deviations beyond D2R3.3.93847. **No pet upgrade is implemented by this document.** Preserve baseline abilities unless a specified adjustment replaces them, and verify composition explicitly when implementation begins. Pure pet build viability remains the balance aim; exact benchmark criteria remain open.

Discussion preference: the user asked to stop the question popups because their draft text was disappearing. **Do not send further asynchronous question forms.** Leave space for free-form chat answers and keep unapproved proposals open.

Druid update, 2026-11-01 JST: the user explicitly leaves the spirits and Carrion/Solar vines unchanged. Ravens were not mentioned in this update and remain under review. Poison Creeper’s requested target is a damage-over-time total; do not reinterpret it as per-second damage or silently choose an effective/hard-rank or pre/post-synergy reference. Wolf cold-only damage must remain compatible with the chosen Open Wounds consumer rather than adding a guessed physical component.

## Necromancer defaults proposed in chat — awaiting user decisions

The user requested resolving the remaining details. The following numbered defaults were presented as **proposals**, not approvals. Do not implement them merely because they appear here.

1. Extra Fast: use the existing movement-only affix exactly; adding attack speed would require changing the shared affix. Reconcile with the earlier movement-and-attack answer.
2. Skeleton ATD: start at34flat physical at10hard points, with no further rank scaling; aggregate through existing item-property rules.
3. Mage scaling: basic spell level follows effective Raise Skeletal Mage; retain Skeleton Mastery benefit but no Sorceress masteries/synergies. Fixed secondary levels stay as explicitly specified.
4. Multishot: three shots per basic attack; triple Charged Bolt’s normal volley; never multiply secondary spells.
5. Procs/resources: one roll per eligible offensive cast, each Lightning secondary at5%with at most one chosen; retain spell cooldowns and Mage timing; no NPC mana requirement.
6. Buffs: Enchant owner/party/allied summons in normal targeting range/LOS, prioritizing missing buffs during combat; self-cast Frozen Armor on summon and refresh after expiry.
7. Golem baseline: Skeleton damage curve at effective golem rank, substituting Golem Mastery for Skeleton Mastery; Iron item bonuses still apply; Fire gets2×physical Clay base plus Holy Fire. Existing innate-fire composition still needs an explicit resolution if this choice is accepted.
8. Holy Fire level: equal effective Fire Golem level, with Paladin damage/radius progression and no invented Paladin synergies.
9. Open Wounds/affixes:100%Open Wounds on successful hits; full named monster-affix effects without unrelated elite health/loot upgrades; cold immunity remains ordinary breakable resistance-based immunity.
10. Existing actors: gain/lose unlocks on training/respec; convert saved poison Mages to ice preserving life percentage and ownership.
