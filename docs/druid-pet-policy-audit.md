# Druid pet policy audit

Reviewed 2026-11-18 JST against the installed class package. The mechanical reference remains D2R Reign of the Warlock 3.3.93847, with the approved Sanctuary changes first. The [requirement map](../tools/reference/druid-pet-policy-audit.json) joins 11 policy groups to their current consumers and retained native evidence.

| Approved policy | Consumers and evidence |
| --- | --- |
| Spirit Wolf: Open Wounds at hard 10, cold immunity at hard 20; retain cold-only attacks and baseline unchillability | `SpiritWolfUpgrades`, shared wounds and wolf hit/status consumers; current upgrade and mode tests |
| Dire Wolf: existing movement-only Extra Fast at hard 10, Extra Strong at hard 20 | `DireWolfUpgrades`, native movement/hit consumers; current upgrade and natural-action tests |
| Grizzly: normal-hit stun at hard 10, 10% Crushing Blow at hard 20 | `GrizzlyUpgrades`, ordinary hit and crushing consumers; retained normal/boss chances and durations, Smite exclusion and real contact tests |
| Poison Creeper: 2,000 average base damage over four seconds at rank 20, before Rabies | `DruidCreeperPoison`; retained curve, rounding, synergy, actual poison and persistence checks |
| Creeper captures its inherited stats at birth; emitted fields retain packets | Existing captured context and field scheduler; current learned-cast/respec/field tests |
| All three vines stay vulnerable | Creeper/Recycler bodies and learned actors; current damageability and actual damage checks; no exposure-window immunity |
| Carrion Vine baseline and approved Solar table recovery with whole-point rounding | Existing recovery/model/learned checks; no additional pet balance change |
| Spirits retain baseline behavior | Existing Oak/Wolverine/Barbs birth/aura/casting and saved-state families |
| Raven retains baseline behavior and captured stats | Current Raven casting/snapshot/budget/retarget families |
| Beast birth stats and hard-point upgrades survive later owner changes | Current Druid public-birth snapshot cohort already retains 12 original actors through two reloads |
| Safe-area parking works across all ten Druid summon implementations | New focused actual-region/return controls described below |

The latest 1,139-result Druid cohort already includes the damage, affix, vine, spirit, Raven and current public-birth snapshot families. Those results are reused. No unchanged class suite or formula matrix was repeated. Druid pet counts are unchanged; the Necromancer army-size revision remains limited to its own class.

The new integration fixture creates one real prepared actor of each of the ten summon types, using separate owners so mutually exclusive spirits/vines are not combined into an impossible roster. Actual guarded-region entry must internalize the original pet; repeated resume while safe must leave it parked. Returning through the shared admission service must restore that same actor without healing, mana gain, rank/max-life changes, duplicate follower slots or a Raven attack-budget reset. The fixture then saves the parked actors and repeats the cycle after two actual native reloads. These are offline prepared-body controls. Existing authenticated learned input and public-birth snapshot proofs remain separate. No new authenticated movement, cross-facet, healer/companion or graphical combat claim is made here.

Acceptance uses Integration build 2 and unchanged installed native/content/data. Both integration builds have zero warnings/errors. The focused runner passes **12 results across three boots, three saves and two reloads**: three Druid parking results and nine existing Iron birth/item/resource prerequisites. Its saved manifest retains ten original pets and ten owners. These results are not added to the old class total as independent full-class coverage.

The first run passed initial parking/return for all ten types, then failed Raven's first reload cycle. Offline player bodies load on the Internal map; the temporary fixture region had incorrectly used that map instead of the saved world facet. The corrected fixture registers its region on the saved facet. Failed sources, all five assemblies, artifact/source manifests, logs and the first saved actor manifest are retained under `failed-run-1`. The temporary native world itself was cleaned up by the existing runner and is not claimed preserved. No production behavior changed.

Evidence is retained under `.local/class-skills/druid-safe-area`, including both source archives, accepted Integration2, copied data/artifact hashes, the native log and saved manifests. Original installed saves, configuration and client state retain their hashes. Final effects/audio and talent/hotkey icons remain identified placeholders. Unsupported sunder delivery, historical compatibility and individual modern source/geometry limits remain explicit; this audit does not issue a complete-class or complete-goal certificate.

```bash
python3 tools/record_druid_pet_policy.py --check
```
