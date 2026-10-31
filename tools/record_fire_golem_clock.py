#!/usr/bin/env python3
"""Bind the isolated G3 clock increment without claiming complete Fire combat."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/fire-golem-clock'
sha = inventory.sha


def record(build, run, check=False):
    inventory.STAGE = STAGE
    inputs = {name: inventory.validate_inventory(STAGE / (name + '-inputs.json')) for name in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts']
        assert value['copiedData'] == inputs['birth']['copiedData']
    previous = ROOT / 'tools/reference/fire-golem-absorption-native-evidence.json'
    predecessor = json.loads(previous.read_text())
    ref = predecessor['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before) == set(after)
    changed = {p for p in before if before[p] != after[p]}
    assert changed == {'src/Sanctuary.Content/FireGolemBody.cs', 'tests/Sanctuary.Integration/FireGolemBirthChecks.cs'}
    assert set(old['copiedData']) == set(inputs['birth']['copiedData'])
    assert {p for p, h in old['copiedData'].items() if inputs['birth']['copiedData'][p] != h} == {'Data/Sanctuary/Balance/necromancer-summons.json'}
    old_data = json.loads(subprocess.check_output(['git', 'show', 'c303e87:data/Sanctuary/Balance/necromancer-summons.json']))
    current_data = json.loads((ROOT / 'data/Sanctuary/Balance/necromancer-summons.json').read_text())
    assert [r for r in current_data if r['key'] != 'firegolem-animation'] == old_data
    row = next(r['fields'] for r in current_data if r['key'] == 'firegolem-animation')
    assert [row[k] for k in ['A1Frames', 'A1Speed', 'A1Action']] == [17, 256, 10]
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/fire-golem-absorption/birth-native-checks.log'
    assert sha(baseline) == predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    assert markers(STAGE / 'birth-native-checks.log') == markers(baseline)
    assert sum(markers(baseline).values()) == 360
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    fire = json.loads((STAGE / 'fire-golem-birth-fixtures.json').read_text())
    assert fire['Generation'] == 2 and fire['Maximum'] == 216345 and fire['Life'] == 216345 - 257
    absorption = json.loads((STAGE / 'fire-golem-absorption-fixtures.json').read_text())
    assert absorption['Generation'] == 2 and absorption['Life'] == 84134 - 10000 - 208
    for kind in ['content', 'integration']:
        text = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(x in text for x in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name, count in [('named-item-runtime-audit.json', 842), ('first-loop-affix-audit.json', 21)]:
        audit = json.loads((STAGE / name).read_text())
        prior = json.loads((ROOT / '.local/class-skills/fire-golem-absorption' / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert all(audit[k] == v for k, v in prior.items() if k not in ['ContentAssemblyPath', 'ContentAssemblySha256', 'InputHashes'])
        assert len(audit['InputHashes']) == count
        assert audit['ContentAssemblySha256'] == inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
    assert markers(STAGE / 'audit-native-checks.log') == Counter({'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    audit_log = (STAGE / 'audit-native-checks.log').read_text()
    assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count('Shutting down') == 1
    tests = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in tests and '\nOK\n' in tests
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    proof_path = ROOT / 'tools/reference/fire-golem-mode-source-evidence.json'
    proof = json.loads(proof_path.read_text())
    assert proof['probeSha256'] == sha(ROOT / 'tools/probe_fire_golem_mode.py')
    assert proof['cases'] == 2880 and proof['ordinaryCases'] == 480 and proof['guardCases'] == 2400
    for path, digest in proof['artifacts'].items():
        assert sha(ROOT / path) == digest, path
    files = [previous, proof_path, Path(__file__), *[ROOT / p for p in changed]]
    files += [ROOT / p for p in ['tools/probe_fire_golem_mode.py', 'tools/build_class_skills.py', 'tests/check_fire_golem_clock.py',
              'data/Sanctuary/Balance/necromancer-summons.json', 'tools/reference/fire-golem-world-preflight.json',
              'docs/fire-golem-clock-checkpoint.md', 'docs/class-skills-resume-checkpoint.md', 'docs/class-skill-roadmap.md',
              'docs/necromancer-mechanical-audit.md', 'docs/skill-mechanical-reference.md', 'docs/skeletal-mage-upgrade-plan.md',
              'tools/reference/named-item-runtime-audit.json', 'tools/reference/first-loop-affix-audit.json']]
    files += [STAGE / p for p in [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log',
              f'run-{run}.log', f'audit-run-{run}.log', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log',
              'audit-native-checks.log', 'python-tests.log', 'fire-golem-birth-fixtures.json', 'fire-golem-absorption-fixtures.json']]
    result = dict(referenceVersion='3.3.93847', component='Learned Fire Golem G3 A1 clock', installed=False,
                  goalComplete=False, completedClasses=[], build=build, run=run,
                  verified=dict(nativeMarkers=360, retainedPredecessorMarkers=360, boots=3, actualReloads=2, saves=3,
                                auditMarkers=4, pythonTests=71, namedDefinitions=573, auditFingerprints=[842, 21],
                                unchangedInstalledArtifacts=106, sourceContexts=2880, baselineActionFrame=10, baselineEndFrame=17,
                                baselineActionMs=400, baselineCycleMs=680, halfSpeedFrames=[20, 34], cappedRateFrames=[6, 10],
                                observedConnectedFireRelease=False, handlers=186, necromancerHandlers=30),
                  inputInventories={name: dict(path=str((STAGE / (name + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (name + '-inputs.json'))) for name in inputs},
                  changedSourceHashes={path: after[path] for path in sorted(changed)},
                  artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
                  established=[
                      'New learned Fire subtype uses the shared G3 event/rate planner instead of a generic half-cycle. Native supplier checks cover400/680ms baseline,50percent slow,slow removal,175percent final rate cap and reconstruction across2reloads. Existing plain-minion saves retain their path.',
                      'Only the6-mode G3runtime row is added;all existing summon rows are unchanged.360retained native markers,6Fire right/quick casts,4audits/71Python and clean builds pass.573named decisions,staged kernel and106installed artifacts are unchanged.',
                      'Separate complete legacy Fire mode execution covers480ordinary/2400guard contexts. Owned current formula symbol indices support the older whole-point fire-stat chain;the optional noRatio element-field discrepancy is recorded without changing source or selecting guessed modern behavior.'],
                  remaining=[
                      'No observed connected Fire release or complete current-executable mode-entry certificate is claimed. Mid-animation rescheduling,initial S1/AI/movement and world/client parity remain open.',
                      'Fire outgoing optional-monster-element behavior,RNG order,unarmed/critical/contact,Holy Fire weapon/pulse and death/replacement explosion remain unfinished;absorption full recipient-return/initializer/event parity and historical migration remain separate.',
                      'No completed class,deployment or new broad1180-marker run. Effects/audio and tree/hotkey icons remain separate placeholders. Extra pet abilities unlocked by hard points remain scheduled after all baseline skills.'])
    destination = ROOT / 'tools/reference/fire-golem-clock-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert destination.read_text() == rendered
    else:
        destination.write_text(rendered)
    print('Fire G3 clock:360markers/3boots/2reloads,400/680ms suppliers,4audits/71Python;installed unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.check)
