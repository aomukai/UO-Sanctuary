# Baseline audit

17 October 2026. Source inspection, Linux compilation, asset installation, server account/save checks, and client startup checks. Andy also confirmed desktop login, entry into the game world, working doors, banker interaction, and equipping/unequipping a dagger. Bank item transfers and character/inventory persistence checks remain outstanding. Exact source pins and run commands are in [the README](../README.md).

## Reuse findings

Paths below are relative to `upstream/ModernUO/` unless marked ClassicUO. Existing support means source was found, not that the crossover's acceptance checks passed.

| System | Source evidence | Proposed treatment and gap |
| --- | --- | --- |
| Login and accounts | `Projects/UOContent/Accounting/AccountHandler.cs`; `Misc/AccountPrompt.cs` within UOContent | Keep. Local owner creation and account authentication/server-list response passed before and after restart. Andy confirmed desktop login and entry into the game world. |
| Character creation | `Projects/UOContent/Engines/Character Creation/CharacterCreation.cs`; root `src/Sanctuary.Content/ClassCreation.cs` | Extended: seven Diablo class buttons precede the retained appearance editor. No custom template or skill allocation; the server assigns a saved level-1 class. |
| Saves | `Projects/Server/World/World.cs`, `Load()` and `Save()` | Keep. World snapshots and owner-account reload passed. Character/item round trip still requires gameplay verification. |
| Inventories, banking, equipment | `Projects/Server/Items/Container.cs`; `Projects/UOContent/Mobiles/Townfolk/Banker.cs`; `Items/Weapons/BaseWeapon.cs` within UOContent | Keep baseline. Andy confirmed banker interaction and dragging a dagger onto the paperdoll to equip it, then back to the backpack. Bank item transfers and inventory persistence remain to be checked. Combat properties and class requirements need adaptation. |
| Housing | `Projects/UOContent/Multis/Houses/BaseHouse.cs`; `HouseFoundation.cs` in the same directory | Keep foundation. Town-house ownership and NPC reservations require a separate map audit. |
| Tooltips and properties | `Projects/Server/PropertyList/ObjectPropertyList.cs`; `Projects/Server/Items/Item.cs`, `GetProperties()` | Keep and extend for rarity, class affixes, sockets, and provenance. |
| Damage and resistances | `Projects/Server/Mobiles/Mobile.cs`, `ResistanceType`; `Projects/UOContent/Misc/AOS.cs`, `Damage()` | Five stock channels: physical, fire, cold, poison, energy. Lightning naming and a separate magic channel need deliberate integration; physical rules and minimum damage differ from intended immunities. |
| Materials and professions | `Projects/UOContent/Misc/ResourceInfo.cs`, `CraftResources`; `Engines/Harvest/Core/HarvestSystem.cs`; `Engines/Craft/Core/CraftItem.cs` within UOContent | Reuse infrastructure; audit each recipe, gain, and material bonus before remapping. |
| Imbuing | `Distribution/Data/skills.json`; `Projects/UOContent/Skills/SkillsInfo.cs` | Skill registration exists. Focused searches of skills and crafting did not establish a working imbuing implementation. White-to-rare conversion is not existing support. |
| Repairs | `Projects/UOContent/Engines/Craft/Core/Repair.cs`, `CheckWeaken()` and `CheckRepairDifficulty()` | Adapt. Stock skill rolls and weakening conflict with guaranteed skilled repair and repair-resource consumption. No repair changes made yet. |
| Treasure maps | `Projects/UOContent/Items/Maps/TreasureMap.cs`; `Items/Containers/TreasureMapChest.cs` within UOContent | Reuse decipher/dig foundation; catacomb allocation, ownership, expiry, and duplicate-reward prevention remain new work. |
| Attacks and casting | `Projects/Server/Mobiles/Mobile.cs`, `NextCombatTime`; `Projects/UOContent/Items/Weapons/BaseWeapon.cs`, `OnSwing()` and `GetDelay()`; `Spells/Base/Spell.cs` within UOContent | Extend after observation. Stock weapon skills and spell disturbance are not class progression or Diablo hit recovery. |
| Movement | `Projects/Server/Network/MovementThrottle.cs`; `NetState/NetState.Movement.cs` within Server/Network; `Projects/UOContent/Engines/Pathing/Movement.cs` | Keep validation and movement initially. Server action restrictions must later agree with client prediction and animation. |
| Effects | `Projects/Server/Effects.cs`, `SendMovingEffect()` and `SendLocationEffect()` | Reuse effects APIs. Spiral hammers and orb bursts have not been demonstrated. |
| Controls and targeting | ClassicUO `src/ClassicUO.Client/Game/Scenes/GameSceneInputHandler.cs`; `Game/Managers/MacroManager.cs` in the same project | Existing mouse handling, cast/last-target macros, chat focus, and interactions are integration points. Diablo mouse assignments still need prototype work; right-mouse state currently participates in movement. |
| Parties | `Projects/UOContent/Engines/Party/Party.cs`, `Capacity = 10` | Keep party primitives. Dungeon auto-grouping, larger populations, reward attribution, and errand reservations are not solved by stock party support. |
| AI and followers | `Projects/UOContent/Mobiles/AI/BaseAI/`; `Mobiles/BaseCreature.cs`; `Mobiles/Hireables/BaseHire.cs` within UOContent | Reuse ordinary AI/follower foundations. Diablo mercenary equipment, progression, death, and paid resurrection need design and implementation. |
| Death and insurance | `Projects/UOContent/Engines/Insurance/Insurance.cs`; `Mobiles/PlayerMobile.cs` within UOContent | Reuse where compatible. Mixed insured/uninsured inventories, insufficient funds, and corpse behaviour still need acceptance tests. |
| Expansion coverage | `Projects/Server/ExpansionInfo.cs`; `Distribution/Data/expansions.json`; `Projects/UOContent/Configuration/ExpansionConfiguration.cs` | Expansion definitions include AOS and SA, but flags do not prove content completeness. Neither is locked as the project's era. |

## Implemented and checked

- Initialized the root Git repository and pinned two upstream submodules, including the client's recursive dependencies.
- Installed SDK `10.0.201` under `.tools/`; detected existing ICU, libdeflate, and Argon2 native libraries on Pop!_OS 24.04 x64.
- Added `dev` commands for setup, build, game-data installation, readiness inspection, one-time configuration, and interactive server/client launch.
- Staged Debug binaries under ignored `.local/` while preserving runtime configuration and saves across builds.
- Compiled ModernUO successfully with the local dependency override: zero warnings and errors in the successful build. Compiled ClassicUO successfully with upstream warnings and zero errors.
- Added focused configuration checks for refusing overwrite, rejecting missing data, and keeping local listeners on loopback.
- Restored the kickoff brief from the user's Downloads copy after it disappeared from the workspace. The restoration is byte-for-byte identical to that copy; the cause of disappearance is unknown.
- Installed Classic Client data 7.0.117.1 from the publisher's patch servers using pinned UO Patcher source. Verified 420 game files and 44,200 game archive entries against manifest identities/sizes. Omitted unavailable patch-note web pages.
- Configured loopback-only SA test settings with Felucca and Trammel. Disabled optional startup path pre-baking; recorded the actual client-data version explicitly.
- Created a local owner account, received the expected server-list packet after authenticating, saved the world snapshot, restarted the server, authenticated again, saved again, and shut down cleanly. No character was created during this protocol check.
- Started ClassicUO under Xvfb with software OpenGL and dummy audio. Assets loaded and rendering initialized; this does not verify the user's physical GPU, audio output, or interactive gameplay.
- Verified actual ClassicUO authentication against the running local server after correcting the login field's length. The client progressed through `VerifyingAccount` and `LoginInToServer` to `CharacterCreation` without creating a character.
- Andy subsequently confirmed successful desktop login and entry into the game world, establishing that the installed client/server pair works in-game. He then closed both applications. This confirmation does not establish the individual movement, equipment, banking, or inventory persistence checks.

## Experiments and limitations

The unmodified server failed NuGet restore because a transitive dependency resolved vulnerable `System.Security.Cryptography.Pkcs 10.0.0`. `build/ModernUO.targets` adds the patched `10.0.13` reference to the application and content projects. Auditing was not disabled. The client still reports ImageSharp advisories; its upstream dependency versions were preserved.

The shallow server checkout could not calculate Nerdbank version height. Local Debug builds use its documented disabled-Git-engine option; release version stamps are not valid. The exact submodule SHA remains the source identity. The server engine is unchanged. A tracked UOContent patch now exposes character-creation hooks for the project module.

Client startup initially failed with `InvalidOperation_CannotRegisterSecondResolver`: `ClassicUO.DllMap.Init()` and the pinned FNA's `Microsoft.Xna.Framework.FNADllMap` module initializer both registered a resolver for FNA. The tracked client patch defers to modern FNA on JIT builds while retaining the legacy path for other builds. Client rebuild and initialization passed after the patch. Native AOT publishing has not been tested.

The initial generated owner password had 24 characters, but ClassicUO's login password control accepted only 16. The UI silently truncated it and saved the truncated prefix, causing `BadPass`; the original protocol-only check bypassed that control. The second tracked patch raises the password control's limit to the packet's 30-character capacity and logs credential-free login-state transitions. The account password was preserved, the complete saved password was restored, and actual-client authentication passed.

The project drive is exFAT. Downloading tens of thousands of temporary chunks there was slow; RAM-backed staging completed the installation, and finished archives were copied to the project drive. Interrupted disk staging and temporary RAM files were removed. The downloader's source commit/hash and installation provenance are recorded in ignored `.local/uo-installation.json`.

Staff characters are assigned Green Acres on Felucca by `CharacterCreation.GetStartLocation()` at `(5445, 1153, 0)`. Andy reported appearing in an empty staff area, then confirmed Britain was also empty after teleporting there. Terrain and static buildings alone did not provide the interactions needed for the baseline checks.

The project supplies a separate `Sanctuary.Content.dll`. It installs 330 stock decoration placements, including eight doors, within a small Felucca neighbourhood around West Britain Bank. Three stock vendor spawners maintain Banker/Minter, Provisioner/Cobbler, and Armorer/Weaponsmith pairs. The data is a coordinate/GUID subset of the pinned ModernUO distribution. A saved invisible stock Static marker prevents decoration reinstallation on restart; existing spawners and residents are retained. No custom serialized item/mobile types were introduced. `[SanctuaryTest bank|vendor|smith` provides staff travel to the test locations.

The content build passed with zero warnings/errors. `python3 tests/check_britain.py` booted a disposable world on separate loopback TCP/UDP ports, verified eight doors, three spawners, and six residents, saved and shut down, then booted again and verified identical door/spawner/NPC serials before another successful save/shutdown. It used no player accounts or live saves. This verifies population persistence; actual client interactions and player inventory persistence remain separate checks. The three existing configuration tests also passed.

The launcher installs content at server startup. Server/client builds refuse to replace binaries while the corresponding application is running. Before the initial live installation, the stopped server's saves and configuration were copied to `.local/backups/pre-britain-20261017-163138/`.

Andy confirmed both requested in-game interactions work: operating a door and opening the bank through a banker. This establishes those interactions separately from the automated population checks.

Andy then confirmed dragging the test dagger onto the paperdoll equips it and dragging it back to the backpack works. Double-clicking instead invokes bladed-tool targeting; the earlier instruction to double-click to equip was incorrect. Retaining skinning for hides → Tailoring → light armour bases is now an explicit design requirement. Actual corpse skinning and hide processing have not been tested.

Character creation now starts with Amazon, Sorceress, Necromancer, Paladin, Barbarian, Druid, and Assassin buttons, followed by the existing UO appearance editor and direct world entry. The client sends shared reserved class IDs, and the server validates them before character allocation. New characters have player access, neutral 30/30/30 stats, no UO starting skill allocation, and a per-character class/level/XP record initially set to level 1 / XP 0. Existing characters and owner-account privileges are preserved. The class/level appears on the paperdoll and through `[Class`. These are creation and persistence foundations; class abilities and XP gain are not implemented. Details and reproducible checks are in [character creation](character-creation.md).

Creation integration checks passed for all seven classes, invalid/custom ID rejection, stat/skill sanitization, appearance, starter inventory, deleted-profile pruning, preservation of an existing GM, and saved class/level/XP after restart. The real patched ClassicUO also passed class selection, appearance, Back/reselection, and direct world entry; the server verified the final Paladin choice at level 1. Patch series passed fresh-source and repeated-application checks. The live world was saved and backed up under `.local/backups/pre-classes-20261017-165511/`, then reloaded successfully with the same town object identities and an authenticated owner login.

Walking/running, combat timing, bank item transfers, actual sound output, and the original GM's manual inventory round trip remain separate play checks. No combat room, new economy system, NPC language service, or public shard has been implemented or deployed. No population or performance target has been verified.

## Next smallest experiment

Use the new class identity/progression record for the isolated combat-room prototype: one provisional class, a basic attack, a projectile, AoE, and visible recovery timing. Continue ordinary interaction and persistence checks alongside that work.

## Class talents and trades — 17 October 2026

The paperdoll now routes Class to a server gump with three ten-skill trees for the selected class, and Trade to the retained UO skills window with exactly 13 gathering/crafting/Cartography skills. Both traditional and advanced skills views filter the same shared IDs; old saved group layouts are rebuilt when they contain removed skills. Server skill values and recipes are unchanged. Imbuing remains a listed profession, not a completed crafting implementation.

The 210-skill catalog records Blizzard source URLs. Rank allocation checks class ownership, level and per-rank level gates, prerequisites, a 20-rank cap, and available points on the server. One point is earned per level after level 1. Ability effects and combat XP awards remain pending. Version-1 persistence stores stable skill IDs and reads version-0 profiles.

Integration checks passed rank caps, rejected invalid/cross-class/unaffordable allocations, level gates and prerequisites, save/reload of allocations, and version-0 migration. The actual patched client passed Class tab switching, learning Prayer with a server-verified point debit, and the complete 13-skill Trade view. Town identities survived all three isolated test boots. The patch-series tests cover overlapping patches, repeat application, partial installation, and preserving conflicting local edits. See [talents and trades](class-talents-and-trades.md).

### Paperdoll and skills refinement

Renamed Trade back to Skills. Skills reuses the original blue Skills button art; Class uses the same installed artwork’s rim and clean blue face with its own caption, including hover and pressed states. The skills window title is Skills. Removed Reset Groups, New Group, group renaming/deletion, cross-group drops, the drag cursor/selection highlight, and dragging out floating skill shortcuts (including the advanced view). Sections are rebuilt on login. Kept skill use buttons, lock controls, show-real/show-cap toggles, and section collapsing. No server gameplay or character data changed.

Verification passed in the actual client: Class still opens the correct trees, the restored Skills button opens all 13 professions, dragging Cartography toward Gathering leaves its section unchanged, and dragging Imbuing out creates no floating skill button. The test saved through the client's existing Save Desktop macro and checked the resulting group and desktop XML. Visual checks confirmed blue paperdoll buttons, the Skills title, and removal of editing controls. The client rebuilt with upstream warnings and no errors; all six helper tests and repeated patch application passed. Client settings and actual default-path profiles were backed up to `.local/backups/pre-skills-ui-20261017-173851/` before staging. The live server remained running and wrote a successful save.

## Mouse combat prototype — 17 October 2026

Class characters now use hostile click-to-approach/repeated basic attacks, ground click-to-cancel/move, and right-click ability use. Shift holds position without triggering an action on its own. War mode is hidden/disabled for class players; a UOContent hook routes player attacks and suppresses stock automatic player swings/retaliation, while preserving NPC combat and legacy staff characters. Hostiles are server-classified wild creatures; vendors, neutrals, owned creatures, and players are protected. The server rechecks ownership, range, visibility, line of sight, capability, mana, and cooldown. Target death stops the selected action without retargeting.

Equipped stock daggers grant Necromancers +1 effective Teeth. The grant is derived from equipment and adds to allocated ranks. Teeth uses a direct Magic Arrow missile placeholder with no reagents, spellbook, Magery, or UO spell checks. Mana recovery is provisionally independent of Meditation for class characters. Basic and Teeth damage/range/timing are prototype constants; final ability effects, projectile travel/collision, hit recovery, and combat XP remain work ahead. The class gump distinguishes the active Teeth prototype from inactive skills.

Server checks passed target classification, range, repeat/cancel, shared cooldowns, no automatic retaliation, no replacement target, mana and frozen restrictions, equipment/allocated ranks, zero UO skill gains, and preserved legacy combat. The actual ClassicUO passed approach and repeated melee, ground cancellation/walking, Shift hold, right-click Teeth, removal of the weapon grant, kill/no retarget, and blacksmith context-menu interaction. Three isolated save/restart boots preserved town identities. Tests use disposable worlds, never live player characters. See [combat controls, provisional tuning, practice command, and checks](combat-prototype.md).


## Held mouse controls — 17 October 2026

Left-held ground movement now continuously updates the path destination; Shift pauses movement and releasing Shift resumes it while the mouse remains held. Right-held Teeth repeats on the server's 800 ms cadence. Button release, capability loss, target death, and input lease expiry stop casting. Near and far world aim points describe a direction, with reach limited to eight tiles and line of sight. Starter daggers now show “Necromancer Starting Dagger” and “Teeth +1 (Necromancer only)”; transferred daggers remain usable as melee weapons by Barbarians without granting Teeth.

Server checks, real-client mouse tests, six developer-helper tests, patch application checks, and three isolated save/restart boots passed. The live backup is `.local/backups/pre-held-controls-20261017-183555/`. See [combat controls](combat-prototype.md) for limits and verification details.


## Run/Walk and projectile reach — 17 October 2026

The restored paperdoll button and Tab select Run/Walk. One choice drives click/hold paths, enemy approach, and keyboard movement; the old Always Run option cannot override Walk for class characters. The HUD and paperdoll stay synchronized, and the character's client profile saves the preference. Accepted running steps suppress weapon evasion and parrying; turns, blocked moves, walking, and standing still retain normal defense. NPC/legacy movement is unchanged.

Teeth's provisional range is now 16 tiles instead of 8. Ground-ray obstruction checks use projectile height. Server checks verify a hit at 12 tiles, the 16-tile bound, and no damage through a wall. D2MOO's 1.10f implementation and extracted 1.13 missile/skill data establish speed, lifetime, and Strafe acquisition radius as separate mechanics; relative ranges and source links are recorded in [combat notes](combat-prototype.md). Final Diablo unit conversion and projectile simulation remain tuning work.


## Hostile picking and Focus leak — 17 October 2026

Class combat now uses ordinary pixel picking first, with filled hostile sprite bounds as a fallback. Visible creature pixels beat another creature's fallback; normal depth order resolves opaque overlap, and the front fallback wins transparent overlap. World-space Teeth collision remains independent, using a body/missile footprint and hitting the first eligible body for both unit and ground aim.

The passive Focus gain came from stock stamina regeneration. Class stamina recovery now bypasses Focus, and UO gains are restricted to the 13 retained trade skills for class characters. Mana recovery continues to bypass Meditation. Existing accidental skill values are preserved and no longer affect recovery. See [combat notes](combat-prototype.md) for D2MOO references, provisional shapes, and test details.

Pixel-mask boundary tests also reproduced and corrected rejection of the first opaque pixel in a run; mirrored class selection uses width minus one. This removes two sources of disagreement between drawn bones and precise mouse hits. A minimized paperdoll now hides its Run/Walk button, preventing stray controls from intercepting world clicks.


## Stationary melee — 17 October 2026

Shift + left-click/hold now performs the left-bound action in place, including empty melee swings. Cursor-selected hostiles take damage only within melee reach, line of sight, and height tolerance; empty swings consume the same shared cooldown. Shift release ends the gesture without starting pursuit. Server and actual-client checks passed alongside existing combat/picking and three save/reload boots. Live installation backup: `.local/backups/pre-stationary-melee-20261017-194720/`. See [combat controls](combat-prototype.md).


## Nearby corpse loot — 17 October 2026

Class characters now single-click a monster corpse for one grid combining eligible contents within five tiles, and single-click an icon to take its whole stack. Each item remains in its source corpse until server-validated pickup. Empty unharvested bodies turn grey and remain usable. The native bladed-tool target interaction adds harvesting materials to the body; successfully harvested bodies disappear only after their last remaining item is taken. Retained contents preserve ordinary decay. Player corpses retain ordinary recovery. Eligible kill gold is deposited into the backpack, with capacity failures leaving it in the corpse.

Server and actual-client checks passed five-tile combined looting, tooltips, whole-stack transfer, weight/item capacity failure, ownership/player-corpse boundaries, repeated harvesting, grey empty bodies and removal only after final harvested pickup. Three save/reload boots retained town identities, harvested flags and leftover equipment. Six helper tests and fresh/idempotent patch application checks passed. Live backup: `.local/backups/pre-nearby-loot-20261017-200947/`. See [loot rules and limits](loot-prototype.md).


## Class action timing and weapon speeds — 17 October 2026

Class combat now uses a server-owned windup, one effect/release frame and a full-cycle deadline, with separate class/family profiles and IAS/FCR frame breakpoints. Cancelling or swapping equipment cannot refund a cycle. Weapon bases have explicit modifiers and bounded inheritance/fallback rules across the arsenal. Weapon Speed tooltips now show Diablo-style labels, with current class and cycle seconds when equipped. UO sprite animations receive cycle timing for both the acting client and observers. Existing NPC/legacy combat formulas and saved weapon Speed values are preserved.

Server checks, actual-client cadence/bonus measurements, rendered tooltip inspection, existing control checks and nearby looting/harvesting checks passed. Three isolated save/reload boots preserved town identities, class progression and harvested-body state. Live backup: `.local/backups/pre-action-timing-20261017-204218/`. See [profiles, assignments, formula, and limitations](combat-timing.md).


## Class attributes and graveyard undead — 17 October 2026

Four attributes and five points per level now drive class resource pools and physical combat. The expanded status window spends points with server validation and shows actual damage, AR, Defense, IAS and FCR; Lightning replaces the Energy resistance tooltip. Profile save version 2 migrates both prior versions while preserving classes/talents. Native UO stat gains are disabled for class characters.

Three persistent graveyard spawners maintain three skeletons, two zombies and a lich with explicit Diablo-style monster profiles. The lich casts Firebolt and repeatedly revives eligible corpses. A persistent corpse reward flag carries to revived creatures and their later corpses, suppressing additional rewards. Consumed bodies preserve unclaimed loot until emptied. See [formulas, encounter rules, limitations and verification](character-stats.md).

Installed successfully after all seven-class allocation/resource checks, the actual-client status test, repeated-resurrection/loot suppression/harvest-race tests, and the complete actual-client combat/timing/nearby-loot regression pass. Both integration suites completed four save/reload boots including v1 and v0 migration. Six developer helper tests and fresh/idempotent patch staging passed. The live migration preserved class, level, XP, talents and allocation budget; 32 configuration/profile hashes remained identical. Town serial identities were unchanged. Three graveyard spawners were installed and the world saved successfully. Client left closed. Backup: `.local/backups/pre-stats-graveyard-20261017-212516`.
