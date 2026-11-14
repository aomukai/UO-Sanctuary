# Demon birth path: source preflight

2026-11-14 JST. Reference: **D2R Reign of the Warlock 3.3.93847**. This is bounded source discovery, with **no runtime changes or new builds**. Coverage stays **220/240 handlers, Warlock11/30; no class certified**.

The [owned-data receipt](../tools/reference/pet-birth-path-preflight.json) establishes a difference the generic test path does not represent:

| Source species | Class | Current source dimensions | Flying | Opens doors |
| --- | --- | --- | --- | --- |
| Goatman | 742 | 2×2 | No | Yes |
| Tainted | 743 | 3×3 | No | Yes |
| Defiler | 744 | 2×2 | Yes | Yes |

These values come directly from pinned monstats/monstats2. Gargoyle, Succubus and Gazer remain approved appearance mappings; the appearance does not determine source footprint or movement. In particular, a common ground-only path would lose Defiler's flying property, and a common one-cell fixture footprint is not evidence for the three production footprints.

The retained older common `PATH_AllocDynamicPath` is a useful reuse lead. It centers source coordinates at FP16+0x8000, gets size/collision pattern, and branches on flying before open-door ground movement. Its masks evaluate to0x1804 for flying and0x3401 for open-door ground movement. The caller-controlled `bSetFlag` branch adds0x10. Those are **older constructor observations**, not qualified current3.3.93847 constructor outputs or approved production defaults. Current field offsets and the actual constructor/caller must be established, or replaced by an explicitly approved native mapping.

Two already-qualified current operations must stay separate. The existing path reset clears0x20 and both counters and supplies Raw96=0; that does not establish fresh constructor values. The current monster placement's nearby search passes0x1c09 as its search masks; those are not the older movement masks. Copying either value into the other operation would conflate distinct behaviors.

The existing birth registration verifies body/owner/path identity, location and bounds, and safely persists its supplied context. It deliberately does not certify the supplied fixture geometry. The new AI-startup approval remains fully consumed by that binding and does not grant missing path construction or automatic clock/action semantics. Existing controller/save state remains untouched.

Two other routes were rechecked before this discovery. **Sigil: Death** still lacks qualified event33/34 bookkeeping and do153 corpse/explosion behavior; a shared marker alone cannot establish it. **Slow Missiles** still lacks ordinary native ranged travel/impact and relevant spell/breath/child coverage. The present BaseRanged.OnSwing emits its visual, then immediately resolves CheckHit/OnHit. A cast-only route would leave that producer functionally unaffected. Neither route was silently enabled, and no protected callback or closed D2R client was retried.

Next: prepare a concrete tested Sigil: Death adaptation using the existing Sigil, corpse and explosion services for review, with every unavailable choice stated. Keep demon geometry/clock bindings and Slow Missiles producer work separate, and reuse their existing source-qualified components. Do not replace missing behavior with fixture constants or a simpler AI.
