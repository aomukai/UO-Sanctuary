# T34 — Scrolls, consumable tomes and bonus Teleport output

Implemented 21 October 2026. The Inscription pen opens a recipe book for Identify and Town Portal scrolls and tomes; the separate rune action remains available. All recipes require the active Inscription profession, an owned pen and a registered public desk.

## Recipes and supply

| Output, for either utility | Required Inscription | Starting charges | Ingredients |
| --- | ---: | ---: | --- |
| Single-use scroll | 0 | 1 | 1 Blank Scroll + 1 matching reagent |
| Tome | 50 / 60 / 70 / 80 / 90 / 100 | 3 / 4 / 5 / 6 / 7 / 10 | 1 Leather + one Blank Scroll and matching reagent per starting charge |

Identify uses Black Pearl; Town Portal uses Bloodmoss. Recipes use the native success/gain curve from the required skill to 50 points above it. At exactly the minimum, the unmodified chance is zero; skill-100 full tomes therefore need effective skill above 100. Higher trained profession caps remain T38. A failed attempt consumes half of each ingredient, rounded up. Each committed attempt uses one pen charge, subject to existing crafted-tool saving. Quotes, cancellation, stale inputs and failed output-capacity checks consume nothing. Actual material stacks, pen, connection, profession, desk and skill conditions are checked again when the native crafting timer completes.

Crafted tomes use ordinary quality: below required skill +10 they are Average; at +10 the normal roll gives 10% High Quality and 1% Superior. High Quality adds one charge; Superior adds two. Charges are saved, cannot be refilled or saved by tool perks, and the tome is consumed when empty. Partial tomes preserve their exact charge count through native copying and restart. Every successful skill-100 ten-charge tome recipe independently has a 1% chance to produce one additional single-use Teleport scroll. Both possible outputs must fit before the success/bonus rolls. There is no separate Teleport recipe or vendor supply.

Town scribes sell Identify scrolls for 80 gold, Town Portal scrolls for 100, three-charge Identify tomes for 300 and three-charge Town Portal tomes for 360. These are configurable starting prices. Purchased tomes are Average with exactly three charges.

## Use

Right-click or double-click a utility in your backpack. Identify opens the ordinary targeting cursor and reveals only valid unidentified equipment in your own backpack, retaining its original rolled properties. Cancel, invalid targets and repeated target replies spend nothing; success spends one scroll or tome charge. Existing Identify scrolls remain usable, including their original unhued appearance.

Town Portal opens an owner-only two-way pair to the nearest supported town on the same facet. Opening a new pair replaces the previous one only after valid endpoints are found. The town destination uses the existing civic-office list. Portal positions must be supported and compatible with native gate travel, housing and multi restrictions; travel rechecks these conditions. The subsequent [allied movement rule](allied-movement.md) permits allied bystanders over endpoints without transporting them, selects a clear owner arrival within two tiles and disperses followers beside the owner. Overload, faction sigils and normal character readiness restrictions apply. Normal followers travel with their owner, and native arrival fields still execute.

The pair remains after the outbound trip and closes after the owner's return, death, disconnect or logout. Restart also closes saved endpoints. Other characters cannot use it. A failed opening spends nothing; a successful opening spends one charge and reserves the normal class casting cycle. Passing through an existing pair spends no additional charge.

Single-use Identify and Town Portal scrolls also fit the potion belt. Identify opens its target; Town Portal opens its pair. Teleport retains T33's direct world-cursor hotkeys. Tomes remain in the backpack. Existing potion filling skips scroll-led columns, and scrolls retain individual identities through removal, overflow and death transfer.

The nearest-town, replacement and return-close behavior follows [Blizzard's Town Portal description](https://classic.battle.net/diablo2exp/basics/). Owner-only access, the charge ladder, no refill, recipes and prices are the user's project rules.

## Verification

Disposable integration worlds cover the fourteen recipes, quality thresholds, success/failure conservation, exact bonus boundary, full backpack, stale/cancelled/replayed transactions, pen use, Identify targeting and charge exhaustion, belt rejection of tomes, portal ownership/replacement/travel/death, actual native movement into a portal, logout/disconnect events, restart cleanup of active endpoints and saved partial tomes. Actual client checks exercise the crafting quote/cancel/timer, tome targeting, belt loading and portal round trip. Final installation evidence is recorded under `.local/logs/t34/` and `.local/logs/deploy-t34/`.

T34 originally shipped with belt protocol 3 and combat protocol 6. The later [allied movement update](allied-movement.md) advances combat to protocol 7 and keeps belt protocol 3. ClassicUO patch 0028 contains the utility controls. Native repository pins and engine binaries are unchanged.

Content and integration builds pass with zero warnings/errors; the client has its nine existing warnings and no errors. All 37 Python checks pass. Source cumulative/legacy and installed cumulative/legacy save/reload checks pass. Real source client checks cover Inscription, the existing belt, T33 Teleport and T17/T18 Imbuing/Identify. Matching installed client checks repeat Inscription and the belt. All 34 ModernUO patches/75 paths and 28 ClassicUO patches/37 paths reproduce exactly and reverse cleanly.

Deployed with the matching client/content/data on 21 October 2026. The full pre-install backup is retained, with all 62 protected stage files and 34 non-save configuration/preference files preserved. Pre-install, installed and post-live save copies match both characters, all native skill values/caps/locks, gear, materials and 485 gold stacks. Live save, clean stop, restart and second save pass; the second server boot remains running. The existing 476 merchants and 22 civic offices were retained without new population objects or production test fixtures.
