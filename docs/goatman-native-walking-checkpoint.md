# Goatman native walking and source geometry

Reference: owned D2R Reign of the Warlock **3.3.93847**, using the existing qualified source motion, coordinate-commit, room-link and native ground adapters. Implementation and focused verification pass: **18 native markers, three boots, two reloads and three saves**. Automatic controller and learned summon remain unfinished.

`GoatmanNativeWalking.Advance` consumes an explicitly planned source motion state matching the registered actor path. It advances the existing movement kernel against actual native ground/occupancy and commits the resulting FP16 position through the same source coordinate and room/update lists already used by placement. It does not select a route, invent a source path constructor or interpret undocumented movement-control words. A qualified active two-cell ground footprint is required; unsupported input flags are refused rather than replaced with fixture defaults.

Continuous transport updates the source client coordinates, previous/current rooms, room membership and update lists before publishing the native point. Existing native floor/height rules remain in force. No teleport effects, target reset, damage, healing or random draw is attached to walking. A blocked destination or mismatched input motion refuses before publishing a new position.

Melee geometry now reads a registered Goatman's source cell instead of reconstructing it from the rounded native tile. This preserves half-tile movement information for subsequent attack range/collision checks. An active controller/native-position mismatch is explicit; parked bodies can still expose source metadata while correctly reporting no live combat room. Unregistered bodies and other species retain their existing geometry paths.

## Verification

The original actor walked 79 source updates through one room boundary, with 40 fractional-cell geometry samples. Native occupancy and mismatched/inactive-path refusals left position and room state intact. Wounds and the shared AI/combat seed stayed unchanged. The same source path, room graph, controller, ownership and wounded body survived two reloads. Decision-root and saved-Frenzy action regressions passed in the same cohort.

Content build1, integration build2, source snapshot2 and native run2 are authenticated by `tools/reference/goatman-native-walking-native-evidence.json`. The first fixture requested an unavailable terrain corridor; its log/source/DLL are retained under the ignored stage diagnostics. The accepted fixture uses an existing clear arena; no terrain or production rule changed. Installed106 artifacts,38 saves,20 configuration files and43 client patch files are unchanged.

## Remaining host integration

The source root still requires actual AI-entry preprocessing, room/ownership traversal, birth-path construction, route and mode/velocity selection, warp, timed action/event delivery and durable clock/rearm. This transport supplies the movement commit leaf; it does not make the full controller automatic. Native fixture flags, routes and velocities are declared test inputs only. Source size/flying differences for Tainted and Defiler are not enabled by this Goatman-specific adapter. Summon cap and learned casting remain outstanding.

No new timer, persistence ID or save version is introduced. Existing Gargoyle appearance is reused; final skill effects/audio and talent/hotkey icons remain separate. Installed build/configuration/saves and native/client binaries remain protected.
