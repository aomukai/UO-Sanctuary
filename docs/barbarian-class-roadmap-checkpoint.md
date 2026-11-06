# Barbarian class roadmap checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first.

**Current state (2026-11-07 JST): 30/30 handlers; 202/240 total; no completed class.** The [current acceptance checkpoint](barbarian-current-acceptance-checkpoint.md) covers all thirty talents in a 224-marker combined run and an 18-marker natural Find Potion/save cohort, each across three boots and two reloads. The [30-talent coverage map](barbarian-coverage-matrix.md) separates actual tested mechanics from unresolved acceptance. Installed build and saves remain unchanged.

| Formerly deferred talent | Current implementation | Remaining limits |
| --- | --- | --- |
| Double Swing | [Approved paired SQ11 startup/payment and ordinary fallback](double-swing-checkpoint.md) | Exact current startup, rate/hand/target/callback parity and full gear/client contexts remain bounded. |
| Frenzy | [Approved paired contacts, payment and successful-contact charges](frenzy-checkpoint.md) | Exact current startup/charge timing and additional gear/owner/client contexts remain bounded. |
| Leap Attack | [Approved circular landing hit, later SQ14 weapon strike and saved flight](leap-attack-checkpoint.md) | Current rate/target/clock, terrain, zero-distance/dead-owner and full gear/PvP/client acceptance remain. |
| Grim Ward | [Approved captured context and paid saved totem lifecycle](grim-ward-actor-checkpoint.md) | Exact current executable callback/context, native terrain/room and full owner/PvP/client acceptance remain. |

These decisions resolve the earlier availability gates; do not request their approval again. Find Potion's earlier Act/region question is superseded by the approved corpse-monster-level tiers. The user cannot supply Barbarian gameplay tests; continue from saved references and explicit adaptations without asking for a Barbarian character.

The combined checks retain the existing source/native boundaries. Bash's old post-allocation flat field leaves its stored life total unchanged; current D2R parity remains uncertain. Find Item retains its modern chance conflict and gated special treasure contexts. Other per-skill timing, RNG, snapshot, control metadata, gear, owner, native terrain, PvP/multiplayer and graphical-client gaps remain in the status ledger and coverage map. Passing a bounded check does not remove those limits.

The owned reference executable and older source comparisons remain evidence with their recorded scope. No game launch, injection, installed configuration change or deployment is required for this checkpoint. Final [effects/audio](skill-effects.md) and [talent/hotkey icons](skill-icons.md) remain independent placeholders.

Next: continue Amazon Slow Missiles/Valkyrie dependencies, reusing shared projectile, item/stat/event, pet and persistence code. Keep individually unsupported contexts gated and progress through unaffected class work.
