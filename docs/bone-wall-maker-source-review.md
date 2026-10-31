# Bone Wall maker growth and retirement — source checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations first. This increment executes source control flow; it does not change or certify native growth. The prior [barrier casting acceptance](bone-barrier-cast-checkpoint.md) remains separately bounded. No skill or class is complete.

This source-only checkpoint was followed by the [independent-maker implementation and native acceptance](bone-wall-maker-checkpoint.md). Its descriptions of the old native counter below identify the implementation being replaced; source-world geometry and retirement limits remain open.

The [receipt](../tools/reference/bone-wall-maker-source-evidence.json) binds five complete, unchanged legacy bodies to commit `0c0bbd1e6bef9181a3da1c046523af622a925416` and the pinned current tables. `python3 tools/probe_bone_wall_maker.py --check` compiles and executes **2,592 composed maker/collision cases, a six-frame failed/successful allocation sequence and 96 AI contexts**, with clean AddressSanitizer/UndefinedBehaviorSanitizer output. All 71 Python tests pass. Factory, contact eligibility, path-cell flag setter and Wall casting bodies are statically reviewed separately. Declared services are listed in the receipt; this is not execution of the full world engine or current executable.

The maker reads the prior path-change flag and current integer position before the common missile update. No owner or zero budget returns removal immediately. With no changed path, it skips allocation but still runs movement/lifetime/collision. A found root allows one exact flags13 allocation, with no nearby retry. A successful allocation links the root/minion, applies modifier15 and summon passives, stores the original caster, registers node9 and then decrements the success budget. **A failed allocation does not stop the arm or consume that budget.** A missing root also leaves it unchanged while common updates continue. Allocation can precede a terminal movement/expiry callback in the same update.

Current maker data gives velocity12, maximum12, CanSlow1, size1, mode8 and lifetime `7 + 2*lvl` frames. The factory's 75% velocity factor and prior Slow Missiles adjustment are statically reviewed. Mode8 uses the existing player-or-monster finder with monster/player/missile-barrier/wall masks; its finder is not interchangeable with an unconditional hostile-target scan. Bone Wall's species footprint is **2×2 source subtiles**. The provisional two-subtiles-per-native-tile mapping alone does not establish accepted segment spacing or wall length.

Both AI bodies execute. Initialization takes species skill0's Param2, currently600 frames, and adds the birth frame. At equality the think callback still idles15 frames; only `deadline < current frame` requests DEATH mode. First-think scheduling, actual event cadence, DEATH duration, hidden-dead behavior and minion removal are not established by those bodies. The current native direct24-second deletion remains an adapter.

Reuse before implementation:

- Reuse `BasicMissileUpdate` and the existing poison collision probe ABI for the common collision/update contract. This source probe already composes that ABI rather than copying it.
- Reuse `MissileVectors`, `SourceMissileSteps` and `MissileValues` for supported vector, saved-cell and formula behavior. Preserve the maker's previous-step ordering, independent arm lifetime/budget and mode8 contacts.
- Reuse `BoneBarrierBirth` and `BoneBarrierBody` for child resources; determine each child's own birth/death deadline separately from root-owned cleanup.
- Preserve historical `BoneBarrierSegment` and prepared-body saves when introducing a growth controller. The older normalized whole-tile counter lacks the required saved maker state.

Next: implement and verify independent maker state through those shared services, with blocked-then-successful placement, terminal updates and actual save/reload checks. Keep exact footprint/room search and current-executable behavior flagged where the low-level spawn routine remains a stub. Prison raster geometry, source retirement/minion phases and actual old-build upgrade remain separate work. No questions block this preparation.

No runtime C#, balance data, serialization, installed files or saves changed. No server build or deployment was needed for this source-only increment. The last accepted native component remains build4/run2/audit2:43 checks,186 handlers/Necromancer30, no completed class. Effects/audio and talent/hotkey icons remain in their separate placeholder lists; approved pet upgrades and safe-area parking remain queued.
