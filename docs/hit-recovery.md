# Player hit recovery and monster reactions — 18 October 2026

Monster recovery is now implemented separately through the [common creature profiles](creature-profiles.md), alongside the [species approach improvement](monster-reactions.md). Player behavior below remains unchanged by that addition.

This is a bounded on-foot class-player recovery prototype alongside the [pursuit and weapon-reach comparison](melee-reach.md). Physical impacts from the project graveyard undead can interrupt movement and an unspent attack/cast effect. This is distinct from attack windup range checks and from the time an enemy takes to notice or approach you.

## Verified Diablo II rules

D2MOO revision `0c0bbd1e6bef9181a3da1c046523af622a925416`, [SUnitDmg::sub_6FCC1870](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Game/src/UNIT/SUnitDmg.cpp#L2162), filters recovery by damage and hit class. It excludes pure poison and less than one point of damage; hit classes can use different divisors. The Normal skeleton and zombie [MonStats2 records](https://github.com/fabd/diablo2/blob/master/code/d2_113_data/MonStats2.txt) have hit classes 3 and 12. In the [hit-class definitions](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Common/include/D2DataTbls.h), those are one-hand swing versus large and claw. Both enter the damage helper's **default divisor 16**, rather than its special divisor-8/32/64 branches.

For those two physical attacks, the resulting probabilities are:

| Actual damage from one hit, relative to maximum life | Recovery chance |
| --- | ---: |
| Below 1/16 | 0% |
| At least 1/16, below 1/8 | 37.5% |
| At least 1/8, below 1/4 | 75% |
| At least 1/4 | 100% |

The first chance band comes from two sequential filters, not just one 50% roll. The prototype uses a single equivalent eighths roll. Fractional thresholds use cross multiplication against integer UO life; they do not truncate `maximumLife / 16` to a whole life point first.

[PlrModes::sub_6FC81890](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Game/src/PLAYER/PlrModes.cpp#L1570) rejects another normal get-hit mode transition while the player is already in get-hit mode. Thus another ordinary hit does **not** keep resetting one recovery animation. After it ends, another eligible impact can start a new one. Staggered pack attacks create more opportunities to chain recovery; they do not add several individually subthreshold hits into one qualifying hit. More maximum life also raises the damage threshold. Blizzard's [melee equipment guide](https://classic.battle.net/diablo2exp/classes/meleeequipment.shtml) describes the resulting danger of repeated interruptions and the value of stacked FHR.

Recovery speed follows [Units::UNITS_UpdateGetHitAnimRateAndVelocity](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Common/src/Units/Units.cpp#L1548): effective FHR is `floor(120 * FHR / (120 + FHR))`, and animation advance is `floor(sourceSpeed * (50 + effectiveFHR) / 100)`. The legacy [AnimData.d2 fixture](https://github.com/OpenDiablo2/OpenDiablo2/blob/master/d2common/d2fileformats/d2animdata/testdata/AnimData.d2), SHA-256 `e3a54b9ab71df7ccdf3655bbba5868c9d53cd8716b0aad25e8c3d5621579cecb`, was decoded directly. Human get-hit records have 6 frames for Amazon, 8 Sorceress, 7 Necromancer/Druid and 5 Paladin/Barbarian/Assassin. Most source speeds are 256; Paladin STF/2HT is 192 and Druid 1HS is 248. These are metadata frames, not final game-frame durations.

The prototype's game-frame duration is `max(1, ceil(sourceFrames * 256 / advance) - 1)` at 40 ms per frame. A zero-FHR Necromancer therefore takes **13 frames / 520 ms**, versus a Barbarian's 9 / 360 ms and Sorceress's 15 / 600 ms. Necromancer FHR breakpoints tested are 0/5/10/16/26/39/56/86/152/377 for 13/12/11/10/9/8/7/6/5/4 frames. These are legacy human timings; this is not a port of shape changes, block recovery, knockback, special stun skills or D2R-specific PvP recovery changes.

## Implementation boundary

`HitRecovery.cs` owns the threshold, chance, class timing and session deadline. `GraveyardUndead.Deal` reports the actual life lost after the physical `AOS.Damage` call. Misses, zero damage and lethal hits do not start recovery. Fire/spell damage, poison ticks, stock UO enemies, pets and player-versus-player effects have not been assigned recovery rules by this prototype. Skeleton and zombie physical melee use the verified common divisor above; new monster hit classes need explicit rules before adding them here.

A new optional UOContent `ClassCombatHooks.MovementAllowed` callback rejects player movement while the project deadline is active. It does not edit the server engine or use native Frozen/Paralyzed flags. Existing paralysis remains independent. The shared combat state packet adds the remaining recovery milliseconds (protocol **5**). ClassicUO pauses predicted walking, including keyboard walking, and route replanning during that time. The server still validates every movement request; in-flight prediction uses the existing movement acknowledgement/denial correction. No rendered offset, teleport, speed bonus or movement-credit grant is added.

Recovery cancels the unspent effect and leaves the existing shared action deadline intact. New basic attacks, Teeth and Raise Skeleton are blocked until recovery ends. Held input/selected-target intent remains, so a still-held ground gesture or attack can resume; releasing during recovery discards the gesture. Hits received during active recovery neither extend the deadline nor queue another recovery. Death, logout/restart and client world reset clear transient state. The UO hurt animation is paced to the recovery duration through the existing animation lease.

`HitRecovery.Fhr` reads existing `balance1/2/3` item modifiers for future equipment integration, and the duration calculation is covered across breakpoints. **This change does not enable those affixes in the gear generator or alter existing items/catalogs.** Current ordinary equipment therefore still has zero active FHR. Making FHR gear available and showing its total in the stats panel remains a separate gear step. A change of equipment does not shorten an already-started recovery.

## Reaction time is a separate monster behavior

[D2MOO AiThink::AITHINK_Fn002_Skeleton / Fn003_Zombie](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Game/src/AI/AiThink.cpp#L295) confirms distinct approach decisions as well as distinct melee decisions. The 1.13 Normal skeleton record has 60% approach chance, a 15-frame stall, and 75% melee attack chance. The zombie branches on awareness distance (10), approach chance (30%), and special conditions when choosing how to approach; in melee it does not use the skeleton's attack-or-idle roll. These are not a universal fixed reaction delay, and the post-animation `aidel` is another separate wait.

Our existing `EarlyNormalMonsters` already ports the melee decisions, A1/A2 mix, effect frame and 600 ms neutral delay. `GraveyardUndead.GetSpeeds` still gives these enemies the same 100 ms active / 400 ms passive AI intervals, while native UO acquisition/path following supplies approach. Their approach chances, awareness branches, run/charge choices and individual initial-reaction profiles are **not fully ported**. That is a confirmed source gap; its contribution to a particular observed encounter is not measured here. The slow zombie movement and current population remain unchanged. A future species-behavior comparison should measure first sight → target acquisition → first movement → attack start → impact separately, rather than shortening every monster's attack animation or global AI interval.

## Verification

The focused checks are `HitRecoveryChecks.cs`, the real-timer monster suite, and `RecoveryUiChecks.cs` / `check_recovery_ui.py`. The UI fixture injects deterministic eligible damage events to isolate prediction/input; the connected-client zombie case verifies that an actual zombie melee impact invokes the same recovery path. The focused run passed the connected-client zombie critical, weak-hit movement, 520 ms server lock, repeat-hit non-extension, automatic held-input resumption, two successive recoveries from staggered hits, and release during recovery. First accepted movement after the isolated strong hit was **528 ms** after its start. Peak movement rate was **7 packets/second**, with no remaining movement queue and no accepted translation during a recovery. Screenshots capture the hurt pose and damage number.

The ordinary enclosed-target hold, short-click/release and heavy-equipment movement tests also passed (worst route 4.87 ms; heavy running median 250 ms). The server combat/threshold/class-breakpoint/interrupt/legacy checks and real-timer normal/graveyard monster suites passed. Four disposable save/restart boots preserved town and creation/migration identities. Logs: `.local/logs/hit-recovery-run.log`, `hit-recovery-server.log`, `hit-recovery-client.log`, `hit-recovery-ui.json`; screenshots: `hit-recovery-zombie.png` and `hit-recovery-locked.png`.

All 18 ClassicUO patches reproduce the 31 touched source files, and all 11 ModernUO patches reproduce 25 files from their pinned revisions. Six Python helper tests passed. The UOContent build had no warnings; the client retains its existing upstream warnings. The full combined client combat/timing/loot suite also passed with protocol 5: overlap/body picking, approach and repetition, ground cancellation, Shift swings, move-to-cast transition, held Teeth and release, equipment IAS/FCR, dagger/halberd cadence, death/no retarget, friendly interactions, looting/harvesting and Run/Walk preference persistence. This run completed four more save/restart boots. Logs: `.local/logs/melee-combat-regression.log`, `melee-combat-regression-server.log`, `melee-combat-regression-client.log`.

## Installed comparison

The protocol-5 client, UOContent and project content are installed together. The live server reloaded the existing world and completed a save; town identities stayed unchanged. All four installed assemblies match their build outputs. The desktop client is left closed.

Backup: `.local/backups/pre-melee-recovery-20261018-152038/`; installation record: `.local/logs/melee-recovery-deploy.json`; live log: `.local/logs/server-melee-recovery.log`. All 90 protected save/config/client files were unchanged during installation. After reload/save, 74 protected progression/config/preferences files retained their payloads; the account index changed only its documented v5 save-start timestamp (bytes 4–11). Account data, class progression/bindings, gear rolls and client preferences are preserved. The backup also contains the matching pre-task protocol-4 client runtime for rollback; do not mix protocol-4 and protocol-5 client/content builds.
