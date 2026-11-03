#!/usr/bin/env python3
"""Bind actual Tainted transport, retained NPC services and original saves."""
import argparse
import json
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/tainted-fireball'
FOCUS = ROOT / '.local/class-skills/tainted-fireball-focus'
PARENT = '84c587cacc3adf3c2273baad936d0b08579f6d63'
sha = inventory.sha


def record(compare=False):
    inventory.STAGE = STAGE
    native = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    audit = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    focused = inventory.validate_inventory(FOCUS / 'birth-inputs.json')
    for folder, name in ((STAGE, 'birth-inputs.json'), (STAGE, 'audit-inputs.json'), (FOCUS, 'birth-inputs.json')):
        snapshot(STAGE / 'pre-build-4.json', folder / name)
    assert native['artifacts'] == audit['artifacts'] == focused['artifacts']
    assert native['copiedData'] == audit['copiedData'] == focused['copiedData']
    prior = json.loads((ROOT / '.local/class-skills/tainted-body/birth-inputs.json').read_text())
    assert {n for n in set(prior['sourceInputs']) | set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n) != native['sourceInputs'].get(n)} == {
        'src/Sanctuary.Content/NpcEquipmentRandom.cs', 'src/Sanctuary.Content/WarlockTaintedBody.cs',
        'src/Sanctuary.Content/TaintedFireBallMissiles.cs', 'tests/Sanctuary.Integration/TaintedFireBallChecks.cs'}
    assert {n for n in set(prior['copiedData']) | set(native['copiedData']) if prior['copiedData'].get(n) != native['copiedData'].get(n)} == {
        'Data/Sanctuary/Balance/tainted-fireball-flight.json', 'Data/Sanctuary/Balance/class-skill-status.json'}
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert native['artifacts'][name] == prior['artifacts'][name] == sha(STAGE / 'native/Distribution' / name)
    source_path = ROOT / 'tools/reference/tainted-fireball-flight-source-review.json'
    source = json.loads(source_path.read_text())
    assert source['referenceVersion'] == '3.3.93847' and not source['currentExecutableParity'] and not source['newSourceRoutinesExecuted'] and not source['handlerEnabled']
    for path, digest in source['inputs'].items():
        assert sha(ROOT / path) == digest, path
    baseline = ROOT / '.local/class-skills/tainted-body/birth-native-checks.log'
    parent = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/tainted-body-native-evidence.json'], cwd=ROOT))
    assert sha(baseline) == parent['evidence'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    expected['tainted-fireball-check'] += 1; expected['tainted-fireball-reload-check'] += 2; expected['tainted-fireball-reload-result'] += 2
    assert sum(expected.values()) == 372 and markers(STAGE / 'birth-native-checks.log') == expected
    for folder, count in ((STAGE, 372), (FOCUS, 40)):
        log = (folder / 'birth-native-checks.log').read_text()
        assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == count
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        assert log.count('tainted-fireball-reload-result: passed') == 2
        state = json.loads((folder / 'tainted-fireball-fixtures.json').read_text())
        assert state['Generation'] == 2 and state['Flights'] == [] and state['Body'] != state['Owner'] and state['Helm'] > 0
        assert state['Mana'] == 22784 and state['Life'] == 116786
        seed = (666 << 32) | 173
        for _ in range(2):
            seed = 0x6AC690C5 * (seed & 0xffffffff) + (seed >> 32)
        assert state['ActorSeed'] == seed
    for kind in ('content', 'integration'):
        assert all(t in (STAGE / f'{kind}-build-4.log').read_text() for t in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for name, count in (('named-item-runtime-audit.json', 976), ('first-loop-affix-audit.json', 21)):
        current = json.loads((STAGE / name).read_text())
        assert current == json.loads((ROOT / 'tools/reference' / name).read_text()) and len(current['InputHashes']) == count
        assert current['ContentAssemblySha256'] == native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path, digest in current['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        old = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/' + name], cwd=ROOT))
        skip = {'InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath'}
        assert {k: v for k, v in current.items() if k not in skip} == {k: v for k, v in old.items() if k not in skip}
    assert (STAGE / 'audit-native-checks.log').read_text().count(': passed') == 2
    assert 'Ran 71 tests' in (STAGE / 'python-tests.log').read_text() and '\nOK\n' in (STAGE / 'python-tests.log').read_text()
    for name, text in (('readiness-check.log', '573 definitions accounted for'), ('catalogue-check.log', 'Eight classes, 240 source skills'),
                       ('roadmap-check.log', 'Class handler coverage, blockers, effects and icons recorded separately.'), ('source-check.log', 'owned734 flight fields')):
        assert text in (STAGE / name).read_text()
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    for name, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / name) == digest, name
    preserved = json.loads((ROOT / '.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key, relative in (('saves', '.local/server/Saves'), ('configuration', '.local/server/Configuration')):
        folder = Path(installed['runtimeRoot']) / relative
        assert {str(p.relative_to(folder)): sha(p) for p in folder.rglob('*') if p.is_file()} == preserved[key]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation'] == 'handler-present' for s in status['skills']) == 191
    for name in ('diagnostic-1', 'diagnostic-2'):
        assert 'Slow Missiles not captured on NPC' in (STAGE / name / 'focus/birth-native-checks.log').read_text()
        assert sha(STAGE / name / 'content/Sanctuary.Content.dll') == native['artifacts']['Assemblies/Sanctuary.Content.dll']
    accepted = STAGE / 'accepted-build-3'
    assert (accepted / 'birth-native-checks.log').read_text().count(': passed') == 372
    assert sha(accepted / 'content/Sanctuary.Content.dll') == native['artifacts']['Assemblies/Sanctuary.Content.dll']
    files = [Path(__file__).resolve(), source_path, ROOT / 'tools/review_tainted_fireball_flight.py', ROOT / 'tools/build_class_skills.py', ROOT / 'tools/build_skill_roadmap.py', baseline,
        ROOT / 'tests/check_tainted_fireball.py', ROOT / 'tests/check_tainted_fireball_focus.py',
        *(ROOT / 'docs' / n for n in ('tainted-fireball-checkpoint.md', 'class-skill-roadmap.md', 'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md', 'skill-effects.md', 'skill-icons.md')),
        *(STAGE / n for n in ('pre-build-4.json', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'native-run-2.log', 'audit-native-checks.log', 'audit-run-2.log',
            'content-build-4.log', 'integration-build-4.log', 'source-check.log', 'python-tests.log', 'readiness-check.log', 'catalogue-check.log', 'roadmap-check.log', 'installation-check.log', 'tainted-fireball-fixtures.json')),
        *(FOCUS / n for n in ('birth-inputs.json', 'birth-native-checks.log', 'native-run-4.log', 'tainted-fireball-fixtures.json')),
        *(p for name in ('diagnostic-1', 'diagnostic-2', 'accepted-build-3') for p in (STAGE / name).rglob('*') if p.is_file() and not p.is_symlink())]
    result = dict(referenceVersion='3.3.93847', parent=PARENT, component='Explicit Tainted NPC422 release and original saved missile734 transport',
        handlerEnabled=False, goalComplete=False, completedClasses=[], verified=dict(nativeMarkers=372, retainedNativeMarkers=367, focusedNativeMarkers=40,
            boots=3, reloads=2, saves=3, originalFlightResumptions=2, pythonTests=71, namedDecisions=573, auditFingerprints=[976, 21], handlers=191, protectedInstalledArtifactsUnchanged=106),
        tested=native, limitations=[
            'Explicit prepared NPC release and actual transport only; no automatic GenericPet controller/selection/mode timing, NPC420 aura, Blood Boil or learned summon/cap/payment/lifecycle completion.',
            'Held/rearmed original flight inspection clocks are explicit. Native room/list/terrain/footprint/world seed/global phase and current source-removal behavior remain adapters or uncertain. No unheld downtime claim.',
            'Ordinary non-pet NPC recipients only; source passive avoidance, PvP, controlled/summoned actors, reflective recipients and unsupported return/mode services refuse or remain separate.',
            'Mixed positive/negative intrinsic rolls, copied raw damage, actual timer reload damage, launch-time slow state and captured owner resources/gear/wounds are checked. Dedicated native wall/corner/terminal-area and callback-fault fixtures remain pending.',
            'Two invalid-caster Slow Missiles fixture diagnostics and an earlier passing cohort are preserved with byte-identical production DLLs. Final metadata and mixed-critical fixture use build4.',
            'Effects/audio and talent/right/quick icons are separate placeholders. No deployment, installed-save write or whole-class completion.'], evidence={str(p.relative_to(ROOT)): sha(p) for p in files})
    path = ROOT / 'tools/reference/tainted-fireball-native-evidence.json'
    text = json.dumps(result, indent=2) + '\n'
    if compare:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Tainted Fire Ball:372 retained/40 focused native checks;two original flights resume;976/21 audits and71Python;learned summon gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
