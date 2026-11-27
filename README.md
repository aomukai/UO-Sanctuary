# Sanctuary

**Diablo II builds and loot in the persistent Britannia of Ultima Online.**

Sanctuary is about discovering what Britannia has to offer and deciding what to do with it. Spend an evening clearing crypts and chasing better gear, follow rumours into a dungeon with Iolo, or make a living at the forge and build a home. You can change direction and keep playing the same character.

## What makes Sanctuary different

- **One Avatar, eight classes.** Play a Necromancer today and a Druid tomorrow. Once you unlock the wardrobe through the campaign or crafting recognition, you can change classes and professions. Each class keeps its own level, attributes, talents, bindings, and equipment. Your crafting progress is saved when you switch trades, and gathering stays available alongside your active craft. [Items, crafting, and wardrobe access](docs/items-and-crafting.md)
- **An interface built for both sides of the game.** Move, attack, and cast with the mouse. Assign quick skills to the hotbar, drink from the belt with **1–4**, and track summons on their skill icons. The character sheet brings equipment and combat stats together, trade skills have their own window, and your companion’s portrait and vitals can dock beside yours. Conversations use illustrated portraits, and the journal records what you learn. [Controls and companions](docs/player-guide.md)
- **A campaign you can take at your own pace.** Follow a three-chapter story through Britannia, skip the story audiences and pursue the bosses with a party, or spend your time elsewhere. Boss victories open later encounters, and finishing the campaign opens boat travel and the post-campaign facets. You can also earn local trust and hear different accounts depending on what your Avatar has done. [Campaign guide, with spoilers](docs/campaign.md)
- **Three custom bosses and arenas.** Face King Leoric in a ruined throne room below Hythloth, Lazarus in a vast mausoleum beneath Deceit, and Diablo on a basalt platform above lava under Destard. Each has its own fight and animated character art. [Boss art and in-game previews](art/campaign-bosses/README.md)
- **Companions with stories and combat roles.** Recruit Iolo for crossbow support or take Wolfgang along when you need someone with a shield. Companions have their own skills, equipment, and supplies. You can leave them at the inn when you want to travel alone. [Companion guide](docs/player-guide.md#companions)
- **Loot hunting in a world you can live in.** Hunt for socketed and magical equipment in hand-built dungeons, buy a shovel to try one of New Haven’s graveyard crypts, or gather, craft, and trade in town. Banks, housing, and workshops give you reasons to come back between expeditions. [Items and crafting](docs/items-and-crafting.md)

## Classes and skills

The eight classes have **240 talents across 24 trees**, including the Warlock from Reign of the Warlock. Combat uses Diablo-style levels, attributes, and talent points alongside Ultima Online’s gathering and crafting skills. Sanctuary adapts skills to its own terrain and persistent world. Supported summons can persist, ranged attacks work without ammunition management, and selected skills can pick a nearby target from a ground click.

| Class | A few ways to play |
| --- | --- |
| Amazon | Lightning Fury, Strafe, or a Valkyrie alongside your bow or spear |
| Sorceress | Frozen Orb, Chain Lightning, and Hydra |
| Necromancer | Skeletons and golems, curses, Bone Spear, and Corpse Explosion |
| Paladin | Zeal, Holy Shield, Blessed Hammer, and defensive or offensive auras |
| Barbarian | Whirlwind, dual wielding, Leap Attack, and war cries |
| Druid | Raven flocks and wolves, elemental spells, Werewolf, and Werebear |
| Assassin | Martial arts, Lightning Sentry, and Shadow Master |
| Warlock / Witch | Bound daemons, Cleave, Hexes, Sigils, and Entropy magic |

[Full talent trees and Sanctuary’s combat rules](docs/classes.md)

## Start in New Haven

The harbour guard will point you toward Iolo at the Bountiful Harvest Inn. Speak to him again at level 6 to open the island’s moongate. You can recruit him or set out alone. The [player guide](docs/player-guide.md#new-haven-and-leaving-the-island) covers the island, travel, and the default controls.

Sanctuary is a solo-developed **PvE development build**. New Haven is playable and the three campaign encounters are implemented. Dungeon populations and the wider world are still being built. [Development roadmap](docs/roadmap.md)

## Running locally

The development installation uses **ClassicUO**, **ModernUO** and separately installed UO client data. Once the installation and local account are configured, open the Linux desktop launcher:

```bash
./dev launcher
```

Click **Play** to open ClassicUO. If the server is stopped, the launcher first installs the latest built content and data and starts it. Closing ClassicUO or the launcher leaves the server running. Click **Save & Shut Down** to save the world and stop the local server, including after reopening the launcher. The Linux launcher uses X11 or XWayland, Tkinter, and Python Xlib (`python3-tk` and `python3-xlib` on Ubuntu). Logs go to `.local/logs/launcher-server.log` and `.local/logs/launcher-client.log`.

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

Install the base UO client data separately. Configure a fresh installation using the version that matches its installed client data. The development installation currently uses **7.0.117.1**, with `SA` as its provisional era:

```bash
./dev configure .local/uo-data --client-version 7.0.117.1 --expansion SA
./dev doctor
```

Existing installations do not need to be configured again. The server console handles initial owner-account setup. The default server is local-only at `127.0.0.1:2593`.

**Classic MIDI soundtrack.** After configuring the client, run `./dev install-classic-music` to render the original MIDI tracks into your local game data. The import uses FluidSynth, FFmpeg and a General MIDI soundfont. On Ubuntu, install `fluidsynth ffmpeg fluid-soundfont-gm`. Choose **Classic MIDI** under **Options → Music**. Cues without a classic recording use the modern track. Use `--archive` to import a ZIP of original MIDI files or `--soundfont` to choose the instruments for the recordings.

For content changes, close the server, run `./dev build content`, and then use the launcher’s **Play** or `./dev server` to install the updated content and data. `--installed` starts the existing installed package without restaging it. Runtime configuration, saves and client settings live under `.local/`.

</details>

### Set up with an agent

If you're using a coding agent on Linux, you can give it this request in the Sanctuary checkout:

> Set up Sanctuary for local play using the Running locally instructions in this README. Check what's already installed, reuse my existing configuration and saves, and run the documented setup commands for anything missing. Run `./dev doctor`, start the local server and client, and tell me when the game is ready or if the server needs me to create the first account.

For source builds, test isolation, and installed updates, see [local development and deployment](docs/local-development.md).

## Handbook and roadmap

The [Sanctuary handbook](docs/README.md) covers playing, classes, GM placement, story, crafting, and local development. Track open work on the [roadmap](docs/roadmap.md).
