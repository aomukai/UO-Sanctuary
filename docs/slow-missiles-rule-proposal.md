# Slow Missiles: complete proposed Sanctuary rule

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations first. The current do6/second-stat and precise damage/factory ordering remain unavailable. **Pending approval; public learned input stays gated.** Existing legacy-source comparisons and firsthand edited-game observations support the components but do not establish current executable parity.

## Action, state and targets

Use the existing owned Amazon SC/FCR clock and shared action session, with no inferred one-frame correction from the older frame-role ambiguity. This is a right/quick, caster-centered spell. Require a living eligible Amazon outside town, a trained skill, permitted shape and sufficient mana at entry. Windup is unpaid. Movement, weapon changes, interruption, frozen/paralyzed state, lost eligibility or Stop cancels it without payment. At release, retire the pending action first, read current effective rank and pay **5mana once** before searching. Empty searches and searches finding only stronger effects still pay. Late insufficient mana refuses release; no basic attack is substituted for this spell. Ordinary animation recovery remains in force.

Use the pinned 27-source-subtile radius, squared center distance, height difference below16, town exclusion and source filter0xC583. Its optional LOS bit is absent: do not add a LOS gate. Preserve shared living hostile/attackable/non-NPC/no-aura eligibility. A mapped source species with Boss=true is excluded; do not reinterpret ordinary champion/unique grade as that species flag. The existing explicit EncounterCreature adapter is retained.

For the existing **GraveyardLich**, add an explicit native classification: ordinary/nonboss, attackable, not NPC/no-aura. This describes Sanctuary's creature and does not assign it a D2 species identity or alter other skills' target rules. An explicit native Boss trait overrides the ordinary classification. Unknown native types remain skipped and are reported rather than acquiring an invented identity or default. Adding another native type requires its own qualified classification.

Duration is300+150×(effective rank−1)frames. Existing rank curves supply independent velocity and damage percentages: rank1 is68%speed/94%damage,rank20 is33%/67%. Weaker ranks cannot replace stronger live states; equal rank refreshes expiry while retaining original creator/captured values; stronger rank replaces. Target death/deletion clears the state. Caster death,logout or class changes do not recall already released debuffs. Expiry continues during downtime with no reset; expired/dead recipients are pruned on restore. Reject non-UTC clocks atomically before changing any recipient state. This preserves profile149/version0.

## Projectile and damage rule

At each supported missile creation, capture the affected **actual firing creature's** state. CanSlow controls velocity only; damage reduction also applies to a non-slowable missile. Use the existing fixed-point factory operation: scale source/supplied FP8 velocity by the captured velocity percentage, then apply its normal75%factory conversion. Preserve the original missile lifetime. Do not retroactively mutate already-created missiles when the state is refreshed,removed or expires.

For damage, adopt the existing deterministic signed-FP256 integer rule: multiply by the captured damage percentage and truncate once, before defender mitigation. Keep the slow multiplier separate from the additive Weaken/Taunt/Battle Cry pool. Preserve cold/poison lengths,poison count,drain coefficients and unrelated melee/DoT events;ordinary damage-derived recovery still uses the resulting reduced damage. Do not introduce a global received-health multiplier.

The existing factories have two explicit packet boundaries:

- Graveyard firebolts capture and scale unscaled fire endpoints at creation, then use the usual missile damage roll and typed impact. They retain their source vector,range,terrain and actual collision; no homing or visual-only damage timer is invented.
- Encounter arrow/fire/lightning paths capture the payload at launch. Their final native mixed physical/fire/cold/lightning/magic/poison packet is scaled once after its native damage/critical/weapon terms and before defender mitigation. Poison uses the existing rate/count representation,not a changed duration or a lossy total-only scalar.

The existing encounter one-tile-per-frame/18-frame baseline is retained as its declared native geometry adapter. Captured velocity ratios implement this skill's slowing;this is not a new claim that the encounter baseline equals D2's absolute missile range or velocity.

A new child constructed from an unscaled packet captures its actual owner state at that child's creation; a child inheriting an already-scaled packet must not multiply it again. Each producer must identify its boundary rather than assuming every child callback behaves alike. The unlinked prepared raw float-packet helper is not silently treated as a verified Slow Missiles dispatcher. Exact current global ordering/rounding and unqualified child contexts remain explicitly uncertain.

## Scope decision: current D2 combat versus legacy UO combat

**This proposal supports the current D2 encounter arrow/fire/lightning paths and the Graveyard firebolt path.** Other class/NPC adapters already expose captured slow fields,but that does not qualify every special producer or PvP target. Unsupported paths remain listed and must be tested as they are integrated.

Your instructions put replacing monsters' UO spells with D2 spells on a separate future track. This proposal keeps legacy UO instantaneous ranged hits,MagicArrow/Fireball/EnergyBolt-style native spell timers and dragon breath on that conversion track,including any flight bridge they need. They are **not claimed as slowed or certified by this skill checkpoint**. Keeping instantaneous UO ranged attacks on that same track is an explicit scope choice requested here; it is not presented as a fact established by your spell-replacement instruction. If those legacy producers must participate before this skill is released,that bridge must be implemented before learned admission instead.

The supported D2 factories use real motion and impact damage. No visual delay or broad AOS hook pretends to supply missing travel,lifetime,CanSlow,owner or child semantics for legacy producers. This boundary must remain visible in the monster-combat roadmap and future producer acceptance,not disappear behind a handler count.

## Verification and presentation

The complete prepared candidate is tested alongside the accepted Abyss/Entropy/Flame Wave/Chain/Bolt/Mirrored cohorts and the existing Slow Missiles windup fixture. The connected rules fixture checks caster payment/cancel and actual released native/encounter state,known-rank goldens,stronger-state priority,real firebolt endpoints/velocity/travel/HP impact,encounter launch capture,six damage channels,unchanged lengths,CanSlow0damage,atomic UTC restore and original state/resources through two reloads. Public Slow Missiles socket inputs and final graphical acceptance remain unverified until this decision is approved. Final accepted hashes/counts and failed diagnostics are in [the checkpoint](slow-missiles-rules-checkpoint.md).

No timer,profile,serialization version,client/kernel build,new live reference read or deployment is added. Final cast/affected-state/slow-projectile/audio effects and talent/right/quick icons remain separate missing assets;current native markers and sound are identified placeholders. This is not a whole-class/special/PvP/property certificate. On approval,wire learned right/quick input and verify it through actual packets,released state and current D2 missile damage before counting the handler.
