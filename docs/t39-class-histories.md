# T39 — Independent class histories

T40 now enables gameplay class activation with real equipment and summon transactions. See [T40](t40-wardrobe-transactions.md); references below to disabled public swaps describe the original T39 release.

Each character now saves an active class ID and an independent history for every class they have activated. Each history owns its level, experience, allocated attributes and talent ranks. Mouse bindings and twelve quick-cast slots are keyed by character and class. The existing progression APIs operate only on the active history, including XP, level gains, point spending and the second-ceremony respec.

Class IDs101–107 and talent IDs are unchanged. A fresh class starts at level1 with zero XP, no allocated points and its normal default mouse bindings. Activating a history grants no items. Inactive classes earn no XP, attribute points or talent points. Existing staff-granted levels and XP are retained exactly, including levels granted independently of the XP curve.

## Character-owned state

The backpack, bank, gold, potion-belt storage and contents, quests, reputation, position and general UI preferences belong to the character. Native trade skills, caps and locks, recipe knowledge, recognition and housing rights remain character-owned. No copies or replacement containers are created for another class.

Health, mana and stamina are shared current resources. A class transition carries each fraction into its new maximum using integer downward rounding: `old current × new maximum / old maximum`. Full remains full, empty mana/stamina remain empty, and cycling classes cannot increase a resource fraction. A living-health result of zero rejects the transition and restores the old class and pools; it does not grant a free minimum-health point.

T27's temporary effects and unfinished restorative lanes remain shared. They settle elapsed time before a transition, retain their original expiry times and remaining work, advance offline and during inactivity/downtime, and clear on death. Class selection does not clear effects, restart durations or reset action, skill, spell or recovery deadlines. Loading an existing active history uses saved native pools, capped to their existing maximums; it does not perform a fresh activation or percentage conversion.

Gear loadouts and class summons remain intended class-specific state. T40 supplies their complete transaction, including real-item storage and summon suspension. T39 does not move or clone these entities.

## Quick cast and skill assignment

The ClassicUO client displays twelve slots preassigned to unmodified F1–F12. Press S to open or close the available-skills panel. Hover a skill and press an F key to assign that slot. Left-click or right-click a skill to assign the corresponding mouse button. The Clear binding row removes a chosen slot or mouse binding. Assignment consumes no points or resources and never performs a cast.

Press an assigned F key with the cursor over the world to cast once at that location or entity, without changing either mouse binding. Teeth fires toward the cursor. Raise Skeleton requires an eligible hovered corpse; it does not silently select another nearby corpse when the cursor is empty or the selected corpse cannot be used. Normal mana costs, cast timing, recovery, range, sight and movement-only restrictions apply. Repeated keydown events do not cast repeatedly. Text controls and active chat retain their input; modified function keys remain available to existing macros.

The panel lists implemented, currently usable abilities: Basic Attack and, when available, Teeth, Raise Skeleton and Teleport. Unimplemented talents are not offered as working skills. A saved binding remains stored if respec or equipment removal makes it unavailable; its bar slot becomes inactive until the ability is available again. New class histories have empty quick bars. The bar adapts its slot widths to leave the potion belt accessible in narrow game windows.

Bindings are saved on the server and polled by the client. The version1 binding protocol uses extended command0xF7 and includes the expected active class in edits and casts. The server validates class, slot, saved ability, current availability and the normal cast rules. Stale class requests cannot edit an inactive history or cast its skills. Existing combat, stat and belt protocol versions remain unchanged.

## Save migration and validation

`SanctuaryCharacters` advances from version2 to version3. Old versions0/1/2 become exactly one history, retaining the original active class, level and XP; versions1/2 retain talent ranks and version2 retains allocated attributes. Fields absent from older versions retain their established zero defaults. Reading a history does not run new-character equipment creation, reset points or award levels.

Version3 writes a character count, then each character's mobile serial, active class byte, history count and histories. Each history contains the same class/level/XP/allocation/rank fields as version2. Characters, classes and talent IDs are written in stable order. There must be1–7 distinct histories, including the active class. Readers validate IDs, counts, allocations, rank limits, prerequisites and earned point budgets before replacing the loaded dictionary. Unsupported versions, duplicate identities and invalid data fail without partially replacing loaded progress.

`SanctuaryBindings` advances from version0 to version1. Version0 pairs attach to the character's migrated active class after character histories load, with twelve empty quick slots. Version1 stores a class byte and twelve quick-slot bytes beside each existing owner/pair. Saved choices are preserved even when a talent becomes unavailable after an intentional respec. Default mouse bindings are derived only when a class has no saved pair. Both readers validate a complete replacement before accepting it.

## Activation boundary

The wardrobe continues to allow its existing craft-only transactions. Other class selections remain disabled and server-rejected. There is no new player command or packet for activation.

The internal history foundation is accessible to the integration assembly for acceptance checks and to a future T40 transaction. It refuses equipped gear, mounts or followers until T40 can handle them, as well as death, overlap, held items, combat, aggression, pending actions and unexpired deadlines. Shared hidden potion-belt storage is allowed. The API neither suspends summons nor attempts partial gear switching.

## Verification and recovery

`ClassHistoryChecks` exercises every class ID through all three old character-save versions and the old bindings format, validates stable new-format round trips, rejects corrupt replacements, levels two histories independently, saves distinct mouse and quick bindings, checks active-only respec and preserves shared item identities, skill caps/locks and effect deadlines. It also checks empty pools, repeated class cycling, failed transitions and a non-original active class across four server boots. The cumulative and legacy suites retain the existing crafting, combat, progression and migration checks.

`check_quick_skills_ui.py` drives the actual client: S, hover assignments to F1/F2/F12, both mouse buttons, cursor-aimed Teeth, hovered-corpse Raise Skeleton, rejected empty/UI cursor summons, unavailable saved skills after respec and clearing a slot.

Copied installed saves are inspected before installation and after live restart. The read-only snapshot includes every history, its allocation/ranks and each class's binding pair, alongside the existing player, skill, gear, material and gold checks. `tests/check_class_migration.py` independently reads the original character and binding bytes from the stopped pre-T39 backup and compares every stored field with the migrated snapshot, including empty new quick bars.

The new format is not readable by an older content assembly. To roll back, stop the server and restore the complete matching pre-T39 server backup, including its assemblies and saves, together. Do not pair an old assembly with migrated saves. Keep the pre-migration backup until T40 is verified.


## Deployment checkpoint — 21 October 2026

Installed and live-save/restart verified. Source focused, cumulative and actual quick-client suites pass across four boots; source legacy migration checks pass too. Installed cumulative, legacy and actual quick-client suites pass across four boots. Catalogue reproduction,41 Python tests and exact forward/reverse patch round trips pass:38 ModernUO patches over81 paths and31 ClassicUO patches over39 paths.

The read-only byte audit of the actual pre-T39 backup preserves the original class ID, level, XP, allocated attributes, talent ranks and mouse pair. New quick slots are empty. Pre-install, installed and post-live copied-save comparisons preserve both characters, all native skill values/caps/locks, every class history/binding and all tracked gear/material/gold snapshots. Staging preserves65 protected files;34 non-save payloads include12 client preference files.

The complete matching rollback backup is `.local/backups/pre-t39-20261021-063939`. Evidence is under `.local/logs/t39` and `.local/logs/deploy-t39`, including `legacy-byte-audit.json` and `deployment.json`. Live boot1 saved at06:43:10 JST and stopped cleanly at06:43:18; boot2 loaded the new versions and saved at06:44:20. The second boot remains running. The engine, logger, application and native content are unchanged; Sanctuary content and the ClassicUO client are updated. Production contains only UOContent and Sanctuary.Content. Permanent populations are unchanged.
