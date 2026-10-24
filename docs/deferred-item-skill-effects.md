# Deferred item skill effects

User decision: return to skill-dependent item effects after the relevant class mechanics are implemented. A finished skill handler still needs its item trigger, grant, aura or charge delivery system. Existing supported bonuses keep working; this list does not disable them.

| Return task | Source property codes | Required work |
| --- | --- | --- |
| Chance to cast on attack, on striking, or when struck | `att-skill, hit-skill, gethit-skill` | Trigger stage, chance/rank, target, ownership, delivery and cooldown; then exercise each referenced class skill. |
| Chance to cast on kill, death, or level-up | `kill-skill, death-skill, levelup-skill` | Correct event and caster/target lifetime; a working spell alone does not implement the event system. |
| Charged skills | `charged` | Use/recharge rules, rank, resource policy, charge depletion and persisted charges. |
| Cross-class skill grants | `oskill` | Rank/grant rules, class restrictions, bindings and equipment/lifecycle removal. |
| Aura while equipped | `aura` | Item aura suppliers, combining items/ranks, recipients, pulses and equipment removal. |
| Reanimate as a monster | `reanimate` | Source species, on-kill chance, ownership, original monster abilities, lifetime and persistence; do not substitute a generic pet. |
| Item replacement arrows | `magicarrow, explosivearrow` | Connect the actual item attack to the existing class projectile/area mechanics and verify ammunition/resource rules. |
| Direct and general skill bonuses | `skill, skill-rand, class/tree/all-skill bonuses` | Review the referenced class handlers and item-grant adapters; preserve ordinary hard-point versus effective-rank rules. Warlock bonuses remain included. |

For each returned effect: identify every referenced skill/source parameter; verify its handler; implement the exact trigger/target/rank/ownership/resource rules; test real item acquisition/equip/removal/broken/unidentified behavior and two save/reload cycles; then review the item’s remaining runtime gates.

Source uncertainties remain explicit. Unresolved property groups do not receive invented effects. Charms remain intentionally excluded. Missing visual assets remain independently tracked and do not block mechanical delivery.

The [named-item return ledger](deferred-named-skills.md) lists affected named items, spells and set partners. The [shared property gap list](item-property-integration.md) lists ordinary-affix and NPC integration work. The [visual effects](skill-effects.md) and [skill icons](skill-icons.md) registers remain separate.

Rebuild with `python3 tools/build_property_integration_backlog.py`; machine details are in [item-property-integration-backlog.json](../tools/reference/item-property-integration-backlog.json).
