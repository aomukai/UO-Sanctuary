# Iron Golem’s Spectral Hit unlock

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. This increment follows `51a153c`. Iron’s live hard-20 unlock now shares the existing encounter Spectral Hit producer and resistance rules. This is a bounded mechanical checkpoint; **no class is complete and the candidate is not deployed**.

## Reuse and composition

Before adding the perk, inspected existing wild `EliteCombat.Hit`, `EncounterProfiles.Resistances`, ordinary NPC item damage/poison/critical allocation, ordered Iron contact, saved effect storage, owner-credit policy and the complete older Strong/Spectral routines. The existing-affix deviation takes precedence over older code that uses a different channel order, level-table damage and stat-list length additions. See the [reuse review](../tools/reference/iron-affix-reuse-review.json).

`SpectralHit` extracts the current wild rule without changing its draw overloads or order: roll an inclusive amount from 66% of the raw minimum to the raw maximum, then select magic, fire, lightning, cold or poison. Iron uses its captured innate Clay-curve endpoints as that raw basis; original-item local damage and live Extra Strong/ordinary damage percentages do not multiply this separate affix. It uses the existing prepared actor stream. Historical plain Iron retains its original controller/Utility stream.

The four direct elemental choices join the existing typed receiver **after intrinsic critical**. Poison remains an aggregate amount over 1,600 ms; it is not an ordinary item-poison rate and is not added to the initial HP packet. Cold supplies a minimum 1,600 ms chill before shared duration mitigation. These statuses apply after direct damage/return, to survivors only. Ordinary original-item poison, criticals and cold keep their existing allocation and delivery order. No new timed effect store or mobile serialization field is added; optional packet metadata omits null values.

Prepared and historical actors read live owner hard points. The resistance half calls the existing cold/fire/lightning sequence, under-75 threshold and two-immunity guard over existing resistance inputs. Queries do not recapture birth values, consume RNG, heal, change maximum life, replace controllers or reorder pets. Poison mitigation uses the actual NPC with its captured pierce; durable owner credit follows existing NPC poison policy. Player mastery, weapon and potion bonuses are not inherited.

## Verification

**Accepted build 5 / run 5 / audit 5:** both clean rebuilds have zero warnings/errors. **376 native markers across three boots, two reloads and three saves** retain all 370 predecessor scopes. Four runtime audit markers, **71 Python tests, 573 named definitions and eight classes/240 skills** pass. Fresh audit fingerprints are **884/21**, with decisions, copied runtime data, staged kernel and **106 installed hashes unchanged**. Pre-build, native and audit source/data/DLL inventories match. No deployment occurred.

The component checks cover 2,000 old-versus-extracted wild RNG contexts; all five actual wild and prepared outcomes; zero-amount draws; critical exclusion; hard-19 plus an item rank, training/respec/relearn; resistance threshold/order/immunity composition; no-fill/range/lethal rejection; zero-physical and poison-immune recipients; source-removal poison/chill expiry; actual Jade Tan Do original poison coexisting with magic/Spectral poison; historical fallback; and natural hard-20 unlock during G4 windup. Saved observation clocks are explicitly held; natural contact and separately exercised expiry use ordinary clocks.

The first attempt placed a test corridor beyond its helper’s supported range. The second incorrectly assumed zero innate resistance instead of composing Iron’s existing birth/item resistance. A later live check initially omitted the neighboring fixtures’ regeneration guard: the target recovered nine life before observation. The strict predicted-HP assertion remains; the existing guard isolates the hit and diagnostics retain both the packet and actual controller. These fixture errors were corrected without changing gameplay or item eligibility; logs and build snapshots are retained.

See the [acceptance validator](../tools/record_iron_golem_spectral.py) and [native evidence receipt](../tools/reference/iron-golem-spectral-native-evidence.json). The general retained runner/older component logs describe their original bounded scopes; this checkpoint supplies the current Spectral status.

## Remaining work and assets

More complex original-item cold/pierce/absorption combinations, reflected source death during the same release, broader historical-controller outcomes and PvP/protection remain additional acceptance work. Current executable/global RNG/stat-list/event/room equivalence is not certified. Unsupported original properties remain explicit: The Atlantian’s class-skill writer 21 and Baranar’s Star’s unrecorded zero-parameter cold-length fallback are unchanged. Held observation saves do not prove installed old-DLL/concurrent/unheld-save acceptance.

Next Necromancer work includes those bounded integration gaps, Fire Golem’s approved damage/aura/defenses, Mage basic spell/triple/Mastery mapping, barriers and safe-area parking. Druid and remaining classes follow the roadmap. Coverage remains **186 handlers / Necromancer 30; no completed class**.

Body `14`/hue `0x835`, summon `0x376A`, existing contact cues and shared chill/poison particles are identified placeholders. Final five-channel impact/status/expiry feedback and audio belong in [skill effects](skill-effects.md). Iron’s talent-tree/right/quick icons remain in [skill icons](skill-icons.md); this automatic perk adds no separately bindable action or icon.
