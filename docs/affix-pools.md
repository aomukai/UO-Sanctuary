# Affix pool examples

Generated from the pinned D2 affix and item-type catalogs. Each cell is **magic ceiling / rare ceiling** for a single affix at some reachable affix level (1–99). A dash means that property has no eligible affix on that base type. This is neither an item total nor a promise that a low-level item can roll the ceiling. Required levels, exclusions, groups, and weights remain in the full catalog.

| Single-affix stat | Sword | Body armour | Shield | Gloves | Boots | Ring | Amulet |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Life | — / — | 100 / 60 | 60 / 40 | — / — | — / — | 40 / 40 | 100 / 60 |
| Mana | 20 / 20 | 40 / 40 | 20 / 20 | 40 / 40 | 40 / 40 | 120 / 90 | 120 / 90 |
| Strength | 15 / 15 | 20 / 20 | 9 / 9 | 15 / 15 | — / — | 20 / 20 | 30 / 30 |
| Dexterity | — / — | 15 / 9 | — / — | 20 / 15 | 15 / 9 | 20 / 15 | 30 / 20 |
| Enhanced damage % | 300 / 300 | — / — | — / — | — / — | — / — | — / — | — / — |
| Enhanced defence % | — / — | 200 / 200 | 200 / 200 | 200 / 200 | 200 / 200 | — / — | — / — |
| Attack speed % | 40 / 40 | — / — | — / — | 20 / 20 | — / — | — / — | — / — |
| Cast rate % | — / — | — / — | — / — | — / — | — / — | 10 / 10 | 10 / 10 |
| Run/walk speed % | — / — | — / — | — / — | — / — | 40 / 30 | — / — | — / — |
| Hit recovery % | — / — | 24 / 24 | 17 / 17 | — / — | 10 / 10 | — / — | — / — |
| All resistances % | — / — | — / — | 30 / 20 | — / — | — / — | 15 / 11 | 30 / 20 |
| Fire resistance % | 30 / 30 | 30 / 30 | 30 / 30 | 30 / 30 | 40 / 40 | 30 / 30 | 40 / 40 |
| Life stolen % | 9 / 9 | — / — | — / — | 3 / 3 | — / — | 8 / 8 | 6 / 6 |
| Mana stolen % | 9 / 9 | — / — | — / — | 3 / 3 | — / — | 6 / 6 | 8 / 8 |
| Magic find % | — / — | — / — | — / — | 25 / 25 | 35 / 25 | 25 / 15 | 35 / 25 |

Belts, headgear, class-specific items, and other weapon families have their own pools; consult the source eligibility rules for those types.

These are full source-pool ceilings. Only implemented effects are enabled for live item creation, as described in [Balance foundations](balance-foundations.md#affixes-and-rarity); monster drops are disabled.

Examples of level gates: gloves gain 10% IAS at affix level 20 (required character level 15), then 20% IAS at affix level 43 (required level 35). Leather Gloves have quality level 3, so the latter needs item level 44. Boots gain 10/20/30% run/walk at affix levels 12/22/37; magic-only 40% needs affix level 51 (required level 43). Leather Boots have quality level 3, so 40% needs item level 52. Boots never roll IAS. Item level, affix level, and required character level are separate gates.
