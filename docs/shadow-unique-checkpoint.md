# Shadow unique-recipient checkpoint

2026-11-08 JST. Mechanical reference: D2R Reign of the Warlock **3.3.93847**, current summon rows and the pinned older source identified in the [modifier review](shadow-death-modifier-review.md). Approved Sanctuary deviations take precedence. **208/240 handlers; no class certified complete.** Learned Shadow Warrior/Master remain gated.

Both original Shadow bodies now expose their saved creation modifier42 through the shared unique-recipient predicate. Player melee, original-pet melee and original-pet missiles use that predicate. Smite retains its existing additional Champion treatment. This does not assign EncounterGrade, boss status or encounter loot.

The approved pet-item/Clay slow rule now caps Shadows at **50%**, matching unique recipients. Native tests apply the slow to both original species and retain the existing pet-slow category, contact, fully-absorbed-hit, refresh, expiry and save tests.

Shadow stun eligibility reads its original monstats row. Both pinned rows have **Velocity0** even though the separate animation/movement implementation moves them. The older ExecuteEvents routine therefore rejects ordinary stun after its unique roll. Native controls verify this distinction, the shared unique rejection boundary for an explicitly supplied nonzero-velocity recipient, unchanged original attack RNG/birth state and identity/wounds across reload. This is a qualification against pinned data and older source; current executable stun behavior has not been independently observed.

Validation: one content build and one integration build, both zero warnings/errors. Final cohort: **15 native success markers, three boots, two actual reloads, three saves**. No old compiler matrices rerun. First run passed the new Shadow checks, then an existing pet-slow fixture lacked its required connected-socket harness. Reusing that fixture's configuration fixed the runner; unchanged assemblies were reused. The first log/input manifest is retained. [Reproducible receipt](../tools/reference/shadow-unique-native-evidence.json), generated and checked by `tools/record_shadow_unique.py`. Installed artifacts, saves, configuration and historical client patches remain preserved.

Still unfinished:

- Modifier42's four-frame delayed death nova, retention of the actual dead caster, damage context, once-only delivery, original-item/roster cleanup and save/reload behavior. Ordinary replacement, parking and deletion must remain distinct from death.
- Fury callback/rewind and the separate pending NPC missile-critical question; Master item/range/action gaps and learned Shadow admission.
- Full class and current executable/PvP certification.

No new icon is required by this internal recipient change. Stun keeps the existing clearly identified presentation placeholder. The future death nova must retain its separate effects backlog entry; no new artwork was generated here.
