# T25 — Permanent rune sockets

Double-click an owned rune, target identified equipment with an empty socket in your backpack, review the resulting bonus and level requirement, then confirm. Installation consumes the rune only: no gold, tool, station or crafting skill is needed. The character must be ready and outside a native crafting action. Permitted hosts are socketable weapons, shields, helmets and chest armour. Vendor-origin, unidentified, protected, foreign, equipped or full hosts are rejected.

Installation is permanent. There is no extraction or replacement. The host stores the rune's frozen pattern, baseline, tier, stat, source identity and quality as a value record; the loose rune is consumed once. No additional affix roll occurs. Cancellation and changed ownership, inputs, session, connection or host state spend nothing. A full backpack does not prevent insertion because there is no output item. Broken hosts may receive a rune, but their bonuses remain inactive until repaired.

Filled sockets impose minimum character levels:

| Filled sockets | 0 | 1 | 2 | 3 | 4 | 5 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Level floor | 0 | 30 | 40 | 50 | 60 | 70 |

The effective requirement is the maximum of this floor and the existing base/affix level. Attribute requirements remain in force. Empty sockets and the rune's identity add no independent requirement. Tooltips distinguish empty and filled sockets and list each installed rune and its exact percentage.

All 15 supported percentage-stat families enter existing gear consumers once. Host-slot restrictions do not limit rune stats. In particular, enhanced damage in armour contributes to the active weapon, and enhanced defense in a weapon contributes to active armour. Existing local equipment affixes retain their existing formulas. Five 10% FHR runes add 50 percentage points to existing FHR and change the actual recovery consumer; insertion adds no separate movement, attack or cast timer path. Unequipped, broken or otherwise inactive gear contributes no rune bonus.

Copy and transfer preserve installed identities without sharing mutable arrays. Unequip/re-equip, native repair and world reload preserve totals. Salvage destroys installed runes, produces the same physical/essence yield as the corresponding empty host and recovers no contents. Other destructive transformations retain their existing rules.

Gear metadata advances from version 3 to 4. Versions 0–3 load with an empty installed-rune array; every prior field, roll, origin and socket provenance remains intact. Version 4 saves the complete frozen identities and validates the host, count and stat payload when loading. The copied-world comparison normalizes only the newly absent `Runes` field to an empty array when comparing old and new snapshots; it does not discard or rewrite existing data.

Verification covers all stat families and permitted slots, level30–70 equip boundaries, five-rune actual FHR, cross-slot damage/defense, copy/trade/reload, break/repair, unchanged salvage yield, capacity, stale/cancel/replay and invalid-host guards. The actual ClassicUO client double-clicks a rune, targets, cancels, confirms and displays the installed host, then checks equip/unequip totals. Source and installed four-boot cumulative suites, separate legacy migrations, 26 Python checks and native patch round trips pass. Production deployment uses full runtime backup, protected-file hashes, copied-world comparison and two saved live boots with a clean restart. Fixture assemblies and worlds are excluded from production.

[T26](t26-gem-sockets.md) now adds gem insertion and real slot effects. Runes and gems share capacity and filled-socket level requirements; gear persistencev5 preserves allv4 rune records.
