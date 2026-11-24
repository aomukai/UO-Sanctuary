# Items, crafting, and economy

## Profession progression

Characters choose one active craft and keep Mining, Lumberjacking, and Fishing available in parallel. The first craft is confirmed before committing production or paid training. Trained values, caps, locks, and learned recipes persist when switching at an unlocked wardrobe. Public workshops are free to use. Bring the matching tool and come within two tiles of the needed public facility.

Trainers teach through 30.0 at 1 gold per 0.1. Practice consumes ingredients and gives a chance to gain without producing an item, from 30 to 70. The production and gain paths run through 120. Each profession starts with a cap of 100 and uses tradable, skill-specific boss Power Scrolls to unlock 105, 110, 115, and 120 in order. Scrolls raise the cap. Train the points separately. [Profession practice](../data/Sanctuary/Balance/profession-practice.json) · [Full profession and recipe coverage](../tools/reference/reports/profession-coverage.md)

| Profession | Common work |
| --- | --- |
| Alchemy | Mortar and pestle at the workshop. Restorative and combat potions, chitin processing. |
| Blacksmithing | Smithing hammer at forge and anvil. Metal weapons and armour. |
| Bowcraft / Fletching | Fletching tools at workshop. Bows, crossbows, other wooden weapons and shields. |
| Carpentry | Carpentry tools at workshop. Furniture, storage, station furniture, and manual kegs. |
| Cartography | Mapmaking pen. Surveys and deciphering treasure charts. |
| Cooking | Skillet and hearth or kitchen. Meals and timed food effects. Basic meat and fish can also be cooked at a campfire. |
| Imbuing | Town imbuing workshop. Turn suitable white bases and essence into Magic or Rare equipment. |
| Inscription | Scribe’s pen. Identify/Portal scrolls, charged tomes, and socket runes. |
| Tailoring | Tailoring kit. Cloth, leather, studded, and heavy bone gear. |
| Tinkering | Tinker’s tools. Tools, keys, white jewellery, gem upgrades, and sockets. |

Basic resource processing can be performed without choosing its associated crafting job. Native equipment requirements still apply, such as a forge for smelting and a loom for cloth. Many gathering yields can be used in crafting or sold. Town vendors supply tools, reagents, bottles and selected consumables. [Stations](../src/Sanctuary.Content/CraftFacilities.cs) · [Town markets](../data/Sanctuary/Balance/town-markets.json)

## Equipment lifecycle

- **Base equipment.** Ordinary crafting produces white bases. Normal, Exceptional, and Elite crafted gear use item levels 35, 70, and 99 respectively, with base equip levels 0, 60, and 80. At skill 100 and above, eligible crafted bases receive a durability bonus and maker’s mark. High and Superior quality can add starting sockets. Material stats roll when the item is made and remain with it. [Equipment crafting](../src/Sanctuary.Content/EquipmentCrafting.cs)
- **Identification, Imbuing, and salvage.** Reveal unidentified properties through a scroll, tome, or NPC. Valid Imbuing creates magical gear. Salvage eligible equipment for essence. Repair with the craft suited to the physical material, or use an NPC service. Fortification restores lost maximum durability through its chitin route. [Imbuing](../src/Sanctuary.Content/TieredImbuing.cs) · [Salvage](../src/Sanctuary.Content/EquipmentSalvage.cs) · [Repair](../src/Sanctuary.Content/RepairGump.cs) · [Fortification](../src/Sanctuary.Content/FortificationCrafting.cs)
- **Sockets.** Socketable bases support up to five. Tinkering adds empty sockets, with skill gates and falling success odds for later sockets. A failure can destroy the equipment. Filled sockets are permanent. Inscription uses moulds that select the rune’s stat plus Runebark for crafting. Mining and expedition rewards supply socket gems. Gems and runes apply their implemented bonuses through equipment stats. [Socket gump](../src/Sanctuary.Content/SocketGump.cs) · [Tinkering](../src/Sanctuary.Content/TinkeringSockets.cs) · [Rune recipes](../src/Sanctuary.Content/RuneCrafting.cs) · [Gem bonuses](../src/Sanctuary.Content/GemSockets.cs)
- **Named equipment.** Set and Unique definitions are catalogued. Only eligible definitions enter affected acquisition paths, and Set readiness includes its pieces and bonuses. The generated readiness and skill-delivery reports give the exact holds and reasons. Charms and runewords are outside the current design. [Named gear code](../src/Sanctuary.Content/NamedGear.cs) · [Catalog and readiness reports](../tools/reference/README.md#items-and-professions)

## Rewards, insurance, and recovery

Eligible monsters award XP and can drop gold or equipment for one reward life. Gold Find scales eligible gold. Magic Find affects eligible equipment quality. Click corpse loot or supported chest gold to take it. Towns offer merchants, banking, and tavern gambling. [Loot distribution](../src/Sanctuary.Content/ClassLoot.cs)

A normal class-character death charges 20% of carried backpack gold, rounded down across the total. Bank gold, XP and allocations stay with the character. Insure eligible identified equipment through **[Insurance** with an upfront bank premium. Covered items go to the backpack, and renewals are optional. An unclaimed player corpse reaches escrow after 24 real hours. Ask a banker about recovery or open **[Recover**. Fees follow the held item valuation and retrieval rules. Class summons and companions follow their own recovery rules. [Insurance](../src/Sanctuary.Content/EquipmentInsurance.cs) · [Banker escrow](../src/Sanctuary.Content/CorpseEscrow.cs)

Earn wardrobe access through Lord British’s campaign proclamation or crafting recognition at skill 100. Wardrobes preserve each class’s own level, stats, talents, bindings, and equipment state. Craft switching preserves the careers themselves. Recognition also connects to housing eligibility. Check the architect or relevant civic service for plot access. [Recognition](../src/Sanctuary.Content/TownRecognition.cs) · [Housing](../src/Sanctuary.Content/Housing.cs)

## Treasure expeditions

Treasure charts can drop separately from ordinary gear. Their source level determines the chart tier. Active Cartography is used to decipher the chart. Read it for the exact defences before using a digging tool to search for the entrance.

| Chart | Cartography | Expedition monster levels | Defence profile |
| --- | ---: | --- | --- |
| Stash | 27 | 15–20 | Normal |
| Supply | 70 | 30–40 | Nightmare |
| Cache | 90 | 50–60 | Nightmare |
| Hoard | 100 | 70–80 | Hell, one immunity and another high elemental resistance |
| Trove | 100 | 85–99 | Hell, two immunities |

The public campaign uses its own resistance rules. An expedition is private to its owner and pets. The saved dungeon has an empty arrival, encounters, and a final guarded chest. Double-click the entrance and exit. Bring the matching Tinkering key, which is spent on a successful unlock after the guards die. Gold, gear, a gem, resources, and a possible rare recipe vary by tier. A learned recipe persists through class and craft changes. Expeditions last two real hours, with recovery handling for the owner’s property when they expire. [Treasure tiers and rewards](../data/Sanctuary/Balance/treasure-hunts.json) · [Expedition code](../src/Sanctuary.Content/TreasureExpeditions.cs)
