#!/usr/bin/env python3
"""Bind saved Chilling Armor return bolts and retained Sorceress contracts."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import sys
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/sorceress-chilling-bolt'
BUILD = 4
PARENT = '3dc8418'
sha = inventory.sha


def historical(name):
    return subprocess.check_output(['git', 'show', PARENT + ':' + name], cwd=ROOT)


def record(check=False):
    inventory.STAGE = STAGE
    tested = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    snapshot(STAGE / f'pre-build-{BUILD}.json', STAGE / 'birth-inputs.json')
    counts = markers(STAGE / 'birth-native-checks.log')
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert sum(counts.values()) == 481 and ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert 'pass across3boots/2reloads' in (STAGE / f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0, str(ROOT / 'tests'))
    from check_chilling_bolt import configuration
    config = configuration(True)
    assert len(config['extra_families']) == 36 and len(config['extra_checks']) == 10
    for name in config['extra_families']:
        assert counts[name + '-check'] == 1 and counts[name + '-reload-check'] == 2
    for name in config['extra_checks']:
        assert counts[name] == 3
    assert len(config['extra_target_input_commands']) == 42
    for name in config['extra_target_input_commands']:
        assert counts[name + '-check'] == counts[name + '-result'] == 3
        assert log.count(' ' + name + ': windup') == 3, name
    for text in ('765 rank/hard goldens,36 complete event/factory comparisons',
                 'TK source/native:rolls=7680 deliveries=2560 deaths=864 pushes=848',
                 'Ordered monster release:1616native complete-source compositions',
                 'NPC return:60488complete-source policies/exact traces'):
        assert text in log, text
    saved = json.loads((STAGE / 'chilling-bolt-fixtures.json').read_text())
    assert saved['Generation'] == 2 and saved['Flight']['Ability'] == 42 and saved['Flight']['Frame'] >= 3
    assert saved['Flight']['Owner'] == saved['Owner'] and saved['Flight']['X'] > saved['Point']['X']
    assert saved['Mana'] > 0 and saved['OwnerLife'] > 0 and saved['TargetLife'] < 12800000
    assert counts['chilling-bolt-reload-result'] == 2
    for kind in ('content', 'integration'):
        text = (STAGE / f'{kind}-build-{BUILD}.log').read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        name = f'{kind}/Sanctuary.{"Content" if kind == "content" else "Integration"}.dll'
        assert sha(STAGE / name) == sha(STAGE / 'focused-accepted-3' / name)
    focused = json.loads((STAGE / 'diagnostic-3/birth-inputs.json').read_text())
    focused_log = (STAGE / 'diagnostic-3/birth-native-checks.log').read_text()
    assert focused['artifacts'] == tested['artifacts'] and sum(markers(STAGE / 'diagnostic-3/birth-native-checks.log').values()) == 18
    assert focused_log.count('Shutting down') == focused_log.count('Writing world save snapshot done') == 3
    assert ' ERR]' not in focused_log and ': failed' not in focused_log
    for path, digest in focused['sourceInputs'].items():
        assert sha(ROOT / path) == digest, path
    assert focused['copiedData'].keys() == tested['copiedData'].keys()
    assert {p for p in tested['copiedData'] if focused['copiedData'][p] != tested['copiedData'][p]} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    prior = json.loads((ROOT / '.local/class-skills/sorceress-cold-armor/birth-inputs.json').read_text())
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert tested['artifacts'][name] == prior['artifacts'][name]
    assert prior['copiedData'].keys() == tested['copiedData'].keys()
    assert {p for p in tested['copiedData'] if prior['copiedData'][p] != tested['copiedData'][p]} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    source = ROOT / 'tools/reference/chilling-armor-source-evidence.json'
    proof = json.loads(source.read_text())
    assert proof['referenceVersion'] == '3.3.93847' and proof['cases'] == 2880 and proof['sanitizers'] == 'ASan/UBSan clean'
    assert proof['currentRow']['*ID'] == 264 and proof['currentRow']['ReturnFire'] == 0 and len(proof['routineHashes']) == 2
    for path, digest in proof['artifacts'].items():
        assert sha(ROOT / path) == digest, path
    for path, digest in proof['currentTable'].items():
        assert sha(ROOT / path) == digest, path
    for path, digest in proof['sourceHashes'].items():
        assert sha(ROOT / '.local/class-skills/d2moo-pinned-source' / path) == digest, path
    parent = ROOT / 'tools/reference/cold-armor-native-evidence.json'
    assert parent.read_bytes() == historical(str(parent.relative_to(ROOT)))
    now = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    before = json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
    for talent in ('sorceress.shiverarmor','sorceress.chillingarmor'):
        changed = next(x for x in now['skills'] if x['talentId'] == talent)
        previous = next(x for x in before['skills'] if x['talentId'] == talent)
        assert changed['verification'] == previous['verification'] == 'partial-native-coverage'
        previous['remaining'] = changed['remaining']
    assert now == before and not now['completedClasses']
    audited = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    assert audited['artifacts'] == tested['artifacts'] and audited['copiedData'] == tested['copiedData']
    assert markers(STAGE / 'audit-native-checks.log') == Counter({'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    assert ' ERR]' not in (STAGE / 'audit-native-checks.log').read_text()
    fingerprints = []
    for name in ('named-item-runtime-audit.json', 'first-loop-affix-audit.json'):
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        previous = json.loads(historical('tools/reference/' + name))
        ignore = ('InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath')
        assert {k: v for k, v in audit.items() if k not in ignore} == {k: v for k, v in previous.items() if k not in ignore}
        fingerprints.append(len(audit['InputHashes']))
    assert fingerprints == [892,21]
    text = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
    text = (STAGE / f'checks-{BUILD}.log').read_text()
    for message in ('Named readiness: 573 definitions accounted for', 'Eight classes, 240 source skills', 'Class handler coverage, blockers, effects and icons recorded separately.'):
        assert message in text
    assert 'Traceback' not in text
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    files = [Path(__file__).resolve(), ROOT / 'tests/check_chilling_bolt.py', ROOT / 'tools/probe_chilling_armor.py', source, parent,
             ROOT / 'tools/build_skill_roadmap.py',
             *(ROOT / 'docs' / name for name in ('sorceress-chilling-bolt-checkpoint.md', 'sorceress-armor-retaliation-preflight.md', 'sorceress-cold-armor-checkpoint.md', 'class-skills-resume-checkpoint.md', 'class-skill-roadmap.md', 'skill-effects.md', 'skill-icons.md')),
             *(STAGE / name for name in (f'pre-build-{BUILD}.json', 'birth-inputs.json', 'birth-native-checks.log', f'full-run-{BUILD}.log', 'chilling-bolt-fixtures.json', 'diagnostic-3/birth-inputs.json', 'diagnostic-3/birth-native-checks.log', 'diagnostic-run-3.log', 'audit-inputs.json', 'audit-native-checks.log', f'audit-run-{BUILD}.log', 'python-tests.log', f'checks-{BUILD}.log', f'content-build-{BUILD}.log', f'integration-build-{BUILD}.log')),
             *(ROOT / 'tools/reference' / name for name in ('named-item-runtime-audit.json', 'first-loop-affix-audit.json'))]
    result = dict(referenceVersion='3.3.93847', component='Chilling Armor saved return bolt', installed=False,
                  graphicalClientVerified=False, completedClasses=[],
                  verified=dict(nativeMarkers=481, focusedMarkers=18, boots=3, reloads=2, saves=3, newServerWireActions=0,
                                retainedServerWireActions=126, rankHardGoldens=765, nativeFactoryComparisons=36,
                                olderEventFactoryCompositions=2880, sameOriginalFlightResumes=2,
                                fixtureFamilies=36, sharedChecks=10, auditMarkers=4,
                                auditFingerprints=fingerprints, pythonTests=71, unchangedInstalledArtifacts=106),
                  markers=dict(sorted(counts.items())), artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
                  established=['Complete older event01 and shared factory compose in2880sanitized cases. Original incoming missile owner,hostility/ReturnFire,absolute coordinates,flags32,negative rank normalization and factory failures are bounded separately from current damage/world services.',
                               'Missile264 uses the saved do1 spell controller,25frames/velocity18/InitSteps0,one reactive emission without extra mana and ReturnFire0. Retained armor rank reads live emission hard/gear values;emitted damage/length/modifiers remain captured and armor expiry does not cancel it. Rankless armor carries captured values and flight rank0 without a fabricated rank.',
                               '765independent rank/hard goldens,36native factory comparisons,actual native arrow/typed immunity/potion/cold,legacy state and harmless offline aging pass. The same original emitted bolt/caster/recipient/gear/wounds/mana survives two real reloads,advances in place,then naturally hits without duplicate flights or resetting resources/positions. Inspection holds onlyNextUtc and does not claim natural lifetime across downtime.',
                               'Focused/final DLL hashes match,with only remaining-work metadata changed between copied data snapshots. Retained126server inputs,shared return/melee/shield scopes,four selected-build audits,71Python/573named/240catalogue/generator checks preserve item decisions and106installed hashes. No deployment/class certificate.'],
                  remaining=['Full modern damage-creator/event/global actor seed,exact initial step/Size/cell/movement,incoming ownership/notification,special/PvP/Sunder/charged/oskill/graphical and old-DLL/unheld concurrent-save acceptance remain open. Existing shared mitigation/return and offline/facet/death policies retain their prior limits.',
                             'Shiver still uses a floating/generic hit with ordinary recovery;its early CheckHit bridge is not full attack-event chronology. Older FillDamageValues and DrainItemDurability have distinct attempted-melee notification sites that require outer-path composition before migration.',
                             'Final Chilling armor/return travel/contact/cold/expiry and audio remain pending;tree/hotkey icons stay separate identified placeholders.'])
    out = ROOT / 'tools/reference/chilling-bolt-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded
    else:
        out.write_text(encoded)
    print('Chilling Armor:481combined/18focused markers,2880source/36native factory cases,765goldens,two same-original flight resumes;four audits/71Python;106installed hashes preserved;remaining modern/Shiver/client/assets explicit.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check', action='store_true')
    record(p.parse_args().check)
