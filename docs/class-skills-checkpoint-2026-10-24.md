# Class skill checkpoint — 24 October 2026

Goal active. Reference: **D2R 3.3.93847 — Reign of the Warlock**. Sanctuary’s approved deviations take precedence.

- **Implemented and installed: 130 handlers.** Necromancer 28, Sorceress 29, Paladin 30, Barbarian 25, Amazon 18; Assassin, Druid and Warlock mechanics remain pending. No class has a completion certificate.
- **Latest: [Exploding Arrow](../tools/reference/exploding-arrow-native-evidence.json).** Weapon arrow and delayed fire explosion are separate. Contacts, attack misses, shield blocks, walls and range expiry create the explosion. It retains the arrow’s rank and collects current owner fire and hard Fire Arrow synergy when created.
- **Verified:** focused and full native suites each passed three boots and two actual reloads. Real client packets, gear changes, shared area damage, resistance, item-event separation and independent saved arrows/explosions passed. All 70 Python tests and fresh 471/15 audit fingerprints passed; builds have zero warnings or errors. Compiled source components retain explicit service boundaries.
- **Working build preserved:** matching content loaded the existing world and shut down cleanly. Server stopped; saves retained. Client, native patches and all 573 named-item availability decisions are unchanged.
- **Next: [Ice Arrow](../tools/reference/ice-arrow-preflight.json).** Verify its separate freeze-state and cold-length conversion before reusing current control helpers, then continue the remaining Amazon talents.
- **Still needs acceptance:** exact modern scheduler, AR/stat/seed/quiver/item/proc/geometry/owner/client behavior. [Ordinary cold corpse-shatter](../tools/reference/cold-corpse-reference-gap.json), Lightning Strike’s deferred branch and earlier per-skill questions remain separately tracked.
- **Assets:** final effects/audio and talent/hotkey icons remain in independent [effects](skill-effects.md) and [icons](skill-icons.md) registers, each covering 240 talents. Exploding Arrow uses identified travel, sound and explosion placeholders.

Warlock/Witch identity 108 and its 30 pinned talents/bindings are staged; skill mechanics are pending. See the [resume checkpoint](class-skills-resume-checkpoint.md), [roadmap](class-skill-roadmap.md) and [verification record](class-skills-verification-2026-10-24.json). Verified backups go to origin/main.
