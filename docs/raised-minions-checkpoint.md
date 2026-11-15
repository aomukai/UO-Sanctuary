# Player-owned raised helpers

This is a prerequisite for native Lich Revive, not admission of that still-unfinished cast path. The existing Lich raise animation creates the existing Skeleton/Zombie type. A persistent player-owned caster now binds its helpers to the same player, while a separate saved graph retains the actual caster. UO's control and summon master properties share one native master slot, so that slot cannot hold both identities.

Caster-dependent cleanup is a **prepared Sanctuary mapping**, not an observed D2R rule: these player helpers disappear with their actual raising caster, while wild helpers retain their existing independence. Its adoption belongs with the complete native Lich Revive proposal before that learned route is admitted. The current tests qualify the implementation without implying that design approval.

Wild Liches keep their existing raise path. Persistent owned casters use shared corpse access and loot rights. Failed owner binding leaves the corpse intact. Helpers retain the existing raised-undead damage/stats and nearby six-raised-unit limit; no additional ranks, spells or combat bonuses are invented. They are marked raised/no-rewards and cannot recursively create helpers. Existing parent-independent Hydras retain their separately approved lifetime rule.

The adapter reuses `IPersistentMinion`, the pet wardrobe/parking service, class history, safe-area rules, owner allegiance and difficulty helpers. Parking grants no healing or reset. Actual caster death/deletion removes its dependent helpers; owner loss and orphan cleanup use the existing pet sweep. No extra world timer is introduced. The new `SanctuaryRaisedMinions` version-0 profile stores actor, caster, player, class, difficulty and exact saved fractional life without changing an old serializer layout.

Facet changes reuse the existing Skeleton/Zombie variants and saved Normal baselines, preserving current life percentage. The helper's current facet projection is separate from its original spawn identity. Save/load restores its own actor, owner, caster, difficulty and wounds; offline helpers remain parked without an expiration timer.

## Verification boundary

The fixture qualifies a real controlled Lich through a test-only persistent behavior. It does not install a substitute Revive handler or claim that native Lich admission is complete. Live checks use the actual delayed `BeginRaise`/`ResolveRaise` path and a wild-Lich control, then exercise player allegiance, corpse loot, difficulty rescaling, parking/resume, caster-dependent cleanup and saved injured helpers. Existing native corpse and mapped-D2 corpse cohorts remain controls.

Content build 2 / Integration build 3 / source snapshot 4 / native run 2 passes **39 markers, nine authenticated connections and two reloads**. Three connections drive real owned and wild Lich raise animations; six retained inputs exercise Corpse Explosion and Poison Explosion. Original helper/player/caster identity and injured fractional life survive two reloads. The [evidence receipt](../tools/reference/raised-minions-native-evidence.json) is checked by `python3 tools/record_raised_minions.py --check`.

Integration build 1 failed because the native pooled mobile enumeration has no LINQ `SingleOrDefault`; the fixture now enumerates it directly. Native run 1 timed out before its raise started. Its cause remains unassigned. Additional start/owner/corpse diagnostics were added, and the unchanged Content build 2 then passed all three boots in native run 2. The failed sources, logs and assemblies remain archived; this checkpoint does not claim a recovered D2R timing rule or establish the timeout's cause.

## Remaining work

Native Lich Revive still needs explicit native eligibility, retained original capture/activation, combat/life integration and facet admission. Its legacy UO ability repertoire remains on the separate monster-conversion track; existing supported Revives keep living abilities. Other unclassified native species and flesh-dependent corpse consumers remain unfinished. Four older Warlock proposals remain frozen. Coverage remains 236/240 handlers, with no class certified.

Existing raise/summon visuals and audio are placeholders already covered by the effects register. This helper graph adds no talent or hotkey icon. Installed build, configuration and saves remain protected.
