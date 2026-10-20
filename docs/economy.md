# Equipment economy, salvage and imbuing

> **Current design versus installed build — 19 October 2026:** The completed [user replies](economy-open-questions-replies.md) are reconciled in [crafting.md](crafting.md). They supersede earlier target values: new tier-based resale/no buyback, skill/cost-tiered Imbuing, irreversible tinker-added-socket exclusion, stat-specific moulds, new gathering thresholds, manual corpse gold and flat20% carried-gold death loss. The current rows and linked card documents record implemented changes; the older prototype/deployment history below remains historical evidence.

18 October 2026. These notes distinguish the installed equipment-economy prototype from the user's design handoffs. The later [crafting handoff](../sanctuary-crafting.md) supersedes conflicting parts of the [older economy draft](../sanctuary-economy.md); subsequent explicit user confirmations settle single-use moulds and nine gem families. The handoffs have now been reconciled. This document is not authorization to implement every proposed sink, profession recipe or drop table.

## Agreed direction and scope

Gold should retain purchasing power, ordinary short outings should support NPC expenses and savings, and professions should provide useful alternatives without becoming compulsory maintenance. NPC loot purchases create gold; player trades transfer existing gold. Meaningful optional improvement attempts can consume equipment and resources. Reliable repair and profession training remain separate work.

This pass reduces NPC **equipment resale** and adds a playable salvage/imbuing comparison. It introduces one magical ingredient, Magic Essence, plus Metal Scraps for Tinkering, Wooden Chips for Cooking, Leather Strips and Cloth Scraps for future material recipes. The latter four are stored/tradeable resources; their profession recipes, direct-from-raw-resource recipes, skill requirements and gains are not implemented here. Existing crafting/gathering behavior is not replaced wholesale.

The user explicitly chose:

- Only dropped, crafted and gambling-generated equipment can be salvaged. Ordinary vendor equipment can be sold or discarded; trading or buyback must not change its origin.
- Salvage can recover both Magic Essence and a primary physical material, with independent chance rolls and variable quantities. A completed attempt destroys the selected item even when all recovery rolls fail.
- Rarity determines the essence range. Item level determines the material range. White equipment yields no essence, but can yield material.
- Physical material determines the resource, independently of armour weight. Leather armour happens to be Light in the current catalog and chain/ringmail Medium; this does not make leather produce cloth or metal produce leather.

## Latest target rules versus current implementation

The [current specification](crafting.md) contains the full numeric tables; [the tracker](economy-open-questions.md) retains partial questions and delegated authoring.

| System | First-implementation target | Installed comparison status |
| --- | --- | --- |
| Repair/recharge | Exact matching processed materials plus corresponding scraps where specified; Magic essence ranges by base tier, Rare double, white none; recharge1essence/charge. Skill-based max-durability loss and saving tables, chitin→Powder of Fortifying restoration | Revised repair/recharge/restoration system not installed. Floors, scrap quantities, saving scope and restoration amounts still need definition. |
| Imbuing | Normal 80/100gold+5essence; Exceptional 100/500+10; Elite 120/1000+20.75/25 Magic/Rare below 120,70/30 at 120; jewellery exceptions. Native quality sockets permitted; tinker-added sockets prohibit later Imbuing | [T17](t17-imbuing.md) implements the full tier/grade ladders, ladle/cauldron,120 odds and socket-provenance restriction. |
| Socket creation | Max5 on helm/chest/weapon/shield. Base success100/50/25/5/5%; skill gates, additive tool bonuses, charge-only cost and120intuition retry. Contents permanent | [T20](t20-sockets.md) adds empty sockets and destructive attempts; rune/gem insertion remains T25/T26. |
| Professions/materials | Physical-material routing; leather/cloth and heavy bone armour to Tailoring. Useful recipes/practice training, crafted quality/material benefits, tool rules | Recipe/progression/repair rewrite not installed. Existing salvage already follows physical material. |
| Salvage/processing | Corresponding tool, no skill/charges/gold; retain independent essence/physical recovery. Raw processing halves weight; processed→ten scraps preserves it | [T07](t07-processing.md) processing and [T16](t16-salvage.md) corresponding-tool salvage are implemented and deployed. |
| Runes/gems | Stat fixed by mould; Inscription30–100, new bark45–80/gem40–80 unlocks,5% byproducts. Revised rune formula/quality, prices, legacy gems decorative | Not installed. See [runes and gems](runes-and-gems.md). |
| Vendors/death | Tier sale bands100–500/500–1000/2500–5000, persistent quote, unidentified10%, no buyback; manual corpse gold; flat20% carried-gold death loss | [T10](t10-rewards.md), [T11](t11-vendors.md) and [T13](t13-death-gold.md) implement these rules. T19 adds mapped Normal gear outcomes to the shared reward draw. |

Tools do not replace repair material/essence costs. Earlier assistant recommendations contrary to the completed replies are not adopted. Most numbers now have starting values; missing fields are identified in the tracker rather than treated as permission to invent a rule.

The subsequent [T13 implementation](t13-death-gold.md) replaces the historical level-based death penalty below with flat20% of backpack gold, rounded down once, with a saved corpse claim; bank gold and XP remain safe.

The subsequent [T11 implementation](t11-vendors.md) replaces the prototype resale/stock behavior below. The old percentage/cap settings are removed; current prices live in `market-settings.json` and `market-supplies.json`.

## Historical prototype resale

`data/Sanctuary/Balance/economy.json` contains the comparison settings. Converted equipment pays `floor(native NPC offer * resalePercent / 100)`, with a minimum of one gold for an already-positive native offer and a rarity cap. A zero native offer remains zero.

| Setting | Provisional value |
| --- | ---: |
| Resale percentage | 10% |
| Normal / cosmetic cap | 10 gold |
| Magic cap | 25 gold |
| Rare cap | 50 gold |

These caps are ceilings, not guaranteed rarity prices. Many inexpensive items sell for one gold. Existing vendor assortments, accepted item types, stock prices, buyback asking prices and non-equipment goods retain native pricing. This is a gear-resale reduction, not a final global income/price rebalance.

The optional UOContent `ClassEconomyHooks` hook is applied at the two native vendor offer/payment call sites; the same resolved quote is used for both. Native `GenericSellInfo.GetBuyPriceFor` still uses the original valuation, so reducing NPC purchase offers does not make buyback stock arbitrarily cheap. Existing item-origin persistence is reused; no sale or salvage action rewrites an existing item's origin.

## Installed salvage rolls and material selection

Double-click the corresponding production tool and choose **Salvage gear**: smithing hammer for metal gear, fletching tools for wood, tailoring kit for leather/cloth/bone, tinker's tools for jewellery, and a scribe's pen for magical spellbooks. Target eligible backpack gear and review its icon/tooltip, known rarity/item level and recovery ranges (hidden for unidentified gear), zero cost and contents-destruction warning before confirming. No matching skill, active craft, tool uses, gold or town facility is required; the functional tool remains required. The **Imbuing** / **`[Essence`** menu remains a shortcut and binds the same actual tool. See [T16](t16-salvage.md). Cancel consumes nothing. The item must still be owned, eligible and unchanged when confirmed. A completed quote cannot be replayed.

| Output | Provisional recovery |
| --- | --- |
| Magic Essence from magic gear | 80% chance of 1–2 |
| Magic Essence from rare gear | 90% chance of 2–5 |
| Magic Essence from unique gear | Reserved 95% / 5–12 configuration; **not active** |
| Each physical material | 85% chance of its item-level quantity range |

For each physical material, the initial range is `1–3 + floor((itemLevel - 1) / 10)`. Thus level 1 gives 1–3, level 30 gives 3–5, and level 99 gives 10–12 on a successful recovery roll. Metal, wood, leather and cloth have separate range/chance/level-step settings so they can diverge later. Chance and quantity are rolled independently for essence and the physical material, only after confirmation. Rejected, cancelled, stale or capacity-failed requests preserve gear and contents and spend no gold or uses. Newer quotes and reconnects invalidate old salvage confirmations. Tool identity/ownership/parent/uses, native wear/material/charges, item state and attached contents are rechecked. Successful recovery can be partial or empty; an empty result is not a rejected request and does destroy the input.

| Physical equipment | Resource |
| --- | --- |
| Plate, chain/ringmail, metal weapons and jewellery | Metal Scraps |
| Wooden shields/armour, bows/crossbows, wooden staves/wands and explicit wooden melee bases | Wooden Chips |
| Leather/studded leather armour and leather clothing/footwear | Leather Strips |
| Cloth clothing and cloth armour | Cloth Scraps |

Weapons with mixed construction supply their primary material, not simultaneous wood and metal. Native weapon resource fields default to iron even for some wooden bases, so the physical weapon family and explicit wooden exceptions are used. Native armour material metadata distinguishes chain, leather, wood and cloth; clothing uses its leather resource or cloth fallback. Glass/stone/bone weapon exceptions and bone/scale/stone armour have no new physical resource in this pass; magic/rare examples can still recover essence with their family's tool. Filled contents and native wand charges are destroyed with the host and add no yield. Cosmetic metal/leather/cloth pieces can recover physical material without acquiring combat affixes. Uncatalogued tools, furniture and raw resource recycling are outside this gear pass.

Unprofiled converted gear starts at item level 1; player level, UO fame, native rarity labels and material value are not substituted for item level. Current gear state supports Normal, Magic and Rare only. Native UO artifact names are not treated as project Unique rarity. The reserved unique yield becomes meaningful only when actual unique item identities and their rarity policy exist.

Protected, insured, quest/nontransferable, equipped, dragged and starter-protected items are excluded. Existing vendor-origin gear is excluded even after player trading. Gambling rewards will be eligible when generated with the already-agreed Drop origin; no gambling vendor is added now.

Native scissors, forge resmelting and salvage bags cannot recycle converted equipment through their old deterministic paths. They direct players to the common salvage action, which also closes alternate vendor-gear extraction routes. Non-equipment native resource processing retains its existing behavior. The optional hooks delegate to native behavior if project content is absent.

All recovered materials are real stackable items, created with Crafted origin, carried with weight and saved with their quantities/types/origin. Matching stacks in the main backpack merge automatically. Capacity checks reserve space for all rolled outputs before any input is consumed; they deliberately require spare slots/weight without counting the gear that will be destroyed. A full pack can therefore reject an otherwise space-neutral salvage. Outputs never spill to the ground on rejection.

## Installed essence-funded imbuing

The other menu action targets an eligible, unprotected dropped/crafted/gambled **white** base. [T17](t17-imbuing.md) requires an owned ladle, active Imbuing, the matching base skill and a public cauldron. Equipment tiers require80/100/120 skill,100/500/1000 gold and5/10/20 essence; jewellery uses its five-grade ladder. Outcomes are75% Magic /25% Rare below120 and70% /30% at120, followed by ordinary eligible affix rolls. It does not have a separate destructive-failure roll. Invalid inputs, unavailable affixes or missing resources consume nothing. An imbued item cannot be re-imbued by this action.

The same item is updated in place. Its identity, base, base defense, item level, origin, material, name/hue and durability remain; affixes and their resulting requirements/stats change. Native socket fields are not replaced, and no new socket system is introduced. The pending roll is validated before both costs are consumed together on the game loop. No materials or affixes are saved in a UI-only state.

These installed prototype actions work without a skill threshold and do not grant skill gains; the new E01 skill gates and practice progression are target design, not installed behavior. Profession skill requirements, training pace, tool charges, socketing attempts, raw-resource conversion and repair changes require their later design pass. They are not silently inferred from the economy handoff.

At these settings the expected recoverable essence from a freshly imbued result is `0.75 * (0.8 * 1.5) + 0.25 * (0.9 * 3.5) = 1.6875`, before considering the consumed white base, against an input of five essence and 100 gold. This is an initial check against a self-feeding essence loop, not a claim that all future material recipes or markets are balanced.

## Rune and gem design follow-up

The [current rune/gem design](runes-and-gems.md) now follows replies G/H: moulds specify the property, bark unlocks45–80, gem qualities40–80, and one 5% find roll occurs per successful harvest. Runes use a provisional category-minimum-plus-tier/quality formula rather than universal 1–8%. Socket/filled-slot rules, tier-level drops and quality gates are in the specification. Nine gem families/single-use moulds remain confirmed. Family mapping, extra flat effect and some recipe/delivery details remain open. None of this is claimed installed by the economy prototype.

## Reconciliation with the handoff

Already implemented foundations include the fixed Enter-to-chat controls, tabard belt capacities/overflow, restoration potions and provisional 20% bottle recovery, saved item origins, common monster XP, owner credit and revived-life suppression. The current death implementation removes `min(level, 20)%` of carried/corpse gold and leaves bank gold alone; the handoff's final death-cost formula remains an open design question, so this pass does not change that implementation or adopt it as a permanent economy target. No XP death penalty is added.

Ordinary equipment stock redesign, gambling, repair tools/reliability/prices, profession recipes/gains, potion buffs/food, mercenary systems, housing prices and final drop tables remain outside this pass. Monster death loot, including gold, remains disabled. Consequently an outing-income/expense budget cannot yet be claimed as validated. Existing world/account/progression data must be preserved during installation.

`[EconomyTest` is an explicit GM-only comparison kit with representative gear, an ineligible vendor item, 20 essence and 500 gold, added only when the command is invoked and space allows. Nothing is granted automatically on login or world load.

## Verification

Server checks exercise actual NPC sale payments; unchanged stock/buyback and non-equipment pricing; all four physical material families, including chain versus leather and wooden armour; rarity/item-level quantity bounds; ordinary random seeds with zero/min/max outcomes; independent recovery rolls; zero-yield destruction; full-pack and missing-resource rejection; invalid ownership/protected/equipped/vendor inputs and stale/replayed quotes; native scissors/forge/salvage-bag bypass prevention; both imbuing rarity branches; atomic costs and retained item identity/material/durability/level; and material/imbued-state persistence over four boots.

The real SDL client receives native menu/quote gumps and sends selection, cancel and confirm replies. Target selections are supplied by the disposable console fixture through the normal server Target callback; this does not claim to retest backpack pixel picking. Screenshots are inspected for the menu and destructive/imbue confirmations. No client or protocol change is required.

After building the native UOContent hook, project content and integration assembly, reproduce server checks in a disposable world:

```sh
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_ECONOMY=1 \
python3 tests/check_britain.py
```

Client checks additionally use `SANCTUARY_TEST_UI=1 SANCTUARY_TEST_COMBAT_UI=1 SANCTUARY_TEST_ECONOMY_UI=1 SANCTUARY_TEST_UI_STATE="$PWD/.local/logs/economy-ui-state.json"`. Do not rebuild DLLs while a disposable server maps them. Never install the integration assembly or test fixtures into the live world. Final validation passed: native/content/integration builds with zero warnings/errors; all nine Python helper/authoring tests; 13 ModernUO patches reproducing 29 files and 19 unchanged ClassicUO patches reproducing 34 files; native-client visual/confirm/cancel checks; and the combined economy, creature XP/revival, potion-belt, recovery, pets, combat, resurrection, gear/origin/class checks over four boots. The final combined suite used the unchanged live application/core with the newly compiled UOContent and project content, matching the installed combination.

Evidence: `.local/logs/economy-combined-tests.log`, `economy-combined-server.log`, `economy-ui-tests.log`, `economy-ui-server.log`, `economy-helper-tests.log` and the corresponding native/content/integration build logs. Screenshots: `economy-menu.png`, `economy-salvage-confirm.png`, `economy-imbue-confirm.png`, `economy-vendor-rejected.png` under the same local logs directory. The final UI quotes were visually inspected, including the selected-item icon/rarity/level and full warning/cost text.

## Installed comparison build

Installed after a fresh save and clean shutdown, with backup `.local/backups/pre-economy-20261018-185348/`. All 91 protected save/configuration/client-preference files were byte-identical during installation. Live reload/status/save passed with the same town identities and all 75 progression/configuration/preference payloads preserved; only the normal Accounts.idx save-start timestamp changed. UOContent/project content and all installed catalogs match their built outputs. Existing application, core, logger and client binaries stayed byte-identical. No test assembly, fixture, test account, automatic kit, gear reset or XP reset was installed. The server is running; the client remains closed.

Deployment record: `.local/logs/economy-deploy.json`. Live server log: `.local/logs/server-economy.log`. At that deployment, the original `sanctuary-economy.md`, prior gear/potion/creature catalogs and XP-curve/progression source retained their recorded hashes. The subsequent document reconciliation changes the handoffs and documentation only; it does not alter the installed build, catalogs or world state. Final yields, prices, income budgets, recipes, skill progression and unique items remain future work.


## Identification — T18

[T18](t18-identification.md) is deployed. Scribes sell single-use Identify scrolls for80gold; Inscription can craft them from1BlankScroll+1BlackPearl at a public desk with a pen (0–50 band). A successful own-backpack reveal consumes the scroll and changes only the saved identification flag. New magical drops start hidden and cannot equip; crafted/imbued/ordinary shop items are known, and existing items retain their state. Unidentified resale pays10% of the persistent identified offer. Native tooltips/labels and salvage/repair views respect hidden state. Charged tomes remain T34; [T19](t19-gear-drops.md) supplies the mapped Normal encounter gear tables.


## Production gear drops and Magic Find — T19

[T19](t19-gear-drops.md) uses the existing mapped Normal reward roster and pinned players-1 treasure tables. Gold and gear share one terminal draw; unsupported bases, later-system branches and successful Set/Unique rolls stay empty without rerolls. White subqualities produce ordinary white bases. New Magic/Rare drops are unidentified and use the existing identify, resale, equip and salvage paths. Equipped working numeric/material Magic Find affects quality selection with the source diminishing returns; the status panel displays its active total. Full Set/Unique identities, distinct dropped-white quality bonuses and higher-difficulty encounters remain separate work.


The subsequent [Nightmare/Hell extension](t19-difficulties.md) adds explicitly spawned persistent variants with matching numeric stats/XP/loot. Existing populations remain Normal and placement is deferred. The higher treasure tables retain source group/level upgrades and the same empty Set/Unique policy.

[T21 known-stat moulds](t21-moulds.md) add one independent0.1% ordinary reward roll with source-frequency stat weights. Rune crafting/Runebark and socket insertion remain later cards.
