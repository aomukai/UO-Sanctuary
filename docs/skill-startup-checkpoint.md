# Shared skill startup checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations taking precedence. This checkpoint implements the selected-skill startup wrapper and start dispatcher from the complete pinned older engine source. It complements the existing shared do dispatcher. **Current executable parity is unverified.**

The wrapper preserves enemy/pet/ally permission order, removes invalid or mismatched corpse targets, reads effective ranks, substitutes item-owned base ranks, rejects empty charges, and handles ally and town restrictions. The dispatcher preserves room and record checks, the separate server mana-admission service, item-cast bypass, all five collision masks, zero-coordinate behavior, start-table bounds, callback results, mana consumption and periodic-event deletion. A missing nested service throws; the kernel cannot turn an unimplemented effect into a successful cast.

This is a shared dispatcher, **not a live NPC adapter**. It does not impose the player's UI use-state checks on Shadows. The source server mana helper returns immediately for NPCs; its full player behavior remains a required host service. No existing player or item cast is rerouted through this kernel yet. Permission/collision hosts, nested start bodies, mode/sequence/action events, ordinary effects and combat, AI105/106, and learned Shadow input/payment still need implementation. Shadows remain parked.

Verification is in progress against Content 1 / Integration 1 / snapshot 1. Both C# builds pass with zero warnings/errors. The source probe executes both complete older routines over **6,796 contexts**, with ASan/UBSan clean; nested host services are declared inputs, not certified implementations. The isolated native suite passes three boots and two reloads. Wider regressions and the final receipt are pending.

The native startup test compares actual C# kernel results and ordered host calls to the compiled source probe, including absent and invalid start entries, negative/nonzero callback results, charged-rank replacement, cleanup and refusal before payment/events. Its saved digest proves stable test inputs only. Retained suites separately verify real original NPC bodies, equipment, wounds, resources and saved books.

There are **203/240 handlers**, no certified completed class and no deployment. Weapon Block rank 20 remains the approved **58%** adaptation, rather than a new D2R observation. Effects, audio and talent/hotkey icons remain separate work; this dispatcher introduces no new asset or placeholder. The existing Shadow appearance placeholder is unchanged.

Evidence: [source probe](../tools/reference/skill-startup-source-evidence.json), [scope review](../tools/reference/skill-startup-review-evidence.json). The execution state is selected by `.local/class-skills/current-checkpoint.json`.
