# Character attributes and graveyard combat

Class characters use four attributes: Strength, Dexterity, Energy (native Intelligence storage), and separately persisted Vitality. The expanded player status window keeps its existing artwork and layout. Its four left-column rows spend one point per left click; a plus appears while points remain. HCI's bottom-left cell is now Vitality. Tooltips identify the attributes and effects. The old stat-lock controls are absent, and UO skill use cannot award class attributes, including crafting.

Each level after level 1 grants five attribute points and one talent point. Unspent points equal the earned budget minus saved allocations; clients cannot submit stat values, point balances or levels. The existing `AdvanceLevel` API awards skipped levels correctly. GM command `[ClassLevel <higher level>` exercises level rewards. Graveyard kills now award XP through the normal player flow, with one skill point and five stat points per earned level. The command remains a development aid. See [progression and pets](death-and-pets.md).

## Class values

Starting attributes and resource growth follow Blizzard's [class references](https://classic.battle.net/diablo2exp/classes/) and [attribute rules](https://classic.battle.net/diablo2exp/basics/characters.shtml). These are the original seven-class profiles.

| Class | Str | Dex | Vit | Energy | Life | Stamina | Mana | Life/level | Stamina/level | Mana/level | Life/Vit | Stamina/Vit | Mana/Energy |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Amazon | 20 | 25 | 20 | 15 | 50 | 84 | 15 | 2 | 1 | 1.5 | 3 | 1 | 1.5 |
| Sorceress | 10 | 25 | 10 | 35 | 40 | 74 | 35 | 1 | 1 | 2 | 2 | 1 | 2 |
| Necromancer | 15 | 25 | 15 | 25 | 45 | 79 | 25 | 1.5 | 1 | 2 | 2 | 1 | 2 |
| Paladin | 25 | 20 | 25 | 15 | 55 | 89 | 15 | 2 | 1 | 1.5 | 3 | 1 | 1.5 |
| Barbarian | 30 | 20 | 25 | 10 | 55 | 92 | 10 | 2 | 1 | 1 | 4 | 1 | 1 |
| Druid | 15 | 20 | 25 | 20 | 55 | 84 | 20 | 1.5 | 1 | 2 | 2 | 1 | 2 |
| Assassin | 20 | 20 | 20 | 25 | 50 | 95 | 25 | 2 | 1.25 | 1.5 | 3 | 1.25 | 1.75 |

Fractions are retained in quarter-point arithmetic and rounded down only after adding level and attribute contributions. Validated converted equipment supplies Energy, Vitality and additive life/mana/stamina bonuses; native UO bonuses are suppressed. Spending adds only the gained resource capacity to current pools, preserving prior deficits. Level advancement fills pools. Save loading clamps resources without filling them. UO's 150 effective-stat limit is bypassed for class characters; allocation is limited by the level-99 earned budget.

Converted gear uses explicit normal D2 base damage, defence, Strength/Dexterity and level requirements, speed and attribute damage coefficients. Strength retains UO carrying capacity. Weapon-local enhanced damage and flat damage apply to the base; off-weapon flat damage and enhanced damage combine with the source stat coefficients. Physical damage honours the target's physical resistance. Teeth remains a prototype magic attack and does not use physical Attack Rating.

Base Attack Rating is `5 * Dex - 35 + class bonus`, with Amazon/Druid +5, Sorceress −15, Necromancer −10, Paladin/Barbarian +20, Assassin +15. Add flat affix AR before percentage AR. Defence is `floor(Dex / 4)` plus active converted equipment defence, including shields. Cosmetic extra UO slots contribute nothing. Hit chance is `2 * AR/(AR+Defense) * attackerLevel/(attackerLevel+defenderLevel)`, bounded to 5–95%. Shield blocking uses the D2 base/class/Dexterity/level formula, capped at 75%. Accepted running steps disable avoidance; standing/walking restore it. These checks bypass UO combat skills. Elemental resistances come from implemented affixes and cap at 75%; white material armour grants none. No dungeon immunity variants are added.

The status window shows these actual damage/AR/defense values, available points, character level, and equipment IAS/FCR. It renames Energy resistance to Lightning while retaining the native resistance channels and caps. Physical resistance is separate from Defense Rating. FHR is explicitly marked pending; unused reagent/spell bonus cells show `--`. Luck remains a labeled UO equipment value rather than pretending to be Diablo magic find.

## Saves and protocol

`SanctuaryCharacters` version 2 appends four allocation counts before the existing talent data. Versions 0 and 1 migrate with zero allocations and the full level-earned budget. Native base attributes are synchronized to class bases plus allocations on creation/load/spending; names, appearance, equipment, class, XP, level and talents are retained. Previous prototype 30/30/30 values are replaced by class bases, rather than treated as earned points.

Extended packet `0xBF/0xF5`, version 4, carries only a query or an attribute identifier and monotonic sequence from client to server. Replies contain authoritative derived values and class coefficients. The server rejects malformed, replayed, invalid, dead-character or over-budget requests and rate-limits polling/spending. The client does not optimistically alter values. Resource maxima use optional content hooks; unrelated legacy characters preserve their existing rules. No server-engine files are modified.

## Graveyard encounter

Felucca's Britain graveyard has two stable hostile spawners: three skeletons and two zombies, respawning after 45–75 seconds. The lich is removed from this test population until world building; its implementation remains available. Installation also deletes the retired lich spawner from existing saves, including its tracked lich wherever it chased. The healer is unchanged. GM travel command: `[Graveyard`. `sanctuary.graveyard.enabled=False` suppresses installation in disposable test worlds. `[CombatTest` also uses the new skeleton profile, with frozen 60-life practice targets.

Skeleton and zombie combat values now use the [early D2 Normal baseline](early-normal-monsters.md). The lich remains a prototype for later world building:

| Enemy | Level | Life | AR | Defense | Melee damage | Attack cycle | Movement step | Physical/Fire/Cold/Poison/Lightning resistance |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Skeleton | 2 | 7–11 | 12 | 10 | 1–3 | 1360 ms minimum, plus AI stalls | 320 ms | 0/0/0/50/0 |
| Zombie | 1 | 7–12 | 8 | 5 | A1 1–3 / A2 2–3 | 1240 ms | 960 ms | 0/0/0/50/0 |
| Lich | 5 | 90 | 50 | 20 | — | 1200 ms casting | 360 ms | 10/20/40/80/10 |

Wild project undead abandon pursuit beyond 24 tiles from their saved home, or after 12 seconds without reaching attack range and line of sight. They cancel pending attacks, walk home, and pause there for three seconds before acquiring another enemy. Attacks do not pull them back into a chase during the return. A blocked return falls back to the home position after 60 seconds. Returning does not heal them; skeleton guardians retain their pet behavior. The leash home survives saving and loading. Zombie movement remains the same slow 960 ms/tile in and out of combat.

On gaining a level, the client shows an eight-second **LEVEL N!** heading below the combat readout. **Stats +N** and **Talents +N** buttons open the expanded status and Class talent windows. Each button remains while its corresponding points are unspent, including after relogging, and disappears when those points are spent. The existing level-up chat message, effect and sound remain. Initial login with unspent points shows the buttons without replaying the level-up heading. Stat protocol version 2 adds the server-authoritative talent-point count.

The three types have explicit levels, life, AR, defense, resistances, damage and speeds. Their melee rolls and damage bypass UO combat skill/stat scaling. Skeleton and zombie attacks land on their D2 action frames, then finish the animation and a separate 600 ms AI pause. They revalidate range/height/LOS/origin, roll normal critical hits, and use the baseline AI attack choices described above. Lich uses melee AI for target acquisition and movement at a 10-tile preferred range, with its own spell controller rather than UO random mage spells or reagents. Firebolt starts within 12 tiles, releases after 480 ms, and deals 6–10 fire damage after a short distance-based flight. It currently follows its selected target; straight-line projectile interception and a broader monster roster remain future work. Stock unconverted creatures retain a temporary compatibility conversion (level 1, AR from Dexterity, native armor defense) when interacting with class combat. Skeleton guardians also have explicit Diablo profiles. Vendors and other creatures retain the temporary conversion.

A lich prioritizes an eligible undead corpse within eight tiles, with an 800 ms revival windup and eight-second revival cooldown. It checks the body again at resolution, including harvest/consumption state and a valid spawn location. Liches do not resurrect other liches, players, pets, ordinary UO corpses, or harvested/consumed bodies. A local cap of six living revived undead bounds encounter density. Death/deletion cancels pending effects; no in-flight action is restored on loading.

Corpses store persistent kind bits and `SanctuaryNoRewards`. First death awards its normal loot, then marks the corpse's rewards as already granted. Revival copies this state to the creature's serialized `Raised` flag and native `NoKillAwards`. Subsequent deaths produce no loot and remain revivable indefinitely: the flag carries across every creature/corpse generation and save/reload. The XP pipeline honors `NoKillAwards` and credits a controlled pet’s killing blow to its owner.

A successful revival marks its source corpse Animated and Consumed. If unclaimed equipment remains, it stays accessible in that corpse under its original ownership/decay rules. The consumed body cannot be harvested or reused, and disappears when emptied. An empty source body disappears immediately. The revived creature's next death leaves a new eligible body. This supports later shattering, Corpse Explosion, player resurrection and corpse-denial mechanics; those ability effects are not implemented here.

## Verification

Isolated checks cover all seven classes, five points per level, skipped levels, allocation exhaustion, hit/miss rolls, resource fractions, attributes above 150, disabled UO stat gains, stat scaling, saved points, talents and migrations from both earlier profile formats. Existing combat/timing/movement/hitbox/loot checks use deterministic combat rolls and current damage ranges rather than the former flat-six prototype damage.

The real ClassicUO client opens the expanded status, spends each attribute, verifies derived resource pools, displays IAS/FCR, and rejects further spending after points run out. Graveyard checks exercise melee windup, Firebolt release/flight, repeated resurrection, reward suppression, a harvest race, spawner idempotency, and saved creature/corpse/reward state. Four save/reload boots preserve town object identities. Native client test windows run only in Xvfb.
