# Barbarian class roadmap checkpoint

**Current follow-up (2026-11-06 JST):** [Grim Ward](grim-ward-actor-checkpoint.md) now has the learned paid cast, approved captured context and saved totem lifecycle. Barbarian has **29/30 handlers**, with Leap Attack still missing. Dedicated checks retain source/native limitations; this is not class completion.


Shared input/save-policy follow-up: [Barbarian bindings](barbarian-binding-checkpoint.md) now uses the pinned class mouse flags, preserves valid and independent quick/history selections, and leaves all four deferred actions unavailable. Four-class regression passes three boots/two reloads. No class certification.

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. This follows the bounded Paladin review and its [save-safety acceptance](paladin-aura-persistence-checkpoint.md). No class is certified complete. Barbarian currently has 26 handlers and four individually deferred talents; continue source-backed work without asking the user for a Barbarian they do not have.

| Talent | Current state | Missing contract |
| --- | --- | --- |
| Double Swing | Pending; selected-hand, stat and transactional dual-weapon equip/primary-identity foundations exist. | Modern start68, SQ11 hand/target transitions and payment/eligibility; older start68 is null. Old do70 is not a substitute for that start. |
| Frenzy | Pending; current fields and older do9/charge bodies are reviewed. | Modern start67, first-hit flag/charge initialization, prepared damage and payment cadence; older start67 is null. |
| Leap Attack | Pending; source table and released modern leap/nova rules are recorded. | Modern do78 landing nova count, directions, creation phase and weapon/area order, plus changed adjacent/ground targeting. Do not replace it with Leap knockback or invent a Shout ring. |
| Grim Ward | Blocked individually; placement, corpse/body and elapsed-pulse foundations exist. | Modern body-missile formula context, owner hard-Find-Potion synergy and fear creator/target context. Do not silently use caster-centered Howl, zero synergy or assumed owner values. |

Use the existing preflights: `barbarian-double-swing-preflight.json`, `barbarian-frenzy-preflight.json`, `barbarian-leap-attack-preflight.json` and the Grim Ward lifecycle records. They preserve reviewed bodies and explicit missing boundaries. The verified dual-equip foundation includes ordinary/named pairs, transactional rollback, real lift/equip packets, reverse presentation layers and saved primary identity; reuse it rather than rebuilding a dual-wield system.

The installed reference executable remains present and byte-matches the recorded 3.3.93847 SHA-256. The game process is closed. A read-only PE scan for one older dispatch-array null/duplicate-pointer pattern found no candidates; that single heuristic does **not** establish that static inspection is impossible or that the modern table is absent. Its diagnostic is `.local/research/d2r-3.3.93847/start-table-candidates.json`. No game was launched, modified, debugged or injected. Investigate actual modern dispatch identity and bodies before inferring any of the missing starts.

The user's [reference site](https://diabloforfans.com/skills?cls=bar) identifies the same build, but its descriptions are labelled AI-assisted. Its [mechanics notes](https://diabloforfans.com/skills/undocumented?cls=bar) are supplementary leads, not executable-contract proof. In particular, item-type membership alone does not establish mastery stat aggregation: the existing compiled throwing selector explicitly distinguishes throw routing from melee fallback. Compare the actual caller/stat layers before changing it. The site does not resolve the missing start, landing or owner contexts listed above.

For the existing 26 handlers, first reuse their dedicated source/native receipts and identify current gaps. Current source/status and newer evidence take precedence over historical preflights: old mastery notes that excluded Throwing Mastery are superseded by its later native implementation; old Find Potion Act questions are superseded by the approved corpse-level brackets. Do not reintroduce those resolved questions or replay every unrelated skill solely to produce another counter.

Record nonblocking questions, implement supported portions when their behavior is established, and keep uncertain branches explicit. If these four remain unsupported, proceed through unaffected class work and leave a precise checkpoint rather than inventing mechanics or marking the whole goal blocked. Preserve installed artifacts and saves; keep effects/audio and talent/hotkey icons separate with labelled placeholders.
