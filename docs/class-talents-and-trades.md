# Class talents and trades

> **Design update, 19 October 2026:** [Current first-implementation crafting/economy rules](crafting.md) reconcile the completed replies. The implementation/verification descriptions below refer to the existing comparison build, not completion of the new recipes, socket rules, potion/food catalogue or progression changes.

The paperdoll’s **Class** button replaces Skills and opens the selected character’s class gump. **Skills** replaces Quests and opens the existing UO skills window. `[Class` also opens the class gump. Characters without an assigned Diablo class are not assigned one automatically.

## Class names and character sex — design update, 19 October 2026

Every class is available to either sex chosen during character creation. The user specified these display names:

| Existing class identity | Male display name | Female display name |
| --- | --- | --- |
| Amazon (101) | Gargarean | Amazon |
| Sorceress (102) | Sorcerer | Sorceress |
| Necromancer (103) | Necromancer | Necromancer |
| Paladin (104) | Paladin | Paladin |
| Barbarian (105) | Barbarian | Barbarian |
| Druid (106) | Druid | Druidess |
| Assassin (107) | Assassin | Assassin |

These are display variants of the same seven classes, not additional classes or separate progression histories. They share the same stats, skills, talents, equipment rules and balance. Keep the existing wire/save class IDs and talent IDs stable. The character's creation sex supplies the display variant for every class, including when using the planned wardrobe; switching class does not change that sex.

This naming rule applies to character-creation class choices/summaries, class/talent headings, wardrobe class lists and player-facing class references. Earned title names remain a separate, unfinished catalogue; the display aliases do not implicitly choose sex-specific achievement titles.

**Implementation status:** [T03 implements and verifies the shared display mapping](t03-class-names.md) for creation choices/summaries, paperdoll titles/wrapping, talent headings and class-labelled speed tooltips. Both sexes retain the same existing wire/save identity and talent keys. The planned wardrobe must use this helper when implemented. The matching client/content builds are tested but not installed; the live comparison build remains unchanged.

## Current talents implementation

Each of the seven classes has three named trees with ten skills each. The gump shows ranks and level gates, lets players switch trees and inspect prerequisites, and offers Learn when allocation is allowed. Names and dependencies come from [Blizzard’s skill guide](https://classic.battle.net/diablo2exp/skills/); every tree’s source URL is retained in [the catalog](../data/Sanctuary/talents.json). No Blizzard art is bundled.

The provisional allocation rules follow [Diablo II skill basics](https://classic.battle.net/diablo2exp/skills/basics.shtml): level 1 starts with zero points, each subsequent level earns one point, and each learned rank costs one. Skills have a maximum of 20 allocated ranks, prerequisite skills need at least one rank, and learning rank N requires character level at least the skill’s unlock level plus N minus one. Quest bonus points and respec are not implemented. The discussed alternative cap of 25 remains undecided.

The server validates every allocation against current state, including class ownership and available points. Button IDs cannot grant levels, bypass requirements, or spend points twice. Ranks are stored by stable skill ID alongside class, level, and XP through the normal world save. Current save format 2 preserves class, level, XP, ranks and stat allocations; formats 0 and 1 migrate without losing existing progression. Changing catalog IDs after players allocate points requires a migration.

Click anywhere on a skill card to select it, then anywhere across **Learn — 1 point** to allocate the point. Selection alone does not spend it. Both the text and arrow accept the Learn press; allocation submits on mouse-down rather than depending on a later release event. Hovering highlights the active area. Selected cards retain the same grey background as other cards, with gold text indicating selection. A successful allocation confirms the skill name and rank in chat. Client overlays apply only to the deterministic TalentGump type ID, which the integration suite checks against the server. The same server allocation checks remain authoritative. A real-client regression demonstrates that clicking the Learn text failed with the previous client and succeeds with the updated one, including subsequent mouse binding, casting, pet statistics and relogging.

`CharacterProfiles.AdvanceLevel` is a trusted server API for progression up to a provisional level 99; the client has no level-grant command. Graveyard kill XP now uses the Diablo II level thresholds. Teeth, Raise Skeleton and Skeleton Mastery have gameplay effects; the other 207 talent effects remain pending. Class → Mouse skills assigns implemented actions to either button, and Class → Pets displays guardian stats. See [progression and pets](death-and-pets.md) for the current loop and hotkey controls.

The T05 development client groups the thirteen retained skills as follows; this client is tested, not deployed:

| Group | Skills |
| --- | --- |
| Gathering | Fishing, Lumberjacking, Mining |
| Crafting | Alchemy, Blacksmithing, Bowcraft/Fletching, Carpentry, Cartography, Cooking, Imbuing, Inscription, Tailoring, Tinkering |

[T05](t05-professions.md) implements this grouping and profession selection/training; its server release flag defaults to disabled until T14/T15 provide earned public switching. The installed comparison retains Cartography in its separate Treasure Hunting section and all retained crafts usable. In the target [wardrobe design](wardrobe-progression.md), treasure hunting remains an activity; only crafting100, including Cartography, qualifies for Pillar of Society.

Both traditional and advanced skills windows use [the shared list](../shared/TradeSkills.cs), and totals include only those skills. The two development-client sections (three in the installed comparison) are fixed and rebuilt on login. Reset Groups, New Group, group renaming/deletion, and skill drag-and-drop are removed. Dragging a skill neither transfers it between sections nor creates a floating shortcut; the same shortcut drag is disabled in the advanced view. Section collapsing, use buttons, skill locks, and the real/cap toggles remain available. The installed comparison's server skill values/gain rules, recipes, tools and caps remain unchanged. The tested T05 feature adds confirmation, active-craft checks, training/practice and independent progression when enabled; native values/caps/locks remain saved normally. Imbuing opens the white-to-magic/rare and salvage prototype described below. Skinning stays a bladed-tool action supplying hides for Tailoring.

## Verification

Build with `./dev build all`, then run:

```bash
python3 -m unittest discover -s tests
.tools/dotnet/dotnet build tests/Sanctuary.Integration/Sanctuary.Integration.csproj -c Debug -o .local/integration
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_UI=1 python3 tests/check_britain.py
```

The isolated integration world checks all seven classes, allocation rejection and rank limits, persisted ranks after restart, and migration of old profiles on a third boot. Its real-client test opens all three tabs, learns Prayer after a test-only level grant, and attempts dragging Cartography into Gathering and Imbuing out of the menu, saves the actual client state through its Save Desktop macro, and verifies the unchanged 13-skill sections and absence of floating skill buttons. Server assertions check the learned rank and point debit. Screenshots are written under `.local/logs/`; the test uses Xvfb with software rendering. No disposable characters or accounts are copied to the live world.

The live installation was saved and stopped before staging. Its saves, configuration, and client profiles were backed up to `.local/backups/pre-talents-20261017-172819/`. The updated server loaded the same eight mobiles and town object identities. A subsequent save confirmed that Mii (serial 8) retained Necromancer, level 1, XP 0, with no ranks allocated, in save format 1. No test characters were installed in the live world.

Live allocation diagnostics are logged by the server. While investigating a click that fails only in live play, an ignored client marker `Data/Client/talent-input.enabled` also enables `talent-input.log` with incoming gump IDs, mouse targets and submitted button IDs; it records no credentials or chat. Remove the marker after diagnosis. Copied-world checks using the saved character, original account privileges and character profile can learn normally, so they do not by themselves establish that the live input failure is resolved.

The press-submission, grey-card and release-to-stop update is installed with backup `.local/backups/pre-talent-input-release-20261018-005943/`. Saved character progression and all existing client preferences were preserved. Press-before-release learning, summon/binding/relog, movement, combat and loot regressions passed. Live allocation is awaiting the reported-failure retry; do not treat the copied-world success as live confirmation.

The [potion belt and consumable direction](potion-belt.md) now establishes a foundation for alchemy: short clutch power/restoration potions, with hour-long convenience meals reserved for cooking. The belt prototype does not yet add profession recipes or change potion drops.

## Imbuing and salvage prototype — 18 October 2026

The retained Imbuing skill now opens a native menu, also available through `[Essence`. It offers confirmed randomized gear salvage and essence/gold-funded imbuing of eligible white bases. [Economy documentation](economy.md) defines origin/protection checks, recovery chances, item-level material yields and provisional costs. Metal Scraps, Wooden Chips, Leather Strips and Cloth Scraps are physical resources for later profession recipes. This pass does not set profession skill requirements/gains or replace native crafting and repair systems. Material follows the physical base rather than the armour weight category.

The later [crafting handoff](../sanctuary-crafting.md) supersedes conflicting economy-draft rules. Tailoring includes leather and cloth. The repair target is matching material plus rarity-based essence, essence-only spell-charge restoration, and possible maximum-durability loss with a separate special restoration resource; tools remain a separate decision. Imbuing has no destructive failure. Socketing retains destruction on failure with a proposed 100% / 50% / 25% / 5% base schedule. These repair/socket/progression requirements are design, not additional implemented features. Single-use moulds and nine gem families are confirmed in [runes and gems](runes-and-gems.md).
