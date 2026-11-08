# Shadow Dragon Flight action proposal

2026-11-08 JST. **Approved by the user: “Approve this Shadow Flight rule.”** Pinned reference: D2R Reign of the Warlock 3.3.93847; existing approved adaptations take precedence.

Both original Shadow monster rows link skill slot 5 to `seq_swdragonflight`. Its 23 current-file points have action markers at raw points 9 and 14. NPC skill metadata selects mode 14. The retained complete ordinary NPC sequence callback passes the raw point; the older Flight do52 interprets odd as kick and even as teleport. That composition appears to reverse the intended order, and a successful first contact can set the flag that suppresses the later callback. This is a source/data inference, not an observation of the current executable. [Authenticated review](../tools/reference/shadow-flight-source-review.json). Existing source matrices were authenticated, not rerun.

Approved Sanctuary rule for both Shadow species:

- Use the owned monster sequence, with an explicit teleport stage at its first marker and kick stage at its second. Keep the original target. Qualify the NPC sequence clock separately; do not silently import the player callback ordinal bridge or player clock.
- Require actual successful landing before the kick. Resolve near the target through the existing source free-coordinate search and explicit native ground/occupancy/town/travel checks using the Shadow as traveller. Failed landing cancels the remaining action.
- Spend neither pet nor player mana. This is an intrinsic NPC action.
- Use original NPC body/boots, Flight damage/AR, source-128 typed fill, own equipment/RNG and shared contact/charge services. Charged kicks bypass accuracy only, retain shield/block/dodge rules, and consume one charge per active family on successful contact.
- Interruptions, target loss, external movement, parking and offline action loss stop later stages. Pending intent is discarded, never replayed from saves. Exact owned actor/item/resource/wound/charge/RNG persistence remains required.

The player Flight approval remains separate. This proposal defines the NPC phase/landing policy; it does not certify modern executable parity, unrestricted Master AI, exact world geometry, final assets or the full Shadow skills. Implementation must still qualify the NPC sequence clock, landing transfer and two-stage contact before admitting the action.
