# Shadow / Valkyrie reuse audit

2026-11-08 JST. Read-only audit after the verified Flight checkpoint `e40b5a3b`; **208/240 handlers, Assassin28/30**. No new runtime code, rebuild or regression run was needed for this audit.

The Shadows already reuse the equipment/stat/resource framework and minion infrastructure. The next work is connecting their own source profiles to existing services, rather than creating another summon system.

| Area | Reuse | Actual remaining connection |
| --- | --- | --- |
| Original equipment and numeric resources | `GeneratedNpcStatGraph`, `GeneratedNpcLiveResources`, `ShadowLoadout`, `ShadowEquipmentResources` | Shadow resources currently require `Map.Internal`; introduce qualified world admission without replacing original items, wounds, ranks or RNG. |
| Ownership, safe-area parking and difficulty | Existing `SkeletonGuardian` / `IPersistentMinion`, `PersistentMinions`, `PetOwnerRoutes`, `MinionSafeAreas`; Shadows already register here | `AssassinShadowBody.CanActivate` is deliberately false. Connect its own action suspend/resume and combat admission before enabling it. No new roster or parking persistence is needed. |
| Item events | Type-independent `GeneratedNpcItemEvents` and its saved original-item/event-order binding | `GeneratedNpcLiveResources.Validate` currently forbids Shadow item events and only admits Valkyrie event owners. `NpcOriginalItemProcs` also hardcodes `AmazonValkyrieBody`. Generalize the host boundary with species-specific validation, not copied callback implementations. |
| World movement, melee and incoming combat | `NecromancerGroundWorld`, ground motion/contact capabilities, `PreparedMonsterContact` / incoming/return services, existing Valkyrie integration patterns | Supply Shadow-specific metadata, weapon/animation/event bindings and its own action conductor. Do not give a Shadow the Valkyrie source ID, stats or AI. |
| Casting and skill ownership | Existing `ShadowSkillBook`, owner snapshots, passive graph, `SkillStartup`, `SkillExecution`, projectile/elemental services | Supply real Shadow NPC permission/target/mode/event execution and AI105/106 selection. Valkyrie provides lifecycle/combat patterns, but does not supply the Assassin spell-selection behavior. |
| Learned summon cast | Existing Valkyrie release transaction: destination check, candidate creation, payment, replacement, rollback; shared casting/landing helpers | Use Shadow-specific birth/rank/cost/shared-summon-limit rules. Keep the talent unavailable until the real actor can act. |

Next implementation slice: qualify a world Shadow resource/event host and incoming combat, then execute an actual owned NPC Fire Blast through the existing dispatch/projectile services. Preserve its original graph and exercise world movement/parking and one reload. Extend outgoing contact and AI selection on that same body; avoid parallel player-cast proxies or another generic pet engine.

The parked-body guard is a dependency marker, not D2R behavior. Removing it alone would leave an inert or incorrectly exposed summon. Modifier42 death behavior, exact current NPC permission/AI code and existing source/geometry/item limitations remain explicitly unresolved. Current appearance/effects and talent/hotkey icon registers remain separate.

Use focused checks for each new connection. Run the shared-NPC/class regressions when the host/controller integration is ready, rather than repeating the full suite for each helper edit.
