# Barbarian acceptance map

Mechanical reference: **D2R Reign of the Warlock 3.3.93847**. Approved Sanctuary rules take precedence. All thirty talents have handlers, but handler presence and a combined native run do not certify exact modern behavior or class completion. Detailed per-skill limits remain in [the status ledger](../data/Sanctuary/Balance/class-skill-status.json).

The current-class driver is [check_barbarian_current.py](../tests/check_barbarian_current.py). It exercises the following existing fixture families together on the accepted Leap Attack build, including natural loops and original saved state. No fixture grants a completed-class status. The final accepted command inventory, source hashes and build identity belong to the accompanying checkpoint receipt, rather than to this mapping alone.

| Talent | Mechanical scope exercised | Fixture family |
| --- | --- | --- |
| Howl | Paid travelling ring, eligibility, shared Terror and saved curse | `howl` |
| Find Potion | One corpse attempt, paid cast, approved monster-level potion tiers, retained loot | `find-potion` |
| Shout | Paid self/travelling ally defense, duration, replacement and saved waves | `shout` |
| Taunt | Paid selected/fallback target, curse, approach and melee behavior | `taunt` |
| Battle Cry | Paid travelling hostile ring, curse replacement, damage/defense effects | `battlecry` |
| Find Item | Paid one-attempt corpse search, retained corpse/loot and captured drop context | `find-item`, `find-item-primitives` |
| Battle Orders | Paid self/travelling ally life/mana/stamina, proportional fractional resources | `battle-orders` |
| Grim Ward | Approved rank/context capture, paid corpse claim, placement, timed pulses and shared fear | `grim-ward-actor`, `grim-ward-world-pulse`, `grim-ward-curse`, `grim-ward-lifecycle` |
| War Cry | Paid travelling physical spell hits, block, stun/recovery and saved waves | `warcry` |
| Battle Command | Paid self/travelling all-skills buff, passives, pet caps and resource retention | `battle-command` |
| Blade Mastery | Eligible blades, effective-rank attack/damage and physical critical ordering | `barbarian-masteries` |
| Axe Mastery | Eligible axes and shared mastery consumers | `barbarian-masteries` |
| Mace Mastery | Eligible blunt ancestry and shared mastery consumers | `barbarian-masteries` |
| Polearm Mastery | Eligible polearms and shared mastery consumers | `barbarian-masteries` |
| Throwing Mastery | Throw AR/damage/critical/pierce and conserved/recovered quantity ledger | `throwing-mastery` |
| Spear Mastery | Eligible spears and shared mastery consumers | `barbarian-masteries` |
| Increased Stamina | Effective-rank maximum, Vigor composition, no refill and native recovery | `barbarian-passives` |
| Iron Skin | Additive defense, gear/Dexterity and running suppression | `barbarian-passives` |
| Increased Speed | Skill velocity plus Vigor, separate item FRW diminishing, accepted steps | `barbarian-passives` |
| Natural Resistance | Four-element resistance before caps/pierce, actual typed incoming damage | `barbarian-passives` |
| Bash | Paid prepared melee damage/accuracy/synergies, timed source-mapped knockback | `barbarian-attacks` |
| Double Swing | Approved paired SQ11 contacts, per-release cost and low-mana ordinary attack | `double-swing`, `double-swing-sequence`, `double-swing-phase` |
| Leap | Paid saved flight, placement/reservation, locks, deferred death and landing knockback | `leap`, `leap-geometry` |
| Double Throw | Two SQ15 hand releases, throwing projectiles, damage and quantity | `double-throw` |
| Stun | Paid prepared melee hit, hard-point duration, mapped eligibility and actual control expiry | `barbarian-attacks` |
| Leap Attack | Approved landing physical area followed by SQ14 weapon contact and saved flight | `leap-attack`, `leap-attack-sequence` |
| Concentrate | Paid prepared hit/conversion, action-long defense and uninterruptible sequence | `concentrate` |
| Frenzy | Approved paired contacts/payment, successful-contact charges, speed and duration | `frenzy`, `frenzy-release` |
| Whirlwind | Paid saved walking spin, paired cadence, target cycling, reservations and lifecycle | `whirlwind` |
| Berserk | Paid physical-to-magic hit, timed zero-defense override and temporary mode | `berserk` |

Mouse/quick/history migration and per-skill availability use `barbarian-binding`; the ten SC routes use `barbarian-cast-clock`. Real Double Swing, Frenzy, Grim Ward and Leap Attack mouse/quick packets retain separate dedicated checks. Other families exercise their existing controlled casts and natural connected loops; this is not a claim that all twenty active talents received every input mode or graphical-client acceptance.

## Approved deviations retained

- Find Potion uses the **corpse monster's level**: 1–19 light, 20–39 regular, 40–59 greater, 60+ super, full rejuvenation from level 40. No Act/region default is required.
- Double Swing and Frenzy use their recorded explicit startup, per-release payment, timing and low-mana rules. Frenzy's successful unblocked contacts build the approved speed charges.
- Grim Ward snapshots its effective rank and hard Find Potion at release, centers pulses on its totem and starts the shared fear behavior against the casting Barbarian.
- Leap Attack uses one weapon-independent circular physical landing hit, then its separate post-landing weapon strike. The circular hit replaces unresolved modern ray travel/overlap/order.

These decisions are in the linked individual checkpoints and exact decision records. They do not silently settle other current-engine mechanics. The broader proposed melee targeting/free-empty-swing pass remains a later controls task.

## Remaining mechanical acceptance

- Current executable timing, IAS/WSM, animation/event ownership, snapshot and RNG ordering remain bounded per skill. Supplied-rate older source comparisons establish only their named contracts.
- Bash's old post-allocation flat physical-event addition leaves its stored health total unchanged. That legacy behavior is implemented and tested, but its current D2R counterpart remains unverified.
- Find Item's current chance conflict and special/quest/boss/elite/changed-level treasure contexts remain gated. Native corpse footprint and auto-approach are separate input limits.
- Unknown control species, native terrain/room/footprint/subtile mapping, dynamic landing, zero-distance Leap Attack, delayed-dead landing damage, unsupported gear/recipient combinations, one-broken-hand spin continuation and full dual-throw snapshots remain explicit limits.
- Original save/resource checks, controlled natural loops and actual wire inputs have their exact scopes; they do not replace PvP/multiplayer and rendered-client acceptance.
- Final [effects/audio](skill-effects.md) and [talent/hotkey icons](skill-icons.md) remain separately tracked, with identified native placeholders.
