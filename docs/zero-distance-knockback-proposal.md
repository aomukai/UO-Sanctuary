# Approved zero-distance native knockback

Reference:D2R Reign of the Warlock3.3.93847. **User-approved Sanctuary adaptation,2026-11-07:** “Use the attacker’s facing for knockback; preserve normal distance, chance and damage.”

When attacker and recipient share a native tile,`SourceKnockbacks` obtains a unit direction from the attacker’s current facing,masking the Running bit. The existing source offset/distance and trajectory/collision/recovery code then run unchanged. The fallback consumes no actor,defender or world RNG and changes no damage or chance calculation. Ordinary nonzero position deltas retain their existing direction. This is a native projection adaptation,not an observed current D2R rule.

A real unfiltered item rolled Knockback while the ground fixture used overlapping native positions. The approved fallback now keeps that case operational without filtering or rerolling the item. The fixture retains its overlap,with an additional16-case all-eight-facing/frozen-state check through the actual shared ordered return.

The shared source return selects knockback before its frozen/hit-recovery gate. The native return now uses the existing prepared-knockback entry point,which handles the already-selected mode without an extra native Frozen/Paralyzed veto and preserves its existing frozen movement pause. This reuses the same helper already used by prepared Smite/skill knockback.
