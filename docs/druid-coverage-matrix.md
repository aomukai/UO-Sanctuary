# Druid acceptance map

**Native shield producer update — 2026-11-16 JST.** [Qualified checkpoint](unresisted-shields-checkpoint.md):33 checks/six actual Energy Shield casts/two reloads now cover normalized native armor-ignore channels through the existing Energy/Bone/Cyclone pipeline. This corrects a missing producer without new shield formulas or priority policy. Unknown direct/remainder inputs and Ward/Energy overlap remain refused; source monster drains and special native/PvP/event ordering remain open. Older scopes and receipts below retain their historical binaries/source inventories.

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. Charms and runewords are excluded. All 30 talents have handlers; the current compatibility cohort passes 684 exact results across three saves/two reloads. A handler or passing subset does not certify complete modern D2R parity.

The [elemental Health Link checkpoint](druid-element-link-checkpoint.md) adds 32 direct contexts and four periodic-ground controls per boot for six spells. Settled non-poison channels now reach linked recipients; Boulder ground remains periodic, and pet melee remains separate. Verify the current receipt with `python3 tools/record_druid_element_link.py --check`.

| Talent | Mechanical scope | Existing fixture |
| --- | --- | --- |
| Firestorm | Paid SC cast, distinct stream delay, travelling fire and saved emission | `firestorm` |
| Molten Boulder | Fractional payment, rolling physical/fire hit and knockback, trail/terminal ground fire | `molten-boulder` |
| Arctic Blast | Approved held input, fractional startup/renewal, captured cold/potion stream, release and no saved intent replay | `arctic-held-input`, `arctic-stream`, `arctic-potion` |
| Fissure | Paid ground cast, local delay, saved vent field, footprints and natural damage | `fissure` |
| Cyclone Armor | Paid elemental absorption pool, hard synergies, expiry/replacement and saved state | `cyclone-armor` |
| Twister | Paid three-path cast, physical damage/stun, travelling collision and saved paths | `twister` |
| Volcano | Paid delayed ground field, physical/fire emission and saved field/missiles | `volcano` |
| Tornado | Paid travelling charged path, periodic/direct contacts and saved packet/path state | `tornado` |
| Hurricane | Paid self weather, captured duration, periodic cold/chill and saved pulses | `hurricane` |
| Armageddon | Paid self weather, captured duration, periodic impacts and saved emitters/meteors | `armageddon` |
| Werewolf | Paid human entry/free same-form return, SC/cooldown, captured form and relative life | `druid-form-model`, `druid-form-world`, `druid-form-casting` |
| Lycanthropy | Captured form life/duration passive, item ranks and saved transformed resources | `druid-form-model`, `druid-form-world` |
| Werebear | Paid human entry/free return, captured damage/defense/life and form-specific attacks | `druid-form-world`, `druid-form-casting`, `druid-form-attack` |
| Maul | Bear-only paid weapon contact, captured charges, stun/damage, expiry and live combat | `druid-maul`, `druid-charge-model` |
| Feral Rage | Wolf-only paid contact, movement/life-leech charges and natural combat | `druid-feral`, `druid-charge-model` |
| Fire Claws | Wolf/bear weapon plus mastered fire, hard synergies, payment and original saved packet | `druid-fire-claws` |
| Rabies | Paid wolf S3 bite, captured poison, separate marker/emitter/spread, lifecycle and saved infection | `druid-rabies`, `druid-rabies-infection`, `druid-rabies-lifecycle`, `druid-rabies-packet` |
| Shock Wave | Bear-only SC/FCR release, physical/stun waves, natural expiry and saved missiles | `shock-wave-casting`, `shock-wave-components`, connected live control |
| Hunger | Wolf/bear S3 contact, damage penalty, recovery, avoidance and saved resources | `druid-hunger`, `druid-hunger-model`, `druid-form-basic-block` |
| Fury | Paid wolf series, live targets, interruption/retargeting and no saved paid-series replay | `druid-fury`, connected pre-save stage |
| Raven | Current birth packet/budget, commanded focus, natural attempts/lifetime and saved flock | `raven-snapshot`, `raven-body`, `raven-casting`, `raven-stats`, `raven-flight` |
| Poison Creeper | Approved captured rank/Rabies, stronger rank-20 curve, poison fields, always-vulnerable body and saved cast | `creeper-learned`, `creeper-poison`, `creeper-ground`, `creeper-world`, `creeper-body`, `creeper-balance` |
| Oak Sage | Paid summon, captured spirit stats/aura, shared spirit slot and saved original body | `oak-casting` and retained spirit consumers |
| Summon Spirit Wolf | Paid independent wolves, captured effective synergies/hard upgrades, cold/wounds and saved original actors | `spirit-wolf-casting`, `druid-snapshot`, `spirit-wolf-upgrades`, `spirit-wolf-mode` |
| Carrion Vine | Paid shared-vine summon, corpse consumption/life recovery and original saved injury | `recycler-learned`, `recycler-casting`, `recycler-world`, `recycler-recovery` |
| Heart of Wolverine | Paid shared-spirit summon and captured attack/damage aura | `wolverine-casting` and retained spirit consumers |
| Summon Dire Wolf | Paid independent wolves, captured synergies/Fast/Strong, corpse rage and saved actors | `dire-wolf-casting`, `druid-snapshot`, `dire-wolf-upgrades`, `dire-wolf-world` |
| Solar Creeper | Paid shared-vine summon, approved whole-point mana recovery and saved body/resources | `recycler-learned`, `recycler-recovery`, `recycler-body` |
| Spirit of Barbs | Paid shared-spirit summon and captured flat-return aura | `barbs-casting` and retained spirit consumers |
| Summon Grizzly | Paid original bear, captured synergies/normal-hit stun/Crushing Blow and saved combat state | `grizzly-casting`, `druid-snapshot`, `grizzly-upgrades`, `grizzly-world` |

The [current driver](../tests/check_druid_element_link.py) retains the planned Druid cohort and adds Defiler/readiness controls, for 59 fixture families, 23 shared checks, 43 current input commands, four natural live commands, nine elemental wire sequences, three held Arctic sequences and sixteen pre-save snapshot/expiry commands per boot. The existing paid Fury staging command runs on its own connection immediately before saving. Its naturally expiring form is not staged prematurely while unrelated casts run.

## Approved behavior retained

Current public summons use captured birth stats and approved difficulty rescaling preserves injured life percentage. Explicit supplement-absent historical profiles remain compatibility scopes; natural historical rank-transition controls are distinguished from current public-birth checks. Beast hard-point upgrades, independent wolf targets, shared Raven focus, edible monster corpses, Grizzly's 50%/three-second normal and 10%/one-second boss ordinary-hit stun, and ten-percent Crushing Blow retain their approved rules.

Poison Creeper targets 2,000 average base damage over four seconds at rank 20 before its hard Rabies synergy, scaling the existing curve. All vines remain vulnerable; Solar recovery uses the pinned table with whole-point rounding. Rabies stops spreading on logout/facet separation while timers continue. Werewolf/bear entry and exit preserve relative life; repeated same-form use returns human without mana. Arctic uses its separately approved held-input rule. Safe-area parking and normal effect expiry retain their shared no-recovery/no-reset rules.

## Remaining scope

Exact current executable animation/input/RNG/stat/event/world/PvP equivalence and exceptional item/recipient/terrain cases remain explicit per talent. Prepared/injected-clock and historical-profile tests retain their bounded claims; they are not substituted for every current naturally scheduled context. Broader graphical-client acceptance is still separate. See the [skill ledger](../data/Sanctuary/Balance/class-skill-status.json) and individual source/native receipts. Four older Warlock proposals remain frozen pending.

Final [effects/audio](skill-effects.md) and [talent/hotkey icons](skill-icons.md) remain separately tracked with identified placeholders. This checkpoint includes no artwork, charm/runeword replacement or deployment. Installed build, saves, configuration and client protection remain required.

Accepted candidate: **C1/I15/source 17/native 7**, 57 families, 23 shared checks, 201 connections, three saves and two reloads. [Receipt](../tools/reference/druid-planned-acceptance-native-evidence.json), [checkpoint](druid-planned-acceptance-checkpoint.md). Five known WardrobeSet save warnings remain explicit; no unsupported-context warnings or connection denials occurred. Production gameplay/data/kernel and installed saves/configuration/client remain unchanged. Next class: Warlock/Witch; its four frozen proposals remain pending.
