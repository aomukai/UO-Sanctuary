# Blood Golem’s fixed Life Tap proc

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. This increment follows `5af5841`. Blood’s learned source-world path now implements the approved **5% chance of level-5 Life Tap at 20 hard points**, alongside its earlier 150% Clay damage and hard-10 Open Wounds. No class is complete and this candidate is not deployed. Legacy/native fallback, broader engine equivalence and final assets retain the limits below.

## Related mechanics reused

Reviewed the existing NPC item registry, prepared source/target damage and HP order, Blood healing and Open Wounds, player Life Tap casting/receiver/defender healing, fixed-rank Mage spell contexts, source uninterruptible state, and saved curse records before implementation. The [event reuse review](../tools/reference/blood-pet-event-reuse-review.json) distinguishes the enabled inherent slot from unsupported arbitrary item procs.

`NecromancerCurseCasts` now shares one area traversal and receiver between player curses and this NPC Life Tap route. The caster supplies identity/origin/presentation; its owner supplies allegiance. The fixed NPC `SkillValues` context supplies Life Tap rank 5 and no other skill ranks, synergies, player gear or mana. Player payment and ordinary casting stay in their existing wrapper. `NecromancerEffects.CurseApplied` accepts the actual mobile caster; no second curse store, formula, radius check or healing scheduler is introduced.

The registered Blood slot is present even below hard 20 so training/respec during an attack windup is observed at the actual event. Soft ranks do not unlock it. The source-damage event runs before the defender’s Life Tap healing and HP clipping: the first proccing swing can heal the golem from **50% of its mitigated pre-clip physical damage**, including lethal overkill. Successful zero physical damage and full absorption still attempt the proc but create no healing from zero physical damage. Misses and shield blocks skip the event.

The callback requires positive chance and packet hit flag `0x20` before drawing once from the existing actor stream. Uninterruptible/missing cast context can reject after that draw. The native adapter’s explicit inherent sequence is Blood healing, Open Wounds, Life Tap, then defender events/HP. This is the chosen ordering of Sanctuary’s two new inherent slots, not certification of arbitrary D2 item subscriber priority. The inspected older `SUNITEVENT_Register` inserts at the head and `Trigger` walks that list; dynamic source registration history/global RNG remain separate evidence gaps.

## Fixed curse context and persistence

Level 5 uses the pinned **8-subtile radius and 640-frame/25.6-second duration**, before recipient curse resistance. The area is centered at the struck target’s current position. It shares the existing provisional two-subtile-per-tile geometry, height and allegiance filters, eligibility/no-walk rules, source duration, Attract protection and rank replacement. No extra per-recipient LOS gate is added. Stronger Life Tap rejects; equal rank refreshes expiry while retaining the original creator; weaker same-state curses replace normally.

Casting preserves the summon’s active attack controller. There is no player cast action, mana payment or owner Life Tap rank supplier. Curse state stores the real golem serial and origin. The existing defender-healing consumer continues after that caster is deleted. No serialized record, persistence version or mobile field layout changes.

The older item helper restores a previous unit target when present; with no previous unit target it restores coordinates/server-do flag but can retain the temporary target identity. The source probe records that distinction. This native adapter leaves `Combatant` and its existing controller unchanged; full native/source path-target equivalence is not inferred.

## Verification

**Accepted build 4 / run 2 / audit 2:** both rebuilds have zero warnings/errors. The native suite passes **358 markers across three boots, two reloads and three saves**, retaining all 313 predecessor marker scopes.

- The new suite compares 720 chance/hit-flag controls with the complete source output. Hard 19 plus one item rank refuses; live training to 20 enables the proc during windup, and respec disables it. The inherent wound/proc sequence consumes one draw each when eligible.
- Fixed rank, real caster/origin, target-centered radius boundary, 25.6-second duration, no mana/controller changes, success/failure, actual shield block, missing hit flag, physical immunity, full absorption, uninterruptible, Attract and stronger/equal/weaker curse controls pass.
- The first proc heals on that same swing, including pre-clip lethal overkill. The golem remains the direct killer and its owner receives loot credit through the existing ledger.
- A held curse snapshot survives both actual reloads after deleting its NPC caster, preserving source/rank/expiry and defender healing. Three connected learned-world sessions use normal ticking with deterministic proc seeds and preserve the curse/healing after deleting the golem.
- All nine affected player curse families and six actual player Life Tap right/quick input sessions pass. Four audits, **71 Python tests, 573 named definitions and eight classes/240 skills** pass. Audit decisions remain unchanged with 881/21 fresh fingerprints; runtime data, staged kernel and **106 installed hashes** are unchanged.

The initial Python/readiness run occurred before installing the freshly exported audit receipts into the reference directory and correctly rejected stale source fingerprints. That failed output is retained; the rerun against the fresh receipts passes without weakening validation.

The [complete source probe](../tools/probe_npc_hit_skill.py) executes unchanged proc 20, target and position wrappers, and the item-effect router in **9,360 contexts**, including 1,088 successful notifications, under ASan/UBSan. Cast/start/world/path/stat/RNG/notification services are explicit. It distinguishes chance success from handler/notification success, temporary target/flag restoration, packet and null guards, decoding, and uninterruptible/item/start rejection. Do30/curse factory/healing are not executed inside this particular probe; their existing evidence is composed and tested in the native suite.

The first test compilation used the wrong existing curse field name (`Owner` instead of `Source`); only the fixture was corrected. The first native run’s overkill fixture expected the player in `LastKiller`; the established direct-hit contract instead retains the golem there and credits the player through `ClassLoot.Killer`. The fixture was corrected to assert both, with no production credit changes. Failed/partial attempts remain archived.

See the [source receipt](../tools/reference/npc-hit-skill-source-evidence.json), [native receipt](../tools/reference/blood-life-tap-native-evidence.json) and [validator](../tools/record_blood_life_tap.py).

## Remaining scope and assets

Original item property writer 11 and arbitrary proc skills/parameter layers remain explicitly unsupported. This increment enables the approved fixed inherent slot; it does not claim all NPC equipment procs. Native fallback/original plain historical controllers, PvP/unmapped recipients, full source stat-list/room/protection/uninterruptible/client behavior and modern registration/seed equivalence retain their earlier limits. Held saved curse observations are distinct from natural live combat and do not prove installed old-DLL upgrade or concurrent/unheld saves. Generic/player legacy wound rollout remains separate.

Next: Iron Golem’s approved damage/Extra Strong/Spectral Hit changes, beginning with its existing original-item and encounter-affix consumers and their exact difficulty/grade contracts. Mage basic spell/Mastery mapping, Fire upgrades, barriers, safe-area parking, Druid and remaining classes stay open. Coverage remains **186 handlers / Necromancer 30; no completed class**.

Blood body `14`/hue `0x21`, summon particles `0x376A`, curse marker `0x375A`/hue `0x455`, recovery marker `0x376A`/hue `0x26` and sound `0x1E5` are identified placeholders. Final automatic proc/area/curse application, recovery, expiry and audio feedback belong in [skill effects](skill-effects.md). Blood and Life Tap talent-tree/right/quick artwork remain separately pending in [skill icons](skill-icons.md). The automatic proc adds no player action or separate hotkey icon.
