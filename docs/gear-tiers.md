# D2 normal, exceptional and elite bases

D2 has no universal damage or defence multiplier between these base tiers. Each family links three separately authored records. The pinned `Weapons.txt` and `Armor.txt` already contain those records and their `normcode`, `ubercode` and `ultracode` links; the current Sanctuary runtime imports normal bases only.

## Examples

These are unmodified base values, before magical affixes, ethereal/superior modifiers or character damage bonuses. Multipliers compare arithmetic midpoints of the ranges, exceptional against normal and elite against exceptional; they are not character DPS or chance-to-be-hit multipliers.

| Family | Normal | Exceptional | Elite | Average tier multipliers |
| --- | --- | --- | --- | --- |
| Dagger damage | Dagger: 1–4 | Poignard: 6–18 | Bone Knife: 23–49 | ×4.80, then ×3.00 |
| Blade damage | Blade: 4–15 | Stiletto: 19–36 | Legend Spike: 31–47 | ×2.89, then ×1.42 |
| Quilted armour defence | Quilted Armor: 8–11 | Ghost Armor: 102–117 | Dusk Shroud: 361–467 | ×11.53, then ×3.78 |
| Ancient armour defence | Ancient Armor: 218–233 | Ornate Armor: 417–450 | Sacred Armor: 487–600 | ×1.92, then ×1.25 |

Official references: [normal daggers](https://classic.battle.net/diablo2exp/items/normal/daggers.shtml), [exceptional daggers](https://classic.battle.net/diablo2exp/items/exceptional/daggers.shtml), [elite daggers](https://classic.battle.net/diablo2exp/items/elite/daggers.shtml); [normal armour](https://classic.battle.net/diablo2exp/items/normal/armor.shtml), [exceptional armour](https://classic.battle.net/diablo2exp/items/exceptional/armor.shtml), [elite armour](https://classic.battle.net/diablo2exp/items/elite/armor.shtml). The pinned raw table spells Stiletto as `Stilleto`; the display spelling above follows Blizzard's guide.

The exceptional Dagger's average is 380% higher than normal, while the elite Blade's average is only about 42% higher than exceptional. A global +50% rule would lose both the tier progression and the differences between base families.

## Requirements and progression

Requirements and quality level also belong to the individual record. For example:

| Base | Character level | Strength | Dexterity | Quality level |
| --- | ---: | ---: | ---: | ---: |
| Dagger | 0 | 0 | 0 | 3 |
| Poignard | 19 | 25 | 0 | 31 |
| Bone Knife | 43 | 38 | 75 | 58 |

Affixes can raise the character-level requirement further. Item level, base quality level, affix level, base tier and character-level requirement remain separate values. Rings and amulets have no exceptional/elite base ladder.

Sanctuary's proposed vendor stock bands—normal around 1–40, exceptional around 41–70—are custom, tentative availability bands. They are not D2's required-level boundaries: an exceptional Poignard already requires level 19, and an elite Bone Knife level 43. Vendors will offer normal/exceptional white or blue gear, with exact regional assortments to be designed later.

## Implementation direction

Extend the base catalog and persisted base selection using the linked source records, retaining each record's damage (including separate melee/two-handed/missile fields), defence, speed, requirements, stat damage coefficients, quality level and magic level. Affix eligibility must use the selected tier's actual base record. Jewellery and cosmetic UO-only slots must not acquire invented tier bonuses. Keep UO crafted Exceptional quality separate from a D2 exceptional base tier.

This research and the accompanying vendor/profession rules are design documentation. No blanket multiplier or runtime exceptional/elite conversion is applied in this pass. Item-origin enforcement, vendor creation, Tinkering, Imbuing and javelin mode switching remain future implementation work; see the root [itemisation design](../Britannia-Diablo-Project-Kickoff.md#7-itemisation-and-equipment-economy).
