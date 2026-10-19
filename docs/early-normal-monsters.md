# Early Diablo II Normal monster baseline

The graveyard uses ordinary `skeleton1` and `zombie1` records from vanilla Diablo II 1.13, expansion Normal, one player. These are regular monsters, without champion, unique, boss, difficulty, or multiplayer multipliers. `EarlyNormalMonsters.cs` keeps the two melee variants and AI settings; `GraveyardProfiles` supplies the resulting levels and combat ratings. The lich stays out of the test population and retains its prototype profile.

## Source records

Primary extracted tables: [MonStats](https://github.com/fabd/diablo2/blob/master/code/d2_113_data/MonStats.txt), [MonLvl](https://github.com/fabd/diablo2/blob/master/code/d2_113_data/MonLvl.txt), [MonStats2](https://github.com/fabd/diablo2/blob/master/code/d2_113_data/MonStats2.txt). Level scaling follows [D2MOO MonsterTbls::DATATBLS_CalculateMonsterStatsByLevel](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Common/src/DataTbls/MonsterTbls.cpp): multiply the level's expansion (`L-`) base by the monster's percentage, then divide by 100 with integer truncation.

| Record | Level | L-HP | HP % min/max | L-TH | TH % | L-AC | AC % | L-DM | A1 damage % | A2 damage % | L-XP | XP % |
|---|---:|---:|---|---:|---:|---:|---:|---:|---|---|---:|---:|
| skeleton1 | 2 | 9 | 86/129 | 12 | 101 | 12 | 84 | 3 | 34/101 | 34/101 | 40 | 86 |
| zombie1 | 1 | 7 | 101/181 | 8 | 101 | 6 | 84 | 2 | 51/151 | 101/151 | 30 | 111 |

| Monster | Rolled life | AR | Defense | A1 damage | A2 damage | Base XP |
|---|---|---:|---:|---|---|---:|
| Skeleton | 7–11 | 12 | 10 | 1–3 | 1–3 | 34 |
| Zombie | 7–12 | 8 | 5 | 1–3 | 2–3 | 33 |

Both have 0 physical/fire/cold/lightning resistance and 50 poison resistance. Their 5% monster critical chance doubles physical damage; an ordinary hit can therefore become 2–6 damage. Critical handling follows [D2MOO MonsterMode::D2GAME_MONSTER_ApplyCriticalDamage](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Game/src/MONSTER/MonsterMode.cpp). Life is rolled once at creation and the actual maximum is saved. XP above is the base payout; [the balance foundation](balance-foundations.md) applies solo character-level penalties. Parties and multiplayer scaling remain deferred. Existing player XP and levels are preserved.

Both records have `DamageRegen=2`. [Monster initialization](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Game/src/MONSTER/Monster.cpp) sets fixed-point recovery to `((rolledLife << 8) * 2) >> 12` per game frame; one life is 256 units and a frame is 40 ms. Thus a monster rolled at 7 life has zero recovery after truncation; rolls of 8–12 recover one life per 10.24 seconds. Recovery pauses while poisoned or returning home and does not accrue long inactive-sector gaps. Native UO regeneration stays disabled.

## Attacks and decisions

The animation records were decoded directly from the legacy [AnimData.d2 fixture](https://github.com/OpenDiablo2/OpenDiablo2/blob/master/d2common/d2fileformats/d2animdata/testdata/AnimData.d2) (SHA-256 `e3a54b9ab71df7ccdf3655bbba5868c9d53cd8716b0aad25e8c3d5621579cecb`). `MonStats2.BaseW` selects `1hs` for skeletons and `hth` for zombies.

| Animation | Frames | Speed | Action flag index | Hit time | Animation end |
|---|---:|---:|---:|---:|---:|
| SKA11HS | 16 | 224 | 10 | 480 ms | 760 ms |
| SKA21HS | 16 | 224 | 9 | 440 ms | 760 ms |
| ZMA1HTH | 16 | 256 | 9 | 360 ms | 640 ms |
| ZMA2HTH | 16 | 256 | 9 | 360 ms | 640 ms |

The 8-bit animation counter advances by Speed each 40 ms game frame. Hit time is `ceil(actionIndex * 256 / speed) * 40`; animation end is `ceil(frames * 256 / speed) * 40`. This follows [D2MOO SUnit::sub_6FCBCE70](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Game/src/UNIT/SUnit.cpp). Each attack then has the Normal `aidel=15` neutral delay (600 ms), scheduled by `MonsterMode::sub_6FC64310`. Minimum attack-start intervals are therefore 1,360 ms for skeletons and 1,240 ms for zombies.

The [legacy skeleton/zombie AI](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Game/src/AI/AiThink.cpp) supplies the decisions:

- Skeletons attack on 75% of melee decisions, otherwise idle for 15 frames (600 ms). Given an attack, they choose A1 75% of the time and A2 25%.
- Zombies attack whenever a valid melee decision is ready, choosing A1 20% and A2 80%.

The server checks hit chance and damage at the action time, revalidating target, map, range, height, line of sight, and attacker origin. Retries return the remaining cooldown rather than starting or extending an attack. Pursuit stops through the animation and neutral delay. Native UO low-health fleeing is disabled for these two records. The existing client animation-duration protocol paces the UO attack art to the D2 animation duration; the neutral delay is separate from the visual attack.

## Saved worlds and adaptations

Saved default maxima of 24 life (old skeleton) or 44 life (old zombie) are migrated deterministically into the new life range, preserving the current health fraction and serial/ownership. Repeated loading does not reroll life. Native damage and resistance seeds are corrected on load. Explicit practice targets such as the 60-life combat dummy remain intact. Pending attacks and offline recovery are not restored.

The established 24-tile/12-second leash, three-second home pause, spawn roster, corpse/loot handling, and guardian stats remain. Movement is still mapped onto the UO grid at 320 ms/tile for skeletons and 960 ms/tile for zombies; zombies retain the user's slow walking behavior. AI approach/pathfinding, [player hit recovery](hit-recovery.md), poison status effects, and rewards beyond base XP are separate project systems; this update establishes the standard physical combat records and attack timelines, rather than a complete port of every Diablo II subsystem.

The subsequent [reaction/approach improvement](monster-reactions.md) removes native notice latency for these two wild species. [Common creature profiles and monster hit recovery](creature-profiles.md) add separate recovery metadata; the neutral pause after an attack is not hit recovery.
