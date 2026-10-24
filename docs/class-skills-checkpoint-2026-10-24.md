# Class skill checkpoint — 24 October 2026

Goal active. Reference: **D2R 3.3.93847 — Reign of the Warlock**. Sanctuary’s approved deviations take precedence.

- **Implemented and installed:133 handlers.** Necromancer28, Sorceress29, Paladin30, Barbarian25, Amazon21; Assassin, Druid and Warlock mechanics pending. No class completion certificate.
- **Latest: [Freezing Arrow](../tools/reference/freezing-arrow-native-evidence.json).** The weapon parent creates separate typed cold/freeze explosions. Children retain parent rank, collect live gear/hard synergies and deliver one shared cold-only area packet plus freeze length. Shared freeze handles owner/expiry, resistance/difficulty, elite chill fallback, thaw AI and frozen-corpse eligibility with retained loot.
- **Verified:** focused/full suites each passed three boots/two actual reloads and real input each boot. Complete source360 creator/216 getter/256 parent/4320 area cases retain explicit service boundaries. Real gear, parent/child/state/resources/bindings, natural expiry and lethal corpse tests pass. All70 Python tests, current473/15 fingerprints and zero-warning/error builds pass. Earlier bow input remains covered.
- **Working build:** existing-world load/listen/clean shutdown passed. Server stopped; existing save bytes unchanged. Prior content/data/saves retained in `.local/class-skills/pre-freezing-arrow-install`. Client/native patches and all573 named availability decisions unchanged.
- **Next: [Immolation Arrow](../tools/reference/immolation-arrow-preflight.json).** Establish immediate explosion, integer ground disc, field damage/rate and15-frame localdelay before enabling a handler.
- **Still needs acceptance:** exact modern global scheduler/event/AR/stat/seed/quiver/item/geometry/owner/client behavior, D2-only hostile freeze modes and other producer ordering. [Ordinary cold shatter](../tools/reference/cold-corpse-reference-gap.json), Lightning Strike’s deferred branch and earlier questions remain separately tracked.
- **Assets:** final effects/audio and talent/hotkey icons stay in separate [effects](skill-effects.md) and [icons](skill-icons.md) registers, each covering240 talents. Freezing Arrow uses identified travel/explosion/freeze/audio placeholders.

Warlock/Witch identity108 and30 pinned talents/bindings are staged; mechanics pending. See the [resume checkpoint](class-skills-resume-checkpoint.md), [roadmap](class-skill-roadmap.md) and [verification record](class-skills-verification-2026-10-24.json). Verified backups go to origin/main.
