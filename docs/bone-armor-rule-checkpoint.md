# Bone Armor current absorption qualification

Reference: **D2R Reign of the Warlock3.3.93847**,approved deviations first. [Proposed absorption rule](bone-armor-rule-proposal.md);[current evidence](../tools/reference/bone-armor-rule-native-evidence.json). **Policy adoption remains pending.** Existing learned Bone Armor stays available; no new gate was added.

## Fixed and qualified

Native AOS armor-ignore damage previously entered the finite-physical-shield hook only for Ward or other special contexts. Bone Armor alone therefore failed to activate that hook. It now activates the existing hook and absorbs its physical component before Ward,without applying resistance or modifying the nonphysical remainder. Existing unshielded routes retain their behavior. This is a producer correction using the existing pool helper,no new damage or event engine.

The rest of the proposed policy retains existing mechanics:conventional FP256 breaking-hit overflow,supported typed shield/mana order,group/snapshot/recast/lifecycle rules and legacy persistence. Modern event22 overflow and global ordering remain unavailable;the older branch's different breaking-hit result is explicitly recorded rather than presented as current D2R behavior.

## Verification

**Content2/Integration2/snapshot2/native2 passes36markers,six connected real Bone Armor right/quick casts,three saved boots and two reloads.** Both builds have zero warnings/errors. The matching prebuild snapshot binds all current source,data and loaded assemblies.

- New controls verify fractional,exact and breaking pool boundaries;actual AOS armor-ignore physical/mixed hits with original Shaftstop;typed Energy-before-Bone-before-resistance ordering;poison exclusion and partial-mana exhaustion.
- A supported rank-zero legacy Energy Shield capture is used for stored-overlap controls,not a new learned cross-class skill. The original owner,injured Bone Armor metadata,Energy Shield deadline,fractional life and mana survive both reloads. Live mitigation is exercised after load,then the exact inspected original state is restored without refilling or extending it.
- Existing Bone Armor capacity/source,learned windup/payment,cancellation,actual AOS,gear/hard synergy,snapshot/recast/group/lifecycle and new/legacy saves remain. Existing Psychic Ward,Cyclone Armor and encounter shield producers also pass.
- Six actual right/quick casts verify the current self-cast wire route with natural windup and once-only payment. This is a socket/protocol fixture,not a graphical-client or account-handshake test.

The initial C1/I1/N1policy baseline also passed36markers and two reloads before the distinct native armor-ignore gap was found. Its exact sources,assemblies,inputs and logs remain in `before-armor-ignore`;it is not reported as a failed run or substituted for the corrected C2scope. One retained encounter fixture's stale unavailable-Ward assertion was reconciled with the already-approved learned Ward route. No old compiler/supplier/protected-reference matrix was rerun.

The generated Wall/Prison remaining descriptions now match their approved world rule. Only those two descriptive fields changed in the status data;handler counts,verification flags and skill identities did not change. The reconciliation was folded into this required build rather than triggering a separate runtime regression.

## Limits and preservation

The native armor-ignore hook exposes only total/physical data;full Energy Shield/Cyclone handling of its unqualified elemental contexts is not claimed. Modern global event priority,all special/PvP/recipient producers,class certification and graphical comparison remain separate. The proposed policy requires an explicit adoption decision;this checkpoint does not label conventional overflow as a current-executable observation.

Coverage remains236/240handlers,Necromancer30/30,no class certified. Blood Oath,Engorge,Blood Boil and Consume proposal/evidence pairs stay frozen. Installed protection retains106artifacts,38save files and20configuration files. No installed deployment,migration,new client/kernel build,timer,profile or artwork occurred. Final shield effects/audio and tree/hotkey icons remain separate placeholders.
