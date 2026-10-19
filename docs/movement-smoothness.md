# Movement smoothness investigation — 18 October 2026

The comparison build keeps UO's authoritative tile movement and changes only the responsive ClassicUO route follower. It aims to reduce unnecessary bends and turn pauses. It does **not** supply arbitrary-angle authoritative travel or claim Diablo II movement parity. The independent gear patches and live server remain unchanged.

## What the code establishes

| Layer | Evidence and restriction |
| --- | --- |
| Controls | `ClassicUO/Game/ClassCombatClient.cs`: ground click/hold, 200 ms steering heartbeat, 200 ms click/hold threshold, release/Shift/focus cancellation. A short click completes its destination route; release after a hold stops further requests, allowing the committed step to finish. Run/Walk comes from the saved explicit toggle. Targeting, skills, interactions and combat timers are outside this change. |
| Route generation | `ClassicUO/Game/Pathfinder.cs`: eight neighbour offsets; diagonal edges pass side-tile/corner checks and height calculation. The bounded A* charges 1 for an axis edge and 2 for a diagonal, although both take one movement interval. Its tie/order/cost choices can group directions into long legs rather than follow the requested straight line. |
| Requests and prediction | `PlayerMobile.Walk` uses the previous committed endpoint and server direction, then queues a turn or adjacent tile step. `Send_WalkRequest` sends packet `0x02`: direction/run bit, sequence and fast-walk key, **no X/Y coordinates**. The client normally waits 80 ms for a turn (45 ms with fast rotation); it waits the full selected/equipment-adjusted interval for translation. |
| Authority | ModernUO `IncomingMovementPackets.MovementReq` calls `MovementThrottle.ValidateAndExecute`. `Mobile.Move` turns when facing differs and translates only on the subsequent same-facing request. `PlayerMobile.ComputeMovementSpeed` charges the configured turn delay (this installation: 0), 200 ms for an ordinary foot run and 400 ms for a walk, with existing running modifiers. Throttling retains sequence, timing credit, ordered queue and rejection/resync handling. The feature flag can disable timing detection; the new disposable test explicitly enables it for a normal-access player. |
| Collision | `Server.Movement.Movement.Offset` changes integer X/Y by one. UOContent `MovementImpl.CheckMovement` checks terrain, static/dynamic items, surface/bridge heights, headroom and diagonal side cells. Player diagonals require both side checks. Doors and fences are collision objects; changing a door changes subsequent legal edges. Neither sprite artwork nor facing creates these restrictions. |
| Rendering | `Mobile.ProcessSteps` already interpolates each adjacent step using elapsed time, including elevation. It uses eight direction vectors and integer-pixel offsets. A step finishes at `stepDelay - FrameDelay[1]`, then offsets reset exactly to the tile endpoint. There is no existing spline or independent fractional authoritative position. `GameObject` projects tile coordinates with 22-pixel isometric axes and four pixels per Z unit. |
| Combat | `ClassCombat` tests server tile position, range, LOS and elevation, snapshots the action origin, and cancels pending effects when the player moves. Running defense penalties are tied to accepted translations. A render-only offset cannot change those authoritative tests. |

Paths above are under `upstream/ClassicUO/src/ClassicUO.Client/` or the corresponding `upstream/ModernUO/Projects/` tree. These are observations of the modified checkouts, not assumptions about stock clients.

## Confirmed causes and remaining hypotheses

The actual baseline client took grouped six-step paths to `(±6, ±2)` and `(±2, ±6)` on unobstructed terrain. Maximum perpendicular deviation from the requested line was **1.265 tiles**. Its straight accepted-step interval was about **200 ms**, while the largest intervals at direction changes were **273–290 ms**. This confirms coarse path shape and the extra client turn wait as contributors; it does not show an unexplained pause after every ordinary straight step.

Responsive replanning previously started at displayed `Player.X/Y/Z`, even though `PlayerMobile.Walk` starts at the queued endpoint. The mismatch is confirmed in code; the extent to which it affected perceived steering in earlier play is not separately measured. The follower also trusted A*'s cached incoming direction, even though an improved parent assignment does not update that direction. The responsive follower now derives an edge from its adjacent coordinates.

Residual stepping is expected: interpolating the same eight tile edges cannot create an arbitrary straight travel angle. Directional sprite alternation can remain visible on an interleaved path. The early interpolation completion and pixel rounding may contribute a small frame-scale hitch, but were not isolated as measured causes here. No rendering curve was added based on that hypothesis.

## Implemented bounded change

The dedicated patch is [`0016-continuous-tile-steering.patch`](../patches/ClassicUO/0016-continuous-tile-steering.patch). Only `Pathfinder.cs` changes in production code:

- Responsive routes start at the endpoint of all committed steps, including its Z. An already-reached endpoint requires no route search. Partial-route continuation can use that same endpoint without waiting for an empty acknowledgement queue.
- For destinations up to 32 tile steps away, first try a rounded integer line. It distributes the two adjacent travel directions instead of grouping them into long legs. Every edge uses `CanWalk`, including height and both diagonal side checks. A redirected/blocked edge rejects the direct route and falls back to A*. This is tile path selection, not corner cutting or a visual shortcut.
- Direct probing and A* share the existing 4 ms search deadline. A* retains the 256-expanded-node limit, populated-list scans, safe partial paths and bounded reconstruction from the held-movement hang fix. Initialization, JIT and one collision expansion can exceed the nominal search budget; it is not a hard frame-time guarantee.
- A responsive follower advances a path node only when a request actually reaches that node. It derives direction from the adjacent endpoint coordinates and abandons a non-adjacent or redirected route.
- At a normal request deadline, it may send **one turn plus one translation** together. It removes only the client's artificial turn wait; it never bypasses a translation deadline. It keeps the existing five-step cap and declines pairing when three acknowledgements are outstanding. Ordered stock packets, server movement costs and server collision validation remain intact.
- Native pathfinding callers retain their default behavior. Existing steering heartbeat limits, stop semantics, run modifiers, request sequence, interpolation and denial handling remain in use. Optional `SANCTUARY_TRACE_MOVEMENT=1` records search time, source endpoint, destination and path size; normal play does not log each search.

The original eight directions still take the same time per tile step, including diagonals; consequently their pre-existing Euclidean distance difference remains. The change removes the *extra* time associated with a path turn and does not add per-angle speed multipliers. The comparison routes retain six translations and the same mix of axis/diagonal edges.

## Why visual smoothing alone is insufficient

Existing elapsed-time interpolation makes the sprite move between endpoints, but a path with long orthogonal/diagonal legs still has those legs. A spline through the path could cross a wall corner or closed door; a lagging smoothing filter could show the player outside melee range while the server has already accepted the attack. Unbounded easing could also accumulate error after stops or server corrections.

A later, small rendering experiment could use the full translation duration and carefully carry frame remainder, without altering endpoints or movement rate. Any attempt to round a bend needs a swept-clearance check for the entire displayed corridor, a strict displacement limit, correct picking/depth/camera integration, and an exact reset on release, cast, denial, teleport and map change. At one-tile authority resolution, such a limit leaves visible staircase motion. This build preserves the existing on-edge interpolation and exact endpoint resets; it introduces no persistent visual drift state.

## Diablo II source check

D2MOO revision `0c0bbd1e6bef9181a3da1c046523af622a925416` was inspected as a reference, with no code imported. Its [`Path/Path.h`](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Common/include/Path/Path.h) defines a 16-bit fractional position representation, separate integer path points, velocity/direction vectors, and current/new direction fields with 64 internal directions. [`Path.cpp`](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Common/src/Path/Path.cpp) projects precise coordinates into client coordinates and has separate direction-adjustment and path-point access functions. This establishes the separation of representations in that reimplementation; it does not establish the number of sprite facings or prove that adopting one structure reproduces original D2 feel.

## Proposed finer-position prototype — not implemented

If the comparison still feels too coarse, use an opt-in prototype for one test character and its observers in a small test area, retaining UO tiles as collision/terrain data. This needs both client and server changes, beyond a cosmetic patch:

1. **Authoritative position and speed.** Add a canonical fixed-point X/Y position and a surface/Z identity. Integrate normalized velocity on a fixed server tick (for example 40 ms), at explicit walk/run speeds expressed in world distance per second. Tile `Location` becomes a spatial-index projection, not a second independent movement truth. Turning changes heading without resetting the movement budget. Decide deliberately how current diagonal speed, equipment modifiers, stamina and running-defense duration convert.
2. **Collision and elevation.** Sweep a defined actor footprint from old to new position, subdividing at every crossed tile boundary and diagonal corner. Reuse tile heights, land slopes, bridge/surface, headroom and impassable flags, with conservative rules until fractional clearance is defined. Test both side cells at corners. Walls/fences remain solid volumes; door opening/closing invalidates clearance and cannot close through an occupied swept footprint. Track which floor/bridge the actor stands on; resolve stairs, drop limits and Z continuously or by explicit surface transitions. Rounding X/Y before a single destination-tile check would permit tunnelling and is insufficient.
3. **Navigation and facing.** Keep tile A* for coarse waypoints, then follow collision-checked segments using fractional position. Local steering must preserve the current corridor and avoid an expensive full search every input tick. Choose the closest existing sprite facing from velocity, with limited hysteresis at facing boundaries; do not quantize the velocity to that facing. Idle/attack facing can be independent of locomotion.
4. **Protocol and prediction.** Negotiate an extension; send sequenced move intent/aim and an immediate stop, never trusted client positions. Send authoritative fractional position, surface/Z, velocity and last-processed input sequence at a bounded snapshot rate. Both ends use the same integration/collision rules; the client predicts, replays unacknowledged input after correction and interpolates remote actors. Cap input rate, age, acceleration/speed and prediction horizon. Stops/focus loss need a short intent lease as well as an explicit stop. Teleport, rejection, death, reconnect and map change reset prediction. Define observer packets and legacy-client fallback; `0x02/0x22` alone cannot express this state.
5. **Combat and world integration.** Use the canonical fractional position for reach, projectile footprints, LOS origins, elevation and movement-interrupt checks. Decide a reach metric and threshold that preserve intended melee balance; keeping integer `InRange` would create early/late hits at tile boundaries. Update interaction/door use distances, pickups, triggers, regions, mobile occupancy, map indexing, saves and NPC observations wherever rounded coordinates are insufficient. Make movement/cast ordering deterministic so neither a stop packet nor a tile-boundary crossing refunds an action cycle.
6. **Acceptance gate.** Compare fixed-distance travel times at many headings; prove no speed gain on rapid turning. Test thin walls, both sides of corners, a closing door, multilevel floors, stairs, blocked melee approach, move-to-cast transitions and long holds. Add delayed/lost snapshots and input, forced server corrections, reconnects and simultaneous observers. Measure maximum prediction/visual error, rejection frequency, input/snapshot rates and on-loop CPU. Keep the prototype isolated until both collision and combat-distance agreement pass.

A finer packet step without a fractional authoritative position does not solve this: it either keeps the same tile quantization or multiplies requests while server range/collision still snap to tiles.

## Verification and comparison

The comparison uses the following reproducible harness. Automated play uses the real ClassicUO renderer/input/network against a disposable ModernUO world and disposable profiles; it is not a human feel assessment. The live world and accounts are not test fixtures.


```bash
./dev build client
.tools/dotnet/dotnet build tests/Sanctuary.Integration/Sanctuary.Integration.csproj \
  -c Debug -o .local/integration
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_UI=1 SANCTUARY_TEST_COMBAT_UI=1 \
  SANCTUARY_TEST_MOVEMENT_UI=1 SANCTUARY_TEST_SMOOTH_UI=1 \
  SANCTUARY_TEST_MOVEMENT_VIDEO=1 \
  SANCTUARY_TEST_UI_STATE="$PWD/.local/logs/combat-ui-state.json" \
  python3 tests/check_britain.py
```

The optional video flag requires `ffmpeg`; the existing UI harness requires Xvfb, xdotool and Pillow. Without the video flag, only screenshots/telemetry are recorded. JSON results, client search traces, screenshots and video stay in ignored `.local/logs/`. `SANCTUARY_TEST_SMOOTH_CASES` can restrict diagnostic reruns to comma-separated fixture names; the complete run leaves it unset.

For baseline measurements, the harness supports `SANCTUARY_TEST_SMOOTH_BASELINE=1` and `SANCTUARY_TEST_CLIENT_DLL` pointing to the preserved pre-change client with its runtime/dependency files. This session retained that assembly in `.local/movement-smooth-before/`. This is a local comparison artifact, not a second distribution or a repository dependency. Both client builds use the same disposable accounts, profiles and server fixtures. The normal `./dev client` launcher uses the installed comparison build and the user's existing profile.

The full existing combat regression is a separate run:

```bash
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_COMBAT=1 SANCTUARY_TEST_UI=1 \
  SANCTUARY_TEST_COMBAT_UI=1 \
  SANCTUARY_TEST_UI_STATE="$PWD/.local/logs/combat-ui-state.json" \
  python3 tests/check_britain.py
python3 -m unittest discover -s tests -v
```

Do not run these harness instances concurrently: their diagnostic log/state filenames are shared. Tests use software-rendered Xvfb and loopback networking. High-latency congestion, human perception of animation, natural multistorey interiors and every door orientation remain outside these automated checks. The opened door is controlled by the fixture; this checks collision/traversal, not a new test of door double-click interaction. The stair fixture is a sequence of ordinary UO surface tiles rising two Z units per step.


The completed movement run also checked a 6-second hold against a closed-door barrier (31 searches; worst 4.87 ms), traversal through the opened doorway, detours around a fence and an L-shaped corner, and ascent/stop/descent across the stair surfaces. The closed-door assertion checks that no accepted position crossed its blocked cells; going around a barrier's end is legal. A 17.05-second continuously steered hold produced 85 accepted translations, 84 searches (worst 0.62 ms; 46.04 ms total synchronous search time), all eight travel directions, a peak of 8 movement packets per second and an empty server queue after release. Search time was about 0.27% of that interval. Total client CPU, including software rendering, was 8.67 CPU-seconds: about 0.51 cores while moving versus 0.49 in the short idle sample. This is a single local measurement, not a hardware-independent CPU benchmark.

The new release/reversal check observed the first reversed translation after 252 ms and the last accepted translation 7 ms after the release marker; the already committed visual step may still finish afterward. Subsequent position checks stayed stable. The existing enclosed-target regression completed 15 bounded route attempts with a worst synchronous search of 4.67 ms, and the original click-to-finish/hold-to-stop checks passed. Existing equipment timing still produced 250 ms running steps for the 20% heavy armor/shield fixture.

The client build and integration assembly compiled successfully (the client retains its existing warnings). All 16 client patches applied to the pinned source and reproduced all 31 touched files exactly. Six developer-helper tests passed. The complete movement run completed four disposable-world save/restart boots with door/spawner/resident identities preserved. No live server restart was required to install this client-only change.


The preserved pre-change client was then rerun against the same current fixtures with server timing detection enabled. Its comparison results were:

| Measurement | Pre-change client | Comparison build |
| --- | ---: | ---: |
| Maximum deviation from the requested line, four angled runs | 1.265 tiles | 0.316 tiles |
| Translations per angled route | 6 | 6 |
| Largest step interval in each angled run | 273–290 ms | 200–210 ms |
| Median straight running interval | 200 ms | 200 ms |
| Median angled walking interval | 401 ms | 400 ms |
| Largest angled walking interval | 482 ms | 400 ms |

Each route used offsets `(6,2)`, `(2,6)`, `(-6,-2)` or `(-2,-6)` from the same clear test origin. These timings describe accepted server translations, not render FPS. The direct routes' smaller deviation is a measured improvement in path shape; their remaining 0.316-tile deviation and alternating facings show the tile limitation. A single reversal/stop sample is not a latency benchmark: the old client reversed after 221 ms and the comparison build after 252 ms, both subject to the existing input-heartbeat phase. No input-frequency or instant-response claim is made.

Local comparison artifacts are `.local/logs/smooth-baseline.json`, `smooth-improved.json`, the corresponding `smooth-*-client.log` and `smooth-*-server.log`, and `smooth-baseline.mp4` / `smooth-improved.mp4`. The latter records the full movement sequence; the former records the baseline straight/angled/walking/steering sequence. These local recordings and telemetry are intentionally excluded from Git.


The combat harness additionally presses right mouse while the left-held route is still active, checks that position stays fixed after the committed step and that repeated Teeth casts consume mana, then releases the inputs. Its approach assertion now requires six deterministic hits using the existing `CombatTestActions.HitDamage` helper instead of a fixed 12-damage threshold, matching the current gear foundation without changing damage, gear or action timing.


The combined combat run passed server combat/timing/movement/resource/hitbox/loot checks and actual-client overlap picking, hostile approach/repeated melee, ground cancellation, Shift pause/resume and stationary attacks, move-to-cast stopping, held Teeth/release, friendly interaction, dagger/halberd/IAS/FCR cadence, loot/harvesting and saved Run/Walk preference. It also completed four save/restart boots. Logs are `.local/logs/smooth-combat-regression.log`, `smooth-combat-client.log` and `smooth-combat-server.log`. The installed client assembly matches the tested build. The client was left closed and the pre-existing live server was left running.
