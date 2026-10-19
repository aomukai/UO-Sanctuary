# Sanctuary

Diablo II combat and itemisation in a persistent Ultima Online world. The design baseline is [the kickoff brief](Britannia-Diablo-Project-Kickoff.md). Agreed direction and candidate mechanics remain distinct.

**Start here for the next implementation:** the [crafting/economy roadmap](docs/implementation-roadmap.md) orders small tasks by dependency and playable loop, with current code entry points, open-decision gates and acceptance checks. Pick one ready task rather than implementing the whole design at once.

The [T01 working baseline](docs/t01-working-baseline.md) records the comparison build verified on 19 October. The [T02 reference cohort](docs/t02-reference-cohort.md) adds eight base mappings, five physical materials and audited affix pools. [T03](docs/t03-class-names.md) implements and verifies the confirmed cosmetic class names. [T04](docs/t04-item-state.md) versions gear metadata with deterministic legacy defaults and preserves it through native item paths. [T05](docs/t05-professions.md) adds first-profession confirmation, server activation checks, paid training and no-output practice. These matching builds are tested, not deployed; T05 defaults to disabled until earned switching is available. [T06](docs/t06-crafting.md) adds guarded native tool crafting, persisted tool quality/uses, configurable pricing and shared public town facilities. [T07](docs/t07-processing.md) adds canonical material weights, portable generic scrap processing and atomic direct native conversions, with existing stacks and vendor exclusions checked in disposable worlds. [T08](docs/t08-white-bases.md) adds603 white-equipment recipes across201 standard-material identities and Normal/Exceptional/Elite, including gloves, boots and potion tabards, with persistent quality, sockets, maker marks and durability. These profession changes remain disabled and undeployed. Coloured-material rolls are saved but inactive pending T09. The [ordinary recipe book](docs/t08-recipe-book.md) is complete for the agreed scope; ten exotic recipes are explicitly deferred. T09 is next.

The [balance foundation](docs/balance-foundations.md) defines the players-1 level 1–99 monster curve, converted normal gear and cosmetic extra UO slots, running penalties, magic/rare affix rules and persisted rolls, class growth, and solo XP. Monster drops are disabled until loot tables are designed. Dungeon resistance and immunity profiles remain a separate later layer in the design document.

The [equipment economy prototype](docs/economy.md) reduces NPC gear resale and adds randomized salvage into Magic Essence and physical materials, plus essence-funded white-base imbuing. Vendor-origin equipment cannot be salvaged or imbued. Prices, yields and chances are provisional tuning data.

The [first-implementation crafting/economy specification](docs/crafting.md) reconciles the completed player decisions. It describes the next target and is separate from the installed comparison build; the [decision tracker](docs/economy-open-questions.md) records remaining details.

The [reviewed monster XP roster](docs/creature-reward-roster.md) covers every common creature type, with D2 counterpart rewards and consistent combat/XP levels. Normal is live; Nightmare/Hell rewards are prepared data. [Reward rules and limitations](docs/creature-experience.md).

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

The current [combat prototype](docs/combat-prototype.md) supplies hostile click-to-approach/repeated attacks, ground click-to-stop/move with continuous steering while held, Shift + left-click/hold swings in place (including empty swings), and dagger-granted Teeth on right click (hold to repeat at the casting rate), with no reagents or war-mode toggle. The former paperdoll toggle and **Tab** select Run/Walk, and the choice applies to ground movement and enemy approach. Running steps disable defense/parrying; standing still retains defense. Teeth currently reaches 16 tiles for play testing. Hostile picking prefers visible sprite pixels, then uses filled body bounds; physical projectile collision is separate. Class regeneration bypasses Meditation/Focus, and UO skill gains are limited to the 13 retained trades. Enter `[CombatTest` on a class character for two practice targets, and `[CombatTest return` to leave. Play feedback on controls, timing and the recovery prototype is still needed; FHR equipment and species-specific approach behavior remain separate follow-ups. Most ability effects, further economy rules, and NPC dialogue remain ahead.


A [movement smoothness comparison](docs/movement-smoothness.md) improves responsive tile paths and removes the client turn pause while retaining the existing protocol, collision and controls. That report records the remaining tile limitations and a proposed fractional-position prototype. The [melee comparison](docs/melee-reach.md) adds responsive pursuit and provisional halberd reach; [hit recovery](docs/hit-recovery.md) covers the separate recovery rules and monster reaction findings.

Class characters also have [nearby corpse looting](docs/loot-prototype.md): single-click a monster corpse for one combined grid within five tiles, then click an icon to take its whole stack. Empty bodies turn grey and remain usable; successfully harvested bodies disappear only after their remaining contents are taken. Eligible kill gold goes to the backpack, leaving any stack that cannot fit in its corpse. Tooltip simplification and explicit Diablo rarity colours remain ahead.


[Attack and casting timing](docs/combat-timing.md) now uses class/weapon-family profiles and frame breakpoints for IAS/FCR. Actions have a windup, one hit/release point and a full-cycle deadline; equipment changes cannot refund it. Weapon Speed tooltips show Diablo-style labels, with class and actual cycle time when equipped. Weapon damage and projectile travel remain provisional.

[Character attributes and graveyard combat](docs/character-stats.md) add four spendable attributes, five points per level, class-specific life/mana/stamina growth, physical Attack Rating and Defense, weapon stat scaling, and IAS/FCR in the expanded status window. Level-up notifications include Stats and Talents buttons while points are available. The test graveyard currently spawns three skeletons and two slow zombies using [early D2 Normal combat stats and attack timelines](docs/early-normal-monsters.md); mobs have a home leash and abandon prolonged chases. The Firebolt-casting lich and its repeated revival behavior remain implemented for later world building. Only the first death grants rewards, with persistent corpse flags preserving this rule across restarts. Graveyard kills award XP, including kills by skeleton guardians.

The current progression, resurrection, corpse recovery, skill assignment and Raise Skeleton controls are described in [death and pets](docs/death-and-pets.md).

Wild graveyard skeletons and zombies now have [separate notice and approach behavior](docs/monster-reactions.md), preserving their walking rates. A [common creature profile](docs/creature-profiles.md) covers all 726 built-in UO creature types and four project types, with Normal/Nightmare/Hell fields and monster hit recovery. Unconverted stats remain native; creative analogues such as fire dragons are recorded as disabled proposals until their abilities are converted and tested.

Class characters now have a [four-column potion belt](docs/potion-belt.md) in the existing tabard slot, with 1–4 hotkeys and mandatory Enter-to-chat. This comparison adds real persisted belt storage, healing/mana restoration and a 20% empty-bottle recovery chance. GM comparison kits are explicit; drop tables and profession recipes remain separate. The same document records short clutch potion buffs versus hour-long convenience meals as the direction for the later alchemy/cooking pass.

[Kill experience](docs/creature-experience.md) now uses enabled common creature-profile rewards, retaining the original D2 level curve, pet-owner credit and persistent suppression of repeat revived-monster XP. Unreviewed/disabled reward entries remain zero.
