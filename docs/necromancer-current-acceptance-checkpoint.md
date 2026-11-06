# Current Necromancer combined acceptance

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary changes taking precedence. This checkpoint consolidates existing mechanics and the new army limits; it adds no production mechanic or asset identity. **197/240 handlers, all 30 Necromancer handlers, zero completed classes.**

## Build and run status

The production Content4 assembly is byte-identical to the accepted NPC lightning-return checkpoint. Production C#, runtime data, project inputs and native kernel are unchanged. Integration7 compiles with zero warnings/errors; source snapshot7 binds its inputs. The current 115-family driver adds the army-limit and original Revive activation families to the older combined cohort, retaining its 21 shared checks and special barrier/Fire save coordinator.

Diagnostic run5 passes **473 markers across three boots, two actual reloads and three saves**. Full actual-input run1 is in progress; its expected854markers are still a plan. The final current receipt must validate its complete log, exact markers, all three boots/two reloads/three saves and the same frozen binaries before reporting full acceptance.

The [30-talent map](necromancer-coverage-matrix.md) distinguishes this combined cohort from newer dedicated scopes. Earlier receipts retain their original assembly identities; this run does not retroactively make them current.

## Fixture corrections

The new caps deliberately change legal roster sizes. Four old fixture assumptions needed correction; production caps were not relaxed to make tests pass.

1. **Parking:** the old fixture trained Revive rank2 but retained two original Revives. The approved limit at rank2 is one. It now trains rank10 and checks both original actors still exist before movement. The earlier focused parking/army run passed21markers through two reloads on its recorded Integration3; the final combined run must recheck this on the final assembly.
2. **Mage element migration:** eight saved Mages under one rank20 owner exceed both the rank20 limit5 and the hard limit7. All eight legacy/prepared element cases remain, split between two rank20 owners with four Mages each. Saved ownership and FIFO checks are owner-local. Original wound extrema, captured stats, seeds, clocks and held old poison/new ice packet assertions remain.
3. **Iron cast refusal:** Goblin Toe's Crushing Blow consumer is now supported. The refusal fixture now uses unmodified Howltusk and requires an unsupported-consumer reason before windup, retaining original item, old golem and mana assertions. Positive Crushing Blow coverage remains in its existing consumers.
4. **Summon Resist:** three captured Mage recipients were saved at Raise Skeletal Mage rank1. The fixture now trains rank3 for the legal three-Mage roster. All19 recipient cases, resistance snapshots, item equip/removal controls and exact wound reload assertions remain.

Failed native runs1–4, their assemblies, inputs, source snapshots and relevant fixture sources are preserved under `necromancer-current-mechanics/failed-mechanics-*`. The test-only CS0136 build4 failure is preserved separately under `necromancer-current-acceptance/failed-build-4`; a local variable rename corrected it. Integration6's earlier audit is preserved under `prior-build-6` before the final fixture rebuild. Do not count these failed or superseded runs as final acceptance.

## Map correction

Runtime parking measurements establish **Trammel and Felucca are7168×4096** in this build. The earlier lightning fixture's6140corridor failure was incorrectly attributed to a presumed6144-wide map. That explanation is withdrawn in its checkpoint, and an explicit correction is stored alongside the original diagnostic. The exact6140placement/terrain failure remains unresolved. Preserve the original failure rather than rewriting it into evidence for a boundary it did not test.

## Remaining work

After the final mechanics diagnostic, run actual connected actions and right/quick inputs on the same binaries. Refresh runtime item audits and checks, verify installed artifacts/saves/configuration/client hashes, issue the new current receipt, then commit/push the checkpoint.

Continue the individual mechanical gaps in the status ledger afterward. Exact Bone Wall/Prison death scheduling, footprint and owner graph remain explicitly unresolved; inaccessible current initializer pages and the older null stub do not establish those mechanics. Wider native AI/species/item/event/PvP and current-executable parity are bounded separately. Final graphical-client testing, [effects/audio](skill-effects.md) and [icons](skill-icons.md) remain separate; no new asset is needed by these fixture corrections.
