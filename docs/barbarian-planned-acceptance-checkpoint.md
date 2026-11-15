# Current Barbarian combined acceptance

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. **Charms and runewords are excluded.** All 30 Barbarian handlers exist; this checkpoint does not certify full class completion or exact modern D2R parity.

Reused gameplay C1 with Integration I3/source snapshot 4/native run 2 passed **245 exact results**, across **25 fixture families, nine shared checks, 39 spell inputs, three checked natural Find Potion actions and six corpse-snapshot callbacks**. The disposable world completed 60 connections, three boots/saves and two reloads. No save-deletion or unsupported-context warnings were recorded in this cohort. [Receipt](../tools/reference/barbarian-planned-acceptance-native-evidence.json), [coverage map](barbarian-coverage-matrix.md).

The [driver](../tests/check_barbarian_planned_acceptance.py) retains the earlier 24-family class cohort and saved corpse snapshots, adding the existing selected-weapon-hand family, right/quick SC/FCR input checks and natural Find Potion release. The latter uses the actual timer and verifies an already-paid cast after mana is removed. All 20 supplied references are cached; no source-reference compiler rerun or running D2R client was required. The accepted Paladin gameplay assembly and runtime data are unchanged. The candidate has its own integration build; accepted parent binaries remain intact.

## Fixture correction and retained evidence

Combined native run 1 stopped at a stale War Cry Champion-stun assertion. Its exact input inventory, binaries, logs and captured C# sources are retained under `barbarian-planned-acceptance/failed-run-1`. The cached `warcry-engine-evidence.json` binds the reviewed `SUnitDmg.cpp` SHA-256 `56ab5e14f7aceb18681f1c1e720e89c63bfe2a25e2e41b8aa9907652a7c9daf7`: the shared path tests Unique bit 8 rather than Champion bit 4. This agrees with the already-qualified shared stun implementation; Smite's separate Champion rule is unchanged.

Only `WarCryChecks.cs` changed. An eligible Champion must stun without drawing Unique RNG. A native Unique is also classified as a boss by the existing encounter profile: it draws the Unique roll once but remains unstunnable even at roll 90. Separate supplied-metadata checks retain the nonboss Unique 89/90 boundary. These assertions characterize the existing mapping without changing it or claiming full modern equivalence.

Two focused attempts exposed fixture setup assumptions: encounter identity cannot be reassigned after creation, and native Unique leaders have boss immunity. Both attempts are frozen with exact logs, inputs, binaries and captured C# sources. Separate fresh actors and their actual metadata now replace those assumptions. The final integration build passed with zero warnings/errors; focused native run 3 passed 15 exact results across three boots/saves and two reloads before combined run 2. [Focused receipt](../tools/reference/barbarian-warcry-current-native-evidence.json).

## Approved behavior retained

The exact approved Double Swing, Frenzy, Grim Ward context and Leap Attack landing decisions remain authoritative. Find Potion uses the corpse monster's level: 1–19 light, 20–39 regular, 40–59 greater, 60+ super; full rejuvenation starts at 40. No Act tags or default are introduced. The existing Find Item/Find Potion snapshot callback, transport shutdown/settling safeguards and original save fixtures remain.

## Remaining work

Bash's post-allocation damage-total question, Find Item chance/special-treasure contexts, native Unique boss stun mapping, exceptional hand/landing/recipient cases, exact modern timing/event/RNG/world equivalence and graphical-client behavior remain explicit. A passing combined cohort does not remove those limits. The broader empty-swing/targeting melee convenience pass remains deferred. Final [effects/audio](skill-effects.md) and [talent/hotkey icons](skill-icons.md) remain separately tracked with identified placeholders. Historical wardrobe save-pruning concerns remain separate even though this run emitted none.

Protection checks confirm 106 installed artifacts, 38 saves, 20 configurations and 44 client patches remain unchanged. There was no installation, client patch, save reset or artwork change. Continue next with Amazon's existing mechanics and current scoped gaps; keep unsupported mechanics explicit and avoid rerunning unchanged source matrices.

Review with `python3 tests/check_barbarian_planned_acceptance.py --plan`. The [recorder](../tools/record_barbarian_planned_acceptance.py) derives the exact expected marker multiset from declared commands, verifies captured source/data/artifacts and writes the receipt only after a successful terminal run.
