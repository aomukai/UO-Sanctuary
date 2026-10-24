# Summon difficulty across facets

Approved Sanctuary deviation, 25 October 2026: a facet's explicit Normal/Nightmare/Hell designation supplies summon difficulty. When the owner enters a facet with a different difficulty, **existing summons rescale** and **preserve their current life percentage**. This takes precedence over treating D2R's game-wide difficulty as permanently fixed at summon creation.

Specific Nightmare/Hell facet assignments will be designated later. No facet names or assignments have been invented. The engine exposes map-change handling; the shared summon integration still needs to connect that transition to the designated difficulty and stat consumers.

Recompute difficulty-dependent actor stats from retained summoning context. Rescaling does not count as another cast or summon, does not recharge mana or restart lifetime, and must not recollect an already-created missile's weapon damage from later gear. Preserve FP256 health precision and the existing ownership, parking, following, teleport and death rules. Injured transitions in both directions, fractional rounding, parked/offline summons and save/reload require tests.

**Implementation status:** approved design, pending shared implementation. Existing Normal-base summon behavior and Blade Sentinel's prepared world adapter are not evidence that facet scaling is already active. This resolves the policy question; it does not complete summon/spawn integration or a class certificate.

See the [machine-readable decision](../tools/reference/summon-facet-difficulty-policy.json), [mechanical reference](skill-mechanical-reference.md) and [pet lifecycle](death-and-pets.md).
