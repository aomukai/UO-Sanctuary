# Item property integration gaps

**This gap is part of the class-mechanics work.** A supported named-item property does not automatically work on ordinary affixes or NPC equipment. This inventory describes source registration and routing; it is not a complete behavioral certificate.

The ordinary generator accepts **78 codes**. Of the current Valkyrie catalogue’s **478 type-compatible affixes**, its filter would accept **322** and reject **156**, involving **14 property codes**. **9** of those codes already have named-property registrations; the remaining codes are `att-skill, charged, gethit-skill, hit-skill, war`. Affix counts precede level and rarity filtering.

Ordinary leech, elemental/poison ranges, regeneration, damage reduction, level-scaled stats, requirements and maintenance now use the shared property representation. Skill grants/events and socket affixes remain gated. The table below lists the exact remaining ordinary-route gaps; complete NPC property application remains separate work.

The broader named-item backlog also contains skill procs, charged skills, oskills, item-granted auras, reanimation, Magic Arrow/Exploding Arrow item replacements, and newer immunity-breaking/Sunder property groups. Some direct-skill gates are waiting for their class handlers; some newer source definitions are unresolved. See [named-item readiness](named-item-readiness.md) and the machine inventory’s recorded gate codes.

Valkyrie additionally needs NPC consumers for the selected equipment. Existing player stat/effect code is a reuse point, not a substitute for source-appropriate summon behavior. Charms remain intentionally excluded by Sanctuary policy.

## Implementation order

1. Verify the shared ordinary/named scalar/range/parameter resolver through real rolls, identification, equipped/broken/unequipped behavior, combat/resources, maintenance and two save/reload cycles; preserve legacy numeric saves and explicit unsupported events.
2. Implement complete source-quality affix/property rolling and actual NPC equipment stat consumers, reusing the shared combat/resource/skill systems. Do not filter out unsupported source rolls or use player-only effects as NPC proof.
3. After the relevant class mechanics are ready, return to the deferred skill-effect list: skill-event dispatch, charges, oskills/item auras and remaining newer properties, using the relevant class handlers and exact ownership/resource/target/trigger rules. Individually uncertain mechanics remain recorded while unaffected work continues.
4. Verify real ordinary magic/rare and named items plus NPC equipment through damage/resources/skills/events and two save/reload cycles. Refresh acquisition eligibility only after its consumer checks pass.

## Recipient-level numeric suppliers

The prepared original-item reader now takes an explicit recipient level for ordinary and named properties, local armor and weapon endpoints. Its life/mana suppliers retain FP256 fractions. Four source to-hit properties (`att/lvl`, `att%/lvl`, `att-dem/lvl`, `att-und/lvl`) use halves rather than the eighths used by the other registered scaled properties. See [scoped native evidence](../tools/reference/npc-item-suppliers-native-evidence.json). This is a numeric reader, not a complete NPC stat graph or combat/event consumer.

`dmg%/lvl` now uses the dedicated `dmg-max%` field, with a separate maximum calculation after ordinary ED and flat maximum damage. Actual Reaver/Eaglehorn equipment, weapon packets/strikes and original-item reloads verify the correction; see [maximum damage acceptance](../tools/reference/item-maximum-operations-native-evidence.json). The former numeric-reader rejection is removed. Full NPC stat application, callbacks and item procs remain unfinished. Existing base-ED integer rounding remains unchanged while its modern parity is uncertain; by-time operations6/7 and property17 parameter-zero fallback generation remain separate gaps. Named acquisition decisions are unchanged.


## Original-item stat attachment

The prepared C# graph matches the source posting/removal/owner/damage-toggle policy over 12,288 contexts and exact ordered service traces. An explicit adapter wraps actual bound original items and recipients, copies caller-supplied stat arrays and requires recalculation/propagation services. Native original armor/sword fixtures pass attachment, repeat, damage toggle and removal before and after two world reloads. Their aggregate is deliberately a fixture service; no live NPC life/defense/damage setter or automatic full-stat projection is certified. The transient graph is not serialized or replayed. Multi-item ordering, cycles, reentrancy, real recipient-level mapping and complete stat operations remain unfinished. See [scoped attachment acceptance](../tools/reference/item-stat-attachment-native-evidence.json).


## Numeric stat graph

The prepared numeric service now supplies recursive queries, dependency recalculation, propagation and base setters to the ownership graph. Source-assembled metadata covers 368 rows; 4,784 fields and every row ordinal match the owned build. Native results match 105,984 queries and 480 six-stage update cases from 23 complete source routines, including layered keys and value-change callbacks. Actual original armor/sword suppliers enter and leave transient numeric graphs before and after two reloads. The item projection is still explicitly supplied, with no live NPC stat setter or automatic graph replay. Player ModStats registration and world-time/class mapping remain outside the NPC adapter. By-time arithmetic is source-verified under declared clock angles; the world-time item consumer is still unfinished. See [numeric graph acceptance](../tools/reference/item-stat-values-native-evidence.json).


## Exact ordinary-affix gap in the current Valkyrie pool

| Source property code | Affixes containing the gap | Existing named registration |
| --- | ---: | --- |
| `ama` | 4 | Yes; integration/consumer checks required |
| `ass` | 2 | Yes; integration/consumer checks required |
| `att-skill` | 7 | No |
| `bar` | 4 | Yes; integration/consumer checks required |
| `charged` | 42 | No |
| `dru` | 2 | Yes; integration/consumer checks required |
| `gethit-skill` | 8 | No |
| `hit-skill` | 4 | No |
| `nec` | 2 | Yes; integration/consumer checks required |
| `pal` | 2 | Yes; integration/consumer checks required |
| `skilltab` | 72 | Yes; integration/consumer checks required |
| `sock` | 3 | Yes; integration/consumer checks required |
| `sor` | 2 | Yes; integration/consumer checks required |
| `war` | 2 | No |

Some affixes contribute to several rows. “Yes” means the named property resolver registers the code; it does not certify all parameters, ordinary items or NPCs.

Rebuild with `python3 tools/build_property_integration_backlog.py`; use `--check` to validate. [Machine-readable inventory](../tools/reference/item-property-integration-backlog.json) records exact source hashes, affected affixes, and last verified named-item effect gates.
