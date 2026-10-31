# Approved Skeletal Mage upgrade plan

Approved Sanctuary design beyond **D2R Reign of the Warlock 3.3.93847**, settled 2026-11-01 JST. This plan is not an implementation certificate. Design was requested now while baseline class skills are being finished.

Use **fire, lightning and ice Mages**. Remove poison from new summons; saved poison Mages convert to ice while preserving ownership and life percentage. Keep existing verified summon/clock/projectile foundations and baseline capture/persistence rules outside the explicit changes.

| Raise Skeletal Mage hard points | Fire | Lightning | Ice |
| --- | --- | --- | --- |
| 1 | Fire Bolt. | Charged Bolt. | Ice Bolt. |
| 5 | Triple basic Fire Bolts. | Triple the normal Charged Bolt volley. | Triple basic Ice Bolts. |
| 10 | 5% level-10 Meteor. | 5% level-10 Chain Lightning. | 5% level-10 Glacial Spike. |
| 20 | Automatic level-20 Enchant on allies at combat start. | 5% level-5 Static Field. | Intrinsic, always-on level-20 Frozen Armor. |

**Scaling:** basic spell level equals effective Raise Skeletal Mage, including items, and uses the corresponding Sorceress spell curve. Keep Skeleton Mastery’s normal Mage benefit. There are no Sorceress masteries or synergies. The secondary spell levels listed above are fixed; item bonuses do not raise them.

**Multishot:** only basic Fire Bolt, Ice Bolt and Charged Bolt gain triples. Charged Bolt produces three normal volleys; no secondary spell gains extra copies.

**Proc timing/resources:** one roll per eligible offensive cast; at most one secondary. Each Lightning secondary has 5% chance. Respect existing spell cooldowns and Mage timing; NPC casts require no mana. No repeated same-tick rerolls or simultaneous double-secondary cast.

**Automatic buffs:** Enchant applies at combat start to eligible owner/party/allied summons within normal targeting range/LOS. There is **no casting action**, sequential buff-casting phase or requested continuous scanning. Reuse existing Enchant duration and stacking/strength rules. Fire Mages have no Fire Mastery. Frozen Armor is an **always-on effect while the 20-hard-point unlock is active**, rather than a timed self-cast that needs refreshing. These choices supersede the earlier active buff-casting proposals.

**Lifecycle:** existing pets gain/lose hard-point unlocks on training/respec. Preserve old actors and their life percentage/ownership when converting poison to ice; no deletion or random reroll. Preserve established baseline birth/stat-capture behavior beyond these explicit changes.

**Corpse shattering is accepted:** the player chooses between a corpse-explosion build and an army build. Reuse existing cold/freeze/death/corpse mechanics, not a blanket shatter flag for every cold hit.

Reuse existing spells/formulas, NPC damage/status services and the shared projectile fan. The earlier Fire Ball/Blizzard/poison-cloud package is superseded. The player has no Mage points and cannot provide current-build Mage observations; do not request a respec/test. See the [full pet design](pet-adjustment-review.md) for other approved pets and remaining Druid questions.

Effects/audio and tree/hotkey icons remain separate in [skill effects](skill-effects.md) and [skill icons](skill-icons.md). Use identified placeholders and existing assets as appropriate. Autonomous abilities add no approved player binding. No spell upgrade, final art or icon is implemented by this plan.
