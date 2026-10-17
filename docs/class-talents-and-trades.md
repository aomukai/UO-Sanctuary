# Class talents and trades

The paperdoll’s **Class** button replaces Skills and opens the selected character’s class gump. **Skills** replaces Quests and opens the existing UO skills window. `[Class` also opens the class gump. Characters without an assigned Diablo class are not assigned one automatically.

Each of the seven classes has three named trees with ten skills each. The gump shows ranks and level gates, lets players switch trees and inspect prerequisites, and offers Learn when allocation is allowed. Names and dependencies come from [Blizzard’s skill guide](https://classic.battle.net/diablo2exp/skills/); every tree’s source URL is retained in [the catalog](../data/Sanctuary/talents.json). No Blizzard art is bundled.

The provisional allocation rules follow [Diablo II skill basics](https://classic.battle.net/diablo2exp/skills/basics.shtml): level 1 starts with zero points, each subsequent level earns one point, and each learned rank costs one. Skills have a maximum of 20 allocated ranks, prerequisite skills need at least one rank, and learning rank N requires character level at least the skill’s unlock level plus N minus one. Quest bonus points and respec are not implemented. The discussed alternative cap of 25 remains undecided.

The server validates every allocation against current state, including class ownership and available points. Button IDs cannot grant levels, bypass requirements, or spend points twice. Ranks are stored by stable skill ID alongside class, level, and XP through the normal world save. Current save format 2 preserves class, level, XP, ranks and stat allocations; formats 0 and 1 migrate without losing existing progression. Changing catalog IDs after players allocate points requires a migration.

Click anywhere on a skill card to select it, then anywhere across **Learn — 1 point** to allocate the point. Selection alone does not spend it. Both the text and arrow accept the Learn press; allocation submits on mouse-down rather than depending on a later release event. Hovering highlights the active area. Selected cards retain the same grey background as other cards, with gold text indicating selection. A successful allocation confirms the skill name and rank in chat. Client overlays apply only to the deterministic TalentGump type ID, which the integration suite checks against the server. The same server allocation checks remain authoritative. A real-client regression demonstrates that clicking the Learn text failed with the previous client and succeeds with the updated one, including subsequent mouse binding, casting, pet statistics and relogging.

`CharacterProfiles.AdvanceLevel` is a trusted server API for progression up to a provisional level 99; the client has no level-grant command. Graveyard kill XP now uses the Diablo II level thresholds. Teeth, Raise Skeleton and Skeleton Mastery have gameplay effects; the other 207 talent effects remain pending. Class → Mouse skills assigns implemented actions to either button, and Class → Pets displays guardian stats. See [progression and pets](death-and-pets.md) for the current loop and hotkey controls.

The Skills window contains exactly these professions:

| Group | Skills |
| --- | --- |
| Gathering | Fishing, Lumberjacking, Mining |
| Crafting | Alchemy, Blacksmithing, Bowcraft/Fletching, Carpentry, Cooking, Imbuing, Inscription, Tailoring, Tinkering |
| Treasure hunting | Cartography |

Both traditional and advanced skills windows use [the shared list](../shared/TradeSkills.cs), and totals include only those skills. The three sections are fixed and rebuilt on login. Reset Groups, New Group, group renaming/deletion, and skill drag-and-drop are removed. Dragging a skill neither transfers it between sections nor creates a floating shortcut; the same shortcut drag is disabled in the advanced view. Section collapsing, use buttons, skill locks, and the real/cap toggles remain available. Actual server skill values, skill gain, recipes, tools, and caps are unchanged. Imbuing is visible but its planned white-to-rare system remains future work. Skinning stays a bladed-tool action supplying hides for Tailoring.

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
