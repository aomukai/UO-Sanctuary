# Necromancer roadmap annotation reconciliation

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. This updates verification annotations and documentation; it enables no talent and certifies no class.

Ten stale Necromancer labels are corrected to `partial-native-coverage`. All30talents now carry that bounded label. Seventeen skill rows have changed verification/remaining fields, with source IDs, implementation states, other classes and every completion flag preserved. Notes now reflect existing summon, poison, curse and projectile receipts instead of describing implemented checks as still missing.

The roadmap generator marks its overview and skill tables explicitly. Subsequent writes update those regions while retaining authored preambles, between-table notes and suffixes. Effects and icons remain separate240-row registers. The new roadmap checkpoint paragraph is the only declared prose addition to those three preserved documents; no final artwork is created.

The full [845-marker acceptance receipt](../tools/reference/necromancer-acceptance-native-evidence.json) remains byte-identical to commit `6dc7e60`. Its log, inventories, three boots/two reloads/three saves and all five tested assemblies are preserved. Current C#/project inputs are identical to that frozen build. The only copied-data difference is `class-skill-status.json`, whose changes are restricted to the reviewed Necromancer annotations.

The strict full recorder remains tied to its original annotation bytes. It is not changed to ignore new inputs. The separate [metadata receipt](../tools/reference/necromancer-roadmap-metadata-evidence.json) validates this declared annotation-only transition, preservation of authored text, fresh audit fingerprints and unchanged106installed artifacts. It makes no new gameplay or current-engine parity claim.

Fresh selected-build audits pass all four checks;887/21fingerprints include the updated annotation file and retain identical decision payloads. All71Python tests,573named readiness decisions and the eight-class/240source catalogue pass. Both audit assemblies are the same tested build10DLLs; no rebuild, deployment or save migration occurs. An initial readiness/unit invocation ran before the refreshed audit was copied and correctly rejected its stale fingerprint; that log is preserved. The successful pass follows audit validation/copy.

Reproduce the current checks with:

```sh
python3 tools/build_skill_roadmap.py --check
python3 tools/record_necromancer_roadmap_metadata.py --check
```

The proposal and unapplied-patch record remain historical preparation artifacts; the metadata receipt records their application. Individual barrier linked-death/collision/removal/acquisition and broader native/current-engine/control/credit/PvP/Sunder/client limits remain explicit. Missing effects/audio and talent/hotkey icons remain separately tracked placeholders.

Next: the [Sorceress baseline](sorceress-acceptance-preflight.md). Reuse existing spell/field/projectile/sequence tests, cover missing actual player input and lifecycle cases, and keep Inferno's modern held-input/payment cadence individually deferred. No class is marked complete by this annotation update.
