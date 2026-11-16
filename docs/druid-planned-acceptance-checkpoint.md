# Current Druid combined acceptance

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. **Charms and runewords are excluded.** All 30 Druid handlers exist. No combined class acceptance, full class certificate or goal completion is claimed yet.

## Verified focused checkpoints

| Scope | Candidate | Actual results |
| --- | --- | --- |
| Human SC/form clock | C1/I1/source 2 | 33 exact markers, six actual right/quick inputs, three saves, two reloads |
| Shared Rabies learned/infection/lifecycle | C1/I8/source 10 | 27 exact markers, three saves, two reloads |
| Shared Arctic potion/stream/held input | C1/I12/source 14 | 39 exact markers, nine held wire sequences, three natural stream controls, 18 connections, three saves, two reloads |
| Exact Firestorm reload before new clock casts | Unchanged C1/I12/source 14 | 21 exact markers, three actual input casts, three saves, two reloads; identical-input original-order negative retained |

[Clock receipt](../tools/reference/druid-clock-current-native-evidence.json), [Rabies receipt](../tools/reference/druid-rabies-composition-native-evidence.json), [Arctic receipt](../tools/reference/druid-arctic-composition-native-evidence.json). Current integration build I12 has zero warnings/errors. Gameplay C1, runtime data, native kernel and client remain unchanged. Installed protection verifies 106 artifacts, 38 saves, 20 configuration files and 44 client patches. No deployment, current D2R process/CASC access, new reference compiler run or artwork generation occurred.

## Snapshot and rescaling audit

Current Spirit/Dire Wolf and Grizzly births capture effective summon ranks and effective summon synergies. Their rescalers reconstruct from recorded values. `SummonUpgradeSnapshots` captures hard upgrade ranks; `DruidBeastAttackStats.Values` reads the captured birth attack rank. Supplement-absent historical actors retain explicit compatibility until resummoned.

Current Raven births use `sanctuary-summon-snapshot-v1`, capturing damage and attack budget. Only the explicit historical profile rereads owner attack values. Poison Creeper captures hard Rabies and owner level. Recycler vines and spirits likewise retain birth rank/level; Oak's separate live Battle Command modifier remains distinct. Older notes saying Raven rollout was unfinished predate that profile.

`SummonDifficulties.Sync` captures resources, refreshes difficulty and invokes `BattleOrders.Change(... preserveSummonLifeRatio: true)`. The fractional-life mapping retains an injured percentage and a living fraction without rerounding repeated same-difficulty notifications. Existing cap, permission, parking and external-buff rules remain separate.

## Fixture corrections and preserved failures

The accepted Assassin build is the unchanged gameplay baseline; `artifact-reuse.json` binds its binaries and data. Source snapshot 1 and prechange integration/sources preserve the initial Druid candidate.

- Human-clock availability: its owner trains all talents; four implemented vine/Arctic routes were incorrectly expected unavailable. They are now required available. Human Shock Wave refusal, eighteen ordinary SC routes, separate clocks, weapon/FCR, transactions, cancellation and input/save assertions remain.
- Combined N1: Shock Wave's live control ran before its component manifest existed. The unchanged connected control now follows family creation. Its damage/stun/expiry and pool assertions remain.
- Combined N2 and Rabies focused failures 1–6: the version-0 control selected an unrelated positional emitter; later controls assumed exclusive ownership of global counts/state. The three fixtures now compare every owned marker/emitter/path/seed and preserve global monotonicity, no-replay and unrelated-state checks. Migration restores and verifies the whole surrounding pool. Loaded children may age/reschedule and record the existing Internal-map/logout fault without movement, damage or RNG replay. The old emitter retires only on the first reload; the second retains its survivors.
- Combined N3 and Arctic focused failures 1–4: the projectile pool matched, but the retained target had taken a small cold hit before saving. Reload ordering did not resolve it. The held control advanced the global projectile clock at a synthetic future epoch and then restored packets without undoing foreign damage. It now uses a temporary empty missile set with the original allocation counter and restores/compares the entire surrounding pool afterward. All threshold, fractional payment, chunking, gear, poison-potion, release, actual-damage and no-replay assertions remain. New target-life guards observe the save stage, world save and reload controls. [Focused detail](druid-arctic-composition-checkpoint.md).

Exact failed inputs/logs, binaries, every C# fixture and both Python harnesses are preserved under `failed-run-1` through `-4`, `failed-rabies-focus-1` through `-6` and `failed-arctic-focus-1` through `-4`. The Codex filesystem filled while preserving evidence. Archives now live under `/media/aomukai/FILES/Sanctuary-checkpoint-archives/revive-respec/druid-planned-acceptance`, with verified regular copies and original local paths retained as symlinks. FILES does not support internal symlinks; no evidence depends on that unsupported operation. Source snapshot 9's failed empty write is preserved separately; complete later snapshots use fresh numbers. Accepted focused stages point at preserved binaries before any subsequent rebuild.

Only six C# fixture files changed: `DruidHumanCastClockChecks`, the three `DruidRabies*Checks`, `ArcticStreamChecks` and `ArcticHeldInputChecks`. No gameplay fix is inferred from these fixture failures.

## Combined run next

The [driver](../tests/check_druid_planned_acceptance.py) declares 57 existing families, 23 shared checks, 43 current input commands, four natural live commands, 146 cached reference files and sixteen pre-save snapshot/expiry controls. Each boot retains nine elemental Cast sequences, three held-Arctic sequences and an independently connected paid Fury save stage. Socket pacing preserves native security. Optional pre-save callbacks and interboot delay default inactive for older cohorts.

**Combined N5 is next on unchanged C1/I12/source 14.** The [strict recorder](../tools/record_druid_planned_acceptance.py) expects 675 exact markers and 201 connections across three saves/two reloads; those are expectations until the actual run succeeds. [Thirty-talent map](druid-coverage-matrix.md). Poll its exact handle before C#/runtime/harness edits and freeze failed evidence before repairs.

Explicit current-engine timing/RNG/event/geometry/recipient/owner/PvP and exceptional world cases, graphical-client acceptance and effects/audio versus talent/hotkey icons remain separate. Four older Warlock proposals stay frozen pending, without inferred approval or a repeated question.


## Firestorm reload ordering

Combined N4 passed Arctic's exact target-wound save/reload controls, then stopped at Firestorm's full saved-pool equality. The human-clock check deliberately releases real Firestorm casts, cleans its temporary emitters and advances the global allocation counter. Running it before the exact reload assertion changes that assertion's precondition. [Order receipt](../tools/reference/druid-firestorm-order-native-evidence.json).

An unchanged-binary control reproduces the old-order failure. Moving Firestorm's exact reload assertion ahead of new clock casts passes the first reload. The separate order-only control also establishes that normal later input checkpointing must record subsequent legitimate allocations before the next save. The final paired control retains that actual input: 21 exact positives, three input casts and two reloads; its original-order negative uses identical recorded source/data/binary inputs. The combined driver now uses the existing early-reload hook for Firestorm and retains all nine later elemental input/checkpoint sequences. No C#, build, gameplay, assertions or native security changed for this ordering correction. Historical accepted receipts remain unchanged; their old driver hash is authenticated against frozen N4 sources, while N5 binds the new driver directly.
