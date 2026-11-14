# Prepared complete Engorge checkpoint

2026-11-15 JST. Reference: owned **D2R Reign of the Warlock 3.3.93847**. [Complete proposed rule](engorge-world-rule-proposal.md). **Learned Engorge remains disabled pending that adaptation decision; coverage remains 226/240 handlers, Warlock 17/30, no class certified.** Blood Oath's earlier reaction decision is independent and still pending. Existing Death Mark approval and learned route remain accepted.

`EngorgeCasting` composes the existing corpse eligibility/reservation and protected-loot consumption, near-click acquisition, original demon roster, shared SC/FCR clock and fractional mana. It revalidates at release, pays once on corpse commitment and never replays or invents rollback of completed callbacks. Movement, including moving away and back, uses the existing summon/cast location hook. Prepared casting is internal; no normal input route is granted.

`EngorgeEffects` stores an optional immutable payload in the three original demon body JSON schemas, without adding a persistence ID or changing their serialization version. It heals using the authenticated percentage and actual current maximum life, then supplies flat armor, physical resistance and skill attackrate through existing consumers. The existing source-frame recovery path adds captured raw regeneration only inside the effect interval; no second timer or offline recovery is added. Recasting replaces the payload and restarts duration without stacking. Gear/training changes leave it frozen; ordinary parking, difficulty and body lifecycle policies remain in force.

Life steal is a proposed **percentage** adaptation because the tooltip and older NPC flat-drain allocator disagree. Actual Goatman ordinary melee reuses `WeaponLeech.Calculate`, victim life clipping, drain effectiveness and difficulty divisors, healing only the attacking demon before victim life commitment. Elemental, suppressed/unsuccessful, periodic, ranged, expired and inactive contacts cannot gain this leech. A positive raw NPC drain packet is refused rather than silently combined. No Tainted spell leech, Defiler weapon attack, owner leech inheritance or extra proc is introduced.

Accepted candidate: **Content 3 / Integration 5 / source snapshot 5 / native run 3**, 15 focused markers, three authenticated sessions, three boots/saves and **two reloads**. Native checks include:

- Actual corpse loot retention, two-tile cursor acquisition, once-only reservation/payment and no-demon/low-mana/consumed-corpse refusal.
- Unpaid windup, explicit cancellation, movement out-and-back cancellation, competing corpse consumption, successful shared-clock release and no replay.
- Three original recipients with exact FP8 healing, flat armor, IAS and physical resistance; birth snapshots remain unchanged.
- Exclusive expiry, interval-limited raw regeneration through the existing source recovery method, refresh replacing rather than stacking, and atomic rejection of malformed owner metadata.
- Real Witch/Tarnhelm rank 19→20 and hard Blood Oath training 7→8 changing only new captures; previous buffs keep their captured 175 armor/35 raw regeneration.
- Actual ordered Goatman melee leech to the demon only, alongside zero-gain controls for elemental, suppressed, unsuccessful, periodic and expired packets, plus raw-drain conflict refusal.
- The original three body buffs, absolute timestamps, wounds, births, RNG and controllers survive both parked reloads without offline healing; the original roster feeds again after login.

The earlier 765-context supplier proof is retained at its original scope, not rerun or relabeled as new executable evidence. No new socket skill-input transactions are claimed. Final tree/hotkey icons remain separate from missing corpse-transfer/heal/buff effects and audio; runtime visuals/sound are clearly identified shared placeholders.

Diagnostics are preserved. Integration build 1 used the wrong shared action method name and failed compilation; the corrected fixture uses `ResolveAction`. Native run 1 (C1/I2) reached the training assertion after passing corpse/stat/expiry/recovery checks: the fixture's `Train` helper only raises a minimum rank, so requesting rank 1 did not refund hard points. The fixture now performs an actual 7→8 increase. C2 also added the immediate movement-cancellation hook, and native run 2 (C2/I4) passed. C3/I5 adds explicit direct/successful leech guards and their controls; native run 3 passes. Earlier source, DLLs, inputs and logs are retained in the stage's `compile-1`, `native-1` and `native-2` directories.

Both accepted builds have zero warnings/errors. Installed 106 artifacts, 38 saves, 20 configurations and 43 client patches remain unchanged. No native/client rebuild, deployment, new live/process/protected-memory read, source compiler matrix, timer or save-version change. [Source boundary](../tools/reference/engorge-source-review.json), [native evidence](../tools/reference/engorge-world-native-evidence.json).

Next: obtain the single complete rule decision, then connect and verify learned left/right/quick input using these same services. If unanswered, continue independent unfinished skills. Current do161 identity/ordering/stat installation, future Bind Demon integration, wider PvP/property/global-world/client certification and final assets are not proven by this candidate.
