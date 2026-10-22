# Targeting and controls follow-up

User direction recorded23 October2026. Implement this pass **after the class mechanics work**. These are deferred Sanctuary changes and acceptance notes; no new targeting or payment behavior is enabled by this document.

## Forgiving crowd targeting

The user values Fist of the Heavens’ cursor-based acquisition, pet classes and untargeted areas. Chasing small moving melee targets produces accidental movement and frustration, especially around fleeing Fallen/Flayers and shamans keeping Inferno distance. Preserve deliberate aim while allowing useful targeting leeway. The implemented FoH behavior remains the individually verified baseline; define broader skill-specific changes during this later pass.

## Standing melee swings and mana

Allow a player holding Shift to stand and swing into empty space while waiting for enemies to enter reach. A completely empty swing should consume no mana. Check enemy reach/eligibility both when the attack starts winding up and when its impact fires.

The working interpretation is **at most one mana charge per activation**, paid if either checkpoint finds an eligible enemy. A charge already paid at wind-up is not paid again at impact.

| Enemy in range at wind-up | Enemy in range at impact | Intended mana result |
| --- | --- | --- |
| No | No | Free swing |
| Yes | No | Charge once from the wind-up check |
| No | Yes | Charge once at impact |
| Yes | Yes | Charge once total |

**Target clarification:** hit the closest eligible mob within melee reach; when waiting with no target in reach, acquire the first mob that enters. Define equal-distance ties, facing/LOS and committed-target/retarget timing during implementation.

**Insufficient-mana clarification:** fall back to an ordinary basic attack. Interpret the user’s “autohit” as a normal auto attack with the usual hit roll, consistent with the existing approved Zeal fallback. Do not grant a guaranteed hit. Apply this fallback to the affected standing-melee flow, including a target that first enters at impact; avoid a second mana debit. Animation/series transition details and prevention of unpaid skill-only effects still need implementation design.

This bases the decision on combat eligibility and reach, rather than whether an attack roll ultimately hits or damage penetrates defenses. It should let an empty standing swing connect normally when a monster enters legitimate reach before impact. Preserve the skill’s attack cadence and prevent double payment or duplicate delivery.

Resolve these details when implementing:

- Exact debit/reservation sequencing, equal-distance ties, facing/LOS, range eligibility and committed-target/retarget timing at each checkpoint.
- Transition from an already-started empty skill wind-up to the approved ordinary attack when mana is insufficient at impact; define its remaining animation/series accounting.
- Multi-hit/sequence accounting, including Zeal’s one payment per series, and cancellation after payment.
- Skills with effects beyond weapon damage: temporary defense, Berserk’s penalty, self-life costs, charges or other state changes. Decide explicitly which effects an unpaid empty swing can apply.
- Scope beyond Shift-held standing melee. Spells, ranged attacks, item charges and other resources are not covered by this proposal.

Acceptance should include an enemy entering/leaving reach during wind-up, repeated empty swings, moving small targets, misses/blocks/immunity, insufficient mana, held-input release, interrupted attacks and multi-hit skills. Keep the D2R3.3.93847 reference separate from this user-directed Sanctuary behavior.
