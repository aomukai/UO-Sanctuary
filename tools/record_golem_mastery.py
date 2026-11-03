#!/usr/bin/env python3
"""Bind dedicated Mastery lifecycle checks without claiming class completion."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/golem-mastery'
PARENT = '76b96239434de010d78cf72d6be793bd42874e0a'
sha = inventory.sha


def record(check=False):
    inventory.STAGE = STAGE
    native = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    audit = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    for name in ('birth-inputs.json', 'audit-inputs.json'):
        snapshot(STAGE / 'pre-build-2.json', STAGE / name)
    assert native['artifacts'] == audit['artifacts'] and native['copiedData'] == audit['copiedData']
    previous = ROOT / '.local/class-skills/grimoire-automatic'
    prior = json.loads((previous / 'birth-inputs.json').read_text())
    assert {n for n in set(prior['sourceInputs']) | set(native['sourceInputs']) if n.endswith('.cs')
            and prior['sourceInputs'].get(n) != native['sourceInputs'].get(n)} == {'tests/Sanctuary.Integration/GolemMasteryChecks.cs'}
    assert {n for n in set(prior['copiedData']) | set(native['copiedData'])
            if prior['copiedData'].get(n) != native['copiedData'].get(n)} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert native['artifacts'][name] == prior['artifacts'][name] == sha(STAGE / 'native/Distribution' / name)
    parentraw = subprocess.check_output(['git', 'show', PARENT + ':tools/reference/grimoire-automatic-native-evidence.json'], cwd=ROOT)
    parent = json.loads(parentraw)
    baseline = previous / 'birth-native-checks.log'
    assert sha(baseline) == parent['evidence'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    for family in ('golem-mastery', 'clay-golem-birth', 'blood-golem-birth', 'fire-golem-birth'):
        expected[family + '-check'] += 1
        expected[family + '-reload-check'] += 2
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert markers(STAGE / 'birth-native-checks.log') == expected and sum(expected.values()) == 423
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    for name in ('content-build-1.log', 'integration-build-2.log'):
        assert all(s in (STAGE / name).read_text() for s in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    saved = json.loads((STAGE / 'golem-mastery-fixtures.json').read_text())
    assert len(saved) == 4 and {r['Kind'] for r in saved} == {65, 68, 70, 71}
    goldens = {65: (179712, 1420, 43, 44), 68: (321085, 1420, 64, 66),
               70: (411264, 1360, 43, 44), 71: (420672, 1480, 86, 88)}
    for row in saved:
        b = row['Birth']; maximum, attack, minimum, high = goldens[row['Kind']]
        assert b == dict(Rank=2, Mastery=21, Maximum=maximum, Attack=attack, Velocity=34, Minimum=minimum, MaximumDamage=high, Percent=0)
        assert (row['Life'], row['Mana'], row['OwnerLife']) == (maximum - 257, 22784, 37632)
        assert len({row['Owner'], row['Pet'], row['Helm']}) == 3 and (row['Original'] > 0) == (row['Kind'] == 70)
    for name, count in (('named-item-runtime-audit.json', 992), ('first-loop-affix-audit.json', 21)):
        new = json.loads((STAGE / name).read_text())
        assert new == json.loads((ROOT / 'tools/reference' / name).read_text()) and len(new['InputHashes']) == count
        assert new['ContentAssemblySha256'] == native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for filename, digest in new['InputHashes'].items():
            assert sha(ROOT / filename) == digest, filename
        old = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/' + name], cwd=ROOT))
        skip = {'InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath'}
        assert {k: v for k, v in new.items() if k not in skip} == {k: v for k, v in old.items() if k not in skip}
    assert (STAGE / 'audit-native-checks.log').read_text().count(': passed') == 2
    assert 'Ran 71 tests' in (STAGE / 'python-tests.log').read_text() and '\nOK\n' in (STAGE / 'python-tests.log').read_text()
    for name, text in (('readiness-check.log', '573 definitions accounted for'), ('catalogue-check.log', 'Eight classes, 240 source skills'),
                       ('roadmap-check.log', 'Class handler coverage, blockers, effects and icons recorded separately.')):
        assert text in (STAGE / name).read_text()
    diagnostic = STAGE / 'diagnostic-1'
    assert 'Learned once-only golem payment ClayGolem' in (diagnostic / 'birth-native-checks.log').read_text()
    oldinputs = json.loads((diagnostic / 'birth-inputs.json').read_text())
    assert sha(diagnostic / 'GolemMasteryChecks.cs') == oldinputs['sourceInputs']['tests/Sanctuary.Integration/GolemMasteryChecks.cs']
    for kind in ('content', 'integration'):
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(diagnostic / kind / name) == oldinputs['artifacts']['Assemblies/' + name]
    assert sha(diagnostic / 'content/Sanctuary.Content.dll') == native['artifacts']['Assemblies/Sanctuary.Content.dll']
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for name, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / name) == digest, name
    protected = json.loads((ROOT / '.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key, relative, count in (('saves', '.local/server/Saves', 38), ('configuration', '.local/server/Configuration', 20)):
        folder = Path(installed['runtimeRoot']) / relative
        assert len(protected[key]) == count and {str(p.relative_to(folder)): sha(p) for p in folder.rglob('*') if p.is_file()} == protected[key]
    state = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not state['completedClasses'] and sum(row['implementation'] == 'handler-present' for row in state['skills']) == 191
    assert 'Dedicated learned creation' in next(row['remaining'] for row in state['skills'] if row['talentId'] == 'necromancer.golemmastery')
    files = [Path(__file__).resolve(), ROOT / 'tests/check_golem_mastery.py', ROOT / 'tools/build_skill_roadmap.py',
             *(ROOT / 'docs' / name for name in ('golem-mastery-checkpoint.md', 'class-skill-roadmap.md', 'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md')),
             *(STAGE / name for name in ('pre-build-1.json', 'pre-build-2.json', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log',
                                        'native-run-2.log', 'content-build-1.log', 'integration-build-1.log', 'integration-build-2.log', 'audit-native-checks.log',
                                        'audit-run-2.log', 'python-tests.log', 'readiness-check.log', 'catalogue-check.log', 'roadmap-check.log', 'golem-mastery-fixtures.json')),
             *(p for p in sorted(diagnostic.rglob('*')) if p.is_file())]
    result = dict(referenceVersion='3.3.93847', parent=PARENT, parentReceiptSha256=hashlib.sha256(parentraw).hexdigest(),
                  component='Dedicated Golem Mastery learned creation and captured-rank lifecycle', handlerEnabled=False,
                  goalComplete=False, completedClasses=[], productionCSharpUnchanged=True,
                  verified=dict(nativeMarkers=423, retainedNativeMarkers=411, boots=3, reloads=2, saves=3, golemRecipients=4,
                                capturedMastery=21, currentOwnerMastery=1, pythonTests=71, namedDecisions=573, auditFingerprints=[992, 21], handlers=191,
                                protectedInstalledArtifactsUnchanged=106, protectedSavesUnchanged=38, protectedConfigurationsUnchanged=20),
                  tested=native, limitations=[
                      'Dedicated lifecycle coverage reuses existing complete birth source matrices;no new formula or production mechanic is introduced.',
                      'Approved Skeleton-based golem damage is a Sanctuary deviation,not original D2R Mastery damage.',
                      'Learned server windup/release uses manual event timestamps;new right/quick graphical client acceptance is not claimed.',
                      'Original parked actors and inspected wounds/resources survive both reloads;unheld active-world and actual old-binary migration are separate.',
                      'Current executable initializer/events/owner/room/movement/client equivalence and unsupported inherited properties/special recipients remain open.',
                      'No new handler,completed skill/class,deployment or assets;effects/audio and talent/hotkey icons remain separate.',
                  ], evidence={str(p.relative_to(ROOT)): sha(p) for p in files})
    target = ROOT / 'tools/reference/golem-mastery-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Golem Mastery:423checks/411parent retained;four learned recipients;captured21/current1through2reloads;71Python tests;protected build/saves unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
