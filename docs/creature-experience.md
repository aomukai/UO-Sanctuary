# Common creature kill experience

18 October 2026. Kill XP now reads the common creature profiles rather than a graveyard-only type check. The existing D2/LoD level 1–99 thresholds, solo level-gap adjustments, level-70+ reduction, integer truncation and level-99 cap are unchanged.

## Reviewed roster and source calculation

Every common type now has an explicit reward policy: **324 reward-bearing types and 406 zero-reward exclusions**, covering all 726 built-in types plus four project types. The [complete roster](creature-reward-roster.md) records each counterpart, level, difficulty reward and rationale. Domestic/ambient wildlife, service and quest NPCs, hirelings, summon implementations, scripted copies and helpers have explicit zero entries. Champion/peerless encounters remain reserved for manual dungeon-boss profiles; no D2 act-boss reward or multiplier is imported.

Ordinary hostile families use suitable D2 analogues: orcs → Goatmen, liches → Unravelers, ophidians → Claw Vipers, giant spiders → Arachnids, plant brutes → Thorned Hulks, and fire dragons → Megademons. These are authored comparisons based on combat role, durability, abilities and relative species tier; they are not assertions that native UO abilities match D2. Larger variants have explicit later levels, rather than all dragons inheriting one reward.

| UO type | D2 analogue | Normal level | Base XP |
| --- | --- | ---: | ---: |
| Skeleton | Skeleton | 2 | 34 |
| Zombie | Zombie | 1 | 33 |
| Orc | Night Clan Goatman (`goatman2`) | 6 | 72 |
| Ogre | Brute family (`brute4`), adjusted level | 12 | 168 |
| Lich | Unraveler (`unraveler1`) | 15 | 252 |
| Drake | Balrog (`megademon1`) | 24 | 566 |
| Dragon / SerpentineDragon | Venom Lord (`megademon3`) | 30 | 1,001 |
| GreaterDragon | Venom Lord, adjusted level | 36 | 1,504 |
| AncientWyrm | Venom Lord, adjusted level | 40 | 1,971 |
| ShadowWyrm | Venom Lord, adjusted level | 43 | 2,415 |

The generator uses the pinned LoD tables: `floor(MonLvl[level].L-XP[difficulty] * MonStats[counterpart].Exp[difficulty] / 100)`. `Exp` is a percentage, not the payout. For example, the Dragon's Normal/Nightmare/Hell values are `659 * 152 / 100 = 1,001`, `10,044 * 130 / 100 = 13,057`, and `49,836 * 130 / 100 = 64,786`, each truncated. The unadjusted Megademon rewards agree with Blizzard's [Megademon bestiary](https://classic.battle.net/diablo2exp/monsters/act4-megademon.shtml). Source tables: [MonStats](https://github.com/fabd/diablo2/blob/45112569deb9384738ccafe5c24ebbb71f41c7c9/code/d2_113_data/MonStats.txt), [MonLvl](https://github.com/fabd/diablo2/blob/45112569deb9384738ccafe5c24ebbb71f41c7c9/code/d2_113_data/MonLvl.txt); existing `sources.json` and `d2-creature-fields.json` retain their hashes.

All three reward variants are populated independently. Authored ordinary levels remain within Normal 1–43, Nightmare 36–68 and Hell 67–85. For a counterpart with an adjusted Normal level, the initial Nightmare/Hell assignments use the same offset from its source levels, capped at 68/85 and floored at 36/67; the resulting explicit triples are saved in the rules and can be tuned independently. These are species-level starting points, not D2 area-level scaling. Some top Hell variants share level 85 and therefore share the same family reward. Future dungeon area levels, elite/champion bonuses, party sharing and player-count scaling are separate work. The existing tutorial graveyard lich retains its level 5 / 200 Normal XP as the single explicit legacy comparison exception; Nightmare/Hell use its Unraveler source formula.

## Runtime contract and level consistency

`CreatureProfiles.Experience(creature)` resolves the Normal `rewards` policy. `rewards.enabled` explicitly opts into a reviewed reward **independently of the combat variant's `enabled` and `applyStats`**. Thus a Dragon now pays its reviewed XP while its broader combat conversion remains disabled. `rewards.enabled: false` or explicit zero XP pays zero. An enabled policy requires a level, non-null nonnegative XP and source/rationale metadata. Custom subclasses inherit their nearest known definition; otherwise they use the disabled native fallback with null XP. UO fame, karma and hit points are never substituted for XP.

Reward-bearing profiles supply an explicit `rewards.level`. `CreatureProfiles.Level` / `ClassStats.Level` use that same level for class-combat hit calculations and XP penalties, replacing the old level-1 fallback on ordinary native monsters. This is an intentional combat-level change: an advanced native monster now has the appropriate level advantage. It does not replace native HP, attack rating, defense, damage, resistances, spells, breath, speed, AI or recovery. The owner/skill-based level of the project SkeletonGuardian still comes from its existing implementation. A fully applied numeric combat profile's level takes precedence, and startup rejects a conflicting active combat/reward level.

Native combat values remain only partly converted, so these are provisional progression rewards, not a claim that all 324 types have D2 encounter difficulty or equal XP per second. In particular native randomized life, paragon enhancements and powerful native spells remain. Play tuning should compare time/risk against the authored level and adjust the relevant species profile; increasing HP alone is not an automatic XP multiplier. Existing populations remain Normal. The [T19 extension](t19-difficulties.md) adds persistent Nightmare/Hell spawn identities and matching numeric profiles/XP/loot. Passing a difficulty explicitly to `Snapshot` or `Experience` still only previews it; omitted arguments use the saved spawn identity. Population placement remains later work.

Author families and type assignments in `creature-reward-rules.json`, then run `tools/build_creature_profiles.py` using the pinned inputs described in [creature profiles](creature-profiles.md). The generator writes `creature-rewards.json` without touching gear catalogs. Duplicate, stale or missing type assignments, invalid ordinary level bounds, invalid source monsters and accidental boss references fail generation. Startup separately requires one policy for every catalog type and validates reward metadata and nonnegative XP. Adding a new upstream creature therefore requires an explicit policy instead of silently inheriting zero or a generic high reward.

The loader merges the difficulty reward policy after that difficulty's combat template and before its per-type override. Override `variants.Normal.rewards.experience` (and `level` when appropriate) on the existing `uo-creatures.json` row for a local type adjustment. Use `rewards.enabled: false` to disable its XP. Set Nightmare/Hell reward overrides explicitly; their generated policies are independent of Normal. The generator preserves authored native **and project** variant overrides. [T19](t19-gear-drops.md) consumes shared gold/gear treasure draws; the [difficulty extension](t19-difficulties.md) adds the corresponding Nightmare/Hell catalogues.

## Kill credit and a spent life

`ClassLoot` calls `CreatureExperience.Award` from the existing native creature-death callback, before the native base handler deletes the creature and clears ownership. The native last-killer result supplies credit. If the killer is a pet or summon, its controlling/summoning player is resolved through the existing `ClassLoot.Killer` path. No XP is split with the pet or awarded to an unrelated nearby player. Party sharing and player-count multipliers remain outside this pass.

A recipient must be a living, undeleted class player on the corpse's map and within the existing 18-tile XP sight radius. Killing a player, controlled pet, summoned/animated creature, vendor or `NoKillAwards` creature does not grant class XP. Raised project undead are excluded explicitly as well.

The death must supply that creature's own monster corpse. Its existing serialized `SanctuaryNoRewards` flag is set before checking recipient eligibility or the configured reward. This records that the life has spent its reward opportunity, including deaths with no player credit, zero XP, an out-of-range owner or a level-99 recipient. Repeated calls cannot claim it later.

The project undead death override still records its revival kind, while the shared reward service now owns the spent flag. Existing lich revival copies that flag into the revived creature's `Raised`/`NoKillAwards` state. `Raised` persists and restores `NoKillAwards` on load, and every later death marks its new corpse spent again. Thus both repeated revival and save/reload retain suppression. Native animated summons are also excluded. Future revival implementations must carry this spent-life state into their replacement creature; merely constructing a new ordinary mob is a new spawn, not a tracked revival.

No save format, Server-engine code, ModernUO patch, client code or network protocol changes are needed. Existing corpse flags and character progression persistence are reused. This service runs only on creature death, with no new timer or global world scan.

## Verification

The focused integration check drives actual native Skeleton, Zombie, Orc, Drake and Dragon deaths through the normal callback. It verifies level-matched Orc/Drake payouts, pet-owner credit for a Dragon despite its disabled combat conversion, and the level-1 recipient's reduced Dragon payout of 20 rather than 1,001. It checks that native Dragon life, damage and fire resistance stay unchanged while reward levels become active. The updated checks also verify that inspecting enabled Nightmare/Hell variants leaves an unassigned Normal dragon unchanged. Separate higher-variant checks exercise actual spawn identity, rewards and reloads.

Existing checks still cover controlled/summoned killer ownership, tutorial rewards, excluded victims, vendor exclusion, remote-owner and non-class/NPC credit, repeat claims, zero/disabled reward overrides, high-level and level-gap reductions, the level-99 cap, and the 500-XP threshold with five stat points and one talent point. Two actual timed lich revivals, a saved live revival, spent corpse flags and later reloads retain no-double-XP behavior. The legacy migration fixtures intentionally retain only their original seven class profiles on later boots, so XP-player persistence is checked before those migrations; later boots check spent claims separately.

Python authoring checks cover exhaustive unique assignments, vendor/champion exclusions, all three variants, level bounds, increasing difficulty rewards, the exact source percentage calculation and rejection of missing, duplicate and boss assignments. Generator output is checked for determinism and preservation of per-type Normal/Hell overrides in temporary directories.

Reproduce after building content and integration, without rebuilding DLLs while a disposable server maps them:

```bash
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_CREATURE_XP=1 \
python3 tests/check_britain.py
```

The final combined regression passed: common XP/revival persistence, creature recovery, pets, normal/graveyard combat, resurrection, gear/origin/class progression and four boot/save/shutdown cycles. Content and integration builds passed with zero warnings/errors, and all eight Python helper/authoring tests passed. Generator determinism, native/project override preservation, unchanged curve/progression source and unchanged gear/potion catalogs were verified. Logs: `.local/logs/creature-roster-combined-tests.log`, `creature-roster-combined-server.log`, `creature-roster-helper-tests.log` and the corresponding content/integration build logs. No client code changed; these checks exercise real server deaths and callbacks rather than claiming every species was played manually.

## Earlier shared-XP installation

Installed after a successful live save and clean shutdown, with backup `.local/backups/pre-creature-xp-20261018-175214/`. All 91 protected save/configuration/client-preference files remained byte-identical during installation. Live reload/save passed with the same town identities and 75 progression/configuration/preference payloads preserved; only the standard Accounts.idx save-start timestamp changed. No character/XP reset, integration assembly, test account or fixture was installed. The client remains closed and unchanged.

Deployment record: `.local/logs/creature-xp-deploy.json`. Server log: `.local/logs/server-creature-xp.log`. This earlier installation connected shared XP. The subsequent roster pass above supplies the remaining reward policies; broader native combat conversions remain separate work.

## Reviewed-roster installation — 18 October 2026

Installed the bounded content/data update after a fresh save and clean shutdown. Backup: `.local/backups/pre-creature-roster-20261018-181328/`. All 91 protected files were byte-identical during installation. Live reload/status/save then passed with the same town identities and all 75 progression/configuration/client-preference payloads preserved; only the normal Accounts.idx save-start timestamp changed. Native server/UOContent and client binaries remain byte-identical. Installed content and all catalog files match the built source outputs. No test assembly, test account, progression reset or gear change was installed. The server is running; the client remains closed.

Deployment record: `.local/logs/creature-roster-deploy.json`; server log: `.local/logs/server-creature-roster.log`. The reward roster and common levels are ready for play comparison; full native encounter-stat conversions remain provisional as described above.
