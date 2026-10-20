# T37 — Treasure expeditions, keys and rare recipes

Treasure maps uncover private temporary dungeons for their owner and pets. Each dungeon joins hand-built rooms with randomized connections. Its monsters, layout, defenses and chest contents remain fixed through leaving, logout and server restarts.

## Difficulty and deciphering

| Map | Monster map-drop band | Cartography | Dungeon monsters | Shared defenses |
|---|---:|---:|---|---|
| Stash | 1–20 | 27 | Normal 15–20 | No immunities |
| Supply | 21–40 | 70 | Nightmare 30–40 | No immunities |
| Cache | 41–60 | 90 | Nightmare 50–60 | No immunities |
| Hoard | 61–80 | 100 | Hell 70–80 | One random immunity and a different high Fire, Lightning or Cold resistance |
| Trove | 81–99 | 100 | Hell 85–99 | Two distinct random immunities |

Each eligible reward life has one separate 1% map roll, independent of Magic Find and encounter grade. Existing summon, pet, vendor and duplicate-life exclusions apply. The map's source level selects its tier; it does not force every dungeon monster to that source level.

Deciphering requires active Cartography and the listed trained skill, with a normal skill/gain check through 60 points above the threshold. Deciphered maps are blessed. Reading one reveals difficulty, monster levels and the exact shared defenses. Those defenses are rolled when the map is created and saved; repeated deciphering cannot change them. A deciphered map can be traded, but the hunter still needs its Cartography threshold.

Immunities are selected from Physical, Magic, Fire, Lightning, Cold and Poison. Hoard's extra elemental resistance adds 75 points, capped at 99; it cannot become another immunity. Trove applies its chosen pair to every monster. Other resistances are capped below immunity, including after elite modifiers, so these tiers retain their announced immunity counts. Lower tiers likewise cannot acquire an immunity from elite modifiers.

## Finding and exploring

Carry the deciphered map and a digging tool, select **Search for entrance**, and target ground within six tiles. Mining retains the native search tolerance of 1/2/3/4 tiles at 0/51/81/100 trained skill; an active World survey adds five. Occupied ground, houses and blocked arrival areas do not consume a map or move their occupants.

An accepted search reserves one expedition and spends that map. It builds 8–12 rooms from three authored 23×17 masks, with room centers at least 48 tiles apart and five-wide corridors. Each room beyond the entrance holds a spaced pack; the last holds a random unique and five minions guarding the chest. Encounters use the existing difficulty statistics, XP, combat AI, elite modifiers and rewards, with levels constrained to the map's band. Existing permanent world populations are unchanged.

The entrance and exit require an explicit owner double-click. Pets accompany the owner; other players cannot enter or loot the chest. Teleport cannot escape the generated floor or enter another hunt. Dungeon surveys recognize the expedition. Re-entering does not respawn enemies or refill loot.

The two-hour lifetime starts when the entrance is uncovered and advances offline. Layout, partial construction, monster injuries/deaths, chest contents/unlock state and expiry are saved. Expiry returns the owner, their pets, player corpses and recoverable loose belongings to the entrance. Offline owners receive a corrected login position. Loose belongings go into an owner-only recovery bag; unclaimed generated chest loot and monster corpses are removed. Construction failure cleans up and refunds the map.

Allocation uses nine bounded 224×224 slots per Britannian facet in unused Green Acres space. A slot is skipped if it contains objects/mobiles or its planned floor is physically obstructed. Saved sessions retain their actual origin. Exhaustion leaves the map unchanged.

## Keys and rewards

| Chest | Key | Tinkering | One ingot | Gold | Gear rolls / item level | Gem | Resource stack | Recipe chance |
|---|---|---:|---|---:|---|---|---:|---:|
| Stash | Primitive | 25 | Iron | 1,000 | 1 / 20 | Chipped | 20 | 20% |
| Supply | Clunky | 50 | Shadow Iron | 2,000 | 2 / 40 | Flawed | 40 | 40% |
| Cache | Simple | 75 | Bronze | 3,000 | 3 / 60 | Standard | 60 | 60% |
| Hoard | Intricate | 100 | Agapite | 4,000 | 4 / 80 | Flawless | 80 | 80% |
| Trove | Splendid | 120 | Valorite | 5,000 | 5 / 99 | Perfect | 100 | 100% |

Craft keys from the Tinkering tool book at a public workshop. Ordinary keys use the required-skill-to-+50 curve and normal gains; Splendid requires its learned recipe and succeeds at 120. Success and failure each consume the one ingot and one tool use, with crafted-tool saving. Cancelled, stale, disconnected or undeliverable work spends nothing.

A key opens one matching-tier chest after its guarding pack is defeated. Only successful unlocking consumes it. Lockpicking and Remove Trap are not required. Unlocking/opening cannot generate another reward.

Chest gear draws use existing mapped weapon/armor treasure pools, base weights and rarity/Magic Find rules. Magic Find is captured when the entrance is uncovered. Successful reserved Set/Unique rolls stay empty without replacement. Each chest contains one random usable socket gem at its listed quality—an explicitly approved exception to mining-only usable gem supply—and one random stack of iron ingots, boards, leather, cloth or bone. Values are configurable in `data/Sanctuary/Balance/treasure-hunts.json`.

## Rare recipes

One successful recipe roll awards one uniformly selected tradable recipe, including duplicates. The catalogue includes all current skill-120 equipment recipes, tier-three meals, the Protection potion, Splendid keys and Elite equipment Imbuing. A character learns a recipe permanently by reading it in their backpack; an already learned duplicate is retained for trading. Knowledge belongs to each character, survives class/profession changes and does not grant skill or bypass ordinary tools, facilities or costs. Both quote and completion enforce recipe knowledge.

## Native map and fishing adaptation

The five modern named map tiers follow the [official UO treasure-map and Cartography references](https://uo.com/wiki/ultima-online-wiki/gameplay/treasure-maps/). Existing native maps adapt as levels 0–1 Stash, 2–3 Supply, 4–5 Cache, 6 Hoard and 7 Trove, retaining their site and decoder; completed maps cannot create fresh expeditions. New monster maps directly use the five-tier level bands.

Fishing retains the pinned native harvest rules. Deep-water special mutation checks require trained Fishing 80 for special nets/big fish, 90 for land treasure maps and 100 for bottles. Checks are sequential, using the native `(effective skill − 80) / 4000` probability per eligible branch; normal fish/footwear and native skill checks remain. Fished land maps enter the new expedition flow, and special catches survive both native packing and corpse-loot suppression without enabling unrelated native loot.

[Message-in-a-bottle SOS salvage](https://uo.com/wiki/ultima-online-wiki/skills/fishing/sunken-treasure/) remains a separate native sea route, not a second copy of this land-chest table. The native ordinary net spawns 2–5 creatures (the special hue uses 1–3), each choosing equally among Sea Serpent, Deep Sea Serpent, Water Elemental and Kraken. A net-spawned Kraken separately has a 20% bottle chance; the Kraken's existing before-death hook has its own 5% bottle chance, and its native UOR constructor supplies a fishing net. Owned pet inventories are retained; summoned reward lives remain excluded. No new kraken rate, combat difficulty or sea reward table is invented for T37. The source references are pinned `Fishing.cs`, `SpecialFishingNet.cs`, `Kraken.cs` and `TreasureMap.cs`; official [Fishing](https://uo.com/wiki/ultima-online-wiki/skills/fishing/) describes the separate sea activity.

## Verification

Integration checks cover 100 generated layouts, connectivity and pack spacing; exact drop boundaries; all reward tiers; recipe learning and duplicate retention; key costs/guarantees; private entry, guarded unlocking and once-only use; immunity counts and level bands; and four-boot persistence plus offline expiry/recovery. Actual-client checks cover key crafting, immunity disclosure, entrance search, room movement, unlocking and exit. Cumulative and legacy suites, catalogue reproduction, Python tests, native patch round trips and copied-save comparisons form the deployment checks.

## Deployment checkpoint — 21 October 2026

Installed and live-save/restart verified. Final source focused/cumulative/legacy suites and installed cumulative/legacy/client suites pass, including native Fishing give → serpent inventory → corpse retention. Catalogue reproduction, 41 Python tests and exact round trips for 37 ModernUO and 30 ClassicUO patches pass. Pre-install, final-installed and post-live copied saves preserve both characters, all recorded skills/caps/locks and gear/material/gold snapshots. All 63 protected payloads were retained at staging; 34 non-save payloads include 12 client preference files.

The full backup is `.local/backups/pre-t37-20261021-052942`; local evidence is under `.local/logs/t37` and `.local/logs/deploy-t37`. Both actual live boots saved successfully, with a clean shutdown between; the second boot remains running. Engine, logger and application are unchanged. The native content routes treasure maps and fishing cargo, while the client recognizes generated zero-height floor tiles for click/held movement. Production contains only UOContent and Sanctuary.Content. Existing permanent populations are unchanged.
