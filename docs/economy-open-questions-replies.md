# Economy and crafting: decisions and remaining questions

Editable worksheet, 18 October 2026. Scope: [crafting handoff](../sanctuary-crafting.md), [economy draft](../sanctuary-economy.md), [implemented economy](economy.md), and [runes and gems](runes-and-gems.md). The crafting handoff and subsequent explicit user confirmations take precedence over older notes.

**Status — corrected 18 October 2026:** User answers are preserved below their stable question IDs. An answer may resolve only part of a multi-part question; unspecified details remain open. Later explicit answers in this document supersede older proposals and prototype values. This is a design handoff, not a statement that these changes are implemented.

**Corrections and provisional interpretations in this edition:**

- F01: maximum equipped sockets corrected to **20 with weapon and shield, or 15 with a two-handed weapon**.
- A04/B05: Imbuing always produces an item; destructive failure belongs to Tinkering socket attempts.
- A05: provisional quality eligibility ceiling at skill 120 prevents top-tier recipes from losing all quality chances. Rune-specific thresholds in G01 still override the general rule.
- D07: repair brackets made non-overlapping; the 100-skill bracket provisionally extends through 119.9.
- G04: provisional rune formula made explicit: lowest affix value + (tier − 1) + quality bonus, in percentage points.
- H08: exponential bark resale replaced with a **provisional 10–80 gold** table. Gem prices are unchanged.
- O01: the written flat **20% carried-gold penalty** is selected over the conflicting “keep current” phrase.
- P: superseded prototype settings are labelled historical and point to the newer decisions. Unanswered fields remain open.

**Already settled, excluded from the questions:** material-plus-rarity-essence durability repairs; essence-only spell-charge restoration; possible maximum-durability loss and a separate special restoration resource; no destructive Imbuing failure; destructive socket failure; one socket per successful attempt; leather and cloth in Tailoring; vendor-origin equipment excluded from salvage/imbuing/socketing; gambling rewards use loot eligibility; single-use rune moulds; nine gem families with five qualities; eight mould/bark tiers and their recorded level/Lumberjacking bands; percentage-only runes; no runewords; gathering-only gems/bark; no fresh rewards from revived lives; no XP loss on death; convenience food versus short combat potions; no tome refilling.

## A. Shared profession and recipe rules

Sources: crafting §§1, 3, 6–8, 12, 16–17.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| A01 | Which professions/actions/representative recipes belong in the first implementation, and which should remain deferred? | Answer recorded below; any unspecified details remain open. |

we will implement them all at once, then iterate.

| A02 | What are the skill-gain rules: gain amount/chance, recipe difficulty bands, gain ceilings per recipe, and intended training pace? Can gains occur on an unsuccessful ordinary craft? | Answer recorded below; any unspecified details remain open. |

we want to provide a UO-esque pace, without the "500 daggers for the trash bin" experience. means gathering skills can go up at the rate they would in UO. all skills need to have a new recipe every 10.0 skill points that are easy to use. an exception is after 110, where during the last 10 points, most of the skill gains would just come from doing the top tier recipes: imbuing lvl 90+ items, punching 4th and up sockets in yellow items (lower chance on lower recipes), like imbuing a lower item, or punching a 2nd or 3rd socket into a blue or yellow, etc)

| A03 | What per-profession and aggregate skill-cap rules apply? Can one character eventually master every retained profession? | **User direction recorded:** independent class/profession progression on one character; one class and one crafting job selected, with Mining/Lumberjacking/Fishing always active. Cartography is a selectable crafting job. Swaps are free/no-cooldown after unlock and require alive/out-of-combat state. Eligibility: class level 80 OR one **crafting** skill at 100; gathering does not qualify. Public town-hall wardrobe access accompanies the unlock. Overweight swaps are allowed with normal encumbrance. Skill caps start at 100 and rise through 105/110/115/120 Power Scrolls from rare dungeon-boss drops. See [wardrobe progression](wardrobe-progression.md) and Q01–Q10 for remaining details. |

| A04 | For ordinary crafting outside the separately settled Imbuing/repair/socket rules, what is the success formula and what inputs are lost on failure? Identify any recipe exceptions, including rune crafting. | Answer recorded below; any unspecified details remain open. |

all skills should be trainable for gold to 30.0 at their respective trainers. past that, i believe we could provide practice recipes every 10.0 skill points for every skill up to 70. these would not create anything. you'd do the recipe and receive nothing. basically, all ressources go directly down the drain. a new practice recipe would have 100% chance to gain 0.1 for the first produced practice thing, and 1% for the last. this would be a way for people who have abundant ressources to "powerlevel" the skill, losses be damned, and it would spare you the need to throw away the items you crafted just for skill gain. all other items in the recipe books will have to be useful and would probably be preferable past a certain point, even while practice recipes are still available. we can use UO-style success/failure chances for ordinary cooking, blacksmithing, tailoring and similar recipes. Imbuing is an explicit exception: a valid attempt always produces magic or rare equipment, consumes the stated inputs, and does not destroy the base. Its random rarity and affixes provide the gamble. Repairs remain reliable; Tinkering socket attempts retain destructive failure.

| A05 | How are ordinary/high/superior crafting qualities produced: probabilities, skill dependence and applicable product types? Which products have distinct quality rules? | Answer recorded below; any unspecified details remain open. |

For ordinary recipes with quality outcomes, High Quality has a 10% chance and Superior a separate, mutually exclusive 1% chance once eligible; the remaining 89% is Average. These are quality rolls after a successful craft, not extra chances for crafting success.

**Provisional ceiling interpretation:** eligibility for both qualities begins at `min(recipe required skill + 10, 120)`. Below that threshold, successful crafts are Average. This allows a skill-120 recipe to produce quality results at 120. Rune recipes instead use the distinct thresholds in G01 and probabilities in G05. Other explicit recipe exceptions take precedence.

| A06 | What tools does each profession require, with what crafting recipe, required skill, uses/charges and consumption per action? Include Imbuing ladle/cauldron choice, Tinkering hammers and Cartography pens. Tool costs are additional to, not replacements for, the settled repair resource model. | Answer recorded below; any unspecified details remain open. |

cartography would have a pen; blacksmiths a hammer for smithing, and a special one for repairs; tailors would have tailoring kits and scissors. tailoring kits would be used for crafting, scissors for repairs (we can just call them "tailoring repair kit"); imbuing could have a ladle as tool (cauldron would be their workbench, similar to the carpentry table or the anvil/forge for the blacksmith, basically the crafting stations in town at crafting halls of the respective guild houses); tinkerers would use tinker's tools. i believe for most crafting, there's already tools and stations in the game files. our imbuing is a bit different, we won't use the big imbuing stations, we'll use a cauldron.

| A07 | What are vendor starter-tool prices, stock quantities and replenishment? Which other permitted ordinary supplies are stocked, and at what prices? | Answer recorded below; any unspecified details remain open. |

i'd keep them cheap at 10gp-ish, with varying charges, between 20 and 30. tinkerer-made ones would have 100 charges, and a 10% chance to not use a charge on use.

| A08 | How are ambiguous equipment bases assigned to crafting/repair professions? Resolve catalogue edge cases such as medium metal armour; leather and cloth already belong to Tailoring. | Answer recorded below; any unspecified details remain open. |

we should separate who makes an item by the material, not the type of armour. if a medium armour is made of metal, blacksmiths craft it. if it's leather, tailors do. cloth, leather and studded would be tailor. repairs would be handled by the craft that creates the item.

## B. Equipment, materials and acquisition

Sources: crafting §§3–6; economy §§4–5.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| B01 | For each initial equipment recipe: base, normal/exceptional/elite availability, skill requirement, resource type/quantity, tool/station requirements and output quantity? | Answer recorded below; any unspecified details remain open. |

crafters only ever craft bases.
normal item recipes become available between 0 and 80 skill, exceptional between 80 and 110, and elite between 110 and 120.

| B02 | How is crafted equipment item level determined? This governs later affix eligibility and salvage yield separately from material, rarity and crafting quality. | Answer recorded below; any unspecified details remain open. |

normal gear lvl 35, exceptional gear lvl 70, elite gear lvl 99. normal gear has no level requirement to equip, exceptional requires level 60, elite requires level 80, plus what str or dex requirements an item might have.

| B03 | Which coloured metals, woods and hides are available initially, at what gathering thresholds, frequency, quantity and locations? Which native resource/discovery rules should remain for now? | Answer recorded below; any unspecified details remain open. |

we'll go with the native UO gathering, where anything is available anywhere as a chance to proc on performing the gathering at a specific skill level. this pertains to wood and ore. UO also already has leather types associated with creatures (spined, horned etc), which we can leave as-is, too. we just insert gems into mining and bark into lumberjacking.

| B04 | What exact deterministic bonuses does each coloured material give to weapons and armour? Define flat versus percentage units and how they combine with affixes/socket bonuses. | Answer recorded below; any unspecified details remain open. |

ores:
armour could give 1-5% of their respective resistance, weapon could give 5-10% of the weapon's base damage as bonus damage of an element.
iron: nothing
shadow: physical
bronze: poison
agapite: lightning
valorite: fire
dull copper: cold
copper: a random element
gold: 1-2% magic find (armour) / gold find (weapon)
verite: heals 1 hitpoint per 5 seconds (armour) / mana per 5 seconds (weapon)

wood:
plain: nothing
oak: fire
ash: lightning
yew: poison
bloodwood: physical
heartwood: a random element
frostwood: cold

leather:
normal: nothing
spined: cold and lightning
horned: fire and physical
barbed: guaranteed poison and a random element

| B05 | Do crafted whites have a chance to start with sockets at 100 skill? If adopted, specify chance/count and its interaction with the socket cap. This was only a proposal. | Answer recorded below; any unspecified details remain open. |

crafted items can proc with up to 2 sockets. we can maybe set the general socket limit on items to 5. so a normal item would be crafted without a socket, a high quality with 1, and a superior with 2. Tinkering socketing goes 100%/50%/25%/5%/5% success chance. so a superior base that already has 2 sockets would start at 25% chance to punch the first new (the third) socket. 100.0 skill unlocks the procs.

| B06 | At what crafting skill is extra durability granted? If adopted, specify amount and interaction with crafting quality. | Answer recorded below; any unspecified details remain open. |

at 100.0 skill, items would be crafted with extra durability (+10-20%) and the crafter's name on it. UO already has "crafted by playername" somewhere in the code IIRC.

| B07 | How much leather/hide, meat and other recipe material does each eligible beast provide, and what corpse/species eligibility applies? | **Answered in part:** butchering/skinning requires no skill and no separate active profession. Native UO quantities/species rules are retained below. |

we'll go with default values from UO. i think a dragon gives 30 barbed leather or something.

| B08 | What nonvendor sources supply alchemical reagents and other cooking ingredients not yet assigned? | Answer recorded below; any unspecified details remain open. |

meat from beasts. we can provide flour, fruits, veggies and spices at inns for not so much money, where the hearth/kitchen would be the "cooking crafting station". UO magery reagents like blood moss or black pearl would be used by alchemists, bottles come from these vendors as well, and from the chance to keep an empty bottle when consuming a potion. we can also make kegs as carpenter (they should already have those as recipes)

| B09 | Is any NPC ingot supply permitted? Ore/wood/leather gathering exceptions must not be bypassed through an unapproved processed-resource vendor. | Answer recorded below; any unspecified details remain open. |

metal, wood and leather should only come from gathering. cloth is the one exception, but it should be expensive - alternatively, players can go through the process of gathering wool from sheep (UO mechanic) and producing yarn and then bolts in the tailor's shop/loom/spinning wheel, which is already in the game. we need to remove the option to cut up cloth and create bandages, as we won't use those in the game.

| B10 | May starter equipment ever be imbued/socketed/salvaged, or should the prototype's starter-protected exclusions remain? Its retention through death is already settled. | Answer recorded below; any unspecified details remain open. |

no, starter equipment will be "newbie" (persist through death), but can't be salvaged, socketed or imbued. selling them should get the player 1gp per item.

| B11 | Does the potion tabard/belt receive combat affixes and sockets, and within what budget? Other designated cosmetic layers must remain without combat contribution. | Answer recorded below; any unspecified details remain open. |

combat affixes yes, as we're simulating D2's belts, but not sockets. gloves and boots can also not be socketed. sockets are allowed for chests, helmets, weapons and shields. all other slots (arms, gorgets, pants, cloak) will only be cosmetic, no stats or sockets. imbuing will be available for helmets, chests, gloves, boots, tabard/belt of white quality from drops or crafting.

## C. Salvage and material processing

Sources: crafting §5; economy implementation §§Salvage, Imbuing. Existing variable recovery settings are in P03–P05 below.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| C01 | What are direct-production recipes for ingots → Metal Scraps and logs/boards → Wooden Chips: quantities, skill, tool use and gains? | Answer recorded below; any unspecified details remain open. |

i'd go with 1 ore/ingot = 10 metal scraps, 1 log/board = 10 wooden chips, 1 leather = 10 leather strips, 1 cloth = 10 cloth scraps. producing those would be done with the profession's designated production tool, but shouldn't cost anything (no charges). all of these should give skill points up to 30.0 skill (and become trivial at 30). producing these should also reduce their weight by half, so if you go out to lumberjack for wood chips and get to your carrying capacity, you should be able to craft wood chips and have some more leeway (similar for the other resources)

| C02 | Adopt direct leather → Leather Strips and cloth → Cloth Scraps recipes? If so, specify their quantities, requirements and gains. | Answer recorded below; any unspecified details remain open. |

see C01

| C03 | Which recipes consume Leather Strips and Cloth Scraps, and how much? Straps/grips/padding/upholstery/repair tools remain examples. | Answer recorded below; any unspecified details remain open. |

i would just add those to normal items the profession crafts. a tinkerer would use them to craft lock picks and tools for the other profession. a cook would use them as fuel for cooking the recipes. leather strips would be used to repair leather armours (in addition to some leather of the material the armor is made of, like barbed or horned), cloth scraps to repair cloth armour, metal scraps to repair metal armour (plus ingots of the metal used to craft that item, like copper or gold). for wooden shields, bows etc, wood chips make no sense, so those would take the normal materials used to craft them, like yew or bloodwood (logs/boards)

| C04 | Does salvaging itself require skill/tools or grant gains? The prototype has no skill threshold/gains and a configurable gold fee. | Answer recorded below; any unspecified details remain open. |

no skill needed. needs the crafting tool for that profession, but salvaging costs no charges, so a tailoring kit or blacksmith's hammer would last you forever. anyone can destroy gear ;)

## D. Repairs, spell charges and durability

Source: crafting §9. Repair success and resource categories are settled; the following details are not.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| D01 | Which material forms are consumed: ingots/boards/leather/cloth, salvage materials, or an explicit choice? | Answer recorded below; any unspecified details remain open. |

skins need to be cut to leather to use them. ore needs to be in ingot form to use, and logs need to be turned into boards. bones can be used as-is to create heavy armour (bone should be heavy, the only heavy armour a tailor can craft)

| D02 | Must coloured equipment be repaired with its exact coloured subtype, or can ordinary matching material be used? | Answer recorded below; any unspecified details remain open. |

must be the exact material

| D03 | How much material restores how much current durability? Specify rounding/minimum costs and whether repairs can be partial. | Answer recorded below; any unspecified details remain open. |

i'd say we go with a flat 1-3 ingot/board/leather/sheet of cloth for normal, 3-5 for exceptional and 5-10 for elite gear, randomly rolled on each repair.

| D04 | What is the durability-repair essence cost by rarity, including white gear? Does it scale with durability restored or other item properties? | Answer recorded below; any unspecified details remain open. |

white gear has no magic properties, so no essence needed to repair. blue would take 1-3 for normal, 3-5 for exceptional, 5-10 for elite. yellow would take 2x as much. randomly rolled on each repair.

| D05 | What is the essence cost for restoring spell charges, by number/type of charges and rarity? Define partial versus full recharge. | Answer recorded below; any unspecified details remain open. |

i'd say 1 essence per 1 charge

| D06 | If one action repairs durability and restores charges, are costs simply combined or calculated differently? | Answer recorded below; any unspecified details remain open. |

combined. you repair an exceptional blue axe, you need 3-5 iron ingots and 3-5 essence.

| D07 | What chance does an ordinary repair have to lower maximum durability? Does the chance depend on amount repaired, item/rarity or repairer skill? | _open_ |

| Repairer skill | Chance to lose maximum durability | Loss when triggered |
| --- | ---: | ---: |
| Below 80.0 | 50% | 1–3 |
| 80.0 to below 100.0 | 25% | 1–2 |
| 100.0 to below 120.0 | 10% | 1–2 |
| 120.0 | 5% | 1 |

**Provisional gap fix:** the 100-skill bracket extends through 119.9; the original text did not assign 110–119.9. These are durability-loss chances, not chances for the repair itself to fail.

| D08 | How much maximum durability is lost, what is its minimum floor, and what happens when equipment reaches zero current durability? | Answer recorded below; any unspecified details remain open. |

see D07. when an item has 0 durability, it can still be equipped, but it will be just cosmetic. a weapon at 0 durability will not work as a weapon, you'd hit with your bare hands. an armour piece at 0 durability will give no protection, you're functionally naked. any stats on the gear will also be shut down.

| D09 | What wear rate should ordinary gameplay use, or should native wear remain initially? | Answer recorded below; any unspecified details remain open. |

we'll use UO default rates initially and tune as needed.

| D10 | What is the special maximum-durability restoration resource, where/how often is it obtained, and how much maximum durability does each use restore? Include any restoration limit or additional requirements. A hive is only a candidate source. | Answer recorded below; any unspecified details remain open. |

all Solen, Terathan and other arachnid creatures will have a chance to drop chitin. chitin can be broken down to "powder of fortifying" with the alchemy crafting tool (mortar and pestle)

| D11 | What repair/recharge tools are required and how are their uses consumed? Specify any requirements different from the shared tools in A06; resource costs above still apply. | Answer recorded below; any unspecified details remain open. |

see A06

| D12 | How do NPC services handle material and essence: player supplies them, vendor supplies them for gold, or both options? What gold fee/premium applies? | Answer recorded below; any unspecified details remain open. |

NPCs require the same resources a player would need, plus a monetary fee. i would say 100gp for normal items, 500gp for exceptional, 2500gp for elite items, for starters. we can later tune that. NPCs repair like a crafter at 80.0 skill

| D13 | Adopt a skill-based resource-saving chance, such as the earlier 30% at 120 proposal? If so, which resources/actions qualify and what is the curve? | Answer recorded below; any unspecified details remain open. |

30% at 120, 25% at 100, 20% at 80, 10% before 80

| D14 | Does spell-charge-only restoration cause any durability loss? No such loss has been approved; specify whether to keep it exempt. | Answer recorded below; any unspecified details remain open. |

no durability loss for use of charges, and repairing items who have no lost durability only take essence to repair

## E. Imbuing

Sources: crafting §7; implemented economy §Essence-funded imbuing. Valid completed actions always produce an item; blue/rare rerolling is not an approved action.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| E01 | What Imbuing skill requirements apply by base/item level/tier? Define recipe gain bands alongside A02. | Answer recorded below; any unspecified details remain open. |

imbuing normal gear will take 80 skill, exceptional at 100, elite at 120. imbuing will have practice recipes before that, see A04. i believe we should also let tinkerers craft white rings and amulets at skill level 30, 50, 70, 90, 110 that can be imbued by an imbuer with the same imbuing skill requiring.
30er jewelry: shoddy (ring/amulet of) <affix name>
50er jewelry: plain (ring/amulet of) <affix name>
70er jewelry: solid (ring/amulet of) <affix name>
90er jewelry: elegant (ring/amulet of) <affix name>
110er jewelry: fancy (ring/amulet of) <affix name>

| E02 | Keep the current fixed 5 essence + 100 gold cost, or scale it by item level/base/tier or another explicit rule? Tool costs are specified separately in A06. | Answer recorded below; any unspecified details remain open. |

let's make it 100gp/5e for normal, 500gp/10e for exceptional, 1000gp/20e for elite gear

| E03 | Keep the current 75% Magic / 25% Rare split, or vary it with skill or recipe? No destructive-failure chance is needed. | Answer recorded below; any unspecified details remain open. |

let's keep it below 120 skill. at 120 skill, we can make it 70% blue, 30% yellow

| E04 | Which additional equipment affixes should the eventual Imbuing pool support, including binary properties, with what eligibility/ranges/weights? The current pool only admits implemented effects; this larger pool can explicitly remain deferred. | Answer recorded below; any unspecified details remain open. |

everything from the normal stats (what runes and gems can give), plus binaries

| E05 | How do installed sockets and material benefits survive a rarity change if socket caps differ by rarity? Preservation is the intended rule; resolve cap compatibility in F01 rather than silently deleting properties. | Answer recorded below; any unspecified details remain open. |

sockets have nothing to do with rarity

## F. Socket creation, tools and socketable use

Sources: crafting §8; runes/gems implementation dependencies. Proposed base success values are already listed in P01.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| F01 | What maximum socket counts apply by base/slot, base tier and rarity? Decide whether caps are rarity-independent or define how E05 is preserved. | Answer recorded below; any unspecified details remain open. |

max 5 slots for all items that allow sockets (helm, chest, shield, weapon). a player with all max socket gear can have 20 sockets (or 15, for 2h weapon wielders)

| F02 | What Tinkering skill requirements apply to each socket/base, and does skill affect eligibility only or also success? Include behavior above 100 skill. | Answer recorded below; any unspecified details remain open. |

normal gear from skill 30, exceptional from skill 80, elite gear from skill 100. skill 30 can punch the first socket, 50 the second, 70 the third, 90 the fourth, 110 the 5th  - chance to produce first socket 100%, second 50%, third 25%, fourth and fifth 5%. 120 skill will have a 5% chance on failure to give the tinkerer an "intuition" with a system message like: "you have a bad feeling with this. you take a deep breath and try again." for a second try, as 120er skill perk.

| F03 | What gold/material/tool costs are consumed per attempt, including failed destructive attempts? | Answer recorded below; any unspecified details remain open. |

an attempt costs a tinker tool charge, and potentially the item, nothing else. the item might blow up, that's enough of an (emotional) cost ;)

| F04 | Are proposed hammer bonuses +1/+2/+3 additive percentage points or relative percentages? Example: 5% becomes 8% or 5.15% at +3. Final success is capped at 100%. | Answer recorded below; any unspecified details remain open. |

additive percentages. 8% is still only 8%.

but we need to implement a rule: tinkerer-socketed items can't be imbued. items crafted natively with 1-2 sockets can. imbued items can then be socketed. an item crafted with sockets, and then some tinker-added sockets on top are also excluded. there should be a warning before punching a hole into a white item: "socketing this item prevents it from being imbued. continue anyway?" - because we want to make losses sting. else you'd just go to a tinkerer with a stack of 100 viking swords and don't care how many are destroyed.

| F05 | How are those hammer bonuses acquired? Are average/high/superior labels crafting quality, Imbuing affix tiers or a defined combination? Supply probabilities/requirements and any bonus limits. | Answer recorded below; any unspecified details remain open. |

all tools are crafted by tinkerers, and tinkerers create their own hammers. average/high quality/superior should just follow the normal rules for crafting. all tools of high quality could give 10%  more charges (110), and superior tools would have 120 charges

| F06 | Which equipment categories accept which rune/gem properties? Do gear-slot affix restrictions also constrain socket effects? Define stacking/caps with existing bonuses. | Answer recorded below; any unspecified details remain open. |

if it's a socket, it accepts a gem or a rune. no restrictions. if you have a helmet with 10% FHR and plug in 5 10% FHR runes, you'll have a helmet with 60% FHR.

| F07 | Can installed socketables be removed or replaced? If so, what is consumed/destroyed/recovered and what costs apply? | Answer recorded below; any unspecified details remain open. |

something that's socketed can't be taken out anymore, and an item with runes or gems socketed can't change what's in these sockets anymore. adding a socket is permanent, and so is slotting something into a socket.

| F08 | What happens to installed socketables when equipment is salvaged? The gear is consumed; socketable recovery versus destruction is not yet specified. | Answer recorded below; any unspecified details remain open. |

whatever is in sockets is lost. a sword with 0 sockets and the same sword with 5 sockets filled with runes/gems should both just get metal scraps.

## G. Inscription runes and mould rewards

Sources: crafting §13; runes/gems §§Progression, Quality. One mould is consumed per rune; the existing eight bands are not being reopened.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| G01 | What Inscription skill is required for each rune tier? Lumberjacking thresholds do not set Inscription eligibility, and low runes need not wait until 110. | Answer recorded below; any unspecified details remain open. |

T1: 30 skill required
T2: 40 skill required
T3: 50 skill required
T4: 60 skill required
T5: 70 skill required
T6: 80 skill required
T7: 90 skill required
T8: 100 skill required

high quality and superior would each occur at required skill +10/20 respectively, so a superior T8 rune can only happen at 120 skill.

| G02 | How much suitable Runebark does each rune consume, plus any other recipe ingredients/gold? Tools are covered in A06. | Answer recorded below; any unspecified details remain open. |

let's keep it simple: T1 requires 1 bark, T8 requires 8 bark of their respective type (crude, rough etc) or above, plus 1 charge of the scribe's pen.

| G03 | Which percentage properties are in the initial rune pool, and what is each property's selection weight? Choose whether semantic stats have equal odds rather than inheriting affix-row counts. | Answer recorded below; any unspecified details remain open. |

i would set the drop rate very low, we can start experimenting with 0.1% chance to drop. a rune mould should already determine the stat type: a "crude mould of the apprentice" or a "flawless mould of chance". this way, moulds will be pretty rare to begin with, and finding the desired mould can take a while.

| G04 | Which percentage properties share the tier-plus-quality numerical curve and which need different amounts/caps? The 1–8% FCR plus 0/1/2 quality-point example is already recorded; define the other properties. | Answer recorded below; any unspecified details remain open. |

i would go with always the lowest possible intensity roll for drops/imbuing.  let's take this example:

Faster Run/Walk (& Slower Stamina Drain)
Suffix 	Stats
Pacing 	10%
Haste 	20%
Speed 	30%
Transportation 	30% & 80-90%
Acceleration 	40%

These are stats that can roll on drops or when imbuing. Use the lowest eligible value in the selected stat category as the rune's baseline.

**Provisional explicit formula:** `rune percentage = category minimum + (tier − 1) + quality bonus`, with quality bonuses Average +0, High Quality +1, Superior +2 percentage points. For the 10% Faster Run/Walk baseline above, T1 Average gives 10%, T8 Average 17%, and T8 Superior 19%. Sol should obtain each category minimum from the chosen affix data; do not assume every category starts at 1%. This supersedes the earlier universal 1–8% example. Only percentage properties are eligible; binary/flat properties remain excluded.

| G05 | What are Average/High Quality/Superior rune chances, and how does Inscription skill affect them? | Answer recorded below; any unspecified details remain open. |

Use A05's quality probabilities, with G01's rune-specific eligibility: below required skill +10, Average only; from +10 to below +20, 10% High Quality and 90% Average; at +20 or above, 1% Superior, 10% High Quality and 89% Average. These outcomes are mutually exclusive.

| G06 | What character-level requirements apply to runes/socketed items, and how do they interact with the host item's requirements? | Answer recorded below; any unspecified details remain open. |

i'd not make equip requirements dependent on the runes/gems, but on the number of filled sockets. say you have a normal item with 5 sockets. you can equip it at the following values:

1 filled socket at level 30
2 filled sockets at level 40
3 filled sockets at level 50
4 filled sockets at level 60
5 filled sockets at level 70

you can equip it without additional requirements if you don't fill any of the sockets. in that case, you just have an item with 5 sockets, and all of them are empty, to be filled once you're high enough to wield such power.

| G07 | Confirm final intermediate tier names and canonical stat suffixes. Current working tiers are Crude/Rough/Shaped/Etched/Refined/Polished/Flawless/Perfect; FCR names include unselected Spellcasting/Magus proposals. | Answer recorded below; any unspecified details remain open. |

here's a list of suffixes: https://diablo.fandom.com/wiki/Suffix_(Diablo_II)
note that we only ever use the lowest tier of a category ("of chance for magic find, "of greed"" for gold find, "of balance" for faster hit recovery, and so on)

| G08 | What mould drop chance/quantity applies per eligible monster, and are there encounter/difficulty modifiers? Previously spent lives cannot roll again. | Answer recorded below; any unspecified details remain open. |

0.1% chance to drop from normal monsters, 1% chance to drop from champions (blue monsters), 5% chance to drop from unique monsters (random bosses that spawn with 5 minions), 10% chance from super uniques (named bosses in dungeons)

| G09 | Where will tier-8 moulds come from? Their band is monster level 90–99, while the current common Hell reward roster caps at 85. Future higher-level enemies/bosses versus changing the bands requires an explicit decision. | Answer recorded below; any unspecified details remain open. |

Dungeon level 3+ can contain monsters above level 85; 85 is not a minimum for the entire floor. The level-85 cap does not apply here. in a dungeon level 3+, monsters close to the entrance will be 60-70, then 70-80 further inside, then 80-90 even deeper, and 90-99 near the boss.

## H. Runebark and mining gems

Sources: crafting §§4, 13–14; runes/gems §§Progression, Mining, Code baseline. Nine families/five qualities and recorded unlock thresholds are already specified.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| H01 | Do gathering unlocks use trained Base skill or modified skill Value? Apply the choice consistently to bark and gems unless an exception is intended. | Answer recorded below; any unspecified details remain open. |

trained base skill

| H02 | What bark discovery chance/quantity applies, and how are finds distributed among unlocked tiers? | Answer recorded below; any unspecified details remain open. |

i'd make both barks from lumberjacking and gems from mining a 5% chance. with 5 levels of gems and 8 of barks, i'd make it so that 40, 50, 60, 70 and 80 skill are the threshold at which you can find the next higher tier of gems and 45, 50, 55, 60, 65, 70, 75, 80 for barks, and when you find a bark or gem, it's of random tier up to the highest you can find. that means at 5% chance to find a gem or bark, it can be any gem or bark of any tier at 80 skill, not a 5% chance per tier.

| H03 | What gem discovery chance/quantity applies, with what family weights and distribution among unlocked qualities? | Answer recorded below; any unspecified details remain open. |

see H02

| H04 | What triggers a byproduct roll: successful resource harvesting, successful backpack delivery, or another explicit event? What happens with a full pack—no byproduct, loss, placement at feet or another rule? | Answer recorded below; any unspecified details remain open. |

let's make it successful harvesting, and you get a system message that you just found X

| H05 | What is the complete nine-family property mapping? Choose the second extra flat effect alongside Attacker Takes Damage; Mana After Each Kill and Magic Damage Reduced by X remain candidates. Amber/Star Sapphire assignments and Citrine/Topaz or Tourmaline/Skull analogies are also unselected. | Answer recorded below; any unspecified details remain open. |

let's use the original diablo stats: https://diablo.fandom.com/wiki/Gems
for the unassigned, we use flat-number stats regardless of slot

| H06 | Does a gem's effect change between weapons, shields and armour, or is it the same wherever eligible? Specify the five-quality value table for every chosen family/category. | Answer recorded below; any unspecified details remain open. |

see H05

| H07 | Are gem/bark upgrading or conversion recipes allowed? If adopted, give inputs, outputs, skill and costs; no D2 three-to-one rule has been adopted automatically. | Answer recorded below; any unspecified details remain open. |

yes for gems, no for bark

| H08 | What small NPC purchase price applies to each bark tier and gem family/quality? NPCs must not offer the sold stock back for purchase. | Answer recorded below; any unspecified details remain open. |

gems: chipped 25gp, flawed 75gp, normal 250gp, flawless 500gp, perfect 1500gp
**Replacement provisional bark resale table (gold per unit):**

| Tier | Bark | NPC pays |
| --- | --- | ---: |
| 1 | Crude | 10 |
| 2 | Rough | 20 |
| 3 | Shaped | 30 |
| 4 | Etched | 40 |
| 5 | Refined | 50 |
| 6 | Polished | 60 |
| 7 | Flawless | 70 |
| 8 | Perfect | 80 |

The previous multiply-by-three rule is removed; it would have reached 21,870 gold per Perfect bark. These replacement values are a modest starting point for playtesting, not a balanced final price list. Vendors purchase bark/gems but never resell them.

| H09 | How are pre-existing native gems treated when socket gems arrive: migration, decorative/crafting-only legacy items, or replacement? Vendor-origin legacy gems must not become an unapproved socket-gem source. | Answer recorded below; any unspecified details remain open. |

decoration only (can lock down in your house, for example)

## I. Inscription scrolls, tomes and Teleport bonus

Source: crafting §12. Tomes are consumed through use and cannot be refilled.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| I01 | Confirm the proposed progression bands: scrolls to 50, tomes 50–100, full tomes 100–110 and runes for gains at 110–120. Which recipes/gain ceilings distinguish the tome bands? | Answer recorded below; any unspecified details remain open. |

let's use UO-typical crafting gain chances here

| I02 | What are Town Portal/Identify scroll and tome recipes, ingredient/output quantities, skill requirements and NPC prices? Specify initial versus maximum charges on produced tomes. | Answer recorded below; any unspecified details remain open. |

tomes would start with 3 charges at skill 50, 4 at 60, 5 at 70, 6 at 80, 7 at 90, 10 at 100 skill.

| I03 | How are high/superior tome qualities obtained, with what probabilities/skill dependence? I03 below supersedes the older charge-saving proposal in P08; quality-production odds remain separate. | Answer recorded below; any unspecified details remain open. |

high quality and superior would follow normal crafting rules, and high would just add +1 charge, superior would add +2 charges

| I04 | Adopt the bonus single-use Teleport scroll from full-tome crafting at 100+? If so, give chance, exact requirements and intended behavior/restrictions equivalent to the proposed level-1 Sorceress effect. | Answer recorded below; any unspecified details remain open. |

crafting a full tome at skill 100 should have a 1% chance to also craft a teleport scroll. the scroll would function like town portal/identify scrolls in D2: right-click to use (and then give a UO target cursor), or put it in a belt slot and execute from there (and then you teleport to where your cursor is, without the UO target cursor). restrictions should be the same as the sorc teleport

## J. Alchemy, restorative supply and bottles

Sources: crafting §10; economy §9. Short combat effects and Alchemy-only rejuvenation direction are settled; exact catalogues are not.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| J01 | Which potion effects/tiers belong in the initial catalogue, with what magnitudes and skill requirements? Include rejuvenation/full rejuvenation recipes. | Answer recorded below; any unspecified details remain open. |

potion of the second wind (+30% runspeed for 30 seconds - craftable at skill 30)
instant heal (full health - craftable at skill 50),
instant recharge (full mana - craftable at skill 60),
rejuvenation - craftable at skill 70,
full rejuvenation - craftable at skill 80,
potion of deep wounds (+10 min, +10 max physical damage for 30 seconds - craftable at skill 100),
potion of magical penetration (+10% damage of every element added to spells, for 30 seconds - craftable at skill 100),
potion of stealing (+7% life and mana steal for 30 seconds - craftable at skill 110),
potion of protection (-10 damage of any type taken for 30 seconds - craftable at skill 120),

| J02 | What duration does each combat effect have? How do same/different tiers combine: refresh, extend, replace or coexist? Define duration caps, effect categories and any cooldowns. | Answer recorded below; any unspecified details remain open. |

30 seconds, duration (but not effect) stackable 3 times, up to 90 seconds. if your timer is at 12 seconds and you drink one more, your timer now sits at 42 seconds.

| J03 | What are ingredient/reagent/bottle quantities, recipe output quantities and tool use for each potion? Reagent price and bulk quantities both affect cost. | Answer recorded below; any unspecified details remain open. |

second wind: bloodmoss, mandrake root, sulfurous ash and an empty bottle
instant heal: 3 super heal and an empty bottle
instant recharge: 3 super mana and an empty bottle
rejuv: 3 greater heal and 3 greater mana and an empty bottle
full rejuv: 3 super heal and 3 super mana and an empty bottle
deep wounds: nightshade, spider's silk and an empty bottle
magical penetration: nightshade, sulfurous ash, blood moss and an empty bottle
stealing: garlic, ginseng, nightshade, spider's silk and an empty bottle
protection: garlic, ginseng, mandrake root, sulfurous ash and an empty bottle

| J04 | Which healing/mana tiers are sold by NPCs, at what prices/stock, and which drop from monsters at what rates? Whether these ordinary restoratives also need craft recipes is not settled as an Alchemy progression requirement. | Answer recorded below; any unspecified details remain open. |

let's sell all tiers up to greater at every alchemist vendor in every city/town, super would be drop-only. no availability limits (vendor always stocked), we can take the prices from diablo 2 for starters

| J05 | Keep the existing restorative amounts/time profiles as the initial balance, or choose replacements? Their class scaling and mixed-tier reapplication should remain explicit rather than being replaced implicitly by the buff catalogue. | Answer recorded below; any unspecified details remain open. |

keep d2 values

The bottle-return chance is an existing tuning value (P09). The installed belt's capacity/overflow and bottle-capacity handling remain the comparison baseline; those mechanics are not new unanswered interface questions.

## K. Cooking and hunting outputs

Source: crafting §11; economy §10.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| K01 | What initial food effects/tiers are available, with what values and skill requirements? Light radius and carrying capacity are examples, not a complete menu. | Answer recorded below; any unspecified details remain open. |

i would have 3 tiers per meal type
light radius +1/2/3
carrying capacity +25/50/74 stones
10% chance to get 1/2/3 more resources when gathering
gathering speed accelerated by 1/2/3 seconds
1/2/3 additional meat/skins/feathers when using a bladed weapon on an eligible corpse
aggressive monsters wait 1/2/3 seconds before aggroing

i'm not very creative here when it comes to naming and the concrete recipes, but i'd make the meals take fish from fishing, meat from butchering, fruits, veggies and other such ingredients from the UO cook vendors. none of them should be very expensive to make. 1 of each fish, meat, veggie, fruit, flour or whatever. tier 1 foods should be available at skill ranges 0-50, tier 2 at 50-100, tier 3 at 100-120, not all at the same skill - spaced out in the bracket, for example light radius tier 1 at 0, carrying capacity tier 1 at 10, and so on. cooking should also use wooden chips, maybe 5 for tier 1, 10 for tier 2, 30 for tier 3 meals.

| K02 | What duration does each meal have? Roughly one hour is a proposal; define refresh/extension, effect categories and whether different meal effects coexist. | Answer recorded below; any unspecified details remain open. |

i'd keep it to 1 food effect at a time, and 1h duration

| K03 | What are food recipes, ingredient/fuel quantities, output quantities and tool use? Include fish/meat, ordinary purchased ingredients and Wooden Chips as appropriate. | Answer recorded below; any unspecified details remain open. |

see K01

| K04 | Is any hunger requirement intended, or are meals optional effect consumables without a hunger system? | Answer recorded below; any unspecified details remain open. |

no hunger effects, food should be buffs only

| K05 | When carrying-capacity food expires while the character is overweight, what encumbrance/movement rule applies? Possessions must not be destroyed. | Answer recorded below; any unspecified details remain open. |

overweight/encumbered rules apply. if the char can't walk at all anymore, drop it on the ground. item decay would follow normal UO server settings.

## L. Cartography, keys, treasure maps and fishing

Sources: crafting §§8, 15–17; economy §11. Exact world layouts can be deferred without guessing these mechanics.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| L01 | What are town/region/dungeon map recipes and their Cartography requirements/gain bands? How are the mapped place and eligibility determined? | Answer recorded below; any unspecified details remain open. |

towns and dungeons are probably easy, as these zones have names. regions, i'm not sure UO even has "regions". maybe we should replace region with world, then it would be a map of the entire continent.

writing these maps would give skill from - up to:
city maps 0-30
world maps 30-60
dungeon level 1 maps 60-80
dungeon level 2 maps 80-100
dungeon level 3+ maps 100-120

deciphering treasure maps should also give skill: https://www.uoguide.com/Treasure_Map

| L02 | Are usable maps consumed on activation, charged, or reusable? Specify charge counts, cooldowns and stacking rules. | Answer recorded below; any unspecified details remain open. |

city/world/dungeon maps are consumed on double click, no charges. 1 buff at a time, buff duration 30 minutes

| L03 | Is map-buff duration based on the maker's recorded skill or the user's current skill? What formula handles skill 0, 1–9, 10–19 and exactly 120, with what rounding? | Answer recorded below; any unspecified details remain open. |

duration is 30 minutes regardless of skill

| L04 | Confirm/change the proposed map bonuses: town vendor benefit 5%, region search radius +5 tiles, dungeon-level Magic Find +10%. Does the town bonus discount purchases, improve resale, or both? Define stacking/caps without a buy/sell profit loop. | Answer recorded below; any unspecified details remain open. |

city map: town vendor benefit 5%
world map: region search radius +5 tiles
dungeon level 1 map: magic find +10 in this dungeon (leave the current place and the buff drops)
dungeon level 2 map: magic find +20 in this dungeon (leave the current place and the buff drops)
dungeon level 3 map: magic find +30 in this dungeon (leave the current place and the buff drops)

| L05 | Where do map effects apply, and what happens on leaving/re-entering the town/region/dungeon level? | Answer recorded below; any unspecified details remain open. |

city and world anywhere, dungeon maps just in that one specific dungeon where the buff was activated.

| L06 | What treasure-map tiers and Cartography breakpoints exist, and which monsters/fishing bottles supply each tier at what rates? | Answer recorded below; any unspecified details remain open. |

here's a link to the TH site at uoguide: https://www.uoguide.com/Treasure_Map

we can make the maps come from:
stash: monsters lvl 1-20 at a drop rate of 1%
supply: monsters lvl 21-40 at a drop rate of 1%
cache: monsters lvl 41-60 at a drop rate of 1%
hoard: monsters lvl 61-80 at a drop rate of 1%
trove: monsters lvl 81-99 at a drop rate of 1%

| L07 | What Tinkering key tiers match which chest tiers, with what recipes, required skill, charges and charge-consumption rules? | Answer recorded below; any unspecified details remain open. |

stash: primitive skeleton key (1 iron ingot, craftable at skill 25)
supply: clunky skeleton key (1 shadow ingot, craftable at skill 50)
cache: simple skeleton key (1 bronze ingot, craftable at skill 75)
hoard: intricate skeleton key (1 agapite ingot, craftable at skill 100)
trove: splendid skeleton key (1 valorite ingot, craftable at skill 120)

| L08 | Which rare profession recipes can appear as treasure rewards, with what eligibility and drop weights? | Answer recorded below; any unspecified details remain open. |

cooking tier 3 recipes, and all skill 120.0 crafting recipes.
chance for 1 random recipe (alongside cash, random gear, a random gem, random resources):
stash: 20%
supply: 40%
cache: 60%
hoard: 80%
trove: 100%

| L09 | What fishing skill thresholds, fish yields, bottle chances and map contents apply initially? Which native fishing behavior is retained? | Answer recorded below; any unspecified details remain open. |

see these links:
https://www.uoguide.com/Message_in_a_Bottle
https://www.uoguide.com/Fishing

| L10 | What triggers a kraken encounter, at what chance/skill/location, and what difficulty/reward is intended? | Answer recorded below; any unspecified details remain open. |

see L09

## M. Gold, ordinary vendors and equipment drops

Sources: economy §§2–5, 12, 14; implemented economy §Reconciliation. Drops are currently disabled; numerical settings below do not imply they are already active.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| M01 | What gold-drop formula/chance/range applies by monster level and encounter/difficulty? Define Gold Find's effect and bounds. No one-tenth D2 multiplier or coupling to XP awarded has been adopted. | Answer recorded below; any unspecified details remain open. |

let's take a look at D2 and adopt those numbers tentatively (average per level gold with some randomness)

| M02 | What equipment-drop chances/counts, base-tier eligibility, item-level rules and rarity weights apply to the first encounters? Which encounters have exclusive rewards? | Answer recorded below; any unspecified details remain open. |

we want gear drop chances to mirror D2's

| M03 | How is automatic kill-gold collection handled when capacity prevents transfer? What owner/party distribution applies? Owner credit for pet kills is already settled. | Answer recorded below; any unspecified details remain open. |

let's scratch automatic kill-gold, players loot it from the corpse. i've tested the grid view, and it's perfectly servicable.

| M04 | What does a representative short outing mean in minutes, and what expenses/savings should it support at starter/mid/endgame levels? Give rough targets rather than a complete price list if preferred. | Answer recorded below; any unspecified details remain open. |

a 30 minute run should be enough to sustain repair costs and make some bonus cash. i'd adopt d2 numbers for now, we need to finetune them after testing. it's impossible to say what feels fun without playing for real.

| M05 | Which ordinary equipment bases/rarities/normal-or-exceptional tiers do vendors stock at each progression band? Define prices, quantities, restock, town specialties and locations. The earlier 1–40/41–70 bands are proposals. | Answer recorded below; any unspecified details remain open. |

vendors should sell random white and blue items up to level 41. britain vendors would sell gear up to 70. let's forget the "amazon gear in skara brae, sorc/necro gear in moonglow" idea, too convoluted.

| M06 | What buyback behavior should player-sold eligible gear have: who can repurchase it, at what price, and with what retained eligibility? Origin must not be laundered by sale. Vendors may buy gems/bark at H08 prices but must not resell them. | Answer recorded below; any unspecified details remain open. |

gear sold to a vendor is gone, no buyback. any gear sold to a vendor that's drop or crafted should sell for 100-500 gold (normal), 500-1000 (exceptional), 2500-5000 (elite). a price can be generated ad hoc when the player puts an item on the sales list (see UO vendor behaviour and buy/sell menu). that price would be saved to the item. so, all items need a "price" entry, and a price is generated the moment a vendor sees it for the first time.

an unidentified item would go for 10% of what it would bring when identified.

PS: for the vendor list, mouseover an item, and a tooltip shows the stats. this behaviour is not normally in UO and might need to be implemented (if ClassicUO doesn't have it yet)

## N. Gambling

Source: economy §5. Rewards use ordinary loot improvement eligibility, not ordinary shop-stock restrictions.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| N01 | Which equipment types can be selected, and how does gold price scale with base/character level? | Answer recorded below; any unspecified details remain open. |

let's adapt the d2 system for now, we can adapt prices later: https://diablo.fandom.com/wiki/Gamble
generally, all item types should be on the list, and the prices should be in the same general range they are for d2 items in d2

| N02 | How is reward item level calculated, and what are base-tier/rarity probabilities? Are actual uniques eligible once their system exists? | Answer recorded below; any unspecified details remain open. |

not sure what you're asking. if you mean what you can gamble for at which level, d2 would provide the answer

| N03 | Which NPC/location provides gambling, and what access/progression requirements apply? | Answer recorded below; any unspecified details remain open. |

i'd have a gambler in every tavern. you'd use them like a normal vendor, only that they offer gambling instead of selling gear whose stats you can see when you mouseover it.

## O. Death, insurance, companions and housing

Sources: economy §§12–14; crafting §10 for consumables. These are broader/later economy decisions, not prerequisites to every crafting recipe.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| O01 | What final death-gold penalty and source apply? The current comparison implementation takes min(level, 20)% of carried/corpse gold and leaves bank gold safe; retain or change explicitly. No XP penalty is needed. | Answer recorded below; any unspecified details remain open. |

Use a flat 20% penalty on gold carried in the backpack at death. Bank gold remains safe and there is no XP penalty. This explicitly replaces the prototype min(level, 20)% curve; it is not a request to retain that curve.

| O02 | How long do player corpses persist, how does logout affect them, and how should recovery handle full packs/equipment conflicts and multiple deaths? | Answer recorded below; any unspecified details remain open. |

player corpses should persist for 24 hours, then be in escrow. a player can go to a banker and pay a fee, based on the contents of the corpse, to have the items transferred to their bank box. if the player refuses or lacks the funds, the items are deleted.

| O03 | Is equipment insurance wanted alongside the newer corpse-recovery rules? If so, define eligible items, fee/reinsurance and what remains on the corpse. | Answer recorded below; any unspecified details remain open. |

yes, item insurance is wanted:
100gp for a white normal item, 200 for a white exceptional, 300 for a white elite
250gp for a blue normal item, 500 for a blue exceptional, 750 for a blue elite
500gp for a yellow normal item, 1000 for a yellow exceptional, 1500 for a yellow elite
1000gp for a unique normal item, 2000 for a unique exceptional, 3000 for a unique elite

| O04 | What mercenary resurrection price applies, and how does it scale? Broader recruitment/loadout design remains later work. | Answer recorded below; any unspecified details remain open. |

let's go with mercenary level *1000gp. the merc won't lose any gear or items in their inventory.

| O05 | What potion inventory/limits, drink thresholds, in-progress-healing rules, eligible potion types and low-stock warnings should mercenaries use? | Answer recorded below; any unspecified details remain open. |

mercenary inventories can only carry healing potions, nothing else. they have no carry capacity. mercenary inventories are read-only: a player can't use the merc to carry their potions for them. a merc will let the player know they have 10, then 5, then 1 potion left, and when they finally run out.

| O06 | Are houses player-placed, purchased vacant town houses, or both? Define initial availability, acquisition prices and the intended ordinary-play saving timescale. No rent/tax/upkeep has been adopted and none is implied by this question. | **Answered in part:** housing permission comes from the class-level-80 OR crafting-skill-100 recognition ceremony; gathering does not qualify and Cartography does. It grants the right to place/buy, not a free house. The unlocked public town-hall wardrobe permits switching before owning a home. Actual housing availability, locations/prices and savings targets remain _open_. |

both. houses are sold at the architect NPC. houses in towns might be purchasable from their sign - that needs to be implemented first.

| O07 | What initial dungeon/region/boss/chest layouts and reward tables are needed for treasure entrances, special restoration resources and housing/world placement? This can be answered with a deferred-content list rather than designed now. | Answer recorded below; any unspecified details remain open. |

we'll look at the map and find usable dungeons. likely smaller dungeon levels from existing dungeons, only with spawn adapted to the map (temporary copies of the empty dungeon, plus our own )

## P. Starting proposals and current prototype tuning

This section distinguishes historical prototype settings from the newer decisions above. It does not claim that the new rules are installed. Unchanged prototype values are retained as provisional baselines; they can be tuned later. Unique recovery remains reserved until real unique support exists.

| ID | Setting | Authoritative starting rule / status |
| --- | --- | --- |
| P01 | Socket success | F01–F05: five-socket cap; base chances 100% / 50% / 25% / 5% / 5%, destructive failure, tool bonuses and skill-120 retry perk. |
| P02 | NPC gear resale | M06 replaces the old 10% native-price rule and 10/25/50 caps: normal 100–500, exceptional 500–1,000, elite 2,500–5,000; unidentified 10% of identified offer, persistent generated price, no buyback. Starter gear remains 1 gold under B10. Unspecified origin cases remain open. |
| P03 | Magic/Rare essence salvage | Existing provisional baseline: Magic 80% recovery, 1–2 essence; Rare 90%, 2–5. |
| P04 | Physical salvage | Existing provisional baseline: each family 85% recovery, quantity [1+s, 3+s], s=floor((itemLevel−1)/10); independently configurable per material. |
| P05 | Salvage fee / future Unique | Existing fee 0 gold. Reserved Unique essence: 95% recovery, 5–12; inactive until actual unique support. |
| P06 | Imbuing | E01–E03 supersede the flat prototype cost: normal 100 gold + 5 essence; exceptional 500 + 10; elite 1,000 + 20. Below 120: 75% Magic / 25% Rare; at 120: 70% / 30%. No destructive failure. |
| P07 | Rune numbers | G04's explicit provisional minimum-affix-plus-tier formula replaces the universal tier% example. Quality bonus +0/+1/+2 points; G01/G05 govern eligibility and odds. |
| P08 | Tomes | I02/I03 replace crafted 20-charge tomes and quality-based charge saving: 3/4/5/6/7/10 charges at skill 50/60/70/80/90/100, HQ +1 and Superior +2 charges. No refills. Vendor capacities/prices still need a decision consistent with this new ladder; the old vendor-10 proposal is not adopted here. |
| P09 | Empty-bottle return | Retain existing provisional 20%; preserve capacity-safe handling. |
| P10 | Potion belt capacities | Retain existing unbelted 1 row and five tabard tiers 2/2/3/3/4 rows, four columns; preserve interface/overflow behaviour. |

## Q. A03 follow-up: wardrobe, class profiles and town crafting

Source: the user's A03 answer and subsequent clarifications, recorded in [wardrobe progression](wardrobe-progression.md). Free/no-cooldown swaps require alive/out-of-combat state. One class and one crafting job are selected; all three gathering skills stay active. Cartography is a craft and qualifies for the crafting-only skill-100 recognition route. The class-80 alternative and 100→120 Power Scroll progression remain recorded. Answered parts below are not being reopened.

| ID | Unresolved question / values needed | First implementation answer |
| --- | --- | --- |
| Q01 | How does a first crafting profession obtain gathering materials before wardrobe access? | **Resolved:** Mining, Lumberjacking and Fishing are always active and do not use the crafting slot. Gathering cannot earn Pillar of Society. |
| Q02 | What happens to access/progression if a personal wardrobe or house is moved, sold or lost? | **Answered in part:** a public town-hall wardrobe is available after unlocking, before home ownership. Personal furniture loss/storage/progression ownership details remain _open_. |
| Q03 | How is out-of-combat state detected, and how are active actions/pending targets handled during a swap? | **Answered in part:** swapping requires being alive and out of combat, free/no-cooldown after unlock. Exact combat detection and action/target handling remain _open_; pool/status rules are in Q04. |
| Q04 | What health/mana/stamina, buff/debuff and timer state is shared versus saved per class, and how does it advance while that class is inactive? Switching must not silently become a free refill/cleanse/reset. | _open_ |
| Q05 | How are class summons suspended/restored, with what preserved health, remaining lifetime and follower-slot accounting? How are ordinary pets/mercenaries treated compared with class summons? | _open_ |
| Q06 | Which remaining state is shared versus class-specific: backpack/bank/gold, potion-belt contents, quests/reputation, position and UI preferences? Class level/XP, allocations/talents, equipped gear, hotkeys and summons are already intended to persist per class. | _open_ |
| Q07 | Can one real gear item be referenced by multiple class loadouts? Define storage/capacity/ownership and what happens if a saved item has been moved/sold/lost or the target class cannot equip it. | **Answered in part:** swapping to a weaker class may leave the character overweight; allow it and apply normal encumbrance. Item identity/storage/equipment eligibility details remain _open_. |
| Q08 | How are initial class/profession choices and first activation of another class handled, including starting gear/bindings? What is the ownership scope of the unlock/certificates/housing right (character or account), which town hall is selected, and does later earning the other achievement grant its second title/certificate? | _open_ |
| Q09 | Are Power Scroll cap unlocks stored per profession on the character, and must 105/110/115/120 be applied in order? Specify whether scrolls are profession-specific, their boss eligibility/drop weights, and trading restrictions if any. | _open_ |
| Q10 | Which crafts require town facilities, and which portable exceptions are allowed? Private home workshops remain undecided; initial town usage fee is zero. Define any fee knob's future basis without enabling a fee now. | _open_ |
| Q11 | What title does each class earn at 99 and each skill earn at 100? Are higher skill ranks added at 105–120, and what are they named? | **User proposal recorded:** optional class prefix at 99 and skill suffix at 100, including gathering; example Archmage Joe, Legendary Blacksmith. Names/additional ranks remain _open_. Grandmaster 100 / Legendary 120 is an assistant recommendation, not a chosen replacement. |
| Q12 | Can title slots select any earned class/skill title, or must they match the active class/crafting job? | **Answered in part:** class prefix and skill suffix are independently optional. Selection scope remains _open_; freely choosing from earned titles is a recommendation. |
| Q13 | How do Hero of the People/Pillar of Society honors and legacy fame/skill titles fit the selectable title display? | _open_. Gathering may earn a skill title at 100 but still does not qualify for Pillar of Society or wardrobe/housing rights. No change to criminal/notoriety rules is implied. |

## Suggested answer format

Use the answer column, or write `D01: ...`, `D02: ...` in chat. Section P now points to the latest rules rather than inviting retention of superseded values. Items can be answered in batches; unanswered items remain open, and `defer` does not authorize an invented default.

No changes to gameplay, live state or existing balance values are made by creating this worksheet.
