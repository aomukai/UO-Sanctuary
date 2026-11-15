# Bone Armor absorption rule

Reference: **D2R Reign of the Warlock3.3.93847**, with approved Sanctuary deviations first. **Approved by the user on 2026-11-16 JST.** [Exact decision](../tools/reference/bone-armor-rule-approval.json). The body below is the tested proposal; its current-code adoption is recorded separately. Bone Armor already remains playable with the conventional overflow policy described below. The pinned modern event22 callback could not be read; the older complete source has a different exhausting-hit branch. Neither is silently substituted for a current observation. This proposal resolves the supported incoming absorption contract without claiming the whole current event engine is recovered.

## Finite physical pool

Retain exact FP256 capacity and consumption. Absorb the smaller of incoming physical damage and remaining capacity. A breaking hit passes only the excess through; an equal hit consumes the shield without passing physical damage through. Clear the pool and its captured metadata together at zero. For example,2.75points remaining against3.25physical damage absorbs2.75and leaves0.5damage. Do not reproduce the older branch that passes the whole breaking hit through.

Bone Armor does not consume capacity for fire,cold,lightning,magic or poison. Keep captured effective rank,original maximum and remaining capacity; later gear/talent changes do not rewrite an active shield. Recasting refills from current effective rank and hard-only Wall/Prison synergies,using the existing unpaid windup,once-only release payment and failure/cancellation rules. Preserve the current cold-armor/Psychic-Ward group replacement and death,respec,class-change cleanup. Keep old integer saves readable without inventing their missing original rank or maximum.

## Supported incoming order

Adopt the existing typed pre-resistance sequence as Sanctuary policy: Cyclone Armor's eligible elemental pools,then Energy Shield's eligible components and shared mana,then Bone Armor's physical pool,then Psychic Ward's physical pool,followed by ordinary resistance/reduction/absorption and life settlement. Normal cast rules prevent Bone Armor,Psychic Ward and cold armor from stacking in their shared group; this sequence does not grant otherwise unsupported cross-class item skills or authorize malformed state combinations.

For a supported stored Energy Shield/Bone Armor overlap,spend Energy Shield mana first. Bone Armor consumes the remaining physical component. Preserve poison exclusion and fractional mana; partial mana exhaustion settles once and removes the empty Energy Shield through its existing service. Actual physical resistance applies after shield absorption: at20%Energy Shield,20points of Bone Armor and30%physical resistance,a50physical packet leaves14physical damage and spends20mana under the existing rank-zero legacy Energy Shield capture.

The native AOS armor-ignore path bypasses resistance,not Bone Armor's finite physical pool. Its existing physical-component hook now applies Bone Armor before Psychic Ward while preserving the nonphysical remainder and existing event context. A30physical armor-ignore hit against20armor leaves10life damage even with Shaftstop equipped; the resistance is bypassed but the shield is consumed once.

That legacy hook carries only total damage and a physical percentage. It does not establish full Energy Shield/Cyclone typed handling for native armor-ignore attacks. Those unsupported lane/producer contexts remain explicit for the later world/producer work; this proposal does not invent missing elemental fields or claim they are shielded. Ordinary typed class and encounter paths retain their qualified full-packet route.

## Persistence,verification and presentation

Keep existing profiles and serializer versions. New and legacy injured Bone Armor pools,unknown old metadata,shared mana fractions and supported stored overlaps retain their original values through loading. No reset,refill or replay of a cast is added by restoration.

The [current checkpoint](bone-armor-rule-checkpoint.md) records the36-check affected-system suite,six real right/quick casts and two actual reloads. It includes ordinary physical/native AOS/encounter routes,breaking/fractional cases,actual Shaftstop ordering,partial-mana overlap,group/lifecycle and original new/legacy save controls. The old cached source cases are reused without rerunning their compiler or attempting protected current-code reads. This is not a whole-class,PvP,special-producer,global-event-order or graphical certificate.

The existing0x375A cast/shield marker and sound remain identified placeholders. Final orbit,absorption,break and state effects/audio remain in [effects](skill-effects.md);talent/hotkey icons remain in [icons](skill-icons.md). No new image,client/kernel build,timer,profile or deployment is needed for this rule. The exact tested candidate and its evidence are pinned when the adoption decision is recorded.
