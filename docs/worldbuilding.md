# Worldbuilding and GM catalog

Build towns around residents, trades, homes, and roads. Give recurring enemies places to live and each encounter room enough space for an approach and a retreat. Sanctuary’s world installer handles stock Britannia decoration, doors, signs, vendors, and wildlife. The tables below cover Sanctuary’s custom population and placement tools. [World installer](../src/Sanctuary.Content/BritanniaWorld.cs)

## Spawners

Use `[Add <type>` and target the tile. The spawners in this first table maintain one encounter. The values are per draw, so a 10% entry replaces the common spawn.

| Type | Draw and behavior | Respawn |
| --- | --- | --- |
| `HavenBogling` | Slow bogling, level 1–3. 25% chance of one plain log. | 75–120 seconds |
| `HavenBandit` | 90% farm bandit, 10% Haven thief, level 4–6. Both melee. A thief can vanish for three seconds once, on a 30% roll below half health. | 75–120 seconds |
| `HavenUndead` | Equal draw of slow melee skeleton or zombie, level 4–6. | 75–120 seconds |
| `HavenGhost` | 10% spectre caster (6–8), 40% wraith melee (6–8), 20% skeleton knight (8–10), 30% ghoul (9–11). A ghoul can feed at a nearby corpse for 25% maximum health, with a one-minute cooldown. The corpse stays, and healing requires a corpse. | 75–120 seconds |
| `HavenMines` | 90% earth elemental (6–8), with a 10% iron ore drop. 10% granite elemental (7–9), which throws rocks. | 75–120 seconds |
| `DespiseTroll` | Troll or harpy, level 24–28, equal weights. | 75–120 seconds |
| `DespiseOgre` | Ogre or stone harpy, level 26–30, equal weights. | 75–120 seconds |
| `DespiseOgreLord` | 80% ogre lord, 20% ogre mage, level 28–30. Lord variants can roll Champion. | 75–120 seconds |
| `DespiseEttinSpawner` | Wandering level 22 ettin warlord. Uses built-in Despise spawn points, so use it at its authored site. | 5–7 minutes |
| `YewCultist` | 75% black-robed mage and 25% red-robed healer. Black becomes a level 28–34 imp, hell hound, hell cat, or gargoyle (70/10/10/10). Red heals an ally within eight tiles for 30% maximum life and transforms into a level 25 daemon. | 2–4 minutes |
| `HedgeGazer` | 30% larva or 30% imp (30–35), 15% gazer or 15% gargoyle (33–38), 5% elder gazer or 5% stone gargoyle (38–42). | 2–4 minutes |

[Haven spawners](../src/Sanctuary.Content/HavenSpawners.cs) · [Haven specials](../src/Sanctuary.Content/HavenEncounterSpecials.cs) · [Despise spawners](../src/Sanctuary.Content/DespiseSpawners.cs) · [Ettin warlord](../src/Sanctuary.Content/BritanniaFrontier.cs) · [Yew cult](../src/Sanctuary.Content/YewEncounter.cs) · [Hedge encounters](../src/Sanctuary.Content/HedgeEncounter.cs)

The default Haven monster population was retired for hand placement. The named mine ogre **Krulg** and wildlife are retained. Haven surface encounters scale at Players 3 and the mines at Players 5. Saved placements can differ from a fresh world. [Haven encounter data](../data/Sanctuary/new-haven-encounters.json)

### Hythloth spawners

Target the center of a clear area. Each ordinary Hythloth spawner maintains one creature at a time. Each respawn draws a role using the listed odds and rolls that creature’s level. Ordinary respawn checks run every 75–120 seconds. The named court bosses use individual spawners with 5–7 minute respawn checks.

| Placement | Creature | Level | Chance | Appearance and abilities |
| --- | --- | ---: | ---: | --- |
| `[Add HythLow` | Acolyte | 30–32 | 34% | Bald human in a black robe. Dagger melee. |
| | Royal Soldier | 31–33 | 33% | Skeletal knight model. Melee. |
| | Undead Servant | 32–34 | 33% | Skeleton. Ice Blast and single-target healing for other undead. |
| `[Add HythMed` | Temple Guard | 33–35 | 25% | Human in Shadow Iron chain and ringmail with a chaos shield. Melee and one Paladin aura rolled at spawn. |
| | Royal Arbalest | 33–35 | 25% | Skeletal crossbowman. Fires bolts. |
| | Apprentice | 34–36 | 25% | Robed human. Lightning and staff melee. |
| | Undead Cleric | 35–37 | 25% | Skeleton in a black robe. Fireball, nearby undead healing, and raises fallen human Hythloth allies as skeletal knights. |
| `[Add HythHigh` | Royal Knight | 36–38 | 50% | Human in Shadow Iron plate with a halberd. Whirlwind and Battle Orders for the nearby court. |
| | Skeletal Mage | 37–39 | 50% | Skeleton in a black robe. Fire Bolt. |
| `[Add HythBoss Herald` | The Herald | 32 | — | Skeletal bowman. Announces the Avatar as an unwelcome guest and calls four Royal Soldiers once per life. |
| `[Add HythBoss Jester` | The Jester | 35 | — | Skeleton with a jester’s hat. Teleport and Frozen Orb. One combat bark at most every 30 seconds. |
| `[Add HythBoss Chamberlain` | The Chamberlain | 36 | — | Skeleton with a mace. Landed hits have a 25% chance to stun for three seconds, followed by five seconds of immunity to that stun. Scolds servants at most every 30 seconds. |
| `[Add HythBoss Executioner` | The Executioner | 38 | — | Oversized skeleton with an executioner’s axe. Landed hits have 10% Crushing Blow and apply Open Wounds. |
| `[Go Leoric` | King Leoric | 40 | — | The existing arena manages his spawn and awards campaign victory. |

The Undead Servant and Cleric heal 25% of maximum life within eight tiles on an eight-second cooldown. The Cleric’s raised knights give no kill rewards. Individual roles can also be placed with `[Add HythlothEncounter <Role>` using the role name without spaces. The custom skeleton outfits and Executioner scale use the matching ClassicUO build. [Spawner definitions](../src/Sanctuary.Content/HythlothSpawners.cs) · [Role behavior and bark text](../src/Sanctuary.Content/HythlothEncounter.cs) · [Leoric arena](campaign.md#progression-and-arena-access)

Each miniboss drops two pieces of equipment from its own pool and four healing or mana potions. The Herald rolls bows, crossbows, chain armour, and amulets. The Jester rolls staffs, wands, and jewellery. The Chamberlain rolls maces, mauls, and shields. The Executioner rolls axes and plate. Gear rolls use the miniboss’s level and the credited player’s Magic Find.

### Deceit spawners

Each Nightmare spawner draws **one creature**, with a 25% chance per row in its pool and a 75–120 second respawn interval. Slave and follower ancestry is rolled separately, with equal chances for human, elf, and gargoyle. Use `[Add DeceitEncounter <Role>` to place a specific role, using its name without spaces.

| Placement | Creature | Level | Appearance and combat |
| --- | --- | ---: | --- |
| `[Add DecLow` | Slave | 35–37 | Human, elf, or gargoyle in worn cloth. Melee. At quarter health, becomes neutral, calls out in confusion, and flees before despawning. |
| | Demonic Taskmaster | 37–39 | Monster gargoyle. Melee and Might. |
| | Watchful Overseer | 39–41 | Gazer. Decrepify and Bone Spear. |
| | Devout Follower | 39–41 | Human, elf, or gargoyle in black. Resurrects a fallen Deceit ally within eight tiles, with a 10-second cooldown. Resurrected allies yield no kill rewards and their corpses cannot be raised again. |
| `[Add DecMed` | Familiar | 41 | Horde Minion with half normal life. Landed melee hits have a 25% chance to stun for three seconds, with no immunity or diminishing returns. |
| | Necromancer Apprentice | 41–43 | Bald human in a black robe. Bone Spear. |
| | Cultist Enslaver | 43–45 | Human in medium armour. Melee and Vigor. |
| | Demonic Torturer | 45–47 | Horrific Beast. Melee and Sigil: Lethargy. |
| `[Add DecHigh` | Necromancer | 47–49 | Lich. Weaken, Life Tap, and Bone Armor. Summons a level-25 skeleton every 30 seconds in combat, up to five living skeletons. |
| | Seductress | 49–51 | Custom succubus. Lower Resist and blue cold bolts. |
| | Infernal Mistress | 51–53 | Custom succubus. Amplify Damage and red fire bolts. |
| | Bishop | 53–54 | Bald human in a black robe with a black staff. Heals nearby wounded Deceit allies and casts Lightning and Miasma Bolt. |

### Destard spawners

The bottom red moongate leads to the [Diablo cave and boss arena](campaign.md#destard-and-the-dragons). GMs can enter directly with `[go Diablo`.

Each spawner draws one creature and checks for a respawn after 75–120 seconds. Destard uses the campaign’s Hell resistance rules, with an 85% ceiling and no dual immunities.

| Placement | Creature | Level | Chance | Combat |
| --- | --- | ---: | ---: | --- |
| `[Add DestLow` | Wyvern | 55–57 | 34% | Poison melee and poison bolt. 75% poison resistance. |
| | Enslaved Drake | 56–58 | 33% | Melee and Inferno. 75% fire resistance. |
| | Lesser Daemon | 57–59 | 33% | Imp model. Lightning and Fireball. |
| `[Add DestMed` | Daemon | 59–60 | 50% | Melee and Fire Wall. |
| | Enslaved Dragon | 60–62 | 50% | Melee, Fireball, and Meteor. 75% fire resistance. |
| `[Add DestHigh` | Wyrm | 61–63 | 50% | Black wyrm. Melee, Blizzard, and Frozen Orb. 75% cold resistance. |
| | Archdaemon | 62–64 | 50% | Black daemon. Hydras and Conviction. |
| `[Add DestSpawn` | Corrupted Hatchling | 20 | 50% | Miniature dragon. Melee and Conviction. |
| | Daemon Spawn | 20 | 50% | Horde Minion model. Melee and Lower Resist. |

Use `[Add DestardEncounter <Role>` to place an individual creature. Write the role without spaces. Enslaved drakes and dragons speak their last words on death. Identical Conviction auras and repeated curses use the shared strongest aura and curse rules. For Destard’s Lower Resist and Conviction, the stronger overlapping resistance penalty applies. [Spawn pools](../src/Sanctuary.Content/DestardSpawners.cs) · [Creature behaviors](../src/Sanctuary.Content/DestardEncounter.cs)

Place **Vaelith, the dragon queen** with `[Add DestardQueen` and **Rhazek, the queen’s consort** with `[Add DestardConsort`. Put them within talking range of a shared spot near the entrance so both appear in the queen’s dialogue. Vaelith pays for daemon horns. Rhazek explains who betrayed them and who was enslaved. The hatchling scale and both portraits use the matching ClassicUO build. [Destard court](../src/Sanctuary.Content/DestardCourt.cs) · [Bounty and campaign aid](campaign.md#destard-and-the-dragons)

### Shame

Shame is an optional level 20–40 dungeon on Trammel. Players 5 scaling applies throughout, including the pirate expedition. The first level has the excavations and the dead miner’s note. The second has pirates and the shipboy’s cargo manifest. Both mage houses on the third level have clues, and the western house has the unstable mineral. On the fourth level, the main pirate ship lies at **5749, 60 (-5)**. Board from the east via the gangplank.

Double-click the entrance or exit sparkles within two tiles. Step into the black moongates between levels or double-click them within two tiles. On the second level, the pirate ships connect the eastern shore at **5596, 87 (0)** with the isolated western bank at **5553, 80 (0)**. Step onto either ship’s shore-side gangplank or double-click it from up to two tiles away. You arrive on land beside the other ship.

Place Shame’s ordinary enemies manually with the following GM spawners. Each spawner keeps one creature active and rolls uniformly among its roles. Every spawn has an independent 10% chance to be a Paragon within the same level band. Respawn checks run every 75–120 seconds.

| Placement | Level | Roster |
| --- | ---: | --- |
| `[Add ShameOne` | 20–25 | Silicon, chrysoberyl, corundum, and tungsten elementals |
| `[Add ShameTwo` | 24–29 | Titanium and moissanite elementals, pirate, and tidesinger |
| `[Add ShameThree` | 28–33 | Stishovite and diamond elementals, stormcaller, and corsair |
| `[Add ShameFour` | 38–40 | Wind, fire, water, and earth elementals |
| `[Add ShameMage` | 33, 35 | Crazed researcher (Twister, Tornado) or fanatic experimenter (Hurricane, Firestorm) |

Clear the ship’s crew to open the wheel. It admits the first Avatar and then their original party members for five minutes. The ship has one shared expedition at a time. The route begins with Keelbone, then Brine and his daemon, then Cutwater and Cinders. The castaway captain points out two shields. Order leads to shore. Chaos leads to the level-38 fleet captain and the plunder chest. The normal run lasts 30 minutes from the first crossing. A final victory sets the end ten minutes from the kill. Taking Order sets it to ten minutes once the last admitted player leaves. Death sends the player’s ghost to the Shame entrance and their corpse to the fourth-level shore at **5748, 89 (0)**.

### Wrong

Wrong is a former prison taken over by ogres who raise drakes and hire human manhunters to bring them captives. On Trammel, open the entrance doors and follow the hall to the sparkles. Double-click the sparkles within two tiles to use the entrance, exit, and stair passages.

Place the ordinary encounters manually. Each spawner maintains one creature, draws each role with equal probability, and has an independent 10% chance to spawn it as a Paragon at the same level. The respawn interval is 75–120 seconds.

| Placement | Level | Roster |
| --- | ---: | --- |
| `[Add WrongOne` | 50–56 | Ogre thug (50–52), ogre ruffian (51–53), manhunter (53–54), and slave merchant (54–56) |
| `[Add WrongDrake1` | 52–56 | Drake hatchling (52–54), drake juvenile (53–55), and adult drake (54–56) |
| `[Add WrongTwo` | 55–58 | Ogre rowdy (55–57) and ogre brute (56–58) |
| `[Add WrongDrake2` | 56–59 | Battle drake (56–58) and assault drake (57–59) |

Two named encounters install automatically on Trammel and respawn together 5–7 minutes after a group is defeated.

- **Kitchen.** Wonk **5675, 529 (0)**, Uruk **5691, 528 (0)**, and Glokurg **5707, 524 (0)** are level 58 and join the same fight.
- **Drake court.** Goegork **5703, 639 (0)** is level 60. Xoldred **5700, 639 (0)** and Suldri **5706, 639 (0)** are level 59, with Stone Skin, 75% resistance in every damage channel, and twice their normal life. Goegork watches until both drakes are slain. His dialogue uses the visitor’s race and level. He identifies their class after seeing two distinct learned skills from it.

[Wrong spawners](../src/Sanctuary.Content/WrongSpawners.cs) · [Combat and rosters](../src/Sanctuary.Content/WrongEncounter.cs) · [Boss encounters](../src/Sanctuary.Content/WrongBossSite.cs)

### Single creatures and encounter packs

Unique and SuperUnique creatures use the model’s own palette or natural skin hues. Use equipment, body models, and animations to distinguish them. The full-body grade tint is reserved for Paragons.

| GM command or spawn entry | Use |
| --- | --- |
| `[SpawnEncounter <profile-or-name> <difficulty> <grade>` | Target clear ground within 12 tiles. Difficulty is Normal, Nightmare, or Hell. Grades include Ordinary, Paragon, Unique, and SuperUnique. Profiles and named encounters are data-driven. [Encounter profiles](../data/Sanctuary/Balance/encounters.json) |
| `[SpawnVariant <difficulty> <CreatureType>` | Target a position for a supported ordinary creature with the chosen difficulty. [Variant code](../src/Sanctuary.Content/MonsterVariants.cs) |
| `HavenEncounter <id> <level>` or `<id> <minimum> <maximum>` | Specific New Haven species. [Species IDs](../src/Sanctuary.Content/HavenEncounter.cs) |
| `FrontierEncounter <id> <level> [modifierCount]` | Frontier and Despise species. [Species IDs](../src/Sanctuary.Content/FrontierEncounter.cs) |
| `CoveEncounter <id> <level>` · `CovetousEncounter <species> <level>` | Cove and Covetous species. [Cove](../src/Sanctuary.Content/CoveEncounter.cs) · [Covetous](../src/Sanctuary.Content/CovetousEncounter.cs) |
| `OrcFortEncounter <role>` · `BrigandEncounter` | Orc fort roles and randomized brigands. [Fort](../src/Sanctuary.Content/OrcFortEncounter.cs) · [Brigands](../src/Sanctuary.Content/BrigandEncounter.cs) |
| `YewEncounter [id]` · `HedgeEncounter [id]` | A rolled cultist or maze monster, or a supported named entry. Consult the constructors for valid IDs. [Yew](../src/Sanctuary.Content/YewEncounter.cs) · [Hedge](../src/Sanctuary.Content/HedgeEncounter.cs) |
| `GraveyardSkeleton` · `GraveyardZombie` · `GraveyardLich` | Custom cemetery creature types. `[Graveyard` takes a GM to the Britain cemetery test site in Felucca. [Creature types](../src/Sanctuary.Content/GraveyardUndead.cs) · [Command](../src/Sanctuary.Content/GraveyardPopulation.cs) |

## Placeable templates

For camps and the hoard, target the center after entering the command, or pass `x y [z]` on the current facet. Omitting Z uses the surface height. Check the footprint before placing against walls, roads, low ceilings, or another encounter.

| Command or fixture | Places |
| --- | --- |
| `[OrcCamp` | Scenery with six Cove orc spawners (four warriors, a scout, a shaman, all level 18–20). The template clearing is outside Cove at `(2337, 1222, 0)`. The orcs can drop bounty ears. [Implementation](../src/Sanctuary.Content/OrcCamp.cs) |
| `[OrcCampDeco` | The same grouped scenery without spawners. [Implementation](../src/Sanctuary.Content/OrcCamp.cs) |
| `[EttinCamp` | Animated bonfire, skinned deer, hides, four level 15–20 ettins, and one level 18 boulder thrower. [Implementation](../src/Sanctuary.Content/EttinCamp.cs) · [Bonfire art](../art/world/ettin-camp/README.md) |
| `[OgreCamp` | The hunting-camp scenery with four to six level 24–28 ogres. [Implementation](../src/Sanctuary.Content/EttinCamp.cs) |
| `[DragonHoard` | Grouped nest and ground scatter. [Placement](../src/Sanctuary.Content/DragonHoard.cs) · [Template](../data/Sanctuary/dragon-hoard-template.json) |
| `[Add DragonHoardChest` | Standalone hoard reward chest. Stocks immediately and refills 10–30 minutes after emptying. One roll of gold, a potion, or equipment. [Chest](../src/Sanctuary.Content/DragonHoardChest.cs) |
| `[MarkHouse` | GM survey gump for authoring a town housing footprint and its placement data. [Housing](../src/Sanctuary.Content/Housing.cs) |
| Sanctuary travel fixtures | Double-click stairs, route sparkles, ferry points, and story gates. Use the relevant installer for quest access and arrival Z. See the campaign routes, Covetous stairs, and Britain sewer ladder. [Campaign stairs](../src/Sanctuary.Content/CampaignDungeonRoutes.cs) · [Covetous](../src/Sanctuary.Content/CovetousPassages.cs) · [Sewers](../src/Sanctuary.Content/BritainSewerLadder.cs) |
| Public facilities and resurrection refuges | Authored civic/vendor placements and public workshop or shrine locations. [Town authoring data](../data/Sanctuary/Balance/town-markets.json) · [Ankh refuges](../src/Sanctuary.Content/AnkhRefuges.cs) |

Deleting a grouped camp removes its props, owned spawners, and their creatures. Treat associated standalone loot and unguided GM spawners as separate placements.

## Bundled populations and scenery

| Area | Placement files |
| --- | --- |
| New Haven | [Encounters](../data/Sanctuary/new-haven-encounters.json) · [Decor](../data/Sanctuary/haven-decor.cfg) · [Doors](../data/Sanctuary/haven-doors.cfg) |
| Britain | [Cemetery](../data/Sanctuary/graveyard.json) · [Britain decor](../data/Sanctuary/britain.cfg) · [Vendors](../data/Sanctuary/vendors.json) |
| Frontiers and southern Britannia | [Frontier spawns](../data/Sanctuary/britannia-frontier-spawns.json) · [Frontier decor](../data/Sanctuary/britannia-frontier-decor.cfg) · [Compassion frontier](../data/Sanctuary/compassion-frontier-spawns.json) · [Southern Britannia](../data/Sanctuary/southern-britannia-spawns.json) |
| Cove and the orc forts | [Cove](../data/Sanctuary/cove-spawns.json) · [Cove fort](../data/Sanctuary/cove-fort-spawns.json) · [West orc fort](../data/Sanctuary/west-orc-fort-spawns.json) |
| Vesper | [Coast](../data/Sanctuary/vesper-coast-spawns.json) · [Graveyard](../data/Sanctuary/vesper-graveyard-spawns.json) |
| Dungeons and maze | [Despise](../data/Sanctuary/despise-spawns.json) · [Covetous](../data/Sanctuary/covetous-spawns.json) · [Hedge Maze](../data/Sanctuary/hedge-maze-spawns.json) |

Use stable GUIDs for bundled spawners. For existing saves, check whether the installer updates an existing GUID’s position or counts and handles removed GUIDs. Test both initial installation and reload when changing a population. [Spawner installer](../src/Sanctuary.Content/GraveyardPopulation.cs) · [Data layout](../data/Sanctuary/README.md)

### Heartwood

The Yew passage opens directly for elves and via the mayor’s permission for others. Heartwood has shopkeepers, a banker, a healer, named residents, and Nightwind’s authored role. Its services and characters have separate placements. [Residents](../src/Sanctuary.Content/HeartwoodTown.cs) · [Passage](../src/Sanctuary.Content/YewStory.cs) · [Nightwind](../src/Sanctuary.Content/Nightwind.cs)

## Dungeons

Dungeon encounters use players 5 for life, experience, and treasure-table NoDrop odds. Sanctuary applies the same scaling to native dungeon creatures through their existing stat and reward profiles.

Give monsters their spells by name. Spell damage comes from the encounter’s level-based damage roll, with per-hit tuning for attacks such as Hydra bolts and Firestorm patches. Monster spell damage has no skill-tree synergy requirements. The monster’s level also supplies effect formulas for properties such as curse radius and duration. Auras use their own level curves. Specify an individual effect only when the encounter calls for an exception.

- **New Haven crypts.** Four saved Hythloth-art layouts, drawn randomly by Jens Won’s shovels. Each has a small empty arrival room and return rope, three differently themed combat rooms, and four-wide corridors. Each combat room draws two Haven ghost encounters, each corridor one. Eligible rooms roll a 50% chest chance, with 50–100 gold. Visits remain cleared until a fresh visit refills a vacant crypt. [Layouts](../data/Sanctuary/HavenCrypts) · [Layout builder](../tools/build_haven_crypts.py)
- **Static layout trials.** Use the seedable Hythloth layout tool to draft standalone treasure or utility maps. Review the floor plan and the complete props in game before installing the fixed decoration file. [Local workflow](local-development.md#dungeon-layout-trials)
- **Treasure expeditions.** Deciphered maps open owner-specific timed expeditions from authored room masks with randomized connections. Their monsters, layout and treasure persist through leaving and reloading. [Treasure rules](items-and-crafting.md#treasure-expeditions)
- **Campaign arenas.** Use the named GM destinations to inspect the three arenas. Author access from the last floor, verify red gate eligibility and return routes, and keep ordinary populations separate from campaign boss state. [Campaign](campaign.md#progression-and-arena-access)

## Dialogue and journal

Give a speaking NPC their own portrait with eyes meeting the player’s gaze. Reuse the art when they return unless an authored change of age or mood calls for a variant. Trainers also get their own artwork. Chain different speakers into separate dialogue pages with each speaker’s portrait. Write durable discoveries and permissions to the character’s journal when the interaction grants them. Keep shared world facts distinct from personal story and companion state. [Portrait asset guide](../art/player-ui/npc-portrait-prompts.md) · [Dialogue and journal protocol](../shared/PortraitProtocol.cs)

Use first-person journal entries that reflect what this particular Avatar learned. Let books, people, places, and choices unlock lore in any order. Reveal a connection when the player has found both sides. An NPC can refer to what they saw, what the Avatar told them, or what they heard as rumour. Preserve those sources when branching dialogue.

Scripted dialogue is the established path. Optional model-assisted conversations, personal NPC variants, and voiced localization are planned work. Keep authored discoveries and transactions server-controlled so both conversation paths can use the same state. [Roadmap](roadmap.md#local-play)
