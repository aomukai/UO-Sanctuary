# Own Shadow Fury travelling projectile checkpoint

2026-11-08 JST. **208/240 learned handlers; Assassin28/30; no class certified complete.** Prepared intrinsic Shadow Fury projectiles and their ranged receiver are verified within the scope below. Full NPC Fury channel/AI and learned Shadow Warrior/Master remain gated.

Reference: D2R Reign of the Warlock **3.3.93847**, current curated skill/missile tables, and the explicitly identified older D2MOO bridge at `0c0bbd1e6bef9181a3da1c046523af622a925416`. Current executable parity is not established by compiling that older source. The approved [NPC mana exception](shadow-fury-mana-proposal.md) remains in force; this checkpoint adds no further design deviation.

## Implemented

- `ShadowFuryMissiles.LaunchPrepared` takes the actual original Shadow, its intrinsic Fury rank, a supplied creation-critical decision and private child seed. The existing collector supplies its own primary-claw physical96/source endpoints, typed channels, hard synergies and saved double target-armor delta. Player ownership supplies durable credit, not player weapon stats or mana.
- Optional NPC metadata extends the existing `WeaponFlight` record without changing old player JSON. Movement, source-step size1 collision, activation1, velocity22,40-frame lifetime and save host reuse `WeaponMissiles`. Released flights do not depend on retaining Fury as the actor's used skill. The full action's interruption/critical/seed allocation policy is still separate.
- Impact reuses the qualified `AssassinTrapAttack` NPC accuracy helper: saved missile to-hit is a flat contribution, own Dexterity/attack/percentage are read from the original NPC, and ordinary mapped defense/blocking remains shared. Actor accuracy precedes private missile damage rolls; private rolls precede block/avoidance and source return. A failed accuracy roll does not allocate a damage return.
- `IronGolemContact.ReleaseMissile` reuses the prepared ordinary-NPC typed resistance, absorption, poison, resource, event, HP and return services with ranged event names. It does not invoke the melee contact wrapper or progressive charge release. Successful damage and blocked return remain distinct. Fixed NPC life/mana drains retain their existing resource semantics.
- Current Life Tap data registers handler5 on both `damagedinmelee` and `damagedbymissile`. The shared prepared missile receiver now invokes the existing Life Tap helper in its ranged lane. Physical immunity suppresses its physical healing; ranged hits do not acquire melee-only Iron Maiden retaliation.
- Saved armor reduction is applied after damage/return, including source block, and not on failed accuracy. Unsupported burn/bypass/positive-armor packets refuse explicitly. Unsupported recipients log the reason once rather than acquiring an invented fallback.
- Parked/offline/cross-facet flights stop moving/fighting while their original lifetime continues. Missing/dead source or owner currently retires the flight; exact modern caster-removal behavior remains unverified.

## Verification

Accepted **Content2 / Integration7 / snapshot7 / native-run7**. Content2's production inputs match the final snapshot; subsequent changes corrected integration fixtures only, so no redundant content rebuild was needed. Both accepted builds have zero warnings/errors.

**21 native success markers, three boots, two actual reloads, three saves.** The focused cohort covers both original seed1 Shadow species, real flight before impact, normal/critical typed damage, own versus controller AR, accuracy and private-RNG ordering, shield return, Master fractional life/mana drains, physical immunity/Life Tap/Iron Maiden separation, selected-claw source-rate poison with existing owner credit, post-HP double armor mutation, positive-armor preflight refusal, inactive expiry, and exact partial-flight/original-body/items/resources/wounds/RNG persistence. Retained player Fury, own Fury packet/resource, original Revive/Iron Golem melee and resource tests also pass. Learned Shadows remain disabled.

**128 new complete unchanged `MISSMODE_SetDamageFlags` controls** cover all32 defense masks, physical eligibility, supplied critical flag, event eligibility and unconditional missile return. ASan/UBSan are clean. Existing NPC accuracy6804, impact reader5184 and own creator16 proofs are reused; old compiler matrices were not rerun. Declared source services and limits are recorded separately from native tests.

Diagnostic runs and their inputs remain under `.local/class-skills/shadow-fury-missiles`. Fixture corrections concerned numeric-frame rebuilding on skill selection, the existing owner-credit/per-millisecond poison representation, the armor floor, Master's fixed mana-drain healing, and keeping the reload target on its original projectile lane. These did not require further production receiver changes.

- [Source evidence](../tools/reference/shadow-fury-missiles-source-evidence.json)
- [Native evidence and preservation receipt](../tools/reference/shadow-fury-missiles-native-evidence.json)
- Runner: `SANCTUARY_TEST_SERVER=/media/aomukai/FILES/Sanctuary/.local/server python3 tests/check_shadow_fury_missiles.py`
- Receipt: `python3 tools/record_shadow_fury_missiles.py --check`

The installed server build/configuration, existing saves and historical client patches pass the existing preservation checks. Tests used disposable `/dev/shm` servers. No deployment or user-save migration occurred.

## Remaining work and assets

Next qualify the actual NPC Fury SC startup/do callback, ordinary rewind and state/lease hosting, critical selection, world/private child-seed allocation and AI; do not import the player's held-input timeline or the unused NPC sequence by assumption. Then finish Blade Sentinel, death modifier42 and learned Shadow admission. The earlier Shock Web NPC localdelay15 question remains nonblocking. Exact modern executable/PvP, unqualified recipient and arbitrary callback-mutation behavior remains outside this checkpoint.

Effects/audio: travelling blades currently reuse the identified `0xF42` placeholder and shared contact/return cues. Final NPC cast/channel poses, blade travel/impact/expiry and sounds remain pending in [effects inventory](skill-effects.md). Icons are separate: Blade Fury and both Shadow talents retain their existing identified talent/hotkey text placeholders in [icon inventory](skill-icons.md).
