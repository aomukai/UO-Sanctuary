# Approved Necromancer and Druid pet adjustments

Reference baseline: **D2R Reign of the Warlock 3.3.93847**. Approved Sanctuary deviations take precedence. This is the approved design, updated 2026-11-01 JST; **the upgrades are not implemented yet**. The user requested settling the design while baseline class skills are being finished. The balance aim is viable pure pet builds.

Only Necromancer and Druid pets receive the balance changes below. Warlock, Assassin and Amazon pets retain their intended baseline behavior. Reuse existing affixes, item properties, spells, projectile fans, statuses, AI, damage and persistence services.

## Shared rules

- Ability/affix unlocks use **hard points in the relevant summon skill**. Existing pets gain/lose unlocks on training/respec.
- Use named monster affixes **exactly as currently implemented**. In particular, Extra Fast is movement-only; this explicitly supersedes the earlier movement-and-attack answer. Do not change the global affix or add unrelated elite health/loot bonuses.
- Open Wounds is **100% on successful hits**, using the existing item-property consumer. This common decision also applies to the Druid wolf unlock; preserve Spirit Wolves’ cold-only attacks rather than adding a guessed physical component.
- Cold immunity is ordinary resistance-based immunity and remains breakable under the existing immunity rules.
- Keep baseline abilities and lifecycle rules except where the approved replacement formula or ability below changes them. Preserve existing birth/stat-capture behavior outside explicitly live hard-point unlocks; no new blanket gear-refresh rule is introduced.
- Preserve the working build and saves. Old poison Mages convert to ice, retaining ownership and current life percentage. Do not delete or reroll them.

## Necromancer

| Pet | Damage/baseline | 10 hard points | 20 hard points |
| --- | --- | --- | --- |
| Skeleton Warriors | Straight melee. | **34 flat physical attacker-takes-damage**, fixed after unlock and aggregated through existing item-property rules. | Existing **Extra Fast** affix. |
| Clay Golem | Skeleton Warrior damage curve at the golem’s effective rank, with **Golem Mastery replacing Skeleton Mastery** for damage mastery scaling. | Stone Skin. | Magic Resistant. |
| Blood Golem | **150% of Clay’s damage**. | Open Wounds. | **5% chance to proc level-5 Life Tap**. |
| Iron Golem | Clay’s base damage; original-item bonuses/effects still apply through existing rules. | Extra Strong. | Spectral Hit. |
| Fire Golem | **200% of Clay’s physical base**, plus Holy Fire separately. Holy Fire level equals effective Fire Golem level and uses Paladin damage/radius progression, with no Paladin synergies. | Cannot Be Frozen. | Cold immunity. |

Golem damage ratios describe the approved replacement baseline rather than an additional multiplier on the old unrelated base damage. Compose existing item/enchantment/aura contributions through the shared consumers without adding the old innate Fire Golem skill-fire packet again. Other baseline golem abilities remain unless this formula replaces their damage component.

Revives keep the previously approved no-expiration rule: they last until their death, Necromancer death or replacement, and retain the original monster’s living abilities. Loot stays lootable while combat effects are preserved separately. No additional Revive adjustment was selected in this pass; global monster spell conversion remains separate work.

### Skeletal Mages

Use **three types: fire, lightning and ice**. Remove poison from new summons and convert saved poison Mages to ice under the preservation rule above. Keep each type’s elemental identity.

| Type | Basic attack from rank 1 | 5 hard points | 10 hard points | 20 hard points |
| --- | --- | --- | --- | --- |
| Fire | Fire Bolt, using Sorceress scaling. | Triple basic Fire Bolts. | **5% level-10 Meteor**. | Automatically apply **level-20 Enchant** to allies in range when combat starts. |
| Lightning | Charged Bolt, using Sorceress scaling. | Triple the normal Charged Bolt volley. | **5% level-10 Chain Lightning**. | **5% level-5 Static Field**. |
| Ice | Ice Bolt. | Triple basic Ice Bolts. | **5% level-10 Glacial Spike**. | **Always-on level-20 Frozen Armor**. |

Basic spell level follows effective Raise Skeletal Mage, including items. Keep Skeleton Mastery’s normal Mage benefit. Mages have **no Sorceress masteries or synergies**; in particular Fire Mages do not have Fire Mastery. The specified secondary spell levels are fixed and do not rise with soft summon ranks.

Multishot affects **only** Fire Bolt, Ice Bolt and Charged Bolt. It never triples Meteor, Enchant, Chain Lightning, Static Field, Glacial Spike or Frozen Armor.

Roll once per eligible offensive cast. At most one secondary is chosen; the two Lightning choices each retain 5% chance. Use existing spell cooldowns and Mage timing. NPC casts have no mana requirement. Eligibility and failed/unavailable secondary choices use existing target/cooldown rules rather than permitting repeated same-tick rerolls.

**Buff simplification explicitly approved:** Frozen Armor is an intrinsic always-on level-20 effect while unlocked, with no cast/refresh cycle. Enchant applies automatically at combat start to eligible owner/party/allied summons within normal targeting range/LOS, with **no casting action**. This replaces the earlier scan-and-sequential-cast/continuous-priority proposal. Reuse existing Enchant duration and stacking/strength rules; no new continuous scan is requested.

Corpse shattering is accepted. Necromancers can choose a corpse-explosion build or an army build. Use existing cold/freeze/death/corpse consumers; this does not invent a blanket shatter rule for every cold hit.

## Druid

| Pet | 10 hard points | 20 hard points / balance change |
| --- | --- | --- |
| Spirit Wolves | Open Wounds. | Cold immunity. Preserve cold-only attacks and baseline unchillability. |
| Dire Wolves | Existing Extra Fast affix. | Existing Extra Strong affix. |
| Grizzly | Stun. | **10% Crushing Blow**. |
| Poison Creeper | — | Improve scaling toward **about 2,000 total poison damage over four seconds at rank 20**. This is total damage, not damage per second. |
| Carrion Vine / Solar Creeper | — | No balance changes. |
| Oak Sage / Heart of Wolverine / Spirit of Barbs | — | No balance changes. |

The Grizzly stun is an approved addition beyond the baseline knockback/no-stun observation. Its chance, duration and eligible attacks still need a decision. Poison Creeper’s exact damage curve and rank/synergy reference around the rank-20 target remain open. Ravens were not specified in this pass and remain unreviewed.

## Assets and implementation acceptance

### Safe-area parking — approved lifecycle change, not implemented

Long-lived summoned armies park automatically when their owner enters an **actual safe area** and return when the owner leaves it. Use explicit region/safety classification, never an indoor/roof/building test. A dungeon building is not a safe area merely because it is indoors. Houses qualify only where their area is actually classified as safe.

Parking retains the same roster and summon identities, ownership, equipment/combat snapshots and wounds. It grants **no recovery or reset**: no healing, curing, resource refill, cooldown reset or refreshed buff duration on entry or return. Temporary effects retain their original deadlines and normal expiry behavior while parked. Do not implement this by deleting and freshly summoning the army or freezing its effect timers. Normal owner-death, replacement and expiry rules still apply; parked summons are not protected from those lifecycle rules.

Town healers may heal the owner's parked living roster through their service menu. This is an explicit healer action, not an automatic benefit of entering town. Reuse existing healer access/service rules; do not invent a new price or resurrection entitlement. A service must find the owned parked roster without briefly spawning it in town.

The owner's **named mercenary remains beside them** in safe areas. Identify this companion through the existing mercenary ownership/profile service, not merely a custom name. Town dialogue is a possible later use of the existing conversation hook; no dialogue content is approved or implemented here.

Keep the earlier short-lived trap rules separate: traps remain in the old facet until expiry, stop fighting while the owner is offline, and retain running lifetimes. This parking proposal does not make traps travel with the army.

Implementation leads: `PersistentMinions` already internalizes persistent actors without deleting their native ownership, and `MinionWardrobeProfiles` persists roster/slot/expiry state. Existing class-switch parking and logout suspension alone do **not** establish the new safe-area behavior. `MercenaryCare.HealFollowers` currently requires the pet to be near the healer, so it does not yet reach internalized rosters. Verify region entry/exit/login/teleport/reload, stationary parked-state expiry and no regeneration, owner death, replacement, safe-area summoning, one-time follower slot accounting, failed return placement and healer eligibility before marking this lifecycle change complete. Reuse existing safe-region policy only after checking that its actual classifications match this rule.

Track missing cast/projectile/impact/status/body/shatter/audio requirements in [skill effects](skill-effects.md), reusing existing assets where appropriate and identifying placeholders. Track talent-tree/hotkey icons separately in [skill icons](skill-icons.md). Autonomous pet abilities add no approved player hotkeys.

No upgrade is mechanically complete merely because its design is approved. Verify formulas, shared affix composition, AI/proc timing, targets, damage/events, live unlock changes, saved poison-Mage conversion, original-item preservation and real save/reloads when implementation begins. Keep unsupported details explicit and continue unaffected baseline work.

Discussion preference: the user requested **no asynchronous question popups** because draft text disappeared. Use free-form chat for any remaining design questions. The ten numbered Necromancer defaults were approved in chat, with the explicit buff changes incorporated above; they are no longer open proposals.
