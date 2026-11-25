# Sanctuary reference data and tooling

The [handbook](../../docs/README.md) is the current human-readable entry point. This directory contains machine data, generated reports, and exact qualification inputs. Report output reflects the source and evidence available at generation. Check the linked current code or data before reusing numbers from its historical qualification.

## Class reports

- [Mechanical qualification tables](reports/class-skill-roadmap.md)
- [Talent and binding icons](reports/skill-icons.md)
- [Talent effects and audio](reports/skill-effects.md)

These tables are maintained by `python3 tools/build_skill_roadmap.py`. Keep the generated section markers. The roadmap checks the pinned qualification inputs, which predate later source changes. See [local verification](../../docs/local-development.md#verification-and-evidence).

## Items and professions

- [Profession coverage](reports/profession-coverage.md) · [Affix pool ceilings](reports/affix-pools.md)
- [Named equipment catalogue](reports/named-equipment-catalogue.md) · [Named item readiness](reports/named-item-readiness.md) · [Per-item skill delivery checklist](reports/deferred-named-skills.md)
- [Item property integration](reports/item-property-integration.md) · [Deferred item skill effects](reports/deferred-item-skill-effects.md)

The builders in `tools/` maintain these outputs. The supported runtime and recipe data live in [Balance](../../data/Sanctuary/Balance). These commands verify a fresh named export against the current assembly and write the affix evidence to the same disposable directory.

```sh
python3 tools/build_named_readiness.py --audit "$stage/named-item-runtime-audit.json" \
  --assembly "$stage/content/Sanctuary.Content.dll" --check
python3 tools/build_first_loop_catalogue.py --source-dir .local/research/foundation \
  --affix-audit "$stage/first-loop-affix-audit.json" --output "$stage/first-loop-evidence.json"
```

The tracked first-loop receipts record an earlier build. The export commands above exercise the latest rules and produce temporary affix evidence.

## Rules and receipts

`rules/` holds exact approved inputs still read by current test or review tools. `receipts/` holds historical JSON verification results relocated from the handbook. Rule and receipt bodies retain their original path references for traceability. See [historical records](../../docs/README.md#historical-records) to retrieve an original path, inspect earlier reports or reproduce an older evidence-generation run from its own tree.
