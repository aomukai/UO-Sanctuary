# Skeletal Mage upgrade plan

Approved Sanctuary design beyond **D2R Reign of the Warlock 3.3.93847**, updated2026-11-01JST. The user requested designing pet adjustments now while baseline class skills are being implemented. Implementation was previously scheduled after baseline skills; the design below does not claim a completed upgrade.

Use **three Mage types: fire, lightning and ice**, with poison removed and the existing cold type consolidated into ice. Each keeps its element. Unlock extra abilities from **hard points in Raise Skeletal Mage**. The player has no points in Mages and cannot supply current-build observations; continue from existing references without asking for Mage tests.

| Summon hard points | Fire Mage | Lightning Mage | Ice Mage |
| --- | --- | --- | --- |
| 1 | Fire Bolt, scaling like Sorceress Fire Bolt. | Charged Bolt, scaling like Sorceress Charged Bolt. | Ice Bolt. |
| 5 | Multishot for basic Fire Bolts only. | Multishot for basic Charged Bolts only. | Multishot for basic Ice Bolts only. |
| 10 | 5% chance to cast level-10 Meteor. | 5% chance to cast level-10 Chain Lightning. | 5% chance to cast level-10 Glacial Spike. |
| 20 | Cast level-20 Enchant on allies. | 5% chance to cast level-5 Static Field. | Gain level-20 Frozen Armor. |

**Fire Mage combat opening:** scan nearby allied units when combat starts. Enchant units that lack the buff, continuing until all allies in range are enchanted, then perform offensive casting. Fire Mages have **no Fire Mastery**. Do not replace this priority with a guessed random Enchant proc. Ally range, ongoing maintenance/expiry, strength arbitration, resources and cast cadence remain open.

**Corpse shattering is approved.** The user accepts choosing between corpse-explosion and army builds. Reuse existing cold/freeze/death/corpse consumers according to the chosen spells; do not invent a blanket cold-damage shatter rule.

The stated secondary spell levels are fixed requested levels (Meteor10, Enchant20, Chain Lightning10, Static Field5, Glacial Spike10, Frozen Armor20), not assumed to rise with soft summon ranks. Basic spell level mapping, soft-rank scaling and any later changes to secondary levels remain undecided. “5%” is the requested chance; its roll opportunity, time cadence and arbitration are still open. Multishot applies only to basic bolts; count/spread and interaction with Charged Bolt’s existing projectile count need design.

Keep the existing verified bolt/clock/summon machinery as the baseline foundation. Reuse existing Sorceress spell/formula, shared projectile fan, status, AI, health and summon-persistence services rather than duplicating them. Old poison-Mage saves require an explicit migration choice; no silent conversion, random reroll or deletion. Unlock changes after training/respec and existing-actor updates also need a defined rule.

The earlier proposed Fire Ball/Blizzard/poison-cloud package is superseded. The [full pet review](pet-adjustment-review.md) records the confirmed Skeleton Warrior/golem changes and remaining design details. Warlock, Assassin and Amazon pets remain outside this adjustment pass.

Final assets stay independent of mechanical implementation. Track missing cast/projectile/impact/status/body/shatter/audio requirements in [skill effects](skill-effects.md), using identified placeholders. Track talent-tree/hotkey icons separately in [skill icons](skill-icons.md); autonomous pet abilities do not automatically create player hotkeys. No new spell, final asset or icon is implemented by this plan.
