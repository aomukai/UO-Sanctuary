# Sanctuary

**Diablo II character builds and combat in the persistent world of Ultima Online.**

Sanctuary is a solo-developed game set in Britannia, combining Diablo-style classes, loot and progression with UO's gathering, crafting, towns and player housing. The aim is a world where adventuring and making a home both matter.

## Current state

All **eight classes and their 240 talents** have mechanical implementations, including **Warlock / Witch**. The mechanical reference is **Diablo II: Resurrected — Reign of the Warlock, version 3.3.93847**, with deliberate Sanctuary adaptations for the persistent world.

The current build is a **PvE development build**. Britain and its graveyard provide a small playable test area. The wider world, dungeon progression and overall balance are still being developed. All talents have UO-style icons that also appear on their available bindings. Many combat effects still use temporary UO graphics.

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

Full mercenary recruitment and combat, broader monster ability conversion, world population and progression, remaining item-effect delivery, and final combat visuals and audio are still ahead.

## Playing

New characters begin near **West Britain Bank**. Open the paperdoll's **Class** panel to spend talent points and assign learned skills; **Skills** opens the retained UO trades.

| Control | Action |
| --- | --- |
| Left-click / hold on the ground | Move / steer |
| Left-click / hold on an enemy | Approach and attack with the assigned left action |
| Shift + left-click / hold | Attack while standing in place |
| Right-click / hold | Use the assigned right action |
| F1–F12 | Use assigned quick skills |
| 1–4 | Drink from the potion belt |
| Tab | Toggle Run / Walk |
| Enter | Enter chat |

Available bindings depend on the skill. Running trades defense for movement speed, so positioning and the Run / Walk choice matter in combat.

## Running locally

The development installation uses **ClassicUO**, **ModernUO** and separately installed UO client data. From a configured checkout, start the installed server:

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

## Work ahead

The next phase will use smaller goals, with one complete, playable result at a time:

- Spell effects, animations and sound.
- D2-style ability repertoires for UO monsters, followed by encounter and dungeon authoring.
- Mercenary recruitment, equipment and combat built on the existing companion systems.
- Remaining equipment effects and later designs for charged or crafted skill-granting gear.
- World difficulty, rewards, combat balance and targeted control improvements.

The [design outline](Britannia-Diablo-Project-Kickoff.md) describes the broader game direction.
