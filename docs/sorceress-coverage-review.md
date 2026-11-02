# Sorceress coverage and mouse-binding review

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations first. This review covers all30talents. There are29handlers:25active skills and four passives. Inferno remains individually gated. **No completed-class or exact-parity certificate is claimed.** The full per-skill remaining mechanics are preserved in the [roadmap](class-skill-roadmap.md).

Build2 passes **684 full /15 focused native markers** across three boots, two process reloads and three saves, retaining198actual wire actions. Four fresh audits preserve item/affix decisions with905/21fingerprints. All71Python tests,573named definitions,240catalogue skills and generation checks pass;106protected installed hashes remain unchanged. The native review checks all30pinned rows/60mouse flags, real learned assignment, passive/disabled availability, independent quick selection,18version0/1legacy migrations and byte-idempotent serialization. The previous pushed DLL fails at Frost Nova’s left-slot permission; its source, DLL and regression log are preserved. The [receipt](../tools/reference/sorceress-binding-native-evidence.json) binds exact source/data/DLLs/logs and original saved bindings. All build/native/audit readers are terminal; no deployment or class certificate is claimed.

The production change extends the existing Necromancer/table validator to Sorceress, preserving the approved [T33 Teleport](t33-teleport.md) left-slot exception. Six newly enforced right-only skills are Frost Nova, Nova, Static Field, Telekinesis, Thunder Storm and Hydra. Existing right-only skills keep their policy. Assignment, binding-menu buttons, canonical reads and post-load normalization all share that validator. Invalid saved left selections fall back to Basic Attack while valid right/quick entries remain; valid Fire Bolt and approved Teleport left selections survive. No save format or ability ID changes.

| Talent | Native scope retained | Mouse controls after approval | Mechanical status |
| --- | --- | --- | --- |
| Ice Bolt | ice-bolt | left/right | Partial native coverage |
| Frozen Armor | cold-armor | right | Partial native coverage |
| Frost Nova | frost-nova | right | Partial native coverage |
| Ice Blast | ice-blast | left/right | Partial native coverage |
| Shiver Armor | shiver-armor | right | Partial native coverage |
| Glacial Spike | glacial-spike | left/right | Partial native coverage |
| Blizzard | player-blizzard | right | Partial native coverage |
| Chilling Armor | chilling-bolt | right | Partial native coverage |
| Frozen Orb | player-frozen-orb | left/right | Partial native coverage |
| Cold Mastery | sorceress-passives | passive | Partial native coverage |
| Charged Bolt | charged-flight | left/right | Partial native coverage |
| Telekinesis | telekinesis-world / telekinesis-combat | right | Partial native coverage |
| Static Field | static-field | right | Partial native coverage |
| Lightning | lightning-flight | left/right | Partial native coverage |
| Nova | nova | right | Partial native coverage |
| Chain Lightning | player-chain | left/right | Partial native coverage |
| Teleport | player-teleport | left/right (T33) | Partial native coverage |
| Thunder Storm | storm-pulse | right | Partial native coverage |
| Energy Shield | energy-shield | right | Partial native coverage |
| Lightning Mastery | sorceress-passives | passive | Partial native coverage |
| Fire Bolt | fire-bolt | left/right | Partial native coverage |
| Warmth | sorceress-passives | passive | Partial native coverage |
| Inferno | prepared channel / sequence / rate only | left/right | Prepared only; learned disabled |
| Blaze | blaze | right | Partial native coverage |
| Fireball | fire-ball | left/right | Partial native coverage |
| Fire Wall | fire-wall | right | Partial native coverage |
| Enchant | enchant-cast | right | Partial native coverage |
| Meteor | player-meteor | right | Partial native coverage |
| Fire Mastery | sorceress-passives | passive | Partial native coverage |
| Hydra | hydra-world | right | Partial native coverage |

These fixture names identify bounded native scopes, not interchangeable completion claims. Current source tables, complete checked older routines, host adapters and native input/save observations have distinct evidentiary limits. The class still has recorded modern engine/world/path/collision/event/lifecycle, special-recipient/PvP/charged/oskill, graphical-client and full old-DLL/unheld-save gaps. The unchanged681marker Teleport/Hydra/spell cohort is retained in the combined run; its count alone cannot close those gaps.

Inferno’s source-compared channel/resource/sequence/rate components already exist. Modern accepted-start/reentry/payment frequency/release composition and player fire-stream/input binding remain unresolved; see [Inferno preparation](sorceress-inferno-preflight.md). Keep that skill isolated and continue unaffected class work instead of assigning a guessed cadence.

The binding fix creates no new effect, player action or icon. Final effects/audio and talent/hotkey artwork remain independently tracked in [effects](skill-effects.md) and [icons](skill-icons.md), using identified placeholders. Passive talents need tree icons rather than active hotkey icons.

Next: run the existing Paladin cohort before replacing or extending its mechanics. Reuse the accepted shared damage, resource, projectile and summon services. The installed build and existing saves remain unchanged.
