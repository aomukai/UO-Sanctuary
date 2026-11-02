# Barbarian binding checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first.

The existing shared mouse policy now includes Barbarian. New assignments, displayed mouse choices, direct reads and version-0/version-1 load normalization use the pinned class and left/right flags. Forbidden left slots normalize to Basic Attack; forbidden right slots normalize to None. Valid choices survive, including references to individually deferred skills; separate availability gates still prevent using those skills. Quick slots keep their existing independent policy and saved values. No binding format, skill ID, combat formula or gameplay data changed.

`tools/build_barbarian_bindings.py` pins all 30 mouse rows directly from the owned current-build skills table, with its SHA-256. War cries and corpse skills are right-only, ten passives have neither mouse flag, and combat skills have both flags. This introduces no new Sanctuary exception.

Verification is recorded in `tools/reference/barbarian-binding-native-evidence.json`. The fixture covers all 30 rows, both assignment slots, passive and four deferred availability gates, quick-slot independence, and rejected-assignment state/resource stability. There are 132 migration contexts: both save versions, both mouse slots, all 30 class skills plus Basic Attack, None and a foreign-class skill. Raw version-1 reads are normalized without mutating serialization; normalized saves are idempotent. Class-history switching keeps independent selections. Original Bash-left/Shout-right/quick selections and the inactive history are checked after both actual world reloads.

The same test fails against the parent content DLL on forbidden legacy normalization. An initial candidate run passed migration/reference checks but failed history activation because the fixture lacked a backpack. Adding the required backpack changes only test setup; the diagnostic preserves its log, inputs, source and DLLs. No production history requirement was weakened.

The focused disposable-server run also retains the existing Necromancer, Sorceress and Paladin binding fixtures, including approved left-button Teleport and deferred Inferno. These checks establish the scoped binding/save behavior; they do not certify Barbarian combat, physical client rendering, every packet route, or a complete class. Existing mapped Iron Golem item/resource checks are harness regressions, not new claims about finished summons.

Double Swing, Frenzy, Leap Attack and Grim Ward remain individually deferred as detailed in the [class checkpoint](barbarian-class-roadmap-checkpoint.md). Handler coverage remains 186/240, Barbarian 26/30, with no completed class. Effects/audio and talent/hotkey icons remain in separate ledgers; this policy fix adds no assets. Installed build and saves are preserved; no deployment is part of this batch.
