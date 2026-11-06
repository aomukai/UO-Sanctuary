# Class skills reboot checkpoint — 2026-11-06 JST

The user requested a stop to move Codex to another disk, update the system and reboot. Resume the existing full class-skills goal when they return. This checkpoint does not redefine completion or mark the goal complete.

## Saved and pushed

- Branch: `codex/necromancer-poison-dagger`.
- Latest gameplay commit: `21aa8264b82cbe578915c9fad960dc70f192fc8d`, verified against the remote branch before this checkpoint.
- Frenzy is implemented under the user's explicit approved startup/payment/charge contract. It shares paired contact, hand selection, SQ11 scheduling and geometry with Double Swing. See [Frenzy checkpoint](frenzy-checkpoint.md), [approval](frenzy-startup-proposal.md) and [exact decision](../tools/reference/frenzy-start-decision.json). Do not ask for that approval again.
- Respec now preserves fractional life, mana and stamina, capped to new maxima without refilling. The first failed Frenzy run exposed the prior whole-point capture/cleared mana fraction; its original binaries, source and logs are preserved.
- **200/240 handlers; no class complete.** Barbarian now has 28/30 handlers. A handler or scoped native check is not a whole-class completion certificate.

## Verified state

- Stage: `.local/class-skills/frenzy`; retained stage: `.local/class-skills/frenzy-retained`.
- Content build 4; Integration build 2; pre-build source/data snapshot 4.
- Focused native run 2: **69 markers**. Retained run 1: **254 markers**, retaining all 230 parent markers. Both cover three boots, two actual reloads and three saves.
- Real left/right/quick input, normal/throwing melee pairs, independent paid strikes, misses/blocks/absorption, low-mana fallback, rank caps, Berserk conversion, eleven cancellation cases, actual speed consumers, natural expiry and original saved gear/resources/charge state are covered. The separate saved buff fixture deliberately extends its expiry only for retention.
- All **71 Python tests** and eight verification commands passed. Audit run 2 retains all 573 named-item decisions with **1,103/21 input fingerprints**.
- [Native receipt](../tools/reference/frenzy-native-evidence.json) and [owned-table rule binding](../tools/reference/frenzy-rule-reference.json) are saved. Exact current start67/charge chronology, paired IAS/WSM quirks and full world/PvP/client acceptance remain unverified; the approved adaptation is explicit.
- Installed **106 artifacts / 38 saves / 20 configuration files** and accepted **41 client patches** remain unchanged. No deployment occurred.
- **No test or build job is live.** The retained run and git push finished successfully; do not resume old process handles.

## Resume

1. Locate this checkout after the disk move and read this checkpoint plus `docs/class-skills-resume-checkpoint.md`. The previous workspace was `/home/aomukai/.codex/worktrees/revive-respec/Sanctuary`; paths may change.
2. Inspect git status and the current local/remote branch before editing. The pre-existing untracked `.tools` link is intentional; do not stage or delete it. Validate tool/native-stage/server links against their actual targets after moving the checkout.
3. Latest local state pointer: `.local/class-skills/current-checkpoint.json`; detailed execution state: `.local/class-skills/frenzy/execution-state.json`. Local test binaries, logs, source snapshots and failed diagnostics live under `.local/class-skills` and are not tracked by git.
4. Continue the remaining Barbarian gaps: Leap Attack's modern landing emission/ordering and Grim Ward's current missile formula/owner/context, placement and lifecycle integration. Consult the existing preflights and prepared shared components before writing new mechanics. Flag uncertainties and proceed with unaffected skills; do not silently invent D2R behavior.
5. Follow the remaining class roadmap. All 240 talents, playable Warlock/Witch, approved Sanctuary deviations and separate effects/icons tracking remain the goal. The D2R client was closed for work; do not require it for unaffected offline work.

Mechanical reference remains **D2R Reign of the Warlock 3.3.93847**. Final effects/audio and talent/hotkey icons remain separately tracked with identified placeholders; no image generation occurred in this checkpoint. Existing source/native evidence must retain its original scope rather than being relabelled as complete-class acceptance.
