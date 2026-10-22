# Class skill roadmap

Mechanics reference: **D2R 3.3.93847, Reign of the Warlock**. See [reference and deviations](skill-mechanical-reference.md). The user authorized work past individually blocked skills. Missing final art does not block mechanics. **No class is marked complete at this checkpoint.** Handler counts are implementation coverage, not parity or full verification.

| Order | Class | Handlers present | Individually blocked | Pending | Class completion |
| --- | --- | ---: | ---: | ---: | --- |
| 1 | Necromancer | 28 / 30 | 2 | 0 | Open |
| 2 | Sorceress | 29 / 30 | 1 | 0 | Open |
| 3 | Paladin | 30 / 30 | 0 | 0 | Open |
| 4 | Barbarian | 17 / 30 | 0 | 13 | Open |
| 5 | Amazon | 0 / 30 | 0 | 30 | Open |
| 6 | Assassin | 0 / 30 | 0 | 30 | Open |
| 7 | Druid | 0 / 30 | 0 | 30 | Open |
| 8 | Warlock | 0 / 30 | 0 | 30 | Open |

Later-class order is a dependency-based working assumption; only Necromancer-first was required. Warlock/Witch identity, trees and growth are staged; Warlock skill mechanics have not been implemented.

Use [the current checkpoint](class-skills-resume-checkpoint.md) for verification evidence, unanswered questions and the next executable work. Use the independent [effect](skill-effects.md) and [icon](skill-icons.md) registers for the asset phase.

Shared direct-damage checkpoint: integer FP256 channel reduction/absorb, fractional native damage commitment and the source less-than-one-life lethal boundary pass native checks and two save/reload cycles. See [numeric evidence](../tools/reference/damage-rounding-engine-evidence.json). The resource-load correction also passes the full suite and a nineteen-actor eight-class/legacy reload matrix; the existing title byte now accepts Warlock's eighth achievement bit without inventing its cosmetic prefix. Negative-MDR overflow and modern event/mode behavior remain open. Sacrifice prepared-hit/self-cost mechanics are staged with [source/native evidence](../tools/reference/sacrifice-engine-evidence.json); Smite shield/S1/stun, Zeal timed series and Charge travel/impact phases are staged. Charge has168 compiled source cases plus native phase/resource/input/reload and connected-owner ground-motion coverage; standard mapped-monster knockback now uses the shared source mode/path controller; special species and complete geometry/mode acceptance remain open. Conversion now has its attack/allegiance/AI/reward/expiry paths; higher-level life restoration and unsupported target cases remain explicit gates. Continue unaffected roadmap work while retaining individual limits. No class completion is claimed. The user-authorized local test installation is updated; current verification and remaining acceptance work are in class-skills-checkpoint-2026-10-23.md.

Implementation rule: inspect existing handlers and shared mechanics before adding a new controller. Reuse verified geometry, movement, timing and resource consumers where the source agrees, keeping skill-specific damage, collision and targeting rules separate. Pinned Teeth and Multiple Shot both use server do8; verify/extract that common fan from the existing provisional Teeth implementation when reaching Amazon. Battle Command already reuses the Shout/Battle Orders travelling-ray and proportional-resource paths.

Howl source preparation now has1169 bounded compiled cases in [source evidence](../tools/reference/howl-engine-evidence.json). Its native handler uses a separate source AI payload in the shared curse lane: Terror includes initial escape, later threat selection/close combat and a default30-distance idle boundary. Earlier ordinary-Terror/item-fear variants still need their own acceptance before adopting this payload; the native Howl consumer is verified separately.

## Necromancer

| Skill | Source ID | Implementation | Remaining acceptance work or blocker |
| --- | ---: | --- | --- |
| Raise Skeleton | 70 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Skeleton Mastery | 69 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Clay Golem | 75 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Golem Mastery | 79 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Raise Skeletal Mage | 80 | handler-present | Summoning, independent count and persistence covered; all four projectile elements, poison caps and moving-target travel remain to verify. |
| Blood Golem | 85 | handler-present | Native target drain, 30/70 healing allocation, both overflow directions and recipient caps covered. Return-damage/incoming-damage events, difficulty bases and D2R comparison remain. |
| Summon Resist | 89 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Iron Golem | 90 | blocked | Requires authoritative metal-item eligibility, ownership/consumption, complete inherited item effects/procs, and persistent item identity. |
| Fire Golem | 94 | handler-present | Native source passive selection and replacement death burst covered. Aura scaling, absorb order, combat death and difficulty bases still need dedicated fixtures. |
| Revive | 95 | blocked | Requires saved corpse species/combat reconstruction, original special abilities, excluded-species rules and the source 180-second lifetime; do not substitute a generic skeleton. |
| Teeth | 67 | handler-present | Numerics covered; fan geometry, per-cast next-hit delay, moving-target collisions and final client presentation still need play verification. |
| Bone Armor | 68 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Poison Dagger | 73 | handler-present | Dagger targeting and source poison present; equipment poison duration/rate combination and drain interactions need further verification. |
| Corpse Explosion | 74 | handler-present | Corpse/RIP guards covered; complete base-life/level/difficulty/elite fixtures and saved orphan-corpse handling remain. |
| Bone Wall | 78 | handler-present | Destructible timed source-stat segments, growth and collision implemented. Native terrain, damage, lifetime and reload checks; exact growth missile range/cadence, inherited level, difficulty and live-client geometry still require D2R comparison. |
| Poison Explosion | 83 | handler-present | Source cloud lifetime retained; exact child-cloud footprint/hit cadence and overlapping clouds require D2R behavior comparison. |
| Bone Spear | 84 | handler-present | Native travelling/piercing and damage tiers covered; moving-target/obstacle/friendly edge cases remain. |
| Bone Prison | 88 | handler-present | Twelve destructible source anchors with provisional raster connectors enclose the target. Native gap/destruction/lifetime/reload checks; exact footprint, incomplete terrain, projectile exceptions and difficulty scaling remain. |
| Poison Nova | 92 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Bone Spirit | 93 | handler-present | Homing/retarget code present; retarget cadence, lost targets and all collision cases need dedicated native checks. |
| Amplify Damage | 66 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Dim Vision | 71 | handler-present | Native chase preservation, obstruction stop, adjacent acquisition, special suppression, expiry and saved motion covered. Caster/species behavior and live-client comparison remain. |
| Weaken | 72 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Iron Maiden | 76 | handler-present | Native melee reflection and ordinary ranged/elemental exclusions covered; source event order, reflected damage and special missile exceptions remain. |
| Terror | 77 | handler-present | Unsupported level gate removed. Source cast origin and flee-to-blind motion retained; ordinary-curse/flee coexistence and boss/species exceptions need D2R comparison. |
| Confuse | 81 | handler-present | AI consumer present; allegiance/credit/expiration behavior needs dedicated world verification. |
| Life Tap | 82 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Attract | 86 | handler-present | Non-overwrite rule covered; natural AI target acquisition, expiration and interaction with Confuse require world play tests. |
| Decrepify | 87 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Lower Resist | 91 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |

## Sorceress

| Skill | Source ID | Implementation | Remaining acceptance work or blocker |
| --- | ---: | --- | --- |
| Ice Bolt | 39 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Frozen Armor | 40 | handler-present | Native exclusive armor, source defense/duration/synergy, physical-melee freeze and reload covered; full cold immunity/species and client comparisons remain. |
| Frost Nova | 44 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Ice Blast | 45 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Shiver Armor | 50 | handler-present | Native attempted-melee retaliation including misses, chill instead of freeze, replacement and reload covered; all special attack/event cases remain to compare. |
| Glacial Spike | 55 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Blizzard | 59 | handler-present | Source elapsed-frame sampling, independent storms, source-rank snapshots, fresh per-fall synergies, short-lived LastCollide shards, cold damage/chill and local delay implemented. Exact D2R Size2 footprint/unit-search order, terrain mask, initial activation, lag and owner transitions require comparison; current tile projection is provisional. |
| Chilling Armor | 60 | handler-present | Source ReturnFire filtering and a travelling counter-bolt from a native arrow covered. Other missile/collision exceptions, cast/impact modifier timing and live-client comparison remain. |
| Frozen Orb | 64 | handler-present | Source rotating trail emissions, sixteen terminal curved shards, damage/chill, per-shard collision and local delay implemented. Overdue source frames are now processed before natural expiry; a controlled two-frame delayed callback preserves remaining travel emissions and terminal shards. Exact D2R initial-step/cell/path quantization, footprint, unit ordering, lag and owner transitions require comparison. |
| Cold Mastery | 65 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Charged Bolt | 38 | handler-present | Source bolt count/cap, fixed seed/waypoint vectors, capped lifetime, one-time mana and independent non-piercing overlaps have dedicated fixtures. Two-subtile tile projection, continuous collision/velocity, Slow Missiles and actual-client/D2R path comparisons remain open. |
| Telekinesis | 43 | handler-present | Hostile-monster damage and source 35% terrain-checked knockback present. Remote item/object/portal/waypoint interactions and forced hit-recovery semantics are not implemented; no generic interaction is substituted. |
| Static Field | 42 | handler-present | Native current-life fraction and mastery exclusion covered; Nightmare/Hell floors and mixed mitigation need dedicated fixtures. |
| Lightning | 49 | handler-present | Distinct source SQ sequence, travelling pierce and target exclusions have native coverage. Exact Size=2 footprint, LastCollide overlap cases, Slow Missiles and actual-client comparison remain open. |
| Nova | 48 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Chain Lightning | 53 | handler-present | Travelling segments, finite source hit counts, cyclic identity targeting, fresh child damage at the original cast rank, per-cast four-frame next-hit delay, resources and persistence implemented. Cast scope follows official 2.7 notes and firsthand original testing; compare the pinned modern build. Exact Size2/FP16 paths, target flags, gear timing, owner transitions and actual-client acceptance remain open. |
| Teleport | 54 | handler-present | Pre-existing working Sanctuary Teleport; its approved range/scroll/tome and timing rules remain authoritative. |
| Thunder Storm | 57 | handler-present | Source duration/interval, cast-rank snapshot, one-time mana cost, serial-order target cycling and resumed strikes through both reloads have native coverage. Shared-world radius mapping, full target eligibility and D2R gear timing still need acceptance. |
| Energy Shield | 58 | handler-present | Native pre-resistance absorption, hard-Telekinesis efficiency, fractional mana, depletion, channel order, poison exclusion, actual-life-loss conversion and reload covered. Special drain fields and native ignore-armor paths remain unsupported; broader mixed mitigation and gear timing need comparison. |
| Lightning Mastery | 63 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Fire Bolt | 36 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Warmth | 37 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Inferno | 41 | blocked | SQ6 timing and launched flames are prepared, but modern repeated-payment/re-entry cadence remains unverified. startmana6 is a threshold; legacy start/do/rewind and rank1 debit paths cannot safely establish the D2R channel loop. Keep talent gated pending trace/capture; do not charge six mana as a substitute. |
| Blaze | 46 | handler-present | Dedicated fixtures cover idle exclusion, accepted movement, source aura/speed/patch lifetime, fractional fire, expiration and saved patches. Exact subtile footprint, lag/view limits, full-heal fractional projection, town/owner-death lifecycle and client comparison remain open. |
| Fireball | 47 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Fire Wall | 51 | handler-present | Dedicated fixtures cover compiler-verified source synergy, perpendicular growth, damage-rate MDR scaling, fractional damage and independent local delay/expiry. Exact terrain/footprint, lag/view limits, town/owner-death lifecycle and client comparison remain open. |
| Enchant | 52 | handler-present | Dedicated fixtures cover ally/pet and held targeting, attack rating, cast snapshots, weaker/equal/stronger replacement, class/native/summon weapon fire, ranged wielder mastery, expiry/death and reload. Actual-client targeting/glow, special weapon paths and charged/oskill delivery remain open; no weapon item is modified. |
| Meteor | 56 | handler-present | Source delayed area impact, shared impact roll, distinct residual-fire damage/synergy, eighteen source offsets, rank-scaled flame lifetime and independent local delay implemented. Exact D2R terrain/Size2 overlap, timing, special gear paths, owner lifecycle and actual-client comparison remain open. |
| Fire Mastery | 61 | handler-present | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Hydra | 62 | handler-present | Three stationary untargetable heads, six-group cap, source rank/damage/mastery/pierce snapshots, legacy animation/AI timing, travelling bolts and persistence implemented. Exact modern startup/target search/terrain fallback, immunity piercing, owner-removal order, Slow Missiles/CanDestroy and client acceptance remain open. |

## Paladin

| Skill | Source ID | Implementation | Remaining acceptance work or blocker |
| --- | ---: | --- | --- |
| Prayer | 99 | handler-present | Delayed periodic direct healing, effective rank, useful-pulse-only fractional mana payment, natural recovery suppression and independent approved flat recovery staged. Modern callback/stacking, Blood Mana, owner/native timer transitions and client acceptance remain open. |
| Resist Fire | 100 | handler-present | Selected periodic resistance aura and allocated-point active/inactive maximum resistance staged. Effective rank drives ordinary resistance; active state suppresses the passive. Same-aura rank replacement, independent aura addition and player cap after pierce are covered. Modern filters/gear-owner transitions, full pet/native paths and actual-client acceptance remain open. |
| Resist Cold | 105 | handler-present | Selected periodic resistance aura and allocated-point active/inactive maximum resistance staged. Effective rank drives ordinary resistance; active state suppresses the passive. Same-aura rank replacement, independent aura addition and player cap after pierce are covered. Modern filters/gear-owner transitions, full pet/native paths and actual-client acceptance remain open. |
| Resist Lightning | 110 | handler-present | Selected periodic resistance aura and allocated-point active/inactive maximum resistance staged. Effective rank drives ordinary resistance; active state suppresses the passive. Same-aura rank replacement, independent aura addition and player cap after pierce are covered. Modern filters/gear-owner transitions, full pet/native paths and actual-client acceptance remain open. |
| Defiance | 104 | handler-present | Selected-right-slot aura and source defense-percent leases staged, additive with cold-armor defense. Exact modern aura filter/linger, town-room projection, all native defense paths and actual-client acceptance remain open. |
| Cleansing | 109 | handler-present | Delayed periodic remaining poison/curable-curse duration reduction with source state flags and effective Prayer healing staged. Unrelated native UO poison/curses/shrines lack adapters; modern timing/filter/stacking and actual-client checks remain open. |
| Vigor | 115 | handler-present | Selected aura with source stamina/recovery/velocity values, player/pet resource and movement consumers staged. Native baseline/tile-clock projection is retained; absolute D2R mode-dependent recovery, run/walk scaling, full pet modifier composition, gear/owner/client acceptance remain open. |
| Meditation | 120 | handler-present | Delayed player-only mana-recovery bonus additive with Warmth/gear and effective Prayer healing staged. Sanctuary native regeneration baseline is retained; absolute D2R max-mana recovery and native first-tick retiming remain acceptance questions, alongside gear/owner/client paths. |
| Redemption | 124 | handler-present | Delayed owner-only per-corpse recovery and one-use consumption staged with pinned corpseSel rules, captured death-time source identity and approved corpse access/loot protection. Unmapped legacy/native species are excluded; modern RNG/search/species/owner/client behavior remains acceptance work. |
| Salvation | 125 | handler-present | Periodic fire/cold/lightning resistance without poison or maximum-resistance bonuses staged. Adds to different elemental resistance auras. Modern filter/owner/item paths and actual-client acceptance remain open. |
| Might | 98 | handler-present | Selected-right-slot aura, immediate self/ally refresh, source periodic leases and physical-only player/skeleton/golem/native-pet bonuses staged. Exact modern aura filter/linger, town-room projection, all pet/item combinations and actual-client acceptance remain open. |
| Holy Fire | 102 | handler-present | Supplier-only whole weapon stats, shared fractional soft-hit pulse, delayed cadence and released2.4 proximity rule staged. Explicit linear tile/melee projection is provisional: modern distance metric, unit sizes and rounding, full mastery/ranged/native/owner/gear/filter/client acceptance remain open. |
| Thorns | 103 | handler-present | Source free delayed recipient aura, attack-attempt flat physical damage and prepared-physical percentage return staged. Native misses, typed/fractional/zero/ranged/spell cases, life-capped before-incoming-hit ordering, immunity, rank20 and two reloads covered. Legacy player/hireling percent reduction and NativeBaseHire projection are explicit; full gear/attempt ordering/owner/modes/PvP/client acceptance remains open. |
| Blessed Aim | 108 | handler-present | Selected-right-slot attack-rating aura and allocated-point passive staged. Active aura suppresses the passive; same aura does not sum. Exact modern aura filter/linger, town-room projection, all pet/item combinations and actual-client acceptance remain open. |
| Concentration | 113 | handler-present | Physical enhanced damage and20-percent interruptible class-action recovery guard staged, with source cast-time Blessed Hammer bonus delivery staged. Modern mode-transition/gear/owner/client paths remain acceptance work; NPC mode code has no equivalent protection check. |
| Holy Freeze | 114 | handler-present | Supplier cold weapon stats and shared pulse, source-mapped Prime-Evil/ColdEffect gates, separate capped movement/attack/other slow state and twenty-percent independent shatter marker staged. Target slow expiry does not silently clear that separate marker. Active shatter deaths retain loot but exclude corpse skills. Native UO other-animation mode consumers, modern lethal-pulse/death ordering, full species/state interactions, proximity geometry/rounding and gear/owner/client acceptance remain open; unmapped native targets are excluded explicitly. |
| Holy Shock | 118 | handler-present | Supplier-only minimum-one weapon stat, lightning synergy, shared fractional soft-hit pulse, delayed cadence and released2.4 proximity rule staged. Explicit linear tile/melee projection is provisional: modern distance metric, unit sizes and rounding, full mastery/ranged/native/owner/gear/filter/client acceptance remain open. |
| Sanctuary | 119 | handler-present | Free delayed undead magic/proximity pulse and source-mode knockback, supplier-only undead damage/flat AR and separate positive physical-resistance clamp staged. Legacy supplier-stat mana threshold is retained provisionally; knockback works at zero mana. Broader undead bypass flag, low-mana supplier stats, exact geometry/mode flags/post-KB recovery, special sand-leaper/owner/item/client behavior remain explicit acceptance work. No class/item gate cleared. |
| Fanaticism | 122 | handler-present | Skill attack rate separate from gear IAS, attack rating and caster/recipient physical bonuses staged in player/native pet/class summon consumers. Strict legacy positive-mana self override and equal-rank replacement are explicit, not modern-certified; full timing/dual-wield/pet/owner/item/client acceptance remains open. |
| Conviction | 123 | handler-present | Hostile source-ranked leases, supplier protection, base-immunity snapshot deltas, independent Lower Resist combination, defense-percent addition and class/legacy elite priority staged. Native damage, signed source defense, radius, equal/weaker rank behavior and two reloads covered. Original native base-stat mapping, shared self/enemy state edge cases, modern sunder/PvP/hireling/filter/owner/gear/client acceptance remain open. |
| Sacrifice | 96 | handler-present | Melee action/input, additive skill damage and attack rating, FP256 endpoint roll, prepared-physical/target-life-capped rank-dependent self cost and self-before-target commit staged. Native zero mana, immunity, miss/block, Energy Shield, real absorb/soft-rank gear, whole-life debit/self death and two reloads covered. Modern start/do snapshots, converted throwing adapter and full curse/item/leech/LifeTap/retaliation/mode/owner/client acceptance remain open; no item gates cleared. |
| Smite | 97 | handler-present | Pinned69 shield bases, stored-rank Holy Shield damage, additive shield Strength/off-weapon/skill percentages, physical-only hit without ordinary crit/leech/elements, distinct S1 timing and2-mana event payment staged. Mapped-species stun plus separate shared timed source knockback replace the old instant one-step displacement; native movement/attack gates, natural motion/recovery/expiry and two stun reloads covered. Active item_normaldamage now augments shield endpoints before percentages, with real Stone Crusher rolls and two-reload coverage. Unknown native control metadata, modern modes/timing, gated Astreon/Redeemer, absent Grief runeword acquisition and full proc/LifeTap/durability/geometry/client acceptance remain open; no item gates cleared. |
| Holy Bolt | 101 | handler-present | Timed ally/hostile cast, source fixed-point mana/damage/healing, piercing persisted flight, current Prayer/original missile rank healing, undead positive-resistance/MDR/absorb bypass and demon damage staged. Native pipeline, immune undead and poisoned ally/pet checks pass. Exact modern SC event/collision/unit-size/filter/owner/gear/client behavior remains acceptance work. |
| Zeal | 106 | handler-present | Source-counted timed melee series, one successful-start mana payment, live attack rating/damage/hard Sacrifice synergy, cyclic target identities, retargeting and uninterruptible movement/recovery lifecycle staged. Source scheduler/selection probes and native input/gear/late-frame/diagonal/resource/two-reload coverage; Approved low-mana ordinary-attack deviation retains bindings and resumes held Zeal at two mana; pinned D2R was observed to stop instead. Native zero/fractional-mana/input/return-to-Zeal coverage; modern timing/geometry and full item/curse/proc/durability/owner/client acceptance remain open. Active inputs are transient across logout/restart; no item gates cleared. |
| Charge | 107 | handler-present | Travel/arrival/one-impact/recovery phases, source velocity baseline100, additive skill damage/AR, hard Might/Vigor synergies, one9-mana start payment, close basic-attack fallback and accepted native steps staged. Native input/miss/obstruction/owner-transition/two-reload and connected-owner ground phases covered. Mapped standard-monster knockback now uses the shared source mode/path controller; unknown species and special sand-leaper control stay gated. Item knockback remains suppressed. Modern collision/range/body/FP projection, fallback payment, full gear/proc/curse/durability/owner/client acceptance remain open; no item gates cleared. |
| Vengeance | 111 | handler-present | Melee weapon timing/range, source additive skill attack rating, separate shared fixed-point weapon-base elemental roll, mastery percentage rounding, one event payment and typed defense/absorption delivery staged. Native damage/miss/block, actual soft/mastery gear, two-handed base/reach and two reloads covered. Distinct active item_normaldamage is excluded from the elemental roll only; real Stone Crusher10/30 differential/upper-endpoint/native physical-and-elemental/reload checks pass. Converted throwing weapons need a melee base adapter; exact modern start/do snapshots, mode/event/owner/gear/client acceptance remain open. No charged/oskill/item gates cleared. |
| Blessed Hammer | 112 | handler-present | Cast-time hard synergies/Concentration,77 source waypoints, Q12/FP16 stepping, five-update acceleration, piercing most-recent-collider history, ordinary magic immunity and separate species bonuses staged. Native source goldens, resources and two reloads covered. Fog float LUT is reconstructed explicitly; exact modern path/cell/body/terrain/order/RNG/SC, full gear/owner and actual-client behavior remain acceptance work. |
| Conversion | 116 | handler-present | Available melee attack with one4-mana start payment, prepared hit/base weapon packet, event-time conversion roll, no weapon damage on successful conversion, species-preserving allegiance/AI/protection/reward/expiry/persistence. Focused input/hit/miss/payment/success/cancel and two-reload coverage. Higher-level life restoration, unmapped species/imp attachment and bard interaction have explicit target gates. Complete stat-list removal, modern snapshots/proc/curse/durability/owner/gear/client behavior remain acceptance work; no item gates cleared. |
| Holy Shield | 117 | handler-present | Right-only shield-gated timed cast, source fixed-point mana, stored block/rank, live hard Defiance defense, shield-dependent consumers and faster block rate staged. Native cast/equipment/expiry/lifecycle and two reloads covered. Stored-rank Smite damage now has its separate staged consumer. Modern mode/weapon-swap/owner/gear/client acceptance remain open. |
| Fist of the Heavens | 121 | handler-present | Selected-target delayed lightning strike, original-position capped secondary burst, fresh child synergy and separate cooldown staged. Secondary Holy3 bypasses undead/demon resistance; no NHD with NextHit0, no ally/pet healing with parent flags0. Converted/dead target burst and saved corpse identity covered. Cursor-nearest automatic acquisition in the click-facing half-circle and held-ground identity retention are implemented from pinned-game user checks. Equal-distance ties, own-tile cursor and perpendicular boundary candidates stay gated; exact range/metric/cursor snapshots, complete removed-unit lifetime, timing/secondary order/primary-origin collisions and owner/gear/client acceptance remain open. |

## Barbarian

| Skill | Source ID | Implementation | Remaining acceptance work or blocker |
| --- | ---: | --- | --- |
| Howl | 130 | handler-present | Reuses the64-ray controller with pinned4mana, rank velocity, source MaxVel clamp/deceleration disable, stored rank/live caster-level gate, LastCollide and no Terror refresh. Shared curse metadata preserves initial escape/cleared caster override/later threat selection, idle/close combat, move intent and expiry. Supported Skeleton/Zombie/Megademon source AIs only; native source pathing/scan/leash adapters, Attract/curse-resistance failure transients, other callback species, modern SC/FCR/geometry/owner/client remain explicit limits. Ranks81+ remain gated for uncertain signed32 source overflow. Focused/full native verification recorded separately; no class certificate. |
| Find Potion | 131 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Shout | 138 | handler-present | Self state plus64 travelling decelerating rays, source6-mana do-event payment, stored effective rank/live hard duration synergies, repeated aligned recipient hits and weaker replacement are implemented. Cooperative players/pets receive additive defense, with Berserk retaining final zero defense. Dedicated native moving-ally, real gear, resource/interruption, lifecycle and two reloads exercise the bounded port. Source ring/hit/stat/acceleration routines compiled separately. D2 subtile-to-UO tiles, collision footprints/terrain/neutral NPC alignment, exact SC event/FCR, mid-windup mana-loss ordering, modern scheduling/snapshots and actual-client equivalence remain unverified; effects/icons are independent placeholders. |
| Taunt | 137 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Battle Cry | 146 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Find Item | 142 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Battle Orders | 149 | handler-present | Self state and64 travelling rays implement pinned7mana after self stats, stored effective rank/live hard duration, replacement and proportional FP256 current pools. NPC resource hooks preserve saved seeds/loading pools, Vigor joins additively, and summon creation fractions/HP regeneration have dedicated consumers. LastCollide initializes to caster; saved skill-local NextHit4-frame gates and rays resume across reloads. Focused player/pet/gear/zero-pool/expiry/lifecycle and two reloads pass. Exact modern NHD caster/cast scope, consume-failure edge, SC/frame/geometry/neutral/owner/client and approved whole-resource projection remain explicit limits; no class certificate. |
| Grim Ward | 150 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| War Cry | 154 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Battle Command | 155 | handler-present | Immediate self state and shared64-ray warcry implement11mana, nonstacking allskills1, stored cast rank/live hard duration and saved LastCollide/skill-local NextHit gates. Effective ranks update learned/granted skills without changing hard synergies; native passives/resources refresh, summon creation snapshots stay fixed and pet caps remove oldest saved creation order on expiry. Focused native three-boot/two-reload checks pass, including new/old summon snapshots, FIFO cap removal, passive resource transitions and resumed saved rays. Combined and actual-client acceptance remain separate. Historical pet order falls back to serial; modern SC/FCR/geometry/NHD caster/cast scope, NPC own skills and full owner/item/client behavior remain unverified. |
| Blade Mastery | 127 | handler-present | Pinned item-type inheritance and effective-rank melee AR/damage/critical are implemented. Strongest eligible bonus is selected; percentages join existing pools; mastery critical precedes Deadly Strike and doubles physical once. Real converted one/two-hand weapons, named soft/local/offweapon/deadly/flat-element gear and two reloads are covered by the dedicated fixture. Legacy selector/critical blocks are compiled separately. Dual wield, melee use of converted throwing items, modern snapshots/modes/owner/proc/client and material conversion acceptance remain open; Throwing Mastery is separate. |
| Axe Mastery | 128 | handler-present | Pinned item-type inheritance and effective-rank melee AR/damage/critical are implemented. Strongest eligible bonus is selected; percentages join existing pools; mastery critical precedes Deadly Strike and doubles physical once. Real converted one/two-hand weapons, named soft/local/offweapon/deadly/flat-element gear and two reloads are covered by the dedicated fixture. Legacy selector/critical blocks are compiled separately. Dual wield, melee use of converted throwing items, modern snapshots/modes/owner/proc/client and material conversion acceptance remain open; Throwing Mastery is separate. |
| Mace Mastery | 129 | handler-present | Pinned item-type inheritance and effective-rank melee AR/damage/critical are implemented. Strongest eligible bonus is selected; percentages join existing pools; mastery critical precedes Deadly Strike and doubles physical once. Real converted one/two-hand weapons, named soft/local/offweapon/deadly/flat-element gear and two reloads are covered by the dedicated fixture. Legacy selector/critical blocks are compiled separately. Dual wield, melee use of converted throwing items, modern snapshots/modes/owner/proc/client and material conversion acceptance remain open; Throwing Mastery is separate. |
| Polearm Mastery | 134 | handler-present | Pinned item-type inheritance and effective-rank melee AR/damage/critical are implemented. Strongest eligible bonus is selected; percentages join existing pools; mastery critical precedes Deadly Strike and doubles physical once. Real converted one/two-hand weapons, named soft/local/offweapon/deadly/flat-element gear and two reloads are covered by the dedicated fixture. Legacy selector/critical blocks are compiled separately. Dual wield, melee use of converted throwing items, modern snapshots/modes/owner/proc/client and material conversion acceptance remain open; Throwing Mastery is separate. |
| Throwing Mastery | 135 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Spear Mastery | 136 | handler-present | Pinned item-type inheritance and effective-rank melee AR/damage/critical are implemented. Strongest eligible bonus is selected; percentages join existing pools; mastery critical precedes Deadly Strike and doubles physical once. Real converted one/two-hand weapons, named soft/local/offweapon/deadly/flat-element gear and two reloads are covered by the dedicated fixture. Legacy selector/critical blocks are compiled separately. Dual wield, melee use of converted throwing items, modern snapshots/modes/owner/proc/client and material conversion acceptance remain open; Throwing Mastery is separate. |
| Increased Stamina | 141 | handler-present | Pinned live effective-rank maximum-stamina percentage, additive with Vigor and without free refill, is implemented. Real gear, natural native recovery after training, respec/history/death and two-reload checks cover the supported path. Exact modern stat collation/FP and item/owner/client acceptance remain open. |
| Iron Skin | 145 | handler-present | Pinned live effective-rank defense percentage is additive in the shared defense pool. Native gear/Dexterity defense, running suppression, respec/history/death and two-reload checks cover the supported path. Exact modern mode/event/owner/client comparison remains open. |
| Increased Speed | 148 | handler-present | Pinned diminishing skill velocity is added alongside Vigor to native walking/running, separate from item FRW diminishing. Accepted steps, gear/Vigor composition, respec/history/death and two-reload checks cover the supported path. Existing Sanctuary movement clocks are retained; modern continuous/mode/client equivalence remains open. |
| Natural Resistance | 153 | handler-present | Pinned diminishing four-element resistance enters the raw pool before cap/pierce, with no physical/magic/maximum-resistance grant. Actual typed native damage, real soft-rank/resistance gear, respec/history/death and two-reload checks cover the supported path. Exact modern stat/item/owner/client comparison remains open. |
| Bash | 126 | handler-present | Prepared melee hit/AR/hard synergies, critical/mitigation snapshots, one2-mana start payment and source-mapped timed knockback implemented. Source calc2 adds to the physical-event field after allocation leaves the stored life total unchanged; the legacy health consumer commits that stored total. Current D2R stale-total behavior is unverified. Input/cancellation/real gear/immune/miss/native control and two reloads have dedicated coverage. Unknown/special control species and converted throwing melee are gated; low-mana AttackNoMana, empty-ground acquisition, PvP and exact modern events/procs/leech/owner/client remain open. |
| Double Swing | 133 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Leap | 132 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Double Throw | 140 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Stun | 139 | handler-present | Prepared melee hit/AR/hard Bash damage, one2-mana start payment and stored tiered hard-War-Cry duration implemented. Shared source control supplies species/boss/unique/velocity/cap rules and native movement/attack gates. Input/cancellation/real gear/immune/miss/control natural expiry and two reloads have dedicated coverage. Unknown native control metadata and converted throwing melee are gated; low-mana AttackNoMana, empty-ground acquisition, PvP and modern snapshots/events/procs/owner/client remain open. |
| Leap Attack | 143 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Concentrate | 144 | handler-present | Prepared ordinary melee hit/AR/hard Bash/Battle Orders damage and Berserk physical-to-magic split after critical/before resistance, with one2-mana start payment. Action-long defense starts at preparation, refreshes at impact and lasts through recovery; voluntary movement/get-hit/Stop/rebinding do not interrupt. Connected natural-loop, named gear, misses/immunity, death/history/respec and two reloads have dedicated coverage. Legacy mode/state/conversion blocks compiled separately. Low-mana fallback, empty-ground acquisition, exact modern snapshots/queues/item events/procs/owner/client and dual wield remain open; no modern equivalence certificate. |
| Frenzy | 147 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Whirlwind | 151 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Berserk | 152 | handler-present | Prepared interruptible A1/IAS attack, linear AR, hard Howl/Battle Orders ED, one4-mana start payment and full physical-to-magic conversion after critical/before resistance. Timed final zero-defense override starts on hits/misses, persists through cancelled swings, replaces expiry on recast and saves independently. Do2 recreates an expired state only through the remaining animation. Connected natural timed/temporary expiry, real gear/physical-consumer controls, death/respec/history and two reloads have dedicated coverage. Modern snapshots/slow/modes/item/proc/owner/client, native magic-resistant species, low-mana fallback, automatic ground targeting and dual wield remain open. |

## Amazon

| Skill | Source ID | Implementation | Remaining acceptance work or blocker |
| --- | ---: | --- | --- |
| Jab | 10 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Power Strike | 14 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Poison Javelin | 15 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Impale | 19 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Lightning Bolt | 20 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Charged Strike | 24 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Plague Javelin | 25 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Fend | 30 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Lightning Strike | 34 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Lightning Fury | 35 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Inner Sight | 8 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Critical Strike | 9 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Dodge | 13 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Slow Missiles | 17 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Avoid | 18 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Penetrate | 23 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Decoy | 28 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Evade | 29 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Valkyrie | 32 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Pierce | 33 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Magic Arrow | 6 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Fire Arrow | 7 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Cold Arrow | 11 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Multiple Shot | 12 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Exploding Arrow | 16 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Ice Arrow | 21 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Guided Arrow | 22 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Immolation Arrow | 27 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Strafe | 26 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Freezing Arrow | 31 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |

## Assassin

| Skill | Source ID | Implementation | Remaining acceptance work or blocker |
| --- | ---: | --- | --- |
| Tiger Strike | 254 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Dragon Talon | 255 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Dragon Claw | 260 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Fists of Fire | 259 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Cobra Strike | 265 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Dragon Tail | 270 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Claws of Thunder | 269 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Blades of Ice | 274 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Dragon Flight | 275 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Phoenix Strike | 280 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Claw Mastery | 252 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Psychic Hammer | 253 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Burst of Speed | 258 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Cloak of Shadows | 264 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Weapon Block | 263 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Fade | 267 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Shadow Warrior | 268 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Mind Blast | 273 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Venom | 278 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Shadow Master | 279 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Fire Blast | 251 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Shock Web | 256 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Blade Sentinel | 257 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Charged Bolt Sentry | 261 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Wake Of Fire | 262 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Blade Fury | 266 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Lightning Sentry | 271 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Wake Of Inferno | 272 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Death Sentry | 276 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Blade Shield | 277 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |

## Druid

| Skill | Source ID | Implementation | Remaining acceptance work or blocker |
| --- | ---: | --- | --- |
| Firestorm | 225 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Molten Boulder | 229 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Arctic Blast | 230 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Fissure | 234 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Cyclone Armor | 235 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Twister | 240 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Volcano | 244 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Tornado | 245 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Hurricane | 250 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Armageddon | 249 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Werewolf | 223 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Lycanthropy | 224 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Werebear | 228 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Maul | 233 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Feral Rage | 232 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Fire Claws | 239 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Rabies | 238 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Shock Wave | 243 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Hunger | 242 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Fury | 248 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Raven | 221 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Poison Creeper | 222 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Oak Sage | 226 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Summon Spirit Wolf | 227 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Carrion Vine | 231 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Heart of Wolverine | 236 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Summon Dire Wolf | 237 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Solar Creeper | 241 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Spirit of Barbs | 246 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Summon Grizzly | 247 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |

## Warlock

| Skill | Source ID | Implementation | Remaining acceptance work or blocker |
| --- | ---: | --- | --- |
| Demonic Mastery | 374 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Summon Goatman | 373 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Blood Oath | 378 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Death Mark | 375 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Summon Tainted | 376 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Blood Boil | 380 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Summon Defiler | 377 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Engorge | 379 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Consume | 381 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Bind Demon | 382 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Levitation Mastery | 383 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Hex: Bane | 385 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Cleave | 391 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Echoing Strike | 388 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Hex: Purge | 389 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Blade Warp | 390 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Psychic Ward | 387 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Eldritch Blast | 384 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Hex: Siphon | 386 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Mirrored Blades | 392 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Miasma Bolt | 395 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Ring of Fire | 394 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Sigil: Lethargy | 393 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Sigil: Rancor | 396 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Miasma Chain | 399 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Flame Wave | 398 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Sigil: Death | 400 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Enhanced Entropy | 397 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Apocalypse | 401 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
| Abyss | 402 | pending | Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks. |
