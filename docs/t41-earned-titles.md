# T41 — Optional earned titles

Characters can choose an earned class prefix and an earned skill suffix, or hide either independently. Open **Earned titles** at a wardrobe or use **[Titles**. Choices belong to the character and remain selected across class/craft swaps, death and reload. Neither slot has to match the active class or craft. Both start hidden; earning a title does not automatically replace a preference.

## Unlocks

Each class reaching level 99 permanently earns its prefix:

| Class | Prefix |
| --- | --- |
| Amazon / Gargarean | Warmaster |
| Sorceress / Sorcerer | Archmage |
| Necromancer | Deathspeaker |
| Paladin | High Templar |
| Barbarian | Warlord |
| Druid / Druidess | Archdruid |
| Assassin | Shadowmaster |

Achievement names are the same for either sex. Ordinary class aliases remain unchanged.

All thirteen retained professions earn **Grandmaster at trained 100**, **Elder at trained 110**, and **Legendary at trained 120**. Reaching 105 or 115 grants no additional title. Caps, equipment bonuses and temporary effective skill do not count as trained skill. Higher milestones retain the earlier ranks as selectable choices; losing trained skill later does not erase an earned title.

| Skill | Suffix profession |
| --- | --- |
| Alchemy | Alchemist |
| Blacksmithing | Blacksmith |
| Bowcraft/Fletching | Bowyer |
| Carpentry | Carpenter |
| Cartography | Cartographer |
| Cooking | Cook |
| Fishing | Fisher |
| Inscription | Scribe |
| Lumberjacking | Lumberjack |
| Mining | Miner |
| Tailoring | Tailor |
| Tinkering | Tinker |
| Imbuing | Imbuer |

Existing class histories and current trained skills are scanned when the feature loads and on login. Subsequent milestones are recorded at class level-up or native skill change, including inactive crafts and gathering. Unrecorded earlier skill peaks cannot be inferred from the old save.

## Display and selection

The two visible slots produce names such as **Archmage Joe, Legendary Blacksmith**. Either or both can be hidden. The selector shows locked class titles, earned skill milestones and the three rank choices. Applying an unearned rank or forged selection changes nothing. Replaced, replayed and disconnected sessions cannot apply an older response. Title changes are cosmetic and retain normal movement-only restrictions.

Class characters use the selected achievement slots instead of automatic fame/skill/champion wording. Active class and level remain separately visible. Hero of the People and Pillar of Society stay on their certificates and recognition screen; they are not additional selectable name slots. Gathering titles do not grant recognition, wardrobes or housing rights.

Native name, fame, karma, kills, notoriety, guild membership, character stats, allocations and skill values/caps/locks are unchanged. Guild labels and normal notoriety colors remain. Characters without a class retain their native title formatting.

The native 60-byte paperdoll title packet cannot carry a long name plus both achievement slots and a class line. A bounded, versioned UTF-8 message (`TitleProtocol` version 1, extended command `0xF8`, at most 512 text bytes) supplies the full text to ClassicUO. The client uses a readable wrapping panel, updates already-open paperdolls without opening new ones, and clears its bounded cache on session reset. Standard paperdoll/profile text remains available through the native title function; the full tooltip name uses the native property list. Existing combat, stat, belt and skill-binding protocols are unchanged.

## Persistence and validation

`SanctuaryEarnedTitles` version 0 stores each character's class unlock mask, thirteen highest earned skill ranks, and independent prefix/skill/rank selections. It rejects unknown IDs, invalid thresholds, duplicate owners and selections not supported by the saved unlocks without replacing loaded authority. It backfills known milestones without altering native progression. Property-list reads are pure; unlock recording and invalidation occur outside them.

Validation covers thresholds below/at/between all milestones, all seven classes and thirteen skills, lower earned ranks, hiding each slot, inactive-class selections, skill loss, sex-neutral achievement names, raw gameplay/notoriety preservation, legacy formatting, malformed saves and four-boot reloads. Real-client checks exercise the wardrobe entry, forged/stale selections, title choices, hides, class swapping and a name exceeding the standard paperdoll packet limit. Source/installed regressions and copied-world preservation are recorded in ignored `.local/logs/t41` and `.local/logs/deploy-t41`.
