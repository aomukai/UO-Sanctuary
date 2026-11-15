# Necromancer acceptance map

**Native corpse coverage — 2026-11-16 JST.** The [native Lich checkpoint](native-corpse-checkpoint.md) adds actual native deaths, four public corpse consumers, six CE/Poison Explosion socket casts and two reloads. Raise/Mage are retained paths; CE/Poison selectability is new. Native Lich Revive and flesh-dependent consumers remain outside proven scope. No new skill handler or class certificate.

**Bone Armor policy approved — 2026-11-16 JST.** [Exact decision](../tools/reference/bone-armor-rule-approval.json), [adoption evidence](../tools/reference/bone-armor-rule-adoption-evidence.json), [checkpoint](bone-armor-rule-checkpoint.md). The user adopted the exact tested conventional overflow, supported incoming order and native armor-ignore physical mapping. C2/I2/S2/N2 remains the runtime proof:36checks,six casts,two reloads. No gameplay/source/data/assembly change or redundant native rerun was needed to adopt it. Modern exact executable behavior remains adapted,not newly recovered;unqualified legacy lanes and other class limits stay explicit.236/240handlers,no class certified. Earlier pending statements below describe the pre-approval snapshot.

**Bone Armor absorption qualification — 2026-11-16 JST.** [Checkpoint](bone-armor-rule-checkpoint.md), [policy proposal](bone-armor-rule-proposal.md). Native AOS armor-ignore now consumes the physical Bone Armor pool while bypassing resistance;real Shaftstop/physical/mixed controls pass. Current conventional breaking-hit overflow and typed Energy-before-Bone order pass C2/I2/S2/N2:36checks,six real casts,two reloads,including original stored overlap and fractional resources. Policy adoption remains pending because modern event22/order is unavailable;existing Bone Armor stays playable. Generated barrier descriptions now match the already-approved rule.236/240handlers,no class certified;installed build/saves and separate assets preserved.

**Approved learned Bone Wall/Prison world rule — 2026-11-16 JST.** [Checkpoint](bone-barrier-world-cast-checkpoint.md), [exact approval](../tools/reference/bone-barrier-world-rule-approval.json). Normal learned casts now use the approved formation collapse, immediate collision release and600ms removal. C1/I1/S1/N1 passes49checks, six real inputs and two reloads, retaining earlier collision, AI-clock and legacy-save controls. Historical roots keep historical children; no candidate allocator is used in input tests. **236/240handlers; Necromancer30/30; no class certified.** Installed build/saves remain unchanged; final effects/audio and icons remain separate. Earlier entries below describe historical stages.

**Prepared Bone Wall/Prison world adaptation — 2026-11-16 JST.** [Complete proposal](bone-barrier-world-rule-proposal.md), [checkpoint](bone-barrier-retirement-checkpoint.md). C3/I3/S4/N4 passes 39 checks, six candidate public-input casts and two reloads: whole-formation collapse, immediate movement/placement/projectile release, shared DT15/600ms completion, explicit distant-child cleanup, original pending resources and actual in-progress saves with overdue removal. Existing sentry timing/resource-retention services are reused. **Approval is pending; ordinary learned barriers retain their previous behavior.** Handler coverage stays236/240 and no class is certified. No client/kernel rebuild, new timer/profile, source-version substitution, installation or artwork. The four older Warlock proposals stay frozen.

**Deterministic live-combat acceptance (2026-11-06 JST):** The [Entropy cloud checkpoint](entropy-clouds-checkpoint.md) includes newer Attract input/reload controls. Per-actor ordinary decision/accuracy suppliers retain default game RNG; the fixture forces a first miss then a hit through autonomous AI, and preserves the independent mind seed. A failed eight-second broad damage observation and a passing unchanged-build isolation are archived; the failure alone does not establish its root cause. This is scoped acceptance, not a new class certificate.


**Individual item grants (2026-11-06 JST):** Maelstrom, Ume's Lament, Blackbog's Sharp and Boneshade now pass 14 actual zero-hard skill casts and two-reload original item/rank/resource/buff retention. Twelve additional catalogue bindings enable exactly those four whole items. Content1 / Integration4 / snapshot6 passes 12 focused + 188 retained markers; all 185 parent markers remain. See [checkpoint](necromancer-item-skills-checkpoint.md). **198 handlers; all 30 Necromancer handlers have scoped checks; no class complete.** Held proc/charge items, Sunder and the documented exact modern barrier/world/client gaps remain separate.

**Original individual-skill property checkpoint (2026-11-06 JST):** raw107/property22 now preserves whole Undead Crown equipment on graph Iron and frozen Revive contacts through two reloads, without granting NPC skills. Content1 / Integration2 / snapshot3 passes 12 focused, 185 retained and 12 learned-Iron markers; complete older property22 passes 13,824 source controls. Generic numeric and player item-skill activation gates remain distinct. See [checkpoint](single-skill-property-checkpoint.md). **198/240 handlers; all 30 Necromancer handlers have scoped checks; no completed class or deployment.** Remaining class acceptance includes the individual-skill activation catalogue, nonzero Sunder and documented barrier/current-engine/world/client gaps.

**Howltusk / player-only mana checkpoint (2026-11-06 JST):** original Howltusk now passes learned Iron Golem and original Revive admission without trimming properties. NPC stat114/function13 retains its registrations and has no effect on pet or owner mana, matching the complete older player-only guard. Existing fear, knockback and flat return remain separate. **Content 2 / Integration 4 / snapshot 4 passes 12 focused + 155 retained + 12 isolated learned-Iron markers**, each through two reloads; all 152 parent markers remain. Source 4,032 cases, 71 Python tests, fresh 1089/21 audits and installed-save/client protection pass. Available Deathspade preserves former unsupported-item negatives. See [checkpoint](npc-damage-to-mana-checkpoint.md). Ring of Fire's new do154/damage17 gates now have a [pinned preflight](warlock-ring-of-fire-preflight.md). **197/240 handlers; zero completed classes; no deployment.**

**NPC item knockback checkpoint (2026-11-06 JST):** original Griswolds Edge Iron Golem and frozen original Revive now use stat81/function07 through legacy and saved item-event graphs. The current damage packet carries the knockback request into the existing ordered return/movement consumer. **Content 2 / Integration 7 / snapshot 8 passes 12 focused + 152 retained markers**, each through two reloads; all 149 parent markers remain. Complete supplementary source has 24,576 cases; 71 Python tests, fresh 1089/21 audits and installed-build/save/client protection pass. Earlier fixture LOS/interference and the actual saved-graph omission are documented separately. See [checkpoint](npc-item-knockback-checkpoint.md). **197/240 handlers; zero completed classes; no deployment.**

**NPC item fear increment (2026-11-06):** original Face of Horror Iron/Revive now uses the shared Terror controller with an independent saved item-AI initializer. Content 5 / Integration 8 passes 15 focused and 149 retained checks, each through two reloads. This is a newer dedicated scope, not a rerun of the 115-family acceptance. See [the checkpoint](npc-item-fear-checkpoint.md).

Mechanical reference: **D2R Reign of the Warlock 3.3.93847**. Sanctuary's approved pet, targeting and roster changes override the reference. This map identifies the existing execution paths and tests; handler presence or a passing subset is not a class-completion certificate. The detailed unresolved limits remain in `data/Sanctuary/Balance/class-skill-status.json` and the linked checkpoints.

`NecromancerCombat` supplies the learned begin/release routes, with `ClassCombat` dispatching them before its historical fallback. The three passive talents feed summon creation rather than appearing as active casts. Existing shared flight, poison, curse, prepared-contact and pet controllers remain the implementations of those mechanics.

The current combined driver is `tests/check_necromancer_current_acceptance.py`. Family names below identify its native checks; separately accepted newer scopes are explicitly distinguished afterward.

| Talent | Mechanical execution | Included native families / checks |
| --- | --- | --- |
| Raise Skeleton | Corpse selection, paid summon, saved world AI and prepared melee | `skeleton-birth`, `skeleton-world`, `skeleton-contact`, `skeleton-combat`, `skeleton-roster`, `skeleton-upgrades` |
| Skeleton Mastery | Captured life/damage supplier for Skeletons, Mages and Revives | `skeleton-birth`, `mage-birth`, `mage-basics`, `revive-cast` |
| Clay Golem | Paid summon, world controller, contact and incoming slow | `clay-golem-birth`, `clay-golem-world`, `clay-golem-combat`, `clay-golem-contact`, `clay-golem-slow`, `clay-upgrades` |
| Golem Mastery | Captured life, attack rating, velocity and approved damage supplier | Four golem birth families; dedicated learned Mastery acceptance is a separate newer scope |
| Raise Skeletal Mage | Paid three-element summon; saved bolts, unlocks, buffs and migration | `mage-birth`, `mage-world`, `mage-basics`, `mage-missile`, `mage-elements`, `mage-historical`, `mage-meteor`, `mage-glacial`, `mage-chain`, `mage-static`, `mage-pet-buff`, `necromancer-army` |
| Blood Golem | Paid summon, world melee and fractional life transfer | `blood-golem-birth`, `blood-golem-world`, `blood-golem-combat`, `blood-golem-contact`, `blood-damage`, `blood-life-tap` |
| Summon Resist | Captured passive resistance supplier | `summon-resist`, golem and Skeleton/Mage birth checks |
| Iron Golem | Original item admission, paid birth, saved item graph, world AI and ordered contact | `iron-golem-cast`, `iron-golem-birth-graph`, `iron-golem-body`, `iron-golem-world`, `iron-golem-damage`, `iron-golem-retaliation`, `iron-golem-strong`, `iron-golem-spectral`, shared item/contact checks |
| Fire Golem | Paid summon, world melee, Holy Fire, absorption and retirement | `fire-golem-birth`, `fire-golem-world`, `fire-golem-combat`, `fire-golem-absorption`, `fire-golem-aura`, `fire-golem-death`, `fire-upgrades` |
| Revive | Original corpse actor activation, captured combat effects, persistent roster | `revive-cast`, `revive-native-activation`, `revive-facet`, `necromancer-army` |
| Teeth | Shared integer fan into saved source-flight missiles | `teeth` |
| Bone Armor | Captured fractional physical pool and incoming shield consumer | `bone-armor`, `damage-rounding`, `class-skills` |
| Poison Dagger | Prepared weapon start, ordered contact and combined poison | `weapon-start`, `poison-dagger-attack`, `poison-dagger`, shared prepared/contact checks |
| Corpse Explosion | Paid corpse release into physical/fire damage | `corpse-cast`; newer ground/town acceptance is separate |
| Bone Wall | Paid barrier creation, independent makers, resources and expiry clock | `bone-barrier-cast`, `bone-wall-maker`, `bone-barrier-birth`, `bone-barrier-clock`, `bone-barriers` |
| Poison Explosion | Paid corpse release into eight saved moving clouds | `poison-explosion`, `spell-poison-ticks`; newer ground/town acceptance is separate |
| Bone Spear | Saved piercing magic source-flight missiles | `bone-spear` |
| Bone Prison | Paid barrier allocation and saved anchors/links/expiry | Shared barrier families; newer ground acquisition acceptance is separate |
| Poison Nova | Saved ring flights and strongest-poison tick consumer | `poison-nova`, `spell-poison-ticks` |
| Bone Spirit | Saved guided magic source-flight missile | `bone-spirit` |
| Amplify Damage | Paid curse area, rank arbitration and physical resistance | `amplify-damage` |
| Dim Vision | Paid curse area and saved blind AI controller | `dim-vision-cast`, `blind-world` |
| Weaken | Paid curse area and ordinary weapon damage-percent pool | `weaken` |
| Iron Maiden | Paid curse and ordered fractional melee reflection | `iron-maiden`, shared prepared/contact checks |
| Terror | Paid curse and fear/return-to-combat AI transition | `terror-cast`, `blind-world`, `howl` |
| Confuse | Paid curse, neutral allegiance and independent saved target override | `confuse-cast`, `confuse-resume`, connected reload checks |
| Life Tap | Paid curse and fractional physical-hit healing | `life-tap-cast`, `life-tap` |
| Attract | Paid selected-centered curse and neighbor target leases | `attract-cast`, `blind-world` |
| Decrepify | Paid curse, physical resistance/damage and motion/attack slowing | `decrepify` |
| Lower Resist | Paid four-channel resistance curse | `lower-resist` |

## Newer evidence outside the original combined cohort

The combined driver adds the approved army-limit and original Revive activation families to the original 113-family cohort. It does **not** silently claim to rerun every later checkpoint. Dedicated current receipts cover learned Golem Mastery, explosion ground targeting and corpse-location town rejection, Raise Skeleton/Mage corpse-town rejection, Bone Prison targeting, elite Amplify/Lower Resist integration, the expanded ordinary blind/fear AI catalogue, and the later full NPC damage/status/item callbacks. See their named entries in the status ledger and resume checkpoint. Their historical binary identities and limits stay attached to those receipts.

## Remaining mechanical acceptance

- Bone Wall/Prison now use the explicitly approved native world/linked-death mapping: whole-formation collapse, immediate movement/placement/projectile release, shared DT15/600ms completion and persistent finite expiry. Default learned input, source-derived AI clocks, old saved types and actual in-progress saves pass the [learned checkpoint](bone-barrier-world-cast-checkpoint.md). Exact modern footprint/owner/hidedead parity remains unverified rather than silently inferred; the approved adaptation resolves the playable contract. Historical byte upgrades, unheld concurrent snapshots and graphical acceptance remain separate.
- Iron Golem and Revive admit supported item/stat/event projections. Explicit unsupported projections remain refusals before consumption; wider original-monster AI/species, abilities and current-engine events still need acceptance. UO caster conversion to D2 spells is a separate world task.
- Full modern geometry, room/candidate ordering, RNG/event ordering, PvP/special recipients and some breaking-hit shield/poison details remain bounded individually. The combined cohort validates its actual native adapters; it does not establish every current executable path.
- Actual graphical-client combat and final assets remain separate. [Effects/audio](skill-effects.md) and [talent/hotkey icons](skill-icons.md) retain identified placeholders rather than blocking mechanical work.

Continue source-backed, unaffected skills when one of these is blocked. Do not retry inaccessible reference pages without an external change or silently convert uncertainty into an approved rule.
