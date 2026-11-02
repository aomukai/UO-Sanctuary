# Lightning Sentry learned-casting checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations first.

Lightning Sentry now supports learned right-slot and quick-slot casting. It reuses the shared do45 free-cell placement adapter, S2/IAS timing, unpaid wind-up, captured rank/passive checks and saved S1 actor. Successful creation pays its pinned 20 mana once, with no independent delay. Charged Bolt Sentry retains its separate 13-mana cost and fan; Lightning Sentry produces one Size2 penetrating lightning bolt per shot, with ten shots.

Both current model rows use `lg`/`hth`, ordinary mode/death-end handling, enabled DT/DD and the limited-corpse flag. The retained local monster/model text files were restored from the archived reference after matching their existing SHA-256 pins; all 601 fields across six pairs recheck successfully. Current owned LG death metadata supplies 23 frames at speed 256. Lightning Sentry now shares the identified completed-death profile: natural source-AI death and replacement stop the original NPC, while already-issued straight bolts retain that source during and after death. Exact modern corpse-pool insertion/callback ordering remains unverified.

Straight-projectile save restore now validates every candidate and duplicate ID before replacing the live pool and next ID. Native malformed-tail, duplicate and null controls prove atomic rejection. This shared pool also stores prepared Inferno and Death Sentry flights; their distinct casting and retirement paths remain separate.

Build 2/run 1 passes **105 native command/result markers across three boots, two reloads and three saves**. The combined run retains the CBS component, target/path, actor, aim, emergence, prepared release, retirement and learned-input cohorts. It adds Lightning Sentry's existing source/gear/penetration/wall/lifetime cohort, connected natural firing and offline expiry, and new learned casting/retired-source acceptance.

Eighteen actual wire requests cover twelve successful right/quick casts and six rejected forged left-slot requests across the two skills. All 30 Assassin mouse rows and 132 saved-binding migration contexts still pass. Lightning checks cover once-only payment, early/duplicate release, low mana, stop/movement cancellation, mana lost during wind-up, shared IAS/no FCR and no independent delay. Explicit actor stepping verifies ten shots and death completion. A replaced original dying NPC deals real travelling damage; after completion the same source deals a lethal penetrating hit with NPC/player credit intact.

A real Tarnhelm raises 20 hard points to rank 21. The actual paid Lightning trap naturally emits its first damaging single bolt. Original actor, bolt, captured packet, last-collider identity, gear, right/quick bindings, mana and wounds survive both reloads, then resume under ordinary timers without recasting, recollecting stats or paying again. CBS retains its equivalent acceptance. Inspection clocks are explicitly held/rearmed and wounded receivers are moved off held flight paths before saves. Separate normal firing/offline lifetime checks remain; this does not certify uninterrupted short flight lifetime through downtime.

Both clean builds have zero warnings/errors. Two runtime audit markers, 71 Python tests, 573 named definitions and eight-class/240-skill generators pass. Audit decisions are unchanged with 915/21 fingerprints. The native kernel and all 106 protected installed artifacts keep their hashes; no deployment or save reset. See [receipt](../tools/reference/lightning-learned-native-evidence.json).

Historical saved Lightning retirement-fault states are not automatically converted to a new death timer: earlier inactive aging can erase the original entry time. Their existing fault state is preserved rather than assigning a guessed deadline. Actual old-build upgrade coverage remains separate.

Remaining limits: validated free cells only; occupied size-zero placement and nearby-cell fallback remain unsupported. Exact modern pool phase/order, zero-velocity path flags, inherited sunder, native geometry/seed/event/item/owner/PvP behavior and final client presentation remain explicit acceptance gaps. No class is certified complete. Coverage is **189/240**, Assassin **20/30**.

Next: review Wake of Inferno against the shared cast and straight-flight components, preserving its distinct E9 death metadata, sparse beam emissions, fire damage reduction and duration. Wake of Fire's real wave maker/hit-window work and the Dragon/Shadow/Blade Fury contracts remain individually tracked.

Final S1/S2/neutral/death actor animations, bolt travel/contact and audio remain pending. Native throw animation, sound 0x23B, body 0x9C/hue 0x482 and bolt marker 0x3818/hue 0x482 are identified placeholders. Talent/right/quick icons stay in the separate ledger. No final asset completion is claimed.
