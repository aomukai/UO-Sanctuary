# Profession progression: 0–120

T47 covers all ten crafts and all three gathering skills. Training does not grant120 automatically: the existing ordered Power Scroll caps100→105→110→115→120 remain. Skill locks and independent saved progress remain enforced.

All thirteen skills have paid NPC training0–30. The ten crafts also have optional resource-consuming no-output practice30–70; production can be used instead where a usable recipe is available. Gathering uses real extraction throughout, without practice.

Every trained tenth70.0–119.9 is covered by an implemented production recipe (5,000 coverage checks). A range is a native gain window, not guaranteed skill per attempt. Costs, failures, active craft, public stations and learned recipe requirements still apply. The native fixture separately checks production gain hooks, Cartography boundaries, gathering above100 and save/reload.

| Craft | Implemented production paths after practice | Highest usable gain window |
| --- | --- | --- |
| Alchemy | Restorative mixtures, combat potions, fortification powder | CombatBuff/4: 110–160 (cap120) |
| Blacksmithing | Normal/Exceptional/Elite metal equipment | Assassin Spike (Elite): 119–169 (cap120) |
| Bowcraft/Fletching | Normal/Exceptional/Elite wooden weapons and shields | Tetsubo (Exceptional): 110–170.3 (cap120) |
| Carpentry | Furniture, storage, stations and potion-keg components | Small Bed South deed: 94.7–144.7 (cap120) |
| Cartography | Consumable city/world/dungeon surveys | Dungeon survey III: 100–120 (cap120) |
| Cooking | 18 usable meals, including learned tier3 recipes | Ghostwalk Supper: 115–165 (cap120) |
| Imbuing | White jewellery grades and equipment transformations | Fancy jewellery imbuing: 110–160 (cap120) |
| Inscription | Identify/portal scrolls and tomes; declared runes | Identify tome (10 charges): 100–150 (cap120) |
| Tailoring | Cloth/leather/bone equipment and belts/tabards | Bone Harvester (Elite): 115–165 (cap120) |
| Tinkering | Craft/repair tools, white jewellery, keg components, skeleton keys and gem upgrades | Fancy white jewellery: 110–160 (cap120) |

## Tools, supplies and consumers

| Profession | Production tool / public facility | Input acquisition | Useful consumers and existing perks |
| --- | --- | --- | --- |
| Alchemy | Mortar/pestle; Alchemy workshop | Town reagents/bottles; gathers and chips for advanced recipes | [Belt and manual-keg consumables](crafting.md#alchemy--j01j05) |
| Blacksmithing | Smithing hammer; public forge/anvil | Mined/smelted metal and mapped equipment ingredients | [Wearable weapons/armour; sockets and salvage](t08-recipe-book.md) |
| Bowcraft/Fletching | Fletcher tools; Bowcraft workshop | Harvested boards and mapped equipment ingredients | [Bows, crossbows, wooden weapons/shields](t08-recipe-book.md) |
| Carpentry | Carpentry tool; Carpentry workshop | Boards, cloth, metal; crafted keg components | [Furniture, household/storage deeds and manual kegs](t31-carpentry.md) |
| Cartography | Mapmaker pen; Cartography workshop | Town Blank Maps and crafted/vendor pens | [Activated surveys, purchase discounts and dungeon/search effects](t32-map-surveys.md) |
| Cooking | Skillet; public kitchen | Hunted/fished/gathered food, inn supplies and farm ingredients | [Meals with one timed food effect](t30-cooking.md) |
| Imbuing | Imbuing interface; Imbuing workshop | White equipment, salvaged essence and gold | [Equipped Magic/Rare items](crafting.md#imbuing--e01e05) |
| Inscription | Scribe pen; Inscription workshop | Town scrolls/reagents, leather, moulds and Runebark | [Identify/Portal scrolls and charged tomes; mould/rune outputs](t34-inscription.md) |
| Tailoring | Sewing kit; Tailoring workshop | Hunted leather/bone; bought cloth or wool/yarn/loom | [Cloth/leather/studded/bone equipment](t08-recipe-book.md) |
| Tinkering | Tinker tools; Tinkering workshop | Mined ingots/gems, Runebark and other mapped components | [Tools, keys, white jewellery, keg parts and gem upgrades](crafting.md#tinkering-sockets--f01f08) |

At 100, all ten crafts retain recognition/wardrobe eligibility and the Grandmaster title; gathering earns the title only. At 120, all thirteen retain the Legendary title and their cap. Existing profession-specific100/120 effects and recipe gates remain in the linked specifications: this pass adds no mastery bonuses. Equipment production retains100+ durability/socket quality; 120-gated equipment, rejuvenation, imbuing and key recipes retain their individual learning requirements.

## Ten-point production examples

At 0, 10, 20 use the already available paid trainer; at 30, 40, 50, 60 use optional resource-consuming practice or ordinary production. Practice deliberately produces no item. The table supplies real productive examples for every later ten-point band; the JSON includes all recipes and verifies every tenth 70.0–119.9, including overlapping alternatives.

| Profession | Trained band starts at | Representative usable output | Learning gate |
| --- | --- | --- | --- |
| Alchemy | 70 | InstantMana/1 | Ordinary recipe |
| Alchemy | 80 | Rejuvenation/1 | Ordinary recipe |
| Alchemy | 90 | Rejuvenation/2 | Ordinary recipe |
| Alchemy | 100 | Rejuvenation/2 | Ordinary recipe |
| Alchemy | 110 | CombatBuff/2 | Ordinary recipe |
| Blacksmithing | 70 | Raven Helm (Normal) | Ordinary recipe |
| Blacksmithing | 80 | Gemmed Circlet (Normal) | Ordinary recipe |
| Blacksmithing | 90 | Scepter (Exceptional) | Ordinary recipe |
| Blacksmithing | 100 | Blood Blade (Exceptional) | Ordinary recipe |
| Blacksmithing | 110 | Gemmed Circlet (Exceptional) | Ordinary recipe |
| Bowcraft/Fletching | 70 | Wild Staff (Normal) | Ordinary recipe |
| Bowcraft/Fletching | 80 | Black Staff (Normal) | Ordinary recipe |
| Bowcraft/Fletching | 90 | Elven Composite Longbow (Normal) | Ordinary recipe |
| Bowcraft/Fletching | 100 | Wooden Shield (Exceptional) | Ordinary recipe |
| Bowcraft/Fletching | 110 | Boomerang (Exceptional) | Ordinary recipe |
| Carpentry | 70 | Stone Oven East deed | Ordinary recipe |
| Carpentry | 80 | Large Forge East deed | Ordinary recipe |
| Carpentry | 90 | Easel | Ordinary recipe |
| Carpentry | 100 | Flour Mill East deed | Ordinary recipe |
| Carpentry | 110 | Flour Mill East deed | Ordinary recipe |
| Cartography | 70 | Dungeon survey I | Ordinary recipe |
| Cartography | 80 | Dungeon survey II | Ordinary recipe |
| Cartography | 90 | Dungeon survey II | Ordinary recipe |
| Cartography | 100 | Dungeon survey III | Ordinary recipe |
| Cartography | 110 | Dungeon survey III | Ordinary recipe |
| Cooking | 70 | Prospector's Fishcakes | Ordinary recipe |
| Cooking | 80 | Rushlight Skewers | Ordinary recipe |
| Cooking | 90 | Trapper's Pie | Ordinary recipe |
| Cooking | 100 | Stalker's Batwing Stew | Ordinary recipe |
| Cooking | 110 | Stalker's Batwing Stew | Ordinary recipe |
| Imbuing | 70 | Plain jewellery imbuing | Ordinary recipe |
| Imbuing | 80 | Solid jewellery imbuing | Ordinary recipe |
| Imbuing | 90 | Normal equipment imbuing | Ordinary recipe |
| Imbuing | 100 | Elegant jewellery imbuing | Ordinary recipe |
| Imbuing | 110 | Exceptional equipment imbuing | Ordinary recipe |
| Inscription | 70 | Identify tome (4 charges) | Ordinary recipe |
| Inscription | 80 | Identify tome (5 charges) | Ordinary recipe |
| Inscription | 90 | Identify tome (6 charges) | Ordinary recipe |
| Inscription | 100 | Identify tome (7 charges) | Ordinary recipe |
| Inscription | 110 | Identify tome (10 charges) | Ordinary recipe |
| Tailoring | 70 | Leather Ninja Mitts (Normal) | Ordinary recipe |
| Tailoring | 80 | Leather Do (Normal) | Ordinary recipe |
| Tailoring | 90 | Half Apron (Exceptional) | Ordinary recipe |
| Tailoring | 100 | Fur Boots (Exceptional) | Ordinary recipe |
| Tailoring | 110 | Leather Do (Exceptional) | Ordinary recipe |
| Tinkering | 70 | Clunky skeleton key | Ordinary recipe |
| Tinkering | 80 | Simple skeleton key | Ordinary recipe |
| Tinkering | 90 | Simple skeleton key | Ordinary recipe |
| Tinkering | 100 | Elegant white jewellery | Ordinary recipe |
| Tinkering | 110 | Intricate skeleton key | Ordinary recipe |

## Gathering and sea content

- Mining retains native ore veins, material gates and socket-gem byproducts. Lumberjacking retains native woods, byproducts and Runebark. When the extracted resource's native challenge is exhausted at100+, a successful extraction offers one additional native100–120 gain check. The additional check uses native gain rules with a 50% challenge; its return value does not affect yield. A still-challenging native vein does not get a second gain roll. Yield, success and byproduct probabilities are unchanged.
- Fishing retains native fishing, deep-water mutations, nets, sea creatures and bottle/SOS salvage. The ordinary fish challenge ends at100, so successful catches now support the same100–120 gain check. Fishing maps continue into the custom land-treasure route; bottles/SOS remain the native sea route. No new sea loot table or encounter population is invented by this pass. Native pickaxes/ore veins, hatchets/trees and fishing poles/water remain the respective tools/locations; ores feed smithing, logs feed woodworking, fish feed food and sea catches retain their existing consumers.

## Unlocks, supply and consumers

Town trainers and public facilities cover all thirteen skills/ten crafts. Vendor tools and crafted Tinkering tools provide uses; ore/wood/leather come from harvesting/hunting, cloth from vendor or wool/yarn/loom, reagents/bottles/scrolls from town supply. Mining gems and dropped moulds feed Tinkering/Inscription. Meals, potions, maps, equipment, utilities, furniture and keys have real consumers.

Power Scrolls are skill-specific boss drops with ordered permanent caps. Rare recipes are permanently learned from treasure rewards; tier3 meals and skill120 outputs retain their learning gates. Furniture/station components do not unlock private workshops. Final training speed, resource cost and economic sustainability are T48 work after complete classes and gameplay.

## Verification and remaining scope

Reproduce with `python3 tools/build_profession_coverage.py`; validate with `--check`. The companion is `data/Sanctuary/Balance/profession-coverage.json`. Existing native recipe/consumer suites exercise the actual transactions; coverage of a gain window alone does not certify final gameplay balance. Ten exotic equipment recipes remain excluded by the earlier user decision. New class-specific products and later world placements are separate work, not hidden completed recipes.
