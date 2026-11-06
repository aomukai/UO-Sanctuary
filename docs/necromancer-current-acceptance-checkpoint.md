# Current Necromancer combined acceptance

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary changes taking precedence. This checkpoint consolidates existing mechanics and the new army limits; it adds no production mechanic or asset identity. **197/240 handlers, all 30 Necromancer handlers, zero completed classes.**

## Build and run status

The production Content build 4 assembly is byte-identical to the accepted NPC lightning-return checkpoint. Production C#, runtime data, project inputs and native kernel are unchanged. Integration build 7 compiles with zero warnings/errors; source snapshot 7 binds its inputs. The current 115-family driver adds the army-limit and original Revive activation families to the older combined cohort, retaining its 21 shared checks and special barrier/Fire save coordinator.

**Diagnostic run 5 passes 473 markers; full actual-input run 1 passes 854 markers. Each completes three boots, two actual reloads and three saves on the same frozen binaries.** The full run retains every diagnostic marker and adds 105 live actions plus 138 right-click/hotkey actions. All 115 families and 21 shared checks pass. The [current native receipt](../tools/reference/necromancer-current-acceptance-native-evidence.json) binds both inventories, exact marker sets and preserved diagnostic sources/binaries. Fresh 1087/21 runtime audit fingerprints retain 573 item decisions; 71 Python tests and catalogue/readiness/roadmap checks pass. Installed 106 artifacts, 38 saves, 20 configuration files and 40 client patches remain unchanged.

The [30-talent map](necromancer-coverage-matrix.md) distinguishes this combined cohort from newer dedicated scopes. Earlier receipts retain their original assembly identities; this run does not retroactively make them current.

## Fixture corrections

The new caps deliberately change legal roster sizes. Four old fixture assumptions needed correction; production caps were not relaxed to make tests pass.

1. **Parking:** the old fixture trained Revive rank 2 but retained two original Revives. The approved limit at rank 2 is one. It now trains rank 10 and checks both original actors still exist before movement. The earlier focused parking/army run passed 21 markers through two reloads on its recorded Integration build 3; both final cohorts also pass this scope on Integration build 7.
2. **Mage element migration:** eight saved Mages under one rank 20 owner exceed both the rank 20 limit 5 and the hard limit 7. All eight legacy/prepared element cases remain, split between two rank 20 owners with four Mages each. Saved ownership and FIFO checks are owner-local. Original wound extrema, captured stats, seeds, clocks and held old poison/new ice packet assertions remain.
3. **Iron cast refusal:** Goblin Toe's Crushing Blow consumer is now supported. The refusal fixture now uses unmodified Howltusk and requires an unsupported-consumer reason before windup, retaining original item, old golem and mana assertions. Positive Crushing Blow coverage remains in its existing consumers.
4. **Summon Resist:** three captured Mage recipients were saved at Raise Skeletal Mage rank 1. The fixture now trains rank 3 for the legal three-Mage roster. All 19 recipient cases, resistance snapshots, item equip/removal controls and exact wound reload assertions remain.

Failed native runs 1–4, their assemblies, inputs, source snapshots and relevant fixture sources are preserved under `necromancer-current-mechanics/failed-mechanics-*`. The test-only CS0136 build 4 failure is preserved separately under `necromancer-current-acceptance/failed-build-4`; a local variable rename corrected it. Integration build 6's earlier audit is preserved under `prior-build-6` before the final fixture rebuild. Do not count these failed or superseded runs as final acceptance.

## Map correction

Runtime parking measurements establish **Trammel and Felucca are 7168×4096** in this build. The earlier lightning fixture's 6140 corridor failure was incorrectly attributed to a presumed 6144-wide map. That explanation is withdrawn in its checkpoint, and an explicit correction is stored alongside the original diagnostic. The exact 6140 placement/terrain failure remains unresolved. Preserve the original failure rather than rewriting it into evidence for a boundary it did not test.

## Remaining work

The combined cohort is accepted within its recorded scope. Continue the class roadmap using the 30-talent map to select an actual remaining mechanical gap; do not rerun this accepted subset or treat the older 113-family receipt as a current build. The current receipt deliberately preserves the narrower binary identities of newer dedicated checkpoints.

Continue the individual mechanical gaps in the status ledger afterward. Exact Bone Wall/Prison death scheduling, footprint and owner graph remain explicitly unresolved; inaccessible current initializer pages and the older null stub do not establish those mechanics. Wider native AI/species/item/event/PvP and current-executable parity are bounded separately. Final graphical-client testing, [effects/audio](skill-effects.md) and [icons](skill-icons.md) remain separate; no new asset is needed by these fixture corrections.
