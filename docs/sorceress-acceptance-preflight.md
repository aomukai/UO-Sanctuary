# Sorceress acceptance reuse plan

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations first. This prepares the next class without changing the active Necromancer candidate or enabling a skill. Sorceress currently has29registered handlers; Inferno remains individually deferred. Neither the handler count nor this inventory certifies the class.

## Existing coverage to retain

These are source-inspected fixtures to reuse, not fresh native acceptance results for a Sorceress candidate:

| Existing fixture | Scope already represented | Limits to preserve |
| --- | --- | --- |
| `SorceressSkillChecks` | Frozen/Shiver/Chilling Armor replacement, defense, different retaliation triggers, travelling armor bolt, Energy Shield, Telekinesis, lifecycle and saved armor/shield | Direct release and native damage consumers do not establish ordinary client input for every spell. Saved inspection deadlines are held separately after checking natural cast lifetimes. |
| `EnchantChecks` | Player/ally Enchant, captured damage/AR/duration, independent recipient snapshot and saved state | Distinguish the held inspection record from actual temporary duration; retain caster respec/death and recipient cleanup assertions. |
| `FireGroundChecks`, `MeteorChecks` | Blaze/Fire Wall/Meteor payment and local delays, travelling/ground phases and saved records | Preserve per-skill delay and terminal/field timing. Do not replace emitted effects with a single immediate area hit. |
| `ChargedBoltChecks`, `SorceressLightningChecks`, `ChainLightningChecks` | Wandering paths, count cap, Lightning travel/pierce/sequence and separate chain selection/history | A shared missile kernel or a Mage's fixed secondary does not establish all player spell rank/gear/input behavior. |
| `ThunderStormChecks` | Captured rank/interval/absolute lifetime and resumed periodic strikes | Keep periodic ownership and actual timing distinct from ordinary target casts. Check reloads before natural expiry or assert expired behavior as appropriate. |
| `BlizzardChecks`, `FrozenOrbChecks`, `HydraChecks` | Cast delays, distinct field/parent-child/head actors, collision and saved continuation | Do not merge their hit history, lifetime, target acquisition or damage snapshots by visual similarity. |
| `TeleportChecks`, `TeleportUiChecks` | Shared landing, restrictions, cancellation, scroll/belt conservation, binding and existing UI | Separate actual Sorceress skill mana/input from the consumable service and its own conservation rules. |
| `PlayerInfernoChecks`, `PlayerInfernoSequenceChecks`, `ArcticModeChecks`, `InfernoPrimitiveChecks` | Prepared shared channel/sequence/rate and historical saved flame primitives | None enables learned Inferno. Modern held-input/payment/release composition remains unverified; see the [Inferno preflight](sorceress-inferno-preflight.md). |

The older orchestration in `tests/check_class_skills.py` already lists these commands and their asynchronous result/reload dependencies. Reuse those declarations and the current disposable-server runner instead of building a separate server harness. Inspect each manifest's timing before combining cohorts; an old short run's active-effect expectation may be wrong after a longer run, as [Mage Enchant demonstrated](necromancer-acceptance-checkpoint.md).

## Gaps to cover explicitly

- Inventory the29actual learned routes against their source rows. Add missing player cast/input, mana, cancellation, target, rank/gear and lifecycle acceptance for Fire Bolt, Fire Ball, Ice Bolt, Ice Blast, Glacial Spike, Nova and Frost Nova rather than treating Mage attacks or generic field helpers as that proof.
- Check player Static Field and the passive consumers for Warmth, Fire Mastery, Lightning Mastery and Cold Mastery. NPC fixed-rank Static Field and shared damage enhancement are reuse candidates, not a class certificate.
- Retain the pinned user observations for effective-rank3Charged Bolt3–5, Lightning/Chain Lightning minimum1 and Nova13–37 with their supplied hard synergies. Keep hard versus effective ranks and minimum versus maximum enhancement separate.
- Exercise saved emitted effects and bindings across actual reloads, and distinguish an unpaid transient cast from an independently emitted packet. Verify stop/movement/equipment/recovery/class/facet/disconnect/death behavior against each producer's contract.
- Keep final effects/audio and talent/left/right/quick icons in their separate ledgers, using identified placeholders. No new asset is required by this planning inventory.

`InfernoPrimitiveChecks` still checks the old ordinary `CombatTiming.Calculate` FCR fallback while the learned talent is unavailable. That assertion is **not** channel-rate evidence. The later prepared sequence/rate controls establish the bounded SQ route separately; reconcile the fallback assertion when connecting a learned mode instead of using it to justify FCR-scaled channel cadence.

Finish the current Necromancer run and its audits/checkpoint first. Then start the Sorceress baseline, resolve actual failures, implement unaffected missing mechanics and keep Inferno's individual reference gap explicit. Do not mark either class complete on a handler count or a passing shared component alone.
