# Wardrobe progression: A03 design direction

> Reconciliation note, 19 October 2026: [the completed replies](economy-open-questions-replies.md) answer most economy/crafting questions but leave the Q state/title fields unfilled. They do not revoke the later explicit gathering/Cartography/public-wardrobe/encumbrance or sex-specific class-name decisions below. The overall current economy/crafting specification is [crafting.md](crafting.md).

Recorded from the user's A03 answer, 18 October 2026. This is a proposed progression/housing design, not an implemented feature. User-specified rules below are distinct from recommendations and unresolved details. The feature is broader than a profession-cap setting.

## User-specified direction

- One persistent character can develop all combat classes independently: for example, Amazon 99 and Necromancer 1. Changing class does not erase the other class's progress.
- All classes support either creation sex. Class labels follow the [class display-name mapping](class-talents-and-trades.md): Druid/Druidess, Sorcerer/Sorceress and Gargarean/Amazon; Paladin, Barbarian, Assassin and Necromancer use the same names for either sex. These aliases share each class's existing identity and progression, rather than creating extra wardrobe profiles.
- Exactly one combat class and one **crafting profession** are selected at any time. Crafting professions retain independent progression; one character can eventually develop them all without losing training when switching.
- **Mining, Lumberjacking and Fishing are always active**, with independent progression; they do not occupy the selected crafting slot. This supersedes the earlier one-active-profession rule that also restricted gathering. A Blacksmith can mine its own ore before unlocking wardrobe swaps.
- **Cartography is a crafting profession:** it produces maps and supplies the prerequisite skill for treasure hunting. It occupies the selected crafting slot and can qualify for Pillar of Society. Treasure hunting remains an activity, not an additional skill.
- A wardrobe provides two lists: classes with their achieved levels, and crafting professions with their achieved skill. Opening it preselects the currently active class and craft. Tick exactly one class and one craft before **Swap** becomes available; each list permits only one selection. Either selection can remain unchanged, allowing a craft-only or class-only swap.
- New characters initially display **Alchemy active**, because it is first alphabetically. This default is not a confirmed first-profession lock. Before wardrobe eligibility, the first use of a crafting tool asks the player to confirm that tool's profession as their first profession. Confirming saves the choice; cancelling leaves it unchosen and performs no craft, gain or resource/tool-charge consumption. Gathering tools do not trigger this choice.
- The confirmation explains that only one craft can be chosen until **class level 80 OR 100.0 in a crafting skill** unlocks wardrobe access. Suggested wording: “Before you reach class level 80 or 100.0 in a crafting skill, you can choose only one crafting profession. Choose {profession} as your first profession?” Once confirmed, another craft's tool explains the restriction instead of offering a new choice. The unlock still requires the town-hall recognition/access flow below; it is not a free swap at any tool.
- Switching is free and has no cooldown once unlocked. Class profiles retain equipped gear, stat allocations, talents, hotkeys/bindings and active summons. The intended experience resembles logging out one class and logging in another on the same character.
- At combat level **80 in any one class** or **100 in any one crafting profession**, the character becomes eligible for the unlock. Gathering skills do not qualify. A server message directs the character to a town hall; choosing a random town is a possibility, not a settled selection algorithm.
- Recognition is **Hero of the People** for the combat route or **Pillar of Society** for the crafting route, including Cartography. A certificate uses a UO painting asset, includes the title and character name, and can be displayed in a home.
- The ceremony awards a placeable wardrobe with bookshelf appearance and unlocks the right to place a house or buy one in a town/city. This is permission to acquire a home, not a grant of a free house.
- **A public wardrobe at the town hall is available after unlocking**, so a character can switch before owning a house.
- **Swapping requires being alive and out of combat.** There is no switching cooldown or cost after unlocking. The precise combat-state detection and handling of in-progress actions remain implementation details to define.
- **Swapping to a weaker class may leave the character overweight.** Allow the swap and apply normal encumbrance rules under the new class's carrying capacity; do not reject it solely for excess weight or destroy possessions.
- Profession caps begin at **100** and can be raised to **105 / 110 / 115 / 120** using Power Scrolls, intended as rare dungeon-boss PvE drops. Exact scroll eligibility, consumption/order and drop tables remain open.
- Town crafting facilities are the proposed default: for example, a shared forge for blacksmiths. Start with **no facility usage fee**; a configurable future fee is a tuning possibility, not an adopted charge.
- Private home workshops are not yet decided. Portable activities have explicit exceptions, such as preparing a meal at a campfire; the complete exception list remains open.
- Butchering/skinning requires **no skill**. It is a corpse interaction, not a second active profession.

## Confirmed repository baseline

`src/Sanctuary.Content/CharacterProfiles.cs` currently keeps one `CharacterProfile(Class, Level, Experience)` per mobile serial, with stat allocations and talent dictionaries also keyed by that serial. There is no independent class-history collection or wardrobe switch transaction. Adding this design requires a save migration that preserves the current character's progression as its initial class profile.

`src/Sanctuary.Content/PersistentMinions.cs` already suspends opted-in class minions by internalizing the actual creatures on disconnect and resumes them on reconnect. That is useful groundwork, but it is keyed to the character's followers rather than an active class profile. It does not yet isolate separate class summon sets or provide a wardrobe snapshot.

The retained trade-skill list exposes all professions. `shared/TradeSkills.cs` currently groups nine crafting skills, three gathering skills, and Cartography separately under TreasureHunting. The target design instead has ten selectable crafting professions including Cartography and three always-active gathering skills. Independent stored skills are not the same as enforcing the selected craft's actions, gains and skill-derived effects. No activation gate or UI regrouping is implemented by this documentation change.

## Recommendations, not adopted rules

- **Treat town stations as shared facilities with concurrent users.** Their presence can bring players together without requiring a queue for one person's forge interaction. Keep the initial usage fee at the specified zero.
- **Treat switching as one validated server operation.** Store/move the real equipped items and real summons; do not copy them. A failed equipment/storage validation should leave the previous class and inventory intact. The home furniture should be an access point to character-owned profiles, rather than the sole owner of progression that could disappear with the house.
- **Prevent resets through the remaining state rules.** Alive/out-of-combat access is now decided. Merely switching should not refill pools, cleanse harmful effects, reset timers, resurrect dead summons or retain an inactive class's summon army in the world. Exact pool/timer rules remain open.

## Proposed optional achievement titles

The user's next proposal adds an optional **class prefix** earned at **level 99 in each class** and an optional **skill suffix** earned at **100 in each retained skill**. Either title slot may be hidden independently. The example is **Archmage Joe, Legendary Blacksmith**; final title names are not selected.

Skill-title eligibility includes gathering as well as crafting. This is separate from **Pillar of Society**, whose skill-100 unlock remains crafting-only, and from **Hero of the People** at class level 80. A gathering title must not inadvertently grant wardrobe/housing rights. The new title proposal does not add stat bonuses or change progression requirements.

**Recommendations, not adopted rules:** let the player select from earned prefixes/suffixes rather than forcibly displaying the active class/job's title. Consider Grandmaster at skill 100 and Legendary at 120 to recognize Power Scroll progression. The existing native `Misc/Titles.cs` uses those two ranks at 100/120, but that is implementation evidence, not a decision to replace the user's example.

Final class/skill title names, whether selection is independent of the active class/job, any additional 105–120 title ranks, how town-hall honors fit the selectable catalogue, and interaction with legacy fame/skill title presentation remain open. See Q11–Q13 in the worksheet.

## Details still requiring decisions

The [decision worksheet](economy-open-questions.md), Q01–Q13, records the follow-ups. Q01's gathering-access issue is resolved; public access, alive/out-of-combat use and overweight behavior are decided in Q02/Q03/Q07. Q08 now records the new-character Alchemy default and first-tool confirmation. Existing-character craft migration and first activation of another class remain open, alongside shared versus class-specific state, health/status/summon handling, storage identity/capacity, achievement ownership, Power Scroll rules, town/home craft restrictions and the optional title catalogue. None of the remaining recommendations silently fills those answers.

The [implementation roadmap](implementation-roadmap.md) separates the first-profession/public craft-switch loop from the later full class/gear/summon transaction. Both use this same selection design; the first slice keeps the current class selected without pretending other class profiles already work.

No code, saved progression, native skill caps, item inventory, live state or client preferences changed in this design pass.
