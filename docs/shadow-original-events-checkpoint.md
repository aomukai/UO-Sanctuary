# Shadow original item events

2026-11-08 JST; mechanical reference **D2R 3.3.93847**, approved Sanctuary deviations first. Handler coverage remains **208/240**; neither Shadow talent is enabled yet.

`GeneratedNpcLiveResources.ActivateItemEvents` now accepts the existing equipped-summon base type and admits either a validated Shadow or Valkyrie source frame. Both use the existing `GeneratedNpcItemEvents` registry, original item identity/order, passive graph and saved resource transactions. A Shadow cannot claim a Valkyrie frame; foreign owner/item histories remain invalid. This adds no parallel registry, cloned items, world activation or automatic proc execution.

**Verification:** Content3/Integration3/snapshot3 builds cleanly. Eighteen focused native markers pass through three boots/two actual reloads/three saves. Twelve newly registered Shadows retain real generated events, original items/passives/wounds/RNG and callback/reattachment order; the existing Shadow-body checks and 57 original Valkyrie event loadouts also pass. Snapshot/native input matching and fresh item audits pass with unchanged573availability decisions. Installed artifacts/saves/configuration remain preserved. See [receipt](../tools/reference/shadow-original-events-native-evidence.json). No full class-suite rerun was needed for this registry connection.

The initial compile used a broader actor type than the existing carrier guard accepts; it now uses `SkeletonGuardian`, shared by both equipped summons. Early reduced-harness runs omitted inherited Shadow fixtures/world-RNG continuation bookkeeping; the final focused run retains those original checks with their dependencies. The new registry checks passed throughout; these harness failures are retained in the ignored stage logs.

Next: generalize the species-aware live proc/cast host and incoming receiver, then connect Shadow world action/motion and NPC skill execution/AI105/106. `CanActivate=false`, parked resource/world guards and unavailable learned casts remain until those connections work. No new visual effect is produced; existing Shadow effects/icons remain pending separately.


2026-11-08 follow-up: the original-item spell/proc host now accepts the shared summon base through `OriginalNpcEquipment`. It validates each species' actual owner, original hand binding, source level and numeric/passive graph. Item spell capture, synchronous casts, ordinary/radial/charged flights, chain roots/children and the saved world allocator reuse their existing implementations. Intrinsic missile critical uses the actual source monster ID. An unsupported intrinsic off-class Shadow synergy refuses explicitly. Saved formats are unchanged.

Shadow registration and numeric item-spell capture are available on the parked original body. This does **not** activate its world combat or intrinsic Assassin skills: `WorldEnabled=false`, `CanActivate=false` and the resource map guard remain. Original synchronous spell frames/execution also reject unqualified species before effects or allocation. Valkyrie combat admission is unchanged.

The focused native host package extends the existing Shadow checks and retains Valkyrie item emissions, actual saved projectile travel, chain hops and ordinary/incoming original-item contacts. Final results are recorded in [host receipt](../tools/reference/shadow-original-host-native-evidence.json). No item table or availability decision changed; the full class suite and audit exporter were not rerun for this host refactor. Assets remain the existing appearance/effect placeholders and separate pending icons.

Next: qualify Shadow incoming defense and world/action lifecycle, then its own intrinsic NPC casting and AI. The older AI105/106 routines are in `D2Game/src/AI/AiThink.cpp` (Shadow Warrior starts at line13969), not `AiTactics.cpp`. Current AI/permission and modifier42 death uncertainties stay explicit. Neither Shadow learned route is enabled; coverage remains208/240, Assassin28/30.

Host verification: **27 native markers pass across three boots, two reloads and three saves**, Content2/Integration1/snapshot1. Source snapshot matches the actual tested inputs. Installed106artifacts,38saves,20configuration files and historical client patches match the protected baseline. No live test job remains.
