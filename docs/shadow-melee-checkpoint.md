# Shadow ordinary melee checkpoint

Reference: **D2R Reign of the Warlock3.3.93847**, approved Sanctuary deviations first. Coverage remains208/240,Assassin28/30; learned Shadow routes stay unavailable.

Both AI105/106 use ordinary attacks as a fallback. The original Valkyrie full-fill/contact implementation is now shared through `OriginalNpcMelee`; existing Valkyrie entry points delegate to it. Each species retains its own original numeric graph, weapon binding, passives and intrinsic critical row. Shadows use the posted typed Claw Mastery AR/damage/critical layers and own RNG. Existing elemental/poison/piercing, armor setup, mitigation, ordered HP/return and item callbacks are reused. No player gear/skill proxy or cloned summon inventory is introduced.

The pinned transformation maps monster A1mode4 to Assassin KKmode12. Owned AIKKHT1/HT2/HTH rows have13frames,speed256,marker1at4 and zero frame bonus; the kick-looking graphic does not replace ordinary NPC weapon damage with player boot damage. The source animation probe now executes1,800contexts:900S2 and900converted A1. Their shared dual-hand rate and event routines remain intact. Source fields/rows and complete routines are bound by [source evidence](../tools/reference/shadow-animation-source-evidence.json).

The existing `ShadowActions` conductor now handles intrinsic Attack0 as well as Fire Blast. Ordinary contact allocates at marker1, rechecks live range, finishes once and follows existing cancellation/parking rules. The original selected-hand merge runs before AR/allocation. Shadows' own item percent AR and matching Claw Mastery are combined without forwarding the owner’s attributes.

Verification and exact artifact/input hashes are recorded in `tools/reference/shadow-melee-native-evidence.json`. Native checks exercise both original species, real mastery consumers, marker-only damage/RNG, late end handling, range loss, parking and original saved inventories/wounds/RNG. Prior S2/intrinsic Fire Blast and original Valkyrie contact cohorts are retained. The first run could not find a legal native fixture corridor; it was moved to the existing proven test area without changing gameplay rules.

Shared owner-follow movement (`SourcePetMovement.Common`) is already the same helper used by the older Shadow routines. Next connect that transport with their own AI105/106 decisions; do not substitute Necromancer AI. Remaining intrinsic skill adapters and current death behavior still need qualification. The older unique-modifier table identifies UMod42 callbacks `MONSTERUNIQUE_ColdEnchantedModeChange` and `MONSTERUNIQUE_CastNova`; this is a research lead,not current death-parity proof.

Final Shadow A1/kick presentation, body/equipment,hit/poison/critical/audio assets and talent/hotkey icons remain separately pending. Existing shared source contact presentation remains an identified placeholder. No imagegen asset blocks these mechanics.

**Verified:** Content3/Integration2/snapshot2;21native markers,three boots/two reloads/three saves;source snapshot matches tested inputs. The expanded source oracle covers1,800contexts with clean ASan/UBSan output. Installed106artifacts,38saves,20configuration files and historical client patches remain unchanged. See [native receipt](../tools/reference/shadow-melee-native-evidence.json).
