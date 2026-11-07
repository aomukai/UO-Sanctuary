# Shadow original item events

2026-11-08 JST; mechanical reference **D2R 3.3.93847**, approved Sanctuary deviations first. Handler coverage remains **208/240**; neither Shadow talent is enabled yet.

`GeneratedNpcLiveResources.ActivateItemEvents` now accepts the existing equipped-summon base type and admits either a validated Shadow or Valkyrie source frame. Both use the existing `GeneratedNpcItemEvents` registry, original item identity/order, passive graph and saved resource transactions. A Shadow cannot claim a Valkyrie frame; foreign owner/item histories remain invalid. This adds no parallel registry, cloned items, world activation or automatic proc execution.

**Verification:** Content3/Integration3/snapshot3 builds cleanly. Eighteen focused native markers pass through three boots/two actual reloads/three saves. Twelve newly registered Shadows retain real generated events, original items/passives/wounds/RNG and callback/reattachment order; the existing Shadow-body checks and 57 original Valkyrie event loadouts also pass. Snapshot/native input matching and fresh item audits pass with unchanged573availability decisions. Installed artifacts/saves/configuration remain preserved. See [receipt](../tools/reference/shadow-original-events-native-evidence.json). No full class-suite rerun was needed for this registry connection.

The initial compile used a broader actor type than the existing carrier guard accepts; it now uses `SkeletonGuardian`, shared by both equipped summons. Early reduced-harness runs omitted inherited Shadow fixtures/world-RNG continuation bookkeeping; the final focused run retains those original checks with their dependencies. The new registry checks passed throughout; these harness failures are retained in the ignored stage logs.

Next: generalize the species-aware live proc/cast host and incoming receiver, then connect Shadow world action/motion and NPC skill execution/AI105/106. `CanActivate=false`, parked resource/world guards and unavailable learned casts remain until those connections work. No new visual effect is produced; existing Shadow effects/icons remain pending separately.
