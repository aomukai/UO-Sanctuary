# Item property integration gaps

**This gap is part of the class-mechanics work.** A supported named-item property does not automatically work on ordinary affixes or NPC equipment. This inventory describes source registration and routing; it is not a complete behavioral certificate.

The ordinary generator accepts **34 codes**. Of the current Valkyrie catalogue’s **478 type-compatible affixes**, its filter would accept **189** and reject **289**, involving **53 property codes**. **48** of those codes already have named-property registrations; the remaining codes are `att-skill, charged, gethit-skill, hit-skill, war`. Affix counts precede level and rarity filtering.

The excluded ordinary-affix families include leech; added elemental/poison damage; regeneration and damage reduction; +class/tree skills; level-scaled life/mana/defense/attack/damage; and effects such as knockback, prevent monster heal, poison-length reduction, half freeze, replenish durability, mana after kills and damage taken to mana. These need shared routing and correct property-specific rolls, rather than blanket whitelist removal.

The broader named-item backlog also contains skill procs, charged skills, oskills, item-granted auras, reanimation, Magic Arrow/Exploding Arrow item replacements, and newer immunity-breaking/Sunder property groups. Some direct-skill gates are waiting for their class handlers; some newer source definitions are unresolved. See [named-item readiness](named-item-readiness.md) and the machine inventory’s recorded gate codes.

Valkyrie additionally needs NPC consumers for the selected equipment. Existing player stat/effect code is a reuse point, not a substitute for source-appropriate summon behavior. Charms remain intentionally excluded by Sanctuary policy.

## Implementation order

1. Connect one shared property representation/alias/range/level-scaled resolver to ordinary and named gear, with existing numeric/save compatibility; keep unsupported events explicit.
2. Implement complete source-quality affix/property rolling and actual NPC equipment stat consumers, reusing the shared combat/resource/skill systems. Do not filter out unsupported source rolls or use player-only effects as NPC proof.
3. After the relevant class mechanics are ready, return to the deferred skill-effect list: skill-event dispatch, charges, oskills/item auras and remaining newer properties, using the relevant class handlers and exact ownership/resource/target/trigger rules. Individually uncertain mechanics remain recorded while unaffected work continues.
4. Verify real ordinary magic/rare and named items plus NPC equipment through damage/resources/skills/events and two save/reload cycles. Refresh acquisition eligibility only after its consumer checks pass.

## Exact ordinary-affix gap in the current Valkyrie pool

| Source property code | Affixes containing the gap | Existing named registration |
| --- | ---: | --- |
| `ac/lvl` | 2 | Yes; integration/consumer checks required |
| `ama` | 4 | Yes; integration/consumer checks required |
| `ass` | 2 | Yes; integration/consumer checks required |
| `att%/lvl` | 1 | Yes; integration/consumer checks required |
| `att-demon` | 6 | Yes; integration/consumer checks required |
| `att-skill` | 7 | No |
| `att-undead` | 5 | Yes; integration/consumer checks required |
| `att/lvl` | 2 | Yes; integration/consumer checks required |
| `bar` | 4 | Yes; integration/consumer checks required |
| `charged` | 42 | No |
| `cold-len` | 9 | Yes; integration/consumer checks required |
| `cold-max` | 9 | Yes; integration/consumer checks required |
| `cold-min` | 9 | Yes; integration/consumer checks required |
| `dmg-cold` | 1 | Yes; integration/consumer checks required |
| `dmg-demon` | 6 | Yes; integration/consumer checks required |
| `dmg-fire` | 1 | Yes; integration/consumer checks required |
| `dmg-ltng` | 1 | Yes; integration/consumer checks required |
| `dmg-pois` | 11 | Yes; integration/consumer checks required |
| `dmg-to-mana` | 1 | Yes; integration/consumer checks required |
| `dmg-undead` | 5 | Yes; integration/consumer checks required |
| `dmg/lvl` | 2 | Yes; integration/consumer checks required |
| `dru` | 2 | Yes; integration/consumer checks required |
| `ease` | 2 | Yes; integration/consumer checks required |
| `fire-max` | 10 | Yes; integration/consumer checks required |
| `fire-min` | 10 | Yes; integration/consumer checks required |
| `gethit-skill` | 8 | No |
| `half-freeze` | 1 | Yes; integration/consumer checks required |
| `hit-skill` | 4 | No |
| `hp/lvl` | 2 | Yes; integration/consumer checks required |
| `indestruct` | 1 | Yes; integration/consumer checks required |
| `knock` | 1 | Yes; integration/consumer checks required |
| `lifesteal` | 10 | Yes; integration/consumer checks required |
| `light` | 6 | Yes; integration/consumer checks required |
| `ltng-max` | 10 | Yes; integration/consumer checks required |
| `ltng-min` | 10 | Yes; integration/consumer checks required |
| `mana-kill` | 2 | Yes; integration/consumer checks required |
| `mana/lvl` | 3 | Yes; integration/consumer checks required |
| `manasteal` | 7 | Yes; integration/consumer checks required |
| `nec` | 2 | Yes; integration/consumer checks required |
| `noheal` | 1 | Yes; integration/consumer checks required |
| `pal` | 2 | Yes; integration/consumer checks required |
| `red-dmg` | 9 | Yes; integration/consumer checks required |
| `red-mag` | 7 | Yes; integration/consumer checks required |
| `regen` | 7 | Yes; integration/consumer checks required |
| `regen-stam` | 3 | Yes; integration/consumer checks required |
| `rep-dur` | 2 | Yes; integration/consumer checks required |
| `res-pois-len` | 3 | Yes; integration/consumer checks required |
| `skilltab` | 72 | Yes; integration/consumer checks required |
| `sock` | 3 | Yes; integration/consumer checks required |
| `sor` | 2 | Yes; integration/consumer checks required |
| `stamdrain` | 1 | Yes; integration/consumer checks required |
| `thorns` | 5 | Yes; integration/consumer checks required |
| `war` | 2 | No |

Some affixes contribute to several rows. “Yes” means the named property resolver registers the code; it does not certify all parameters, ordinary items or NPCs.

Rebuild with `python3 tools/build_property_integration_backlog.py`; use `--check` to validate. [Machine-readable inventory](../tools/reference/item-property-integration-backlog.json) records exact source hashes, affected affixes, and last verified named-item effect gates.
