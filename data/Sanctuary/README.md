# Sanctuary data

## Balance catalogs

`Balance/` contains the players-1 numeric foundation: four monster archetypes at levels 1–99, D2 source species references, a complete UO equipment-family inventory, normal D2 reference bases, item-type inheritance, magic/rare affixes, all seven class profiles, and XP references. These catalogs are validated at startup. See [the balance reference](../../docs/balance-foundations.md) for the distinction between source facts, project mappings, active adapters, and deferred loot/effect work. `Balance/sources.json` pins each D2 mechanical table by URL and SHA-256; no artwork or game distribution is included.

`britain.cfg` and `vendors.json` are subsets of the pinned ModernUO distribution, revision `eff9de028136b11e544fd81375d1e0e710e22dd6`, under GPL-3.0-or-later. The license text is in [ModernUO's LICENSE](../../upstream/ModernUO/LICENSE).

`britain.cfg` preserves the entries in `Data/Decoration/Britannia/britain.cfg` whose coordinates fall within inclusive bounds `(1400, 1620)`–`(1490, 1720)`: 330 placements, including eight doors. Headers, item properties, and coordinates are retained.

`vendors.json` preserves three entries from `Data/Spawns/shared/felucca/Vendors.json`, including their GUIDs, NPC types, and respawn settings:

| Location | NPCs |
| --- | --- |
| `(1425, 1690, 0)` | Banker, Minter |
| `(1469, 1668, 0)` | Provisioner, Cobbler |
| `(1447, 1647, 10)` | Armorer, Weaponsmith |

The content assembly uses stock serialized types. Removing it does not remove these objects from an existing save. Restore the pre-installation save if the entire initial population needs to be rolled back; later character changes would also be rolled back.

## Talent catalog

`talents.json` records names, level gates, and prerequisites for the seven Diablo II classes: three trees of ten skills per class. Each tree retains its direct official source URL. Sources are Blizzard's [Arreat Summit skill guide](https://classic.battle.net/diablo2exp/skills/) and [skill basics](https://classic.battle.net/diablo2exp/skills/basics.shtml), consulted on 17 October 2026. Only names and mechanical facts are recorded; no artwork or descriptive passages are bundled. This catalog is separate from the ModernUO data subsets above. Runtime validation rejects incomplete trees, duplicate IDs, invalid level gates, and invalid or cyclic dependencies.
