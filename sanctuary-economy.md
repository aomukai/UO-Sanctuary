# Sanctuary economy design

> **Superseded handoff — 19 October 2026 reconciliation:** The body below preserves the earlier draft/history. The completed [user replies](docs/economy-open-questions-replies.md) and [current first-implementation specification](docs/crafting.md) replace conflicting values, rules and unanswered-question labels. The [decision tracker](docs/economy-open-questions.md) identifies genuinely remaining parts. This historical body must not be used to restore old socket costs, random-stat rune crafting, gathering thresholds, resale caps or charge-saving tomes. Current explicit user clarifications continue to take precedence.

Conversation decisions consolidated on 18 October 2026 (JST).

Suggested repository location: `docs/economy.md`. This is a design handoff, not a report of verified repository implementation. Reconcile it with the current code before changing systems. It records the conversation; it does not attempt to reproduce every Diablo II economic rule.

**Precedence update, 18 October 2026:** [Sanctuary crafting and gathering](sanctuary-crafting.md) records later decisions and supersedes this older draft where they conflict. This revision incorporates those corrections without treating proposals as final values. Subsequent explicit user decisions confirm single-use rune moulds and nine gem families. [The economy implementation notes](docs/economy.md) distinguish installed prototype behavior from design, and [runes and gems](docs/runes-and-gems.md) records socketable details. Earlier assistant recommendations are not adopted rules.

## 1. Purpose and decision status

Sanctuary combines Diablo-style combat and itemisation with UO's gathering, professions, housing and persistent world. It is a cooperative game for solo players, friends and small communities, including people with limited time after work.

The economy should let players enjoy a short outing, pay their expenses and save towards meaningful goals. A quiet evening of crafting, fishing or furnishing a house should also be worthwhile.

Throughout this document:

- **Decided** means the user chose the design direction; it does not mean implementation is complete.
- **Provisional** means a suggested starting number or mechanism that still needs tuning.
- **Open** means no final decision was made. Do not silently turn these into requirements.

The latest explicit decisions take precedence over earlier brainstorming. Keep numerical values configurable and record future changes here.

## 2. Economic goals

**Decided:**

- Gold should retain purchasing power. Short ordinary trips should not routinely generate enormous fortunes.
- Normal adventuring should leave a surplus even when using the more expensive NPC repair route. New players must be able to support themselves without already having trained professions.
- Player professions offer savings and useful products; they should not become compulsory maintenance chores.
- Desirable services and purchases should cost enough to matter. Housing is a longer-term savings goal.
- Becoming comfortably wealthy eventually is acceptable. The aim is to avoid uncontrolled accumulation, not to prevent success.
- Optional improvement gambles can destroy equipment. Maintaining existing equipment should be reliable.
- Gold Find should have a meaningful economic role. Its value must ultimately be assessed against kill-speed equipment and actual gold earned per minute.
- Inventory uses UO-style weight and carrying capacity rather than Diablo inventory Tetris. Houses and chests provide storage and organisation.

No global income rate, house price, repair price, fee schedule or acceptable savings duration has been chosen.

## 3. Gold sources, sinks and transfers

| Mechanism | Economic role | Design status |
| --- | --- | --- |
| Monster gold | Creates currency | Amounts and curves open |
| Selling loot to NPCs | Creates currency | Resale prices need balancing alongside direct gold |
| Player-to-player sales/services | Transfers existing currency | Not itself a gold sink |
| NPC repairs | Removes currency | Reliable, more expensive than player repairs |
| NPC-supplied repair tools | Potential tool-related currency sink | Tool requirements/charges open; do not replace material-and-essence repair costs |
| Equipment gambling vendor | Removes currency and generates equipment | Desired system; exact catalogue and odds open |
| Socketing fees | Removes currency | Modest fee desired; amount open |
| Failed socketing | Removes equipment | Destructive gamble is intentional |
| Death penalty | Removes some gold | Formula and source of gold open |
| Mercenary resurrection | Removes currency | Desired; price open |
| Housing and NPC purchases | Potential major gold sinks | Acquisition/prices open; buying from a player is a transfer |
| Potions, ingredients and consumables | Consume goods; may remove gold through NPC sales | Depends on their source |

Reducing D2-style gold drops to one tenth was floated as an experiment, not adopted as a final formula. Coupling gold to XP awarded was also considered, but no coupling rule was settled. Keep those questions open rather than inferring a fixed curve from this document.

Recommended tuning approach: measure outing income, NPC resale income, routine expenses and remaining savings separately. Check new characters as well as established ones. A global gold multiplier can make experiments easy, but dividing income and all prices equally changes denomination rather than affordability.

## 4. Equipment structure and origin

Keep these concepts separate:

- **Base:** dagger, halberd, armour type, etc.
- **Base tier:** normal, exceptional, elite.
- **Rarity:** white/common, blue/magic, yellow/rare, gold/unique.
- **Item level and eligibility:** which affixes and rolls the generated item may receive.
- **Material:** deterministic base characteristics from resources.
- **Origin:** where the item was generated and whether improvement is permitted.
- **Sockets:** installed count and allowed maximum.
- **Durability:** wear and repair state.

Base tier is not rarity. A white elite base and a rare normal base are different combinations. UO's crafting quality terminology must not accidentally stand in for Diablo's exceptional base tier.

### Origin policy

| Generated source | Imbue white base | Add sockets | Role |
| --- | --- | --- | --- |
| Ordinary vendor stock | No | No; effective maximum zero | Predictable quick upgrade, intended to be replaced |
| Monster/world loot | Yes, when eligible | Yes, within the item's cap | Unpredictable adventuring rewards |
| Player-crafted gear | Yes, when eligible | Yes, within the item's cap | Product of gathering and professions |
| Equipment gambling | Same as normal dropped loot | Same as normal dropped loot | Paid random reward |
| Starter equipment | Not settled | Not settled | Special persistent starting equipment |

**Decided:** ordinary purchased equipment cannot be upgraded. Vendor whites cannot be imbued; vendor whites and blues cannot have sockets punched into them. Gambling-generated gear counts as normal loot, not restricted shop stock. A separate origin label for gambling is not required to obtain that behaviour.

Implementation recommendation: preserve origin through player trading, storage, relog and transformations. Define resale/buyback behaviour explicitly so transactions cannot launder vendor stock into eligible loot. Ordinary shop-stock generation is the restriction, not merely being handled by a merchant at some point.

Starter gear is a special case: it stays with its character through death unless deliberately destroyed. The necromancer starter dagger's Teeth bonus belongs to that starter item, not to every dagger base.

### Tiers and affixes

The direction is to use D2-derived base families and affix eligibility as a numerical baseline, adapted to UO equipment. There is no adopted universal percentage increase between normal, exceptional and elite bases.

The working affix model reported by Sol is one prefix and one suffix at most for magic items; rares target 3–6 affixes, capped at three prefixes and three suffixes. Some stronger affixes are magic-only. Verify against the selected data version when implementing. More affixes must not imply every rare can roll the strongest magic-only values.

Respect equipment-slot restrictions and item-level gates. IAS on gloves versus its exclusion from boots was an explicit example. Do not give every item access to every affix.

Extra UO equipment slots such as gorgets and arm pieces were designated cosmetic white equipment to avoid adding an entire extra set of combat bonuses. Cosmetic means no hidden contribution to the combat budget. A later exception gives the tabard slot the potion-belt role; whether it also receives combat affixes is open.

## 5. Ordinary vendors, gambling and drops

### Ordinary shops

Shop equipment provides an accessible, predictable replacement or upgrade when drops have been unhelpful. Its lack of socketing and imbuing keeps dropped and crafted bases valuable.

The intended shop tiers are normal and exceptional, not elite. Normal at levels 1–40 and exceptional at 41–70 were tentative stocking bands, not adopted D2 requirements. Town specialities, stock levels and prices remain open. Higher-level bows in Yew was an example, not a final assignment.

### Gambling vendor

A Gheed-style equipment gamble is a desirable repeatable outlet for surplus gold. It generates ordinary loot with the same improvement eligibility as dropped items.

Open: price scaling, selectable base types, item-level calculation, rarity probabilities, tier probabilities and access to uniques. No gambling vendor location or identity is fixed. Do not copy ordinary vendor restrictions onto its rewards.

### Loot sources

Monster gear is unpredictable. The world should also have specific rewards associated with particular bosses and other sources, enabling players to seek desired items. Exact tables, exclusivity and probabilities come during content design.

Monster drops were deliberately disabled during foundational equipment conversion. That is a development state, not the final economy.

Previously killed and resurrected monsters do not award fresh XP or loot. Resurrection restores a threat, not its reward entitlement.

## 6. Socketing: Tinkering

**Decided direction:** Tinkering adds sockets one at a time. Failure destroys the item. This is an intentional equipment sink and improvement gamble. Looted gear starts without sockets under the current design.

**Latest proposed base success schedule:** first socket **100%**, second **50%**, third **25%**, fourth and later **5%**. This supersedes the older 10%-per-socket proposal. The first socket is safe once requirements are met; later attempts destroy the item on failure. These are provisional balance values, not installed socket mechanics. Skill requirements and any tool bonus remain open. A proposed +1/+2/+3% hammer bonus has not been defined as additive percentage points versus a relative multiplier; do not choose the arithmetic implicitly.

Charge a modest gold fee per attempt so the service also removes currency without making attempts prohibitively expensive. The fee amount, tools and material cost remain open.

Each base needs a maximum-socket field. The project's caps will differ from D2. The contribution of base type, tier and rarity to those caps is unresolved; do not inherit D2 monster-level socket rules automatically.

Ordinary vendor stock is permanently ineligible. Dropped, crafted and gambling-generated loot are eligible subject to other rules.

Crafted white bases potentially starting with sockets at 100 crafting skill was an earlier proposal, not a settled recipe rule.

## 7. Imbuing and materials

**Latest decision:** Imbuing takes eligible white bases and randomly produces blue or yellow equipment. Both resulting rarity and affix rolls are random within the appropriate rules. A 75% magic / 25% rare split was suggested but is not fixed.

This supersedes the earlier white-to-rare-only concept. No destructive imbuing-failure chance was chosen. Do not import socketing's destruction rule into imbuing.

Ordinary vendor whites are ineligible. Eligible bases come from drops, crafting or equipment gambling. Re-imbuing blue/rare items and reroll loops are not approved mechanics.

Earlier discussion intended base material and installed sockets to survive imbuing. Preserve that design unless explicitly revised; define compatibility with any final rarity-specific socket caps before implementing such caps.

Different gathered materials should give crafted bases useful deterministic characteristics, including resistance bonuses. Verite/physical, shadow/poison and gold/magic were illustrative custom mappings, not a final table or a statement of stock UO bonuses.

Potential crafted-base advantages include additional durability, possibly at 120 crafting skill, and the socket proposal above. Values and eligibility are open. Tier-upgrade recipes for dropped/crafted equipment have not been decided.

## 8. Repair and durability

### Reliability and skill requirements

Repairs do not fail. A player must meet the skill required to craft the corresponding base. A broadsword requiring 80 Blacksmithing to craft also requires 80 to repair; magic properties should not create a separate random maintenance gamble.

NPC repair is the convenient, more expensive option. Player repair requires skill and appropriate tools, with a lower cost. Ordinary outings must remain profitable even for players relying on NPCs.

### Resource costs and maximum durability: later decisions

The later crafting handoff replaces the repair-kit-only proposals:

| Operation | Decided resource rule |
| --- | --- |
| Repair current durability | The item's matching material plus Magic Essence according to rarity |
| Restore spell charges only | Magic Essence only |
| Restore lost maximum durability | A special resource, with its identity/source still open |

Ordinary durability repairs succeed when requirements are met but have a chance to reduce maximum durability. Reliability means no failed repair action; it does not mean immunity to maximum-durability wear. The loss chance, amount, floor and relationship to amount repaired remain open. A hive-related restoration resource was proposed, not selected.

Material forms (ingots/boards/leather/cloth versus salvage outputs), exact coloured subtype requirements, quantities, rarity-based essence costs and combined repair/recharge pricing remain open. Tools may be required, but tool selection/charges are separate decisions and must not replace these resource costs. The earlier 30% resource-saving proposal is unconfirmed under this model. Charge-only restoration does not automatically inherit durability loss.

### Current repair profession mapping

| Profession | Latest stated repair responsibility |
| --- | --- |
| Blacksmithing | Heavy armour and metal weapons |
| Bowcraft / Bowyer | Wooden shields and wooden weapons |
| Tailoring | Medium and light armour, including both leather and cloth; no separate Leatherworking skill |

Reconcile material/classification edge cases against the equipment catalogue. Earlier broader assignments must not silently override this latest mapping.

## 9. Alchemy, potions and the belt

**Profession identity:** alchemists provide short-lived emergency combat tools. Potions should be clutch decisions, not a compulsory set of continuously maintained buffs.

- Ordinary healing and mana potions drop from monsters.
- Rejuvenation and full rejuvenation are intended to move out of drop tables and into alchemist recipes.
- Other short combat effects may include resistance protection, IAS, FCR and related stats. Exact catalogue, magnitude and duration are open.
- Drinking reliably activates the effect; do not import UO bandage slipping/interruption into potion consumption.
- Immediate restoration versus restoration over time is defined per potion, not by a universal instant-heal rule.
- The earlier idea of alchemical out-of-combat regeneration remains a candidate, subject to the latest short-duration potion identity.

D2-style short potion durations are the inspiration; no universal 30-second duration or 90-second stacking cap has been adopted. Stacking, refresh rules and simultaneous-effect limits are open. IAS/FCR bonuses must respect the breakpoint system.

**Provisional bottle rule:** drinking returns an empty bottle 20% of the time. Most bottles are consumed/lost. Inventory stacking and capacity handling for recovered bottles need implementation decisions; bottle recovery must not prevent the potion itself from working.

The tabard slot serves as the potion belt: four columns with hotkeys 1–4, and a variable number of rows depending on the equipped item. Exact capacity tiers and overflow behaviour are open. Enter-to-chat is fixed so gameplay keys remain usable; active text entry must not consume potions.

Mercenaries should accept a supplied potion inventory and drink autonomously, with low-stock warnings. Consumption logic belongs in ordinary code; personality can affect dialogue. Thresholds, inventory limits and treatment of healing already underway remain to be set. Broader mercenary loadout/recruitment design is outside this document.

## 10. Cooking

**Latest decision:** cooking provides longer-lived quality-of-life benefits, distinct from alchemy's short combat effects.

Examples include increased light radius and carrying capacity for roughly an hour. These are proposals, not a final menu or fixed duration. Meals should support gathering, travel and comfortable outings without becoming required combat preparation.

Earlier suggestions for food granting FCR, combat stats or similar power are superseded by this clearer profession split. Food stacking, hunger mechanics, expiry behaviour and exact recipes remain open. Expiry should not destroy possessions; the handling of excess carried weight needs an explicit rule.

## 11. Gathering, crafting, salvage and other professions

Crafting and gathering progress by doing, separately from combat class levels and talents. Final resources, recipes, locations, gain rates and training pace are deliberately deferred.

### Salvage direction

- Only dropped, crafted and gambling-generated equipment is eligible. **Ordinary vendor-origin gear cannot be salvaged**, including through alternate native recycling routes.
- Physical material determines Metal Scraps, Wooden Chips, Leather Strips or Cloth Scraps; item level controls its variable yield. Armour weight does not determine salvage material.
- Rarity controls a separate variable Magic Essence roll. Eligible magical gear may yield both essence and physical material; white gear gives no essence. Recovery chances include zero output.
- Metal scraps should also be made directly from ingots, and wooden chips from logs/boards. Direct leather/cloth recipes remain candidates. Recipe yields and skill gains are open.

The alternative direct recipes prevent forced manufacture of unwanted equipment just to supply another profession. Physical salvage, essence recovery and origin restrictions have a playable prototype; its current numbers are documented in [equipment economy](docs/economy.md) and remain provisional. Direct-production recipes and profession training are not implemented by that prototype.

Check eventual purchase prices and recipes for unintended buy/salvage/profit loops. Imbuing followed by salvage must remain a net resource expense overall.

### Other profession roles discussed

| Profession/system | Intended role or candidate |
| --- | --- |
| Carpentry | Housing furniture; possible masonry later |
| Inscription | Runes made from PvE-dropped moulds |
| Tinkering | Socket attempts and crafted chest keys |
| Mining / Lumberjacking / Fishing | Resource supply and noncombat outings |
| Cartography | Decipher treasure maps and locate buried dungeon entrances |

**One rune mould makes one rune and is consumed.** Charged chest keys remain a separate desired product. Better keys serve higher-tier chests, allowing removal of a separate Lockpicking requirement. Key charges, recipes, key/chest matching and opening rules remain open.

Runes have one random percentage-based property and no runewords. Flat and binary properties are excluded from rune rolls; equipment/Imbuing affixes may later contain excluded properties. Gems retain all nine UO families with five qualities, and selected flat effects supply the two extra families. Bark and gems are gathering-only sources, unavailable from monsters or NPC stock, but NPCs buy them for a small amount. The eight mould/bark tiers and five mining thresholds are recorded in [runes and gems](docs/runes-and-gems.md). Final property assignments and per-stat amounts remain open.

Treasure maps should retain deciphering, locating and digging, but uncover a small dungeon entrance rather than just a treasure chest. A chest at the end provides a potentially lucrative reward. Templates, rewards and repeat-access rules come later.

## 12. Loot handling, death and persistent possessions

Nearby AoE corpse looting uses a grid with quick transfers, tooltips and clear rarity presentation. Items remain associated with their underlying corpse. Empty intact corpses can turn grey while remaining usable for harvesting or necromancy.

Harvesting uses the bladed-tool/corpse interaction. Consumed bodies with remaining loot stay as lootable remains; after emptying, they vanish. Ordinary decay handles abandoned remains. Corpse Explosion and summons must not silently delete unclaimed equipment.

Automatic kill-gold collection was part of the intended initial loop. The earlier implementation proposal put gold in the credited killer's backpack, leaving it in the corpse if capacity prevented transfer. Party distribution and final capacity rules need confirmation.

Death uses UO-style ghost/resurrection gameplay, with shrines, healers and camps planned. There is no XP penalty. Some gold is destroyed; the amount and whether it comes from carried gold, banked gold or both are open.

Clicking one's corpse should recover and re-equip its contents. Each corpse keeps what it held, including after subsequent deaths. Player corpses need long persistence compared with monster remains. Exact timers, logout handling and recovery conflicts remain open. Starter gear survives death unless deliberately destroyed.

Insurance was an earlier desired system: pay gold to retain insured gear, with uninsured gear waiting on the corpse. It has not been explicitly cancelled, but its integration with the newer death rules and any automatic reinsurance costs remains unresolved. Do not assume it is implemented.

Mercenary resurrection costs gold. Pets/companions should survive relog as persistent companions; relog is not intended as a new recruitment or consumable expense.

## 13. Housing and the cooperative economy

**Subsequent A03 direction:** housing permission is unlocked through town-hall recognition after reaching level 80 in one class or skill 100 in one **crafting job**, including Cartography; gathering does not qualify. Mining/Lumberjacking/Fishing remain always active alongside one selected class and crafting job. The award includes a named display certificate and a bookshelf-style wardrobe, with public town-hall wardrobe access available after unlock and before home ownership. Swaps are free/no-cooldown, require being alive/out of combat, and may leave a weaker class overweight under normal encumbrance. This grants housing acquisition rights, not a free house. [Wardrobe progression](docs/wardrobe-progression.md) records details still open. Town crafting starts with no usage fee; private home workshops remain undecided, so the older home-crafting possibilities below are not approval to implement them.

Housing supports storage, furniture, crafting and a sense of home. Players should be able to save towards a house through ordinary play. Player-placed houses and purchasable vacant town houses were both discussed; maps, availability and final acquisition rules are undecided. NPC homes would remain reserved if the town-house idea is used.

No rent, property tax, upkeep or inactivity repossession policy has been adopted. Do not introduce recurring housing costs just to create another sink without revisiting the relaxed-play goal.

The economy serves a small cooperative community, not competitive PvP. Player trade and useful services are welcome. It need not force dependence on other players being online for a viable solo session.

## 14. Implementation order and open balancing work

Earlier broad development roadmap (historical context, not a new implementation instruction; the later crafting handoff supplies the current profession decisions):

1. Finish shared foundations: combat, movement, stats, progression, equipment representation and core interactions.
2. Implement classes individually and establish their equipment needs.
3. Decide vendor locations, stock and progression limits.
4. Adapt skill visuals using available assets.
5. Build gathering/crafting, recipes, gains and salvage.
6. Design final maps, regions, dungeons, monster affixes, bosses and their reward tables.

This document does not authorise immediate implementation of every future feature. Keep the foundations extensible without building speculative content early.

Before economy balancing, settle or measure:

- Gold per outing, including NPC resale, by representative level and build.
- Routine NPC expenses versus player-service expenses and remaining savings.
- Gold Find's actual income advantage relative to kill speed.
- Repair material forms/subtypes and quantities, rarity-based essence costs, separate tool requirements, NPC prices, maximum-durability loss rate and special restoration resource. The material-plus-essence/charge-only-essence split is decided.
- Socket caps, success progression, fee and materials.
- Imbuing costs and blue/rare probabilities.
- Gambling prices and reward generation rules.
- Ordinary vendor level bands and tier availability.
- Potion costs, recipes, duration/stacking, belt capacities and bottle recovery logistics.
- Salvage yields, resource acquisition and skill-gain rates.
- Death gold penalty, insurance decision and mercenary resurrection prices.
- Housing prices and desired savings timescale.

Do not balance only against a rich veteran. A new character using NPC services should progress; a crafter should benefit from their skill; a wealthy veteran should have attractive optional spending opportunities.

## 15. Decisions not to accidentally revert

- Gambling rewards use dropped-loot improvement rules, not ordinary vendor-stock restrictions.
- Vendor stock is a temporary reliable upgrade, not an infinite supply of imbue/socket bases.
- Socket failure destroys equipment; repair does not gamble away equipment.
- Imbuing now rolls magic or rare, not guaranteed rare.
- Potion buffs are brief combat tools; food buffs are longer convenience effects.
- Rarity, tier, item level, material and origin are separate concepts.
- Reanimated enemies do not create fresh XP or loot.
- No XP loss on death; corpse recovery is streamlined and starter gear is retained.
- Ordinary durability repair may reduce maximum durability; restoring it requires a special resource. Charge-only restoration uses essence only, with tools settled separately.
- Combat-buff food is superseded by convenience food; this does not supersede the later maximum-durability-loss decision.
- The proposed socket base success schedule is 100% / 50% / 25% / 5% for sockets one / two / three / four onward, replacing 10% per socket.
- Rune moulds are single-use; all nine UO gem families are retained. Rune rolls exclude flat and binary effects.
- No universal D2 tier multiplier, final gold curve or final vendor level bands have been chosen.

