# NPC Open Wounds and Blood Golem’s hard-point unlock

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. This increment follows `6fc3c68`. Blood Golem now gains **100% Open Wounds at 10 hard points** through the shared NPC item event. Its 150% Clay damage foundation was implemented separately. **The hard-20 5% Life Tap level-5 proc remains pending. No class is complete; this candidate is not deployed.**

## Existing mechanics reviewed and reused

Before implementation, reviewed the named-item wound arithmetic/store, complete older function-15 callback/state factory, NPC item registry, prepared source/target damage ordering, poison fractional frame accumulator, signed item regeneration, and Blood’s captured damage/live-rank split. The [reuse review](../tools/reference/blood-pet-event-reuse-review.json) now identifies the enabled wound consumer separately from the pending skill-proc callback.

`NpcCombatEvents` accepts actual stat 135/function 15 alongside existing flat return. Original item chance and Blood’s live hard-point bonus aggregate into **one subscription and one RNG draw**; even a 100% chance draws before receiver eligibility. Potential Blood subscriptions survive preparation below the unlock so training/respec during windup is observed when the actual damage event fires. Soft points do not unlock it. Other unimplemented conditional item events still refuse preflight.

Delivery uses the real summon and the pre-health source damage event. Existing Blood healing precedes that event; target damage events and HP commitment follow. Target timed effects settle **before entering** the prepared hit, never from inside its wound callback. If overdue damage kills/removes the recipient, the prepared packet cancels without another wound draw, HP write or replayed event. Successful zero physical damage and full Bone Armor absorption still permit the source event; misses and shield blocks do not.

Receiver eligibility reuses the mapped source NPC/attackability policy, without copying curse resistance, Attract, aura or walk-only rejection. Unsupported unmapped recipients warn and skip. This bounded adapter is not proof of all native/PvP recipients.

## Exact NPC frame transport and persistence

New registered NPC wounds use a 40 ms clock and immutable integer FP256 rate in the existing timed actor. They reuse `PoisonStates.AccrueFrame`, `NpcEquipmentRegeneration.Delta` and its existing fractional-health receiver. One signed frame combines item Replenish Life minus poison minus wounds and any due spell-poison bonus; natural regeneration is masked. Prevent Monster Heal and signed item offsets retain the existing consumer’s rules. A wound frame avoids the legacy whole-HP `Damage`/recovery path.

The full unchanged older monster regeneration function executes **2,160 source contexts** under ASan/UBSan: poison/wound masking, signed replenish, prevent-heal, town/fractional thresholds, maximum health, creator priority and deferred-death inputs. Native arithmetic matches all 2,160 results. These source-service tests do not establish modern executable/global scheduler behavior or implement every tested deferred-death branch in the host.

The first application keeps its real creator, rate and clock on refresh; only its eight-second expiry resets. Durable owner credit is saved separately so a wound can kill after the summon has been deleted. While poison contributes a frame, its source has death-credit priority. A source-removed saved wound retains rate 41, phase 39 ms and its owner through both actual reloads; controlled copies verify exact-boundary death credit without native `Damage` calls.

An optional omitted-null `OpenWounds` JSON field extends the existing timed-effect record. Persistence stays version 4 and mobile field layouts are unchanged. Absent clocks retain the existing representation. Existing generic/player/legacy bleed states continue through their old receiver, and refreshing them preserves that first state instead of silently migrating it. Full player/NPC rollout and mixed legacy transport remain separate acceptance work.

## Verification and checkpoint

**Accepted build 2 / run 2 / audit 2:** both rebuilds have zero warnings/errors. The native world passes **313 markers across three boots, two reloads and three saves**, retaining all 307 predecessor marker scopes.

- Hard 9 plus one item rank refuses the unlock; live training/respec during windup enables/disables it. Source-event RNG and payment remain bounded.
- Miss, actual shield block, successful zero/physical immunity and full absorption controls pass. Overdue lethal settlement safely cancels the incoming contact.
- Actual original **Goreshovel 60% plus Blood 100%** produces one 160% subscription/draw; no synthetic item substitution is used.
- Twelve actual signed poison/wound/Replenish Life/prevent-heal controls, a near-zero survival edge and poison-priority lethal edge pass. The real replenish item is the shared original unique-134 fixture.
- The saved 39 ms clock survives both reloads after creator removal. Three natural connected learned-Blood sessions proc wounds, delete the summon, and continue to owner-credited death without a held combat clock.
- Four runtime audit markers, **71 Python tests, 573 named definitions and eight classes/240 skills** pass. Audit decisions remain unchanged with fresh 880/21 input fingerprints. Frozen source/data/DLL inventories match; runtime data, staged kernel and **106 installed hashes** are unchanged.

Attempt 1 used Swordback Hold for the item control, but that item fails existing metal binding eligibility. The retained failed fixture was corrected to eligible Goreshovel; production eligibility was not weakened. Its logs/inventories remain archived. Run 2 is the accepted run.

See [native evidence](../tools/reference/npc-open-wounds-native-evidence.json), [combined-regeneration source evidence](../tools/reference/open-wounds-regeneration-source-evidence.json), [callback source evidence](../tools/reference/open-wounds-source-evidence.json) and [checkpoint validator](../tools/record_npc_open_wounds.py).

## Remaining work and assets

Next: compose Blood’s hard-20 5% Life Tap 5 with the existing complete proc-target/item-cast route, real NPC/fixed-rank area-curse context, subscriber/RNG order and first-proccing-swing healing. Reuse the player Life Tap receiver and existing NPC spell-context services where their contracts match; do not add an after-HP substitute. Spirit Wolf’s approved Open Wounds can later reuse the new event/state consumer while preserving its cold-only damage.

Unmapped/native-fallback recipients, unsupported material replenishment, PvP/non-NPC delivery, protection/event/client/global-frame equivalence, dynamic uninterruptible/death-delay behavior, old-DLL upgrade and concurrent/unheld save acceptance remain explicit gaps. Existing poison life-floor/death/credit adapter limits are inherited. Held saved observations are distinct from the three natural combat sessions. Mage basic spell/Mastery mapping, other golem upgrades, barriers, parking, Druid and other classes remain unfinished. Coverage remains **186 handlers / Necromancer 30; no completed class**.

Blood body `14`, hue `0x21`, summon particles `0x376A` and existing contact/bleed cues are identified placeholders. Final wound application/pulse/expiry/source feedback and audio belong in the separate [effects register](skill-effects.md). Blood’s talent-tree/right/quick-hotkey artwork remains in the [icons register](skill-icons.md). The automatic unlock adds no player action or extra hotkey icon.
