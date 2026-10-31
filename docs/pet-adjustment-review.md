# Necromancer and Druid pet adjustment review

Reference baseline: **D2R Reign of the Warlock 3.3.93847**. This document records Sanctuary design decisions separately from baseline mechanical verification. User requested the design discussion now, while remaining class skills are being implemented. No new adjustment is implemented by this document.

Approved scope: review **all Necromancer and Druid pets**. Warlock, Assassin and Amazon pets are satisfactory and remain outside this adjustment pass. Reviewing a pet does not imply it needs a change. The user’s balance aim is **viable pure pet builds**.

Selected pets may gain extra abilities unlocked by **hard points** invested in their summon skill. Exact pets, unlock thresholds, ability ranks/scaling, cooldowns/resources and AI priorities remain undecided unless recorded below.

| Necromancer pet | Review state |
| --- | --- |
| Skeleton Warriors (Raise Skeleton) | Approved: remain straightforward melee fighters. At 10 hard points gain “attacker takes damage”; at 20 hard points gain “Extra Fast.” Confirmed: **flat physical return like the item property**, and Extra Fast increases **both movement and attack speed**. Exact return amount/scaling and speed magnitudes remain open. Reuse existing return and speed consumers. No enhancement implemented yet. |
| Skeletal Mages | Approved: keep each elemental identity and add matching spells; hard points in Raise Skeletal Mage unlock them. Exact spells and thresholds remain open; see [Mage plan](skeletal-mage-upgrade-plan.md). |
| Clay Golem | Pending role/ability review. |
| Blood Golem | Pending role/ability review. |
| Iron Golem | Pending role/ability review; preserve item-derived identity and existing original-item safety rules. |
| Fire Golem | Pending role/ability review. |
| Revived monsters | Pending additional review. Existing approved rules remain: no expiration timer; last until death, Necromancer death or replacement; retain original living abilities; corpse loot stays lootable while combat effects are preserved separately. Global monster spell conversion remains separate work. |

| Druid pet | Review state |
| --- | --- |
| Ravens | Pending role/ability review. |
| Spirit Wolves | Pending role/ability review. |
| Dire Wolves | Pending role/ability review. |
| Grizzly | Pending role/ability review. |
| Poison Creeper | Pending role/ability review. |
| Carrion Vine | Pending role/ability review. |
| Solar Creeper | Pending role/ability review. |
| Oak Sage | Pending role/ability review. |
| Heart of Wolverine | Pending role/ability review. |
| Spirit of Barbs | Pending role/ability review. |

Implementation should reuse existing player/NPC spells, projectiles, statuses, movement, combat and summon persistence services. Proposed automatic casting, soft-rank spell scaling, skill selection, targeting and control rules have not yet been approved. Record confirmed choices here; leave unanswered questions nonblocking for unaffected baseline work.

Track every extra cast/projectile/impact/status/body/audio requirement in [skill effects](skill-effects.md), and tree/hotkey icon requirements separately in [skill icons](skill-icons.md). Autonomous pet abilities do not imply a new player binding. Final assets must not block mechanics; label placeholders clearly. Preserve installed builds and saved actors; migration of any future enhancement needs its own verified rule.

Mage proposal awaiting feedback: two extra spells unlocked at 10/20 hard points (alternative 5/15); Fire Ball→Meteor, Glacial Spike→Blizzard, Lightning→Chain Lightning, and splashing poison bolt→lingering poison cloud. Poison delivery variants would reuse existing poison mechanics without consuming corpses. These exact spells, timing and variants are proposals only.

Discussion preference: the user asked to stop the question popups because their draft text was disappearing. **Do not send further asynchronous question forms.** Leave space for free-form chat answers and keep unapproved proposals open.
