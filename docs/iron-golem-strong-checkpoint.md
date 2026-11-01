# Iron Golem’s direct Extra Strong unlock

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. This increment follows `c70fce6`. The candidate connects Iron’s approved hard-10 Extra Strong through existing damage/attack-rating consumers. **Hard-20 Spectral Hit remains pending. No class is complete and this candidate is not deployed.**

## Existing mechanics and context

Before implementation, reviewed the saved Iron curve/item recipe, `NpcEquipmentDamage`, `ClassStats.BaseAttackRating`, current encounter Extra Strong, the complete older Strong stat-adder and earlier affix reuse findings. The two existing percentage arrays are extracted into `EncounterProfiles.ExtraStrong`; the wild actor keeps its existing grade and compound modifier calculation.

| Difficulty | Direct damage / AR | Inherited encounter-minion damage / AR |
| --- | --- | --- |
| Normal | 135% / 90% | 67% / 45% |
| Nightmare | 112% / 75% | 56% / 37% |
| Hell | 99% / 66% | 49% / 33% |

Iron receives the affix itself, so it uses the **direct** branch. `EncounterGrade.Minion` describes a monster inheriting its leader’s modifier; it does not classify every player summon. The approved use-existing-affixes rule takes precedence over speculative replacements from older engine tables.

`IronGolemUpgrades` reads current owner hard points. Effective item ranks do not unlock the perk. Prepared Iron adds ED to the existing ordinary NPC percent pool after original-item capture, retaining item-local ED and one-/two-handed rules. AR joins the existing NPC item percentage pool over the complete native/item flat and Dexterity basis. The older Strong routine’s separate damage-percent/to-hit-percent stat additions support that separation; current MonUMod table/executable equivalence remains unproven.

Original historical Iron actors share the live AR provider and native fallback damage supplier. Captured summon/Golem Mastery ranks remain separate. Training/respec/relearn and current facet difficulty determine the affix without saving a second rank, changing the original item/recipe, healing wounds, replacing the actor or consuming RNG. Wild actors retain their own grade composition and unrelated stats.

## Verification checkpoint

**Accepted build 5 / run 5 / audit 5:** both rebuilds have zero warnings/errors. The complete suite passes **370 native markers across three boots, two reloads and three saves**, retaining all 364 predecessor marker scopes. Four runtime audit markers, **71 Python tests, 573 named definitions and eight classes/240 skills** pass. Audit decisions stay unchanged with 882/21 fresh fingerprints. Pre-build/native/audit source/data and tested DLL inventories match; runtime data, staged kernel and **106 installed hashes** remain unchanged.

The new suite covers twelve actual wild Ordinary/Unique/SuperUnique/Minion difficulty pairs; independent direct/inherited coefficients; hard 9 plus a real item rank; training/respec/relearn; original-item flat/Dexterity/percentage AR and local ED; historical actors; three facet transitions; saved resources/recipe/AR/order/stream; and three connected sessions training hard 10 during actual G4 windup. Both actual reloads preserve the prepared actor’s full recipe/controller, physical packet, roster order and stream, together with both actors’ live AR and wounded resources. Only observation clocks are held; the connected attacks use normal ticking.

The retained Iron curve fixture now permits exactly the intended live ED increment while preserving its captured curve, resource and recipe assertions. It also reports preflight/allocation failures precisely rather than reducing them to a generic learned-cast assertion.

The first fixture incorrectly expected normal attack descriptors to retain their old damage; it now allows their intended min/max change and checks that other descriptor fields stay unchanged. The Atlantian was rejected by the existing unsupported `pal` property writer 21; that gate remains in place. Baranar’s Star is independently rejected because its cold length needs an original zero-parameter range-fallback roll that the saved data does not record. The current fixture uses Messerschmidt’s Reaver after inspecting its existing supported projection plans; those two unrelated item support gaps remain documented. Failed attempts and inventories are retained.

See the [affix reuse review](../tools/reference/iron-affix-reuse-review.json) and [acceptance validator](../tools/record_iron_golem_strong.py). See the [native receipt](../tools/reference/iron-golem-strong-native-evidence.json) for immutable inputs and retained attempt logs.

## Remaining work and assets

Spectral Hit still needs all five offensive channels, its existing resistance/amount/type rules, critical/post-direct survivor-only cold/poison order, live hard-20 behavior and saved/natural actor verification. A resistance-only effect is not substituted. Arbitrary original properties/procs, including the class-skill writer, retain explicit support gaps; no eligibility gate is weakened to make a fixture pass.

Modern source/world/global-seed/event/PvP/protection/client equivalence, historical native controllers and installed old-DLL/concurrent/unheld-save acceptance retain their previous limits. Other Necromancer/Mage/Fire/barrier/parking/Druid/class work stays on the roadmap. Coverage remains **186 handlers / Necromancer 30; no completed class**.

Iron body `14`/hue `0x835`, summon `0x376A`, contact `0x37B9`/hue `0x481` and sound `0x1E5` are identified placeholders. Final stronger-hit/status feedback and audio belong in [skill effects](skill-effects.md); Iron’s talent-tree/right/quick icons remain in [skill icons](skill-icons.md). This automatic perk adds no action or extra hotkey icon.
