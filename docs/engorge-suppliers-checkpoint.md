# Engorge numerical suppliers checkpoint

2026-11-15 JST. Mechanical reference: owned Reign of the Warlock **3.3.93847**. This completes the numerical capture component, **not the learned skill**. Coverage stays **226/240 handlers, Warlock 17/30; no class certified**. Death Mark's approved learned route remains accepted. Blood Oath's separate prepared reaction still awaits its existing decision; this work does not depend on it.

The owned skill, skill description and English string tables establish that `calc1/ln34` heals a percentage of maximum demon life. It is not a life cost. `EngorgePrepared` captures the existing formula evaluator's results without applying a state or changing an original summon snapshot:

| Supplier | Effective rank 1 | Effective rank 20 | Rule |
|---|---:|---:|---|
| Mana | 3 | 7.75 | +0.25 per additional rank |
| Duration | 5 seconds | 62 seconds | +3 seconds per additional rank |
| Maximum-life healing | 30% | 49% | +1 percentage point per additional rank |
| Physical resistance | 5 | 5 | Flat source stat |
| Minimum/maximum drain stat | 15 / 15 | 53 / 53 | Raw source fields; delivery unresolved |
| Attack speed | 15 | 34 | +1 per rank, capped at 35 from rank 21 |
| Flat armor | — | — | 25 per **hard Blood Oath** point |
| Regeneration stat | — | — | 5 per **hard Blood Oath** point; raw source units |

The healing helper keeps FP8 fractions, truncates the percentage calculation and caps it at missing life. Immutable captures validate against their original ranks and reject contradictory hard-point contexts or malformed values. Nothing spends mana, consumes a corpse, heals a recipient or starts a timer merely by capturing these suppliers.

Content 1 / Integration 1 / source snapshot 1 / native run 1 passed **15 focused markers**, three authenticated connections, three boots/saves and **two reloads**. The first boot checked 765 effective-rank/hard-point formula contexts without repeating older compiler/source matrices. A real Witch's Tarnhelm changes Engorge 19→20 and effective Blood Oath 7→8; armor remains 175 and raw regeneration 35 because Blood Oath has seven hard points. Removing and re-equipping the helm restores the corresponding capture without rewriting earlier values. Three original learned Goatman/Tainted/Defiler bodies retain their birth data, fractional wounds, RNG and controllers while parked across both reloads. Test-fixture JSON retains numerical captures; **no production Engorge persistence is claimed**. There are no new socket skill-input checks in this component.

Both builds have zero warnings/errors. The existing disposable native server and save checks were reused; no native/client rebuild, deployment, new timer, persistence ID or serialization version. Installed 106 artifacts, 38 saves, 20 configurations and 43 client patches remain unchanged. [Source review](../tools/reference/engorge-source-review.json), [native receipt](../tools/reference/engorge-suppliers-native-evidence.json).

The remaining gaps are concrete:

- Current do161 is unreadable in all seven retained fragments. Corpse targeting/consumption, recipient membership/range/order, payment and state installation/lifecycle still need verification or a complete tested adaptation.
- The tooltip labels the drain value as percentage life steal, while the existing ordinary non-hireling NPC allocator uses `lifedrainmindam/maxdam` as **flat resource amounts**. Neither interpretation is silently installed. Reuse the existing NPC drain receiver once that contract is resolved.
- Raw `hpregen` must use the shared source-frame recovery path, rather than an invented maximum-life percentage or a second timer. Flat armor, physical resistance and attackrate should enter their existing shared consumers.
- Final corpse-transfer/healing/buff effects and audio remain in the effects list; talent/hotkey icons remain in the separate icon list. This capture-only component emits no placeholder effect.

The current-version [datamined Engorge page](https://diabloforfans.com/skill/engorge?lang=enUS) corroborates the speed cap, flat resistance and refresh-only description. The older [original tester notes](https://upload3.inven.co.kr/upload/2026/03/02/bbs/WarlockSkillsNotesandFunctionality.pdf) corroborate the cap and nonstacking behavior but do not pin the modern stat writer or recipient callback. These are recorded with their limits; no live client/protected-memory read was repeated.
