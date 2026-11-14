# Goatman Frenzy and Cleave action proposal

Status: **approved Sanctuary adaptation** — “Approve this Goatman NPC action rule.” Decision: `tools/reference/goatman-npc-action-approval.json`. Reference: owned D2R Reign of the Warlock **3.3.93847**. This proposal covers the NPC action boundary; it does not claim recovered current startup67/68 behavior or a complete learned summon.

The pinned Goatman learns its own Stun at effective summon rank2, Berserk at3, Frenzy at4 and Cleave at5. These are the monster rows416–419, not the player skills. Source evidence and focused acceptance are recorded in `tools/reference/goatman-npc-action-native-evidence.json`.

## Proposed action rule

- Each AI selection runs one action. Frenzy uses the owned `GMA22HS` secondary attack:16frames, contact at8 at base speed. Cleave uses `GMA12HS`:20frames, contact at12 at base speed. Choosing these modes for these two NPC skills is an adaptation; the animation records themselves are extracted from the pinned archive.
- Capture the chosen NPC skill/rank, initial enemy, aim, origin and attack clock at entry. The host supplies the actor’s source attack rate, including its captured birth bonus and current buffs/slows. The actor’s own Frenzy bonus is added once; Cleave adds its own `calc3` speed. Use the existing source scheduler and final15–175 attack-rate clamp. Changes during windup affect later actions.
- NPC actions spend neither demon nor owner mana. Each release rolls ordinary NPC contact/damage with its own source row. Neither action adds player synergies or uses the already-enhanced birth endpoints for another damage multiplier. Frenzy adds no skill damage%; Cleave adds5% at rank1 and one percentage point per additional rank.
- One successful, unblocked Frenzy contact immediately grants one charge, capped at the captured NPC Frenzy rank. This deliberately defines the unavailable current success-latch boundary. Reuse the existing Frenzy stat writer; the monster’s `dm34` supplies movement, attack and other-animation speed, and its duration is200+25×(rank−1) source frames. A miss gives no charge or refresh. Reads/parking do not refresh the deadline.
- Cleave reuses the existing source fan:11of30slices, locked aim, captured origin and source reach+4. At release the host supplies current candidates and source eligibility/range/LOS/safety. Process distinct eligible enemies in distance/serial order through the shared ordered NPC contact pipeline, once each. This explicitly adopts the same direction math and contact ordering as the approved player Cleave adaptation.
- Movement, facet change, death, interruption or an inactive owner/body cancel later work. Losing the initial target before release cancels the action. Mark a release claimed before callbacks; repeated/late ticks and nested callbacks cannot duplicate it. Cancellation during a fan stops later recipients. A backward source clock is rejected; unsupported callbacks fail visibly.

## Implemented and remaining

The candidate includes its action scheduler, shared Frenzy writer, own-row packets and reusable Cleave fan. It has no installed timer, automatic AI registration, learned summon route, new persistence ID or serialization change. The host must retain one Frenzy state per demon and bind its speed to movement/attack/other-animation consumers. Durable buff state, native world path/clock/AI/action/event adapters, demon cap/casting and full summoned-body acceptance remain unfinished. The explicit test world leaves do not establish those contracts.

Effects/audio and icons remain in the separate existing Summon Goatman registers. No new art is required to finish the mechanical host. Installed binaries, configuration, client patches and saves remain protected.

## Verification checkpoint

Content and integration builds pass with zero warnings/errors. Eighteen native markers pass across three disposable boots, two reloads and three saves. Each boot checks510 own-row packets,1530 schedules and255 charge caps, real shared HP commitment, fan ordering/deduplication, nested release/cancellation/freeze, misses, movement, inactivity and exact expiry. The existing24000 Stun/Berserk packet comparisons and15024 older Frenzy service comparisons also pass. Reloads preserve the baseline original-item/resource fixtures; the new candidate actions/buffs are deliberately transient and are recreated on each boot, so this does **not** certify their persistence.

One failed first run is retained: the fixture initially sent a miss into the success-only HP commit entry. Correcting the fixture to call the existing attempt/finish path resolved it; no production damage fallback was added. A further interruption guard stops remaining Cleave recipients if a callback freezes the actor. Reviewed candidate artifacts: content build2, integration build3, source snapshot4, native run2. The approved flag and its assertion were then rebuilt as content3/integration4, source snapshot6, native run3; the production behavior differs from the reviewed candidate only by its approval gate.
