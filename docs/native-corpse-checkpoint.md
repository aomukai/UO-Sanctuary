# Native Lich corpse checkpoint

Reference: D2R Reign of the Warlock **3.3.93847**, with approved Sanctuary deviations. This change applies the user's native-monster classification policy to the existing Graveyard Lich; it does not invent a D2 species or change the living monster's stats.

New player-killed Lich corpses capture their authored level 5, fixed 90 base life, original monster serial, difficulty and source-style seed. Corpse Explosion and Poison Explosion can select these corpses through the existing public cast routes. Raise Skeleton and Skeletal Mage already accepted native bodies; those paths are regression controls. Existing reservation, consumption, loot, town and ownership checks remain shared.

## Verification

The focused isolated cohort uses Content build 1, Integration build 9, source snapshot 9 and native run 8 in `.local/class-skills/native-corpse`. The acceptance receipt is [native-corpse-native-evidence.json](../tools/reference/native-corpse-native-evidence.json), reproduced by `python3 tools/record_native_corpse.py --check`.

The cohort checks real player-caused Lich deaths, all four public corpse-cast dispatches, once-only payment and protected gold. Six authenticated corpse-target inputs exercise automatic Corpse Explosion and Poison Explosion damage. Two reloads retain the original unused corpse, metadata, seed, availability and gold. The existing mapped-D2 corpse cohort reuses its cached 52,100 base-life controls and four saved fixtures; it does not rerun the older source compiler.

Earlier failed runs are preserved separately. Fixture fixes covered the correct corpse entry point and Mage ID, real player loot rights, raise consumption flags, clearing prior pets from a reused spawn point, and restoring an offline fixture player's map before checking live visibility after reload. Native run 7's diagnostics showed the original corpse metadata and loot were intact; the player was on `Map.Internal`. Production Content build 1 remained unchanged through these fixture corrections.

## Remaining gaps

- **Native Lich Revive remains unavailable.** It needs native eligibility/original-actor activation and ownership/lifecycle handling for the helpers its living AI raises. Existing supported Revives retain their living abilities; global monster spell conversion remains a separate project.
- The Lich's flesh classification is unknown. Find Potion and Grim Ward are not newly admitted.
- Only the exact authored Graveyard Lich type is classified. Other native types, subclasses and old unclassified corpses receive no guessed identity or backfilled base life.
- This does not certify shared-consumer, PvP, graphical or whole-class acceptance. Coverage remains **236/240 handlers, no certified class**. Four older Warlock proposals remain frozen pending their own decisions.

No installed build or save was changed. Existing persistence layouts, client, native kernel and clocks are reused. Final effects/audio remain in [skill-effects.md](skill-effects.md), and talent/hotkey icons remain separately tracked in [skill-icons.md](skill-icons.md). No new asset was generated.
