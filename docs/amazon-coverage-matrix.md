# Amazon acceptance map

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. Charms and runewords are excluded. All 30 talents have handlers; the following existing checks establish only their named scopes, not a completed class or full modern executable parity.

The [current driver](../tests/check_amazon_planned_acceptance.py) combines existing skill fixtures, the six-fixture original Valkyrie casting cohort, shared weapon-rate/chain/Slow Missiles checks, old real Cast packets and current right/quick input routes. The combined native run passes 334 exact results, 72 real cast inputs and 84 connections across three boots/saves and two reloads; [receipt](../tools/reference/amazon-planned-acceptance-native-evidence.json). This is scoped acceptance, not a full class certificate. Cached reference files and reused gameplay/integration binaries avoid repeating source compiler matrices.

| Talent | Mechanical scope | Existing fixture |
| --- | --- | --- |
| Jab | Paid three-strike sequence, melee javelins, wear, interruptions and saved resources | `jab` |
| Power Strike | Prepared melee plus separately mastered lightning, AR, hard synergies, cancellation | `power-strike` |
| Poison Javelin | Paid throw, quantity, parent/cloud poison, independent saved flights | `poison-javelin` |
| Impale | Uninterruptible guaranteed-accuracy strike, special wear/depletion, slow and expiry | `impale` |
| Lightning Bolt | Paid javelin conversion, independent item callbacks, quantity and travel | `lightning-bolt` |
| Charged Strike | Melee contact then captured bolts, natural expiry, Slow Missiles launch-speed snapshot | `charged-strike`, `charged-strike-velocity` |
| Plague Javelin | Release-time payment, poison burst/moving clouds, quantity, snapshots and saved delay | `plague-javelin` |
| Fend | Paid multi-contact series, live targets and per-contact stats, actual connected loop | `fend`, `fend-loop` |
| Lightning Strike | Approved melee lightning followed by chains from the struck monster; no chain when alone | `lightning-strike`, `chain-child-search` |
| Lightning Fury | Paid throw, child fan, source-zero bolts and saved parent/children | `lightning-fury` |
| Inner Sight | SC cast, defense debuff, duration/radius and saved recipients | `inner-sight`, `amazon-cast-clock` |
| Critical Strike | Shared physical critical ordering, actual gear and saved passive | `amazon-critical` |
| Dodge | Standing melee avoidance, gear, active attack continuity and saved passive | `amazon-dodge` |
| Slow Missiles | Approved SC/FCR release, priorities, launch speed/damage and supported native projectiles | `slow-missiles-cast`, `slow-missiles-component`, `slow-missiles-bolts`, `slow-missiles-rules`, `slow-missiles-windup`, `slow-missiles-learned` |
| Avoid | Standing ranged avoidance, real enemy firebolts, gear and saved passive | `amazon-avoid` |
| Penetrate | Additive AR, aura coexistence, gear and saved passive | `amazon-penetrate` |
| Decoy | Snapshot stats, paid cast, natural expiry, saved injuries and disconnect cleanup | `decoy-component`, connected `decoy` callback |
| Evade | Actual walking/running avoidance, real attacks, gear and saved passive | `amazon-evade` |
| Valkyrie | Original initialized body/equipment, source AI/contact, SC payment, parking and preserved roster | `valkyrie-initializer`, `valkyrie-ground-combat`, `valkyrie-startup`, `valkyrie-town`, `valkyrie-buffs`, `valkyrie-cast` |
| Pierce | Effective skill plus real item chance, launch budget, flight eligibility and saved passive | `amazon-pierce` |
| Magic Arrow | Fractional release payment, no ammunition, physical-to-magic conversion and saved flight | `magic-arrow-cast` |
| Fire Arrow | Start payment, ammunition, fire conversion/synergy and saved flight | `fire-arrow` |
| Cold Arrow | Start payment, ammunition, cold conversion, chill and state expiry | `cold-arrow` |
| Multiple Shot | Existing Teeth fan reuse, one payment/ammunition, central flags and saved fan identity | `multiple-shot` |
| Exploding Arrow | Travelling parent, fire-only area children, miss/block/wall/lifetime contexts and saved stages | `exploding-arrow` |
| Ice Arrow | Paid parent, cold damage/freeze, thaw delay and saved state | `ice-arrow` |
| Guided Arrow | Captured weapon packet, selected/ground acquisition, homing, no Pierce and saved modes | `guided-arrow` |
| Immolation Arrow | Paid parent, fire area plus disc fields, cooldown, quantity and saved stages | `immolation-arrow` |
| Strafe | Paid bow series, rate rollback, target selection, series-local delay and saved flights | `strafe` |
| Freezing Arrow | Paid parent, typed cold/freeze-only area, difficulty/resistance, thaw and saved stages | `freezing-arrow` |

The fourteen existing bow/javelin Cast packet controls run once per boot; Decoy retains its separate real Cast/expiry/socket-loss sequence. Current packets cover Amazon SC right/quick, Lightning Strike right/quick, original Valkyrie right/quick and Slow Missiles right/quick/FCR. These scopes do not claim every input mode for every talent. General binding restrictions and saved histories are exercised by individual fixtures.

## Approved rules and remaining limits

Lightning Strike uses the explicitly approved chaining adaptation; the hidden first-bolt/primary re-hit path remains unresolved. Slow Missiles uses its approved recipient, damage/speed and scoped native-projectile rule; instantaneous legacy UO ranged hits, old spell/breath timers and broader monster spell conversion remain separate.

Original Valkyrie snapshots, equipment/resources/RNG, safe-area parking without recovery, persistent injuries and source-owned combat retain their existing dedicated proofs. Her shared native two-tile placement is a Sanctuary mapping, not a recovered D2 room-search order. Unsupported item/proc/foreign-class or injected nonzero Sunder contexts remain guarded; charm-only Sunder acquisition is excluded from planned content.

Other explicit limits include current animation/IAS/skill-AR transfer, RNG/stat/owner/PvP/event equivalence, native room/grid/collision order, initial cloud contacts, Strafe cross-cast delay, special quiver/repair/stack cases and ordinary cold corpse-shatter behavior. See each talent's [status ledger](../data/Sanctuary/Balance/class-skill-status.json) and individual source/native receipts for the bounded claims. Existing tests must not be reinterpreted as full current-executable or graphical-client acceptance.

Final [effects/audio](skill-effects.md) and [talent/hotkey icons](skill-icons.md) remain separate, using identified placeholders. No artwork or deployment is included in this mechanical checkpoint. Installed build/saves/configuration/client protection remains required.
