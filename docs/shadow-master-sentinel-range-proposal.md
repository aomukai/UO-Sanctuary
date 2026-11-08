# Shadow Master Blade Sentinel range proposal

Status: **approved by user: “Approve the Master Sentinel range rule.”**. Reference: D2R Reign of the Warlock **3.3.93847**. Approved Sanctuary deviations take precedence.

The pinned current missile table stores Blade Sentinel's `blade creeper` Range as `10 + (lvl * 5)`. The current modding guide describes Range as a duration in frames and allows a missile-scope expression. The older complete Shadow Master AI instead reads a literal missile range word for its type-5/type-11 skill eligibility check. Neither reference establishes how the current AI evaluates this expression or supplies `lvl`.

Approved explicit Sanctuary adaptation: for Shadow Master's Blade Sentinel candidate only, evaluate that expression with the casting Shadow's own current effective Blade Sentinel rank. Supply the result to the existing strict squared-distance guard: reject candidates at or beyond `(range - 1)^2` source-cell distance. Retain all other target acquisition, scores, LOS, leash, action validation and casting rules. This eligibility check does not change the blade's actual lifetime, path, birth snapshot or collision rules.

Examples: rank 1 gives 15; rank 12 gives 70; rank 20 gives 110. The ordinary AI search and cast constraints still apply, so these values do not grant unrestricted targeting. Root player skill ranks do not supply this value. Literal missile ranges remain unchanged. The other expression rows remain explicitly gated until their own caller context is supported.

The prepared evaluator reuses `SkillFormula` and accepts only an explicit `lvl` scope; it refuses other unresolved variables. Validation passed all 694 literal rows, retained all 48 default expression gates, seven rank goldens and 21 strict-boundary whole-policy cases. The existing 497 complete Master AI source controls also pass across three boots and two reloads. These establish the proposed behavior, not current-executable parity. Automatic world use is now enabled only for this approved candidate; its world verification is recorded in the checkpoint.

Sources and checks: `tools/reference/d2r-local-missile-data-evidence.json`, `tools/reference/d2r-local-missile-guide-evidence.json`, `tools/reference/shadow-master-ai-source-evidence.json`, `tests/Sanctuary.Integration/ShadowMasterRangeChecks.cs`. The unchanged older AI accesses `wRange` directly in its type-5/type-11 branches; it does not call a rank-aware range getter.
