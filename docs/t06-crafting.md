# T06 — shared crafting transactions

Scope agreed 19 October 2026: ordinary production and practice use free, shared public town facilities. Basic processing/salvage retain their portable/native-equipment exceptions; campfires permit simple meat/fish cooking, not baking. Private workshops are deferred.

The first production cohort is the ten profession tools. Reuse the nine native Tinkering recipes and add an Imbuing ladle at skill25–75, two iron ingots. Vendor tools cost10gold and receive20–30 uses once supplied. Crafted tools receive100/110/120 uses for Average/High Quality/Superior, with10% charge saving on committed production/practice attempts, including failures. Existing tool uses are preserved. Cancellation, rejection, processing and salvage spend no uses. Ordinary quality is rolled once after success at the already agreed eligibility threshold. Native per-character recipe unlocks are retained across craft changes.

Acceptance: native start/timer/completion paths revalidate profession, facility, tool, selected material, resources, attributes and connection before costs/gains. Full packs reject before consumption; no output drops to the ground. A completion ticket is single-use and transient across reconnect/restart. Test all ten outputs, quality boundaries, failure, cancellation, duplicate/stale replies, material selection, full pack, concurrent users and save/reload. Test the real client and a disposable copy of installed saves. Reproduce the cumulative native patch stack. Keep the profession release flag disabled by default and installed runtime/saves unchanged.

Status: **implemented, verified and deployed for all ten approved tools; enabled in the installed runtime**. See the [T01–T09 deployment](deployment-t01-t09.md). Equipment recipes remain T08; vendor stock and broader world authoring remain their roadmap cards. This is not a complete production acquisition loop.

## Transactions and state ownership

[CraftTransactions](../src/Sanctuary.Content/CraftTransactions.cs) connects the existing native `CraftItem.Craft` timer to a guarded completion for the ten selected output types. [Native patch0016](../patches/ModernUO/0016-crafting-transactions.patch) exposes narrow callbacks and the existing normal/hue-selected resource routines. Native skill requirements, subresource requirements, success chance, ingredient consumption, failure losses and skill gains remain authoritative. The existing configured era is preserved.

The timer carries a transient, single-use request binding its owner/connection, recipe/system, selected material/hue and tool. Completion checks live readiness, active profession, accessible charged tool, public facility, native `CanCraft`, recipe unlock, resources and attributes. Direct calls to either native completion overload cannot bypass the ticket for these tools. Invalid or repeated requests cannot fall back to a fresh transaction. Tickets are not serialized and the old connection cannot complete work after disconnect.

A prospective output must fit the backpack and carrying limit before success, quality, costs or gains. This is deliberately conservative: space freed by consumed inputs is not reserved in advance. Rejection deletes the prospective output; nothing is placed on the ground. Valid attempts use the native ingredient plan once; a failed attempt uses native failure losses and spends one tool use unless saved. Skill gain is checked only after the commit. Successful ordinary quality rolls once using trained skill before that gain, at `min(required skill+10,120)`: Average89%, High Quality10%, Superior1%; below eligibility Average only. Runes and future specialized products remain their own cards.

The guarded completion currently covers **these ten tools**. Other native outputs receive the common profession/station and tool-use checks while keeping their existing completion/quality behavior until their production cards. T06 does not activate equipment tiers, sockets, durability perks or coloured-material bonuses.

[CraftToolProfiles](../src/Sanctuary.Content/CraftToolProfiles.cs) adds optional version-0 `SanctuaryCraftTools` persistence, priority112, containing quality, original capacity and crafted-tool saving provenance. Native `BaseTool.UsesRemaining`, `Crafter` and item origin remain the owners of current uses, maker and source. Native tool quality stays Regular for the new cohort so its Exceptional multiplier cannot double the explicit capacities. The real three-way quality and saving chance appear in tool properties. Native copy hooks preserve this metadata and origin.

Missing metadata leaves an existing tool's uses/quality untouched and gives no inferred new saving bonus. New vendor tools are initialized once through native supply, never refilled by a repeated initialization. Crafted capacities are100/110/120, and crafted provenance plus Crafted origin is required for the10% saving roll. Production failures and practice use the same saving rule; saving never waives ingredients. Maker names follow the native100-skill threshold. Settings in [craft-tools.json](../data/Sanctuary/Balance/craft-tools.json) control starter price/capacities, saving and the ladle recipe; changing capacities does not refill existing tools. No native engine save version changed.

## First tool recipes

The native Tinkering menu now includes the five missing first-cohort entries: fletching tools, skillet, the two pens and ladle. The nine existing recipes retain their native skill ranges and iron quantities; only the ladle is added.

| Tool | Required skill / native maximum | Iron ingots |
| --- | ---: | ---: |
| Mortar and pestle | 20 / 70 | 3 |
| Smithing hammer | 40 / 90 | 4 |
| Fletching tools | 35 / 85 | 3 |
| Saw | 30 / 80 | 4 |
| Cartography pen | 25 / 75 | 1 |
| Skillet | 30 / 80 | 4 |
| Inscription pen | 25 / 75 | 1 |
| Tailoring kit | 10 / 70 | 2 |
| Tinker's tools | 10 / 60 | 2 |
| Imbuing ladle | 25 / 75 | 2 |

Required skill and the upper success/gain parameter are distinct. These are starting tool recipes, not a newly authored equipment recipe book or final profession progression. Stock quantities/restocking and trainer/vendor placement remain T11. Separate repair tools and resource costs remain T12. Native per-character acquired-recipe IDs are reused and checked at completion; recipe rewards remain later work.

## Public facilities and portable exceptions

[CraftFacilities](../src/Sanctuary.Content/CraftFacilities.cs) reads [craft-facilities.json](../data/Sanctuary/Balance/craft-facilities.json). With the profession release flag enabled, it idempotently places immovable, serialized `TownCraftStation` objects at registered public locations. Matching skill, exact map/location/art, two-tile range, vertical range and line of sight are required. An unregistered or movable station, including similarly decorated private furniture, does not qualify. There is no exclusive-user reservation and no station fee. A future fee basis remains undecided; no speculative charging knob is enabled.

The initial cohort uses Britain guild/shop locations from the pinned native `Data/Spawns/shared/felucca/Vendors.json` and `Data/Decoration/Britannia/britain.cfg`, with new station placement as project authoring. The kitchen reuses the existing imported oven. Native forge/anvil checks remain additional requirements for smithing.

| Craft | Felucca station coordinate / art |
| --- | --- |
| Alchemy | 1496,1660,27 / `0xB4A` table |
| Blacksmithing | 1423,1556,30 / `0xFAF` anvil; 1424,1558,30 / `0xFB1` forge |
| Bowcraft/Fletching | 1470,1577,20 / `0xB4A` table |
| Carpentry | 1430,1596,20 / `0xB4A` table |
| Cartography | 1418,1753,10 / `0xB4A` table |
| Cooking | 1425,1714,20 / `0xB4A` kitchen table; existing oven at1424,1712,20 |
| Inscription | 1494,1714,6 / `0xB4A` table |
| Tailoring | 1468,1686,0 / `0xB4A` table |
| Tinkering | 1423,1653,10 / `0xB4A` table |
| Imbuing | 1497,1657,27 / `0x974` cauldron |

All ten ordinary production paths and all practice attempts require their facility. Explicit native Board/Cloth/Bolt/Yarn/Thread processing recipes bypass profession selection, the town restriction and charges; direct native smelting/textile/skinning handlers retain their native equipment requirements. Gathering remains active. Simple cooked bird/chicken leg/fish steak/lamb leg/ribs can use a nearby active native campfire; this does not admit baking, unrelated heat sources or portable practice. Planned scrap conversions remain T07. Salvage remains portable and charge-free; its corresponding-tool routing is T16.

The existing project Imbuing quote/commit also requires a live accessible ladle and the public cauldron, and spends one use only after a valid commit. A quote captures its ladle and connection and rechecks the facility; removing/replacing the tool, moving away or disconnecting cannot charge it. Prototype rarity/prices remain until T17's tier/skill/jewellery work; its action cost is distinct from the zero station fee.

[Client patch0022](../patches/ClassicUO/0022-menu-release-capture.patch) keeps answered/cancelled native item menus present through mouse release. In this fork, disposing on double-click could send the release to the world and move the character away from a station. Repeated replies are suppressed. The actual-client test checks that native menu interaction does not change its captured position.

## Verification

[CraftingChecks](../tests/Sanctuary.Integration/CraftingChecks.cs) passed all ten native tool outputs, all three qualities and their eligibility boundaries,100/110/120 capacities, native vendor initialization/10gold pricing, no refill, copy provenance and legacy17-use preservation. Actual commits cover ordinary success, failed production, crafted-tool saving on success/failure/practice, exact type/hue selection and missing selected resources. Both direct native completion bypasses, duplicate/stale/missing/depleted-tool requests, frozen state, missing resources, full item capacity and carrying-weight rejection leave costs/gains unchanged. Concurrent users commit independently; unlearned recipes reject and learned IDs survive restart.

All eleven real Britain station objects have usable standing positions; smithing positions see both anvil and forge. Repeated initialization does not duplicate them. Tests also reject private/unregistered stations, allow portable processing and distinguish campfire fish cooking from baking. Imbuing checks missing/unpaid/stale ladles and facilities, one charge and replay. The full T05 suite still passes ten first choices, thirteen trainer skills and forty practice bands.

[The actual client](../tests/check_crafting_ui.py) confirms Tinkering, cancels a native menu, selects a tailoring-kit recipe, consumes2iron/one use for one crafted tool, rejects an item-full pack, visits the real cauldron/Tinkering room and rejects its old disconnected ticket while otherwise valid. Server assertions check unchanged position during the native menu interactions. Native resource preselection is supplied by the fixture; the confirmation and recipe menu replies are real mouse events. Tool state, origin, quality, uses and native recipe unlocks passed four disposable boots/saves/restarts. The existing T05 client confirmation/training/practice test remains separately runnable.

The comparison configuration, with professions disabled, passed four boots covering creation/class names, T04 new/legacy item state, economy, monster profiles/XP/revival, belts, pets, resurrection, monsters/combat and class-profile migration. The refreshed T02 export retains24 exact affix queries/1,536 seeded rolls with unchanged effect pools; all12 Python checks pass. Server/content/integration builds have zero warnings/errors; the client builds with59 existing warnings. Fresh/reverse/repeated patch reproduction passes **16 ModernUO patches /36 paths and22 ClassicUO patches /36 paths**.

A disposable copy of installed saves, with the feature enabled only in that copy, passed two boots/save/restarts. Two existing characters retained their profiles, allocations, native skills/caps/locks, worn identities and gear rolls/origins. The ordinary class character starts with an open Alchemy choice; staff remains unrestricted. That copy adds the eleven configured town objects (Drop-origin count510→521); this is not a live migration.

All217 existing installed runtime/configuration/save/preference files recorded at entry remained byte-identical. No installed server was started/stopped, no runtime was staged and no live character or client preference was changed. This turn began without an installed ModernUO process; prior-card PID reports are historical.

## Reproduction and release boundary

Use the non-staging server/content/client builds in [T04](t04-item-state.md) and [T03](t03-class-names.md), then:

```bash
.tools/dotnet/dotnet build tests/Sanctuary.Integration/Sanctuary.Integration.csproj -c Debug -o .local/integration --nologo
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_PROFESSIONS=1 SANCTUARY_TEST_CRAFTING=1 \
SANCTUARY_TEST_CRAFTING_UI=1 SANCTUARY_TEST_UI_STATE="$PWD/.local/logs/t06-crafting/ui-state.json" \
python3 -u tests/check_britain.py
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" SANCTUARY_TEST_PROFESSIONS=1 \
python3 tests/check_working_baseline.py --output "$PWD/.local/logs/t06-crafting/copied-world"
python3 tools/build_first_loop_catalogue.py --source-dir .local/research/foundation --check
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

The disposable harness now copies project data before adding its test-only remote facilities. Production registry data is not rewritten by tests. Do not run harnesses sharing `britain-baseline-check.log` concurrently. Run the ordinary comparison regression without the profession flag using T04's existing flags. The T02 audit must be refreshed from the current runtime export when its provenance changes.

Keep **`sanctuary.professions.enabled=false`** until the release bundle includes T14/T15's earned switching route and the required production/supply cards. Enabling it also creates these registered public facilities, so a later deployment needs matching cumulative server/content/client artifacts and a pre-upgrade backup/save/stop/install/restart check. Do not stage integration assemblies or test kits. Disabling the flag suspends enforcement but does not remove saved station objects or undo transactions; full rollback requires the pre-upgrade save and matching prior binaries. T07 is the next recommended card.

Local supplementary evidence is in `.local/logs/t06-crafting/`: build/test logs, screenshots and UI state, copied-world snapshots, exact patch reproduction and protected-file manifests. Final SHA-256 values:

- Content: `062c10772dd901e147dbf991e83d24b43c7889e7c122cd98e06f871fd1fd1627`
- Server: `ff7ae9a6a92d0f848c470a623a95428594a8f995ae1b65cf1240f2ebaea48486`
- UOContent: `b9d418417c7352fe7e469729198658f0ce495cd6e50cae4b5c13f5405f54fec1`
- Client: `f656607f3f4c6a90e463c49179b3a266f630730a79cf5139e4164909125f40f1`
