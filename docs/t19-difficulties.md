# Nightmare and Hell monster variants

Implemented, verified and deployed, 20 October 2026 JST. The user requested Nightmare/Hell monster versions with matching loot, and confirmed **create variants now; place them later**. Existing world populations keep their current difficulty. This extends T19's reward system; T20 and T35's encounter grades/world placements remain separate cards.

## Spawn identity and combat

`MonsterVariants` provides a persistent per-creature difficulty. Unrecorded and existing creatures remain Normal. A new supported wild creature can be assigned Nightmare or Hell only while still on the internal map, before taking damage or entering the world. The identity cannot be changed after spawning, including through a save/restart or map transfer. Native vendor, controlled and summoned instances cannot be assigned. Instance-level no-awards flags are preserved and still prohibit rewards.

Staff can use `[SpawnVariant Nightmare Skeleton` or `[SpawnVariant Hell Dragon`, then target a walkable point within 12 tiles. This requires GameMaster access. World-authoring code can call `MonsterVariants.Spawn(type, difficulty, location, map)`, or assign a fresh creature before its own spawn path. Native types without a parameterless constructor require their ordinary factory/constructor arguments before assignment; the generic command does not invent those arguments. No population is installed by these APIs.

`creature-difficulties.json` covers all 324 reward-mapped types through the existing 233 counterpart groups. It preserves the reviewed per-difficulty level and XP assignments. Pinned expansion MonStats percentages and the matching MonLvl columns at that reviewed level determine life, primary/secondary damage, AR and defense. Life uses the source maximum, within the native 65,000 cap. Source resistances, drain/chill metadata and complete source fields are retained. Normal stat application is unchanged. Explicit authored per-type difficulty overrides still apply last.

Native AI, attack timelines, movement, special abilities and equipment/status modifiers retain their existing implementations. This is a numeric difficulty conversion, not a completed port of every D2 ability. Project graveyard skeleton/zombie primary and secondary melee, and the lich's existing firebolt, use the selected difficulty's numeric damage while retaining their current timelines. Raised project undead inherit the raiser's difficulty and keep the existing spent-life/no-awards rules. Native equipment and resistance modifiers can adjust source base resistances.

The new scoped AOS content hook clamps each elemental component separately and permits zero damage against an immune higher-difficulty target. This prevents the native one-point floor from leaking damage through an immunity, and prevents a resistance over 100% from cancelling damage in another channel. Normal and non-project damage delegation remain unchanged. Native server engine sources are unchanged.

`SanctuaryMonsterVariants` stores serial/difficulty identities. Deleted records are pruned. On reload, the selected numeric profile is applied without filling an injured creature's life; no item rolls or reward claims are reset. It stores no player data and does not relabel existing spawns.

## Difficulty-specific loot

The original Normal gold/gear catalogues remain byte-for-byte reproducible. Separate `creature-gold-nightmare.json`, `creature-gold-hell.json`, `gear-drops-nightmare.json` and `gear-drops-hell.json` use the same pinned LoD source commit and current UO mappings. Runtime selection reads the victim's saved difficulty, not its map, killer level or a level-band guess. XP, gold amount, gear item level and base selection use that same creature identity.

For Nightmare/Hell, the source's treasure-class upgrade advances within the same source group while the next class's required level does not exceed the monster's reviewed level. It applies only to expansion non-boss/non-noRatio counterparts. Normal keeps its original class. See pinned [selection caller](https://github.com/ThePhrozenKeep/D2MOO/blob/80169009eea696f83d949cc3a6f1bc9302f2fd53/source/D2Game/src/ITEMS/Items.cpp) and [group upgrade](https://github.com/ThePhrozenKeep/D2MOO/blob/80169009eea696f83d949cc3a6f1bc9302f2fd53/source/D2Common/src/DataTbls/MonsterTbls.cpp). Tables and algorithm hashes remain recorded in the source manifests.

| Difficulty | Reachable treasure classes | Base pools | Highest pool | Reachable mapped Normal / Exceptional / Elite bases |
| --- | ---: | ---: | ---: | --- |
| Normal | 104 | 34 | 39 | 75 / 33 / 0 |
| Nightmare | 140 | 62 | 66 | 75 / 73 / 30 |
| Hell | 149 | 87 | 87 | 75 / 74 / 74 |

The base counts in this table exclude directly selected rings/amulets. All difficulties share 225 mapped base identities and 529 UO appearances; extra appearances do not multiply a source base's probability. Source selection still has one terminal draw per eligible life, shared by gold and gear. NoDrop, unmapped bases, non-gear branches and successful **Set/Unique** outcomes remain empty without rerolls or fallback. Source quality ratios, MF diminishing returns, ordinary white subqualities, hidden Magic/Rare drops and existing equip/identify/resale/salvage rules are unchanged. Future Set/Unique identity catalogues can consume their reserved quality results without redistributing today's probabilities.

## Reproduction

After fetching the existing pinned sources:

```sh
python3 tools/build_creature_gold.py --source-dir <source> --difficulty Nightmare --check
python3 tools/build_creature_gold.py --source-dir <source> --difficulty Hell --check
python3 tools/build_gear_drops.py --source-dir <source> --difficulty Nightmare --check
python3 tools/build_gear_drops.py --source-dir <source> --difficulty Hell --check
python3 tools/build_creature_difficulties.py --source-dir <source> --check
```

Regenerate gold projections before the numeric profiles and gear projections when intentionally changing source mappings. All ordinary treasure classes in this bounded roster still have one positive pick and zero quality factors; unsupported semantics fail extraction.


## Verification

All 21 Python checks pass, including exact reproduction of all three loot catalogues, independent source group/level upgrade checks, complete higher-profile coverage and supported/unmapped branch accounting. The 34-effect/24-pool/1,536-roll affix audit is refreshed. Server, content and integration builds report zero warnings/errors. The 26 ModernUO patches / 53 paths and 23 ClassicUO patches / 36 paths reproduce and reverse exactly.

Disposable integration checks validate numeric identity for each reviewed higher variant with an ordinary parameterless native constructor; types needing arguments retain data but are skipped by the generic spawn factory. They verify source base resistances separately from native equipment/status modifiers, real fire immunity and mixed physical/fire damage, the project melee numeric profile, pet-owner XP and gold, native Hell death to Elite loot, repeated death callbacks and empty Set/Unique results. Saved injured project creatures, difficulty, level/XP, loot and paid claims survive four boots. Staff command targeting uses a native LandTarget and creates the selected variant; post-spawn reassignment, vendors and owned creatures are rejected.

The actual ClassicUO Hell flow exercises a mouse kill, hidden Elite drop, corpse-grid pickup, identification/equip and tool salvage. The installed-client Nightmare flow exercises the corresponding Exceptional drop. Identification/equip in these fixtures use native server actions; the mouse kill, loot and salvage confirmation are client interactions. Both retain the verified MF panel total. Normal legacy combat, creature XP/profile, pets, resurrection, belt, economy and item-state migration checks pass.

One early persistence assertion observed ordinary native healing by one point. The injury fixture now uses the project's non-regenerating undead so it can require exact saved life; no production regeneration was changed. Early fixture corrections also used a broader legal metal-weapon seed search and the native LandTarget protocol.


## Deployment

Full rollback backup: `.local/backups/pre-t19-difficulties-20261020-050811`. Deployment hashes and checks are in `.local/logs/deploy-t19-difficulties/deployment.json`. Source and installed cumulative suites pass four save/restart boots, including the Hell/installed Nightmare client paths. Both live boots save successfully; the first shuts down cleanly and the second remains running on 127.0.0.1:2593.

All 60 protected files survive staging. All 34 non-save configuration/client/list payloads, including 13 client preferences, remain byte-identical after the live restart. Pre/source/staged/post-live comparisons retain both characters and their progression/skills/rights, tracked gear/material records and all 485 gold stacks. Server engine/logger and client binaries are unchanged; matching application/UOContent/project content and catalogues are installed. Production loads only `UOContent.dll` and `Sanctuary.Content.dll`; no fixture assembly/world is installed. Existing merchants and civic objects are not duplicated.

The new live variant persistence is version 0 with **zero assigned creatures**, confirming that this deployment adds no Nightmare/Hell population and leaves existing creatures Normal. Future staff/world-authoring spawns opt in explicitly. The client is closed and ready to launch.
