# Class skill reuse notes

Reference: D2R **3.3.93847, Reign of the Warlock**, with Sanctuary's approved deviations taking precedence. These notes identify reuse candidates; they do not certify unimplemented skills.

## Before starting each mechanic

Check existing skills with matching behavior, their shared helpers, tests and evidence before adding an implementation. Record the closest match and any contract differences in the skill's checkpoint. Reuse the matching component and its relevant regression coverage; add a new component only for behavior the existing one cannot represent correctly.

Shared appearance alone is insufficient: compare targeting, damage snapshots versus live values, payment, hit-delay scope, timing, ownership, save/reload and removal rules. Keep uncertain differences explicit rather than copying another skill's policy by accident.

For example, Teeth already uses Multiple Shot's integer projectile fan and Bone Spear's saved flight controller. Bone Armor uses the shared FP256 absorption-pool arithmetic, while its unresolved breaking-hit/event behavior remains a separate reference question. These existing components are the starting points for related work.

## Blade Sentinel and Miasma Chain

The user highlighted Miasma Chain as a future beneficiary of the Blade Sentinel work. Preserve that connection when the roadmap reaches Warlock/Witch.

The pinned rows distinguish their actual routes:

- Blade Sentinel creates a blade actor, whose first AI evaluation creates a following collision missile (`pSrvDoFunc=20`, `pSrvHitFunc=37`). Its damage packet captures the player's weapon before ownership transfers to the blade. Movement and AI observe the source frame clock.
- Miasma Chain (`sourceKey=Miasma Chains`, skill399) uses skill do167 and `miasmachainsmaker`. Its missile rows declare maker do43, travelling link do45/hit41, a separate cloud maker do43 and a cloud field do41. Link anchor/length, attached-unit death and location options are separate table inputs. These new engine routes are not present in the older reconstructed source and still need current-build evidence.

Reuse the frame conductor, persistent effect identities and clocks, immutable damage packets, ownership separation, ordered collision/footprint helpers and contact-delay machinery where the actual contracts match. Miasma Chain has magic damage and link/cloud behavior; do not copy Blade Sentinel's weapon fraction, NPC attack rating, monster AI, reversal rules or lifetime merely because the skills look related. Keep link motion, target acquisition, cloud placement, duration and current hit-delay scope explicit until verified.

Source rows: [class skills](../data/Sanctuary/Balance/class-skills.json), [pinned missiles](../tools/reference/d2r-named-equipment/json/missiles.json). Original table provenance and hashes are pinned by [the mechanical reference](skill-mechanical-reference.md). Required art remains in the independent [effects](skill-effects.md) and [icons](skill-icons.md) registers.

## Blade Shield weapon-area callback

[Compiled source orchestration](../tools/reference/blade-shield-source-evidence.json) separates Blade Shield's gameplay from its cosmetic attachment. Each periodic area pulse rolls its skill damage once and copies that packet per target; each target then gets its own hit resolution, current weapon fill and successful-hit durability/event delivery. Reuse the established weapon damage, poison, critical, defense/block and hit consumers where their contracts match. Blade Sentinel's immutable first-missile weapon snapshot is a different contract and must not become a Blade Shield snapshot by accident.

The periodic driver keeps the cast rank in its state list, while hard synergies and weapon suppliers need their own live-source treatment. The attachment's `Half2HSrc` flag does not by itself modify the separate area callback's weapon fraction. Source-start mana payment, initial animation/pulse order and native saved self-state/input remain explicit implementation work. The independent skill-effects and skill-icons inventories continue to hold the required placeholders.

## Raven summon stats and blindness

Raven reuses the existing 111-row summon level table through `ValkyrieCreation.BaseAtLevel`. Its explicit `max(1, owner level - 2 + skill rank)` stays on the pet; only the stat-table lookup clamps. Valkyrie and the earlier default-level summons retain their existing level formula through the original wrapper. Do not duplicate the table or cap Raven's level to 99.

Keep birth stats separate from the monster attack-mode callback. The inspected source refreshes physical endpoints and flat skill attack rating from the owner's live Raven rank. Its first AI evaluation separately initializes the finite attack budget. Current animation metadata and source AI branches still require actor/event integration; neither a skeleton attack cycle nor a sentry firing sequence establishes Raven timing.

The local current-build Raven property supplies signed `stupidity=-1`. The ordinary player blind entry requires a positive bonus, while the prepared summon entry accepts any nonzero signed value and draws before downstream curse eligibility. Both share the existing blindness state and duration consumer. Prepared source/native comparisons and the explicitly named NPC proxy are recorded in [component evidence](../tools/reference/raven-stats-native-evidence.json); they do not enable a playable Raven or prove its flight, attack-event delivery or saved lifecycle.

Raven's clockwise/counterclockwise flight uses the existing toward-path kernel with direction offsets +2/-2 (path5/6). The shared initializer, three-direction collision order, repeated points and motion state now have dedicated source/native circle comparisons. Keep moving-unit repathing and room geometry separate from these zero-footprint point-target proofs. The user's pinned-build test also confirms that casting on another monster redirects the entire existing flock; preserve this owner-wide focus command when connecting the actor/casting layer.

## Skeletal Mage and sentry primary search

[Mage world integration](mage-world-checkpoint.md) reuses `AssassinSentryTargets.Select`: both call the same legacy source primary selector. `NpcRangedTargets` now holds their shared native enumeration/height/LOS/threat adapter. Stopped Mage think also reuses the existing stationary zero-lead unit-path probe; it is not a moving actor path initializer. Melee contacts remain in `UnarmedPetWorld`, while common owner/frame/load transport is in `NecromancerGroundWorld`. Mage AI/intrinsic criticals reuse the persisted Mage actor stream instead of a fake physical-equipment binding. Keep source-room/cache/event/seed and caster-removal gaps explicit; successful missile creation is not a guaranteed hit.

## Barrier death and sentry death entry

[Barrier death source review](bone-barrier-death-source-review.md) reuses the complete sentry cleanup/owner/death-end composition and adds nonempty minion allocation. Owner-graph changes precede dead collision; ordinary mode12 completion retains registry identity. Initial flags and barrier modifier subscribers are separate from the shared entry contract. A reproduced legacy missing-child null dereference stays unsupported. Do not infer actor deletion from death animation metadata or copy Fire Golem's160ms burst timer into barriers.

## Mage Enchant and intrinsic Frozen Armor

[Approved rank20 buffs](mage-pet-buffs-checkpoint.md) reuse Sorceress effect construction/storage and shared defense/freeze. Keep the NPC fixed-rank/no-synergy/no-mastery context explicit; a player casting wrapper would add incorrect class/payment/enhancement behavior. Intrinsic armor has no refresh timer, while Enchant remains an independent timed snapshot on its recipient. Combat-start scanning does not become continuous maintenance.

## Mage Static Field and player area damage

[Static integration](mage-static-checkpoint.md) shares one player/NPC receiver and explicit caster/allegiance targeting; a Mage area is centered at its own position. Keep player mana/class wrappers outside NPC delivery. Facet difficulty supplies the shared floor. Whole-HP source preparation and FP256 receiver life are distinct stages; legacy negative-resistance precompensation/current typed receiver order remain unresolved. A five-percent policy slot is not an implemented secondary: only routes with actual delivery/cooldown bindings may be available.

## Mage Chain Lightning and saved player flights

[Mage Chain](mage-chain-checkpoint.md) extends the existing saved flight/hop/cast-delay implementation with optional NPC source/seed/slow fields. Keep player live child synergies/mastery separate from fixed NPC values; NPC caster identity controls origin/LOS and damage attribution while the real owner supplies allegiance. The extracted Mage impact consumer preserves intrinsic-critical transport. Atomic restore protects unrelated flights; inspected copied resumption is not unheld save acceptance.

## Mage Glacial Spike and freeze-before-death

[Mage Glacial](mage-glacial-checkpoint.md) reuses the saved Mage ordinary flight and `BasicMissileUpdate`, shared vectors/source cells, Slow Missiles, area enumeration and per-recipient Mage spell/critical consumer. The ordinary Sorceress hit area shares the single raw roll and pre-HP freeze entry. Keep its older private flight's wall/expiry/save gaps explicit; sharing the impact consumer does not certify those paths. Native wall-center area LOS is an adapter, not a reason to invent through-wall damage. Manual fixture clock steps must select their own flight instead of advancing unrelated held save cohorts.

## Mage Meteor, Phoenix and fire fields

[Mage Meteor](mage-meteor-checkpoint.md) extends the existing `FireGround` warning/patch/cooldown stores with optional NPC identity/seed context. The same18-offset producer now serves normal player Meteor, Phoenix and NPC Meteor. Keep normal center's direct hit14 distinct from Phoenix center's hit4 child creation. Reuse the first-collider helper and NPC spell receiver with field damage-rate/no-recovery options, retaining the separate actor intrinsic-critical stream; fixed NPC spell contexts must not inherit real-owner Sorceress mastery, synergy, potion or mana policy. Per-caster cooldowns and copied fixture clock steps must preserve unrelated saved player/pet state. Do5 animation/collision-mask RNG and03SOFTHIT subscribers remain separate from field HP transport.

## Mage element migration and retained spell packets

[Three-element Mages](mage-elements-checkpoint.md) reuse the existing serialized element field and base load hook. Element changes require no rank/resource recapture, actor/controller replacement, roster commit, RNG or HP write. Keep already-fired packet element and ongoing poison state independent from the caster's current element. Plain historical types keep their own identity and AI until an explicit controller migration exists; changing a field does not silently grant the world subtype. Reuse existing cold/freeze consumers after conversion, and keep basic-damage/Mastery policy separate.

## Historical Mage controller adoption

[Original Mage actors](mage-historical-checkpoint.md) share `MageWorld` behavior and the existing ground conductor. Current typed actors keep their own serialized string; original types store optional ground records in existing Mage persistence. Reuse minion resume/sweep, seed, timer-suppression and spell services. Adoption is not a new birth: preserve FIFO order and resources, cancel unsaved native actions once, and keep the missing old clock/seed policy explicit. New births retain their original emergence sequence. Already-created packets remain independent from controller replacement.
