# Abyss: complete Sanctuary world/action candidate

Reference: **D2R Reign of the Warlock 3.3.93847**, owned skill402 and missile709/714/715 graph. Sanctuary approvals take precedence. Current missile do39 behavior remains unavailable. **Pending approval; learned input and automatic world execution stay gated.** This is one complete adaptation proposal, not a claim of recovered modern callbacks.

## Pinned facts and explicit choices

The real graph is709 `abysscenter` (do39/hit11), visual-only714 `abyss`, and terminal715 `abyssexplode` (do39, one-frame range). The center lasts50frames, has Param1/NextDelay12 and radius `min(7 + sksrc(Enhanced Entropy.lvl)/5, 11)`. Both stage radii have the same formula. The old complete hit11 body creates a HitSubMissile with owner/origin/skill/rank and makes an immediate hit only when HitPar1>0;709 has that value0. It supports a distinct delayed child stage without proving modern do39.

For Sanctuary, bind this particular `sksrc` lookup to **captured effective Enhanced Entropy rank**, including gear. This binding is scoped to Abyss; the general formula evaluator still refuses `sksrc`. Radius is7source subtiles at rank0–4,8at5,9at10,10at15,11from20. Use the shared two-source-subtiles-per-UO-tile bridge and existing source area/LOS filter. The tooltip's passive radius increase corroborates these thresholds; obsolete-looking passive damage/duration parameter labels add no invented effect.

Use each missile's own magic progression and its10%per hard Miasma Bolt/Chains point synergy, truncating in the existing missile order. At raw rank1,709 is20–40magic and715 is10–15; these grow with their pinned, distinct bases. **The tooltip's separate damage line reads skill402's20–40, which conflicts with715's10–15. This proposal deliberately chooses715's own packet values for the collapse.** It keeps the skill/tooltip endpoints separately in the captured descriptor for audit; it does not silently overwrite either source.

**Use the skill table's30-frame/1.2-second cooldown.** The pinned skill description advertises50frames/2seconds; the local owned table and its cached JSON agree on30. This discrepancy stays recorded. Skill `calc1=6` is also retained as source data, but the actual missile Radius formula drives both pulse and collapse area; no extra six-unit blast is invented.

## Casting, placement and capture

Use ordinary Warlock SC/FCR timing and the existing action/interruption service. Require a living trained Warlock/Witch, permitted form, active combat facet outside an actual safe area, full current mana at startup, and valid selected enemy or ground aim. Resolve selected aim at release, then keep the field fixed. Movement, equipment changes and ordinary interruption cancel unreleased work without payment. Late insufficient mana fails without allocation or a basic-attack fallback.

Release rechecks placement, pays once and starts cooldown only after successful allocation. Cost is23mana at effective rank1, plus1per additional rank. Reuse the already-tested start14/do28 ground launch: room/town/coordinates, mask5,size3, generic source-distance100boundary, flags1and inherited skill/rank. Britannia placement projects the source three-by-three subtile footprint into normal floor/static/barrier tests, permits occupying creatures and requires caster LOS; no open-tile search or teleport fallback is invented.

Capture effective Abyss/Entropy ranks, hard Bolt/Chains/Entropy points, magic mastery, existing magic/potion modifier packet and an independent saved RNG stream at release. Match the other Miasma consumers: potion additions are captured, target defenses remain live, and magic immunity does not block separately resisted bonus elements. Neither709nor715 is CanSlow, so captured missile slow is unnecessary. Gear changes or respec do not rewrite an existing field. Cross-skill global RNG interleaving remains unverified.

## Pulse, pull and collapse

First center pulse occurs after12frames/480ms, then at24,36and48. Each eligible hostile living NPC can receive one magic contact per pulse, with shared native missile avoidance and hit recovery; no weapon damage, leech, extra stun or weapon proc is added. Each field has its own cadence, so independently paid overlapping casts can each pulse. Walls/LOS, height, safe recipients and shared allegiance rules exclude invalid targets.

Before each pulse's damage, attempt **one ordinary UO movement step toward the center** for a non-elite target not already at the center. Reuse `CombatDisplacement.TryStep`: terrain, doors, occupancy, regions, frozen/paralyzed refusal and movement events still apply; facing is restored. This is an explicit pull adaptation, not an old knockback trajectory or a claimed D2R distance. Bosses/champions/uniques use the shared elite classification and are not pulled. Damage and pull are independent: magic immunity or avoided damage does not itself stop an otherwise eligible pull. A failed movement step does not cancel damage or teleport the monster.

At frame50, the center becomes the terminal child without dealing an expiry hit. At frame51,715 makes **one** magic burst using its own captured endpoints and the same radius, with no further pull, then disappears. The final center pulse and collapse are distinct; no shared12-frame delay suppresses that last burst.714is visual only and produces no extra damage. There is no corpse explosion, corpse requirement, poison or lingering ground damage.

A late connected tick produces at most one due center pulse and advances to the next original12-frame phase, skipping historical pulses. A terminal can execute only in its one-frame window `[frame51,frame52)`; a late call within that window may transition and burst together, but at/after52it is discarded. Repeated calls cannot replay a pulse or burst.

## Lifecycle and persistence

Fields remain where cast. Owner logout, different facet, height separation or actual safe area suspends attacks/pulls while original UTC lifetime continues. Returning may resume remaining pulses without healing, payment or reset. Missed pulses/collapse are skipped. Owner death, deletion or class departure clears the field; stopping a released action or respec alone does not. An overdue restored stage skips offline events rather than replaying damage.

Reuse `SanctuaryMiasmaWorld` profile254/version0 and the shared identity allocator, adding only an optional `Abysses` array. Old JSON omitting it restores an empty Abyss pool. Save center/terminal phase, fixed point/map, captured descriptor, RNG, birth/deadline/expiry and original identity. Validate every pool and cross-pool identity before committing any root state; malformed Abyss entries cannot partially restore Bolt or Chain. Unreleased actions remain transient. No new timer, profile, serialization version, client patch, reference read or deployment is needed.

## Verification and remaining scope

The complete candidate is exercised in the disposable native server with retained Flame Wave/Chain/Bolt/Mirrored consumers. Final counts and accepted source/DLL hashes are recorded in [the checkpoint](abyss-world-checkpoint.md) before asking for approval. Tests cover numeric/radius thresholds and actual original gear, action/payment/interruptions, pulse and terminal damage, real pull, same-horizontal-center height overlap, elite exclusion and blocked/frozen contexts, overlap, explicit immunity, parking/death, late/no-replay deadlines, atomic legacy-compatible shared saves and actual reloads. Dedicated elite-pull exclusion is covered; all special/native/PvP/property recipients and graphical acceptance remain outside this prepared scope; they must not be represented as certified.

Current UO tear/pulse/collapse markers and cast sound are identified placeholders. Final Abyss tear714, center709, collapse715 and cast/audio remain in the separate effects register; tree/hotkey icons remain in the icon register. Missing artwork does not gate mechanics. After approval, enable learned dispatch and automatic progression, then verify actual left/right/quick inputs before counting the handler. Full Enhanced Entropy admission follows its complete consumer checks separately.
