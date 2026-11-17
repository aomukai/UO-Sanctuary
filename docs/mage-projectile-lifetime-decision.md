# Skeletal Mage projectiles after their caster dies

**Approved by the user — 2026-11-17 JST.** The user selected “Keep already-fired projectiles active.” This is an explicit Sanctuary adaptation; pinned D2R 3.3.93847 caster-removal behavior remains unverified.

An already-fired Skeletal Mage projectile continues flying and damaging until its existing expiry after its Mage dies. The Necromancer's death, logout and facet restrictions still apply. Death does not create new shots, renew lifetime, heal/reset the Mage, recalculate captured damage or invent historical phases. Preserve the actual emitter/credit and saved snapshot through the existing shared services wherever possible.

The decision is recorded while the current combined Necromancer run is live. Its binaries/source must remain fixed until that exact run is terminal. Implementation and focused verification follow; this file is an approval record, not a claim that the changed behavior is already enabled or tested.

[Decision record](../tools/reference/mage-projectile-lifetime-approval.json).

Subsequently implemented and verified in the [lifetime checkpoint](mage-projectile-lifetime-checkpoint.md): 57 results, 15 retained live-cast sessions and two reloads. The decision record above remains a point-in-time approval, separate from that implementation evidence.
