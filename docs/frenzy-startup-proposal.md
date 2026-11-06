# Approved Frenzy startup rule

Reference: D2R Reign of the Warlock **3.3.93847**. The user approved this rule; its implementation and scoped native acceptance are recorded in [the checkpoint](frenzy-checkpoint.md). The complete older release/charge routines are already prepared; the current start67 body remains unavailable. See [existing release checkpoint](frenzy-release-checkpoint.md).

The exact question and answer are retained in [the approval record](../tools/reference/frenzy-start-decision.json). The approved rule is:

- Require two eligible melee weapons and use the shared SQ11 events, without Double Swing’s separate 50% skill-speed addition.
- Charge 1.5 mana per released strike; use an ordinary attack when underfunded.
- Each successful, unblocked contact adds one speed charge, capped at the effective Frenzy rank, and refreshes the table duration: 6 seconds plus 0.4 seconds per hard Increased Stamina point.
- Evaluate the pinned movement/attack-speed curves at the charge count.
- Cancellation stops remaining strikes without refunding released strikes.

This is an explicit Sanctuary startup/payment/charge-timing adaptation. It does not establish exact current D2R startup, paired IAS/WSM quirks or the older preceding-packet success chronology. Pinned AR, hard-point damage synergies and Berserk magic conversion still need binding to existing contact/stat consumers and native verification. Final effects/audio and icons remain separate assets.

The learned Frenzy route is enabled under this approved contract. No further approval or running D2R client is needed for it.
