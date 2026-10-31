# Shared Necromancer golem roster checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**. Goal active; 185 handlers / Necromancer 29, no class complete. Candidate remains uninstalled; existing build and saves are preserved.

All four golem skills use one shared pet family and cap of one. Clay, Blood and Fire creation now completes candidate setup before replacing an existing family member. Prepared Iron Golem bodies join explicitly; storage/stat fixtures remain outside the roster. Replacement consumes a joined Iron Golem's original item. Battle Command cap refresh includes parked members and leaves skeletons, mages and other owners separate.

Creation order uses the saved Battle Command ledger. Historical entries without an order use order zero, then serial, before recorded births. Their original FIFO cannot be reconstructed; this is a disclosed deterministic fallback. Membership is an optional saved field, omitted when false. Existing prepared-body saves therefore remain outside the roster until explicitly committed. No old-content-binary migration test is claimed.

Verification uses build 2 / run 2 in `.local/class-skills/golem-roster`. Both clean builds have zero warnings/errors. The complete unchanged older append helper passes 48 ASan/UBSan contexts with supplied allocation/removal/client services. Current pinned tables corroborate the shared pet type and cap; registration and cap-setter routines are static reviews. These controls do not execute the entire current game engine.

The disposable native suite passes 33 markers across three boots, two actual reloads and three saves. It covers all 16 replacement pairs, original-item consumption, unrelated pet categories/owners, invalid placement, injected pre-commit Iron failure, parked Battle Command refresh, historical ordering and storage-body exclusion. Membership, saved order, original identity and full gear rolls survive both reloads. Prior birth/body/world/retaliation checks, three natural aura expiries and three connected world sessions remain covered.

A separate boot retains four timed Paladin/catalogue markers. The 71 Python tests pass. Fresh catalogue audits preserve all 573 named decisions and affix semantics. All 106 installed artifacts remain unchanged. The [receipt](../tools/reference/golem-roster-native-evidence.json) binds exact source/data/five-DLL inputs, logs, fixtures and audit hashes; reproduce with `python3 tools/record_golem_roster.py --check`.

Learned Iron Golem remains disabled. Full attack entry/contact, incoming HP/death/kill-event ordering, remaining item procs/sunder, modern Thorns coexistence, paid targeting/placement and client acceptance remain unfinished. This change does not redesign mana payment or certify a complete casting transaction. The existing Fire Golem replacement explosion hook is retained; cap refresh remains delete-only. Full destruction/explosion event parity is separate.

No new effects or icons are introduced. Existing prepared body/flash placeholders remain identified in [effects](skill-effects.md); talent-tree and hotkey artwork remains independently tracked in [icons](skill-icons.md). Revive keeps the abilities its monster had while alive; replacing UO monster spells with D2 spells is a separate project.

Next: complete the existing shared combat entry/contact/HP/event bridge before enabling the learned Iron Golem cast. Do not hide unsupported callbacks behind no-op services. Keep individually blocked mechanics explicit and continue independent roadmap work.
