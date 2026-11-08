# Remaining Shadow modifier42 work

**Update:** unique-recipient identity and approved slow cap are now implemented and verified in the [unique checkpoint](shadow-unique-checkpoint.md). Delayed death nova remains unfinished. The review below describes its original read-only scope.

Read-only source review,2026-11-08 JST. **Not implemented or certified by this review.** Reference: D2R Reign of the Warlock3.3.93847 current sumumod42 rows and the explicitly pinned older source.

Both Shadow skills assign modifier42 with `bUnique=1`. The assignment helper sets `MONTYPEFLAG_UNIQUE`. This must eventually reach original-recipient semantics such as the unique-only stun roll and the approved unique slow cap. It must not assign generic boss behavior,encounter loot or an encounter grade.

The modifier's mode-change callback schedules MONUMOD four frames after death. Its effect callback casts Nova only when the original is unique. The older helper derives rank from the root owner's effective **Shadow Warrior** rank divided by2,plus1,capped15;it uses Warrior even for Master. Missing owner/skill defaults to1,and life above10% forces rank1. These are reviewed older-source facts,not current executable observations.

Next implementation must cover:

- Correct unique identity in the existing original-NPC recipient services.
- Retained actual-NPC spell identity and delayed damage after UO removes the dead original,without borrowing root-player spell gear or masquerading as a living player caster.
- The approved summon-birth snapshot preference applied explicitly to rank/context;exact modern capture timing remains uncertain.
- Actual death separated from replacement,respec,logout,parking and other deletion paths.
- Once-only scheduling/delivery,original item and roster cleanup,save/reload and native lethal-hit tests.

Reuse existing source nova geometry,typed spell damage and retained combat contexts wherever their contracts match. A visible nova alone would not complete this mechanic.

[Exact source/body hashes and reviewed mapping](../tools/reference/shadow-death-modifier-source-review.json). No new compiler matrix or native execution was run for this review.
