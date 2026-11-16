# Warlock weapon, Hex and Ward acceptance work

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. Charms and runewords are excluded. This is the second Warlock group; the accepted [demon/foundation group](warlock-demons-acceptance-checkpoint.md) covers the first seven implemented routes.

The [driver](../tests/check_warlock_weapons_acceptance.py) composes existing controls for nine routes: Hex: Bane, Cleave, Echoing Strike, Hex: Purge, Blade Warp, Psychic Ward, Eldritch Blast, Hex: Siphon and Mirrored Blades. It retains the existing shared Magic Arrow weapon/missile regression, rather than claiming that regression is a new Warlock route.

Declared scope: **21 families, seven connected asynchronous families, 24 current input commands, four cached reference files, 240 expected markers, 93 expected connections and three saves/two reloads**. Cleave's right-input ground flag and original pre-save pending-action control remain. The seven asynchronous families retain their separate setup/result waits. Expected totals are not passing results until the actual job succeeds.

The candidate copies the accepted demon group's unchanged C1/I15 artifacts and runtime data. `artifact-reuse.json` binds the parent receipt and native-input hashes; source snapshot 1 binds current C#/project/data. No production gameplay/data/kernel change or new reference compilation occurs; fixture-only integration rebuilds below retain that copied baseline. Existing socket pacing preserves native security.

The original weapon/Hex group replacements, Bane mastery/event behavior, Purge reaction, Ward absorption/stun, Blade Warp adaptation, Cleave fan/payment, Echo outward/return and independent hit delay, Mirrored multi-weapon contacts and once-only mana/ammunition, and Eldritch periodic Hex/recovery rules remain their approved component contracts. Original actors, items, wounds/resources, captured packets/RNG, buffers, pending-action cancellation and no replay remain asserted. Existing inspected Hex states already use the established hold interval; natural tests keep their own timing.

The ten Sigil/Chaos routes remain next. Blood Oath, Blood Boil, Engorge and Consume retain their frozen proposals without inferred approval or a repeated question. Unsupported current-engine, native/item/recipient/terrain/PvP and graphical-client boundaries remain explicit. Final effects/audio and tree/hotkey icons remain separate identified placeholders. Installed runtime/saves/configuration/client protection is still required; no deployment or artwork generation is included.


## Current fixture checkpoint

Native N1 stopped at an outdated Hex-group guard requiring learned Purge unavailable. The approved Bane/Purge/Siphon routes are implemented; the fixture now requires all three registered. Its default prepared-call refusal, invalid-family rejection, all nine buff/six cross-family target transitions, failed-payment/event preservation and independent owner/recipient groups remain. The old final log is corrected accordingly.

Focused controls then measured Purge's applying Cleave at two successful contacts/two initial seeds, versus its intended one-contact/one-seed scene. Another retained enemy occupied the restored neighborhood. `HexPurgeCastChecks` now selects an existing terrain-valid corridor clear of foreign actors on creation and reload, retaining the original saved actor, weapon, buff, mark, wounds and resources. The same contact, damage, near-enemy burst, charges and seed assertions remain; diagnostics retain their observed values.

A later focused Bane saved-state assertion failed on the second reload. Its original conjunction is now split into field-specific assertions without changing any condition. The failure did not recur in the next run; **its exclusive cause remains unassigned**, not silently explained away. Current diagnostics stay enabled for the full group.

**C1/I4/source5/focused run4 passes 66 exact results, eighteen current input attempts, 21 connections, three saves and two reloads.** [Focused receipt](../tools/reference/warlock-hex-registration-native-evidence.json), [recorder](../tools/record_warlock_hex_registration.py). Build I4 has zero warnings/errors. Exactly three integration fixtures change relative to the copied accepted I15 baseline: `WeaponHexGroupChecks`, `HexPurgeCastChecks` and diagnostic-only `HexBaneCastChecks`. Gameplay C1/data/kernel/client remain unchanged, and installed artifact/save/config/client protection passes.

The exact failed whole run and three focused controls retain logs/inputs, binaries, all C# sources and original/shared drivers on FILES under `Sanctuary-checkpoint-archives/revive-respec/warlock-weapons-acceptance`, with original local paths as symlinks.

That focused checkpoint preceded full N2; its fixture-capacity failure and the subsequent N3 preparation are recorded below. No whole-group or class acceptance is claimed before a passing full result. Ten Chaos/Sigil routes remain next; the four older proposals stay frozen.


## Shared Cleave/Mirrored fixture capacity

Full N2 passed the corrected Hex group and continued to Mirrored packet controls, where the shared `CleaveActionChecks.Fresh` helper exhausted its original inspection strip starting at `(6560,3500)`. Its unchanged terrain-valid search ends at row 3800; the combined cohort retains more old floors than its original small scope. N2 and the accepted Hex I4/source5 binaries/inputs are frozen before the next build.

One fixture line now starts that same search at row 1000. Reach, map, floor/fit/LOS, contact/range, damage, actor/item identity and all original assertions remain unchanged. Only the shared test factory's available rows increase. **C1/I5/source6/focused run1 passes18 exact results, three saves and two reloads**, retaining original Cleave, Magic Arrow and Mirrored packet controls. [Receipt](../tools/reference/warlock-fixture-space-native-evidence.json), [recorder](../tools/record_warlock_fixture_space.py). Build I5 has zero warnings/errors; installed artifact/save/config/client protection passes. Gameplay/data/kernel/client remain unchanged.

Next is full N3 with the original 240-marker/93-connection scope on C1/I5/source6. The earlier 66-result Hex receipt remains historical and unchanged. The earlier Bane saved-state failure remains unassigned and its field-specific diagnostics remain enabled. Four integration fixture files now differ from the copied parent baseline; no production source changes.
