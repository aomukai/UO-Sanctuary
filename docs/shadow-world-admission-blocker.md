# Shadow world and action admission

Reference: **D2R Reign of the Warlock3.3.93847**, approved Sanctuary deviations first.

An explicit stationary `DefenseProfile` now admits the original Shadow resource graph to world maps for incoming defense and existing original-item services. Both species reuse the shared avoidance, accuracy, ordered melee/missile HP, item callbacks and scalar/duration defenses. Unqualified legacy bodies retain their map guard. Original source identity, owner/items/skills/passives, wounds and actor/world RNG remain intact. See the [verified defense checkpoint](shadow-defense-checkpoint.md).

Automatic activation and learned Shadow casts remain unavailable: `CanActivate=false`, stopped AI and rejected intrinsic melee are still intentional dependency safeguards. Next provide actual NPC skill permission/target/mode/action events, movement and own AI105/106 selection, then qualify current modifier42 death handling. The older AI routines are in `D2Game/src/AI/AiThink.cpp` (Warrior starts at13969), not `AiTactics.cpp`. Reuse existing startup/do dispatch, lob/projectile/elemental services and ownership/parking infrastructure. Do not proxy a player cast or give Shadows Valkyrie identity/AI.

**Approved Shadow Weapon Block adaptation,2026-11-08:** the current owned monster table gives both Shadows `BaseW=hth`, empty shield-component lists and no ShieldBlockOverride despite ToBlock50. The older monster composite routine returns BaseW and its avoidance reader requires HT2(13) for Weapon Block. Current special composite behavior is unverified. The user approved using the actual original active hands: two claws supply13, other combinations0; use the posted skill chance/type layer, saved actor RNG and shared source walk/run behavior. No shield is fabricated. This policy is implemented and tested; it is not a claim of verified current D2R composite behavior.

Native checking identified a separate resolved integration error: public item lineNumber is not the compiled skill type ID. Owned compiled skill fields establish claw layer68; the owned linker table now resolves it to h2h instead of the unrelated public-line type. Both defense and NPC mastery use that shared compiled lookup. Source provenance and exact checks are in the checkpoint/receipt above.

Shadow appearance/body/equipment/effect/audio placeholders and pending talent/hotkey icons remain separate from these mechanical dependencies. No class is certified complete.

Intrinsic Fire Blast connection2026-11-08: both actual Shadow skill books now reach the common startup/do dispatchers and existing persistent lob/landing/explosion/NPC damage services. The release-event caller is explicit; autonomous animation/action, other intrinsic skills and AI remain unfinished. Exact current source-removal behavior is still bounded by harmless dormant expiry. See [verified checkpoint](shadow-fire-blast-checkpoint.md).

S2 conductor2026-11-08: a source-timed transient action now drives the verified intrinsic Fire Blast release/end events and cancellation. Owned state119 provides Assassin transformation; raw monster BaseW alone therefore omitted an important conversion step in the earlier Weapon Block comparison. Approved dual-claw behavior remains. Automatic AI,ground movement,other intrinsic skill consumers and current death handling still gate learned summons. See [verified action checkpoint](shadow-actions-checkpoint.md).

Ordinary fallback attack2026-11-08: both Shadow species now share the original full weapon filler/contact receiver with Valkyrie,including own typed Claw Mastery and item callbacks. Their own converted A1/kick clock drives marker-only allocation and source range checks. This closes a prerequisite used by both AI fallbacks. Ground movement/own AI and other intrinsic skills remain unfinished;see [verified melee checkpoint](shadow-melee-checkpoint.md). The older UMod42event table identifies ColdEnchantedModeChange/CastNova,but current death parity remains unverified.

## Warrior movement/AI update — 2026-11-08

Own AI105, shared ground transport, original melee and Fire Blast handoff are now verified in qualified internal fixtures. Master AI106, other intrinsic consumers and current death behavior still gate learned activation. The approved actual-dual-claw Weapon Block routine observes the owned movement mode; walking/running do not roll it. See [checkpoint](shadow-ground-checkpoint.md).

Own Quickness/Fade consumers now use source SC events and actual timed stat lists, with group-2 replacement, own numeric/curse effects and preserved expiry. Warrior AI selection is verified. Venom and other intrinsic consumers, AI106, death and learned casting remain. See [buff checkpoint](shadow-buffs-checkpoint.md).

Venom's own state-31 list, captured mastery, shared SC cast and actual original-claw poison delivery are now verified. The shared filler/poison engine needed no changes. Other intrinsic consumers, AI106, current death and learned casting remain;see [Venom checkpoint](shadow-venom-checkpoint.md).

Psychic Hammer now has own SC/start22/do33, typed physical/magic HP/death/knockback and Warrior AI coverage. A retained Venom loadout hit an event-history reconstruction mismatch once; it did not recur in the next cohort. The guard remains intact with richer diagnostics, and reproducing/fixing that case is an additional full-activation gate. See [Hammer checkpoint](shadow-psychic-checkpoint.md).
