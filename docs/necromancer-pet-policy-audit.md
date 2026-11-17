# Necromancer pet policy audit

Reviewed 2026-11-18 JST against the installed class package and the approved [pet design](pet-adjustment-review.md). Mechanical reference remains D2R Reign of the Warlock 3.3.93847; Sanctuary's approved changes take precedence. The [requirement map](../tools/reference/necromancer-pet-policy-audit.json) binds 22 requirements to their production consumers, fixtures and retained results.

| Approved requirement | Production consumer | Evidence and result |
| --- | --- | --- |
| Warrior hard-10 return of 34; hard-20 existing movement-only Extra Fast | `SkeletonPetUpgrades`, ordinary retaliation and ground movement | Retained upgrade/actual movement checks; current learned birth hard-9 versus hard-20 controls |
| Fire, lightning and ice Mages; saved poison conversion | `NecromancerSummons`, Mage world/missile services | Retained element migration preserves original actors and proportional wounds |
| Basic Sorceress spell scaling plus approved Skeleton Mastery endpoint increase | `MageBasicSkills` | Retained formula, packet, damage and natural-volley checks |
| Hard-5 triples only basic bolts/Charged Bolt volleys | `MageBasicSkills`, `MageOffensiveSkills` | Retained volley/secondary checks; current born hard-4/5 boundary |
| Fire Meteor 10 at hard 10; automatic Enchant 20 at hard 20 | `MageOffensiveSkills`, `MagePetBuffs`, shared Sorceress consumers | Retained Meteor and ally buff checks; current birth/respec controls and free Enchant |
| Lightning Chain Lightning 10 at hard 10; Static Field 5 at hard 20 | `MageOffensiveSkills`, shared chain/static consumers | Retained separate 5% opportunities, actual damage and natural casts |
| Ice Glacial Spike 10 at hard 10; always-on Frozen Armor 20 at hard 20 | `MageOffensiveSkills`, `MagePetBuffs`, shared armor consumer | Retained secondary/buff checks; current birth hard-10 Glacial damage/freeze and armor boundary |
| Clay Warrior curve with Golem Mastery; Stone Skin/Magic Resistant | `GolemPetDamage`, `ClayGolemUpgrades` | Retained damage/affix checks; current public-birth numeric persistence |
| Blood 150% baseline; Open Wounds; 5% Life Tap 5 | `GolemPetDamage`, `NpcOpenWounds`, `NpcHitSkills` | Retained actual contact/proc checks; current born hard-20 wounds and curse after owner respec |
| Iron Clay baseline plus original item; Extra Strong/Spectral Hit | `GolemPetDamage`, original recipe, `IronGolemUpgrades` | Retained original-item/damage/affix delivery; current learned Iron birth and original item persistence |
| Fire 200% physical baseline, own-rank Paladin Holy Fire; CBF/cold immunity | `GolemPetDamage`, `FireGolemUpgrades`, aura consumer | Retained damage/aura checks; current actual chill/freeze refusal and cold-damage immunity |
| Mage/Revive quantity progression, FIFO and live cap changes | `NecromancerArmyLimits`, existing creation/gear/resume hooks | Retained learned casts, rank boundaries, real gear, parking and wardrobe rollback; caps remain 7/5 |
| Permanent Revives, original abilities and separate loot/combat capture | `ReviveNativeActivation`, original creature and equipment services | Retained original actor/loot/no-expiry/owner-death checks; unsupported original contexts remain refused |
| Safe-area parking without recovery/reset | `MinionSafeAreas`, `PersistentMinions`, timed effects | Retained actual region/facet entry, stationary expiry, harmful poison/wounds, no recovery and original roster return |
| Explicit parked-roster healer service; named mercenary stays present | Existing healer menu and registered mercenary ownership | Retained menu eligibility, no automatic parked healing and registered companion exclusion |
| Facet difficulty rescaling preserves life percentage | `SummonDifficulties`, `ReviveFacets` | Retained configured-facet and original Revive rescaling/reload checks |
| Already-fired Mage projectiles survive Mage death | Existing Mage projectile pool | Retained lifetime/damage/reload tests; owner lifecycle restrictions remain |
| Current births retain ranks, unlocks, mastery, wounds, order and RNG | `SummonUpgradeSnapshots`, existing birth dictionaries/recipes | New focused refresh: two Warriors and 18 Mage/golem cases through two actual reloads |

The earlier 953-result Necromancer composition already retained the listed damage, spell, affix, cap, parking and lifecycle families. Its historical upgrade fixtures deliberately remove optional birth metadata to test compatibility. It omitted the separate `warrior-snapshot` and `necro-snapshot` families, so those results alone could not certify current learned birth behavior.

The focused refresh reuses the installed native/content binaries and the integration assembly compiled for the roster UI checkpoint. There is no rebuild or gameplay/data change. The existing native runner executes both omitted families plus its three ordinary prerequisite families: **15 results across three boots, three saves and two reloads**. Six results cover the two snapshot families; nine retain the runner's Iron birth/item/resource prerequisites. These counts are not added to the earlier class result total as new independent class coverage.

Current public casts distinguish hard versus item ranks, retain low and high born unlocks after training/gear/respec, inspect actual secondary spell/status consumers, and preserve original wounds, maxima, order, RNG, recipes and metadata. Their saved cohorts contain two Warriors plus 18 Mage/golem cases. Separate allocation-time mana-loss checks retain the old pet/corpse and remove only the unpaid candidate. Historical missing-supplement actors retain their explicitly documented compatibility behavior until resummoned; this is not an older-binary writer test.

The approved Necromancer pet adjustment consumers are accounted for within these qualified scopes. This does not qualify every Necromancer talent, arbitrary UO species, unsupported equipment, all modern D2R paths or graphical combat. The runtime ledger keeps its partial coverage flags. Shared sunder delivery and monster spell conversion retain their existing boundaries. Final effects/audio and talent/hotkey icons remain separately registered placeholders.

Evidence is retained under `.local/class-skills/necro-pet-snapshot-refresh`, including exact source archive, input/data/artifact hashes, native log and original-actor save manifests. The original installed saves, configuration and client state remain unchanged; production processes are stopped.

```bash
python3 tools/record_necromancer_pet_policy.py --check
```
