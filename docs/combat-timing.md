# Attack and casting timing

Class actions run on 40 ms logical frames. One server-owned cycle includes windup, one effect/release frame, and the remaining finish time. Basic attacks, empty stationary swings and Teeth use the same action deadline. Starting a cast no longer deals immediate damage or charges mana: those occur once at release. Teeth projectile damage is still resolved immediately at release; simulated projectile travel remains future work.

A started action snapshots its weapon, origin, map, timing and release deadline. Movement, incapacitation, capability loss, weapon replacement or an explicit Stop can cancel the pending effect without refunding the cycle. Releasing a mouse button stops repetition while allowing the committed action to finish. A selected victim is checked again at impact for hostility, visibility, range, line of sight and elevation. No replacement enemy is acquired after a locked target dies. Held stationary aim can update during windup at the normal input heartbeat. A late server tick resolves one pending action and starts at most one new cycle; it does not burst through missed attacks.

## Initial class profiles

These are project balance profiles, independently implemented with legacy Diablo II's frame/breakpoint structure as a reference. They are not a complete reproduction of all legacy or current D2R animations. Values below are full cycles at zero base speed modifier and no IAS/FCR. Actual weapon cycles also use their base modifiers. Cast profiles apply to the current normal-cast ability, Teeth; special skills can select different timing families later.

| Class | Unarmed | 1H thrust | 1H swing | 2H swing | 2H thrust | Bow | Crossbow | Throw | Cast |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Amazon | 14 | 13 | 15 | 18 | 16 | 13 | 20 | 14 | 19 |
| Sorceress | 16 | 15 | 18 | 19 | 21 | 19 | 23 | 18 | 13 |
| Necromancer | 16 | 14 | 17 | 20 | 20 | 18 | 22 | 17 | 15 |
| Paladin | 14 | 14 | 14 | 18 | 17 | 17 | 21 | 16 | 15 |
| Barbarian | 13 | 13 | 14 | 17 | 17 | 17 | 20 | 13 | 13 |
| Druid | 15 | 15 | 17 | 16 | 19 | 18 | 22 | 17 | 18 |
| Assassin | 13 | 12 | 14 | 18 | 16 | 17 | 21 | 13 | 16 |

The implementation is in `src/Sanctuary.Content/ActionTiming.cs`. Initial Necromancer timings: Dagger 12 frames / 480 ms (effect at 240 ms), Broadsword 17 / 680 ms, Halberd 20 / 800 ms (effect at 400 ms), Teeth 15 / 600 ms (release at 280 ms). Attack effects occur halfway through the cycle, rounded up; cast release occurs at 45%, rounded up. These contact points are provisional mappings to UO sprite poses.

## Bonuses and breakpoints

Existing equipped `AosAttribute.WeaponSpeed` values supply IAS percentages; equipped `AosAttribute.CastSpeed` values supply FCR percentages for class controls. This interprets those storage fields as Diablo-style percentage inputs, not UO Fast Cast units. Affix names, pools and magnitudes remain separate itemisation work. UO Dexterity, stamina swing formulas, native casting recovery and reagent rules do not supply these rates.

For nonnegative bonus B, effective bonus is `floor(120 * B / (120 + B))`. Attack rate is `clamp(100 + effective IAS - weapon modifier, 15, 175)`; cast rate is `clamp(100 + effective FCR, 15, 175)`. Animation advance is `floor(256 * rate / 100)`. The cycle is `max(4, ceil((base frames + 1) * 256 / advance) - 1)` whole logical frames. This produces discrete breakpoints rather than continuous millisecond scaling. Negative affixes, slow effects and skill speed bonuses need explicit rules before being introduced.

For example, Necromancer Teeth uses 15 frames at 0 FCR, 10 at 75 FCR and 9 at 125 FCR. IAS has no effect on this cast, and FCR has no effect on basic attacks. Weapon/stat changes affect the next action; a slow action cannot inherit a fast replacement weapon's timing. Dual wielding, shapeshifting, Zeal/Strafe/Whirlwind sequences and skill-specific delay rules remain future work. Finish time after a swing is distinct from the future hit-recovery/FHR state.

## Weapon bases and tooltips

Known bases have the assignments below. Expansion and artifact subclasses inherit the nearest listed ancestor's modifier. Other bases receive a bounded fallback from their immutable native ML base speed: up to 2.0 seconds maps to -20, up to 2.5 to -10, up to 3.5 to 0, up to 4.25 to +10, and slower bases to +20. This classifies every weapon without replacing saved native `Speed` fields or changing NPC/legacy attack formulas. Weapon animation selects its attack family. Lower modifiers are faster.

| Weapon base | Speed modifier |
|---|---:|
| Dagger | -20 |
| SkinningKnife | -10 |
| ButcherKnife | -10 |
| Cleaver | +0 |
| Katana | -10 |
| Cutlass | -10 |
| Scimitar | -10 |
| Broadsword | +0 |
| Longsword | +0 |
| VikingSword | +10 |
| BoneHarvester | -10 |
| CrescentBlade | -10 |
| Halberd | +0 |
| Bardiche | +10 |
| Scythe | -10 |
| Hatchet | -10 |
| Axe | +10 |
| BattleAxe | +10 |
| DoubleAxe | +10 |
| LargeBattleAxe | +10 |
| TwoHandedAxe | +10 |
| ExecutionersAxe | +0 |
| WarAxe | +0 |
| Pickaxe | +10 |
| Mace | +0 |
| WarMace | +10 |
| Maul | +10 |
| WarHammer | +20 |
| HammerPick | +10 |
| Scepter | +0 |
| Club | +10 |
| Spear | +0 |
| ShortSpear | -10 |
| Pitchfork | +0 |
| WarFork | -10 |
| Lance | +20 |
| QuarterStaff | -10 |
| BlackStaff | +0 |
| GnarledStaff | +10 |
| ShepherdsCrook | +10 |
| Bow | +0 |
| Crossbow | +0 |
| HeavyCrossbow | +10 |
| RepeatingCrossbow | -40 |
| CompositeBow | +10 |
| ElvenCompositeLongbow | +0 |
| MagicalShortbow | -10 |
| Yumi | +10 |
| Wakizashi | -20 |
| NoDachi | +10 |
| Daisho | +0 |
| Tekagi | -20 |
| Sai | -20 |
| Kama | -10 |
| Tessen | -10 |
| Nunchaku | -10 |
| Bokuto | +0 |
| Lajatang | +0 |
| Tetsubo | +20 |
| Fists | +0 |

The existing **Weapon Speed** tooltip line now contains **Very Fast**, **Fast**, **Normal**, **Slow** or **Very Slow**. Equipped class weapons include the wielder's class and actual cycle seconds. Labels correspond to cycles of at most 12, 15, 18, 22, or more than 22 frames respectively. An unequipped item instead shows a qualified **(base)** rating from its modifier and own IAS; it does not pretend to know the inspecting character's class or other equipment. The numeric base modifier is also shown. Equip/remove and speed-bonus changes invalidate cached properties outside property generation.

Native UO sprite animations are stretched to the authoritative cycle, with timing metadata sent to nearby clients before the animation packet. Pose/contact matching remains approximate because the UO art uses different frames. The client cannot authorize damage or alter action deadlines. Protocol version 4 carries ability reach/timing and animation metadata.

Basic attacks use the equipped weapon's native reach, bounded by the combat sight range; the client approaches to the same advertised reach. Bow, crossbow and thrown animation families use a placeholder projectile on impact. Ammunition, true projectile travel and weapon-specific damage formulas remain future work. All basic weapons still deal the prototype 6 damage, so this is not a finished weapon DPS balance pass.

## References and verification

[Blizzard's weapon-speed explanation](https://classic.battle.net/diablo2exp/items/basics.shtml) describes class-relative labels and lower-is-faster base modifiers. [D2MOO's reconstructed legacy animation-rate routines](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Common/src/Units/Units.cpp) separate attack/cast modifiers and animation timing. These inform the independently implemented model; no remote code is imported into the project.

Server checks exercise class/family profiles, monotonic IAS breakpoints, known FCR thresholds, stat independence, actual equipped bonuses, speed properties, effect boundaries, one-time resolution, no replacement victim, reach changes during windup, cycle preservation under weapon swaps, cast release and cancellation, movement and gear-loss interruption. Existing combat, movement, harvesting/loot and progression checks continue in disposable worlds. Actual-client checks compare held dagger/halberd/IAS/FCR hit counts and delayed first damage, alongside existing controls, loot and save/reload checks.

Server timing checks, actual-client dagger/halberd/IAS/FCR measurements, rendered equipped tooltip inspection, existing mouse controls, nearby loot/harvesting and three save/reload boots passed on 17 October 2026. Six developer-helper tests and fresh/idempotent patch application checks passed. The live installation backup is `.local/backups/pre-action-timing-20261017-204218/`.

The installed server reloaded the existing world, retained town identities and completed a successful world save. Class progression, configuration and client settings/profiles matched the installation backup byte for byte. The desktop client remains closed.
