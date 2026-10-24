# Class skill checkpoint — 24 October 2026

Goal active. Reference: **D2R3.3.93847 — Reign of the Warlock**. Approved Sanctuary deviations take precedence.

- **Installed:135 handlers.** Necromancer28, Sorceress29, Paladin30, Barbarian25, Amazon23; Assassin, Druid and Warlock mechanics pending. No class completion certificate.
- **Latest skill: [Strafe](../tools/reference/strafe-native-evidence.json).** All10 Amazon bow/crossbow handlers are present with their recorded acceptance limits.
- **Latest correction: [long poison persistence](../tools/reference/poison-duration-native-evidence.json).** Saves now accept durations over60seconds, matching the runtime. Current Poison Javelin lasts62seconds at rank28 and516seconds at rank255. Save versions unchanged; this does not change poison damage or enable another handler.
- **Verified:** long-poison state/health/source/fraction retained across two actual reloads; focused older timed-effect regressions and full combat suite pass three boots each. Full retains all10 bow inputs on all three boots. All70 Python tests, fresh475/15 fingerprints and zero-warning/error builds pass. All573 named availability decisions unchanged.
- **Working build:** existing-world load/listen/clean shutdown passed, server stopped and save bytes unchanged. Prior content/data/save backup: `.local/class-skills/pre-poison-duration-install`. Client and native patches unchanged.
- **Next: [Poison Javelin](../tools/reference/poison-javelin-preflight.json).** New complete source3456 creator/getter,384 poison application,2240 regeneration,192 route/16 sequential-contact and480 factory cases are recorded. Source0 clouds exclude weapon/item poison; factory lifetime60frames; LastCollide tracks only the most recent victim. The shared poison service still needs deliberate FP256 health, player-floor and original-owner corrections before skill certification. Path/do-handler/contact and TH release integration remain.
- **Still uncertain:** exact modern poison event phase/interruptions, mitigation/proc ordering and extended offline catch-up, plus earlier per-skill acceptance gaps. Seven Amazon handlers remain unavailable. Continue unaffected skills around individual blockers.
- **Assets:** [effects/audio](skill-effects.md) and [talent/hotkey icons](skill-icons.md) remain separate240-talent registers. Poison Javelin’s Javelin/PoisonSparks/poisonpuff/trail/contact/audio are recorded as pending; no handler or final asset is claimed.

Warlock/Witch identity108 and30 pinned talents/source/bindings are staged; mechanics pending. See the [resume checkpoint](class-skills-resume-checkpoint.md), [roadmap](class-skill-roadmap.md) and [verification record](class-skills-verification-2026-10-24.json). Verified checkpoints are pushed to origin/main.
