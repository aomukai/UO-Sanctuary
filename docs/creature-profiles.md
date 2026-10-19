# Common creature profiles and hit recovery

This is a common data contract for every built-in UO creature, not a completed rebalance of every creature. `uo-creatures.json` covers all **726 concrete BaseCreature types in the compiled UOContent assembly**, including nested/event/quest types and vendors, plus four project types. Startup checks coverage without constructing NPCs. Existing randomized stats, special abilities, ownership, passive behavior and vendor services remain native unless a profile explicitly opts into stat application. Act-boss rules are outside this work; future dungeon bosses should have manual profiles.

## Files and difficulty inheritance

- `data/Sanctuary/Balance/uo-creatures.json`: one identity, role, template, source reference and conversion status per type; optional `variants.Normal`, `variants.Nightmare`, `variants.Hell` overrides.
- `creature-templates.json`: complete shared defaults and species templates. Normal starts with the defaults, then its template, reviewed reward policy and type override. Nightmare/Hell inherit that result, then apply difficulty defaults, template values, that difficulty's reviewed reward policy and type overrides, in that order. Arrays replace; objects merge; explicit `null` restores native delegation. Unknown typed fields fail startup.
- `creature-reward-rules.json` / `creature-rewards.json`: exhaustive authored counterpart, level and exclusion policies / generated players-1 reward variants. See the [full reward roster](creature-reward-roster.md).
- `d2-creature-fields.json`: complete column inventory and hashes for pinned MonStats (255 columns), MonStats2 and MonLvl, including fields that are graphics/behavior metadata rather than statistics.
- `creature-hit-classes.json`: weapon hit classes from the pinned Weapons table. Gear analogues supply the D2 weapon code; unconverted weapons use divisor 16.
- `CreatureProfiles.cs`: typed contract, validated startup resolution, native conversion, variant preview and opt-in stat application. `CreatureProfiles.Snapshot(creature, difficulty)` previews the selected profile and native fallbacks. Its `Profile.Enabled`/`ApplyStats` flags distinguish a proposal from active stats.

All three difficulties have the same fields. Nightmare and Hell default to **disabled**; they are not spawned by the current world. Enabling difficulty content later also requires selecting/persisting its difficulty at spawn. It does not require a new creature implementation. Profiles do not silently reinterpret the existing 1–99 archetype source bands as world difficulty.

The planned world distribution, confirmed on 20 October 2026, is Normal/Nightmare outdoors in Trammel, Hell restricted to Trammel dungeon floor 3, and Hell mobs outdoors in Felucca. Specific hunting areas and dungeon populations remain future world content; current spawns are unchanged. See roadmap T35 and the crafting specification's world difficulty direction.

Example per-type override (inside an existing identity row):

```json
"variants": {
  "Normal": { "hitRecovery": { "fhr": 20 } },
  "Hell": { "enabled": false, "resistances": { "fire": 130 } }
}
```

`enabled` permits a variant's behavior/recovery settings. Reviewed XP has a separate `rewards.enabled` opt-in and explicit level, described in [creature experience](creature-experience.md). `applyStats` separately opts into numeric combat/native-stat overrides. Changing recovery alone does not change life or damage. Reward-bearing native types now use their reviewed level for class-combat hit calculations and XP penalties; this does not enable the larger native stat conversion. Numeric stat application preserves the current life fraction, including injury, rather than healing a live mob to full. Existing pets are excluded from native stat replacement; their owner/skill-based scaling remains intact. Recovery state is transient and weakly keyed by creature, with no extra per-mob timer or save migration.

## Fields and actual runtime support

| Domain | Common fields | Current implementation |
| --- | --- | --- |
| Core | Level, life, defense | Existing project values or native conversion; explicit opt-in values supported. Reward-bearing types use the reviewed common level for class combat and XP. Unrewarded/unmapped native types retain level 1; native AR `max(5,Dex*2)` and defense `max(0,Dex/4+ArmorRating)` remain **provisional conversions**, not a completed D2 stat conversion. |
| Offense | Attack1, Attack2, Spell: damage, AR, reach, cycle, impact, after-attack pause, physical/fire/lightning/cold/poison/magic, critical chance, skill/level, missile | Native/project attacks remain their existing implementations. Opt-in Attack1 damage and native damage proportions, AR and defense are connected. Additional attack timelines, criticals and abilities still need their attack executor. A populated field alone does not create an ability. |
| Defense | Physical, magic, fire, lightning, cold, poison resistance; block | Native five resistances retained; Energy corresponds to lightning. Native opt-in resistances supported. Magic resistance applies to project Teeth when stats are opted in. Block is a separate reserved field. Immunities are percentages >=100, not a lost boolean. |
| Movement | Walk, run, idle milliseconds per authoritative UO step | Native move clocks retained. Opt-in walk/idle values supported; distinct run pace requires an AI that chooses it. Tile collision, doors and elevation remain native. D2 velocity/subtiles are not milliseconds or UO tiles. |
| Reaction | Behavior, notice, scan, approach chance/radius/retry/wander, attack chance/mix, AI delay | Skeleton/zombie notice and approach use the common profile. Their existing melee executor still owns attack mix and timing. Other AI remains native. |
| Recovery | Enabled, GH metadata frames/speed, FHR | Active for eligible creature damage, authoritative movement, standard melee and AI scheduling. Per-species metadata where mapped; explicit provisional 4-frame/256-speed UO fallback elsewhere. Vendors disable recovery; blessed/dead pets cannot enter it. |
| Targeting | Policy, threat, retain-valid-target | Native policy retained. Threat is stored as source evidence, not interpreted as a universal numeric aggro score. See below. |
| Traits/rewards | Family, undead/demon/boss, size, revival/corpse, regeneration, drain/chill effectiveness, experience, treasure class | Explicit enabled `rewards.experience` now supplies [kill XP](creature-experience.md); null/zero/disabled values pay nothing. Pet kills credit the owner and spent corpse/revival state prevents repeated XP. Other traits retain their native/project implementations; treasure classes and broader drop/ability rules remain future work. |

Every variant also has `sourceStats` and `sourceModes` dictionaries. Bound D2 references retain **every original MonStats/MonStats2 column**, including all eight skills, AI parameters/difficulty settings, elemental attacks, animation/mode permissions, flags and treasure-class references. These preserve information beyond the typed runtime subset. Native-only mappings leave them empty. The schema deliberately distinguishes **missing conversion** from a zero value, zero resistance, or immunity.

## Creative analogue: fire dragons

Dragon, Drake, GreaterDragon, AncientWyrm and SerpentineDragon have a **proposed** `megademon3`/Venom Lord reference. White/cold and shadow variants are not silently turned into fire dragons. The combat proposal uses source players-1 level scaling and resistance bounds. The later independent reward policy differentiates Drake (Balrog) from Dragon (Venom Lord) and larger species; snapshot level/reference now describe that selected reward policy, while the raw combat proposal remains accessible on `Profile`:


| Difficulty | Reference level | Life (source maximum) | AR | Defense | Fire resist |
| --- | ---: | ---: | ---: | ---: | ---: |
| Normal | 30 | 305 | 386 | 181 | 70 |
| Nightmare | 57 | 3102 | 1785 | 897 | 80 |
| Hell | 84 | 15167 | 5392 | 1879 | 130 |

The proposed 50% physical / 50% fire primary attack is a **project design choice**, not the Venom Lord's original melee damage composition. Fire resistance and other source stats come from the pinned tables. These rows are disabled for live stat application: UO dragons have native breath/spell calculations that are not bounded by changing melee fields alone. Before enabling them, route breath/spell damage and recovery through the profile, choose the intended attack timeline/movement and test native resistance caps/immunity behavior. This avoids calling an unconverted UO caster “D2 balanced” merely because its life was changed.

## Monster hit recovery

The checked [D2MOO damage routine](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Game/src/UNIT/SUnitDmg.cpp#L2162) uses final damage, maximum life, hit class and monster GH-mode permission. Pure poison does not request get-hit. Ordinary hit classes use divisor 16; source weapon classes also use 8/32/64. At the default divisor, damage below 1/16 life has no chance, below 1/8 has 3/8 chance, below 1/4 has 6/8 chance, and at least 1/4 guarantees recovery. Small hits do not accumulate. The source has additional frozen, stunned and uninterruptible-state handling that this prototype does not port.

GH speed follows [Units::UNITS_UpdateGetHitAnimRateAndVelocity](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Common/src/Units/Units.cpp#L1548). The project uses a full metadata animation cycle: `ceil(frames*256 / floor(speed*(50+effectiveFHR)/100))*40 ms`, where `effectiveFHR=floor(120*FHR/(120+FHR))`. Decoded fixture metadata gives SKGH1HS 4/256 and ZMGHHTH 5/152: **320 ms skeleton / 680 ms zombie** at zero FHR. These are project server cycle durations, not a claim that every D2 mode-end event is reproduced exactly. The animation fixture hash is recorded in the field audit.

Damage hooks run **after actual life loss** in AOS.Damage (including standard UO weapon attacks); project Teeth explicitly invokes the same recovery path. Pure-poison AOS damage is excluded. Native poison ticks and direct `Mobile.Damage` calls bypass this hook; custom direct-damage abilities need an explicit call, as Teeth has. Other custom periodic AOS effects are not yet tagged separately. Special native independently timed abilities also require individual interruption review.

Recovery blocks BaseCreature.Move, standard weapon swings, and both AI decision and continuation wakes. It clears pending movement intent, cancels unspent project undead/pet impacts, and interrupts native spells. The original attack and movement deadlines are retained. Hits during active recovery deal damage but **do not refresh or queue recovery**; that is a bounded project policy. Another eligible hit after recovery may start the next one. Native Frozen/Paralyzed flags are not set or cleared. UO's human/animal/monster get-hit artwork is reused with the existing duration packet; no client/protocol change is required.

## Target selection: confirmed versus open

[AiUtil](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Game/src/AI/AiUtil.cpp) assigns players threat 14 in two callbacks, while pinned MonStats gives skeleton summons 10 and several hirelings/golems 11. Those callbacks split `threat > 1` from fallback targets and compare distance; they do **not** simply sort 14 ahead of 11 ahead of 10. The evil-alignment target-node path also scans player lists and their attached summons, with reachability fallback. [Targets::D2GAME_UpdateSummonAI](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Game/src/GAME/Targets.cpp) is used by hireling setup and multiple summoning skills.

Therefore the observation “players/mercenaries draw more attacks” is plausible but a universal numerical bias is **not established by this audit**. Current UO closest-target ranking and valid-target retention remain unchanged. The common field can retain source threat without pretending it is a complete aggro algorithm. A follow-up comparison should place player, hireling and summon at equal/unequal range, vary existing target/LOS and damage provocation, and trace actual choices before assigning a project preference rule. No act-boss targeting exceptions are imported.

## Verification and reproduction

`CreatureInventory.Configure` exports the compiled type inventory only when `SANCTUARY_TEST_CREATURE_INVENTORY` is set. Rebuild these focused catalogs without touching gear catalogs:

```sh
python3 tools/build_creature_profiles.py \
  --inventory .local/research/uo-creature-types.json \
  --source .local/research/foundation-pinned \
  --animations .local/research/AnimData.d2
```

The generator preserves per-type `variants` overrides and rejects missing, duplicate or stale reward assignments. Add a reward policy when adding a creature type. D2 tables are pinned to fabd/diablo2 commit `45112569deb9384738ccafe5c24ebbb71f41c7c9`; D2MOO source is pinned above. Neither source is treated as proof of complete runtime parity.

Server integration checks cover compiled-type coverage, disabled difficulty/dragon proposals, complete source fields, vendor policy, threshold bands, weak/strong actual damage, non-refreshing recovery, movement and native melee guards, poison exclusion, FHR reduction and cancellation of a pending project attack without refunding its cooldown. Actual client tests and final deployment results are recorded after validation below.

### Validated comparison build — 18 October 2026

- Native UOContent/application, project content and integration builds passed with zero warnings/errors. Twelve ModernUO patches reproduce all 26 touched files from pinned source; the unchanged eighteen ClassicUO patches reproduce 31 files. Repeated application checks and all six Python helper tests passed. No new server-engine or client/protocol edit was required.
- Actual RMB Teeth hit the zombie, displayed native get-hit art, held its authoritative location during 680 ms recovery, then allowed pursuit to resume. Connected player recovery still resumed held Necromancer movement at 522 ms for a 520 ms event; weak/repeated/staggered hits and release remained correct. Held movement made 15 bounded route searches, worst 5.00 ms, with the expected 250 ms heavy-gear run interval.
- Final reaction samples were 4 ms skeleton / 6 ms zombie for notice and first movement, with unchanged median movement intervals. Connected melee impacts finished both approaches.
- The combined **server** suite passed profile coverage/recovery, species approach, normal/graveyard combat, pet ownership/scaling/persistence, resurrection, class combat, gear/origin checks, and four save/restart boots. The full **client** combat/casting/timing/IAS/FCR/loot/harvest/preferences suite passed in a fresh encounter world, followed by four more save/restart boots.
- Combining all persisted pet/graveyard fixtures with the client practice encounter initially interfered with projectile/held-swing expectations. The projectile interception fixture now has its own location; the client encounter is run in a fresh world. This is not evidence that arbitrary surrounding-player/pet target preference has been validated.
- The focused catalog generator is deterministic and leaves existing gear/balance catalogs unchanged. Profile/collision/behavior work remains separate from gear generation.

Evidence under `.local/logs`: `creature-profile-play-{run,server,client}.log`, `creature-hit-recovery-ui.json`, `creature-hit-recovery.png`, `approach-final.json`, `creature-profile-server-regression{,-server}.log`, `creature-profile-combat-ui-{regression,server,client}.log`, and `creature-profile-patch-check.log`. The initial shared-fixture failure is retained in `creature-profile-shared-fixture-{failure,server}.log`.

The server/content comparison build was installed after a successful live save and backup to `.local/backups/pre-creature-profiles-20261018-165142/`. All 91 protected save/config/client files were byte-identical during installation. Client runtime/protocol 5 remains the existing build; the desktop client is left closed. Live reload/save and progression verification are recorded in `.local/logs/creature-profile-deploy.json`; live server log is `server-creature-profiles.log`.

The live world reloaded with the same town identities and completed a save. All four installed assemblies match their build outputs. After reload/save, 75 protected progression/config/preference files retain their payloads; Accounts.idx differs only in its standard v5 save-start timestamp (bytes 4–11). Account data, class/gear progression, mouse bindings and client preferences remain intact.

The later [reward roster pass](creature-experience.md) fills every type's reward policy and activates consistent reward/combat levels. Dragon combat-stat proposals remain disabled even though reviewed Dragon XP is enabled independently.
