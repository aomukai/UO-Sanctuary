# Pinned D2R named-item source data

Primary mechanical source: [blizzhackers/d2data](https://github.com/blizzhackers/d2data/tree/fa1286949266498b294c40c2942521a7c0d7207a), commit `fa1286949266498b294c40c2942521a7c0d7207a`, retrieved21 October2026. The repository documents these as extractions from the game's CASC data. Its [MIT notice](LICENSE.txt) accompanies the copied tables. No game art, audio, wiki descriptions or downloaded code is imported or executed.

`sources.json` records each immutable URL and SHA-256. The JSON files are unmodified copies. `json/` is the current Reign of the Warlock table layer; `json/base/` provides the pre-expansion comparison for item additions, not Sanctuary's older1.13 balance baseline. The source extraction includes localized English strings, property/stat/type/skill tables and named Cube outputs so names, arguments and special recipes can be retained exactly. The existing Sanctuary source pins and live numeric balance are unchanged.

Run from the repository root:

```sh
python3 tools/build_named_equipment.py
python3 tools/build_named_equipment.py --check
python3 tools/build_named_skill_backlog.py --check
python3 tools/build_named_readiness.py --check
python3 -m unittest discover -s tests -p test_named_equipment.py
.tools/dotnet/dotnet run --project tests/NamedEquipment.Checks -- data/Sanctuary/Balance
```

The compiler validates all source hashes **before** building. It produces `unique-items.json`, `set-items.json`, `equipment-sets.json`, `named-item-bases.json`, `named-item-properties.json`, `named-item-coverage.json` and the [readable catalogue](../../../docs/named-equipment-catalogue.md). Build checks are offline and do not access live DLLs or saves. Do not regenerate the broad foundation catalogue to consume these tables: this separate manifest intentionally preserves its D2R edition independently from the existing1.13 source manifest.

## Interpretation

The skill backlog also uses pinned `Skills`, `SkillDesc` and `States` tables. Runtime readiness is exported by the actual native named-equipment fixture to `tools/reference/named-item-runtime-audit.json`; it records content/native assembly hashes and a complete source/balance/patch fingerprint. Regenerate it after changing those inputs, then run `tools/build_named_readiness.py`. Its return list includes shared non-spell mechanics and the approved custom-art review, as well as individual spell/skill dependencies.

- IDs use the source numeric identity: `unique/<*ID>`, `piece/<*ID>` and `set/<source-row>`. Same-name variants never collapse. Source keys and English display strings are separate; for example `Wihtstan's Guard` displays as **Whitstan's Guard**.
- Every source row and its complete original fields are retained. Current tables contain433 Unique rows:403 source-spawnable,6 named recipe outputs,12 other non-spawnable entries,2 explicitly disabled and10 incomplete reserved rows. All140 Set pieces and35 set definitions are retained;135 pieces in34 sets are source-enabled, and the five inactive Warlord's Glory pieces/their set remain recorded separately. “Source-enabled” does not authorize a new Sanctuary loot path; special sources and source quality-level restrictions still apply (for example, Annihilus and Hellfire Torch have source quality level110).
- All fixed properties, per-piece additional properties, partial set bonuses and full set bonuses keep their slots and arguments. A `sourceMin`/`sourceMax` pair can mean different things for different property functions: a proc's chance/skill level is **not a uniform roll range**. The source property functions, stat IDs, level-scaling fields, groups/weights and original flags remain available for later consumers. Source-disabled `*` property lines are marked, not activated. Canonical property keys resolve case differences such as `Gethit-skill`.
- Set `add func=1` maps each additional slot to a specific sibling in source order, excluding the wearer piece itself. `add func=2` maps additional slot1 to two distinct pieces, slot2 to three, etc. `add func=0` keeps additional properties unconditional. The existing pinned [D2MOO Items.cpp](https://github.com/ThePhrozenKeep/D2MOO/blob/0c0bbd1e6bef9181a3da1c046523af622a925416/source/D2Common/src/Items/Items.cpp), `sub_6FDA4380`, supplies the explicit count/mask behavior. The standalone definition API previews these conditions; it does not apply live stat bonuses.
- One source inconsistency is explicit: Renewed Cold Rupture (`unique/427`) refers to **Gelid-Affix5**, absent from both Properties and PropertyGroups. The corresponding table in [D2R-Excel build3.3.93847](https://github.com/pinkufairy/D2R-Excel/blob/1f16064e09b97e3e65abd6943662207cff00b07f/propertygroups.txt) also has no such group. The catalogue preserves this unresolved reference and reports it through `UnresolvedProperties`; it invents no replacement effect. Other property-group references and their child definitions are resolved.
- [d2rwiki.com](https://d2rwiki.com/uniques) was checked as requested. Its public page advertises417 Uniques; its [set page](https://d2rwiki.com/sets) advertises34 sets/135 pieces. The source tables account for variants and inactive/special rows explicitly, so the generated inventory does not force an unexplained417 count or lose rows to match a headline.

## Boundary of this change

This pass creates the complete named definitions and validated lookup/set-bonus preview API. `NamedEquipmentCatalog` is deliberately not registered in live catalogue loading yet. GearRarity, saved GearState, item constructors, effects, loot, gambling, insurance and other lifecycle paths remain the next integration work in [T49](../../../docs/set-unique-audit.md). The coverage file lists123 source bases missing from the current project catalogue and282 named rows without a current loot appearance. These are integration work, not omitted definitions. Future classes and inventory charm/jewel systems are not enabled by importing their items.


## Class mechanics reference

The class pass pins these matching tables to **D2R 3.3.93847 — Reign of the Warlock**, independently corroborated against D2R-Excel `1f16064e09b97e3e65abd6943662207cff00b07f`. [The reference manifest](../../../data/Sanctuary/skill-reference.json) records all comparison hashes, extraction annotations and the Warlock source IDs. MonStats, MonLvl, Missiles, MonStats2, PetType and MonEquip were added from the same immutable d2data commit for the skill pass. Run `python3 tools/verify_skill_reference.py` offline or `--cross-check` to repeat the immutable remote comparison. Approved Sanctuary rules take precedence; the [mechanical reference notes](../../../docs/skill-mechanical-reference.md) identify unverified engine behavior.

MonEquip retains repeated monster rows in source order. The verifier compares each row by its ordinal so Valkyrie and Assassin summon equipment thresholds cannot collapse to one monster entry. [Valkyrie source evidence](../valkyrie-equipment-engine-evidence.json) separates selection/dispatch proof from pending native item generation and combat.
