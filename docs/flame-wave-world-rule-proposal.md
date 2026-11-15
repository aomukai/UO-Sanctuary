# Flame Wave: complete Sanctuary candidate

Mechanical reference: **D2R Reign of the Warlock 3.3.93847**, skill 398 and owned missile rows 708/690/695/716/240/29. Sanctuary decisions take precedence. The newer installed client is not a substitute reference. Current do156/42/38 callbacks remain unavailable; this document proposes their world composition explicitly. **Approved by the user on 2026-11-15 JST.** [Exact decision](../tools/reference/flame-wave-world-rule-approval.json). The body below records the approved proposal; current learned acceptance is in [the checkpoint](flame-wave-cast-checkpoint.md).

The candidate uses the existing Warlock SC/FCR action clock, fractional mana service, source projectile geometry, typed fire damage, temporary-effect clock, safe-area detection and death/class cleanup. It adds one saved graph profile, with no new timer, client patch or deployment. Verification results will be recorded in the accompanying checkpoint before requesting approval.

## Action and capture

Require a living Warlock/Witch, trained skill, valid ground/target direction, normal range and LOS, permitted form, and an active combat facet outside safe areas. Casting at your own exact tile fails without payment. SC entry reserves no mana; release rechecks eligibility and pays the pinned fractional cost once: 7 mana at effective rank 1, plus 0.5 per additional effective rank. A successful release starts the pinned 15-frame cooldown. Insufficient mana refuses this spell. Movement, weapon changes and ordinary interruption cancel unreleased work without payment; released work is independent of later movement or gear changes. Later failure does not refund a completed release.

Capture effective rank, hard Ring of Fire/Apocalypse points, fire mastery, other existing fire modifiers, Slow Missiles and a per-cast RNG stream at release. Aim and the perpendicular wall remain fixed. Existing released casts survive respec; death, deletion or class departure clears them. **Magical Penetration is read live at contact, as in the shared player-fire services**; this is not a summon/trap snapshot claim. Target defenses remain live. Head damage uses the captured slow payload; rows 695/716 have CanSlow=0, so ground damage is not slowed.

## Revealing and travelling wall

708 is a non-damaging revealing controller in this adaptation. It visually extends two source-subtile lanes to either side per 40 ms update until the captured half-width is reached. Its table velocity, initial steps and collision flags are retained in the owned reference, but are not invented as another travelling damage layer. At update 17, it releases the complete perpendicular row of 690 lanes. Width is `2 × captured half-width + 1`, with half-width following the pinned curve (4 at rank 1, 6 at rank 20, cap 7).

Each lane uses the existing fixed-point direction/velocity bridge, the pinned velocity 24 modified by captured Slow Missiles, and 40 travelling updates including the launch update. Source-subtile crossings find targets and check walls, LOS, bounds and barriers; the continuous endpoint is checked too. A blocked lane stops independently. Monster contact does not stop a lane. The front deals the pinned skill fire packet, with one 15-frame recipient delay **shared by all lanes within that cast**. Different casts have independent delays. Direct contacts use ordinary shared missile avoidance, typed mitigation and recovery; no weapon damage, wear, ammunition or weapon procs are invented.

The exact modern reveal origin, spread order, 17-frame binding, direction rounding and delay identity are adaptations, not recovered callback behavior.

## Tail, static fire and child damage

A changed source-cell crossing emits paired 695 tail and 716 static makers, reusing the old do6 changed-path creation condition. Repeated live makers of the same type at the same cell within a cast coalesce without extending their expiry. Each maker represents a same-position 240 burning child with its captured parent field packet and expiry:

- Tail lifetime: **8 frames**, interpreting the 7-frame row plus wave Param1=1 additively; this override is explicitly proposed.
- Static lifetime: **75 frames / 3 seconds**, the 7-frame row plus Param2=68, independently supported by the pinned skill description.
- Child 240 supplies the existing typed fire return path, size 2, soft-hit draw and damage-rate 41. It inherits the Warlock field endpoints and maker expiry; it does **not** import Meteor's own 90-frame lifetime, Sorceress Inferno synergy or Meteor damage growth.

695/716 use their own pinned elemental progression and 10% per hard Ring/Apocalypse synergy, distinct from the front's 15%. Fire mastery is applied once through the shared endpoint helper. Makers do not also apply an independent damage packet. All overlapping makers/children within a cast produce **at most one ground-fire packet per recipient per update**. Independent paid casts may each burn that recipient.

A newly emitted child first burns on the following update. It cannot burn at or after its absolute expiry. The front and ground may both hit on the same update; the front's delay does not suppress ground fire. Row 29 is an identified wall/termination visual placeholder only, without an invented explosion hit. Children keep their original expiry when lanes end. These inheritance, overlap and first/last-hit rules replace unresolved modern composition explicitly.

## World, lifecycle and saving

Ground and lanes stay where cast. Logout, a different facet, owner height separation or an actual safe area suspends attacks/emission while UTC lifetimes keep running. Return may resume remaining lanes and fields without healing, repayment or timer reset. Missed motion, burns and reveal/launch events are skipped; a launch missed while inactive is not replayed. Ordinary connected server updates advance due 40 ms stages in order. Same-facet owner movement does not drag the fixed wall or fire.

Profile `SanctuaryFlameWave` (255, version 0) saves cast identity, birth packet, RNG, stage frame/deadline, lane geometry/expiry, paired maker/child positions/expiry and front leases. Restore validates the full graph before committing, rejecting malformed births, clocks, positions, duplicate identities and identity rollback. Unreleased actions are transient and are never replayed after loading. Existing FireGround persistence stays separate and retains its previous schema.

## Scope and missing assets

Ordinary supported NPC combat is the tested target. Unsupported native incoming/death-delay/uninterruptible contexts remain explicit refusals or reported ground-field gaps; this does not certify PvP, every special monster, all item properties or the whole class. No current protected code was reread. Effects are temporary UO reveal/front/ground/termination flames and cast sound, clearly identified in code. Final revealing-wall, rolling front, tail, static fire, impact and audio assets remain in the separate [effects register](skill-effects.md); tree/hotkey icons remain in [icons](skill-icons.md).

On approval, enable learned dispatch and the automatic world hook, then verify actual left/right/quick input and reloads before counting another handler. Current coverage remains **232/240 handlers, Warlock 23/30; no class certified**.
