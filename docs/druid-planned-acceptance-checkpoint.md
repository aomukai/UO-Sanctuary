# Current Druid combined acceptance

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. **Charms and runewords are excluded.** All 30 Druid handlers exist; no full class or goal certificate is claimed.

## Snapshot/rescaling audit

Current source already implements the approved summon-time policy. `DruidSpiritWolfStats`, `DruidDireWolfStats` and `DruidGrizzlyStats` capture effective birth rank and effective summon synergies; their rescalers reconstruct values from the captured records, not current owner ranks. `SummonUpgradeSnapshots` captures new beasts' hard-point upgrade ranks, and `DruidBeastAttackStats.Values` uses their birth attack rank. Explicit supplement-absent historical actors retain documented compatibility behavior until resummoned; this is not silently presented as a new birth snapshot.

Current Raven births select `sanctuary-summon-snapshot-v1`, capturing attack packet and initial budget before the first think. Only the explicit historical profile rereads owner attack values. Current Poison Creeper records captured hard Rabies synergy and owner level; its difficulty rescaler uses them. Recycler vines and spirits similarly rebuild from recorded rank/level; Oak aura strength reads birth aura rank plus separate live Battle Command. Existing cap, permission and external-buff rules remain separate.

`SummonDifficulties.Sync` captures resources, refreshes difficulty and calls `BattleOrders.Change(... preserveSummonLifeRatio: true)`. Its `Life256` mapping scales the injured percentage, preserves a living fractional point and avoids rounding again on repeated same-difficulty notifications. These source paths support retaining existing public-birth/snapshot/facet checks. Older notes saying the Raven rollout was unfinished predate the current profile; no working mechanic was rewritten on that basis. Native combined validation remains pending.

## Preflight correction

`DruidHumanCastClockChecks.Owner()` trains every talent but its old assertion required Poison Creeper, Carrion Vine, Solar Creeper and Arctic Blast unavailable alongside bear-only Shock Wave. The four learned routes are implemented/approved. Only the fixture now requires those four available, while separately preserving Shock Wave's human-form refusal. The separate vine/held clocks, eighteen ordinary human SC routes, weapon/FCR checks, transactions, cancellation, right/quick input and saved form state remain.

The isolated candidate copies accepted Assassin gameplay C1/Integration I2. Parent receipt/artifacts/data, prechange integration/source and source snapshot 1 preserve that baseline. Source snapshot 2 records the fixture-only rebuild; no gameplay/data/kernel/client/install/save/artwork changes. Integration I1/source 2 builds with zero warnings/errors. The focused human-clock/form/cooldown cohort passes 33 exact results, six actual right/quick inputs, three saves and two reloads; [receipt](../tools/reference/druid-clock-current-native-evidence.json). Installed artifact/save/configuration/client protection passes.

Use cached references; do not inspect the newer D2R process/CASC or rerun unchanged compiler matrices. Poll live build/native handles before C#/runtime/harness edits and preserve exact failed artifacts before repairs. Keep unsupported mechanics/current-engine uncertainties and effects/audio versus icons separately documented. Four older Warlock proposals remain frozen pending; no approval is inferred.


## Current combined plan

The [driver](../tests/check_druid_planned_acceptance.py) declares 57 existing fixture families, 23 shared checks, 43 current input commands, four natural live commands, 146 cached reference files and sixteen pre-save snapshot/expiry commands. Each boot also reuses nine elemental Cast sequences, three approved held-Arctic sequences and an independently connected paid Fury save stage. Current public-birth controls remain distinct from explicitly historical supplement-absent transition controls; neither is silently substituted for the other.

The shared disposable runner gains optional pre-save callbacks, following its existing wire-callback pattern, to invoke Fury's existing stage command on a real connection immediately before saving. It also gains an optional interboot delay, set to the established five seconds here for offline-expiry controls. Both default to inactive for prior cohorts. The previous harness is preserved under `prechange-harness`; no native kernel or production behavior changes. All callbacks, current clocks, mana/life comparisons, original actor state and no-replay checks remain intact. Python connection pacing reuses the established test helper and retains native security.

**Combined native N1 stopped; the declared scope is not a passing result.** Poll its exact handle before C#/runtime/harness edits, and freeze all failed inputs/logs/binaries/C# sources plus the driver/shared harness before a repair. No new reference compiler or D2R process access is required.


Combined N1 stopped because Shock Wave's live control ran before its component family created `shock-wave-fixtures.json`. The live control had already reached its damage/stun/expiry/pool-preservation assertion and then failed reading the manifest. Exact logs, inputs, binaries, C# sources and both the driver/shared runner are frozen under `failed-run-1`.

N2 runs that unchanged connected live control after all fixture families create their records. It retains its damage/stun/expiry/held-pool checks and manifest update; no assertion is removed. There is no C#/runtime/data/build change for this ordering correction. Current scope is 57 families, 23 shared checks, 43 current inputs and four live commands, plus the original connected Shock Wave callback. [Thirty-talent map](druid-coverage-matrix.md). Combined N2 stopped; no accepted class result is claimed.


Combined N2 next stopped in Rabies' version-0 compatibility control. `MissingMarker()` selected `legacy.Infections[0]` from the whole shared version-1 pool, rather than the new controlled marker's emitter. Earlier learned/infection families retained unrelated entries, so that positional assumption did not describe its intended migration payload. Exact N2 logs, inputs, binaries, C# sources and both Python harnesses are frozen under `failed-run-2`; the accepted clock stage points at preserved I1 binaries.

Only `DruidRabiesLifecycleChecks.cs` changes for this correction. Its version-0 payload now explicitly selects the controlled marker/emitter and caster seed, with no unrelated version-1 children. `Single()` requires exactly that emitter. Existing stopped-emitter, marker/rank/expiry/seed preservation, duplicate rejection, missing-marker tail and expiry assertions remain. The surrounding original pool is restored and now explicitly compared after the control. Gameplay C1, migration/poison mechanics and data are unchanged. I2/source 3 builds with zero warnings/errors; focused learned/infection/lifecycle/save validation precedes combined N3.


The first focused migration rerun passed its version-0 checks, then exposed whole-world count assumptions in the lifecycle scenario. The second passed that scenario and exposed whole-world equality in the learned Rabies pre-save control. Both failed focuses retain exact inputs/logs/binaries/C# sources and drivers under `failed-rabies-focus-1` and `failed-rabies-focus-2`. These are fixture composition failures, not passing acceptance results.

The three Rabies fixtures now compare every owned marker, emitter, contagion path and RNG seed independently, allowing other controlled casters to coexist. Global ID monotonicity remains checked. Before/after reload ticks additionally require unchanged global allocation, seeds, markers, children and unrelated emitters as appropriate; the lifecycle still requires exactly one old emitter to retire. The version-0 control still restores and verifies the entire surrounding pool. All poison, payment, life, bindings, migration, deadline, no-replay and expiry assertions remain. This extends the fixture-only correction to `DruidRabiesChecks.cs` and `DruidRabiesInfectionChecks.cs`; production code/data remain unchanged. I4/source 5 is the next focused candidate; combined N3 has not run.


Further focused reload controls are preserved as `failed-rabies-focus-3` through `-5`. An added strict packet comparison initially rejected legitimate loaded-child clock ageing; diagnostics then identified the exact remaining difference as the existing offline facet/logout fault when a saved player body loads on UO's Internal map. Position, damage, RNG, paths and unrelated children were unchanged. The fixture now explicitly checks that fault and allows only owned age/deadline rescheduling. It retains full packet comparison after normalizing those allowed fields. I7/source 8 is the next candidate; no production code/data changed.


Focused run 6 passed the first reload but caught an added assertion incorrectly expecting the old emitter to retire again on the second reload. The count assertion now requires that single retirement only at reload zero, then retains the two surviving emitters. Failed focus 6 is frozen with exact artifact/source hashes. The Codex loop filesystem filled during archiving; failed focus archives 1–6 now reside on FILES with their original paths as symlinks, with all files hash-verified before removal of duplicate local copies. Installed runtime/saves/configuration remain untouched. The failed empty source-snapshot 9 is preserved as `source-snapshot-9-write-failed.json`; the next complete snapshot is 10 (I8).


## Verified current checkpoint

**I8/source 10/focused run 7 passes 27 exact results, three actual saves and two reloads**, with zero build warnings/errors. [Receipt](../tools/reference/druid-rabies-composition-native-evidence.json) and [strict recorder](../tools/record_druid_rabies_composition.py) bind the four fixture changes, unchanged gameplay C1/data/kernel and all six failed focused stages. The receipt and recheck verify 106 installed artifacts, 38 saves, 20 configuration files and 44 client patches unchanged. Human-clock focus remains the earlier separate 33-result proof. No production deployment, reference process/CASC access, repeated reference compilers or artwork generation occurred.

Next is combined N3 on the same C1/I8/source 10: 57 families, 23 shared checks, 43 current input commands and the retained live/legacy/held/save stages. Its expected pass inventory has 675 markers; this is a declared expectation until the actual run succeeds. No combined Druid acceptance or class certificate is claimed. Charms and runewords remain excluded.
