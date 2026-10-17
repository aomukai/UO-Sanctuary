# Sanctuary

Diablo II combat and itemisation in a persistent Ultima Online world. The design baseline is [the kickoff brief](Britannia-Diablo-Project-Kickoff.md). Agreed direction and candidate mechanics remain distinct.

The [balance foundation](docs/balance-foundations.md) defines the players-1 level 1–99 monster curve, converted normal gear and cosmetic extra UO slots, running penalties, magic/rare affix rules and persisted rolls, class growth, and solo XP. Monster drops are disabled until loot tables are designed. Dungeon resistance and immunity profiles remain a separate later layer in the design document.

The local foundation is installed with Classic Client data **7.0.117.1**. Both upstream projects compile. The server boots, authenticates the local owner account, saves, and authenticates again after restart. Andy confirmed successful desktop login, entry into the game world, working doors, banker interaction, equipping a dagger, and returning it to the backpack. Andy has also confirmed the mouse combat prototype in play, including killing a skeleton with Teeth. Bank item transfers and the original GM inventory persistence round trip remain separate checks.

## Pinned foundation

| Dependency | Revision |
| --- | --- |
| ModernUO | `eff9de028136b11e544fd81375d1e0e710e22dd6` |
| Desktop ClassicUO | `ee79d7ebc1cc0e53ff84fe6cae17d389e2737f93` |
| .NET SDK | `10.0.201`, installed locally in `.tools/dotnet` |

The server and client are Git submodules under `upstream/`. ClassicUO's nested dependencies are pinned by its own submodules. Project content lives in [Sanctuary.Content](src/Sanctuary.Content/). Small server patches expose [character-creation hooks](patches/ModernUO/0001-character-creation-hooks.patch), [class-combat hooks](patches/ModernUO/0002-class-combat-hooks.patch), [equipment running penalties](patches/ModernUO/0008-equipment-running-penalty-hooks.patch), and [converted gear and disabled monster drops](patches/ModernUO/0009-converted-gear-and-disabled-drops.patch); [item origin persistence](patches/ModernUO/0010-persist-item-origins.patch) adds Vendor/Drop/Crafted to every base Item, with origin-preserving stacks and vendor/craft creation tagging. The [client patch stack](patches/ClassicUO/) covers startup compatibility, class selection, paperdoll and talent controls, combat, corpse looting, class stats, level-up notifications, and equipment movement timing. The password patch accepts up to 30 characters, matching the login packet, and logs login-state transitions without credentials. `setup` and builds apply the patches idempotently and reject conflicting edits. Git therefore reports both submodules as modified; the patch files record those intentional differences. Project build overrides live in `build/`; runtime files, saves, accounts, and logs live in ignored `.local/`.

## Linux setup

Prerequisites: Python 3.10 or later, Git, Bash, and network access for initial source and NuGet downloads. Native server dependencies on Ubuntu/Pop!_OS 24.04 are `libicu74`, `libdeflate0`, `libargon2-1`, and `tzdata`. They were already installed on the initial development machine. The client bundles its FNA graphics/audio libraries and also needs a working desktop graphics driver.

```bash
./dev setup
./dev build
./dev install-data
./dev doctor
python3 -m unittest discover -s tests -v
```

`setup` installs the project SDK without changing the system .NET installation. `build` creates Debug binaries, stages them into `.local/server` and `.local/client`, and writes logs to `.local/logs/`. It preserves existing runtime configuration and saves. The client launcher adds its bundled native-library directory to its own library search path. `doctor` returns a nonzero status while required setup is incomplete.

Server builds also build the project content assembly. For content-only changes, use `./dev build content`; the next `./dev server` launch installs it and its data. Builds refuse to replace a running server or client's binaries. Save the world and close the server before restarting it to load content changes.

The server build pins transitive `System.Security.Cryptography.Pkcs` to `10.0.13` via [our MSBuild override](build/ModernUO.targets). The unmodified upstream restore fails on vulnerability advisories for `10.0.0`; NuGet auditing remains enabled. See [Microsoft's advisory](https://github.com/advisories/GHSA-846f-f764-76mx).

For these shallow Debug checkouts, the server build uses `NBGV_GitEngine=Disabled` and `PublicRelease=false`. Assembly version metadata therefore does not identify the source commit; use the submodule hashes above or `./dev doctor`. This follows [Nerdbank's documented shallow-clone option](https://dotnet.github.io/Nerdbank.GitVersioning/docs/shallow-cloning-agents.html). This is a development build, not a release publishing pipeline.

The client compiles with upstream warnings, including ImageSharp package vulnerability advisories. They are retained in its build log; a successful build does not mean those advisories are resolved.

## UO data and first local session

The data is installed in `.local/uo-data`. The official Windows installer is retained in `.local/downloads/`; its source is the [official download page](https://uo.com/client-download/). ClassicUO uses this separate installation's art and maps.

For a fresh Linux setup, `./dev install-data` uses [UO Patcher](https://github.com/andrezaiats/uo-patcher) at commit `e7e890334705032f66b2ed99eee08e5cace6db93`, verifies its source hash, and downloads from the publisher's patch servers. It excludes unavailable patch-note web pages and checks game-file sizes and archive entries against the manifests. The initial installation verified 420 game files and 44,200 game archive entries. Asset data follows the publisher's current manifest, so future downloads may have a different client version.

The helper stages temporary chunks in `/dev/shm` to avoid slow small-file writes on this exFAT project drive. It needs at least 5 GiB free there; set `SANCTUARY_ASSET_SCRATCH` to another suitable staging directory when needed. Finished files stay on the project drive, and temporary staging is removed.

On this machine, the data path and actual version are already configured. The provisional test era is `SA`, with Felucca and Trammel enabled. This does not settle the eventual world or expansion. To configure a fresh checkout after installing data:

```bash
./dev configure .local/uo-data --client-version 7.0.117.1 --expansion SA
```

Use the version matching the installed `client.exe` if downloading newer data. On this machine, start directly with:

```bash
./dev server
# In a second terminal:
./dev client
```

`configure` checks a few required asset files; it does not certify a complete installation. It refuses to overwrite existing configuration. Edit `.local/server/Configuration/` and `.local/client/settings.json` for later changes.

The generated server listens on `127.0.0.1:2593` and disables public-address detection. The ping listener also binds to loopback. Account auto-creation is disabled. This installation has an owner account; its username and generated password are stored in `~/.config/sanctuary/owner-login.json` with mode `0600`. No credentials are stored in the repository or command arguments. On a fresh installation, create an owner account at the server's interactive console.

New characters now choose one of seven Diablo II classes, customise their appearance, and enter the world near West Britain Bank as an ordinary level-1 player with zero XP. There is no custom profession, skill allocation, or city screen. Class and level appear on the paperdoll. Its **Class** button (or `[Class`) opens three talent trees with ten skills each; its **Skills** button opens the UO skills window filtered to gathering, crafting, and Cartography. Talent ranks, requirements, point spending, and save migration are implemented. A [mouse combat prototype](docs/combat-prototype.md) adds repeated basic attacks and reagent-free, dagger-granted Teeth for Necromancers. Raise Skeleton and Skeleton Mastery are active; most other talent effects remain pending. Graveyard kills award combat XP. See [character creation](docs/character-creation.md) and [talents and trades](docs/class-talents-and-trades.md).

Existing characters are preserved, including the original GM character that started in **Green Acres**. From that staff character, enter `[SanctuaryTest` in game chat to reach West Britain Bank. `[SanctuaryTest vendor` goes to the provisioner/cobbler, and `[SanctuaryTest smith` goes to the armorer/weaponsmith. `[Where` reports the current location. These are in-game staff commands, not server-console commands. A new class character on the owner account receives player access; the owner account and existing GM retain their privileges.

The project content installs a small Felucca neighbourhood, bounded by `(1400, 1620)` through `(1490, 1720)`, using bundled ModernUO data: 330 decorations including eight doors, and three spawners maintaining six NPCs. A saved invisible marker makes decoration installation happen once; vendor spawners retain their existing NPCs across restarts. The Britain graveyard now also has six undead across three spawners; the rest of the world is still unpopulated. This is a baseline test area, not the combat prototype or a full world-generation pass.

At the server console, `sanctuary-status` reports the area's doors and population. With the game data installed, `python3 tests/check_britain.py` exercises a disposable world on a separate loopback port, checks its doors and residents, saves it, and verifies the same object identities after restart. It does not use your accounts or saves.

Doors, opening the bank, and equipping/unequipping a dagger are confirmed in game. Equip a weapon by dragging it onto the paperdoll; double-clicking a dagger invokes its bladed-tool targeting action. For the remaining baseline play checks, verify walking/running and transferring items between backpack and bank. Use the upstream `[Save` command as owner, stop the server cleanly, restart it, and verify the same character, equipment, backpack, and bank contents return. At the server console, `save` writes a save and `shutdown` stops the server; shutdown alone should not replace an explicit save. Record outcomes in [the baseline audit](docs/baseline-audit.md).

The current [combat prototype](docs/combat-prototype.md) supplies hostile click-to-approach/repeated attacks, ground click-to-stop/move with continuous steering while held, Shift + left-click/hold swings in place (including empty swings), and dagger-granted Teeth on right click (hold to repeat at the casting rate), with no reagents or war-mode toggle. The former paperdoll toggle and **Tab** select Run/Walk, and the choice applies to ground movement and enemy approach. Running steps disable defense/parrying; standing still retains defense. Teeth currently reaches 16 tiles for play testing. Hostile picking prefers visible sprite pixels, then uses filled body bounds; physical projectile collision is separate. Class regeneration bypasses Meditation/Focus, and UO skill gains are limited to the 13 retained trades. Enter `[CombatTest` on a class character for two practice targets, and `[CombatTest return` to leave. The next step is play feedback on controls and animation/timing, followed by separate speed modifiers and hit recovery. Most ability effects, further economy rules, and NPC dialogue remain ahead.


Class characters also have [nearby corpse looting](docs/loot-prototype.md): single-click a monster corpse for one combined grid within five tiles, then click an icon to take its whole stack. Empty bodies turn grey and remain usable; successfully harvested bodies disappear only after their remaining contents are taken. Eligible kill gold goes to the backpack, leaving any stack that cannot fit in its corpse. Tooltip simplification and explicit Diablo rarity colours remain ahead.


[Attack and casting timing](docs/combat-timing.md) now uses class/weapon-family profiles and frame breakpoints for IAS/FCR. Actions have a windup, one hit/release point and a full-cycle deadline; equipment changes cannot refund it. Weapon Speed tooltips show Diablo-style labels, with class and actual cycle time when equipped. Weapon damage and projectile travel remain provisional.

[Character attributes and graveyard combat](docs/character-stats.md) add four spendable attributes, five points per level, class-specific life/mana/stamina growth, physical Attack Rating and Defense, weapon stat scaling, and IAS/FCR in the expanded status window. Level-up notifications include Stats and Talents buttons while points are available. The test graveyard currently spawns three skeletons and two slow zombies using [early D2 Normal combat stats and attack timelines](docs/early-normal-monsters.md); mobs have a home leash and abandon prolonged chases. The Firebolt-casting lich and its repeated revival behavior remain implemented for later world building. Only the first death grants rewards, with persistent corpse flags preserving this rule across restarts. Graveyard kills award XP, including kills by skeleton guardians.

The current progression, resurrection, corpse recovery, skill assignment and Raise Skeleton controls are described in [death and pets](docs/death-and-pets.md).
