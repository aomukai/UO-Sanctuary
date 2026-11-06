# Approved Sanctuary Inferno input rule

The user approved this adaptation on 2026-11-06 with the explicit answer: **“Approve this Inferno rule.”** This supersedes the pending-decision status of the [review proposal](inferno-held-input-proposal.md). Its historical [prototype receipt](../tools/reference/inferno-held-candidate-native-evidence.json) remains an immutable record of the unapproved test stage.

Mechanical reference remains **D2R Reign of the Warlock 3.3.93847**. This is an approved Sanctuary adaptation for **Sorceress Inferno only**; it does not approve Arctic Blast or Blade Fury timing.

- A fresh press requires six mana, or the current invocation cost if higher. It debits the pinned invocation cost, not the threshold.
- At effective rank 1, entry costs 0.375 mana, first emission occurs at 0.48 seconds, and held renewal then occurs every two 25-Hz frames. Steady cost after windup is 4.6875 mana/second.
- At higher ranks, use the pinned per-invocation cost `(24 + rank - 1) / 64` mana. Continue supplying one accepted start callback immediately before each due source sequence action; repeated delivery at the same frame must not charge or rewind twice.
- Release immediately cancels the channel/lease and stops new flames. Already-fired flames retain their existing independent stream host/lifetime.
- A new press after release starts a fresh windup and entry threshold. Do not enter the unresolved early double-rewind path.
- Resource failure stops the channel. Rank/cost changes affect the next invocation; emitted packets retain their captured state. Held input must not replay after disconnect or restart.

The existing controller/resource/sequence/stream components and the accepted 12 focused / 185 retained prototype markers are reuse evidence. They do not establish modern D2R input chronology. This approval authorizes gameplay integration; it is not a claim that learned press/hold/aim/release, natural scheduling, cancellation, raw client routes or new full-class acceptance have already passed. Existing Slow Missiles and owner/PvP/geometry/client limitations remain explicit until their own work is verified.
