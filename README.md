# Sanctuary

**Diablo II character builds and combat in the persistent world of Ultima Online.**

Sanctuary is a solo-developed game set in Britannia, combining Diablo-style classes, loot and progression with UO's gathering, crafting, towns and player housing. The aim is a world where adventuring and making a home both matter.

## Current state

All **eight classes and their 240 talents** have mechanical implementations, including **Warlock / Witch**. The mechanical reference is **Diablo II: Resurrected — Reign of the Warlock, version 3.3.93847**, with deliberate Sanctuary adaptations for the persistent world.

The current build is a **PvE development build**. [New Haven](docs/new-haven.md) is the first playable stretch, with encounters for levels 1–10, trade training, a mine boss, and the option to recruit Iolo. The wider campaign and world progression are in development.

All talents have UO-style icons that also appear on their available bindings. All 210 active skills have art and audio verified in ClassicUO. [Skill effects and sound](docs/skill-effects.md)

### Classes

Each class has three talent trees with ten talents each.

| Class | Play styles |
| --- | --- |
| Amazon | Bows, crossbows, javelins and spears |
| Sorceress | Fire, cold and lightning magic |
| Necromancer | Skeletons, golems, revives, curses, bone and poison magic |
| Paladin | Auras, holy magic, shields and weapon combat |
| Barbarian | Weapon combat, dual wielding, war cries and movement skills |
| Druid | Animal companions, spirits, vines, elemental magic and shapeshifting |
| Assassin | Martial arts, traps and shadow disciplines |
| Warlock / Witch | Demons, Hexes, Sigils, Entropy magic and weapon skills |

Sanctuary makes its own choices where D2's rules do not suit a persistent UO world. These include persistent summons, safe-area summon parking, improvements to Necromancer and Druid pets, and targeting assistance for selected skills. Charms and runewords are not planned.

### Other systems already in place

- Character levels, attributes, talent points, equipment bonuses and potion belts.
- Equipment drops, magical properties, sockets, materials and crafting.
- Gathering and crafting trades, town merchants, banking and repairs.
- Housing, equipment insurance, death penalties and corpse recovery.
- Persistent characters, inventories and supported companions across server restarts.
- A unified character screen, class talent trees, a journal, and private scripted NPC dialogue.

## Playing

New characters begin in **New Haven**. Follow the harbour guard’s directions to the Bountiful Harvest Inn to meet Iolo and unlock the first journal entry. Speak to him again after reaching level 6 to open the island’s moongate. He can travel with you, or you can leave on your own.

Click NPCs to talk, trade, bank, or train. Trainers teach trades for 1 gold per 0.1 skill up to 30.0. Elara the Scholar at the north mage tower identifies equipment for 50 gold per item and sells Identify and Town Portal scrolls.

| Control | Action |
| --- | --- |
| Left-click / hold on the ground | Move / steer |
| Left-click / hold on an enemy | Approach and attack with the assigned left action |
| Shift + left-click / hold | Attack while standing in place |
| Right-click / hold | Use the assigned right action |
| B / C | Toggle the backpack / character screen |
| J / M | Toggle the journal / world map |
| T / K | Toggle class talents / trade skills |
| Esc | Toggle Options |
| S | Assign learned skills to the hotbar |
| F1–F12 | Use assigned quick skills |
| 1–4 | Drink from the potion belt |
| Tab | Toggle Run / Walk |
| Enter | Enter chat |

Change the window shortcuts under **Options → Hotkeys**. Available combat bindings depend on the skill. Running trades defense for movement speed, so positioning and the Run / Walk choice matter in combat.

## Running locally

The development installation uses **ClassicUO**, **ModernUO** and separately installed UO client data. Once the installation and local account are configured, open the Linux desktop launcher:

```bash
./dev launcher
```

Click **Play** to start the installed server and open the client. **Save & Shut Down** closes the game and waits for the server to write the world save. Closing ClassicUO or the launcher window also saves and stops the local server. The Linux launcher uses X11 or XWayland, Tkinter, and Python Xlib (`python3-tk` and `python3-xlib` on Ubuntu). Logs go to `.local/logs/launcher-server.log` and `.local/logs/launcher-client.log`.

To run the server from a terminal, including for first-time owner account setup, use:

```bash
./dev doctor
./dev server --installed
```

Then start the client in another terminal:

```bash
./dev client
```

At the server console, use `save` and wait for it to finish before `shutdown`.

<details>
<summary>Build and configure a local installation</summary>

You need Git, Bash, Python 3.10 or later, a working graphics driver and network access for the initial downloads. `./dev setup` manages the project's pinned .NET SDK **10.0.201**. On Ubuntu / Pop!_OS 24.04, native server dependencies include `libicu74`, `libdeflate0`, `libargon2-1` and `tzdata`.

```bash
./dev setup
./dev build
./dev install-data
```

UO art and maps are downloaded separately; they are not included in this repository. Configure a fresh installation using the version that matches its installed client data. The development installation currently uses **7.0.117.1**, with `SA` as its provisional era:

```bash
./dev configure .local/uo-data --client-version 7.0.117.1 --expansion SA
./dev doctor
```

Existing installations do not need to be configured again. The server console handles initial owner-account setup. The default server is local-only at `127.0.0.1:2593`.

For content changes, close the server, run `./dev build content`, then start with `./dev server` to stage the updated content and data. `--installed` starts the existing installed package without restaging it. Runtime configuration, saves and client settings live under `.local/`.

</details>

### Set up with an agent

If you're using a coding agent on Linux, you can give it this request in the Sanctuary checkout:

> Set up Sanctuary for local play using the Running locally instructions in this README. Check what's already installed, reuse my existing configuration and saves, and run the documented setup commands for anything missing. Run `./dev doctor`, start the local server and client, and tell me when the game is ready or if the server needs me to create the first account.

The [launcher design](docs/launcher.md) describes the planned guided setup, local and community play, and optional LLM settings.

For source builds, test isolation, and installed updates, see [local development and deployment](docs/local-development.md).

## Work ahead

- The campaign beyond New Haven, including optional investigations and difficulty progression.
- More companions, personal stories, and journal discoveries.
- D2-style ability repertoires for UO monsters and more authored encounters and dungeons.
- Remaining equipment effects and later designs for charged or crafted skill-granting gear.
- World difficulty, rewards, combat balance and targeted control improvements.

The [design overview](docs/design-overview.md) links to the current direction and implementation records.
