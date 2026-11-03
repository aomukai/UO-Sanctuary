#!/usr/bin/env python3
"""Bind qualified GenericPet/A2 release and original-NPC reload acceptance."""
import argparse
import json
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/tainted-casting'
FOCUS = ROOT / '.local/class-skills/tainted-casting-focus'
PARENT = '2d2cb4a431641cbe98d5b6a712d12467cd419572'
sha = inventory.sha


def record(compare=False):
    inventory.STAGE = STAGE
    native = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    audit = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    focused = inventory.validate_inventory(FOCUS / 'birth-inputs.json')
    for folder, name in ((STAGE, 'birth-inputs.json'), (STAGE, 'audit-inputs.json'), (FOCUS, 'birth-inputs.json')):
        snapshot(STAGE / 'pre-build-1.json', folder / name)
    assert native['artifacts'] == audit['artifacts'] == focused['artifacts']
    assert native['copiedData'] == audit['copiedData'] == focused['copiedData']
    prior = json.loads((ROOT / '.local/class-skills/tainted-fireball/birth-inputs.json').read_text())
    assert {n for n in set(prior['sourceInputs']) | set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n) != native['sourceInputs'].get(n)} == {
        'src/Sanctuary.Content/TaintedFireBallCasting.cs', 'src/Sanctuary.Content/WarlockTaintedBody.cs',
        'tests/Sanctuary.Integration/TaintedCastingChecks.cs', 'tests/Sanctuary.Integration/TaintedFireBallChecks.cs'}
    assert {n for n in set(prior['copiedData']) | set(native['copiedData']) if prior['copiedData'].get(n) != native['copiedData'].get(n)} == {
        'Data/Sanctuary/Balance/tainted-fireball-cast.json', 'Data/Sanctuary/Balance/class-skill-status.json'}
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert native['artifacts'][name] == prior['artifacts'][name] == sha(STAGE / 'native/Distribution' / name)
    proof_path = ROOT / 'tools/reference/tainted-fireball-cast-source-evidence.json'; proof = json.loads(proof_path.read_text())
    assert proof['referenceVersion'] == '3.3.93847' and not proof['currentExecutableParity'] and not proof['handlerEnabled']
    assert proof['animation'] == dict(code='BHA2HTH', frames=10, speed=256, events=[[6, 2]]) and proof['fixture']['cases'] == 401
    for path, digest in {**proof['inputs'], **proof['artifacts']}.items():
        assert sha(ROOT / path) == digest, path
    assert sha(ROOT / proof['fixture']['path']) == proof['fixture']['sha256']
    baseline = ROOT / '.local/class-skills/tainted-fireball/birth-native-checks.log'
    parent = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/tainted-fireball-native-evidence.json'], cwd=ROOT))
    assert sha(baseline) == parent['evidence'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    expected['tainted-casting-check'] += 1; expected['tainted-casting-reload-check'] += 2; expected['tainted-casting-reload-result'] += 2
    assert sum(expected.values()) == 377 and markers(STAGE / 'birth-native-checks.log') == expected
    for folder, count in ((STAGE, 377), (FOCUS, 45)):
        log = (folder / 'birth-native-checks.log').read_text()
        assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == count
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        assert log.count('tainted-casting-reload-result: passed') == 2
        state = json.loads((folder / 'tainted-casting-fixtures.json').read_text())
        assert state['Generation'] == 2 and state['PendingTarget'] == 0 and state['Body'] != state['Owner'] and state['Helm'] > 0
        assert state['Mana'] == 22784 and state['Life'] == 116786
        body = json.loads(state['BodyState']); assert body['Owner'] == state['Owner'] and body['Profile'] == 'prepared-tainted-life-owner-v1'
        assert body['Snapshot']['Birth']['Suppliers']['FireBallRank'] == 2
    for kind in ('content', 'integration'):
        assert all(t in (STAGE / f'{kind}-build-1.log').read_text() for t in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for name, count in (('named-item-runtime-audit.json', 978), ('first-loop-affix-audit.json', 21)):
        current = json.loads((STAGE / name).read_text())
        assert current == json.loads((ROOT / 'tools/reference' / name).read_text()) and len(current['InputHashes']) == count
        assert current['ContentAssemblySha256'] == native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path, digest in current['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        old = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/' + name], cwd=ROOT)); skip = {'InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath'}
        assert {k: v for k, v in current.items() if k not in skip} == {k: v for k, v in old.items() if k not in skip}
    assert (STAGE / 'audit-native-checks.log').read_text().count(': passed') == 2
    assert 'Ran 71 tests' in (STAGE / 'python-tests.log').read_text() and '\nOK\n' in (STAGE / 'python-tests.log').read_text()
    for name, text in (('readiness-check.log', '573 definitions accounted for'), ('catalogue-check.log', 'Eight classes, 240 source skills'),
                       ('roadmap-check.log', 'Class handler coverage, blockers, effects and icons recorded separately.'), ('source-check.log', '401 complete source rate/scheduler cases')):
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
    files = [Path(__file__).resolve(), proof_path, ROOT / 'tools/probe_tainted_fireball_cast_clock.py', ROOT / 'tools/build_skill_roadmap.py', baseline,
        ROOT / 'tests/check_tainted_casting.py', ROOT / 'tests/check_tainted_casting_focus.py',
        *(ROOT / 'docs' / n for n in ('tainted-casting-checkpoint.md', 'class-skill-roadmap.md', 'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md', 'skill-effects.md', 'skill-icons.md')),
        *(STAGE / n for n in ('pre-build-1.json', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'native-run-1.log', 'audit-native-checks.log', 'audit-run-1.log',
            'content-build-1.log', 'integration-build-1.log', 'source-check.log', 'python-tests.log', 'readiness-check.log', 'catalogue-check.log', 'roadmap-check.log', 'tainted-casting-fixtures.json')),
        *(FOCUS / n for n in ('birth-inputs.json', 'birth-native-checks.log', 'native-run-1.log', 'tainted-casting-fixtures.json'))]
    result = dict(referenceVersion='3.3.93847', parent=PARENT, component='Qualified GenericPet A2 cast service and once-only timed NPC422 release',
        handlerEnabled=False, goalComplete=False, completedClasses=[], verified=dict(nativeMarkers=377, retainedNativeMarkers=372, focusedNativeMarkers=45,
            boots=3, reloads=2, saves=3, sourceRateSchedulerCases=401, originalActorAutomaticReloadCasts=2, pythonTests=71, namedDecisions=573, auditFingerprints=[978, 21], handlers=191, protectedInstalledArtifactsUnchanged=106),
        tested=native, limitations=[
            'Prepared targeted A2 action service bound to the existing GenericPet wrapper only; mode admission and effective NPC rank are supplied explicitly. No complete autonomous owner/controller/search/selection/movement/skill-lookup or learned summon is claimed.',
            'Native checks execute the actual once-only A2 and projectile timers after two reloads using the original NPC. Newly issued reload casts differ from retained original-flight resumes in the parent cohort.',
            'Unissued host actions are transient and do not replay; pending fixtures use held future clocks. Current D2R offline/reload/interruption behavior is not certified. Active-rate/rank renewal and lost-target path fallback remain explicit gaps.',
            'NPC420 aura/Blood Boil, incoming combat, learned cap/payment and complete owner/death/facet/parking lifecycle remain unfinished. Current native mode initialization/owner services, world/frame/seed/client and source-removal acceptance are separate.',
            'No diagnostics, deployment, installed-save writes, new handler, whole-class completion or final asset generation. Existing cast/travel/explosion presentation and talent/right/quick icons remain separate placeholders.'], evidence={str(p.relative_to(ROOT)): sha(p) for p in files})
    path = ROOT / 'tools/reference/tainted-casting-native-evidence.json'; text = json.dumps(result, indent=2) + '\n'
    if compare:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Tainted A2:377 retained/45 focused native checks;401 source cases;two original-NPC automatic reload casts;learned summon gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true'); record(parser.parse_args().check)
