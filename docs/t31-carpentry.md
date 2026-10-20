# T31 — Carpentry, household stations and potion kegs

Implemented and deployed 20 October 2026.

The configurable book contains 66 recipes for native furniture, storage, barrel parts, household pieces and station deeds. Native materials and Carpentry entry requirements are retained, using ordinary boards. Tinkering continues to make tools, hoops and taps. Crafted station furniture does not register a private workshop. Native processing equipment remains usable under the existing portable-processing rules.

All book crafts require active Carpentry, its tool and a free registered public workshop. Keep native Carpentry’s 50% starting chance, rising to 100% at 50 skill above the requirement, with ordinary skill-gain checks, one output, one tool use with crafted-tool saving, and half of each ingredient rounded up on failure. Inputs, ownership, connection, profession, skill, tool and workshop must still match when the native craft timer finishes. Never consume a filled, trapped, locked or inaccessible container as a component. Existing native furniture placement and house permissions remain authoritative; deeds stay intact on failed placement.

Confirmed keg rule: Carpentry 75 assembles one wooden keg, ten bottles, an extra lid and a tap. A manual keg holds 100 doses of one exact custom potion kind/tier. Deposits return one empty bottle per dose; withdrawals require one empty bottle and produce one identical potion in the backpack. Preserve potion origin, check final inventory weight/slots before mutation, and never automatically fill the belt. Ordinary native kegs must reject custom potions whose identity they cannot represent.

Acceptance checks:

- Every catalogue recipe produces its actual native furniture, container or deed, with exact costs, ordinary gain checks, guarded cancellation/failure and saved identity across restarts.
- Station furniture cannot satisfy the registered public-workshop gate. House placement checks reject invalid ground, collisions and another character's property without consuming the deed.
- All nineteen custom potion identities survive keg deposit, withdrawal and restart. Mixed kinds/tiers, over-capacity, unavailable bottles, full inventories, inaccessible/remote/bank/trade/belt sources, dead actors and replays cannot mutate contents.
- Bottle totals and potion doses balance; inventory weight follows held doses; no native conversion path loses the custom identity.
- Exercise the actual Carpentry book, cancellation, native timer, output and manual keg interaction in ClassicUO, then validate a saved restart and the installed runtime while preserving the existing world and client preferences.


## Using the book and keg

Double-click a Carpentry tool and choose **Craft furniture, stations and kegs**. Pick a category, select the object and review its costs and chance before confirming. Cancel spends nothing. Household and station deeds retain native owner-house placement checks; native furniture retains its normal movement, storage and decoration behavior. Storage outputs are ordinary unlocked containers, without a secondary Tinkering roll or generated key. Furniture has no added quality or combat bonuses. The existing recognition wardrobe remains a separate earned award.

Double-click a finished potion keg, choose **Pour in**, then target a potion stack in your backpack. It pours as many doses as fit, up to 100. Choose **Fill one empty bottle** to withdraw one dose into your backpack. Transfers do not require active Carpentry or Alchemy. Use a portable keg in your own backpack, or a locked-down keg within two tiles in a house you co-own. Ground, bank, trade, other-player and belt storage are not input sources. Old native potion kegs cannot accept these custom potions.

The keg weighs 20 stones empty, plus 0.8 per held dose, matching the native keg. Mixed origins occupy separate saved counts under the same kind/tier; withdrawal selects Vendor, then Crafted, then Drop, preserving each dose's origin. Empty bottles returned by a deposit retain the input potion's origin. No potion effect or drinking roll is evaluated until the bottled potion is actually drunk. Every transfer checks final backpack slots and weight, including the keg's changed weight. A stale menu or target cannot repeat a transfer.

## Recipe book

Native Carpentry requirements and material quantities are retained, with standard Boards replacing Logs. The user approved removing secondary craft-skill gates from station furniture and making the finished potion keg a Carpentry recipe. Hoops and taps remain native Tinkering recipes: five iron ingots at a minimum requirement of −15 for hoops, two at 35 for a tap; the native success/gain curves remain. Ordinary tools are still made by Tinkering.

| Category | Object | Carpentry | Materials |
| --- | --- | ---: | --- |
| Parts | Barrel Staves | 0 | 5 Board |
| Parts | Barrel Lid | 11 | 4 Board |
| Parts | Keg | 57.8 | 3 BarrelStaves, 1 BarrelHoops, 1 BarrelLid |
| Seating | Foot Stool | 11 | 9 Board |
| Seating | Stool | 11 | 9 Board |
| Seating | Bamboo Chair | 21 | 13 Board |
| Seating | Wooden Chair | 21 | 13 Board |
| Seating | Fancy cushioned chair | 42.1 | 15 Board |
| Seating | Cushioned chair | 42.1 | 13 Board |
| Seating | Wooden Bench | 52.6 | 17 Board |
| Seating | Wooden Throne | 52.6 | 17 Board |
| Seating | Throne | 73.6 | 19 Board |
| Tables | Nightstand | 42.1 | 17 Board |
| Tables | Writing Table | 63.1 | 17 Board |
| Tables | Yew Wood Table | 63.1 | 23 Board |
| Tables | Large Table | 84.2 | 27 Board |
| Tables | Elegant Low Table | 80 | 35 Board |
| Tables | Plain Low Table | 80 | 35 Board |
| Tables | Short Music Stand | 78.9 | 15 Board |
| Tables | Tall Music Stand | 81.5 | 20 Board |
| Tables | Easel | 86.8 | 20 Board |
| Storage | Wooden Box | 21 | 10 Board |
| Storage | Small Crate | 10 | 8 Board |
| Storage | Medium Crate | 31 | 15 Board |
| Storage | Large Crate | 47.3 | 18 Board |
| Storage | Wooden Chest | 73.6 | 20 Board |
| Storage | Empty Bookcase | 31.5 | 25 Board |
| Storage | Fancy Armoire | 84.2 | 35 Board |
| Storage | Armoire | 84.2 | 35 Board |
| Storage | Plain Wooden Chest | 90 | 30 Board |
| Storage | Ornate Wooden Chest | 90 | 30 Board |
| Storage | Gilded Wooden Chest | 90 | 30 Board |
| Storage | Wooden Foot Locker | 90 | 30 Board |
| Storage | Finished Wooden Chest | 90 | 30 Board |
| Storage | Tall Cabinet | 90 | 35 Board |
| Storage | Short Cabinet | 90 | 35 Board |
| Storage | Red Armoire | 90 | 40 Board |
| Storage | Elegant Armoire | 90 | 40 Board |
| Storage | Maple Armoire | 90 | 40 Board |
| Storage | Cherry Armoire | 90 | 40 Board |
| Household | Small Bed South deed | 94.7 | 100 Board, 100 Cloth |
| Household | Small Bed East deed | 94.7 | 100 Board, 100 Cloth |
| Household | Large Bed South deed | 94.7 | 150 Board, 150 Cloth |
| Household | Large Bed East deed | 94.7 | 150 Board, 150 Cloth |
| Household | Dart Board South deed | 15.7 | 5 Board |
| Household | Dart Board East deed | 15.7 | 5 Board |
| Household | Ballot Box deed | 47.3 | 5 Board |
| Stations | Small Forge deed | 73.6 | 5 Board, 75 IronIngot |
| Stations | Large Forge East deed | 78.9 | 5 Board, 100 IronIngot |
| Stations | Large Forge South deed | 78.9 | 5 Board, 100 IronIngot |
| Stations | Anvil East deed | 73.6 | 5 Board, 150 IronIngot |
| Stations | Anvil South deed | 73.6 | 5 Board, 150 IronIngot |
| Stations | Dressform | 63.1 | 25 Board, 10 Cloth |
| Stations | Spinning Wheel East deed | 73.6 | 75 Board, 25 Cloth |
| Stations | Spinning Wheel South deed | 73.6 | 75 Board, 25 Cloth |
| Stations | Loom East deed | 84.2 | 85 Board, 25 Cloth |
| Stations | Loom South deed | 84.2 | 85 Board, 25 Cloth |
| Stations | Stone Oven East deed | 68.4 | 85 Board, 125 IronIngot |
| Stations | Stone Oven South deed | 68.4 | 85 Board, 125 IronIngot |
| Stations | Flour Mill East deed | 94.7 | 100 Board, 50 IronIngot |
| Stations | Flour Mill South deed | 94.7 | 100 Board, 50 IronIngot |
| Stations | Water Trough East deed | 94.7 | 150 Board |
| Stations | Water Trough South deed | 94.7 | 150 Board |
| Stations | Alchemist Table South deed | 85 | 70 Board |
| Stations | Alchemist Table East deed | 85 | 70 Board |
| Parts | Potion keg | 75 | 1 Keg, 10 Bottle, 1 BarrelLid, 1 BarrelTap |


## Verification

- Native UOContent, project content and integration builds: zero warnings and errors. No client change is required.
- Focused checks create all 66 real outputs, check exact success/failure costs, reject changed/filled/locked/trapped components, cancellations, wrong crafts, remote actions, full packs and forged native requests, and verify crafted-tool saving. Save/restart retains each output's type, parent, origin and weight.
- All 19 custom potion identities are deposited and withdrawn with all three origins. Checks cover exact bottles/doses, mixed identity rejection, 100-dose overflow, partial deposits, missing bottles, full slots, increased keg weight, stale revisions, inaccessible ownership, bank/belt/ground/locked-parent access and dead/frozen actors. Reload checks withdraw and redeposit saved Crafted doses to verify origin persistence.
- Native fit checks exercise house placement and owner restrictions; created private forge furniture does not pass any public workshop gate. House keg access is restricted to co-owners.
- Source and installed cumulative/legacy suites retain previous profession, crafting, identification, socket, gem/rune, drop/MF, Alchemy, meal, timed-effect, economy, class and persistence behavior. Actual ClassicUO verifies book categories/pages, real cancellation, native timed chair crafting, and Protection-potion keg deposit/withdrawal with exact identity/bottles and an empty belt. The book was visually checked at 640×480.
- All 33 Python checks pass. First-loop authoring evidence was refreshed and reproduced; affix values/pools are unchanged. Original questionnaire replies remain unchanged.
- Native patch 0033 adds an opt-out property for potion types that native kegs cannot represent. The complete 33 ModernUO patches/74 paths and 26 ClassicUO patches/36 paths reproduce exactly and reverse cleanly. Native submodule pins are unchanged.
- A full pre-install runtime backup and protected-file comparison preserve all 62 save/configuration/preference files. Source/pre-install/installed/post-live disposable copies preserve both characters, native skills/caps/locks, gear, materials and 485 gold stacks. Production contains only the two normal content assemblies; no test fixtures are installed. Live boots both saved successfully, with a clean shutdown between; the second boot remains running. All 13 client preference/settings payloads and existing town populations are preserved. Local evidence: `.local/logs/t31/` and `.local/logs/deploy-t31/`.

This card adds household objects and storage. House acquisition and private-workshop policy remain later decisions; making a station deed grants no new housing or profession rights. Existing native expansion recipes outside this catalogue retain their previous behavior. The recognition wardrobe remains an earned award.
