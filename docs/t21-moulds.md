# T21 — Known-stat moulds and ordinary drops

Implemented and deployed 20 October 2026 JST.

Every eligible, credited monster life at level20–99 gets one independent **0.1%** roll for **one mould**, alongside its ordinary gold/gear result. Magic Find does not modify the mould chance or stat weights. The user confirmed the separate roll and chose Diablo affix frequencies rather than equal semantic-stat odds. The existing reward-life claim runs first: no repeat moulds from a spent/revived life, no rewards from summons, controlled creatures, vendors, no-awards or uncredited kills. Pet kills retain owner credit, including a distant/dead owner. Zero XP at the cap or from a level penalty does not suppress moulds.

The source monster's level selects exactly one band:20–29 Crude,30–39 Rough,40–49 Shaped,50–59 Etched,60–69 Refined,70–79 Polished,80–89 Flawless,90–99 Perfect. Difficulty alone does not choose the tier. Existing Normal/Nightmare/Hell identities use their saved reward level. All eight tiers exist; the current roster's85 ceiling leaves natural tier8 supply for future90–99 encounters. No new spawns or encounter grades were placed. The persisted grade field is Ordinary; Champion/Unique/SuperUnique are reserved for T35, where the agreed1/5/10% rates belong.

`tools/build_mould_catalogue.py` reproduces `data/Sanctuary/Balance/moulds.json` from pinned equipment/affix/type catalogues (source commit45112569deb9384738ccafe5c24ebbb71f41c7c9). It considers affix rows applicable to a currently mapped equipment type at some level1–99, excluding charm/jewel-only rows. Each distinct source row contributes its frequency **once**, regardless of how many UO appearances or base types admit it. Higher source affix ranks contribute their frequency to their semantic family; the monster band controls the mould tier rather than source affix rank. The same15-family pool applies to every tier, with total weight430. This is a custom mould adaptation of source frequencies, not a native D2 mould table.

Each mould declares **one working percentage property**. Speed/recovery aliases collapse into semantic families. Baselines use the lowest positive eligible equipment value; source names prefer an actual suffix at equal minimum. Exact suffixes produce “Crude Mould of the Apprentice”/“Flawless Mould of Chance”; prefix-only categories retain their canonical adjective, e.g. “Crude Sturdy Mould”. Attack rating/block extract only their working percentage component from a compound source row; light radius/faster block rate are not copied. No flat, binary, proc, charge or unsupported effect enters the pool, and ordinary gear affix eligibility/value rolls remain unchanged.

| Stat | Canonical source name | Baseline | Weight /430 |
| --- | --- | ---: | ---: |
| Enhanced defense | Sturdy | 10% | 58 |
| Enhanced physical damage | Jagged | 10% | 127 |
| Attack rating | of the Sun | 5% | 1 |
| All resistances | Shimmering | 3% | 52 |
| Fire resistance | Crimson | 5% | 19 |
| Cold resistance | Azure | 5% | 19 |
| Lightning resistance | Tangerine | 5% | 19 |
| Poison resistance | Beryl | 5% | 19 |
| Increased attack speed | of Readiness | 10% | 29 |
| Faster cast rate | of the Apprentice | 10% | 11 |
| Faster run/walk | of Pacing | 10% | 19 |
| Chance to block | of Blocking | 10% | 13 |
| Faster hit recovery | of Balance | 10% | 12 |
| Extra gold from monsters | of Greed | 25% | 8 |
| Better chance of finding magic items | of Chance | 5% | 24 |

The frozen baseline previews the agreed future rune formula: `baseline + tier − 1 + quality`, where Average/High Quality/Superior add0/1/2 percentage points. Inscription and Runebark consumption remain T23; filled-socket requirements/effects remain T25/T26. T21 only supplies collectible patterns and never performs rune crafting or socket insertion.

`RuneMould` is a regular, movable, non-stackable1-stone Drop item. Its name, tier/name, stat/code/label, baseline, source affix/name/kind, monster level, difficulty and reserved grade are saved on the item with serialization generator version0. Reload and native copy/dupe/trade preserve the stored declaration and values; current balance rows do not reroll old moulds. Private identity setters and the native copy hook keep duplication consistent. The item has no gear metadata or equip requirement and cannot enter gear Imbuing/salvage. Standard corpse loot rights, capacity checks and pickup apply.

`MouldChecks.cs` checks all120 identities, all level boundaries, every chance ticket (exactly10/10000 wins), every frequency ticket, compound-stat isolation, minimum/quality formula, native copy/trade/reload, full-pack refusal/pickup, Normal/Nightmare/Hell source levels, MF independence, cap/zero-XP entitlement, pet/dead-owner/exclusion/revival claims, gold coexistence and spent-life reload. `MouldUiChecks.cs`/`check_mould_ui.py` exercise an actual mouse kill, declared tooltip and corpse-grid pickup; the rare roll is fixture-seeded while the production reward path is used. Evidence roots: `.local/logs/t21/` and `.local/logs/deploy-t21/`.

Content/integration builds have0 warnings/errors. Source and installed four-boot cumulative suites, the actual client flow, separate legacy migration,24 Python checks and refreshed authoring evidence pass. Both live boots/save snapshots pass with a clean intermediate shutdown. Pre/source/installed/post-live copies preserve Players/Gear/Materials/Gold exactly (2/1/1/485 records).61 protected payloads were retained at install, and34 non-save files remain unchanged, including12 client profile files plus settings. Application/native content/engine/logger/client binaries are unchanged; only the two production assemblies are listed, with no integration world or fixtures installed. Full rollback: `.local/backups/pre-t21-20261020-143919`. Manifest: `.local/logs/deploy-t21/deployment.json`.
