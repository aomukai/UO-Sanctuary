# Class skill checkpoint — 24 October 2026

Goal active. Reference: **D2R 3.3.93847 — Reign of the Warlock**. Sanctuary’s approved deviations take precedence.

- **Implemented and installed: 131 handlers.** Necromancer 28, Sorceress 29, Paladin 30, Barbarian 25, Amazon 19; Assassin, Druid and Warlock mechanics remain pending. No class completion certificate.
- **Latest: [Ice Arrow](../tools/reference/ice-arrow-native-evidence.json).** Captured physical damage stays intact. Skill/item cold damage and raw length are prepared independently, then length becomes freeze. Shared frame-based freeze preserves owner/longer expiry, difficulty scaling, elite chill fallback and a separate thaw AI delay. Frozen corpses reject corpse skills while retaining loot.
- **Verified:** focused and full suites each passed three boots and two actual reloads, with real input on every boot. Native source matrices, actual gear/variants/elites, natural expiry and saved active freeze/thaw/arrow/corpse/resources passed. All 70 Python tests, fresh 472/15 fingerprints and zero-warning/error builds passed. Compiled source components retain explicit service boundaries.
- **Working build:** existing-world load/listen/clean shutdown passed. Server stopped; existing save bytes unchanged. Prior content/data/saves retained in `.local/class-skills/pre-ice-arrow-install`. Client, native patches and all 573 named-item availability decisions are unchanged.
- **Next: [Guided Arrow](../tools/reference/guided-arrow-preflight.json).** Establish selected-target versus ground-path homing before reusing the simpler current Bone Spirit steering.
- **Still needs acceptance:** native hostile uninterruptable/hireling mode adapters, item/other freeze producer ordering, exact modern AI/corpse/stat/AR/seed/quiver/geometry/owner/client behavior. [Ordinary cold corpse-shatter](../tools/reference/cold-corpse-reference-gap.json), Lightning Strike’s deferred branch and earlier questions remain individually tracked.
- **Assets:** final effects/audio and talent/hotkey icons remain in separate [effects](skill-effects.md) and [icons](skill-icons.md) registers, each covering 240 talents. Ice Arrow uses identified travel, sound and freeze placeholders.

Warlock/Witch identity 108 and its 30 pinned talents/bindings are staged; mechanics are pending. See the [resume checkpoint](class-skills-resume-checkpoint.md), [roadmap](class-skill-roadmap.md) and [verification record](class-skills-verification-2026-10-24.json). Verified backups go to origin/main.
