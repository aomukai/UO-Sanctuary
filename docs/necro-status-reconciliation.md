# Necromancer remaining-work reconciliation

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. All 30 Necromancer talents have handlers and partial native coverage. No skill or class is promoted to complete. The total remains 191 handlers across 240 talents, with no completed class.

Fourteen generated `remaining` descriptions had fallen behind accepted implementations. This change updates those descriptions and their generated roadmap rows. Registration, verification labels, completion flags, formulas, source IDs, class order and gameplay code are unchanged. The other sixteen Necromancer descriptions retain their existing scope and limits.

| Skill | Correction | Primary accepted evidence |
| --- | --- | --- |
| Raise Skeleton | Automatic world AI/follow/A1 already exists; include source15 corpse-town search/release and wounded-actor reloads. | [World controller](skeleton-world-checkpoint.md), [corpse-town guard](raise-corpse-town-checkpoint.md) |
| Clay Golem | Prepared contact and automatic world AI are implemented; retain uncertainty about exact modern startup, outgoing slow and fallback parity. | [World integration](clay-golem-world-checkpoint.md), [approved upgrades](clay-upgrades-checkpoint.md) |
| Skeletal Mage | Fixed secondary spells, Enchant/Frozen Armor, element migration and historical AI adoption are implemented alongside basics/Mastery/triples. | [Basics](mage-basics-checkpoint.md), [buffs](mage-pet-buffs-checkpoint.md), [elements](mage-elements-checkpoint.md), [historical actors](mage-historical-checkpoint.md) |
| Blood Golem | Exact birth, fractional healing, ordered contact/world behavior and approved damage/Open Wounds/Life Tap have scoped acceptance. | [World integration](blood-golem-world-checkpoint.md), [combat and rounding limit](blood-golem-combat-checkpoint.md), [Life Tap](blood-life-tap-checkpoint.md) |
| Teeth | Saved flight, moving-target/lifecycle checks and delay leases are implemented; exact modern delay identity remains uncertain. | [Saved flight](teeth-checkpoint.md) |
| Corpse Explosion | Include nearby-corpse ground selection, client point routing and separate caster/corpse town validation. | [Ground targeting](corpse-aim-checkpoint.md), [corpse towns](corpse-town-checkpoint.md) |
| Bone Wall | Difficulty resources, maker growth and saved AI expiry clocks are implemented; the source death/removal phase remains provisional. | [Birth/resources](bone-barrier-birth-checkpoint.md), [maker](bone-wall-maker-checkpoint.md), [clock](bone-barrier-clock-checkpoint.md) |
| Poison Explosion | Include shared ground selection and corpse-town guards while retaining its own eligibility and saved clouds. | [Clouds](poison-explosion-checkpoint.md), [ground targeting](poison-corpse-aim-checkpoint.md) |
| Bone Prison | Include difficulty resources, saved clocks and approved ground acquisition; exact raster/terrain/death behavior remains open. | [Birth/resources](bone-barrier-birth-checkpoint.md), [ground targeting](bone-prison-aim-checkpoint.md) |
| Dim Vision | The saved blind controller is wired for mapped Skeleton/Zombie/Megademon families; unsupported families and native world adapters remain explicit. | [Casting](dim-vision-cast-checkpoint.md), [live controller](blind-world-checkpoint.md) |
| Iron Maiden | Fractional pre-clipping reflection, source identity through retirement and supported owner-credit behavior are implemented. | [Curse/reflection](iron-maiden-checkpoint.md), [original NPC contact](npc-iron-maiden-checkpoint.md) |
| Confuse | Learned neutral targeting, autonomous damage, normal expiry and post-load reacquisition/combat already have dedicated checks. | [Casting](confuse-cast-checkpoint.md), [combat after reload](confuse-resume-checkpoint.md) |
| Attract | Independent neighbor target leases, natural damage/expiry and reload behavior are covered; factory failure/minion/credit gaps remain. | [Casting and independent targets](attract-cast-checkpoint.md) |
| Decrepify | Rank/gear/resource/lifecycle/input, four stat consumers and saved effects are verified within their documented scope. | [Dedicated acceptance](decrepify-checkpoint.md), [elite replacement](elite-amplify-checkpoint.md) |

The [evidence receipt](../tools/reference/necro-status-reconciliation-evidence.json) binds the historical receipts and their accepted native logs. Those historical suites overlap and are not added together or presented as fresh runs. Their limits remain authoritative; later prose does not turn an earlier scoped pass into a full current-engine certificate.

Fresh verification uses the accepted `508c3f89` assemblies because all C# and project inputs are byte-identical. The only changed copied runtime file is `class-skill-status.json`, and its only changed fields are the fourteen descriptions. Two runtime audit checks preserve all 573 named decisions with fresh 1018/21 input fingerprints. All 71 Python tests and catalogue/readiness/roadmap generator checks pass. No full mechanics cohort is rerun or newly claimed for this metadata change. Installed artifacts, saves and configuration remain unchanged.

Outstanding work still includes:

- Bone barrier DEATH/DT/hidedead, owner/minion graph, final removal/collision order and exact terrain/footprint projection.
- Specific reference uncertainties such as Blood Golem event23 rounding, Clay outgoing item19 applicability, Summon Resist rounding controls and skeleton shield distribution.
- Unsupported species/recipient/item/proc/Sunder/PvP contexts, native world/room/path/scan/event/seed equivalence and actual graphical-client acceptance.
- Historical phase reconstruction, actual old-binary upgrades, unheld concurrent saves and projectile behavior after caster removal where explicitly unverified.
- Final effects/audio and separate talent/hotkey icons. Existing identified placeholders remain; this change adds no asset or imagegen work.

Next mechanical dependency to review: the deferred player incoming-hit/death bridge. The [Shiver Armor checkpoint](sorceress-shiver-armor-checkpoint.md) records a concrete lethal-hit ordering gap: native death clears armor before late retaliation. Review the existing player defenses and ordered NPC/HP consumers before introducing another damage path. Keep the Necromancer-specific reference gaps above nonblocking for unaffected class work; no new completion claim follows from this reconciliation.
