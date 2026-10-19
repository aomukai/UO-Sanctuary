# Monster reactions and approach

The native UO approach-notice delay was a confirmed bottleneck: a player step near an idle graveyard mob waited about 2.3 seconds for acquisition and about 2.65 seconds for first movement. Active/passive AI cadence, approach choice, movement pace and attack windup are separate clocks. Removing the notice delay does not make a slow zombie walk faster.

Wild project skeletons and zombies now process movement notices immediately, scan at a paced 600 ms when idle, and coalesce repeated acquisition calls for at least 200 ms. All native visibility, enemy and LOS filters remain. A new target transitions into its approach decision in the same AI tick. No client movement/path request or new pathfinding algorithm is added.

The checked [D2MOO Skeleton and Zombie routines](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Game/src/AI/AiThink.cpp#L295) support different approach behavior. The project adaptation is:

| Species | Approach decision | Failure | Translation |
| --- | --- | --- | --- |
| Skeleton | 60% commit | Stand for 600 ms, then retry | Existing 320 ms/UO step |
| Zombie | 30% commit inside 10 UO tiles | One collision-checked random local step, wait 600 ms, retry | Existing 960 ms/UO step |

Source D2 distances are subtiles, not UO tiles. The awareness cutoff and one-tile local wander are explicit map-scale adaptations. D2's special AI-state and Burial Grounds zombie exceptions are not imported. A successful approach stays committed until melee, target change or loss; it does not reroll every 100 ms. Entering melee during an existing hesitation does not bypass its remaining wait.

Pursuit uses native path following, collision, doors, elevation and movement budgets. Commitment or a sharp target turn does not refund the remaining step period. Adjacent XY coordinates across a wall or at an invalid height no longer count as a completed approach: the AI continues routing until an actual visible attack position exists. Strict melee impact range remains unchanged. Existing 24-tile home leash, 12-second chase-contact timeout, three-second home pause and blocked-return fallback remain; height and visibility are included in valid contact.

This species behavior is confined to wild project graveyard skeletons/zombies. Native UO mobs, vendors, guardians and the lich retain their existing AI. The shared [creature profile](creature-profiles.md) carries the species reaction settings and exposes fields for later conversions.

## Verification

The same real-client fixture was run before and after, with an accepted player step placing the target nine tiles from the enemy. RNG forces a successful approach decision so the test measures notice/transition latency separately from probabilistic hesitation.

| Real client measurement | Before | After |
| --- | ---: | ---: |
| Skeleton acquisition | 2301 ms | 21 ms |
| Skeleton first movement | 2637 ms | 21 ms |
| Zombie acquisition | 2313 ms | 3 ms |
| Zombie first movement | 2649 ms | 3 ms |
| Skeleton median movement interval (20 ms samples) | 312 ms | 312 ms |
| Zombie median movement interval | 960 ms | 960 ms |

Both approaches ended in an actual connected melee impact. Independent server checks force failed/successful rolls, confirm hesitation does not extend or flood decisions, check no movement-budget refund, enforce the zombie cutoff, preserve hidden-target filtering, bound 64 repeated acquisition requests to one scan, and route around a blocking wall before attacking. All passed. Existing combat/monster checks and four save/restart boots passed before integration with common creature recovery.

Local reports: `.local/logs/approach-baseline.json`, `approach-improved.json`; build/run logs are kept under `.local/logs`. Timing is from a local test environment, not a network-latency guarantee or full D2 AI parity. Door-specific/elevation pursuit and surrounding-player/pet targeting still need dedicated encounter comparisons; native collision remains authoritative.

The final build with creature recovery measured 4 ms skeleton / 6 ms zombie notice and first movement, retained both movement rates, and passed the server and actual-client regressions described in [creature-profile verification](creature-profiles.md#validated-comparison-build--18-october-2026). The server/content build is installed for comparison; client protocol remains 5.
