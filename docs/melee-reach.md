# Melee reach investigation — 18 October 2026

This report records the original investigation and the bounded pursuit/reach comparison implemented afterward. The original findings below describe the pre-comparison build; implementation and measurements follow them.

## Sanctuary / UO behavior before the comparison

`CombatTiming.Range` in `src/Sanctuary.Content/ActionTiming.cs` takes a basic attack's reach from the equipped weapon's `MaxRange`, or 1 for unarmed attacks. ModernUO's `BaseWeapon.DefMaxRange` defaults to 1. `BaseMeleeWeapon`, `BasePoleArm`, Halberd and Bardiche do not override it; the dagger also uses the default. The ordinary melee bases therefore have the same one-tile reach, including long polearms. Explicit staff/custom `MaxRange` overrides can differ. Gear conversion does not currently supply a separate reach profile.

`Mobile.InRange` in `upstream/ModernUO/Projects/Server/Mobiles/Mobile.cs` compares integer X and Y independently:

```
max(abs(attacker.X - target.X), abs(attacker.Y - target.Y)) <= reach
```

At reach 1, all eight adjacent tiles qualify, including diagonals. This is a square neighbourhood in map coordinates, not a circular pixel distance. Project melee separately requires line of sight and a Z difference below 16. Sprite size, the weapon's drawn length and mouse selection bounds do not alter that reach. The body radius used by Teeth's projectile test is not used for melee.

`ClassCombat.UpdateCombat` approaches/starts repeated attacks when server range and LOS pass. `ResolveAction` checks range/LOS/Z again at impact, and movement away from the attack origin cancels a pending effect without refunding its cycle. Thus, a target can legally be adjacent at windup and step outside reach before impact. A base Necromancer dagger has a 480 ms cycle and a 240 ms effect point; an ordinary run step is 200 ms. The relative timing makes this possible even with the fastest starter weapon. Longer weapons have their own timing profiles, but currently get no extra distance.

The client receives the reach in the existing combat state packet and uses it for `WalkTo(..., range, responsive: true)`. `ClassCombatClient.Update` retargets its pursuit route at most once every **500 ms**, versus the ground-steering heartbeat of **200 ms**. At five run steps per second, 500 ms corresponds to 2.5 movement intervals. This does not mean every attack incurs a half-second delay: it limits when an existing pursuit route can respond to a changed enemy position or restart.

The client also stops approach on range and height without a LOS predicate, while the server requires LOS. A target adjacent across an obstacle can therefore satisfy the client stop condition while still being unhittable. This is a confirmed predicate mismatch; it has not been isolated as the cause of the user's current melee feel report.

Confirmed code limitations are equal reach across melee bases, integer tile thresholds, the slower pursuit refresh and impact-time range revalidation. Their relative contribution to observed clunkiness has not yet been measured in moving-target play. Pathfinding to a moving enemy's previous tile and stop/restart timing are plausible contributors, not established diagnoses of a particular encounter.

## Diablo II / LoD comparison

Blizzard's [melee-range description](https://classic.battle.net/diablo2exp/items/basics.shtml) says player range combines a weapon-specific rangeadder with target size. Its [dagger table](https://classic.battle.net/diablo2exp/items/normal/daggers.shtml) and [polearm table](https://classic.battle.net/diablo2exp/items/normal/polearms.shtml) list these examples:

| Weapon | Current ordinary UO/Sanctuary reach | Diablo II rangeadder |
| --- | ---: | ---: |
| Dagger | 1 UO tile | 0 |
| Scythe | 1 UO tile | 1 |
| Bardiche | 1 UO tile | 2 |
| Halberd | 1 UO tile | 4 |

The last column is a weapon bonus, not the final distance, not screen pixels and not UO tiles. Polearms differ by base; a halberd has considerably more reach than a dagger, but not every polearm has the same bonus. Target size contributes to why a short weapon can engage a large monster at a larger centre-to-centre separation.

D2MOO revision `0c0bbd1e6bef9181a3da1c046523af622a925416` supplies corroborating source evidence:

- [`ITEMS_GetMeleeRange`](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Common/src/Items/Items.cpp#L1441) returns the item record's `nRangeAdder`.
- [`UNITS_GetMeleeRange` / `UNITS_IsInMeleeRange`](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Common/src/Units/Units.cpp#L2595) use the equipped weapon for a player, a separate monster reach field for a monster, a size-aware distance calculation (`D2Common_10399`), a caller-supplied bonus and collision validation. The general comparison is against weapon rangeadder + caller bonus + 1, with a separate overlap case and special tentacle handling.
- The distance helper uses integer game coordinates, a close-distance lookup and unit sizes. These are the game's world/subtile coordinates, distinct from rendered client pixels. Although movement has fractional positions, this particular melee helper uses integer coordinates. It is not an exact Euclidean circle or a universal fixed pixel radius.

These sources support the separation of weapon reach, target footprint and collision. They do not justify copying a rangeadder value directly into UO `MaxRange`, treating a D2 tile as a UO tile, or claiming all D2 skills use one universal melee threshold. The call-site bonus is another reason to avoid that translation.

## Investigation recommendations

1. **Measure pursuit before extending short weapons.** Use a moving hostile fixture with a dagger; record enemy updates, planned endpoint, attack start, impact, out-of-range misses and path duration. Compare the existing 500 ms refresh with a bounded, coalesced 200 ms refresh. Retain the existing 4 ms/256-node responsive budget, committed-endpoint planning, request pacing and stop semantics. Never run a route search for every render frame. Match approach completion to a reachable attack position, including the obstacle/LOS mismatch, rather than merely accepting a nearby tile across a wall.
2. **Give long weapons an explicit project reach profile.** A first comparison could retain one tile for a dagger and try two for a halberd/polearm. This is a proposed UO-scale play-test choice, not an original D2 conversion. Keep approach distance, server attack admission, impact checks and advertised reach consistent. Preserve windup/cycle timing, selected-target rules, walls and Z validation. Implement in the project layer so legacy UO and unrelated weapon/item properties need not change.
3. **Treat impact forgiveness as a separate decision.** Retaining a valid attack through a very small target displacement may help, but an unconditional extra tile doubles dagger reach along an axis. Time grace alone must not allow a target far away to be hit. With current integer authority, there are no new authoritative positions between one and two tiles; calculating `1.2` instead of `1` cannot create those positions. A modest radius may distinguish diagonal cases, but genuinely gradual distance thresholds require the finer-position work described in the movement report. Screen offsets must not authorize damage.
4. **Test moving encounters, not just stationary damage.** Compare dagger and polearm approaches head-on and diagonally, retreating targets, targets reversing during windup, two mobs crossing, release/ground cancellation, Shift attacks, doors/walls/fences and elevation. Check that long reach does not bypass collision and that pursuits cannot repeatedly cancel their own committed swings. NPC/pet reach and target footprints should receive explicit rules before claiming D2-style body-size behavior.

Pursuit and range address different problems. Better pursuit can remove waiting and unnecessary repositioning while preserving a short weapon's distance. A reach profile can distinguish a polearm from a dagger. Neither alone removes the coarse authoritative position limit.

The initial investigation used code/source inspection and the existing range-at-impact regression in `tests/Sanctuary.Integration/TimingChecks.cs`.

## Implemented comparison

Client patch `0017-responsive-melee-pursuit.patch` uses the latest received enemy step endpoint, rather than the tile currently being animated. It measures stopping distance from the player's committed endpoint as well. Changed goals (X/Y/Z or advertised reach) can replan once per 200 ms, only when a movement request is due. An unchanged goal keeps its active route. The existing responsive search deadline, node limit, movement acknowledgement/queue limits and held-movement hang fix remain in force. This is current received information, with no target prediction or extra movement speed.

`CombatTiming.Range` now gives a **Halberd** a project basic-attack minimum reach of **2 tiles**; the dagger stays at **1**. Other weapon bases retain their existing reach, and larger explicit native reach overrides are respected. This includes Halberd subclasses, but does not give every polearm two tiles. The ordinary Halberd still requires 75 Strength and 47 Dexterity; the disposable comparison raises only its test character to 100 attributes. Native `MaxRange` remains 1 on the ordinary halberd: legacy UO/NPC weapon behavior is unchanged. The equipped halberd tooltip reports class melee reach. There is no gear table, item identity, serialization or stat-conversion change for this comparison.

The same range function supplies client approach, server attack admission and impact. No impact grace, extra tile, windup change or damage change was added. A Necromancer dagger remains 480 ms / 240 ms to impact, and a halberd 800 ms / 400 ms. Target retreat, walls/closed doors, elevation and the attacker's own movement can still prevent an impact. The new hit-recovery feature described in [hit recovery](hit-recovery.md) is separate from those melee distance rules.

### Measurements and limits

The actual ClassicUO was exercised in a disposable Green Acres world with normal player movement validation enabled. A scripted skeleton attempts one ordinary movement step every 320 ms: retreat, reversal, then lateral turns. It respects collision, including contact with the player, so responsive pursuit can change how many attempted target steps succeed. These are encounter comparisons, not identical prerecorded target-position tracks or a damage benchmark.

| Dagger pursuit | Previous client | Revised client |
| --- | ---: | ---: |
| Mean authoritative separation, tiles | 1.51 | 1.25 |
| Completed route searches during the measured encounter | 9 | 12 |
| Worst synchronous search, ms | 5.12 | 5.07 |
| Peak accepted movement packets/second | 7 | 7 |
| Observed attack starts / connected impacts | 20 / 20 | 25 / 20 |

The previous encounter had a 1,040 ms out-of-reach episode after initial contact. The revised encounter's observed departures lasted approximately 40 ms at the fixture's 40 ms sampling resolution; several attempted enemy moves were blocked by earlier contact. This does **not** establish a universal 40 ms pursuit latency. The revised client logged a minimum replan interval of 200 ms, and both ended with an empty movement queue.

The unchanged impact rule remains observable: closer pursuit did not increase the connected-hit count in this short dagger comparison. There were additional attack starts, some interrupted during moving contact; a start near the end of the measurement may still have an unspent impact. This is a reason to evaluate normal moving encounters further, not evidence that extra impact reach was added or that all melee clunkiness is solved.

With revised pursuit, the separate reach run exercised dagger and halberd, stopped inside their respective advertised reach and connected hits with each. Server checks cover dagger admission at 1 rather than 2, halberd at 2 rather than 3, diagonal tile distance, retreat at impact, exact effect timing/no duplicate effect, unchanged cycle after a miss, Z difference 16, blocking wall, closed/open door, and weapon-swap cancellation. Existing casting, timing and bow-reach checks remain.

The client still lacks the server's LOS predicate in its approach stop condition. A nearby enemy across a wall may still stall pursuit while the server refuses damage. Two-tile reach does not authorize hitting through the wall; improving routing to a reachable LOS position remains a separate bounded follow-up. Integer tile authority, animation buffering and strict windup cancellation also remain.

Artifacts: `.local/logs/pursuit-baseline.json`, `pursuit-improved.json`, `pursuit-reach.json`, the corresponding `*-client.log` / `*-server.log`, and `melee-reach-run.log`. Both dagger comparisons and the reach run completed the ordinary held-movement/short-click/release/heavy-equipment checks and four save/restart boots. `MeleeReachChecks.cs`, `PursuitUiChecks.cs` and `check_pursuit_ui.py` reproduce the comparison; six Python helper tests and clean application of all 17 client patches passed before the subsequent recovery patch.

