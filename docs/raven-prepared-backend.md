# Raven mechanical backend

Raven is being implemented against D2R Reign of the Warlock **3.3.93847**. The learned right/quick casting route is present in candidate166/Druid10, with focused native acceptance. It is isolated in the Raven worktree; the installed Armageddon package remains unchanged. See the [reference preflight](../tools/reference/raven-preflight.json) and [native evidence](../tools/reference/raven-stats-native-evidence.json) for exact inputs and accepted verification scope.

Casting is allowed in town, as the current row specifies. It allocates successfully before paying six mana, then enforces cap/FIFO replacement and issues the flock command. Failed casts preserve existing birds. Shared native range/LOS and registered-dead/deleted target fallback are explicitly bounded adapters. A pending Raven cast cancels immediately on a location or map change, even if the owner returns before its effect event.

The body reuses SkeletonGuardian's saved ownership, summon difficulty, health and wardrobe integration. A shared permission method preserves the existing Necromancer rules while allowing Raven to require its Druid history. Raven overrides native attack and thinking paths, disables regeneration and ordinary walking, has no collision footprint, and cannot take damage. Its bird body, attack animation and name are identified placeholders.

Birth level, life, defense and base attack rating are captured separately from each attack-mode packet. Current owner skill rank, hard wolf/bear synergies and attack bonus are read at attack-mode entry. A gear change during a dive does not rewrite that packet or the birth stats. The first AI event captures the finite attack budget; attempts consume that budget before hit resolution. Each attack uses the existing physical-damage and signed summon-blind consumers.

The flock manager enforces the current cap with existing summon creation order. Explicit focus updates every owned Raven, matching the user's current-build observation. It changes the next target selection; interruption of an already-started dive is not established. Successful paid casting invokes this command for an explicit eligible target.

Saved actor state includes the source motion state, scoped seed, budget, birth stats, attack packet, target and event clocks. Restore validates a complete replacement before assignment, and copies motion arrays in both directions. Logout and wardrobe parking cancel pending movement/attacks, retain consumed attempts, and skip offline time without damage or RNG draws. Returning owners resume the same bodies. Facet callbacks rescale stats using the approved owner-area policy while retaining current life percentage. Orphaned bodies are deleted.

## Timing and remaining limits

The stored timing profile is explicitly named `legacy-npc-clock-current-hk-metadata-v1`. It combines the inspected older NPC scheduler with the installed build's HK animation metadata. The shared animation planner produces emergence end at frame 14 and attack action/end at offsets 11/25. The older initializer schedules the first AI event at frame 25; attack completion then uses the row's 15-frame neutral delay. These are implemented reference values, **not a claim that the current executable uses the same AI schedule**. Blizzard's historical 2.4 release notes changed Raven attack frequency without specifying the replacement scheduler.

Other explicit boundaries remain:

- Flight composes the verified point-target path and motion components with a legal native map as its room and a zero-size footprint. Moving-unit path repair and complete source room recache remain unsupported. A requested unsupported repair faults that actor explicitly.
- Native map/LOS and nearest eligible NPC enumeration adapt the world services. The older owner-vision scan's side effects and modern target ordering are not yet composed. Unmapped source-size targets and PvP are refused.
- Warp placement, native tile/subtile projection, scoped RNG interleaving, million-step hit rolls, whole-life physical rolls, full native aura/item/event interactions and current executable equivalence remain bounded or unverified.
- Nonzero inherited physical sunder is refused pending its missing consumer.
- Six-mana SC release payment, right-only bindings, held-right release, quick input, cancellation/resource edges and saved unpaid-windup discard pass focused checks. Current audits, the Python suite, full regression and matching-client acceptance remain to refresh before installation.

Final summon, emergence, flight, dive, blind, retirement and audio assets remain in the independent [effects inventory](skill-effects.md). Talent-tree and hotkey icons remain in the separate [icon inventory](skill-icons.md). Assets do not block the mechanical work.
