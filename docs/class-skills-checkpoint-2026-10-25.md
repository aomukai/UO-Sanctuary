# Class mechanics checkpoint — 25 October 2026

Goal active. Reference: D2R **3.3.93847, Reign of the Warlock**; approved Sanctuary deviations take precedence. Installed build: bounded Blade Fury channel component, 150 handlers (Assassin 11); previous build `61ef01c` backed up in `.local/class-skills/pre-blade-fury-channel-install`. Latest playable addition: Blade Shield. No class completion certificate.

Current candidate: **Blade Fury resources and animation planning**. Native ordinary-player wrappers and SQ23 profile match7,200 complete source resource and490 initial/renewal controls on focused three boots/two reloads. Real character payments follow live effective rank, preserve fractional mana and charge failed factory attempts. Tarnhelm removal and607FP saved mana (95FP remainder) survive two actual world reloads. Full regression underway; installed9c93a9a remains unchanged. [Candidate evidence](../tools/reference/blade-fury-resource-native-evidence.json). Further projectile preparation:960 complete player creator/physical/typed source controls pass with clean sanitizers, reusing the existing Blade Sentinel/Bow creator contract. Released official2.4 notes corroborate the current10percent-per-rank AR bonus; its absence in the old engine is not a reason to omit it. Actual native weapon/AR/flight integration remains pending.

Regression note: the first full run stopped on Sanctuary's saved knockback endpoint. Its fixture had stopped ordinary AI at creation but only stopped the motion target after reload; the other scene actors could wander. Named whole-scene AI restoration now passes the combined focused three-boot/two-reload suite, with endpoint/recovery assertions unchanged and new terrain/occupancy diagnostics. A repeated full run is underway; the working installation is unchanged. The failed logs are retained in `.local/class-skills/blade-fury-resource-sanctuary-reload-failure*.log`.

## Implemented

- Blade Fury has a bounded, unregistered start/do/expiry state component. It retains the 21-frame fresh lease, seven-frame renewal, strict shot deadline, creation attempt versus allocation distinction, explicit payment and animation services, and flag transitions. Generic wrappers, production input, SQ23 animation, damaging projectiles and production persistence remain separate.
- Sentry's existing unit-path/cast/actor/bolt backend remains installed; learned input is disabled. Its retirement branch is individually deferred while unaffected Assassin work continues.

## Verified

- Blade Fury: 6,912 complete older-engine selector/start/do cases plus 12 actual expiry callback cases pass with clean ASan/UBSan. Native matches all cases on three boots/two reloads, with existing Sentry/Fist/Blade regressions. Full regression also passes three boots/two reloads; component JSON round-trip is not production channel persistence.
- Blade Fury resource preparation: 7,200 complete generic wrappers/channel/actual ordinary-player mana-body compositions pass with clean sanitizers. The old rank1 consumer deducts zero despite eligibility requiring one mana; current table rank1 cost remains authoritative. Native wrappers and held-input are not implemented.
- Blade Fury timing preparation: 14 complete initial scheduler and476 complete source SQ23 event/rewind cases pass with clean sanitizers. Renewal rewrites future event deadlines; native held-input/event scheduling remains pending.
- Sentry: 23,040 complete retained-dead-owner missile/resistance/event/HP cases pass with clean ASan/UBSan. They prove HP subtraction while registration and stats are retained, not actual corpse/GUID retention or death-entry cleanup. Native living-actor policy is unchanged.
- Matching installed component build: focused/full suites each passed three boots/two reloads, 70 Python tests, fresh runtime/property audits and existing-world smoke. Saves/config/client preserved; installed server stopped.

## Next and unresolved

Port the complete generic Blade Fury start/do/payment composition using7200 source controls and current-table rank1 cost256FP; then bind SQ23/seqinput12 and the490 initial/rewind event controls to held input, before player weapon/AR/missile/lifecycle acceptance. Keep learned input disabled until that integration is verified. Keep unsupported mechanics explicit. Sentry still needs current retirement/ownership, exact movement, spawn fallback and learned input acceptance; these do not block other skills.

Effects/audio and talent/hotkey icons remain separate 240-entry inventories with labelled placeholders. No asset generation requested for this phase. The user's tentative idle-expiry expectation does not establish a timeout.

The unregistered Blade Fury start/do/expiry component is installed and verified by focused and full suites, each with three boots/two reloads. All6912 complete source channel and12 expiry cases match per boot. Fresh21-frame and renewal7-frame leases, strict shot deadline, creation attempt versus allocation, explicit payment/rewind services and component JSON round-trip pass. This is not production held input or channel persistence. All70 Python tests, current542/21 runtime/property fingerprints,573 unchanged named decisions and existing-world installed smoke pass. Saves/config/client bytes are unchanged; server stopped. Total handlers remain150, Assassin11; Blade Shield remains the latest playable addition. Sentry retirement is individually deferred; its native living-actor gate remains unchanged.
