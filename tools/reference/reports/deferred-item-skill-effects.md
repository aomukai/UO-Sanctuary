# Deferred item skill effects

**Later design discussion — 2026-11-17 JST.** The user wants to revisit gear with skill charges and original skills granted by equipment, potentially through crafting, after the class work. Reuse the existing skill mechanics and delivery systems where they fit. Eligible skills, ranks, costs, recharge methods, recipes and acquisition rules are undecided; this note does not introduce them. Charms and runewords remain excluded.

**Current direct-grant returns.** [Warpspear](https://github.com/aomukai/UO-Sanctuary/blob/528dd686e310f2885504ad2e6ca4ab47ba254ef5/docs/warpspear-grants-checkpoint.md), [three Sorceress staffs](https://github.com/aomukai/UO-Sanctuary/blob/528dd686e310f2885504ad2e6ca4ab47ba254ef5/docs/sorceress-staff-grants-checkpoint.md), and [Fire Bolt/mastery originals](https://github.com/aomukai/UO-Sanctuary/blob/528dd686e310f2885504ad2e6ca4ab47ba254ef5/docs/sorceress-mastery-grants-checkpoint.md) qualify their complete items, normal active/passive consumers and two reloads. The registry now has 31 entries. The latest return includes all five Tal Rasha originals and their partial/full thresholds. These scopes do not qualify proc/charge/aura delivery or whole Hellslayer admission; other item grants retain their own gates.

**Prepared player Fire Ball path.** The [producer](https://github.com/aomukai/UO-Sanctuary/blob/528dd686e310f2885504ad2e6ca4ab47ba254ef5/docs/player-item-fireball-checkpoint.md), [protected callback frames](https://github.com/aomukai/UO-Sanctuary/blob/528dd686e310f2885504ad2e6ca4ab47ba254ef5/docs/player-item-proc-frames-checkpoint.md) and [saved player proc host](https://github.com/aomukai/UO-Sanctuary/blob/528dd686e310f2885504ad2e6ca4ab47ba254ef5/docs/player-item-proc-host-checkpoint.md) now compose source dispatch with item-owned provider fields, per-player RNG, actual equipment/hand/trade subscriptions and native profile saves. Full original proc projection, initial seed acquisition and normal combat phase callers remain unwired, along with whole Hellslayer/golem admission. Registered event-only carriers are not complete named-item proof; no proc item becomes available yet.

**Current NPC item-spell scope.** The historical Shadow Hydra62 gap is resolved by the [NPC-owned Hydra world/item-proc checkpoint](https://github.com/aomukai/UO-Sanctuary/blob/528dd686e310f2885504ad2e6ca4ab47ba254ef5/docs/npc-hydra-world-checkpoint.md), including the original allocation9 loadout. The [Fire Ball producer checkpoint](https://github.com/aomukai/UO-Sanctuary/blob/528dd686e310f2885504ad2e6ca4ab47ba254ef5/docs/npc-item-fireball-checkpoint.md) adds a separately qualified explicit NPC proc route; whole Hellslayer projection/acquisition and automatic learned golem registration remain unfinished. Historical receipts retain their own source/artifact scope.

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

Rebuild with `python3 tools/build_property_integration_backlog.py`; machine details are in [item-property-integration-backlog.json](../item-property-integration-backlog.json).
