# Sanctuary crafting and gathering

> **Superseded handoff — 19 October 2026 reconciliation:** The body below preserves the earlier draft/history. The completed [user replies](docs/economy-open-questions-replies.md) and [current first-implementation specification](docs/crafting.md) replace conflicting values, rules and unanswered-question labels. The [decision tracker](docs/economy-open-questions.md) identifies genuinely remaining parts. This historical body must not be used to restore old socket costs, random-stat rune crafting, gathering thresholds, resale caps or charge-saving tomes. Current explicit user clarifications continue to take precedence.

Design handoff for Sol — 18 October 2026 (JST)

Suggested repository path: `docs/crafting.md`.

This consolidates the latest conversation decisions. It is not a verification of current code. Reconcile with the repository's implementation notes and economy document. Where older notes conflict, the latest explicit decisions below take precedence. Do not implement unresolved suggestions as settled rules.

## Document precedence and implementation status

Reconciled with the user's subsequent explicit corrections on 18 October 2026. Current explicit user decisions take precedence over this handoff, which supersedes conflicting passages in [the older economy draft](sanctuary-economy.md). In particular, **one mould is consumed per rune** and **all nine native UO gem families are retained**, replacing the older mould uncertainty and seven-family count below. Those passages have been corrected in this revision.

[Equipment economy](docs/economy.md) records the implemented resale/salvage/imbuing prototype. [Runes and gems](docs/runes-and-gems.md) records the specialist socketable design and code audit. A design decision does not imply implementation, and a configured prototype number is not automatically a final balance decision.

The assistant's earlier recommendations to use tool charges alone for repairs and to reject ordinary maximum-durability loss are withdrawn. Repair routing by physical material was unselected at the time of this handoff; the later reply A08 now explicitly adopts it, with bone as heavy Tailoring armour. Other suggestions remain recommendations unless explicitly selected.

## 1. Goals

**Subsequent A03 direction:** [Wardrobe progression](docs/wardrobe-progression.md) records independent class/profession histories, one selected class and **one selected crafting job**, with **Mining/Lumberjacking/Fishing always active**. Cartography produces maps and provides the treasure-hunting prerequisite; it is a selectable craft and qualifies for Pillar of Society. The recognition unlock requires class level 80 OR **crafting** skill 100; gathering does not qualify. Public town-hall wardrobe access follows the unlock. Swaps are free/no-cooldown, require being alive/out of combat, and may leave a weaker class overweight under normal encumbrance. Skill caps start at 100 and rise to 105/110/115/120 through rare dungeon-boss Power Scrolls. Town crafting facilities start without usage fees; private workshops remain undecided and portable exceptions must be explicit. Butchering/skinning requires no skill. These are design changes, not an installed migration; Q01–Q10 retain unresolved details separately from recommendations.

- Professions gain skill through use, independently of combat class progression.
- Training should produce useful items, consumables or salvage, rather than hundreds of worthless objects.
- Gathering, combat drops and crafting should supply each other.
- Ordinary NPC services and shop stock provide an accessible fallback. Crafting offers savings, better products and customisation.
- The game should support short, relaxed sessions. Potions are brief combat interventions; food provides longer convenience benefits.
- Keep recipes, yields, prices, thresholds, odds and durations configurable. Many numbers below are starting proposals.

## 2. Critical corrections and precedence

1. **Imbuing has no destructive failure.** It spends resources and always produces an eligible random blue or yellow item. The random result is the gamble.
2. **Socketing retains destructive failure.** The user's request to remove failures referred to Imbuing, not Tinkering.
3. The latest **proposed base socket success schedule** is **100% / 50% / 25% / 5%** for sockets one, two, three and four onward. This replaces the earlier 10%-per-socket proposal; the rates remain provisional balance values, distinct from the settled destructive-failure rule.
4. **Leather and cloth both use Tailoring.** There is no separate Leatherworking skill.
5. Repairs succeed when requirements are met, but **each ordinary durability repair may reduce maximum durability**. This latest decision supersedes earlier rejection of maximum-durability loss.
6. Repair costs now use **the item's material plus essence according to rarity**. Charges-only restoration uses essence only. Earlier repair-kit-only proposals are not the final cost model.
7. Salvage is permitted only for **dropped, crafted and gambling-generated equipment**. Ordinary vendor-origin gear cannot be salvaged, imbued or socketed.
8. Equipment gambling generates normal loot with normal loot eligibility. It is not ordinary restricted vendor stock.
9. **One rune mould makes one rune and is consumed.** Charged chest keys remain a separate design.
10. **Nine gem families, each with five qualities:** Amber, Amethyst, Citrine, Diamond, Emerald, Ruby, Sapphire, Star Sapphire and Tourmaline.

## 3. Profession responsibilities

| Profession | Role |
| --- | --- |
| Blacksmithing | Metal weapons and heavy armour; repair corresponding equipment |
| Bowcraft / Fletching | Bows, wooden weapons and wooden shields; repair corresponding equipment |
| Tailoring | Medium/light armour, including leather and cloth; repair corresponding equipment |
| Carpentry | Furniture and housing goods |
| Tinkering | Tools for other professions and itself; socket installation; charged chest keys |
| Imbuing | Turn eligible white equipment into random magic/rare equipment |
| Inscription | Scrolls, consumable tomes and runes from moulds plus bark |
| Alchemy | Short combat-buff potions and crafted rejuvenation potions |
| Cooking | Longer quality-of-life food buffs |
| Cartography | Selectable crafting job producing maps and providing treasure-hunting eligibility; qualifies for Pillar of Society |
| Mining | Always-active gathering: ore and gem byproducts; does not qualify for Pillar of Society |
| Lumberjacking | Always-active gathering: wood and tiered bark byproducts; does not qualify for Pillar of Society |
| Fishing | Always-active gathering: fish, map bottles, occasional kraken encounters; does not qualify for Pillar of Society |

Resolve ambiguous equipment assignments against the catalogue. Material and armour weight are independent properties: a metal piece remains metal for salvage even if classified as light armour. Do not silently create extra professions to handle edge cases.

Treasure hunting is an activity; **Cartography is its skill**.

## 4. Materials and acquisition

| Resource | Intended source | Vendor availability |
| --- | --- | --- |
| Ore / ingots | Mining and processing | Do not sell ore; do not use ingot sales to bypass gathering without an explicit decision |
| Logs / boards | Lumberjacking and processing | Do not sell wood |
| Leather / hides | Hunting and skinning beasts | Gathering/hunting source, not ordinary vendor supply |
| Cloth | Vendor purchases permitted | Yes |
| Paper | Vendors | Yes |
| Empty bottles | Vendors and recovery after potion use | Yes |
| Alchemical reagents | Reuse UO reagents; other sources to be assigned | Cheap per unit; vendors can supply them |
| Rune moulds | PvE drops | No |
| Tiered bark | Lumberjacking byproduct | No |
| Gems | Mining byproduct | No |
| Magic Essence | Salvaging eligible magical equipment | No vendor source has been approved |
| Maximum-durability restoration resource | Special encounter/resource source; hive proposed | Not decided |

The broad convenience rule is that crafting tools and ordinary supplies should be purchasable at a premium. The gathering/drop-only exceptions above must remain exceptions. Vendor tools enable starting a profession without first finding another crafter; player-made tools provide an economical alternative.

Bark and gems can be sold to NPCs for a pittance, but cannot be bought from them. They do not drop from monsters. Player trading remains possible.

Hunting materials are obtained through a bladed weapon's double-click/use action followed by targeting the corpse. Preserve existing corpse-loot rules: harvesting must not delete unclaimed equipment.

## 5. Salvage

The user reports this foundation implemented. Verify actual behaviour before extending it.

| Physical equipment material | Salvage output | Intended use |
| --- | --- | --- |
| Metal | Metal Scraps | Tinkering |
| Wood | Wooden Chips | Cooking fuel |
| Leather | Leather Strips | Crafting uses to finalise |
| Cloth | Cloth Scraps | Crafting uses to finalise |

- Material chooses the physical output; **item level controls physical yield**.
- **Rarity controls a separate Magic Essence roll**.
- An eligible magical metal weapon can yield both scraps and essence. White equipment supplies physical material only.
- Yields use configurable ranges and recovery chances, including zero recovery. Avoid fixed guaranteed quantities.
- Higher levels/rarities improve expected recovery, without requiring every individual roll to exceed every lower-tier roll.
- Ordinary vendor-origin gear is excluded from both custom salvage and native recycling routes. It may only be used, sold or discarded under the relevant equipment rules.
- Imbued products remain salvageable. Costs and recovery must make repeated imbuing/salvaging a net resource expense overall, not a self-sustaining production loop.

Metal scraps should also be craftable directly from ingots; wooden chips directly from logs or boards. Direct production of leather strips and cloth scraps from leather/cloth was suggested and remains a sensible recipe candidate, not a final yield specification.

Proposed uses for leather strips include straps/bindings/grips; cloth scraps could supply padding, upholstery or repair-tool construction. Exact recipes remain open.

NPC equipment resale is intended to be sharply reduced, making salvage useful without flooding the world with gold.

## 6. Equipment crafting and coloured materials

Craft eligible white equipment bases using appropriate gathered materials. Coloured ore, wood and hides should offer small predictable bonuses as well as appearance.

The latest examples are modest resistance/defensive benefits for armour and damage-related benefits for weapons. Shadow armour could gain enhanced defence; shadow weapons could gain enhanced physical damage, possibly as flat values. These are examples, not a complete material table.

Material bonuses should be nice to have and useful for min-maxing, while visual identity remains important. Do not let them displace random affixes, sockets or skill builds as the main sources of power.

Keep base type, normal/exceptional/elite tier, rarity, material, origin and crafting quality separate. Exact crafting requirements and tier availability remain open.

Earlier ideas for a crafted-white starting-socket chance at 100 skill and extra durability at 120 remain unconfirmed proposals. Do not activate them automatically.

## 7. Imbuing

### Core behaviour

- Input: an eligible **white** base from drops, crafting or equipment gambling.
- Costs: Magic Essence and the currently configured gold/tool costs.
- Output: the same base transformed into random blue or yellow equipment, with valid random affixes.
- **No outright failure and no base destruction.**
- Unwanted output can be used, traded, socketed if eligible, sold or salvaged.
- Vendor-origin whites are not eligible.

The proposed rarity split is 75% magic / 25% rare, still provisional. Respect item-level gates, slot eligibility, affix compatibility and rarity-specific ranges. Binary properties such as Ignore Target Defence belong in the eventual equipment-affix design, not the percentage-rune pool.

Preserving installed sockets and material benefits was the intended transformation rule. Reconcile with final socket caps rather than silently losing properties. Blue/rare rerolling has not been approved as a separate action.

### Tool and progression

An imbuer's ladle or cauldron was proposed; Tinkering should be able to make profession tools. Exact tool choice, charges, recipe and skill requirements are open.

Imbuing trains through producing equipment. Random results supply the gamble; adding destructive failure is expressly rejected. Balance resource consumption through essence cost, gold/tool costs and salvage recovery.

## 8. Tinkering and socket installation

### Tools

Tinkering supplies hammers, cauldrons/imbuing tools, engraving tools and cartography pens as appropriate. It uses hammers and may craft its own replacements. Provide expensive vendor starter tools so the profession does not have a circular bootstrap requirement.

Charged chest keys remain a desired product, with better keys for better chest tiers. Lockpicking need not be a separate skill. Exact charges, recipes and eligibility are open.

### Socket gamble

Add **one socket per attempt**, never exceed the base's allowed maximum, and destroy the item on failure.

| Socket being added | Proposed base success chance |
| --- | ---: |
| First | 100% |
| Second | 50% |
| Third | 25% |
| Fourth and later | 5% |

A modest gold fee should contribute to the sink without making attempts inaccessible. Material/tool consumption and the relationship between skill and eligibility remain open. The first socket is safe once requirements are met; subsequent sockets risk the item.

Imbuing may add a success bonus to a tinkering hammer: **+1 / +2 / +3%**, associated in discussion with average, high-quality and superior tools. The arithmetic is **not yet settled**: percentage points would turn 5% into 8% at +3; a relative 3% increase would turn 5% into 5.15%. Obtain a design decision before fixing the formula. Cap final success at 100%.

Whether these hammer labels are crafting quality, affix tiers or both also needs an explicit definition. Do not confuse them with normal/exceptional/elite equipment tiers.

Ordinary vendor-origin equipment cannot receive sockets. Looted gear starts unsocketed under the current design. Socket caps are custom to Sanctuary, not copied from D2 item-level caps.

## 9. Repairs and durability restoration

Repairs do not fail when the player meets the requirements. Required repair skill is the skill needed to craft the corresponding base; magical affixes do not independently turn maintenance into a success gamble.

| Operation | Resource rule |
| --- | --- |
| Repair current durability | Material matching the item, plus Magic Essence according to rarity |
| Restore spell charges only | Magic Essence only |
| Restore lost maximum durability | Special material; a hive-related source was suggested |

Every ordinary durability repair has a chance to reduce maximum durability. The chance, amount, minimum floor and relation to amount repaired remain open. Restoring maximum durability is a distinct operation from ordinary repair.

Define what form of material is required: ingots/boards/leather/cloth versus salvage outputs is **not settled**. Also decide whether coloured items require their exact material subtype. Do not infer that scraps are automatically accepted.

NPC repair should cost more than player repair while remaining affordable enough for a newcomer to earn a surplus. Tools may still be required, but their charges and vendor handling of material/essence costs need reconciliation with this latest model.

Earlier ideas of a 30% resource-saving chance at 120 skill remain unconfirmed under the revised cost system. Charge-only restoration should not automatically trigger durability loss unless explicitly decided. Combined repair and recharge pricing is open.

## 10. Alchemy and bottles

Alchemy specialises in short combat buffs and emergency tools. Healing and mana potions may be supplied by vendors as a baseline; monster drops were also part of the earlier plan. These sources do not require ordinary healing/mana potion crafting to be a core Alchemy progression path.

Rejuvenation/full rejuvenation are intended as alchemist products rather than monster drops.

Examples: IAS, FCR and resistance potions. A low-level +5% IAS potion remains usable even when a +20% version exists, though breakpoint effects matter. The exact effects and values are examples, not a completed catalogue.

- Reuse UO reagents as ingredients.
- Reagents can be inexpensive individually, while powerful recipes consume large quantities.
- Ingredient counts are an intentional balancing control for expensive endgame boosts.
- Empty bottles are bought from vendors or recovered after drinking.
- Proposed recovery chance is **20%**; configure it rather than hardcoding it into unrelated systems.
- Potions should activate reliably; do not use bandage slipping mechanics.
- Keep buffs brief so they are situational interventions rather than customary preparation chores.

Exact durations, stacking, effect categories, recipe outputs and skill-gain rates remain open. A costly input alone does not justify a long mandatory buff. Define inventory handling of recovered bottles without allowing it to prevent potion use.

The potion belt uses the tabard slot, four columns/hotkeys and variable row count. That interface already has its own implementation work; this document does not redefine overflow behaviour.

## 11. Cooking and hunting

Cooking provides longer quality-of-life benefits rather than competing with Alchemy's combat buffs.

Examples:

- Light radius, such as a small +1 version still useful when +3 exists.
- Carrying capacity.
- Roughly hour-long convenience effects, with actual durations still configurable and undecided.

Ingredients come from hunting/butchering and fishing, with other ordinary ingredient sources to assign. Wooden chips can be used as fuel. Some rare recipes may be rewards from treasure hunting.

Training should produce food that remains useful instead of disposable trash. Food effect categories, stacking and exact skill progression remain open. Carry-capacity buff expiry must not destroy possessions; decide how encumbrance works explicitly.

## 12. Inscription: scrolls and tomes

### Proposed progression

| Skill band | Training direction |
| --- | --- |
| Up to 50 | Town Portal and Identify scrolls |
| 50–100 | Charged tome production; detailed recipes/gain bands open |
| 100–110 | Full tome production, with a rare bonus Teleport scroll possibility |
| 110–120 | Rune crafting required for further progression |

These are intended progression bands, not a final skill-gain formula. Rune crafting need not begin only at 110; lower rune tiers need their own eligibility thresholds.

### Tomes

- Tomes are consumables: use their charges, then replace them.
- **No refilling.**
- Proposed vendor maximum: 10 charges.
- Proposed crafted capacity: 20 charges.
- High quality: 5% chance not to consume a charge on use.
- Superior: 10% chance not to consume a charge on use.
- Ordinary quality consumes charges normally.

Crafting full tomes at 100+ skill may rarely produce a **single-use Teleport scroll**, using behaviour equivalent to the Sorceress level-1 spell. Treat it as a proposed bonus output, not a reusable teleport item. Bonus probability and exact requirements are open.

## 13. Inscription: runes and bark

No runewords are planned. Runes are individual socketables with a random percentage-based property. Binary properties are excluded; selected flat-value properties are assigned to gems instead. Equipment affixes can later contain properties excluded from runes.

### Inputs and tiers

- PvE drops provide rune moulds.
- Lumberjacking provides tiered bark as a byproduct.
- Inscription combines mould and suitable bark into a rune with a random eligible stat.
- Matching or higher-tier bark can satisfy a mould. Lower-tier bark cannot.
- A higher bark tier should not silently upgrade the mould's output tier.

| Tier | Intended mould drop monster levels | LJ threshold for bark | Proposed rune name |
| --- | --- | ---: | --- |
| 1 | 20–29 | 50 | Crude Rune |
| 2 | 30–39 | 60 | Rough Rune |
| 3 | 40–49 | 70 | Shaped Rune |
| 4 | 50–59 | 80 | Etched Rune |
| 5 | 60–69 | 90 | Refined Rune |
| 6 | 70–79 | 100 | Polished Rune |
| 7 | 80–89 | 110 | Flawless Rune |
| 8 | 90–99 | 120 | Perfect Rune |

The eight sequential monster bands interpret a typo in the original message (repeated tier 3). Confirm against existing implementation. Crude and Perfect were requested endpoints; the intermediate names were assistant proposals, not explicitly finalised names.

These thresholds establish eligibility, not guaranteed bark on every swing. Byproduct probabilities, amounts and selection among unlocked tiers remain open. Inscription requirements are separate from Lumberjacking requirements.

**Confirmed subsequent decision:** one mould makes one rune and is consumed. The earlier charged-mould proposal is superseded. Bark quantity and other recipe/tool costs remain open; single-use mould consumption is no longer an open question.

### Rune value and name

The proposed percentage formula is tier value plus quality bonus:

| Quality | Proposed name prefix | Bonus |
| --- | --- | ---: |
| Ordinary | Average | +0 percentage points |
| High | High Quality | +1 percentage point |
| Superior | Superior | +2 percentage points |

Example: tier-8 FCR gives 8%; superior quality gives **10% FCR**. Quality probabilities and dependence on Inscription skill remain open. Using the same numerical curve for every percentage property is a design starting point, not proof that all properties are equally powerful.

Names combine quality prefix, tier name and property suffix: **Average Crude Rune of Balance**, **Superior Perfect Rune of Luck**.

Candidate suffixes: Alacrity (IAS), Spellcasting (FCR), Balance (FHR), Luck (Magic Find), Wealth (Gold Find). These are proposed naming mappings, not a claim of exact Diablo naming. Keep explicit property/value tooltips.

Validate the pool by actual property units. Flat damage, flat life/mana, flat attributes and mana-per-kill do not become percentage properties merely because they can appear on gear. Ignore Target Defence and other binary properties are excluded.

## 14. Mining gems

Gems are ready-to-socket gathering products, distinct from crafted runes.

| Gem quality | Proposed Mining threshold |
| --- | ---: |
| Chipped | 50 |
| Flawed | 60 |
| Standard | 70 |
| Flawless | 80 |
| Perfect | 90 |

**Confirmed subsequent decision:** retain all nine native UO gem families—Amber, Amethyst, Citrine, Diamond, Emerald, Ruby, Sapphire, Star Sapphire and Tourmaline—with five qualities each. The native family roster is verified; the five-quality socket-gem model is design work, not an already implemented native feature.

Gems do not drop from monsters or appear in vendor stock. They can be sold to NPCs for a small amount. Mining byproduct rates and quantities remain open.

Use selected flat-value stats for the two extra gem families. **Attacker Takes Damage** was the explicit example. **Mana After Each Kill** and **Magic Damage Reduced by X** have been suggested as the second property; neither has been selected. Amber/Star Sapphire assignments were also assistant proposals. Final gem/property assignments, slot-dependent behaviour and values remain open. The older Gold Find/FHR proposal for the two extra families is superseded by the flat-value direction.

## 15. Cartography and treasure hunting

Cartography can train by making maps of towns, regions and dungeon levels. Treasure maps dropped by monsters require skill breakpoints appropriate to their tiers. Fishing can also yield bottles containing maps.

Tinkering produces pens; vendors supply paper.

### Proposed map benefits

| Map type | Proposed temporary benefit |
| --- | --- |
| Town/city | 5% vendor bonus in that town |
| Region | Increase treasure-dig search radius by five tiles |
| Dungeon level | +10% Magic Find on that level |

Duration scales with skill. The example is one minute at skill 1–9, two minutes at 10–19, and so forth. This is not yet a fully defined formula: specify skill 0, exactly 120, rounding, and whether the maker's skill is recorded on the map or the user's skill is evaluated at use.

The earlier assistant suggestion was to store duration from the maker's skill; the user has **not yet explicitly chosen that**. Map consumption on activation, charges, stacking, cooldowns and area restrictions also need a decision. Do not assume that calling them usable maps automatically settles consumption.

Define the vendor bonus precisely: discount on purchases, improved resale or both. Prevent stacking/rounding interactions from enabling buy/sell profit loops.

Maps should have useful outputs at training levels. The small dungeon Magic Find benefit is an intentional power/economic effect, unlike the convenience-only direction for Cooking.

Treasure hunting retains finding/deciphering/digging, with the intended reward encounter becoming a small dungeon entrance and a final chest. Rare recipes are a candidate reward. Layouts, rewards and encounter tiers are later content work.

## 16. Progression, economics and implementation checks

Do not choose final skill-gain speed until representative recipes and material acquisition rates exist. The intended loop is useful production, with salvage or consumption providing ongoing demand.

Required design consistency checks when implementing relevant systems:

- Vendor origin survives trade/storage/relog and cannot be laundered through another salvage route.
- Ordinary shop stock and gambling rewards have different improvement eligibility.
- Imbuing always yields an item and never destroys the base as a failure outcome.
- Socket failure destroys the item; success adds exactly one socket and respects its cap.
- Physical salvage follows material, not armour weight; essence follows rarity.
- Recipe costs and expected salvage recovery cannot create unlimited essence/material profit loops.
- A higher-tier bark may substitute downward but cannot elevate a mould tier.
- Rune affix selection excludes flat and binary properties.
- Tomes cannot be refilled; quality charge-saving rolls do not duplicate effects or charges.
- Repairs distinguish current durability, maximum durability and spell charges.
- Map buffs and tool bonuses expose their effective values clearly.

## 17. Remaining decisions

The deduplicated [editable decision worksheet](docs/economy-open-questions.md) expands these questions into stable IDs, separates optional tuning of existing values, and includes the broader economy's deferred decisions. Its unfilled entries are questions, not new decisions.

- Exact equipment recipes, profession requirements, gain chances and gain caps.
- Final coloured-material bonuses and resource distribution.
- Repair material forms/subtypes, quantities, essence table, tool use, max-durability loss and restoration resource.
- Socket tool bonus arithmetic, skill gates, gold fees and material costs.
- Imbuing tool choice, costs, quality of tools and final blue/rare probabilities.
- Inscription rune skill thresholds, bark quantity/rates, other crafting costs and rune-quality chances; moulds are already confirmed single-use.
- Final rune names, percentage pool and per-stat balance.
- Gem/property assignments, flat values, socket effects and mining probabilities; the nine families and five-quality structure are already confirmed.
- Potion catalogue, ingredient quantities, durations and stacking.
- Food recipes, durations and stacking.
- Tome recipes, quality chances and Teleport-scroll bonus probability.
- Cartography consumption, duration skill source, boundaries, map acquisition requirements and exact vendor bonus.
- Fishing rates, kraken encounter details, treasure-map tiers and rare recipe sources.

The immediate aim is a coherent reusable system. Final world placement, dungeon rewards, boss tables and full economic balancing can follow later.
