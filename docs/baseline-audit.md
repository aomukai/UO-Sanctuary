# Baseline audit

17 October 2026. Source inspection, Linux compilation, asset installation, server account/save checks, and client startup checks. Andy also confirmed desktop login and entry into the game world. Detailed interaction and character/inventory persistence checks remain outstanding. Exact source pins and run commands are in [the README](../README.md).

## Reuse findings

Paths below are relative to `upstream/ModernUO/` unless marked ClassicUO. Existing support means source was found, not that the crossover's acceptance checks passed.

| System | Source evidence | Proposed treatment and gap |
| --- | --- | --- |
| Login and accounts | `Projects/UOContent/Accounting/AccountHandler.cs`; `Misc/AccountPrompt.cs` within UOContent | Keep. Local owner creation and account authentication/server-list response passed before and after restart. Andy confirmed desktop login and entry into the game world. |
| Character creation | `Projects/UOContent/Engines/Character Creation/CharacterCreation.cs` | Keep baseline; extend with class choice after a playable room. |
| Saves | `Projects/Server/World/World.cs`, `Load()` and `Save()` | Keep. World snapshots and owner-account reload passed. Character/item round trip still requires gameplay verification. |
| Inventories, banking, equipment | `Projects/Server/Items/Container.cs`; `Projects/UOContent/Mobiles/Townfolk/Banker.cs`; `Items/Weapons/BaseWeapon.cs` within UOContent | Keep baseline. Combat properties and class requirements need adaptation. |
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

The shallow server checkout could not calculate Nerdbank version height. Local Debug builds use its documented disabled-Git-engine option; release version stamps are not valid. The exact submodule SHA remains the source identity. No upstream gameplay or server engine code was changed.

Client startup initially failed with `InvalidOperation_CannotRegisterSecondResolver`: `ClassicUO.DllMap.Init()` and the pinned FNA's `Microsoft.Xna.Framework.FNADllMap` module initializer both registered a resolver for FNA. The tracked client patch defers to modern FNA on JIT builds while retaining the legacy path for other builds. Client rebuild and initialization passed after the patch. Native AOT publishing has not been tested.

The initial generated owner password had 24 characters, but ClassicUO's login password control accepted only 16. The UI silently truncated it and saved the truncated prefix, causing `BadPass`; the original protocol-only check bypassed that control. The second tracked patch raises the password control's limit to the packet's 30-character capacity and logs credential-free login-state transitions. The account password was preserved, the complete saved password was restored, and actual-client authentication passed.

The project drive is exFAT. Downloading tens of thousands of temporary chunks there was slow; RAM-backed staging completed the installation, and finished archives were copied to the project drive. Interrupted disk staging and temporary RAM files were removed. The downloader's source commit/hash and installation provenance are recorded in ignored `.local/uo-installation.json`.

Staff characters are assigned Green Acres on Felucca by `CharacterCreation.GetStartLocation()` at `(5445, 1153, 0)`. Andy reported appearing in an empty staff area, consistent with this default. The shard's dynamic world population and decorations have not been generated. `[Go Britain` is supported by `Commands/Handlers.cs`; a small populated town area is needed before banker/vendor interaction checks can be completed.

Movement, combat timing, equipment, banking, actual sound output, and character/inventory save/restart checks remain unverified. No class, combat room, new economy system, NPC language service, or public shard has been implemented or deployed. No population or performance target has been verified.

## Next smallest experiment

Prepare a small populated Britain test area, then complete the README's interact–equip–bank–save–restart check before introducing class combat. Use `[Go Britain` to leave the staff starting area. Then build the isolated combat-room prototype and compare server timing with recorded client feedback.
