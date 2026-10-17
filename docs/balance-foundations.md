# Balance foundations

The governing decisions are in the root [design document](../Britannia-Diablo-Project-Kickoff.md#10-dungeon-ecology-and-world-structure). This pass supplies one **players-1 numeric baseline**, normal equipment classifications, affix eligibility/roll rules, class growth, and solo XP. It adds no population, boss, champion, unique, exceptional/elite item tier, or dungeon resistance/immunity variant.

## Source data and reproducibility

`data/Sanctuary/Balance/sources.json` pins the URL and SHA-256 of each consulted D2 1.13 text table. These are normalized mechanical facts; art, localized descriptions, and the game itself are not included. The table extraction is available in the [D2 data repository](https://github.com/fabd/diablo2/tree/master/code/d2_113_data). Blizzard's [class guides](https://classic.battle.net/diablo2exp/classes/), [XP guide](https://classic.battle.net/diablo2exp/basics/experience.shtml), [magic/rare rules](https://classic.battle.net/diablo2exp/items/magic.shtml), [prefix list](https://classic.battle.net/diablo2exp/items/magic/pre.shtml), and [suffix list](https://classic.battle.net/diablo2exp/items/magic/suf.shtml) provide readable references. For formulas and expansion-specific details, use [D2MOO ItemsMagic.cpp](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Game/src/ITEMS/ItemsMagic.cpp) and [MonsterTbls.cpp](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Common/src/DataTbls/MonsterTbls.cpp).

Run `python3 tools/fetch_balance_sources.py --destination .local/research/foundation` to obtain the pinned inputs, then `python3 tools/build_foundations.py --source-dir .local/research/foundation` to regenerate the catalogs. Regeneration is offline. UO classifications come from the pinned ModernUO source plus `tools/reference/uo-equipment-layers.json`, an audit of actual equipment layers in a disposable server world. The integration fixture can refresh that audit with the `SANCTUARY_EQUIPMENT_AUDIT` output path. No live characters or world data are used.

## Monsters: one curve, two future difficulty axes

`monsters.json` contains **396 records: four archetypes × levels 1–99**. `MonsterBalance.For(archetype, level)` returns a validated immutable profile; `profile.Roll(random)` returns one instance's life, attack rating, and defence. Save those assigned values with the creature. Do not roll on property inspection or load.

The D2 expansion `MonLvl` columns supply life, attack rating, defence, damage, and XP baselines. We use Normal source columns at 1–35, Nightmare at 36–65, and Hell at 66–99. These editable boundaries are a project choice, not D2 monster-level caps. They produce explicit stat steps at 36 and 66. There is no player-count multiplier and no selectable dungeon difficulty in this API. Those source columns carry no resistance or immunity into our baseline.

The role profiles are project adaptations for species without a direct D2 equivalent:

| Archetype | Life multiplier | Defence multiplier | Damage multiplier | Animation / impact / recovery | Movement | Range |
| --- | ---: | ---: | ---: | --- | --- | ---: |
| Tank | 150% | 125% | 75% | 640 / 360 / 600 ms | 960 ms/tile | 1 |
| Melee DPS | 100% | 100% | 100% | 760 / 440 / 600 ms | 320 ms/tile | 1 |
| Ranged physical DPS | 80% | 75% | 100% | 800 / 400 / 600 ms | 400 ms/tile | 10 |
| Caster | 70% | 70% | 100% | 800 / 400 / 600 ms | 400 ms/tile | 10 |

Attack rating starts at 101% of the source curve. Per-hit damage spans roughly 50–150% of the role-adjusted source damage, with integer truncation and a minimum of one. Attack/recovery intervals stay constant within a role, allowing increasing life, ratings, and damage to maintain level ordering. Generic caster damage is marked fire as an initial authoring default; species-specific spells and projectile travel remain separate behaviour. Role timings other than the researched skeleton/zombie records are project defaults, not recovered animation data for every species.

Life variance is bounded by approximately 5% and the next level's lower limit. Reserve at least two possible life values; small integer lifts avoid eliminating variance at low levels. Every lower-level maximum life stays strictly below the next level's minimum within the same archetype. Attack rating and defence variance is capped by 5% and half the next-level gap; these may have no variance at small integer values. Damage endpoints never decrease. This orders numeric profiles, not the outcome of every random hit or fight. Tanks can have more life and less damage than a different role at the same level.

The tested skeleton and zombie are explicit anchors in that catalog: melee level 2 has 7–11 life, AR 12, defence 10, 1–3 damage and 34 base XP; tank level 1 has 7–12 life, AR 8, defence 5, 1–3 damage and 33 base XP. Melee level 1 uses 5–6 life to preserve ordering around the skeleton anchor. Their adapters read the catalog and retain their actual A1/A2 choices, stalls, criticals, and regeneration from [the early Normal reference](early-normal-monsters.md). Existing saved life rolls and injuries remain intact. Other stock UO creatures are not silently assigned an archetype or rescaled.

`d2-monsters.json` contains **443 enabled, killable, non-boss source references** with family IDs, base levels, percentage modifiers, and original resistance data. This is reference material for matching species, not a spawn list or an instruction to apply those resistances. It excludes boss/prime-evil/SetBoss/NPC records and zero-XP pets. Each new species still needs an explicit normal-monster adapter; ranged/caster AI is not added by a stat catalog.

Later, dungeon region/depth supplies a separate resistance profile: Hythloth ghosts hostile to physical builds, Covetous populations hostile to lightning, Destard dragons hostile to fire, and so on. A level's numeric curve and its future regional matchups remain independent. The existing researched skeleton/zombie 50 poison resistance is retained; no additional resistance or immunity is introduced here.

## Equipment

`uo-equipment.json` inventories **644 concrete equipment types**, including every concrete descendant of UO's weapon, armour, clothing, jewellery, spellbook, quiver, and talisman families. Direct equippable tools and special-purpose items are also classified. Integration checks reject missing concrete descendants or entries that are not actual item types. `normalDrop` identifies **314 plain base candidates**, excluding named artifacts, fixed-property rewards, variants of concrete bases, ranger rewards, and special-purpose equipment. It is eligibility metadata; it does not enable a new drop table.

Entries have category, canonical stat slot, an explicit normal D2 `baseCode` where applicable, affix-type tag, weight class, `allowsAffixes`, source path, exclusion reason, and **nullable `maxSockets`**. Null means undecided. UO crafted Exceptional quality is distinct from Diablo exceptional/elite tiers and does not add native combat bonuses.

All 644 catalogued types are converted on use, including already-owned equipment: source base damage, defence, speed, attribute damage coefficients, Strength/Dexterity and character-level requirements replace UO combat values. The 314 plain candidates are the future normal-base pool; named artifacts/rewards remain excluded. There are 173 D2 normal reference bases. New conversions and affix rolls persist in `SanctuaryEquipment`; untouched legacy white defence uses a stable item-serial roll until explicitly converted. Inspecting or loading never rerolls equipment.

Canonical stat slots are weapon, shield, head, body, hands, feet, belt, ring and amulet. Only one equipped item contributes per slot; true two-handed bases prevent shield use. UO gorgets, arms, leg armour, bracelets, earrings, cloaks, robes, shirts, trousers, quivers, talismans, tools and other extra layers are **white cosmetic only**: no defence, resistances, attribute/skill bonuses, affixes or run penalty. Hats, gloves, boots and waist belts use their matching D2 pools. Actual necklaces/beads use amulet; rings use ring. Spellbooks use the normal Sorceress orb analogue. Weapons have explicit family analogues, including daggers, swords, axes, maces, polearms, spears, bows/crossbows, throwing weapons, staves, wands, scepters and claws. Class-specific bases retain their class restrictions.

Native UO magical attributes, skill bonuses, material resistances and quality bonuses do not contribute to converted class equipment. Legal saved affixes supply Strength, Dexterity, Energy, Vitality, life, mana, stamina, attack rating, defence, physical damage, elemental resistances, IAS, FCR, FRW and increased blocking. Shield blocking uses the D2 base/class/Dexterity/level formula with a 75% cap; running disables avoidance under the existing project rule. Equipping validates source level and attribute requirements. Only the explicitly marked Necromancer starter dagger grants +1 Teeth. Ordinary daggers have the normal 1–4 damage base and no free skill; one-time legacy migration recognizes the already-named blessed starter.

Running penalties are **0% light, 5% medium, 10% heavy**, counted for body armour and shield only (maximum 20%). Cosmetic layers add none. Item FRW uses `120 × FRW / (120 + FRW)`; running delay is `ceil(baseDelay × 100 / (100 + effectiveFRW − penalty))`. A 20% penalty turns 200 ms into 250 ms. Stat protocol v4 supplies penalty and bonus to the client, whose request pacing and interpolation use the same formula. Walking, turning, ghosts and legacy characters retain their base timing.

**Monster drops are disabled globally**, including gold, equipment and pre-existing packed loot on wild creatures. Corpses remain available for summoning and harvesting; player corpses and controlled-pet belongings are preserved. XP continues normally. Future loot tables must call `GearProfiles.FromMonster` with the killed monster's level as item level, then persist the resulting roll. There are no rarity drop chances yet.

## Affixes and rarity

`affixes.json` preserves **967 spawnable, nonzero-frequency prefix/suffix records**, with stable source row IDs, allowed/excluded item types, affix levels and maximum levels, required levels, class restrictions, weights, groups, and modifier parameters. `item-types.json` supplies the inherited type graph; for example a sword also qualifies as a melee weapon and weapon. Two source typos are normalized explicitly: trailing space in `ring `, and `staff` → `staf`. The level-110 source affix is retained as unreachable reference data, not clamped down to make it available.

The generated [affix pool comparison](affix-pools.md) shows selected single-affix ceilings by equipment type and magic/rare eligibility. The full catalog retains the level gates and exact ranges behind those ceilings.

`AffixBalance.Eligible` answers whether an affix may roll for a base type, rarity, and affix level. `AffixBalance.Roll` returns a pure `GearRoll` containing selected affix IDs and their modifier results:

- Normal rarity: no affixes.
- Magic: at most one prefix and one suffix; target both 25%, prefix only 25%, suffix only 50%.
- Rare: target 3/4/5/6 affixes with weights 1/2/3/2, at most three prefixes and three suffixes. A scarce eligible pool can yield fewer.
- Reject duplicate groups across the item, excluded types, expired/out-of-level records, and magic-only affixes on rares.
- Use the source frequency; a base with magic level weights by frequency × affix level.
- Socket affixes remain present in reference data but are ineligible while our socket limits are undecided.

Affix level uses `max(itemLevel, qualityLevel)`. Add a base's magic level if nonzero; otherwise subtract half the quality level below the high-level threshold, or use `2 × effectiveItemLevel − 99` above it. Clamp the result to 1–99. Base tier, rarity, item level, affix level, and character requirement are separate values. Magic-only eligibility supplies higher ceilings on certain stats; rares have more combinations, not a blanket increase to each magnitude.

Numeric modifiers roll within their source range. Proc records retain chance/skill level separately; charged skills retain charges/level parameters; combined elemental damage retains its endpoints and duration; parameter-driven modifiers retain their scaling parameter. Those structured records are not incorrectly treated as numeric min/max rolls.

`GearProfiles.Convert` attaches a validated base/rarity/item-level/affix roll to a live item and saves the exact affix IDs and values. The live pool is filtered to fully implemented numeric effects; the 967-record reference roller remains complete. Skills, charges, procs, elemental damage, recovery, leech, magic find and other unsupported effects cannot appear through live creation yet. Every modifier of a compound affix must be implemented before the affix is enabled. Saved rolls are checked for type, level, rarity, groups, counts, value ranges and required level on load. This pass adds no identification UI, exceptional/elite bases, uniques or drop tables.

Examples: gloves can roll 10% IAS at affix level 20 and 20% at 43, with required character levels 15/35; boots cannot roll IAS. Magic boots can reach 40% FRW while rares cap at 30%. A magic ring can reach 120 mana versus a rare ring's 90, and body armour 100 life versus 60. Higher item level unlocks eligible tiers; it does not guarantee a higher roll or make every property stronger.

## Classes and XP

`classes.json` is an independent CharStats reference for the seven implemented classes. Tests compare all starting attributes and resource pools to it, then advance and allocate all 490 earned points through levels 1–99 for each class. HP per Vitality, mana per Energy, stamina per Vitality, and all per-level growth retain quarter-point precision until the final sum. The existing class profiles already matched; no player reset or progression migration is required.

`experience.json` independently checks every cumulative threshold and high-level reduction. `MonsterExperience.Award` applies the solo level-difference rules, then the D2 `/1024` reduction from player level 70 onward. For players below 25, positive differences of 6/7/8/9/10+ levels give 88/68/36/15/2%; monsters 6/7/8/9/10+ levels below give 81/62/43/24/5%. At player level 25+, higher monsters instead scale by `playerLevel / monsterLevel`. Level 99 earns no further XP. Integer truncation is intentional; tiny reduced rewards may become zero. Pet kills credit their owner, and revived creatures cannot pay twice. Parties and player-count scaling are outside this players-1 pass.

## Validation

Startup rejects incomplete or nonmonotonic monster curves, unknown/cyclic item types, duplicate IDs, illegal ranges, and malformed catalog entries. Isolated integration checks exercise all 396 profiles and deterministic spawn samples, all seven classes at every level, every XP threshold/reduction, reflection-based equipment coverage, actual running penalties and client snapshots, and legal rolls across normal reference bases, rarities, levels, and seeds. Gear checks instantiate all 314 plain base candidates, verify cosmetic slots and native-property suppression, positive/negative starter migration, real attribute/resource and IAS/FCR/FRW effects, equip requirements, disabled monster loot, retained pet belongings, and exact roll persistence. Actual-client checks cover stats/level-up controls, heavy running penalties and legally rolled attack/cast timing. Existing creation, death, pet, combat-timing, and save/reload fixtures remain part of verification. No integration fixture is installed in the live server.
