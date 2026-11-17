# Barbarian acceptance map

The current [lifecycle/input checkpoint](barbarian-lifecycle-acceptance-checkpoint.md) passes473 exact results/144actual inputs/two reloads,retaining all245predecessor markers. The [class/ability-ID audit](../tools/reference/barbarian-current-input-coverage.json) covers all20active talents;ten passives retain separate consumer checks. Fourteen previously missing input routes now have natural recipient/payment/movement evidence. Current contact/proc/poison,War Cry potion/Health Link,named-readiness and Berserk worker-save checks are included. Production code/data/native binaries were reused;only Integration changed. No native errors or save-deletion warnings;12intentional scalar Health Link refusals remain. This is scoped acceptance,not a class certificate.

The [War Cry/shared landing checkpoint](warcry-link-checkpoint.md) passes 57 focused results, nine existing Leap inputs and two reloads. Settled physical components now reach Health Link while War Cry preserves its block/immunity/stun order. The later [T29 checkpoint](warcry-potion-checkpoint.md) passes 60 results and two reloads with release-captured modifiers, live potion and original unequipped gear; legacy active-potion rays without a capture are explicitly refused. Verify with `python3 tools/record_warcry_potion.py --check`. Verify with `python3 tools/record_warcry_link.py --check`.

Mechanical reference: **D2R Reign of the Warlock 3.3.93847**. Approved Sanctuary rules take precedence. All thirty talents have handlers, but handler presence and a combined native run do not certify exact modern behavior or class completion. Detailed per-skill limits remain in [the status ledger](../data/Sanctuary/Balance/class-skill-status.json).

The current planned-content driver is [check_barbarian_planned_acceptance.py](../tests/check_barbarian_planned_acceptance.py), composing the earlier current-class driver with selected-weapon-hand, SC/FCR input and natural Find Potion release checks. It exercises existing fixture families together on reused gameplay C1, including natural loops and original saved state. Charms and runewords are excluded. No fixture grants a completed-class status. The final accepted command inventory, source hashes and build identity belong to the accompanying checkpoint receipt, rather than to this mapping alone.

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

The earlier planned-content checkpoint passed245 exact results,including39spell inputs,three checked natural Find Potion releases and six corpse-snapshot callbacks. Its [historical receipt](../tools/reference/barbarian-planned-acceptance-native-evidence.json) retains its original build. All245markers remain in the current473-result cohort;historical source readers were not relabelled as current.

Mouse/quick/history migration and per-skill availability use `barbarian-binding`;the ten SC routes retain `barbarian-cast-clock`. `weapon-hands` verifies shared hand selection;`find-potion-live` verifies natural release of an already-paid cast after mana removal. Dedicated Double Swing,Frenzy,Grim Ward and Leap Attack controls retain. The current remaining-input fixture adds right/quick Howl,Find Potion,Taunt,Battle Cry,Find Item,Battle Orders and War Cry,plus left/right/quick Bash,Leap,Double Throw,Stun,Concentrate,Whirlwind and Berserk. This establishes actual input execution for all20active talents;it does not claim every input mode for every talent or rendered-client acceptance. Find Item deliberately exercises its paid failed-roll attempt rather than claiming resolved successful treasure/chance behavior.

## Approved deviations retained

- Find Potion uses the **corpse monster's level**: 1–19 light, 20–39 regular, 40–59 greater, 60+ super, full rejuvenation from level 40. No Act/region default is required.
- Double Swing and Frenzy use their recorded explicit startup, per-release payment, timing and low-mana rules. Frenzy's successful unblocked contacts build the approved speed charges.
- Grim Ward snapshots its effective rank and hard Find Potion at release, centers pulses on its totem and starts the shared fear behavior against the casting Barbarian.
- Leap Attack uses one weapon-independent circular physical landing hit, then its separate post-landing weapon strike. The circular hit replaces unresolved modern ray travel/overlap/order.

These decisions are in the linked individual checkpoints and exact decision records. They do not silently settle other current-engine mechanics. The broader proposed melee targeting/free-empty-swing pass remains a later controls task.

## Remaining mechanical acceptance

- Current executable timing, IAS/WSM, animation/event ownership, snapshot and RNG ordering remain bounded per skill. Supplied-rate older source comparisons establish only their named contracts.
- Bash's old post-allocation flat physical-event addition leaves its stored health total unchanged. That legacy behavior is implemented and tested, but its current D2R counterpart remains unverified. A [tested post-critical/pre-mitigation candidate](bash-flat-damage-proposal.md) awaits an explicit Sanctuary decision; learned Bash remains unchanged.
- Find Item's retained two-stage chance curve is now an [explicitly approved Sanctuary rule](find-item-chance-proposal.md). Special/quest/boss/elite/changed-level treasure contexts remain gated. Native corpse footprint,ground acquisition and auto-approach remain separate input limits;modern executable chance provenance remains unverified.
- War Cry follows the existing shared Unique-only roll; native Unique leaders additionally retain the encounter boss-immunity mapping. This characterization does not establish full D2R equivalence.
- Unknown control species, native terrain/room/footprint/subtile mapping, dynamic landing, zero-distance Leap Attack, delayed-dead landing damage, unsupported gear/recipient combinations, one-broken-hand spin continuation and full dual-throw snapshots remain explicit limits.
- Original save/resource checks, controlled natural loops and actual wire inputs have their exact scopes; they do not replace PvP/multiplayer and rendered-client acceptance.
- Final [effects/audio](skill-effects.md) and [talent/hotkey icons](skill-icons.md) remain separately tracked, with identified native placeholders.
