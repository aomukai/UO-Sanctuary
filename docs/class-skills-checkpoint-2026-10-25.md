# Class mechanics checkpoint — 25 October 2026

Goal active. Reference: **D2R3.3.93847 — Reign of the Warlock**; approved Sanctuary deviations take precedence. Installed150 handlers: Necromancer28, Sorceress29, Paladin30, Barbarian25, Amazon27, Assassin11; Druid/Warlock0. Blade Shield remains latest playable. No class completion certificate.

Current candidate: **Blade Fury player missiles**. Dedicated weapon/skill capture and saved size1 bladefragment1 flights pass focused three boots/two reloads:960 source creator cases, actual weapon/critical/poison/no-ammo/no-wear, travelling impact, captured slow, death/respec and immutable saved-flight continuation. Natural server-timer hits pass each boot. Current mastery-AR/slow rounding and full input/event/client acceptance remain scoped. Full regression/install pending; installed03eb611 is unchanged;150 handlers remain. [Evidence](../tools/reference/blade-fury-missile-native-evidence.json).

## Implemented

- Blade Fury bounded start/do/expiry component, ordinary-player payment wrappers and SQ23 event planning. Native planner retains initial/renewal epochs and scheduled events; it does not register production input/timers or persist an active cast.
- Actual player adapter uses existing fractional-mana storage/payment and current invocation rank. It retains fresh six-mana availability, per-shot cost, same-frame shot deadline and failed-factory attempt semantics. Current rank1 costs256FP; the old consumer's zero debit is not adopted.
- Existing Sentry actor/path/cast/bolt backend remains installed with learned input disabled. Current corpse/GUID retirement is an individual nonblocking deferral; its living-actor gate is unchanged.

## Verified

- Focused/full suites each pass three boots/two world reloads. Per boot:6912 channel+12 expiry,7200 complete generic/channel/resource and490 initial/renewal source comparisons. Clean content/integration builds;70 Python tests; fresh545/21 audits and573 unchanged named decisions.
- Actual character with Tarnhelm pays256FP at rank1 and288FP at rank2, returns to rank1 after removal, preserves fractional mana and pays for a failed factory attempt. Character/equipment and607FP mana (95FP remainder) persist through two actual world reloads. Component snapshots in the fixture manifest are test data, not production channel persistence.
- Projectile preparation:960 complete player creator/physical/typed source cases pass with clean ASan/UBSan, reusing the existing Blade Sentinel/Bow creator ABI. Skill physical remains separate from96/48 weapon scaling; actual native collector/AR/flight remains pending.
- Rate preparation:24 complete older ordinary SQ initialization/rate/router/scheduler cases pass with clean ASan/UBSan. With pinned A1/useAttackRate0, sequence speed remains256 and the source scheduler retains action12/end19 independently of declared animation metadata/other-rate values. Production mode entry/input/current executable timing remain separate.
- Official released2.4 notes corroborate10percent AR per rank, matching the current pinned formula. The absent old-engine injection is not a reason to omit this modern bonus. Primary reference and older guide conflicts are recorded in `tools/reference/blade-fury-primary-notes.json`.
- Sentry:23040 complete retained-dead-owner missile/resistance/event/HP source cases pass, but actual retention/death-entry cleanup remains unproven.
- The first full regression failed a Sanctuary knockback reload. The test now restores stopped ordinary AI for the whole named scene on WorldLoad, matching its creation setup. Combined focused and repeated full suites pass the unchanged endpoint/recovery assertions; production movement is unchanged. The failed run and new endpoint diagnostics are retained.
- Matching installed content loads the existing world and shuts down cleanly. Previous9c93a9a content/data/saves/client are backed up in `.local/class-skills/pre-blade-fury-resource-install`; save/config/client bytes unchanged; server stopped.

## Next and unresolved

Build a dedicated Blade Fury player collector and saved missile path using the960-case complete creator evidence, shared BowLaunch.FromSource/weapon impact consumers and documented current10percent-per-rank AR. Bind mode entry/rate, seqinput12/held input, state-expiry/event dispatch and cancellation with the installed payment/animation components. Keep the optional release observation nonblocking and learned input disabled until actual combat/input/lifecycle acceptance passes.

The optional D2R Blade Fury press/release observation is pending and nonblocking. Current exact warmup/cadence, input/state-expiry ordering, live weapon/AR/property/ownership/slow/credit and actual saved flight remain acceptance work. Sentry retirement/movement/spawn/input questions do not block unaffected skills.

Effects/audio and talent/hotkey icons remain separate240-entry inventories with labelled placeholders. No image generation in this phase.
