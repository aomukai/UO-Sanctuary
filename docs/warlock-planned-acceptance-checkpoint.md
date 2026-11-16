# Warlock/Witch acceptance work

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. **Charms and runewords are excluded.** [Thirty-talent inventory](warlock-coverage-matrix.md). Current implementation has 26 handlers and four frozen pending talents; no whole-class acceptance is claimed.

## Current demon and foundation group

The [driver](../tests/check_warlock_demons_acceptance.py) reuses the existing learned Death Mark and Bind Demon cohorts, Demonic Mastery, ordinary Warlock/Witch casting clock and Levitation controls. Its seven route entry points are:

- Demonic Mastery and Levitation Mastery, including original Warlock/Witch equipment and soft-rank validation.
- Summon Goatman, Summon Tainted and Summon Defiler, including original saved bodies, approved native hosts, movement/flight, automatic combat or linking, birth values and caps.
- Death Mark, including real left/right/quick input, original per-pet warps and approved recipient rules.
- Bind Demon, including original native host audit, real held-input/replay/release controls and original creature persistence.

The declaration contains 21 fixture families, one shared asynchronous check, nine ordinary current input commands, two Bind wire sequences per boot and four cached reference files. It preserves the existing connected Defiler link save command and original assertions. Expected full inventory is 180 markers and 75 connections across three saves/two reloads; these are expectations until a native run succeeds.

Candidate copies the accepted Druid gameplay C1 and integration I15 without rebuilding. `artifact-reuse.json` binds the exact parent receipt, parent native input inventory, copied binary hashes and runtime data. Source snapshot 1 records the current C#/project/data baseline. The shared 2,072-case ordinary SC control is read from its pinned earlier source fixture and hash-checked, not recompiled. Existing socket pacing retains native connection security.

This is the first cohesive Warlock group, not a substitute for the rest of the class. Remaining implemented weapon/Hex/Ward, Sigil and Chaos/Entropy routes are still to be composed and verified. Existing component and learned-input evidence remains useful; do not rerun unchanged reference compiler matrices.

## Unchanged boundaries

Blood Oath, Blood Boil, Engorge and Consume retain their four frozen proposal pairs. This group asserts they remain pending and does not infer approval, enable their learned routes or reask about them. Unaffected implemented routes continue independently.

Approved bodies remain Goatman → Gargoyle, Tainted → Succubus and Defiler → Gazer. The Defiler follows and links rather than making ordinary attacks. Summon-time capture, facet difficulty/percentage-life rescaling and safe-area parking retain their existing no-healing/no-reset rules. Exact current engine, special native species/elite/Sunder, item/recipient/terrain/PvP and graphical acceptance limits remain explicit per component. Current installed D2R is newer than the pinned reference; do not inspect its process or CASC.

Final effects/audio and talent/hotkey icons remain in separate registers with identified placeholders. No new timer, persistence version, production gameplay, native kernel, deployment or artwork generation is introduced by this test composition. Installed runtime/saves/configuration/client protection remains required. The earlier five Druid WardrobeSet save warnings remain a separate shared-cleanup concern.
