# Class skills: implementation versus remaining work

Status: 2026-11-18 JST; current milestones are linked below. Reference: D2R Reign of the Warlock 3.3.93847, with approved Sanctuary deviations first. No charms or runewords.

| Deliverable | Current evidence |
| --- | --- |
| Talent implementation | 240/240 routes across eight classes, including Warlock/Witch; 30 per class |
| Existing mechanical tests | All 240 have scoped native coverage; all eight classes have earlier combined or grouped acceptance evidence |
| Latest consolidated input/lifecycle refresh | All eight classes have current scoped evidence; Warlock/Witch passes906results,26active routes and four separate passive consumers |
| Effects/audio | Separate missing-asset register; identified placeholders remain |
| Talent/hotkey icons | Separate missing-asset register; identified placeholders remain |
| Playable-build handoff | Matching server/client package prepared;original-world copy passes3saves/2reloads with every572mobile/4,698item identity retained;corrected client renders login screen. Promotion remains;real saves/build unchanged |

The old implementation percentage is no longer an estimate of unfinished talents. The [implementation checkpoint](class-skills-implementation-checkpoint.md) records all240routes. The [status ledger](../data/Sanctuary/Balance/class-skill-status.json) marks every route `handler-present` with `partial-native-coverage`; that deliberately does not assert exact modern parity. An empty `completedClasses` list is a strict certification result, not evidence that the classes lack their skills.

Recent Paladin and Barbarian work added missing real input tests and retained existing mechanics. Their production DLL and numerical data were unchanged. Necromancer and Sorceress refreshes also reused the accepted production build. These are verification milestones, not newly implemented classes or another twenty percent of unwritten skills.

Remaining work must be bounded by the original class-skill objective:

1. All eight classes now have current combined input/lifecycle evidence. The final Warlock/Witch composition passes906results across three saves and two reloads,with26active input routes and four passive consumers. Reuse the archived scopes;do not rerun unchanged class cohorts solely for another checkpoint. Use focused checks for newly demonstrated defects or uncovered requirements.
2. Resolve or explicitly defer individual unsupported mechanics, with their exact effect on play recorded. Find Item's disputed chance/special treasure contexts remain documented. Sentinel's Stealskull/Health Link delivery defect is repaired using the existing ordinary-NPC contract,while exact modern flat-drain semantics remain uncertain. Impale's paid shield/avoidance policy is explicitly approved and included in the current809-result Amazon qualification. Passing unrelated tests does not resolve other gaps.
3. Fix demonstrated defects in the intended ordinary class routes and their required shared combat services. Preserve approved Sanctuary rules; do not invent uncertain source behavior or reopen settled approvals.
4. Promote the [prepared matching server/client package](class-skills-handoff-checkpoint.md),preserving the installed build and original saves. The copied world and corrected client startup are verified. Synchronize source and normal staging caches during main consolidation,then verify the installed original world and provide the concrete handoff. Report remaining mechanical limits and separate assets.

The user has clarified that the shared integration and regression work should be completed and that there is no time pressure. Required item,damage,pet,persistence and input interactions remain part of mechanical correctness;240handlers alone is not completion. Report each task's concrete functionality,defect or verified requirement clearly. Global UO-monster spell conversion remains its separately designated project. Missing final artwork stays separately tracked rather than blocking mechanics; unsupported modern-reference and recipient contexts remain explicit.

Do not repeat an unchanged source matrix or full class cohort solely for another checkpoint. Use focused evidence for a new defect, shared-code change or uncovered requirement. Keep historical receipts tied to their exact artifacts; a new test count alone is not new gameplay functionality.

Current evidence: [Necromancer](necromancer-lifecycle-acceptance-checkpoint.md), [Sorceress](sorceress-lifecycle-acceptance-checkpoint.md), [Paladin](paladin-lifecycle-acceptance-checkpoint.md), [Barbarian](barbarian-lifecycle-acceptance-checkpoint.md), [Amazon](amazon-lifecycle-acceptance-checkpoint.md), [Assassin](assassin-lifecycle-acceptance-checkpoint.md), [Druid](druid-lifecycle-acceptance-checkpoint.md). [Warlock/Witch](warlock-lifecycle-acceptance-checkpoint.md). Separate asset lists: [effects/audio](skill-effects.md), [icons](skill-icons.md).

Later gear-skill design discussion requested by the user: items with skill charges and original skills granted by equipment, potentially obtained through crafting, could add character-building depth. Reuse existing skill mechanics where appropriate. This note does not choose eligible skills/classes, skill ranks, charge costs/replenishment, crafting recipes, balance or acquisition rules; discuss those later. It does not authorize charms or runewords, which remain excluded.

After the class work, the user has requested consolidating the branches into main. The [recorded inventory and follow-up](main-consolidation-plan.md) found that most branch tips are already part of the working history;four side branches require separate comparison. Recheck remote refs and preserve the main worktree's existing submodule changes and installed state when performing that consolidation.
