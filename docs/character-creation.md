# Character creation

The local flow is **class → UO appearance/name → world**. The class screen offers Amazon, Sorceress, Necromancer, Paladin, Barbarian, Druid, and Assassin. Back from appearance returns to the class screen and retains the appearance choices. Confirming appearance submits the selected class directly; the original profession, custom skill, and city screens are not part of this flow.

New characters start near West Britain Bank on Felucca as ordinary players, with a saved class, level 1, and zero combat XP. Their paperdoll title displays the class/level, and `[Class` opens their talent trees. They receive the stock generic starter backpack, items, and clothes. Strength, Dexterity, and Intelligence provisionally start at 30 each, with no UO starting skill allocation. These neutral stats are a baseline for the next combat prototype, not Diablo balance formulas. The [talent interface](class-talents-and-trades.md) now supports rank allocation and a trusted server API for advancing levels. Most ability effects and combat XP awards remain pending; the [combat prototype](combat-prototype.md) now supplies basic attacks and dagger-granted Teeth.

Existing characters receive no invented class or reset. Creating a class character on the owner account leaves account privileges and existing GM characters intact; only the newly created character has player access.

Character names and class titles are separate server fields. For example, the new character Mii is named `Mii` with title `Level 1 Necromancer`. UO's paperdoll packet combines them into one caption. The client now displays recognised class captions as two lines, with the name above the level/class title, without changing the stored name or title. Other captions keep their existing formatting.

This display change passed the real-client creation/world-entry check and visual inspection of the two-line paperdoll caption. Character-name and class persistence checks still passed. The original four client patches applied successfully to fresh pinned-source files and passed repeated application checks. Only the client was rebuilt/reopened for this change; the live server stayed running.

## Code and persistence

- [Shared class IDs](../shared/DiabloClass.cs) are compiled into both client and server content. IDs 101–107 occupy the existing creation packet's profession byte, apart from stock UO IDs 0–7.
- [Client patch](../patches/ClassicUO/0003-diablo-class-creation.patch) replaces profession choices, reorders the flow, and excludes the old skill/city gumps from compilation. It retains the existing appearance editor.
- [Server patch](../patches/ModernUO/0001-character-creation-hooks.patch) adds validation/completion hooks, the original requested template ID, and server-side overrides for character access and starting city. No server-engine files are changed.
- [Class creation](../src/Sanctuary.Content/ClassCreation.cs) rejects unsupported and legacy/custom IDs before allocating a character. It selects player access, sanitizes client stats/skills, chooses the test spawn, and assigns class progression.
- [Character profiles](../src/Sanctuary.Content/CharacterProfiles.cs) stores serial → class/level/XP and talent ranks through ModernUO's normal save snapshot in `Saves/SanctuaryCharacters/SanctuaryCharacters.bin`. Deleted characters are pruned before creation and saves. Version 1 adds ranks identified by stable skill IDs and reads existing version-0 profiles without resetting class, level, or XP. No custom mobile/item types were introduced.

`./dev setup` and server/client builds apply their tracked patches idempotently. `./dev build server` also compiles the content module; `./dev server` installs the built module and data before launch. Save and close a running server/client before replacing their binaries.

## Verification

With the project built and game assets installed:

```bash
.tools/dotnet/dotnet build tests/Sanctuary.Integration/Sanctuary.Integration.csproj \
  -c Debug -o .local/integration
SANCTUARY_TEST_CREATION=1 python3 tests/check_britain.py
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_UI=1 python3 tests/check_britain.py
```

The first integration mode exercises all seven classes through the real character-creation event path, rejects invalid/custom IDs, checks that supplied stats/skills cannot override the baseline, verifies appearance and starter inventory, and checks level/class/XP after save/restart. It also checks that an existing GM remains untouched and deleted progression is pruned. Its minimal protocol state has no socket; it is not a network/UI test.

The UI mode additionally runs the actual patched ClassicUO under Xvfb on a separate loopback server: select Amazon, go Back, select Paladin, enter a name, and proceed directly into the world. The server checks that the real packet created the selected class at level 1. The UI test then grants one level only in its disposable world, opens all three Class tabs, learns Prayer, and opens Trade. Server assertions verify the spent point and saved rank; a further restart tests old version-0 profile migration. The saved Trade groups must contain exactly the 13 intended skill IDs. Screenshots and logs are written under `.local/logs/`. This mode needs Xvfb, xdotool, and Pillow, and uses software OpenGL/dummy audio. Both modes use disposable accounts and worlds, never your saved characters.

Both modes passed on 17 October 2026, including all seven profiles after restart and the real client's Paladin creation. The screen test caught and verified a correction to the wizard's clickable bounds so the appearance Next arrow works. Server/content builds had zero warnings/errors; the client built with its existing upstream warnings and no errors. Both patch series were also applied successfully to fresh copies of the pinned source files, and repeated application checks passed.

The live world was explicitly saved and shut down before installation. Saves/configuration were copied to `.local/backups/pre-classes-20261017-165511/`. The updated server then loaded the existing world with seven mobiles, retained the same eight door/three spawner/six resident identities, authenticated the existing owner, and wrote a successful save including the new progression persistence file. No test characters or accounts were copied into the live world.
