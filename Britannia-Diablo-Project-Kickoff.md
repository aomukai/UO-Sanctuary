# Britannia Diablo crossover project kickoff

Design handoff for Andy and Codex • 17 October 2026 • Working design, not an implementation claim

## 1. The game we are building

Bring Diablo II combat and itemisation to Britannia for players who love both Diablo and Ultima Online. Preserve UO's world, towns, homes, gathering, crafting, exploration, and social life. Introduce Diablo-style classes, experience, skill trees, fast combat, distinctive builds, resistances, mercenaries, and an exciting equipment hunt.

This is a crossover, not a one-to-one recreation of either game. Keep UO's visual identity. Adapt Diablo abilities to fit the engine and available art while preserving each class's playstyle. Reuse existing UO systems wherever practical; do not assume that modern UO lacks tooltips, item properties, resistance systems, imbuing, or other later-expansion features.

The intended experience: leave your town house, prepare equipment and supplies, fight through a dungeon or follow a treasure map, bring materials and loot home, work at your craft, and get to know your neighbours.

## 2. Decision status and priorities

Treat **agreed direction** as the project's design intent. Treat **candidate**, **example**, and **open** entries as provisional. Do not silently convert suggested numbers or mechanics into permanent requirements.

| Area | Agreed direction | Still open |
| --- | --- | --- |
| Server and client | ModernUO plus desktop ClassicUO | Pin versions and verify compatibility in a running prototype |
| Content baseline | Later UO systems are useful; Age of Shadows or Stygian Abyss era | Exact expansion, races, regions, and enabled systems; Stygian Abyss is a candidate, not a locked decision |
| Combat | Diablo II feel and class identity | Exact timings, formulas, adaptations, class roster |
| Professions | Learn through use, separate from combat XP | Gain rates, total profession cap, exact recipes |
| Community | Small persistent shared shard, roughly a 20–50-person community | Concurrent cap; around 40 is a design target, not a benchmark |
| Dungeons | Shared, automatically party entrants; cap occupancy if needed | Party membership rules, local rewards, caps, town-return reservations |
| NPCs | Code controls routines and actions; optional LLM dialogue and memories | Model, memory implementation, inference budget, rollout scope |

First priority is proving that movement, controls, attacks, and recovery feel good. Build the crafting and world systems around a successful combat foundation. NPC language features come later.

## 3. Technical starting point and reuse audit

Start with the selected server and client, document exact commit IDs, and establish a reproducible Linux development setup. Do not begin with a full rewrite. Keep project-specific changes clearly organised and upstream changes traceable.

Before replacing a subsystem, inspect its current implementation and record whether it can be kept, configured, extended, or replaced. Audit:

- Login, character creation, saves, banking, inventories, equipment, and housing.
- Item property lists, hover tooltips, resistances, damage channels, and material bonuses.
- Gathering, crafting recipes, imbuing, tool charges, repairs, and treasure maps.
- Movement prediction, speed validation, attack scheduling, spell timing, damage events, and interruption.
- Projectile effects, coloured effects, animation support, input bindings, and targeting.
- Party support, reward attribution, NPC AI, pets, mercenary-like followers, and resurrection.
- Expansion content coverage in the actual chosen ModernUO revision.

ModernUO and ClassicUO being editable is an opportunity, not evidence that every proposed change is already supported. The actual prototype must establish what works. Existing ServUO packages, including instancing systems, are not assumed to be drop-in ModernUO components.

Use existing client art first. Keep the game rules authoritative on the server. Client changes handle presentation, targeting, input, and prediction. External LLM work must not block the game loop.

## 4. Characters and progression

Each character selects one combat class and earns combat XP and levels. Use Diablo II's class identities and skill trees as the starting design reference. Specify the initial class roster before implementation; do not assume every expansion class is included.

The initial character-creation roster is Amazon, Sorceress, Necromancer, Paladin, Barbarian, Druid, and Assassin. Select a class first, then use UO's appearance editor and enter the world at level 1 with zero combat XP. Remove the original profession templates, custom option, and starting skill allocation screen. Class selection is saved per character; it does not depend on the chosen appearance. Class abilities and XP advancement remain subsequent work.

Try to retain the trees before removing skills. Audit every ability for gameplay role and implementation feasibility. If a skill needs adaptation, preserve what makes its build enjoyable rather than its precise geometry or artwork.

Combat competence comes from class progression, not parallel UO weapon-skill grinding. Crafting daggers should improve a profession, not award combat levels. Original UO combat and magical combat skills are generally replaced by the class system. Do not keep old skills merely to fill a list.

If skills are removed or merged, recalculate the point budget. The relevant measure is the cost of a complete specialisation: removing a one-point prerequisite differs from removing a twenty-point synergy. A cap of 25 rather than 20 was discussed as one possible adjustment, not a decision. Rescale power curves as well as costs.

A fully developed specialist should require meaningful commitment. A secondary element may provide coverage, but fully maximising two specialisations must not become free because the tree was shortened. Hybrids should trade some depth for breadth.

## 5. Combat feel and skill adaptation

Fast, readable combat and item-driven builds are central. Running, attacking, casting, hit recovery, blocking, knockback, chilling/freezing, and mercenary reactions need coherent timings.

Keep independent base rates for player movement, monster/pet movement, weapon attacks, casting, and projectile travel. Future Increased Attack Speed (IAS), Faster Cast Rate (FCR), and run-speed modifiers apply to their respective rates. Faster Hit Recovery (FHR) shortens a distinct recovery state entered after sufficiently strong hits. Specify the hit threshold, blocked actions, cancellation/queue rules, recovery duration, and modifier/breakpoint model through play tests; do not silently inherit UO Dexterity or skill formulas as Diablo balance.

Investigate Diablo mechanics using documentation, reconstructed code such as D2MOO, and controlled observation. Use REA-assisted binary investigation only for concrete gaps. Distinguish legacy Diablo II behaviour from later D2R changes. Reimplement selected behaviour for this project rather than assuming old engine code transfers directly.

### Recovery and action states

Implement server-owned action states with consistent client feedback. Research and choose:

- Which hits trigger recovery and whether damage relative to life matters.
- Recovery duration and how Faster Hit Recovery modifies it.
- Which attacks and casts can be interrupted, and when their effects become committed.
- How additional hits interact with ongoing recovery.
- The distinction between explicit stun, ordinary hit recovery, block recovery, and knockback.
- Monster and boss exceptions, uninterruptible skills, and damage-over-time behaviour.

Movement prediction and animations must agree with server timing. Do not create faster combat merely by changing server delays while leaving the client visibly out of sync. Decide explicitly whether to retain Diablo-style breakpoints or use smoother scaling.

### Representative adaptations

| Ability family | Candidate implementation | Identity to preserve |
| --- | --- | --- |
| Firebolt, Ice Blast, Holy Bolt | Existing projectile visuals, recoloured where suitable | Readable ranged attack and damage type |
| Assassin traps | Placed objects with scripted targeting and existing firing effects | Preparation and control of a killing area |
| Auras | Server-applied proximity buffs with restrained visual feedback | Persistent support and aura choice |
| Meteor | Ground-targeted delayed area effect | Anticipation, placement, area damage |
| Frozen Orb | Slowly travelling cold effect with surrounding pulses or emitted shards | Moving cold coverage and chill/freeze control, subject to target rules |
| Blessed Hammer | Prototype a travelling spiral; fallback to expanding damage rings anchored at cast position | Positional aiming, gaps, and overlapping casts |
| Multishot | Fire additional arrows at enemies around the selected target if directional spread is awkward | Bow-based group clearing |
| Druid spirits | Differently coloured wisps following loosely and providing buffs | Recognisable support companions |

The proposed hammer fallback uses expanding bands, for example adjacent tiles, then distance three, then distance five, possibly repeating the outer band. These are illustrative values. Movement and recasting shift the origins and produce an aiming game. Check stacking, overlapping casts, and repeated-hit limits.

Existing moving-effect APIs support investigating custom paths; a convincing spiral or synchronised orb burst has not yet been demonstrated. Neither eight character facings nor a successful straight projectile test alone settles that question.

For each audited skill record its role, targeting, available assets, server behaviour, client work, adaptation, dependencies, and a playable acceptance check. Review whole builds so replacements do not make every class feel alike.

## 6. Controls and interface

Support assignable left/right mouse skills and configurable hotkeys. Investigate existing ClassicUO bindings before extending them. Selecting a spell and separately handling a target cursor for every cast is not the desired combat loop. Diablo class abilities use mana without UO reagents or UO magic-skill requirements.

The initial controls are left-click ground to cancel attacking and move, left-click an enemy to approach and repeatedly use the bound attack, and right-click to use the bound skill with appropriate targeting. Target death stops attacking without choosing another enemy. Shift holds position and blocks movement; its future bound-attack action remains to be wired. Remove war mode from the player experience while adapting internal dependencies as needed. Friendly NPCs remain interaction targets. Preserve reliable interactions with doors, NPCs, containers, and draggable items. Chat input must not accidentally trigger combat hotkeys.

Prefer a UI integrated into the client over a separate operating-system overlay. Reuse existing paperdolls, tooltips, containers, and crafting interfaces where useful. Add visible assigned skills, class progression, XP, and a separate professions view. No pixel-perfect Diablo HUD is required.

Use weight and UO-style carrying capacity instead of Diablo inventory Tetris. Exact capacity scaling is open. Charms have not been approved; if included, their power budget needs an explicit limit rather than relying accidentally on weight.

## 7. Itemisation and equipment economy

Use white, blue, yellow, and gold rarity categories with Diablo-like affixes such as IAS, FCR, magic find, FHR, life leech, and mana after kill. Base the magic/rare eligibility pools, roll ranges, level gates, groups, and frequencies on the D2 1.13 data. Magic items roll at most one prefix and one suffix; rares target three to six affixes, at most three of each kind. Some high-end affixes are magic-only; rare does not mean a higher ceiling on every individual stat. Unique items and loot tables require deliberate design.

Keep base type, base tier/quality, material, rarity, affixes, sockets, and durability as separate concepts internally. Their exact relationship is still open, especially whether rarity or base tier controls socket ceilings. Preserve the ability to make high-quality white bases valuable.

Convert all supported equipment to explicit normal D2 base analogues and enforce their damage, defence, speed, level and attribute requirements. Keep one combat contribution per D2-equivalent slot. UO-only layers—gorgets, arms, leg armour, bracelets, earrings, cloaks and extra clothing—exist only as white cosmetic items, with no combat stats or affixes. They must not enlarge the D2 player power budget. Native UO magical/material/quality bonuses do not bypass the converted rules. Only the actual Necromancer starter dagger has its starter +1 Teeth grant; ordinary daggers do not.

Disable all monster death loot for now, including gold and equipment; design drop tables later. Preserve player and pet belongings and continue XP rewards. Future monster-created magic/rare gear uses monster level as item level, source base quality/magic level to derive affix level, slot/type eligibility, rarity caps, source roll ranges and character requirements. These gates are independent of sockets. Persist selected affix IDs and values, and enable only effects that are fully implemented. Example: gloves can roll IAS, boots cannot; magic-only high tiers can exceed rare ceilings.

The foundation pass covers normal equipment bases only. Exceptional and elite base tiers, uniques, and actual socket ceilings come later. Every supported UO equipment type must have a catalog entry and category; named artifacts and rewards must not accidentally enter the normal base drop pool. Keep a nullable `maxSockets` field on every entry until our own limits are decided. UO crafted Exceptional quality is a separate concept from a Diablo exceptional base tier.

Classify weapons into swords, axes, maces, polearms, spears, daggers, bows, crossbows, throwing weapons, staves, wands, scepters, and claws where appropriate. Armour and shields have light, medium, or heavy weight classes. Light has no running penalty, medium a small penalty, heavy a larger one. The initial implementation uses 0%, 5%, and 10%; only the canonical body armour contributes its weight penalty, then add the shield's penalty. These are editable project rules. Do not multiply an entire Diablo body-armour penalty by the number of UO armour pieces.

### Socketing

- Looted gear starts without sockets in the proposed system.
- Tinkering adds one socket per attempt, up to the item's allowed maximum.
- The intended success ceiling is about 10% at 100 skill; failure destroys the item.
- Successive sockets may become harder. The relationship to skill 120 is not settled; do not silently raise the intended low success ceiling.
- This is deliberately a high-risk improvement minigame and item sink. Do not replace it with a single roll that grants all sockets.
- For scale: three consecutive independent successes at 10% each have probability 0.1%. That harshness was discussed and intentional, though the live economy needs tuning.

Crafted whites may have a chance to start with sockets at crafting skill 100, with additional durability at 120. These are promising candidate milestones, not final probabilities. Starting sockets can make crafted bases desirable without overwhelming loot. Preserve material bonuses and existing sockets when imbuing a white base into a rare.

### Gems and runes

Use individually useful socketables rather than runewords as the working direction. Gems and runes should offer distinct pools. Runes can supply IAS, FCR, mana after kill, leech, FHR, or ignore-target-defence effects that gems do not offer. Exact magnitudes, allowed equipment slots, stacking, removal, and replacement are open.

### Materials and resistances

Reuse UO's ore, wood, leather, and material infrastructure where suitable. Materials give predictable base benefits; imbuing adds randomness. Existing UO material mappings are the first reference, but remapping is allowed. Verite for physical resistance, shadow for poison, and gold for magic were illustrative custom assignments, not a statement of UO's stock bonuses or a final table.

Use physical, fire, cold, lightning, poison, and magic as the intended damage/resistance vocabulary. Investigate mapping UO energy to lightning and adding magic separately. Player resistance caps, monster immunity rules, penetration, and physical mitigation need independent balancing.

Materials should remain useful for different purposes instead of becoming an obsolete ladder where only the rarest matters.

## 8. Professions

Professions improve through use. Total skill caps, mastery rates, and whether one character can eventually master everything remain open.

The paperdoll’s Skills button becomes **Class**, opening the selected class’s three Diablo II talent trees with ten skills each. Quests becomes **Skills**, opening the retained UO skills window with only the 13 professions below, grouped as gathering, crafting (including Imbuing), and treasure hunting (Cartography). The sections are fixed, without group-editing controls or skill drag-and-drop. Both Class and Skills use the blue paperdoll button style. Original combat skills are hidden from this view; recipe and combat-system changes are separate work.

| Profession | Intended role |
| --- | --- |
| Blacksmithing | Metal armour and melee weapons; appropriate repairs |
| Bowcraft and Fletching | Bows and shields; appropriate repairs |
| Tailoring | Light armour; appropriate repairs |
| Imbuing | Convert white bases into rares |
| Tinkering | Destructive socket attempts and charged chest keys |
| Inscription | Create runes from charged moulds dropped in PvE |
| Carpentry | Housing furniture and decoration |
| Cooking | Small buffs such as HP/MP/stamina regeneration, possibly FCR |
| Cartography | Decipher treasure maps leading to catacombs |
| Mining | Ore and related materials |
| Lumberjacking | Timber and related materials |
| Fishing | Fish, materials, and possible special encounters |
| Alchemy | Candidate role: potions and reagent processing |

Resolve uncovered base types such as staves and crossbows in the recipe audit. Do not silently assign every item to a profession without documenting the choice.

Retain bladed-tool skinning to gather hides from skinnable corpses. Hides feed Tailoring to craft light armour bases. Preserve this gathering interaction when adapting weapon and combat controls: double-clicking a bladed tool can invoke its targeting action, while dragging a weapon onto the paperdoll equips it.

Keys and rune moulds are consumables with charges. Better keys open higher-tier chests. A proposed simple rule is that an adequate key guarantees opening and spends one charge; inadequate keys do not spend a charge. Exact tier compatibility, crafting costs, mould uses, and rune yields remain open. Lockpicking is unnecessary under this design.

Food should offer small optional bonuses, not become required for ordinary play. If speed breakpoints are retained, evaluate the actual benefit of a small FCR bonus. Stacking, duration, and behaviour on death remain open.

Most original combat, healing, magic, bard, rogue, and sensing skills need not survive. Review each for a useful non-class role; avoid duplicate combat progression and compulsory identification chores. Taming or bard play would require an explicit future decision.

## 9. Repairs, durability, death, and insurance

**Gamble to improve equipment; do not gamble to maintain it.**

Repair requires the same profession skill needed to craft the base item. A broadsword requiring 80 Blacksmithing to craft also requires 80 to repair, regardless of rarity or affixes. Below the threshold, repair cannot be attempted. At or above it, with required tools and supplies, repair always succeeds.

Both NPC and player repairs require a repair resource. NPCs always consume it and charge gold. Skilled players can preserve the resource, with a proposed maximum 30% saving chance at skill 120. Resource-saving randomness is a bonus, not repair failure. Exact repair resource sources and quantities are open.

Higher durability reduces maintenance frequency and supports longer expeditions. The earlier idea of losing maximum durability on each repair was superseded by the repair-resource approach; permanent degradation is not a settled requirement. Decide separately what reaching zero durability does and whether any rare restoration reagent is needed.

Offer per-item insurance as a recurring gold sink. Insured equipment is retained on death with a gold charge; uninsured equipment is at risk on the corpse. Relogging must not teleport an uninsured corpse or its equipment to the player. Insured players should not have to make naked corpse runs to recover insured gear.

Mercenaries can die and require resurrection for gold. Premium formulas, insufficient funds, corpse expiry, resurrection locations, and protection of items in mixed insured/uninsured inventories need specification.

## 10. Dungeon ecology and world structure

### Foundation baseline — players 1

Finish the combat, itemisation, class growth, and XP foundations before adding more content. Start with a single players-1 monster baseline for levels 1–99: tank, melee damage dealer, ranged physical damage dealer, and caster. Use actual D2 data where a matching species exists; use explicit archetype profiles for UO species without a D2 counterpart. Archetype multipliers and UO movement/animation mappings are project adaptations, not original D2 monster records.

Keep bounded spawn variance within an archetype's level band. A rolled level 39 must remain easier than a rolled level 40 of the same archetype; cross-archetype life/damage trade-offs remain intentional. Integer per-hit damage may plateau, but higher levels must have strictly greater life and no lower attack rating, defence, or damage. Roll once per spawn and preserve the roll through saves; do not reroll whenever a creature is examined or loaded. Bosses, super uniques, uniques, champions, and their special minions are outside this pass.

D2's Normal, Nightmare, and Hell data inform the level progression: roughly the first thirty levels, the next thirty, and the remainder to 99. For the initial editable curve, use Normal source columns at levels 1–35, Nightmare at 36–65, and Hell at 66–99. These are source bands for one curve, not three selectable difficulty profiles or multiplayer scaling. Preserve players-1 HP and XP; do not apply player-count multipliers. Record the band boundaries explicitly so they can be tuned later.

Leave additional resistance and immunity profiles out of this foundation. Later derive dungeon Normal, Nightmare, and Hell variants from the players-1 baseline: for example, floor 1 uses Normal, floor 2 uses Nightmare with stronger resistances, and floor 3 uses Hell with immunities. A floor's difficulty is distinct from a monster's level and from the source band used to build that level's base stats. Do not create those difficulty variants yet. Existing explicitly researched species properties are retained until that later resistance pass.

These are two independent axes of difficulty. Level and archetype supply numeric strength; the later regional resistance profiles supply mechanical matchups between builds and places. This is one persistent world, not three copies of the same world or a linear campaign repeated three times. A level-40 ghost's baseline is still the level-40 baseline when its eventual area profile grants physical immunity. The baseline must work before introducing that immunity.

Examples for the later geography pass: Hythloth's third depth can have physically immune ghosts and poison-immune skeletons/zombies; Covetous's third depth can include lightning-immune populations that remain vulnerable to fire; Destard's third depth can contain fire-immune dragons vulnerable to cold. These are intended regional matchups, not implemented populations or resistance values. As in D2's contrasting skeleton populations, modest Normal resistance differences can grow into decisive Nightmare matchups and Hell immunities. A mercenary, allies, or skipping particular packs can make a hostile area navigable while a favourable area remains comfortable to farm. None of those resistance/immunity additions belong to the current numeric-stat pass.

All seven player classes use D2 starting attributes, HP/mana/stamina, per-level growth, per-Vitality/Energy growth, five attribute points per level, and the original level 1–99 cumulative XP thresholds. Retain quarter-point precision until the final resource total is rounded. Monster rewards use the players-1 base reward, solo level-difference adjustment, and D2's high-level XP reductions. Pet kills credit their owner. Party distribution and multiplayer scaling come later.

Implementation details, source provenance, coverage, and verification live in [the balance foundations reference](docs/balance-foundations.md).

Use fixed layouts with respawning inhabitants. Major dungeons have progressively harder depths: Normal at the top, Nightmare below, Hell deepest. Adapt actual maps as needed rather than assuming every existing dungeon already has exactly three appropriate floors.

Normal introduces resistance themes without ordinary themed immunities. Nightmare strengthens resistance and adds more elite modifiers. Hell introduces established immunities and double-immunity populations. Specialist builds are not expected to farm every location comfortably.

Examples discussed: Hythloth emphasises physical immunity with some poison immunity; Covetous can be hostile to lightning builds; Shame to poison builds. These are provisional themes. Establish the complete eight-dungeon resistance matrix before world balancing. Five main types yield 20 ordered primary/secondary combinations or 10 unordered pairs; magic is a separate wildcard.

Keep population themes predictable. Vary elite modifiers and encounters within them. Rare magic-immune individuals interrupt a hammer paladin's otherwise broad access, requiring a mercenary, ally, or skipped reward. Double immunities are intentional, not a balance failure. Whether magic can create triple immunities remains open.

Persistent spawns mean skipped immune monsters can accumulate while killed monsters reroll on respawn. Preserve this consequence unless testing shows a problem. In shared dungeons, other builds can clear them; that is part of cooperation, not something to artificially prevent.

Outdoor camps can use remaining themes with fewer immune inhabitants. Examples include poison/fire swamp creatures near Britain and fire/lightning desert creatures. Monster immunity does not itself imply that the monster deals that damage type; define outgoing damage separately.

## 11. Multiplayer, travel, and persistence

The latest direction is a small shared community world, not mandatory private instances. Characters, banks, homes, and progression belong to their server. Players do not import a character and possessions from a local world.

Automatically group adventurers entering the same dungeon. Support dungeon population limits if needed. Decide XP proximity, loot allocation, party size constraints, voluntary exits, and grouping across difficulty floors. A cap alone does not settle reward fairness.

Travel to a new dungeon is on foot or horseback. Town portals permit errands. Town moongates can offer return to the player's active dungeon; dungeon floors can contain a gate in a quiet room where nothing spawns. Exact activation and destination rules are open. A spawn-free room is not automatically safe from pursuing monsters, so define its boundary behaviour.

Preserve farming continuity through banking, repair, restocking, mercenary resurrection, and disconnects. A temporary slot reservation during errands is a candidate rule. Do not let reconnecting become an immunity reset or loot duplication mechanism.

Earlier private-instance proposals—one active instance, reset upon entering another dungeon, optional cooldowns—are fallback ideas, not current shared-world rules. Do not reset a shared dungeon because one player enters another. Separate per-dungeon servers and character handoffs are not required for the initial project.

## 12. Treasure maps and catacombs

Reuse the treasure-hunting loop and existing map tiers: maps drop, the player deciphers one, locates the site, and digs. Replace the ordinary chest discovery with access to a small custom dungeon ending in a reward chest.

Use map tier to drive difficulty and rewards. These expeditions should be lucrative and give Cartography a strong role. A clickable revealed entrance is a candidate refinement; immediate transport was also discussed. Decide explicitly.

Handcrafted mini-dungeon templates are sufficient initially. Allocating temporary map areas is still a technical task even though major dungeons are shared. Prototype a small pool of isolated prepared areas before choosing a more elaborate system.

Persist expedition identity, entrance, owner/party eligibility, contents, and chest state. Leaving and returning must not generate another reward. Define map consumption, expiry, simultaneous expeditions, access during town trips, and return location. Showing the chest's key tier on the deciphered map is a useful candidate.

Reward mixes may include charged rune moulds, materials, good bases, and ordinary equipment rolls. Exact exclusivity and drop rates are open.

## 13. Living NPCs and housing

Longer-term goal: selected NPCs feel like residents rather than vending machines. Code manages routines such as work, meals, and returning home. Reserve homes for NPC households and make suitable remaining town houses purchasable. This requires a town housing audit, not simply enabling player placement everywhere.

LLMs provide conversations informed by personality, local knowledge, and player-specific memory notes. One service can serve many NPCs; there is no requirement for a separate continuously running model per character.

World events are real game events first: ore deposits, strange fishing catches, a kraken, or other encounters. NPCs receive limited reports based on occupation, location, witnesses, and social connections. Track source and freshness. They are not omniscient, and old rumours can remain old rather than silently updating everywhere.

Relationships can unlock opportunities through multiple routes. A smith may teach a recipe because of a boss achievement, a relationship with the smith's household, or a referral from another craftsman. These routes should emerge in conversation without mandatory quest checklists. Record meaningful facts and introductions rather than just rewarding message spam.

The world defines teachable recipes and eligibility. Dialogue may propose a teaching action, but validated game code grants the recipe. An NPC must not invent usable items, alter prices, claim unrecorded achievements, or promise a referral that has no corresponding world state.

Memories distinguish witnessed events, reported claims, and verified achievements. Store stable NPC/player IDs and event provenance. A player's conversational claim is not proof that a boss was defeated. Dialogue failure or model unavailability must leave ordinary shop and repair functions usable.

A conversational mercenary is optional later work: ordinary AI handles immediate combat; the LLM interprets supported orders, remembers expeditions, and speaks at suitable moments. No model call per attack.

## 14. Recommended implementation sequence

These milestones are proposed execution guidance. Keep each playable and reviewable; do not implement every subsystem at once.

| Milestone | Deliverable | Exit evidence |
| --- | --- | --- |
| 0. Baseline and audit | Pinned server/client, Linux setup, existing-system map | Login, move, interact, equip, save, restart, and recover state; documented reuse findings |
| 1. Combat room | One provisional class, basic attack, projectile, AoE, recovery | Responsive controls; timing agrees between server and client; hits and recovery are measurable and visible |
| 2. Difficult effects | Frozen Orb and hammer prototypes, plus a trap | Choose adaptations through actual play; record failed approaches honestly |
| 3. Equipment loop | Small affix pool, material base, imbuing, sockets, repairs, charged key/mould | Rules survive restart; invalid actions spend nothing; successful actions consume exactly the intended resources |
| 4. Dungeon slice | One town and dungeon with three difficulty depths, mercenary, insurance | Two or more players cooperate; immunity theme works; death, resurrection, errands, and respawns preserve state |
| 5. Treasure expedition | One map tier and catacomb template | Complete decipher–dig–explore–unlock–return loop without duplicate rewards |
| 6. Broader content | Skill audit, additional builds, materials, regions | Distinct class playstyles and measured progression budgets |
| 7. Living town | One smith, household, schedule, event rumour, and recipe interaction | Memory and eligibility are grounded in actual state; ordinary gameplay works without the LLM |

Use tests for consequential rules: resource consumption, socket destruction, charge accounting, repairs, insurance, persistence, damage types, and reward duplication. Use manual gameplay and capture/logging for responsiveness and effects. Do not substitute a passing unit test for testing combat feel. Performance-test the intended monster density and concurrent population before claiming the 40-player target is supported.

## 15. Instructions for the initial Codex session

Begin with this brief as the design baseline. Inspect local repository instructions and the chosen upstream projects. Establish the working server/client pair and produce a compact gap audit with file references. Implement only the baseline and a minimal combat-room prototype initially.

Preserve unrelated upstream behaviour until there is a concrete reason to change it. Keep numerical tuning in explicit configuration or data definitions where practical. Record decisions and experiments, including approaches that failed. Clearly distinguish existing support, implemented changes, and untested possibilities.

Ask for decisions only when they materially block the next milestone. Use reversible placeholders for ordinary tuning and identify them. Do not silently change the intentional low-success socket gamble, make repair destructive, require all builds to clear all dungeons, or reintroduce combat skill grinding.

The first progress report should state: exact versions, how to run locally, systems reused, changes made, tests/play checks completed, unresolved limitations, and the next smallest experiment. Do not deploy a public shard as part of initial setup.

## 16. Reference starting points

These links are research inputs, not guarantees of compatibility or complete implementation. Inspect current source and pin the versions used.

- ModernUO: https://github.com/modernuo/ModernUO and https://modernuo.com/
- Desktop ClassicUO: https://github.com/ClassicUO/ClassicUO
- D2MOO mechanics reference: https://github.com/ThePhrozenKeep/D2MOO
- D2MOO damage handling: https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Game/src/UNIT/SUnitDmg.cpp
- REA investigation tools: https://github.com/morluto/rea
- UO resistance reference: https://www.uoguide.com/Resistances
- UO ingots: https://www.uoguide.com/Ingot
- Official material bonuses: https://uo.com/wiki/ultima-online-wiki/items/material-bonuses/
- ServUO alternative and custom-system reference: https://github.com/ServUO/ServUO
- Optional historical instancing reference: https://www.servuo.dev/archive/instanced-map-api-instanced-dungeons.988/

The foundation is a playable UO world. The project's success depends on making its combat enjoyable and connecting its professions to that combat without losing the reasons to inhabit Britannia.
