# Approved Sanctuary Blade Fury input rule

The user explicitly answered **“Approve this Blade Fury rule”** on2026-11-08 JST, after the review-only candidate passed12focused/231retained markers. The approved design is in [the tested proposal](blade-fury-held-input-proposal.md); this approval is separate from Inferno and Arctic Blast.

- Require six mana, or the current per-blade cost if higher, on a fresh press. No upfront debit.
- First blade after12frames/0.48seconds; subsequent blades every5frames/0.20seconds under the tested sequence policy.
- Rank1 costs1mana per blade. Higher ranks use pinned `(8 + rank - 1) / 8` mana.
- Release immediately stops new blades; already-fired blades keep their independent damage/lifetime.
- A fresh press starts a fresh windup and threshold, without the unresolved released/active/future-shot fresh rewind.
- Gear/aim changes apply to subsequent emissions; existing packets remain captured. Heartbeats renew transport/aim only.
- Exhaustion/cancellation ends the gesture; held intent is not persisted or replayed.

This is an approved Sanctuary adaptation. Exact D2R3.3.93847 input timing remains unverified. Approval does not certify modern AR/mastery/Slow Missiles ordering, full item/proc/PvP/owner or rendered-client behavior. Final effects/audio and talent/hotkey icons remain separate.
