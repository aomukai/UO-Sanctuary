# Remaining Paladin aura save checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. The [remaining-aura review](paladin-remaining-auras-preflight.md) found concrete persistence defects in Holy Freeze, Conviction and shared source knockback. Focused and full acceptance are complete for this change; no class completion or deployment is claimed.

Implemented: each of the three profiles captures an immutable record array before background serialization. Restore validates the whole candidate dictionary before replacing live state, rejecting malformed/duplicate/null payloads without losing the previous pool. Existing version0 names, fields and validation meanings remain, including independent Holy Freeze shatter state and optional knockback PauseFrozen. No skill formula, selection, damage, slow, immunity, movement or timing rule changed.

Verified: the previous content DLL fails the preserved `Holy Freeze atomic malformed-tail` regression. The candidate passes all three controllers' malformed-tail, duplicate/null-head, null-array and JSON-field roundtrip controls on actual mapped effects. Inputs, old assemblies, original source and fixture remain under `.local/class-skills/paladin-aura-persistence/negative-restore-1`.

Focused run2 passes **577 markers across three boots, two reloads and three saves**. It retains the offensive/resource/aura/melee/projectile cohorts and adds dedicated HolyFreezeChecks, including its cold-immune slow, shatter/retained-loot corpse and original saved-state controls. Conviction and Sanctuary/Charge knockback mechanics and original reload fixtures remain covered. Inspection deadline holds/rearming and fixture custody/template conditioning retain their existing explicit limits.

The first focused run passed native mechanics, then failed copying the Holy Freeze manifest because the runner defaulted to `holy-freeze-fixtures.json` instead of the fixture's `holy-freeze-checks.json`. Its original runner/inputs/logs/binaries are preserved in `diagnostic-holy-freeze-manifest-1`. The mapping correction changes no compiled code: accepted focused2 uses the same binaries and pre-build1 C#/data inputs.

Full run1 passes **1,232 markers across three boots, two reloads and three saves**, covering 79 families, 23 standalone checks and 126 input modes. All 1,121 markers from the earlier full Might receipt remain covered, with 378 retained actual input actions. The full run uses the exact binaries and source/data inputs accepted by focused run2; no intervening rebuild or metadata exception was needed. Focused inputs, logs, manifests and DLLs are frozen in `focused-accepted-2`. All readers are terminal.

Four fresh audit markers preserve item/affix decisions with911/21input fingerprints. All71Python tests,573named definitions,240catalogue skills and generator/binding checks pass. All106protected installed artifact hashes are unchanged, independently recorded in `installed-unchanged-during-full.json`. The [strict receipt](../tools/reference/paladin-aura-persistence-native-evidence.json) binds the accepted inputs, binaries, logs, manifests and preserved regressions. Git history and the remote branch identify completed backups.

Mechanical questions remain separate: Sanctuary's broader legacy BYPASSUNDEAD consumer and low-mana supplier stats, Fanaticism's modern zero-mana self damage, Holy Freeze's lethal-pulse/death/other-mode behavior, exact proximity and full modern owner/item/client/world projections. Unsupported branches are not silently implemented or cleared by the save fix. The older elemental evidence's Holy Freeze gate is superseded by its newer dedicated implementation/evidence.

The user-suggested [reference site](https://diabloforfans.com/skills?cls=bar) still identifies3.3.93847, but labels its descriptions as AI-assisted. Its [Barbarian notes](https://diabloforfans.com/skills/undocumented?cls=bar) provide leads, not proof of missing executable start/landing/owner contracts; no mechanic was changed from that prose. Current local reference process is closed. Continue source-backed unaffected work without requiring the user to reopen it.

Effects/audio and talent/right/quick icons for the twelve auras remain in the separate ledgers with identified placeholders. No final assets, installed build changes or save resets occurred.

Next: [Barbarian class roadmap](barbarian-class-roadmap-checkpoint.md). Its four deferred skills need specific modern reference contracts; their uncertainty must not be hidden or treated as a global stop.
