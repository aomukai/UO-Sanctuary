# Equipment economy, salvage and imbuing

> **Current design versus installed build — 19 October 2026:** The completed [user replies](economy-open-questions-replies.md) are reconciled in [crafting.md](crafting.md). They supersede earlier target values: new tier-based resale/no buyback, skill/cost-tiered Imbuing, irreversible tinker-added-socket exclusion, stat-specific moulds, new gathering thresholds, manual corpse gold and flat20% carried-gold death loss. None of those changes is claimed installed here. The prototype behavior and deployment evidence below remain historical/implementation facts.

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
| Imbuing | Normal 80/100gold+5essence; Exceptional 100/500+10; Elite 120/1000+20.75/25 Magic/Rare below 120,70/30 at 120; jewellery exceptions. Native quality sockets permitted; tinker-added sockets prohibit later Imbuing | Installed prototype remains skill-free,100gold+5essence and75/25, without socket provenance. Valid-action output/no destruction already matches. |
| Socket creation | Max5 on helm/chest/weapon/shield. Base success100/50/25/5/5%; skill gates, additive tool bonuses, charge-only cost and120intuition retry. Contents permanent | Not installed; no general socket application model. |
| Professions/materials | Physical-material routing; leather/cloth and heavy bone armour to Tailoring. Useful recipes/practice training, crafted quality/material benefits, tool rules | Recipe/progression/repair rewrite not installed. Existing salvage already follows physical material. |
| Salvage/processing | Corresponding tool, no skill/charges/gold; keep current provisional essence/physical recovery. Direct1→10 resource conversion and intended half-weight outputs | Recovery/origin foundation installed. New tool requirement and direct conversions not installed; weight rule needs definition. |
| Runes/gems | Stat fixed by mould; Inscription30–100, new bark45–80/gem40–80 unlocks,5% byproducts. Revised rune formula/quality, prices, legacy gems decorative | Not installed. See [runes and gems](runes-and-gems.md). |
| Vendors/death | Tier sale bands100–500/500–1000/2500–5000, persistent quote, unidentified10%, no buyback; manual corpse gold; flat20% carried-gold death loss | Old resale caps/stock buyback and min(level,20)% death loss remain in code. Drops remain suppressed. |

Tools do not replace repair material/essence costs. Earlier assistant recommendations contrary to the completed replies are not adopted. Most numbers now have starting values; missing fields are identified in the tracker rather than treated as permission to invent a rule.

## Installed prototype resale

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

Use **Imbuing** in Skills or enter **`[Essence`** in chat. Choose Salvage, target an eligible item in your backpack and review its icon/tooltip, rarity, item level, ranges, chances and fee before confirming. Cancel consumes nothing. The item must still be owned, eligible and unchanged when confirmed. A completed quote cannot be replayed.

| Output | Provisional recovery |
| --- | --- |
| Magic Essence from magic gear | 80% chance of 1–2 |
| Magic Essence from rare gear | 90% chance of 2–5 |
| Magic Essence from unique gear | Reserved 95% / 5–12 configuration; **not active** |
| Each physical material | 85% chance of its item-level quantity range |

For each physical material, the initial range is `1–3 + floor((itemLevel - 1) / 10)`. Thus level 1 gives 1–3, level 30 gives 3–5, and level 99 gives 10–12 on a successful recovery roll. Metal, wood, leather and cloth have separate range/chance/level-step settings so they can diverge later. Chance and quantity are rolled independently for essence and the physical material, only after confirmation. No gold or gear is lost for a rejected request, insufficient fee or capacity failure. Successful recovery can be partial or empty; an empty result is not a rejected request and does destroy the input.

| Physical equipment | Resource |
| --- | --- |
| Plate, chain/ringmail, metal weapons and jewellery | Metal Scraps |
| Wooden shields/armour, bows/crossbows, wooden staves/wands and explicit wooden melee bases | Wooden Chips |
| Leather/studded leather armour and leather clothing/footwear | Leather Strips |
| Cloth clothing and cloth armour | Cloth Scraps |

Weapons with mixed construction supply their primary material, not simultaneous wood and metal. Native weapon resource fields default to iron even for some wooden bases, so the physical weapon family and explicit wooden exceptions are used. Native armour material metadata distinguishes chain, leather, wood and cloth; clothing uses its leather resource or cloth fallback. Glass/stone/bone weapon exceptions and bone/scale/stone armour have no new physical resource in this pass; magic/rare examples can still recover essence. Cosmetic metal/leather/cloth pieces can recover physical material without acquiring combat affixes. Uncatalogued tools, furniture and raw resource recycling are outside this gear pass.

Unprofiled converted gear starts at item level 1; player level, UO fame, native rarity labels and material value are not substituted for item level. Current gear state supports Normal, Magic and Rare only. Native UO artifact names are not treated as project Unique rarity. The reserved unique yield becomes meaningful only when actual unique item identities and their rarity policy exist.

Protected, insured, quest/nontransferable, equipped, dragged and starter-protected items are excluded. Existing vendor-origin gear is excluded even after player trading. Gambling rewards will be eligible when generated with the already-agreed Drop origin; no gambling vendor is added now.

Native scissors, forge resmelting and salvage bags cannot recycle converted equipment through their old deterministic paths. They direct players to the common salvage action, which also closes alternate vendor-gear extraction routes. Non-equipment native resource processing retains its existing behavior. The optional hooks delegate to native behavior if project content is absent.

All recovered materials are real stackable items, created with Crafted origin, carried with weight and saved with their quantities/types/origin. Matching stacks in the main backpack merge automatically. Capacity checks reserve space for all rolled outputs before any input is consumed; they deliberately require spare slots/weight without counting the gear that will be destroyed. A full pack can therefore reject an otherwise space-neutral salvage. Outputs never spill to the ground on rejection.

## Installed essence-funded imbuing

The other menu action targets an eligible, unprotected dropped/crafted/gambled **white** base. Imbuing costs a provisional **5 Magic Essence plus 100 backpack gold** and rolls **75% Magic / 25% Rare**, followed by the existing legal affix roller. It does not have a separate destructive-failure roll. Invalid inputs, unavailable affixes or missing resources consume nothing. An imbued item cannot be re-imbued by this action.

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
