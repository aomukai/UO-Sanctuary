# Druid Arctic shared-world checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary adaptations first. Charms and runewords are excluded.

C1/I12/source 14/focused run 5 passes **39 exact markers, 18 connections, nine held-input wire sequences, three natural stream controls, three saves and two reloads**. The integration build has zero warnings/errors. [Strict receipt](../tools/reference/druid-arctic-composition-native-evidence.json), [recorder](../tools/record_druid_arctic_composition.py).

The combined Druid run stopped because its retained Arctic target lost a small cold hit. Diagnostic controls proved the saved projectile pool was exact, the wound preceded the stream reload checks, moving those checks ahead of the potion control did not help, and the wound already existed before saving. The held-input fixture's damage case advanced the global stream pool at a synthetic future clock. Its final packet restore could not undo damage to other fixtures.

`ArcticHeldInputChecks.Cases` now runs its synthetic clock against a temporary empty missile set, retaining the original allocation counter. Its final cleanup restores and explicitly compares the entire surrounding pool. The six-mana threshold, fractional debits, chunk-independent emission, live gear, potion handling, release, actual target damage and saved-input no-replay checks remain. `ArcticStreamChecks` adds precise target-life guards before controls, at the expiry stage and world save, and separates packet equality from life equality. All three saves now record the exact expected 50,000-life retained target.

The four failed isolated controls, unchanged reload-order experiment and combined N3 are frozen with exact inputs, logs, binaries, sources and drivers. Original archive paths remain readable through local symlinks to FILES; immutable I10 copies preserve the ordering-only experiment. Gameplay/data/native kernel/client and installed runtime remain unchanged. Protection checks retain 106 installed artifacts, 38 saves, 20 configurations and 44 client patches.

This establishes fixture composition and retained wounds for the declared controls. Combined Druid N4 remains next; no full class or current-executable parity certificate is claimed. Effects/audio and icons remain in their separate registers.
