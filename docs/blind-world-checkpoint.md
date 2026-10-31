# Dim Vision live controller checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**. Approved Sanctuary deviations take precedence. Verification is in progress; this document describes the candidate scope. Full blind AI, skill and class completion remain unproven.

Dim Vision now invokes `BlindAiPolicy` for the traced Skeleton, Zombie and Megademon AI families. A new runtime table retains the 23 current pinned `monstats` timing/range rows, including difficulty variants. Source neutral-delay/range defaults are kept explicit. Facet difficulty selects these rows. Unsupported native actor mappings retain the earlier blind adapter and log the missing mapping.

An optional `BlindAiState` record lives outside the curse in the existing version-0 profile persistence. Its source-algorithm RNG seed survives ordinary curse refresh, replacement and expiry. New actors receive an independent initialized stream; this is not a claim to recover the historical D2 seed or a shared global NPC combat stream. State records the decision deadline, target, native movement goal and step deadline. Native movement deadlines are captured before an immutable worker serialization snapshot. Existing profile shapes remain readable.

The 40 ms controller poll respects saved deadlines. Target-present idle lasts 10 source frames; the target-mode-1 no-target branch uses its separate distance-based delay without a percentage draw. Wandering uses the verified source draw order before projecting the goal into the native world. Pending chase/flee motion is retained until the existing adapter's mode boundary. Walk completion uses the pinned neutral delay. Adjacent attack admission is published before the native `Combatant` setter checks permission; normal weapon scheduling owns the actual attack. Target acquisition has a scoped bypass of the blind range gate, which is removed before ordinary harm checks.

Candidate verification combines the retained 512-marker cast/curse/pet/weapon cohort with the policy differential, saved controller checks and connected natural timing checks. The synthetic controls exercise source deadlines, no early draws, independent adjacent admission, scoped acquisition and seed continuity. A fresh actor isolates adjacent admission because advancing a source inspection clock does not advance `LeashedMeleeAI`'s independent 200 ms scan throttle. Connected checks instead exercise the same actor through a 400 ms target-present idle, a 1,000 ms no-target idle, and adjacent reacquisition after the enemy returns. Neither inspected deadline is edited; the connected player's real sector activation keeps the actor running.

The saved wander fixture holds inspection-only deadlines, verifies exact seed/intent/deadline restoration and resumes actual native steps on both reloads without new decision draws. The retained obstruction fixture now explicitly advances its saved step inspection clock and waits 650 ms to observe the pinned 600 ms neutral boundary, replacing its earlier 350 ms timing. The assertion's purpose remains, but its execution timing changed.

Known boundaries remain explicit:

- Native target scanning, tie order, LOS, reacquisition gates, room/collision checks, coordinate projection, walking and path completion are world adapters. They do not establish current-executable parity.
- Source `dwAiState` 3/19 escape branches have no mapped native representation; this bridge supplies `false`. The distance-5 collision displacement has policy coverage but lacks a world acceptance case.
- Ownership/conversion coexistence, facet transfers and failed goal projection/path entry retain flagged fallback behavior. Other AI families remain unmapped.
- The 40 ms attack-admission poll delegates to normal native weapon timing. Exact source A1/action/recovery callbacks, initial AI deadline, lag behavior and unified NPC RNG ownership remain unverified.

Earlier failed attempts and their frozen inputs/logs/binaries are retained under `.local/class-skills/blind-world`. No deployment or class completion is claimed. Effects/audio and tree/right/quick icons remain separate placeholder ledgers. Pet balance upgrades and [actual-safe-area parking](pet-adjustment-review.md) remain approved design only: wounds remain, normal effect expiry continues, healer treatment is explicit, and named mercenaries stay alongside their owner.
