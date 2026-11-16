# Sorceress mechanical acceptance map

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations first. Charms and runewords are excluded. All 30 handlers exist, including approved held Inferno. The current combined run passes 738 exact results across three boots and two reloads; see the [receipt](../tools/reference/sorceress-planned-acceptance-native-evidence.json). This maps that declared scope and existing dedicated evidence; it does not certify the class or remove the explicit limits below.

The current driver is `tests/check_sorceress_planned_acceptance.py`. Its [checkpoint](sorceress-planned-acceptance-checkpoint.md) identifies the tested candidate and result. Family names below identify actual checks included in that driver, rather than treating a visually similar NPC spell as player acceptance.

| Talent | Mechanical scope | Included native families |
| --- | --- | --- |
| Ice Bolt | Captured cold bolt, hard synergy, travelling hit, chill and saved flight | `ice-bolt` |
| Frozen Armor | Defense buff, replacement, incoming melee freeze, retained rank and expiry | `cold-armor`, `sorceress-skills` |
| Frost Nova | Travelling cold ring, target delays, typed damage and chill | `frost-nova` |
| Ice Blast | Travelling cold hit, freeze, immunity and shattered-corpse eligibility | `ice-blast` |
| Shiver Armor | Defense buff and melee-attempt cold retaliation | `shiver-armor`, `monster-return`, `monster-ordered-release` |
| Glacial Spike | Travelling bolt and copied-area cold damage/freeze | `glacial-spike` |
| Blizzard | Prepared cast/delay, saved scatter centre and independent collision shards | `player-blizzard`, `blizzard` |
| Chilling Armor | Defense buff, ranged reaction and travelling retaliation bolt | `chilling-bolt`, `cold-armor`, `sorceress-skills` |
| Frozen Orb | Saved moving parent, trail/terminal shards, collision and expiry ordering | `player-frozen-orb`, `frozen-orb` |
| Cold Mastery | Effective-rank resistance piercing and supported broken-immunity order | `sorceress-passives` |
| Charged Bolt | Captured bolt count/damage, wandering paths and independent saved flights | `charged-flight` |
| Telekinesis | Prepared damage/stun/knockback, eligible world interactions and payment | `telekinesis-combat`, `telekinesis-world` |
| Static Field | Current-life reduction, difficulty floors, resistance and cast eligibility | `static-field` |
| Lightning | Separate SQ12 timing and travelling piercing lightning packet | `lightning-flight` |
| Nova | Travelling lightning ring, target delays and captured damage | `nova` |
| Chain Lightning | Separate SQ12 timing, primary/chain phases and saved target history | `player-chain`, `chain-lightning` |
| Teleport | Approved landing/mana rule, cancellation, input, scroll conservation and no pending-action replay | `player-teleport`, `teleport` |
| Thunder Storm | Captured buff rank/lifetime and periodic target/damage pulses | `storm-pulse`, `thunderstorm` |
| Energy Shield | Ranked mana shield, live hard Telekinesis efficiency and ordered non-poison absorption | `energy-shield`, `sorceress-skills` |
| Lightning Mastery | Effective-rank lightning enhancement at the applicable producer | `sorceress-passives` |
| Fire Bolt | Captured fire packet, travelling collision, payment and saved flight | `fire-bolt` |
| Warmth | Passive effective-rank mana recovery and resource limits | `sorceress-passives` |
| Inferno | Approved press/hold/aim/release, threshold/payment, independent flames and no held-intent replay | `inferno-held-input`, `inferno-mode`, `inferno-stream`, `inferno-primitives` |
| Blaze | Movement-emitted fire fields, captured damage and saved expiry | `blaze`, `fire-ground` |
| Fireball | One copied-area packet on contact/expiry, captured modifiers and independent mitigation | `fire-ball` |
| Fire Wall | Independent makers/endpoints, periodic fields and saved progression | `fire-wall`, `fire-ground` |
| Enchant | Allied weapon buff, retained damage/AR/duration and failed-recast payment conservation | `enchant-cast`, `buff-payment`, `enchant` |
| Meteor | Saved warning/impact and continuing burning ground | `player-meteor`, `meteor` |
| Fire Mastery | Effective-rank fire enhancement at the applicable producer | `sorceress-passives` |
| Hydra | Original head actors, captured birth stats, autonomous bolts and finite retirement | `hydra-world`, `hydra-prepared`, `hydra` |

Shared `sorceress-binding` checks cover all 30 pinned mouse policies, passive refusal, quick slots and saved normalization. `sorceress-cast-clock-check` covers the 23 ordinary SC routes and keeps Lightning/Chain Lightning, Inferno, passives and weapon timing separate. The held Inferno wire callback tests real left/right/quick press, duplicate delivery, heartbeat, release and stale-heartbeat refusal. The combined save stages held Inferno and an unpaid Teleport action before one save, preserving emitted effects while transient input/actions must not replay.

## Explicit remaining limits

- Fireball's existing wall fixture characterizes an explosion beyond a native blocking tile. Glacial Spike and other explosive producers need the same shared terrain-impact review; a correct D2R-to-UO tile centre has not been established or invented. A [tested native-world proposal](explosive-terrain-rule-proposal.md) now reuses the existing lob last-clear-tile adapter for player Fire Ball/Glacial only (46 results/two reloads); it remains disabled pending a decision and learned-input qualification. Other producers are not silently changed.
- Energy Shield has no qualified live producer for the older monster drain fields. The native armor-ignore route remains distinct from the supported typed five-channel incoming adapter. Psychic Ward's unsupported Energy Shield overlap stays guarded.
- Telekinesis world targets such as portals, waypoints and shrines remain unsupported where no matching world service exists. Current target-specific guards remain authoritative.
- Exact modern room/geometry/candidate and event/RNG ordering, unqualified special/PvP recipients, historical binary upgrades and graphical-client behavior remain bounded by each dedicated receipt. A combined pass cannot establish those absent adapters.
- Final effects/audio and talent/hotkey icons stay separately tracked in [effects](skill-effects.md) and [icons](skill-icons.md). Identified placeholders do not block mechanics.

The [status ledger](../data/Sanctuary/Balance/class-skill-status.json) and linked dedicated checkpoints retain the skill-specific uncertainties. Continue unaffected mechanics when one path is unsupported; do not turn an unresolved rule into silent behavior or class certification.
