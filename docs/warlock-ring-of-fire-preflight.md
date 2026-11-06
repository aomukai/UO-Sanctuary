# Ring of Fire mechanical preflight

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations first. This preparation enables no handler. Coverage stays197/240, Warlock1/30 and zero completed classes.

The [primary-table receipt](../tools/reference/ring-of-fire-preflight.json) binds the owned-build skill extraction, cached current skill/missile rows, pinned executable and supplementary source. Skill394 uses new server do154; parent missile689 uses standard do1/hit1 but new damage17. Both new callbacks are null in the pinned older dispatcher tables. The visual explosion730 has no server-do/server-hit fields: its name is not evidence for a second server damage child.

| Primary-table input | Contract retained for implementation |
| --- | --- |
| Invocation mana | `(16 + rank - 1) × 128` FP256: rank1 costs8mana. Raw mana16 is not the final cost. |
| Missile count | `calc2`:16 below effective rank10;32 from10. Skill Param1=5 is not this count. |
| Skill damage | Fire6–10 at rank1;hard Flame Wave and Apocalypse each add10percent per point through the current skill formula. |
| Parent motion | Velocity/max25, acceleration−1000, Range11, InitSteps1. Units and per-frame composition require the actual missile factory/motion path. |
| Parent contacts | Size2, LastCollide1, NextHit1/NextDelay11, AlwaysExplode1, Pierce1, GetHit1, KnockBack100, ResultFlags13, HitFlags2. Keep flags distinct from conclusions about post-hit damage. |
| Damage17 formula | `100 + fnum × 5`. The time supplier, rounding and placement relative to rolled/modifier/resistance damage remain unverified. |
| Explosion | Parent hit radius parameter3 and visual child730 are separate inputs. Do not infer duplicated damage from the visual child. |

The existing saved missile motion/collision, fire damage and knockback movement consumers are reuse points. Item knockback's size-dependent chance is a different callback and must not replace Ring of Fire's own missile flags/chance behavior. Generic Nova emission does not prove new do154's directions, factory flags, seed/allocation/failure order or caster notification. Hit-delay identity, collision/explosion/immune knockback and damage after the first contact need composition and native checks before a learned route can be enabled.

One maps-only inspection in this goal turn found the current do154 table candidate at RVA0x44a7a0, with unwind fragments0x44a7a0–0x44a84e,0x44a84e–0x44a99a and0x44a99a–0x44aa67. All were inaccessible in owned process694337/start19896864. No process memory was opened/read, no input was sent and no protections changed. The candidate table's shape is corroborated; dispatcher linkage and callback identity are not verified. Do not retry that unchanged observation or claim current-code semantics from it.

The user-supplied [data site](https://diabloforfans.com/skill/ring-of-fire) was checked on2026-11-06. Its broad description is a research lead; the owned raw rows take precedence over summary costs or threshold wording. [Warren/Kabryxis's tested notes](https://upload3.inven.co.kr/upload/2026/03/02/bbs/WarlockSkillsNotesandFunctionality.pdf), page6, report first-contact damage, immune-target knockback and an explosion. Their version applicability remains bounded: their Cleave/Mirrored descriptions differ from current pinned rows. These observations guide future checks without certifying current callback or timing order.

Final cast/ring/projectile/contact/explosion/expiry/audio and talent/left/right/quick icons remain in the separate effects and icons ledgers. Any future native fire marker must be labelled as a placeholder; no final art or new effect producer is created here. Recheck the primary inputs with `python3 tools/record_ring_of_fire_preflight.py --check`. Continue unaffected skills while these specific gates remain open.
