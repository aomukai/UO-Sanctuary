# Approved Necromancer and Druid pet adjustments

**Shared offense foundation (2026-11-06):** the new full NPC allocation path reuses the qualified original item/hand graph and existing damage/poison/critical helpers. This does not alter approved pet counts, snapshots or upgrades, or establish learned summon/controller and recipient-event completion. See the [checkpoint](npc-damage-fill-checkpoint.md).

Reference baseline: **D2R Reign of the Warlock 3.3.93847**. Approved Sanctuary deviations take precedence. This is the approved design, updated 2026-11-01 JST; **the upgrade package is partially implemented** (see [Mage basics/Mastery/triples](mage-basics-checkpoint.md), [Fire damage/aura/cold defenses](fire-upgrades-checkpoint.md), [Iron Spectral Hit](iron-golem-spectral-checkpoint.md), [Iron Extra Strong](iron-golem-strong-checkpoint.md), [Iron damage foundation](iron-golem-damage-checkpoint.md), [Blood hard-20 Life Tap](blood-life-tap-checkpoint.md), [Blood hard-10 Open Wounds](npc-open-wounds-checkpoint.md), [Blood damage foundation](blood-golem-damage-checkpoint.md), [Clay damage/defensive upgrades](clay-upgrades-checkpoint.md), [Warrior hard10/20 unlocks](skeleton-upgrades-checkpoint.md), [Mage hard20 buffs](mage-pet-buffs-checkpoint.md), [Static](mage-static-checkpoint.md), [Chain](mage-chain-checkpoint.md), [Glacial](mage-glacial-checkpoint.md), [Meteor](mage-meteor-checkpoint.md), [element migration](mage-elements-checkpoint.md) and [historical AI](mage-historical-checkpoint.md)); all other entries remain pending unless their own checkpoint says otherwise. The user requested settling the design while baseline class skills are being finished. The balance aim is viable pure pet builds.

Only Necromancer and Druid pets receive the balance changes below. Warlock, Assassin and Amazon pets retain their intended baseline behavior. Reuse existing affixes, item properties, spells, projectile fans, statuses, AI, damage and persistence services.

## Warlock summon presentation

Approved on 2026-11-03: Goatman uses UO **Gargoyle** (body4), Defiler uses **Gazer** (body22; the user's Beholder), and Tainted uses **Succubus** (body149). These choices replace model placeholders while retaining each D2R summon’s species, stats, footprint, abilities and timing. Use the native models/basic sounds, not the corresponding UO monster classes or Magery/AI. The Goatman mapping is wired into its prepared body and old-placeholder load migration; Defiler/Tainted are registered choices awaiting their mechanical bodies. Final custom skill effects and talent/hotkey icons remain separate. See [the appearance reference](../tools/reference/warlock-demon-appearance-reference.json).

## Shared rules

- Ability/affix unlocks use **hard points in the relevant summon skill**, captured when summoned under the later [cross-class snapshot decision](summon-stat-snapshots.md). Current summons retain that birth context through later training/gear changes; resummoning captures the new values. Historical profiles explicitly documented as live keep that compatibility behavior until resummoned. This supersedes the original live-training/respec upgrade plan.
- Use named monster affixes **exactly as currently implemented**. In particular, Extra Fast is movement-only; this explicitly supersedes the earlier movement-and-attack answer. Do not change the global affix or add unrelated elite health/loot bonuses.
- Open Wounds is **100% on successful hits**, using the existing item-property consumer. This common decision also applies to the Druid wolf unlock; preserve Spirit Wolves’ cold-only attacks rather than adding a guessed physical component.
- Cold immunity is ordinary resistance-based immunity and remains breakable under the existing immunity rules.
- Keep baseline abilities and lifecycle rules except where the approved replacement formula or ability below changes them. Use the later shared summon-time snapshot rule for inherited bases/ranks and upgrades; external buffs/auras and approved difficulty rescaling remain separate. This does not grant all owner gear effects to every pet.
- Preserve the working build and saves. Old poison Mages convert to ice, retaining ownership and current life percentage. Do not delete or reroll them.

## Necromancer army-size revision — approved, 2026-11-06

The user requests another Necromancer balance pass: stronger Mages and permanent Revives should trade some army size for their increased utility/persistence. Approved endpoints are **five Mages / three Revives at effective rank20**, rising to **seven Mages / five Revives at effective rank40**, with those counts as absolute ceilings. Item skill bonuses contribute to effective rank; this proposal does not establish a global effective-skill-rank40 cap. **Druid upgrades stay unchanged**, because their original pets need the additional strength without an offsetting nerf.

The user approved the intermediate progression below with “yes, this looks like a good starting point”. The [implementation checkpoint](necromancer-army-checkpoint.md) records actual learned cast/FIFO, gear/parking/wardrobe and two-reload acceptance. It preserves the first three Mage ranks, then adds one slot every ten effective ranks. Revives begin at one and add a slot every ten ranks. An unavailable/unlearned summon still uses its existing availability rules.

| Effective skill rank | Mage maximum | Revive maximum |
| --- | ---: | ---: |
| 1 | 1 | 1 |
| 2 | 2 | 1 |
| 3–9 | 3 | 1 |
| 10–19 | 4 | 2 |
| 20–29 | 5 | 3 |
| 30–39 | 6 | 4 |
| 40+ | 7 | 5 |

Current pinned petmax is `(lvl < 4) ? lvl : (2 + lvl / 3)` for Warriors and Mages, and `lvl` for Revive. With Warriors and the one-golem limit unchanged, the approved counts make17pets at rank20 and28at rank40. No Warrior/Golem quantity change has been selected. Existing successful-cast FIFO replacement is reusable; Mage live-cap enforcement already exists through `NecromancerSkeletons.Enforce`, while prepared Revive currently retires entries above its captured rank on successful activation. Inspect and verify cap-change/gear/respec/parking/save behavior when implementing the selected rule; do not delete historical pets merely by documenting this proposal. Use this approved progression in runtime limits and talent/pet-count displays, keeping the raw D2R reference tables unchanged. No new effect/audio or talent/hotkey icon identity is required.

## Necromancer

| Pet | Damage/baseline | 10 hard points | 20 hard points |
| --- | --- | --- | --- |
| Skeleton Warriors | Straight melee. | **34 flat physical attacker-takes-damage**, fixed after unlock and aggregated through existing item-property rules. | Existing **Extra Fast** affix. |
| Clay Golem | Skeleton Warrior damage curve at the golem’s effective rank, with **Golem Mastery replacing Skeleton Mastery** for damage mastery scaling. | Stone Skin. | Magic Resistant. |
| Blood Golem | **150% of Clay’s damage**. | Open Wounds. | **5% chance to proc level-5 Life Tap**. |
| Iron Golem | Clay’s base damage; original-item bonuses/effects still apply through existing rules. | Extra Strong. | Spectral Hit. |
| Fire Golem | **200% of Clay’s physical base**, plus Holy Fire separately. Holy Fire level equals effective Fire Golem level and uses Paladin damage/radius progression, with no Paladin synergies. | Cannot Be Frozen. | Cold immunity. |

Skeleton Warrior unlocks are implemented within the [shared-consumer checkpoint](skeleton-upgrades-checkpoint.md): live hard10 adds34flat physical return; hard20 adds the existing100%movement-only Extra Fast. All three supported original warrior actor types keep their identity/wounds across reloads; no attack-speed increase is added. Shared full source/event/client limits remain explicit. Clay’s replacement damage and defensive affixes are implemented within the [Clay checkpoint](clay-upgrades-checkpoint.md). Blood’s replacement150%damage is implemented within the [Blood damage checkpoint](blood-golem-damage-checkpoint.md); its [hard-10 Open Wounds](npc-open-wounds-checkpoint.md) now uses the shared registered NPC callback and exact signed regeneration clock. Live hard points govern the unlock; original item chance shares one subscription/draw. The [hard-20 5% Life Tap 5 proc](blood-life-tap-checkpoint.md) now shares the registered source event and NPC area-curse receiver; first-swing healing uses the existing defender event. These upgrades apply to the reviewed learned source-world path. Generic/player legacy bleed transport, native fallback and broader engine/save gaps remain explicit in the checkpoints. Iron’s [captured Clay damage baseline](iron-golem-damage-checkpoint.md) is implemented with existing original-item composition; its [live hard-10 Extra Strong](iron-golem-strong-checkpoint.md) now reuses the direct encounter coefficients and shared NPC ED/AR pools. Its [hard-20 Spectral Hit](iron-golem-spectral-checkpoint.md) now shares the existing five-channel producer, resistance rules and survivor-only post-direct cold/aggregate poison, with remaining acceptance limits recorded there. Fire’s [approved damage/aura/cold defenses](fire-upgrades-checkpoint.md) now share the captured200%curve,own-rank Paladin Holy Fire,item CBF and ordinary breakable cold immunity;startup/historical/full-acceptance limits remain explicit.

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

Spirit Wolf hard10Open Wounds and hard20breakable cold immunity are implemented with dedicated27-marker/3boot/2reload acceptance; see [the checkpoint](spirit-wolf-upgrades-checkpoint.md). Intrinsic cold-only attacks, baseline unchillability and captured birth stats remain. Dire Wolf hard10movement-only Extra Fast and hard20direct Extra Strong are implemented within the [21-marker/3boot/2reload checkpoint](dire-wolf-upgrades-checkpoint.md). Grizzly hard10ordinary-hit stun and hard20Crushing Blow are implemented within the [42-marker/3boot/2reload checkpoint](grizzly-upgrades-checkpoint.md). Poison Creeper’s approved proportional base curve is implemented in the prepared poison path; learned exposure/transport gates remain separate. See [the balance checkpoint](creeper-balance-checkpoint.md).

| Pet | 10 hard points | 20 hard points / balance change |
| --- | --- | --- |
| Spirit Wolves | Open Wounds. | Cold immunity. Preserve cold-only attacks and baseline unchillability. |
| Dire Wolves | Existing Extra Fast affix. | Existing Extra Strong affix. |
| Grizzly | Ordinary successful hits: 50% stun for 3 seconds; boss-flagged targets: 10% for 1 second. No stun on BearSmite/knockback swats. | **10% Crushing Blow**. |
| Poison Creeper | — | Scale the existing curve proportionally to **2,000 average base total damage over four seconds at effective rank 20**, before the existing sole Rabies synergy. This is total damage, not damage per second. |
| Carrion Vine / Solar Creeper | — | No balance changes. |
| Oak Sage / Heart of Wolverine / Spirit of Barbs | — | No balance changes. |

The Grizzly stun is an approved addition beyond the baseline knockback/no-stun observation. The user settled its policy on 2026-11-03: ordinary successful hits have 50% chance for 3 seconds, or 10% for 1 second against existing boss-flagged targets; no stun on the special knockback swat. This Grizzly-only rule overrides ordinary boss stun immunity and does not add the generic unique-avoidance roll. Its implementation/verification scope is recorded in the [Grizzly checkpoint](grizzly-upgrades-checkpoint.md). Poison Creeper’s curve is settled: scale base rates by 640/101 with nearest FP256 rounding, then apply the existing 10% per hard Rabies point. The rank-20 range midpoint is exactly 2,000 before synergy; source upper-exclusive rolls remain unchanged. Its [checkpoint](creeper-balance-checkpoint.md) records implementation, persistence and remaining exposure/transport limits. Ravens were not specified in this pass and remain unreviewed.

## Assets and implementation acceptance

### Safe-area parking

Approved lifecycle policy; the [implementation checkpoint](safe-area-minions-checkpoint.md) records the wired scope and remaining acceptance. The later [legacy/facet and wardrobe checkpoint](parking-boundaries-checkpoint.md) expands Necromancer boundary coverage. Broader species/custom-region/controller/client coverage remains explicit.

Long-lived summoned armies park automatically when their owner enters an **actual safe area** and return when the owner leaves it. Use explicit region/safety classification, never an indoor/roof/building test. A dungeon building is not a safe area merely because it is indoors. Houses qualify only where their area is actually classified as safe.

Parking retains the same roster and summon identities, ownership, equipment/combat snapshots and wounds. It grants **no recovery or reset**: no healing, curing, resource refill, cooldown reset or refreshed buff duration on entry or return. Temporary effects retain their original deadlines and normal expiry behavior while parked. Do not implement this by deleting and freshly summoning the army or freezing its effect timers. Normal owner-death, replacement and expiry rules still apply; parked summons are not protected from those lifecycle rules.

Town healers may heal the owner's parked living roster through their service menu. This is an explicit healer action, not an automatic benefit of entering town. Reuse existing healer access/service rules; do not invent a new price or resurrection entitlement. A service must find the owned parked roster without briefly spawning it in town.

The owner's **named mercenary remains beside them** in safe areas. Identify this companion through the existing mercenary ownership/profile service, not merely a custom name. Town dialogue is a possible later use of the existing conversation hook; no dialogue content is approved or implemented here.

Keep the earlier short-lived trap rules separate: traps remain in the old facet until expiry, stop fighting while the owner is offline, and retain running lifetimes. This parking proposal does not make traps travel with the army.

Implementation leads: `PersistentMinions` already internalizes persistent actors without deleting their native ownership, and `MinionWardrobeProfiles` persists roster/slot/expiry state. Existing class-switch parking and logout suspension alone do **not** establish the new safe-area behavior. `MercenaryCare.HealFollowers` currently requires the pet to be near the healer, so it does not yet reach internalized rosters. Verify region entry/exit/login/teleport/reload, stationary parked-state expiry and no regeneration, owner death, replacement, safe-area summoning, one-time follower slot accounting, failed return placement and healer eligibility before marking this lifecycle change complete. Reuse existing safe-region policy only after checking that its actual classifications match this rule.

Track missing cast/projectile/impact/status/body/shatter/audio requirements in [skill effects](skill-effects.md), reusing existing assets where appropriate and identifying placeholders. Track talent-tree/hotkey icons separately in [skill icons](skill-icons.md). Autonomous pet abilities add no approved player hotkeys.

No upgrade is mechanically complete merely because its design is approved. Verify formulas, shared affix composition, AI/proc timing, targets, damage/events, live unlock changes, saved poison-Mage conversion, original-item preservation and real save/reloads when implementation begins. Keep unsupported details explicit and continue unaffected baseline work.

Discussion preference: the user requested **no asynchronous question popups** because draft text disappeared. Use free-form chat for any remaining design questions. The ten numbered Necromancer defaults were approved in chat, with the explicit buff changes incorporated above; they are no longer open proposals.

## Skeleton Mastery mapping — approved 2026-11-01

Keep the existing captured Mage life benefit. A basic spell’s rank remains effective Raise Skeletal Mage. Add the old NecromageMissile’s elemental endpoint increase attributable to captured effective Skeleton Mastery to the matching Sorceress spell endpoints. Compute that increase by comparing the old mastered and zero-Mastery derived missile ranks at the same captured summon rank; do not add the old unmastered base damage. Apply this flat bonus to each basic bolt, including each Charged Bolt. Fixed-level secondary spells and buffs receive no added Mastery damage. The user approved this mapping in chat; it resolves the previous open design question.
