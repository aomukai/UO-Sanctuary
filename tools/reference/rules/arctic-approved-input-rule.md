# Approved Sanctuary Arctic Blast input rule

The user explicitly answered **“Approve this Arctic Blast rule”** on 2026-11-07. This separately approves the following Druid rule; it is not inferred from Sorceress Inferno's earlier approval.

Reference remains **D2R Reign of the Warlock 3.3.93847**. Exact D2R input timing remains unverified; the timing below is an approved Sanctuary adaptation.

- Require six mana for a fresh press, or the current invocation cost if higher.
- At effective rank 1, spend 0.375 mana on press and emit the first stream after 0.48 seconds. While held, renew every two 25-Hz frames for a steady 4.6875 mana per second after windup.
- Higher ranks use Arctic Blast's pinned invocation cost `(24 + rank - 1) / 64` mana.
- Release stops new emissions. A new press starts a fresh windup. Heartbeats renew aim/lease and cannot restart a released or failed gesture.
- Retain Arctic Blast's own cold packet, range, chill, Cyclone Armor synergy and DamageRate41. This approval does not substitute Inferno's fire values.

The prepared shared channel/sequence and cold stream are reuse points. Learned input, raw client routes, payment, cancellation, saved emission lifecycle and retained Inferno behavior still need verification. Current Slow Missiles damage ordering remains a separate gap. Effects/audio and tree/hotkey icons remain separately tracked.
