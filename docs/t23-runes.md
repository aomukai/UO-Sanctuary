# T23 — Inscription makes the declared rune

Use **Craft rune from mould** on a scribe's pen at a public Inscription desk. Choose a mould in your backpack, review the declared stat, bark plan, success chance, quality odds and failure loss, then confirm. Inscription must be the active craft. First-profession confirmation, station access and native crafting action/timer checks remain in force.

A successful recipe consumes the selected mould, the listed number of suitable Runebark pieces and one pen use. Bark of the same or a higher tier is accepted, including mixed stacks in nested bags. The lowest suitable tiers are selected first and shown before commitment. Higher bark does not change the rune's stat, tier or value.

| Rune tier | Required trained Inscription | High Quality eligible | Superior eligible | Bark pieces |
| --- | ---: | ---: | ---: | ---: |
| Crude | 30 | 40 | 50 | 1 |
| Rough | 40 | 50 | 60 | 2 |
| Shaped | 50 | 60 | 70 | 3 |
| Etched | 60 | 70 | 80 | 4 |
| Refined | 70 | 80 | 90 | 5 |
| Polished | 80 | 90 | 100 | 6 |
| Flawless | 90 | 100 | 110 | 7 |
| Perfect | 100 | 110 | 120 | 8 |

Confirmed on 20 October: use the ordinary native Inscription success curve from the required skill through 50 points above it, with normal skill-gain checks on committed success and failure. Native modified skill and success modifiers affect success chance; trained Base skill controls the recipe gate and quality eligibility. The ordinary curve has zero success chance exactly at its minimum without modifiers, so an attempt needs a positive chance. Chance is capped at 100%; no resources are spent on a rejected zero-chance request. For example, unmodified skill 120 gives a Perfect rune recipe 40% crafting success.

Quality is rolled only after success. Before required+10, all runes are Average. From +10 to below +20, outcomes are Average 90% / High Quality 10%. At +20 or above, they are Average 89% / High Quality 10% / Superior 1%. These outcomes are mutually exclusive. The mould's stat and baseline are never rerolled:

`percentage = mould baseline + (mould tier − 1) + quality bonus`

Quality bonuses are Average 0, High Quality 1 and Superior 2 percentage points. A Perfect Rune of the Apprentice with a 10% Faster Cast Rate baseline gives 17/18/19%. The rune freezes the mould's tier, canonical prefix/suffix, semantic stat, effect code and baseline; ordinary dropped and imbued equipment retains its independent affix value rolls.

**Confirmed failure loss:** consume the mould, half the required bark rounded up, and one pen use. There is no rune output. Crafted-tool charge saving applies to either result. No gold, essence, blank scroll or reagent is charged. A depleted pen can finish its last committed attempt and then breaks normally.

Cancellation, disconnection, stale confirmations, changed or moved inputs/tool, profession or trained-skill changes, dead/frozen characters, missing ingredients and loss of station access spend nothing. The transaction rechecks the quoted inputs after the native crafting timer. Capacity is planned after successful input consumption: a full backpack can replace its consumed mould with the rune if the exchange fits; an overfull or otherwise invalid exchange rejects before rolling or consuming anything.

Runes are persistent, nonstackable, known-stat items with quality and exact percentage tooltips. Native copy, transfer and world reload preserve their identity. They do not count as equipment, receive no random affix roll and apply no bonus by themselves. Permanent insertion and filled-socket requirements are implemented in [T25](t25-rune-sockets.md); mining gems and upgrades are implemented in [T24](t24-gems.md). This card adds no new monsters, vendor supply or native engine/client changes.

Recipe tuning is in `data/Sanctuary/Balance/rune-recipes.json`. `RuneCrafting.cs` uses the existing native crafting transaction hooks; `RuneCraftGump.cs` provides the guarded pen/target/confirmation flow, and `InscribedRune.cs` owns the saved item identity.

Verification covers every tier/stat/quality combination (360 outputs and 720 saved original/copy identities), all quality probability boundaries, exact success/failure costs, mixed higher bark, native gain bands/caps/locks, final pen use, charge saving, full-pack replacement/rejection, moved/stale/foreign inputs, forged native requests and replay. The actual ClassicUO client exercises the pen button, mould target, cancel, confirmation, native timer, a Superior Perfect 19% rune and exact costs. Success/quality tickets are seeded only in the disposable Integration fixture; production has no test setting.

Completed and deployed on 20 October 2026 (JST). Content/integration builds have zero warnings or errors. Source and installed four-boot cumulative suites, separate legacy migration, all 24 Python checks, native patch round trips, copied-world preservation and actual-client checks pass. Live saves/restart and final preservation evidence are recorded in the local deployment manifest. The server engine, native content, client and world populations are unchanged by this card.
