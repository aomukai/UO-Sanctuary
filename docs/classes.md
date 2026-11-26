# Classes and talents

Sanctuary implements eight classes, 24 trees, and 240 talents (210 active, 30 passive) for learned player abilities in the supported PvE contexts. D2R Reign of the Warlock 3.3.93847 is the mechanical reference. Sanctuary supplies persistent-world rules for movement, targeting, equipment, and summons. Character sex changes the presentation of a class name where applicable, such as Warlock / Witch. [Talent definitions and prerequisites](../data/Sanctuary/talents.json) · [Source and binding data](../data/Sanctuary/Balance/class-skills.json)

## Combat and bindings

Allocate talents with **T**, assign them with **S**, and cast quick skills with **F1–F12**. The skill itself determines available left, right, and quick bindings. Passives take effect from their learned rank. Hold and release an attack or channeled skill to use its applicable input rules. Ground-targeting assistance depends on the ability. Weapon skills use their applicable attack/contact rules. Direct physical spells can still use spell hit rules, as Raven does.

Life, mana, stamina, defence, resistances, recovery, attack speed, and cast speed come from the character’s class, attributes, and active gear/skill modifiers. Breakpoints apply where the underlying D2-derived action uses them. [Combat dispatch](../src/Sanctuary.Content/ClassCombat.cs) · [Class stats](../src/Sanctuary.Content/ClassStats.cs) · [Action timing](../src/Sanctuary.Content/ActionTiming.cs)

### Races

- **Elf.** Adds 20 base mana to class resources and raises the normal lightning resistance ceiling from 75% to 80%. Perception reduces ordinary hostile detection distance by three tiles. Elves have racial night sight and Heartwood access through Yew. [Resources](../src/Sanctuary.Content/ClassResources.cs) · [Resistance and wounds](../src/Sanctuary.Content/NamedCombat.cs) · [Heartwood access](../src/Sanctuary.Content/YewStory.cs)
- **Gargoyle.** Flight uses mounted speed and toggles with **F** by default. Natural life regeneration is 3 HP/s. Master Artisan adds five points to the Imbuing cap, up to 125 with the full unlock. Deadly Aim adds five percentage points of Open Wounds to eligible ranged weapon hits. Mystic Insight raises the ordinary resistance ceilings to 80%. Gargoyle throwing weapons use bow-like attacks. [Resistance and wounds](../src/Sanctuary.Content/NamedCombat.cs) · [Gargoyle weapons](../src/Sanctuary.Content/GargoyleBows.cs)
- **Human.** The racial abilities book lists Strong Back, Tough, Workhorse, and Jack of All Trades. The full per-race play verification is tracked in the [roadmap](roadmap.md#combat-classes-and-companions).

## Summons and companions

- Barbarian and Ronin story companions use the standard companion equipment rules, which permit heavy armour.
- Class summons follow the applicable persistence, travel, and targeting policy. Necromancer summons park in safe areas. Druid, Warlock, Valkyrie, and Assassin shadows can accompany their owners in town. Necromancer minions and other pets use shared recovery hooks where applicable. [Safe-area policy](../src/Sanctuary.Content/MinionSafeAreas.cs)
- **Druid Raven** acts as a flock that attacks one target together, with rank-dependent count. Ground casts choose a nearby eligible hostile. It deals physical spell damage on its cadence without an attack hit roll, keeps working if its target stops or flees, and has no collision. Spirit Wolf, Dire Wolf, and Grizzly use the allied path recovery used for mine geometry. The summon names and visuals use the timber wolf, dire wolf, and grizzly bodies respectively. [Druid Raven](../src/Sanctuary.Content/DruidRavenCasting.cs)
- The hotbar shows counts for Necromancer skeletons, Druid ravens and wolves, and Warlock pets. The pet stats gump can also be opened with `[Pets`. The [player guide](player-guide.md#companions) covers story companions and the dockable vitals window. [Pet stats](../src/Sanctuary.Content/SkillBindings.cs)

### Supported play

The class verification scope covers ordinary learned PvE actions, supported monster types, and the qualified gear and world interactions. The game applies Sanctuary terrain, line of sight, ownership, and save rules. PvP edge cases, unsupported item-proc combinations, and specialized target types need separate qualification.

Iron Golem requires an eligible identified metal original and checks its equipment effects before committing it. Revive restores an eligible original monster with its own abilities, leaves loot on the corpse, and retains the monster’s captured combat effects. Both enforce ownership and eligibility. Ordinary UO creatures need a supported classification before their behaviors are included. The [roadmap](roadmap.md#combat-classes-and-companions) tracks the living monster conversion.

## Talent index

Use **Ctrl+F** for a talent. Hover over a talent in-game to read its description and see its prerequisite chain, the talents that improve it, and the talents it improves. For handler coverage, icons, and audiovisual entries, see the [class reports](../tools/reference/README.md#class-reports).

### Amazon

| Tree | Talents |
| --- | --- |
| Javelin and Spear Skills | Jab, Power Strike, Poison Javelin, Impale, Lightning Bolt, Charged Strike, Plague Javelin, Fend, Lightning Strike, Lightning Fury |
| Passive and Magic Skills | Inner Sight, Critical Strike, Dodge, Slow Missiles, Avoid, Penetrate, Decoy, Evade, Valkyrie, Pierce |
| Bow and Crossbow Skills | Magic Arrow, Fire Arrow, Cold Arrow, Multiple Shot, Exploding Arrow, Ice Arrow, Guided Arrow, Immolation Arrow, Strafe, Freezing Arrow |

### Sorceress

| Tree | Talents |
| --- | --- |
| Cold Spells | Ice Bolt, Frozen Armor, Frost Nova, Ice Blast, Shiver Armor, Glacial Spike, Blizzard, Chilling Armor, Frozen Orb, Cold Mastery |
| Lightning Spells | Charged Bolt, Telekinesis, Static Field, Lightning, Nova, Chain Lightning, Teleport, Thunder Storm, Energy Shield, Lightning Mastery |
| Fire Spells | Fire Bolt, Warmth, Inferno, Blaze, Fireball, Fire Wall, Enchant, Meteor, Fire Mastery, Hydra |

### Necromancer

| Tree | Talents |
| --- | --- |
| Summoning Spells | Raise Skeleton, Skeleton Mastery, Clay Golem, Golem Mastery, Raise Skeletal Mage, Blood Golem, Summon Resist, Iron Golem, Fire Golem, Revive |
| Poison and Bone Spells | Teeth, Bone Armor, Poison Dagger, Corpse Explosion, Bone Wall, Poison Explosion, Bone Spear, Bone Prison, Poison Nova, Bone Spirit |
| Curses | Amplify Damage, Dim Vision, Weaken, Iron Maiden, Terror, Confuse, Life Tap, Attract, Decrepify, Lower Resist |

### Paladin

| Tree | Talents |
| --- | --- |
| Defensive Auras | Prayer, Resist Fire, Resist Cold, Resist Lightning, Defiance, Cleansing, Vigor, Meditation, Redemption, Salvation |
| Offensive Auras | Might, Holy Fire, Thorns, Blessed Aim, Concentration, Holy Freeze, Holy Shock, Sanctuary, Fanaticism, Conviction |
| Combat Skills | Sacrifice, Smite, Holy Bolt, Zeal, Charge, Vengeance, Blessed Hammer, Conversion, Holy Shield, Fist of the Heavens |

### Barbarian

| Tree | Talents |
| --- | --- |
| Warcries | Howl, Find Potion, Shout, Taunt, Battle Cry, Find Item, Battle Orders, Grim Ward, War Cry, Battle Command |
| Combat Masteries | Blade Mastery, Axe Mastery, Mace Mastery, Polearm Mastery, Throwing Mastery, Spear Mastery, Increased Stamina, Iron Skin, Increased Speed, Natural Resistance |
| Combat Skills | Bash, Double Swing, Leap, Double Throw, Stun, Leap Attack, Concentrate, Frenzy, Whirlwind, Berserk |

### Druid

| Tree | Talents |
| --- | --- |
| Elemental | Firestorm, Molten Boulder, Arctic Blast, Fissure, Cyclone Armor, Twister, Volcano, Tornado, Hurricane, Armageddon |
| Shape Shifting | Werewolf, Lycanthropy, Werebear, Maul, Feral Rage, Fire Claws, Rabies, Shock Wave, Hunger, Fury |
| Summoning | Raven, Poison Creeper, Oak Sage, Summon Spirit Wolf, Carrion Vine, Heart of Wolverine, Summon Dire Wolf, Solar Creeper, Spirit of Barbs, Summon Grizzly |

### Assassin

| Tree | Talents |
| --- | --- |
| Martial Arts | Tiger Strike, Dragon Talon, Dragon Claw, Fists of Fire, Cobra Strike, Dragon Tail, Claws of Thunder, Blades of Ice, Dragon Flight, Phoenix Strike |
| Shadow Disciplines | Claw Mastery, Psychic Hammer, Burst of Speed, Cloak of Shadows, Weapon Block, Fade, Shadow Warrior, Mind Blast, Venom, Shadow Master |
| Traps | Fire Blast, Shock Web, Blade Sentinel, Charged Bolt Sentry, Wake Of Fire, Blade Fury, Lightning Sentry, Wake Of Inferno, Death Sentry, Blade Shield |

### Warlock

| Tree | Talents |
| --- | --- |
| Demon | Demonic Mastery, Summon Lesser Daemon, Blood Oath, Death Mark, Summon Succubus, Blood Boil, Summon Defiler, Engorge, Consume, Bind Demon |
| Eldritch | Levitation Mastery, Hex: Bane, Cleave, Echoing Strike, Hex: Purge, Blade Warp, Psychic Ward, Eldritch Blast, Hex: Siphon, Mirrored Blades |
| Chaos | Miasma Bolt, Ring of Fire, Sigil: Lethargy, Sigil: Rancor, Miasma Chain, Flame Wave, Sigil: Death, Enhanced Entropy, Apocalypse, Abyss |
