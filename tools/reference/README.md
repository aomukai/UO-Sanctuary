# Sanctuary reference data and tooling

The [handbook](../../docs/README.md) is the current human-readable entry point. This directory contains machine data, generated reports, and exact qualification inputs. Report output reflects the source and evidence available at generation. Check the linked current code or data before reusing numbers from its historical qualification.

## Class reports

- [Mechanical qualification tables](reports/class-skill-roadmap.md)
- [Talent and binding icons](reports/skill-icons.md)
- [Talent effects and audio](reports/skill-effects.md)

These tables are maintained by `python3 tools/build_skill_roadmap.py`. Keep the generated section markers. The release checks validate pinned source and evidence. The current checkout has changed since that qualification. See [local verification](../../docs/local-development.md#verification-and-evidence).

## Items and professions

- [Profession coverage](reports/profession-coverage.md) · [Affix pool ceilings](reports/affix-pools.md)
- [Named equipment catalogue](reports/named-equipment-catalogue.md) · [Named item readiness](reports/named-item-readiness.md) · [Per-item skill delivery checklist](reports/deferred-named-skills.md)
- [Item property integration](reports/item-property-integration.md) · [Deferred item skill effects](reports/deferred-item-skill-effects.md)

The builders in `tools/` maintain these outputs. The supported runtime and recipe data live in [Balance](../../data/Sanctuary/Balance).

## Rules and receipts

`rules/` holds exact approved inputs still read by current test or review tools. `receipts/` holds historical JSON verification results relocated from the handbook. Rule and receipt bodies retain their original path references for traceability. See [historical records](../../docs/README.md#historical-records) to retrieve an original path, inspect earlier reports or reproduce an older evidence-generation run from its own tree.
