#!/usr/bin/env python3
"""Bind converted-recipient/elite-aura regression and original reload checks."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/lower-resist-elite'
FOCUS = ROOT / '.local/class-skills/lower-resist-elite-focus'
PARENT = '631c146814f56d8890dc120de73de76efa28c488'
sha = inventory.sha


def record(check=False):
    inventory.STAGE = STAGE
    native = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    focused = inventory.validate_inventory(FOCUS / 'birth-inputs.json')
    audit = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    for folder, name in ((STAGE, 'birth-inputs.json'), (FOCUS, 'birth-inputs.json'), (STAGE, 'audit-inputs.json')):
        snapshot(STAGE / 'pre-build-2.json', folder / name)
    assert native['artifacts'] == focused['artifacts'] == audit['artifacts']
    assert native['copiedData'] == focused['copiedData'] == audit['copiedData']
    previous = ROOT / '.local/class-skills/golem-mastery'
    prior = json.loads((previous / 'birth-inputs.json').read_text())
    changed = {n for n in set(prior['sourceInputs']) | set(native['sourceInputs']) if n.endswith('.cs')
               and prior['sourceInputs'].get(n) != native['sourceInputs'].get(n)}
    assert changed == {'src/Sanctuary.Content/EliteAuras.cs', 'tests/Sanctuary.Integration/LowerResistEliteChecks.cs'}
    assert {n for n in set(prior['copiedData']) | set(native['copiedData'])
            if prior['copiedData'].get(n) != native['copiedData'].get(n)} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert native['artifacts'][name] == prior['artifacts'][name] == sha(STAGE / 'native/Distribution' / name)
    parentraw = subprocess.check_output(['git', 'show', PARENT + ':tools/reference/golem-mastery-native-evidence.json'], cwd=ROOT)
    parent = json.loads(parentraw); baseline = previous / 'birth-native-checks.log'
    assert sha(baseline) == parent['evidence'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    for family in ('lower-resist-elite', 'lower-resist', 'conversion', 'mind-blast', 'paladin-conviction'):
        expected[family + '-check'] += 1
        expected[family + '-reload-check'] += 2
    for name, count in (('conversion-result', 1), ('mind-blast-result', 1), ('mind-blast-reload-result', 2),
                        ('paladin-conviction-result', 1), ('paladin-conviction-reload-result', 2)):
        expected[name] += count
    assert markers(STAGE / 'birth-native-checks.log') == expected and sum(expected.values()) == 445
    assert markers(FOCUS / 'birth-native-checks.log') == dict(
        [('iron-golem-birth-check', 3), ('iron-golem-items-check', 1), ('iron-golem-items-reload-check', 2),
         ('npc-resource-callbacks-check', 1), ('npc-resource-callbacks-reload-check', 2),
         ('lower-resist-elite-check', 1), ('lower-resist-elite-reload-check', 2)])
    for folder in (STAGE, FOCUS):
        log = (folder / 'birth-native-checks.log').read_text()
        assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        saved = json.loads((folder / 'lower-resist-elite-fixtures.json').read_text())
        assert len({saved[n] for n in ('Owner', 'Paladin', 'Target', 'Emitter')}) == 4
        # Independent worlds roll their original species' birth life. Each
        # cohort checks its own unchanged wound balance across both reloads.
        assert saved['Bases'] == [0, 0, 0, 50] and (saved['Life'], saved['Mana']) == (1535 if folder == STAGE else 2303, 22784)
        c = saved['Curse']; conversion = saved['Converted']
        assert c['Ability'] == 91 and c['Source'] == saved['Owner'] and c['Rank'] == 20
        assert c['ElementDeltas'] == dict(Fire=-62, Lightning=-62, Cold=-62, Poison=-62)
        assert conversion['Target'] == saved['Target'] and conversion['Source'] == saved['Paladin']
        assert conversion['Skill'] == 119 and conversion['Life'] is None
    for kind in ('content', 'integration'):
        assert all(t in (STAGE / (kind + '-build-2.log')).read_text() for t in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    before = STAGE / 'before-fix'; old = json.loads((before / 'birth-inputs.json').read_text())
    assert 'Converted recipient missing hostile elite Conviction' in (before / 'birth-native-checks.log').read_text()
    assert sha(before / 'EliteAuras.cs') == prior['sourceInputs']['src/Sanctuary.Content/EliteAuras.cs'] == old['sourceInputs']['src/Sanctuary.Content/EliteAuras.cs']
    assert sha(before / 'LowerResistEliteChecks.cs') == sha(ROOT / 'tests/Sanctuary.Integration/LowerResistEliteChecks.cs')
    for kind in ('content', 'integration'):
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(before / kind / name) == old['artifacts']['Assemblies/' + name]
    diagnostic = STAGE / 'runner-diagnostic-1'; oldrun = json.loads((diagnostic / 'birth-inputs.json').read_text())
    assert oldrun['artifacts'] == native['artifacts'] and oldrun['copiedData'] == native['copiedData']
    assert {n for n in oldrun['sourceInputs'] if oldrun['sourceInputs'][n] != native['sourceInputs'].get(n)} == {'tests/check_lower_resist_elite.py'}
    assert sha(diagnostic / 'runner.py') == oldrun['sourceInputs']['tests/check_lower_resist_elite.py']
    assert markers(diagnostic / 'birth-native-checks.log') == expected and 'AssertionError' in (diagnostic / 'native-run-1.log').read_text()
    assert ': failed' not in (diagnostic / 'birth-native-checks.log').read_text() and ' ERR]' not in (diagnostic / 'birth-native-checks.log').read_text()
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
    files = [Path(__file__).resolve(), ROOT / 'tools/build_skill_roadmap.py',
             *(ROOT / 'tests' / name for name in ('check_lower_resist_elite.py', 'check_lower_resist_elite_focus.py')),
             *(ROOT / 'docs' / name for name in ('lower-resist-elite-checkpoint.md', 'lower-resist-checkpoint.md', 'class-skill-roadmap.md', 'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md')),
             *(STAGE / name for name in ('pre-build-1.json', 'pre-build-2.json', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log',
                                        'native-run-2.log', 'content-build-1.log', 'integration-build-1.log', 'content-build-2.log', 'integration-build-2.log',
                                        'audit-native-checks.log', 'audit-run-1.log', 'python-tests.log', 'readiness-check.log', 'catalogue-check.log',
                                        'roadmap-check.log', 'lower-resist-elite-fixtures.json')),
             *(FOCUS / name for name in ('birth-inputs.json', 'birth-native-checks.log', 'native-run-2.log', 'lower-resist-elite-fixtures.json')),
             *(p for folder in (before, diagnostic, STAGE / 'recorder-diagnostic-1') for p in sorted(folder.rglob('*')) if p.is_file())]
    result = dict(referenceVersion='3.3.93847', parent=PARENT, parentReceiptSha256=hashlib.sha256(parentraw).hexdigest(),
                  component='Legacy elite aura allegiance and Lower Resist/native Conviction coexistence', handlerEnabled=False,
                  goalComplete=False, completedClasses=[],
                  verified=dict(nativeMarkers=445, retainedNativeMarkers=423, focusedMarkers=12, boots=3, reloads=2, saves=3,
                                beforeFixRegressionReproduced=True, unchangedRegressionTestSource=True,
                                correctedRunnerWithIdenticalGameAssemblies=True, originalActors=4, pythonTests=71, namedDecisions=573,
                                auditFingerprints=[992, 21], handlers=191, protectedInstalledArtifactsUnchanged=106,
                                protectedSavesUnchanged=38, protectedConfigurationsUnchanged=20), tested=native,
                  limitations=[
                      'New interaction uses learned Lower Resist windup/release but qualified allegiance and comparison aura services;not new learned Conversion/Mind Blast or client-cast equivalence.',
                      'Explicitly delivered conversion expiry and modifier refresh are isolated controls. Natural expiry remains covered by the retained Conversion suite.',
                      'Original parked actors and inspection leases persist without deadline extension on reload;unheld snapshots and actual old-binary migration remain separate.',
                      'Current executable formula/event/arbitration,full dynamic item/stat/species/geometry/PvP/Sunder/client correspondence and legacy elite Amplify compound replacement remain open.',
                      'No new handler,class completion,deployment or assets. Effects/audio and talent/hotkey icons stay separate;barrier hidedead44/initial graph remains deferred.',
                  ], evidence={str(p.relative_to(ROOT)): sha(p) for p in files})
    target = ROOT / 'tools/reference/lower-resist-elite-native-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Elite aura allegiance fixed:445broader/12focused checks;423parent retained;real negative control;original curse/actors survive2reloads;protected build/saves unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
