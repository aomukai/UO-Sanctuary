# NPC item-triggered Terror

Mechanical reference: **D2R Reign of the Warlock 3.3.93847**. This increment addresses Iron Golem/original Revive item stat 112, `item_howl`, callback 08. It enables the existing item property through the shared Terror controller; it adds no talent, hotkey or icon identity. Class handler coverage remains197/240 and no class-completion claim is made.

## Source contract

The [source receipt](../tools/reference/npc-item-fear-source-evidence.json) binds 5,376 callback and 1,280 factory/helper compositions. Three complete unchanged older routines execute under ASan/UBSan: callback 08, `AIUTIL_ApplyTerrorCurseState` and the full curse factory. The inherited blind probe supplies the full factory environment and independently rebound constants. New monster-type, skill and stat constants are read from their pinned headers. This is bounded supplementary source evidence, with declared AI-eligibility and initializer services; it does not certify the current executable or native path/room scheduler.

The callback rejects unique/champion targets before RNG, then reads the actual NPC's item chance and consumes one raw actor draw. Chance is `(raw & 127) < bonus`, without a ranged, level or game-difficulty adjustment. Its positive return does not depend on the helper succeeding. The helper supplies **Terror skill/rank 1 and 20 frames**; the passed Howl/20 arguments do not select a learned Howl rank or radius. Shared curse resistance can shorten or reject the list. Curse arbitration retains equal-rank creators, rejects weaker lists and protects Attract.

AI initialization is separate from that list result: after the eligibility gate the source sets the attacking unit as target, initializes Terror AI and requests one idle frame even if the factory declines. A stronger existing Terror keeps its list but follows the new initializer. A protected Attract remains in the curse lane; the transient Terror initializer returns to ordinary AI when it finds no Terror state.

## Runtime integration

`NpcItemFear` owns the callback chance and delegates to `SourceTerror.ApplyItem`. `NpcCombatEvents`, the live proc graph and frozen Revive equipment share stat 112 admission and the same receiver. Legacy and graph ownership remain mutually exclusive. The callback neither recasts a spell nor adds a damage packet or owner mana payment.

The optional saved `NecromancerState.ItemTerror` payload separates item AI from curse ownership. It reuses `TerrorAi` and the existing target, movement, blocked-path, attack and expiry consumers. A later special-AI initializer supersedes it. Learned Terror captures the active item's dynamic target before replacing that initializer. Older JSON omitting this field remains readable. Invalid new item-AI metadata is rejected.

The existing curse duration and known elite-curse replacement services are reused. Unknown historical elite metadata is explicitly logged/refused rather than assigned an invented rank. Unmapped AI/species, native bard/owned-recipient contexts and the existing broader world/geometry/event/PvP boundaries remain explicit. Howltusk still has separate unsupported knockback/damage-to-mana callbacks; adding fear does not silently strip those properties or admit the whole item.

## Execution checkpoint

**Accepted: Content build 5 / Integration build 8 / source snapshot 8. Focused native run 5 passes 15 checks; retained native run 3 passes 149 checks across 23 families. Each completes three boots, two reloads and three saves on the same frozen binaries.** The retained run preserves all 78 parent checks and adds nine real curse input actions. Six natural item-fear sessions pass across the two cohorts. The [native receipt](../tools/reference/npc-item-fear-native-evidence.json) binds the exact marker sets, sources, binaries and preserved diagnostics.

The focused suite uses unmodified **The Face of Horror** through actual learned Iron casting and original Revive activation. It checks chance, fixed duration, arbitration/Attract, learned AI replacement, legacy/graph ordered damage and RNG, natural movement/expiry, original gear/corpse ownership, wounds and two reloads. The original saved fear phase advances after an official pet resume, then the fixture restores its held effect/deadline for the next comparison.

Preserved diagnostics:

- Focused native 1 failed because the frozen Revive bonus reader still rejected `howl` after casting admitted stat 112. The exact typed/raw mapping and reader were added. Its sources, snapshot 3, inputs, logs and binaries remain in `npc-item-fear/failed-native-1`.
- Retained native 1 passed fear/freeze/lightning on its first reload, then an unauthenticated freeze fixture session caused the next connection to be auto-denied. The fixture now authenticates its saved owner; production security and all original assertions remain unchanged. Its artifacts remain in `npc-item-fear-retained/failed-retained-1`.
- Retained native 2 failed the ordinary Attract right-click autonomous damage/target/RNG assertion. That original message lacked lane values, so its exact cause remains untraced. The assertions and deadlines remain; the failure message now records the individual values. An isolated historical Content4/Integration6 run passed 21 checks with all six Attract input actions across three boots, but does not retroactively explain the combined failure or certify later binaries. The original failure remains in `failed-retained-2`. Final retained run 3 passes all nine current curse input actions with the original assertions and deadlines, including the richer failure message; this does not identify the earlier failure’s cause.
- Focused native 4 deliberately exposed the separate item-specific neighbor-lease bug: after first escape, a later mode2 target lease cleared item AI and became mode0/focus0. Active item AI now takes precedence over its shadowed curse payload, and mode2 leases are not treated as new AI initializers. The new regression passes in focused native 5. The counterexample and exact source/binaries remain in `npc-item-fear/failed-native-4`.

Earlier compile-only outputs remain preserved before the target-handoff/shared-factory changes. Fresh audits preserve 573 item decisions with 1088/21 input fingerprints. All 71 Python tests and catalogue/readiness/roadmap checks pass; the source proof also passes its regeneration check. The receipt verifies unchanged installed 106 artifacts, 38 saves, 20 configuration files and 40 client patches. The broader retained suite includes blind/fear/Confuse/Attract, freeze/lightning callbacks, original Revive/contact/resource consumers and real curse inputs.

## Assets and next work

The visible marker reuses the existing identified fear placeholder `0x375A`, hue`0x21`. [Effects/audio](skill-effects.md) retain its final-art replacement; [icons](skill-icons.md) retain the existing talent/hotkey ledger. No new asset identity is introduced by an item callback.

The component is accepted within the recorded scope. Continue the class roadmap’s missing skill routes while retaining Necromancer’s flagged parity gaps. Do not expand this accepted increment into unrelated equipment callbacks before continuing those routes. Exact current-engine AI switches/class callbacks, global event timing, target acquisition/path geometry and graphical-client behavior remain separate; do not promote this increment into a complete class certificate.
