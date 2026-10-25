# Class mechanics checkpoint — 25 October 2026

Goal active. Reference: D2R **3.3.93847, Reign of the Warlock**; approved Sanctuary deviations take precedence. Installed baseline: `61ef01c`, 150 handlers (Assassin 11). Latest playable addition: Blade Shield. No class completion certificate.

## Implemented

- Blade Fury has a bounded, unregistered start/do/expiry state component. It retains the 21-frame fresh lease, seven-frame renewal, strict shot deadline, creation attempt versus allocation distinction, explicit payment and animation services, and flag transitions. Generic wrappers, production input, SQ23 animation, damaging projectiles and production persistence remain separate.
- Sentry's existing unit-path/cast/actor/bolt backend remains installed; learned input is disabled. Its retirement branch is individually deferred while unaffected Assassin work continues.

## Verified

- Blade Fury: 6,912 complete older-engine selector/start/do cases plus 12 actual expiry callback cases pass with clean ASan/UBSan. Native matches all cases on three boots/two reloads, with existing Sentry/Fist/Blade regressions. Full regression underway; component JSON round-trip is not production channel persistence.
- Blade Fury resource preparation: 7,200 complete generic wrappers/channel/actual ordinary-player mana-body compositions pass with clean sanitizers. The old rank1 consumer deducts zero despite eligibility requiring one mana; current table rank1 cost remains authoritative. Native wrappers and held-input are not implemented.
- Sentry: 23,040 complete retained-dead-owner missile/resistance/event/HP cases pass with clean ASan/UBSan. They prove HP subtraction while registration and stats are retained, not actual corpse/GUID retention or death-entry cleanup. Native living-actor policy is unchanged.
- Installed `61ef01c`: focused/full suites each passed three boots/two reloads, 70 Python tests, fresh runtime/property audits and existing-world smoke. Saves/config/client preserved; installed server stopped.

## Next and unresolved

Finish native Blade Fury component verification, compose generic resource wrappers, then source SQ23 and animation rewind before connecting player weapon capture, flight, input and lifecycle. Keep unsupported mechanics explicit. Sentry still needs current retirement/ownership, exact movement, spawn fallback and learned input acceptance; these do not block other skills.

Effects/audio and talent/hotkey icons remain separate 240-entry inventories with labelled placeholders. No asset generation requested for this phase. The user's tentative idle-expiry expectation does not establish a timeout.
