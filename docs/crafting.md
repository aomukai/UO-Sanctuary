# Crafting and economy: first implementation decisions

Reconciled 19 October 2026 (JST) from the completed [user replies](economy-open-questions-replies.md). This is the current design specification, **not a report of installed features**. The replies remain unchanged. Their explicit answers supersede older economy/crafting handoffs, earlier assistant recommendations and conflicting prototype settings. Values identified as provisional in the replies remain provisional first-implementation values.

The [worksheet](economy-open-questions.md) retains stable IDs and tracks unresolved parts. [Installed economy notes](economy.md) describe the comparison build; its older resale, skill-free Imbuing and other settings are not automatically the desired next implementation. [Wardrobe progression](wardrobe-progression.md) and the [class-name mapping](class-talents-and-trades.md) retain subsequent decisions where copied, unanswered worksheet rows add no replacement answer.

## Scope, progression and ordinary quality — A01–A08

- All professions remain in scope. **Later user instruction, 19 October:** deliver dependency-ordered, small playable slices using the [implementation roadmap](implementation-roadmap.md), rather than implementing everything at once. This changes the older A01 delivery order, not the agreed profession catalogue.
- Gathering keeps native UO-style progression. Useful crafting recipes should be available every 10 skill points through 110; the final 110–120 progression emphasizes top-end work, with reduced gains from lower recipes. Exact recipe books and gain curves still require authoring.
- Trainers can teach each skill to 30 for gold. Practice recipes then provide resource-consuming, no-output training through 70, in 10-point bands. The stated gain chance runs from 100% for the beginning of a band to 1% at its end, with +0.1 gains. **Confirmed T05, 19 October:** practice applies only to the ten crafting professions. Each band uses 100% at its beginning, decreasing by one percentage point per +0.1 trained skill to 1% at its last tenth; the next band resets to 100%. Every attempt consumes ingredients with no output, whether or not it gains skill. Trainers charge a configurable 1 gold per +0.1 (300 gold from zero to30), with partial training allowed.
- Ordinary crafting uses UO-style success/failure behavior. Valid Imbuing always produces a magical item; repairs succeed when requirements are met; socket failures can destroy their item.
- Ordinary successful crafts have 10% High Quality, 1% Superior, otherwise Average, once eligible. **Provisional eligibility rule:** `min(recipe required skill + 10, 120)`. Below eligibility, Average only. These are mutually exclusive quality outcomes after crafting success, not additional craft-success rolls. Runes have their own eligibility thresholds.
- Mining/Lumberjacking/Fishing remain active alongside one selected crafting job and class. Cartography is a crafting job. Level 80 in a class or 100 in a crafting job unlocks recognition/wardrobe/housing rights; gathering does not qualify. Profession caps start at 100 and use 105/110/115/120 Power Scroll unlocks. Other wardrobe details remain in Q.
- New characters initially show Alchemy active. Production or practice with a crafting tool asks them to confirm their first profession before its action/cost/gain, and that confirmed craft remains selected until the wardrobe unlock. The initial alphabetical default does not itself lock the choice. Gathering is exempt. Wardrobe opening preselects the current class and craft; Swap requires exactly one checked entry in each list. See [wardrobe progression](wardrobe-progression.md) for the confirmation/access rules and remaining migration questions.
- **Confirmed T05, 19 October:** existing class characters preserve every skill value/cap and start with an unconfirmed first profession; existing staff settings remain intact. Craft trainers offer the same first-choice confirmation before payment; afterwards only the active craft can be trained. Gathering trainers remain unrestricted. Native total-skill-cap mechanics must not erase or block independent retained-skill progression.
- **Confirmed T05 processing access:** anyone can smelt ore, make boards, process wool/yarn/cloth and use the planned scrap conversions without selecting a craft. Crafting-skill gains require the matching active craft; gathering gains remain active. T07 below settles conversion weights, tools and gains; T06 settles station rules.
- Craft/repair assignment follows physical material: metal to Blacksmithing; cloth/leather/studded to Tailoring; wooden weapons/shields to Bowcraft/Fletching. Bone armour is a **heavy Tailoring** product. Repair is handled by the craft that makes the item.

| Profession/action | Specified tool/station |
| --- | --- |
| Cartography | Pen |
| Blacksmith crafting / repair | Smithing hammer / separate repair hammer; forge/anvil |
| Tailoring crafting / repair | Tailoring kit / scissors presented as a tailoring repair kit |
| Imbuing | Ladle; cauldron as the town workbench, not a native soulforge |
| Tinkering | Tinker's tools; tools are made by Tinkering |
| Carpentry | Existing appropriate tools and town carpentry workbench |
| Cooking | Inn hearth/kitchen; existing portable campfire exception remains |
| Chitin processing | Alchemy mortar and pestle |

**Confirmed T06:** the first tool cohort uses mortar/pestle, smithing hammer, fletching tools, saw, cartography pen, skillet, inscription pen, tailoring kit, tinker's tools and Imbuing ladle. Reuse their nine native Tinkering recipes; the ladle provisionally requires25 Tinkering and2 iron ingots. Vendor tools cost10gold and receive20–30 uses once supplied. Crafted tools have100 uses, High Quality110, Superior120, and10% charge saving on committed production/practice attempts, including failures. One use covers one attempt/batch; cancellation/rejection and charge-free processing/salvage use none. Existing tools retain their current uses without retroactive refills or quality conversion. Prices/capacities are tuning data. T11 supplies vendor tools; T12 adds the separate repair tools and their recipes. Additional profession tools retain their own recipe cards. Trainer prices follow T05.

**Confirmed T06 facilities:** ordinary production and practice for all ten crafts require the appropriate public town facility within two tiles. Initial usage is free and stations support concurrent users. Private workshops remain deferred. Basic processing/salvage remain portable while preserving native equipment needs such as a forge or spinning wheel/loom. Campfires permit simple meat/fish cooking; baking still needs a kitchen. Native per-character recipe unlocks persist across profession changes. The initial Britain registry and the first tool transaction cohort are recorded in [T06](t06-crafting.md); equipment production remains T08.

## Equipment bases and materials — B01–B11

Ordinary equipment crafters produce white bases. Imbuing is the separate magical transformation.

| Base tier | Recipe skill band | Crafted item level | Base equip level requirement |
| --- | --- | ---: | ---: |
| Normal | 0–80 | 35 | 0 |
| Exceptional | 80–110 | 70 | 60 |
| Elite | 110–120 | 99 | 80 |

Strength/Dexterity requirements also apply; subsequent affix and filled-socket requirements must still be respected. The standard-material recipe book now has201 identities /603 approved recipes and explicit tier mappings in [T08](t08-white-bases.md), including gloves, boots and potion tabards. Ten exotic glass/stone/dragon-scale recipes are explicitly deferred. Jewellery has the separate E01 ladder below.

At crafting skill 100+, eligible bases receive +10–20% durability and the crafter's name. Quality can also create native starting sockets at 100+: Average 0, High Quality 1, Superior 2, on bases that permit sockets. These quality sockets are distinct from tinkerer-added sockets. **Confirmed T08:** roll an independent integer10–20%, apply once to the ordinary native maximum with upward rounding, and set current durability to that new maximum. Native exceptional/coloured durability bonuses do not stack. The complete ordinary per-recipe table is in [T08](t08-recipe-book.md).

Retain native ore/wood gathering and hide/species assignments as the starting baseline, adding the new bark/gem byproducts. Retain native hunting/skinning quantities, without taking the user's illustrative dragon quantity as a newly verified value. Butchering/skinning requires no skill.

Metal, wood and leather come from gathering/hunting, not NPC resource sales. Cloth can be bought expensively or produced through native sheep/wool/yarn/loom processing. Remove cloth-to-bandage creation; bandages are not part of the intended game. Inns provide inexpensive ordinary food ingredients; alchemy vendors provide UO reagents and bottles. Carpenter-made kegs are desired, using native recipes where suitable; no additional custom keg behavior was specified.

### Material bonus directions

Armour rolls1–5 resistance percentage points independently for each listed resistance. Weapons roll5–10% added damage of the material's type, based on the unmodified reference weapon damage before global bonuses; existing physical damage is retained. **Confirmed T08:** random elements are Fire/Cold/Lightning/Poison; Barbed's random resistance excludes Poison. Rolls are stored once. T09 activates the saved combat rolls: cold adds a base2-second chill, poison delivers its total bonus over3 seconds without repeated-hit stacking, and Verite pieces stack. [T10](t10-rewards.md) activates Gold Find for corpse gold; Magic Find remains gated until its gear-reward consumer exists. See the implementation/verification status in [T09](t09-effective-gear.md). See [T08](t08-white-bases.md).

| Ore | Property identity / exception |
| --- | --- |
| Iron | None |
| Shadow | Physical |
| Bronze | Poison |
| Agapite | Lightning |
| Valorite | Fire |
| Dull copper | Cold |
| Copper | Random element |
| Gold | Armour: 1–2% Magic Find; weapon: 1–2% Gold Find |
| Verite | Armour: 1 life per 5 seconds; weapon: 1 mana per 5 seconds |

| Wood | Property identity |
| --- | --- |
| Plain | None |
| Oak | Fire |
| Ash | Lightning |
| Yew | Poison |
| Bloodwood | Physical |
| Heartwood | Random element |
| Frostwood | Cold |

| Leather | Property identities |
| --- | --- |
| Normal | None |
| Spined | Cold and lightning |
| Horned | Fire and physical |
| Barbed | Poison plus a random element |

Starter items remain newbie-protected, cannot be salvaged/imbued/socketed, and sell for 1 gold. Belts/tabards receive combat affixes but no sockets. Gloves/boots and jewellery have no sockets. Socketable slots are helmet, chest, **weapon (including swords)** and shield. Other designated layers—arms, gorget, pants, cloak—are cosmetic without stats/sockets. **Confirmed T09:** ordinary cloth clothing, including cloth hats, is cosmetic and wearable; belts/tabards are exceptions. Leather footwear retains its combat role, including naturally non-wearing0/0 footwear. Old cloth-hat affixes/socket history remain saved but inactive; new cloth hats cannot receive combat affixes or sockets.

**Explicit clarification, 19 October:** eligible white weapons, shields, rings and amulets are imbueable alongside white helmets/chests/gloves/boots/belts. Drop/crafted/gambled-origin, starter/vendor exclusions, skill requirements and the tinker-added-socket restriction still apply. Being imbueable does not imply being socketable; swords qualify for both as weapons, while rings/amulets do not acquire socket permission.

## Processing and salvage — C01–C04

**Confirmed T07, 19 October:** raw→processed halves weight; processed→ten scraps preserves total weight. This supersedes the earlier half-weight-at-every-step wording. Scraps have only generic family identities, with a warning before deliberate conversion of coloured inputs. Normal processing retains its native subtype.

| Family | Raw weight | Processed weight | Generic output weight / yield |
| --- | ---: | ---: | --- |
| Wood | Log2 | Board1 | Wooden Chips0.1; ten per log/board |
| Metal | Small/medium/large ore1/2/4 | Ingot1 | Metal Scraps0.1;5/10/20 per ore clump, ten per ingot |
| Leather | Hide2 | Leather1 | Leather Strips0.1; ten per hide/leather |
| Textile exception | Native wool4 and yarn/thread1 | Cloth0.1; bolt5 for50 cloth | Cloth Scraps0.01; ten per cloth,500 per bolt |

Native ore yields remain½/1/2 ingots per small/medium/large clump. Native wool/yarn/loom quantities remain. Existing material stacks receive the same canonical weights when the release flag is enabled; amounts, subtype, hue and origin stay intact. Keep native integer per-stack weight rounding and ordinary encumbrance, with no deletion or automatic dropping.

- Processing uses smithing hammer→Blacksmithing, tinker's tools→Tinkering, fletching tools→Fletching, saw→Carpentry, tailoring kit→Tailoring for leather/cloth. Anyone may process without selecting a craft or using a town facility. Valid conversions always produce their output and spend no charges. One native-style skill check per action can gain only for the matching confirmed active craft, through30, respecting individual caps/locks; no checks once trivial. Partial quantities and all-input batches must preflight exact stack/item/weight capacity before consumption. Normal forge/wheel/loom equipment requirements remain. See [T07](t07-processing.md).
- Disable new cloth/uncut-cloth/oil-cloth bandage cutting and NPC bandage supply, including buyback. Existing bandages remain. NPC raw/processed metal, wood and leather supplies and their generic scraps are excluded from stock/restock/buyback; cloth supply stays allowed. Full vendor prices/restocking remain T11.
- Scraps/strips supply ordinary profession recipes. Tinkering uses materials for tools/keys; Cooking uses Wooden Chips as fuel. Metal/leather/cloth repairs also consume their corresponding scrap/strip material; wooden repairs use appropriate boards, not chips. T12 sets the additional metal/leather/cloth repair scraps or strips to1/3/5 by base tier; bone uses bones without extra scraps.
- Anyone can salvage eligible equipment with the corresponding crafting tool. No skill requirement, no charge consumption, no gold fee. No new salvage skill gain rule was selected.
- Drop/crafted/gambled-origin eligibility and separate rarity/item-level rolls remain. Keep the existing provisional recovery values: Magic essence 80% / 1–2; Rare 90% / 2–5; each physical material 85% / `[1+s, 3+s]`, `s=floor((itemLevel−1)/10)`. Unique 95% / 5–12 remains reserved until real Unique support.
- Installed gems/runes are destroyed by salvage and do not add recovery yield. A socketed item still supplies its ordinary physical output and any rarity-based essence entitlement.

## Repair and recharge — D01–D14

Use processed materials: ingots, boards, cut leather and cloth. Coloured equipment requires its exact material subtype; bone is usable directly for bone armour. C03 additionally requires1/3/5 generic scraps/strips for Normal/Exceptional/Elite metal/leather/cloth gear. Wood takes no chips, and bone takes no scraps.

| Base tier | Matching processed material | Magic essence | Rare essence | NPC gold surcharge |
| --- | --- | --- | --- | ---: |
| Normal | Random 1–3 | Random 1–3 | Twice Magic cost | 100 |
| Exceptional | Random 3–5 | Random 3–5 | Twice Magic cost | 500 |
| Elite | Random 5–10 | Random 5–10 | Twice Magic cost | 2,500 |

White gear needs no essence. Rare costs twice the independently drawn Magic essence cost; a3× Unique multiplier is reserved for the future Unique item implementation. Each repair restores current durability to its post-loss maximum, with a maximum-durability floor of1. There are no partial durability repairs. Draws are quoted once for the same damaged state and survive cancel/reopen/relog. Recharge costs **1 Magic Essence per restored charge**, with quantity/fill controls and a persisted capacity. Combined work adds applicable costs. Charge-only work needs no physical material and causes no durability loss; mere use of spell charges does not wear durability.

| Repairer skill | Chance to lose maximum durability | Loss when triggered | Resource-saving chance |
| --- | ---: | --- | ---: |
| Below 80 | 50% | 1–3 | 10% |
| 80 to below 100 | 25% | 1–2 | 20% |
| 100 to below 120 | 10% | 1–2 | 25% |
| 120 | 5% | 1 | 30% |

The 100–119.9 bracket is the reply file's explicit **provisional gap fix**. Repair success itself is reliable when requirements are met. One saving roll waives the whole action's material/essence bill, including recharge. Full quoted resources must be available before the roll; tool use and NPC fees remain payable. NPCs take the same resources automatically from the backpack plus the tier fee once per completed single/combined action, operating at skill80 with20% resource saving. Player repairs require their active craft, the public workshop and appropriate repair tool; NPCs require no matching active craft or personal tool. Repairs grant no skill gains.

Keep native wear rates initially. At zero current durability, equipment may remain worn but contributes no weapon damage, defense or stats; a broken weapon uses unarmed attacks. The item is not deleted. **Confirmed T09:** broken worn items retain weight and existing armor/shield running penalties. Their material effects, block, numerical bonuses and item-granted skills switch off. Broken weapons use unarmed reach/timing as well as damage; changing equipment never refunds a committed action deadline. Numeric FHR rolls are enabled through the existing recovery formulas.

**Confirmed T12, 20 October:** [repair/recharge implementation](t12-repairs.md) records all six approved defaults and settles the remaining action rules: full durability repair, maximum floor1, additional generic scraps/strips1/3/5, reserved Unique3× Magic essence cost, one all-resource saving roll, one tool use, separate repair tools/public workshops, no repair skill gains, automatic NPC backpack payment and quantity/fill recharge for functioning native wands. Tools and consumable tomes cannot be refilled. Prices and quantities remain configurable.

Solen, Terathan and other arachnid creatures can drop **chitin**, processed with mortar/pestle into **Powder of Fortifying** for restoring maximum durability. Drop probabilities, processing recipe/skill, amount restored and restoration floor/ceiling are still unspecified.

## Imbuing — E01–E05

| Base tier | Required Imbuing | Gold | Essence |
| --- | ---: | ---: | ---: |
| Normal | 80 | 100 | 5 |
| Exceptional | 100 | 500 | 10 |
| Elite | 120 | 1,000 | 20 |

Below skill 120, outcomes are 75% Magic / 25% Rare. At 120 they are 70% / 30%. A valid action always produces an item, with no destructive failure or separate blue/rare reroll action. The target effect pool covers normal numerical properties plus binary properties, respecting item eligibility; reference effects still need actual implementation before they can roll.

Tinkering can craft white rings/amulets at skill 30/50/70/90/110; Imbuing the matching jewellery requires the same skill. Their working quality names are Shoddy/Plain/Solid/Elegant/Fancy. Jewellery item levels, equip requirements, costs and detailed affix eligibility have not been specified.

**New transformation restriction:** an item with any tinkerer-added socket cannot later be imbued. Native quality sockets on a crafted white base do not prevent Imbuing. Once any Tinkering socket is added—even on top of native sockets—the item is excluded. Imbue first, then risk adding sockets. Warn before socketing a white base that doing so prevents later Imbuing. Preserve socket provenance through saves/trade; rarity itself does not determine socket capacity.

## Tinkering sockets — F01–F08

All socketable bases cap at **5**, independent of rarity or base tier. Helmet/chest/one-handed weapon/shield allow 20 equipped sockets in total; helmet/chest/two-handed weapon allow 15. Empty socket count is distinct from filled count.

| Socket being added | Minimum Tinkering | Base success |
| --- | ---: | ---: |
| 1 | 30 | 100% |
| 2 | 50 | 50% |
| 3 | 70 | 25% |
| 4 | 90 | 5% |
| 5 | 110 | 5% |

Also require base-tier skill: Normal 30, Exceptional 80, Elite 100. Both gates apply. Native starting sockets count when selecting the next socket's chance/gate. Each attempt costs a Tinkering tool charge, subject to applicable tool charge-saving; no additional gold or material cost. Failure destroys the item.

Hammer bonuses +1/+2/+3 are **additive percentage points**, capped at 100%; tool-quality outcomes follow ordinary crafting and grant 100/110/120 charge capacities. The full acquisition/mapping of success bonuses still needs an explicit tool definition. At skill 120, a failed roll has a 5% chance to grant an “intuition” second try and message. Retry charge consumption, whether it is strictly one retry, and exact roll handling remain unspecified.

Any permitted socket accepts a gem/rune without host-slot stat restrictions. Bonuses stack: 10% existing FHR plus five 10% FHR runes gives 60% FHR. Existing stat formulas/breakpoints still determine the effect of the resulting totals. Filling sockets is permanent: no extraction/replacement; socketables are lost on item salvage.

## Runes and moulds — G01–G09

**The mould now determines the stat.** Crafting no longer chooses a random property: a Crude Mould of the Apprentice or Flawless Mould of Chance produces its corresponding percentage property. Exact property weights and complete mapping still require a catalogue. Flat/binary properties are excluded; no runewords.

| Tier | Mould monster levels | Inscription | New minimum Lumberjacking | Bark consumed | Working tier name |
| --- | --- | ---: | ---: | ---: | --- |
| 1 | 20–29 | 30 | 45 | 1 | Crude |
| 2 | 30–39 | 40 | 50 | 2 | Rough |
| 3 | 40–49 | 50 | 55 | 3 | Shaped |
| 4 | 50–59 | 60 | 60 | 4 | Etched |
| 5 | 60–69 | 70 | 65 | 5 | Refined |
| 6 | 70–79 | 80 | 70 | 6 | Polished |
| 7 | 80–89 | 90 | 75 | 7 | Flawless |
| 8 | 90–99 | 100 | 80 | 8 | Perfect |

Consume one mould, the listed bark quantity and one pen charge. Higher bark substitutes downward but does not change the mould tier. High Quality becomes eligible at recipe skill +10; Superior at +20. Before +10: Average only. Between +10 and +20: Average 90%, High 10%. At +20: Average 89%, High 10%, Superior 1%. Thus Superior T8 needs Inscription 120.

**Provisional formula specified in the replies:** `percentage = selected stat-category minimum + (tier − 1) + quality bonus`, with Average +0 / High +1 / Superior +2 percentage points. A 10% FRW baseline gives T1 Average 10%, T8 Average 17%, T8 Superior 19%. This replaces the old universal 1–8% assumption. The category minima and canonical lowest-tier suffix names come from selected D2 affix data, e.g. Chance/Greed/Balance. **Explicit clarification, 19 October: minimum values apply only to rune baseline calculation. Dropped and imbued gear retain their normal random affix-value rolls within eligible ranges.**

| Monster grade | Mould drop chance |
| --- | ---: |
| Ordinary | 0.1% |
| Champion | 1% |
| Unique random boss with minions | 5% |
| Named dungeon super-unique | 10% |

Previously spent lives cannot reward again; owner credit for pet kills remains. Mould quantity and stat weights are not explicitly specified. The common level-85 roster ceiling must not cap future dungeon content: level 3+ dungeons progress from 60–70 near the entrance through 70–80 and 80–90 to 90–99 near the boss, providing T8 sources.

World difficulty direction confirmed on 20 October 2026: Trammel has Normal and Nightmare outdoor mobs, with Hell mobs restricted to dungeon floor 3. Felucca has Hell mobs in the open world, providing more high-level hunting areas. Specific locations and dungeon populations will be authored later; this does not activate new difficulties in the current runtime. The higher-level dungeon progression above remains a separate encounter-authoring requirement.

Filled sockets raise equipment's minimum character level: 1/2/3/4/5 filled require 30/40/50/60/70. Empty sockets add no requirement. Rune/gem identity itself adds none under this design; existing base/affix/attribute requirements still apply.

## Gathering byproducts and gems — H01–H09

Use **trained Base skill**. A successful wood/ore harvest makes one 5% byproduct roll for bark/gems respectively, not 5% per tier. On a find, select a random unlocked tier and announce the find. Exact quantity/family weights, full-pack delivery and random-selection distribution still need definition.

New Mining quality unlocks are **40/50/60/70/80** for Chipped/Flawed/Standard/Flawless/Perfect. Bark unlocks are **45/50/55/60/65/70/75/80**, as above. These replace the old 50–90 gem and 50–120 bark thresholds.

Retain all nine UO families. Use original D2 gem effects, including equipment-category differences, for the mapped families; the two additions use flat-number effects regardless of slot. Exact UO-to-D2 family assignments and the second added flat effect remain unspecified. Attacker Takes Damage is the stated example; do not choose a companion stat silently. Gem upgrading is allowed, bark upgrading is not; the gem recipe/ratio/requirements are not explicitly supplied.

| Gem quality | NPC purchase price |
| --- | ---: |
| Chipped | 25 |
| Flawed | 75 |
| Standard | 250 |
| Flawless | 500 |
| Perfect | 1,500 |

**Replacement provisional bark prices:** T1–T8 pay **10/20/30/40/50/60/70/80 gold** per unit. Do not restore the discarded exponential rule. Vendors buy but never resell gems/bark. Legacy native gems become decoration only. Gathering-only gem supply conflicts with the new treasure-chest random-gem reward in L08; identify whether that is a usable-gem exception or decorative legacy reward before implementing it.

## Inscription scrolls and tomes — I01–I04

Use UO-style crafting gain chances. Tome initial charges are **3/4/5/6/7/10 at skill 50/60/70/80/90/100**. High Quality adds 1 charge; Superior adds 2, using ordinary quality eligibility/odds. This replaces crafted 20-charge tomes and charge-saving quality bonuses. Tomes remain consumable and cannot be refilled. Vendor capacities/prices and basic scroll/tome recipes remain unspecified.

Crafting a full tome at skill 100 has a 1% chance to additionally produce a single-use Teleport scroll. Intended effect follows the Sorceress/Sorcerer Teleport. Inventory right-click uses a UO target cursor; belt use targets the cursor's world location directly. Belt support and the underlying Teleport ability/range/collision rules are additional implementation work, not existing behavior established by these notes.

## Alchemy — J01–J05

Combat buffs last 30 seconds; repeated use adds duration, not magnitude, up to 90 seconds. A remaining 12 seconds plus one dose becomes 42. Cross-effect coexistence and damage/mitigation arithmetic need definition where not explicit. Keep D2 restorative values/class behavior as the requested baseline; ordinary heal/mana NPC prices are delegated to D2 reference data.

| Potion | Skill | Effect | Listed recipe, plus one empty bottle |
| --- | ---: | --- | --- |
| Second Wind | 30 | +30% run speed | Bloodmoss, Mandrake Root, Sulfurous Ash |
| Instant Heal | 50 | Full health | 3 Super Heal |
| Instant Recharge | 60 | Full mana | 3 Super Mana |
| Rejuvenation | 70 | D2 rejuvenation baseline | 3 Greater Heal + 3 Greater Mana |
| Full Rejuvenation | 80 | D2 full rejuvenation baseline | 3 Super Heal + 3 Super Mana |
| Deep Wounds | 100 | +10 minimum/+10 maximum physical damage | Nightshade, Spider's Silk |
| Magical Penetration | 100 | +10% damage of every element added to spells; precise component formula unresolved | Nightshade, Sulfurous Ash, Bloodmoss |
| Stealing | 110 | +7% life and mana steal | Garlic, Ginseng, Nightshade, Spider's Silk |
| Protection | 120 | −10 damage taken of any type; mitigation ordering unresolved | Garlic, Ginseng, Mandrake Root, Sulfurous Ash |

Quantities for ingredients without numbers and output counts are not explicitly given. Every town/city alchemist sells heal/mana tiers through Greater with unlimited availability. Super tiers are drop-only; rejuvenations remain crafted. Keep 20% empty-bottle recovery and existing capacity-safe handling. Tool/recipe progression must account for the gaps between these initial examples.

## Cooking — K01–K05

One food effect at a time, lasting one hour. No hunger system. Names and concrete economical recipes are delegated for authoring using fish/meat and vendor fruit/vegetables/flour/spices; most ingredient examples use one unit. Tier-1 recipes span skill 0–50, tier-2 50–100, tier-3 100–120, spaced through each band rather than all unlocking together. Proposed Wooden Chip costs are 5/10/30 per tier.

| Meal effect | Tier 1 / 2 / 3 |
| --- | --- |
| Light radius | +1 / +2 / +3 |
| Carry capacity | +25 / +50 / **+74** stones, as written |
| Extra gathering resources | 10% chance of +1 / +2 / +3 |
| Faster gathering | Subtract 1 / 2 / 3 seconds; minimum action duration unspecified |
| Extra corpse materials | +1 / +2 / +3 meat/skins/feathers on eligible corpses |
| Delayed monster aggression | +1 / +2 / +3 seconds before aggression; application/reset conditions unspecified |

On carry-buff expiry, normal encumbrance applies. **Explicit clarification, 19 October: the player manually drops items if needed to move; the game does not automatically spill or select inventory.** Manually dropped items follow normal world-decay rules. Food expiry does not itself remove possessions. This food catalogue supersedes the earlier suggestion that only light/carry effects would be initially considered.

## Cartography, treasure and Fishing — L01–L10

Cartography creates maps and gates treasure hunting; it is the selected crafting job, not a gathering skill. Consumed city/world/dungeon maps give one map buff at a time for **30 minutes**, independent of maker/user skill. No charges. This replaces skill-scaled minutes.

| Map recipe/gain band | Effect |
| --- | --- |
| City, 0–30 | 5% vendor benefit; purchase/sale/both still unspecified |
| World, 30–60 | Treasure search radius +5 tiles; replaces region map |
| Dungeon level 1, 60–80 | +10 Magic Find |
| Dungeon level 2, 80–100 | +20 Magic Find |
| Dungeon level 3+, 100–120 | +30 Magic Find |

City/world effects can operate anywhere. Dungeon effects apply only in the dungeon where activated and end on leaving; exact whole-dungeon versus mapped-level boundaries still need definition. Deciphering treasure maps also grants Cartography skill, using referenced UO behavior as research input.

| Treasure tier | Monster level band, 1% map drop | Skeleton key | Key skill/material | Chance of one random recipe reward |
| --- | --- | --- | --- | ---: |
| Stash | 1–20 | Primitive | 25 / 1 iron ingot | 20% |
| Supply | 21–40 | Clunky | 50 / 1 shadow ingot | 40% |
| Cache | 41–60 | Simple | 75 / 1 bronze ingot | 60% |
| Hoard | 61–80 | Intricate | 100 / 1 agapite ingot | 80% |
| Trove | 81–99 | Splendid | 120 / 1 valorite ingot | 100% |

Recipe rewards include tier-3 Cooking recipes and all skill-120 crafting recipes. Chests also have cash, random gear, random gem and random resources; exact quantities/weights and key charges remain missing. Target encounters are small adapted/copied existing dungeon areas with project spawns; concrete layouts remain later world work. Maps no longer imply new Lockpicking/Remove Trap skills.

Fishing/bottles/kraken rules are delegated to UO references/native behavior. Research must resolve differences between modern five-tier treasure maps, this checkout's native six-level map code, and sea SOS encounters. Do not copy the references' unrelated Magery/Lockpicking/Remove Trap requirements into the retained skill set.

## Gold, vendors and gambling — M01–N03

- Adopt D2-derived gold/drop chances and randomness tentatively, with a target that a 30-minute outing covers repairs and leaves a surplus. Exact UO encounter/base mappings and unsupported rarity handling require authoring/testing, not invented final balance claims.
- **Remove automatic kill-gold collection.** Gold remains in the corpse and is looted through the existing grid. Pet ownership/revived-life reward rules remain.
- Ordinary vendors sell random white/blue gear up to level 41; Britain vendors up to 70. “Level” needs definition as item level versus required character/base level. Drop the class-specific town stock proposal.
- Player-sold items are gone; **no buyback**. Drop/crafted gear gets a generated persistent identified sale offer by base tier: Normal **100–500**, Exceptional **500–1,000**, Elite **2,500–5,000 gold**. Generate it when a vendor first sees the item; do not reroll it by reopening the sales list. Unidentified sells for 10% of the identified offer. Starter items sell for 1. **Confirmed T11:** Vendor-origin gear refunds10% of its original recorded purchase price; legacy unknown-price Vendor gear sells for1 gold. Trades/copy/reload preserve those records.
- Those sale prices **replace**, rather than combine with, the prototype's 10%-of-native/10–25–50 caps. No extra rarity scaling was specified for these new bands.
- Shop lists should provide item-stat tooltips. The current client already has an OPL tooltip hook; actual serial/properties delivery must be checked before adding a replacement UI.
- Put a gambler in every tavern, accessed like a vendor but showing hidden-stat gamble stock. Use D2-derived price/item-level/rarity/base-tier generation as the initial baseline, adapted to the project's item families. All intended item types should be offered; actual Set/Unique support is not thereby implemented.

## Death, insurance, mercenaries and homes — O01–O07

- Death removes **20% of backpack-carried gold**, with bank gold safe and no XP loss. This explicitly replaces the old `min(level, 20)%` curve.
- Player corpses persist for 24 hours, then contents enter escrow. A banker can transfer them to the bank for a content-based fee; declining or lacking payment deletes items under the stated design. Fee/timing/bank-capacity handling are not specified; no automatic deletion behavior is implemented by this document.

| Insured rarity | Normal | Exceptional | Elite |
| --- | ---: | ---: | ---: |
| White | 100 | 200 | 300 |
| Magic | 250 | 500 | 750 |
| Rare | 500 | 1,000 | 1,500 |
| Unique | 1,000 | 2,000 | 3,000 |

Insurance is wanted; payment source, duration/renewal and retention/recovery details remain unspecified. Unique rows are future design, not existing unique items.

Mercenary resurrection costs **level × 1,000 gold**, with gear/inventory retained. Consumable reserve holds healing potions only and cannot be retrieved as player storage. Warn at 10, 5, 1 and zero remaining. Drinking thresholds, any stock limit and the phrase “no carry capacity” require clarification; do not assume ordinary unlimited transport/storage.

Housing includes placed houses bought through architects and town houses, with sign purchasing a proposed interface. Recognition still grants permission rather than a free home. Locations/prices and detailed world placement remain open. Private workshops are not approved by this answer.

## Source verification and remaining work

The supplied Fandom pages could not be opened directly and UOGuide direct requests returned access errors. Search-index excerpts were available for some UOGuide pages. Primary/reference alternatives were checked: [Blizzard gems](https://classic.battle.net/diablo2exp/items/gems.shtml), [suffixes](https://classic.battle.net/diablo2exp/items/magic/suf.shtml), [potions](https://classic.battle.net/diablo2exp/items/potions.shtml), [gambling](https://classic.battle.net/diablo2exp/basics/gambling.shtml), [official UO treasure maps](https://uo.com/wiki/ultima-online-wiki/gameplay/treasure-maps/) and [sunken treasure](https://uo.com/wiki/ultima-online-wiki/skills/fishing/sunken-treasure/). Pinned local D2 tables remain available for implementation-grade verification. The delegation to those systems is resolved direction; the detailed data extraction/mapping is still research/authoring work.

Confirmed code implications:

- Native `TreasureMap.GetMinSkillLevel` in this checkout uses six map levels with thresholds −3/41/51/61/70/70, rather than the modern five named tiers. A version/name mapping is necessary; blindly importing the modern site would change rules beyond the user's custom maps/keys.
- Native `Fishing` has table-based deep-water item chances, including separate TreasureMap and MessageInABottle entries. Its `MessageInABottle` constructs an SOS source; it is not automatically the same as the custom five-tier land-map/dungeon entrance reward.
- Native ingots and cloth weigh 0.1 per unit, leather 1.0; the existing project salvage stacks weigh 0.1 per unit. A fixed 10-unit output with exactly half input weight cannot reuse those weights unchanged for every input form.
- `ShopGump.ShopItem` already calls `SetTooltip(LocalSerial)` when tooltips are enabled. The outstanding check is live vendor OPL availability, not whether any client tooltip mechanism exists.

The worksheet's residual questions separate: (1) genuinely missing decisions; (2) exact recipes/catalogues/reference extraction delegated to implementation; (3) future world content; and (4) still-unanswered wardrobe/title questions. No gameplay code, balance catalogue, binary, save, account, progression or client preference changed during this reconciliation.
