# Native Lich corpses across difficulties

The native Lich corpse entry previously captured only Normal’s level 5/base 90. Its existing living Nightmare/Hell variants already use different levels and life. Their deaths therefore had no matching native base snapshot. This checkpoint copies those authored facts into explicit corpse difficulty rows:

| Difficulty | Existing level | Fixed base life | Authoritative source |
| --- | ---: | ---: | --- |
| Normal | 5 | 90 | `GraveyardProfiles.Lich` |
| Nightmare | 36 | 1,102 | `creature-difficulties.json`, `graveyard-lich` |
| Hell | 69 | 9,118 | `creature-difficulties.json`, `graveyard-lich` |

Normal fields and original source keys remain intact. Other difficulties require an explicit row and matching captured level; they do not fall back to Normal. Source-base capture still excludes later buffs and leaves the pinned D2 species catalogue untouched. Existing corpse metadata is not backfilled or rerolled.

## Verification

The focused cohort builds the existing content and integration projects and runs isolated worlds. New checks kill actual Normal/Nightmare/Hell Liches through player damage, verify their active variant level/life and captured identity/base/seed, exercise the public Corpse Explosion and Poison Explosion dispatches, and save three unused corpses through two reloads with loot and usability intact. Wrong-level and invalid-difficulty calculations are rejected.

Real input controls include Nightmare Corpse Explosion and Hell Poison Explosion, alongside retained Normal explosion inputs. The retained owned/wild Lich raise controls, parked helper wounds, native corpse controls and cached mapped-D2 corpse checks remain in the same cohort. Content build 1 / Integration build 2 / source snapshot 2 / native run 2 passes **54 markers, 15 authenticated connections, 12 actual explosion inputs and two reloads**. The [receipt](../tools/reference/native-corpse-difficulties-native-evidence.json) is checked by `python3 tools/record_native_corpse_difficulties.py --check`.

Native run 1 passed the new difficulty snapshots/consumers and first reload, then timed out on a retained Normal CE input. The cause remains unassigned. Integration build 2 adds precise pending-action/mana/corpse/target diagnostics; the unchanged Content build 1 then passed the full cohort. Failed sources/logs/assemblies are archived. This does not establish that the earlier timeout is fixed; asynchronous input reliability remains a testing limit.

## Remaining work

Native Lich Revive remains refused with no payment or reservation at every difficulty. It still needs eligibility, retained original activation, combat/facet composition and the complete native-world adaptation decision. The existing reward comparison to `unraveler1` is not implicitly promoted into a D2 AI identity. The prepared helper ownership graph is reused for that future work.

The initial native-corpse document incorrectly described this living role as unscaled across difficulties; the current source document now corrects it. This is a concrete corpse-data fix, not a new class handler or class certificate: coverage remains 236/240, with no class certified. Four older Warlock proposals remain frozen.

Installed build/configuration/saves, the client and native kernel remain untouched. No newer D2R reference reads or repinning. Existing effects/audio and icons stay separately tracked; no new artwork is needed for this difficulty metadata.
