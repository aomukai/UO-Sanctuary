# Stationary sentry placement proposal

Reference: D2R Reign of the Warlock **3.3.93847**; Sanctuary deviations take precedence. The complete older trap factory first asks the monster spawner for its default position (`radius=-1`), then retries at radius4. The general position-search body is not recovered. Existing Sanctuary player do45 uses the reviewed Decoy-style free-cell contract and refuses an occupied point. Shadow AI normally targets a monster's occupied position, so this is now a concrete blocker to ordinary automatic trap placement, not a reason to copy the entire trap system.

Proposed explicit Sanctuary rule, shared by Charged Bolt Sentry, Wake of Fire, Lightning Sentry, Wake of Inferno and Death Sentry, for player and Shadow casts:

1. Try the requested point first at its retained Z.
2. If unavailable, examine UO tiles within one tile, then additional tiles out to four tiles. Within each band, order by squared distance from the request, then X, then Y.
3. A candidate must pass the existing world/map bounds, skill range, vertical difference, town, native line-of-sight and `CanSpawnMobile` checks. No alternative elevation, wall crossing, remote room, caster-cell substitution or unbounded search is introduced.
4. Resolve the target's current position at release using the existing dead-but-present/fallback behavior. Startup and release use the same placement helper; placement is revalidated before allocation/payment. A completely blocked area is a free failure; a successful player cast retains the existing once-only table payment. NPC casts still debit neither NPC nor player mana.
5. Creation, emergence, snapshots, shared player five-trap FIFO, shooting, projectile ownership, facet/offline rules and retirement all remain their existing services.

This defines native UO placement; it does **not** claim the unrecovered D2R search order. The existing requested-free-cell route remains available while this proposal is unanswered. Ordinary occupied-target Shadow ground admission currently refuses without allocating a trap or seed. Verify candidate ordering and bounded failures, occupied actors, walls/town edges, windup changes and shared player/NPC input before enabling the adaptation.
