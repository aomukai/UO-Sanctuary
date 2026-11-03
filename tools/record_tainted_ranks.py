#!/usr/bin/env python3
"""Bind shared innate ranks, action-time refresh and natural Command expiry."""
import argparse
import json
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/tainted-ranks'
FOCUS = ROOT / '.local/class-skills/tainted-ranks-focus'
PARENT = '7266244d5ce0e6dbb41808b3faf9af409bf60538'
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
    prior = json.loads((ROOT / '.local/class-skills/tainted-casting/birth-inputs.json').read_text())
    assert {n for n in set(prior['sourceInputs']) | set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n) != native['sourceInputs'].get(n)} == {
        'src/Sanctuary.Content/InnateNpcSkills.cs', 'src/Sanctuary.Content/DruidSpiritStats.cs',
        'src/Sanctuary.Content/TaintedFireBallCasting.cs', 'tests/Sanctuary.Integration/TaintedRankChecks.cs'}
    assert {n for n in set(prior['copiedData']) | set(native['copiedData']) if prior['copiedData'].get(n) != native['copiedData'].get(n)} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert native['artifacts'][name] == prior['artifacts'][name] == sha(STAGE / 'native/Distribution' / name)
    proof_path = ROOT / 'tools/reference/tainted-ranks-source-evidence.json'; proof = json.loads(proof_path.read_text())
    assert proof['referenceVersion'] == '3.3.93847' and not proof['currentExecutableParity'] and not proof['handlerEnabled']
    assert (proof['reusedCompleteGetterCases'], proof['newCompleteDispatcherCases']) == (2040, 30)
    for path, digest in {**proof['inputs'], **proof['artifacts']}.items():
        assert sha(ROOT / path) == digest, path
    baseline = ROOT / '.local/class-skills/tainted-casting/birth-native-checks.log'
    parent = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/tainted-casting-native-evidence.json'], cwd=ROOT))
    assert sha(baseline) == parent['evidence'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    expected['tainted-ranks-check'] += 1; expected['tainted-ranks-reload-check'] += 2; expected['tainted-ranks-reload-result'] += 2
    assert sum(expected.values()) == 382 and markers(STAGE / 'birth-native-checks.log') == expected
    for folder, count in ((STAGE, 382), (FOCUS, 50)):
        log = (folder / 'birth-native-checks.log').read_text()
        assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == count
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        assert log.count('tainted-ranks-reload-result: passed') == 2
        state = json.loads((folder / 'tainted-ranks-fixtures.json').read_text())
        assert state['Generation'] == 2 and state['Body'] != state['Owner'] and state['Helm'] > 0
        assert state['Mana'] == 22784 and state['Life'] == 116786
        body = json.loads(state['BodyState']); assert body['Owner'] == state['Owner'] and body['Profile'] == 'prepared-tainted-life-owner-v1'
        assert body['Snapshot']['Birth']['Suppliers']['FireBallRank'] == 2 and body['Snapshot']['Birth']['Suppliers']['BloodBoilRank'] == 0
    for kind in ('content', 'integration'):
        assert all(t in (STAGE / f'{kind}-build-1.log').read_text() for t in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for name, count in (('named-item-runtime-audit.json', 979), ('first-loop-affix-audit.json', 21)):
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
                       ('roadmap-check.log', 'Class handler coverage, blockers, effects and icons recorded separately.'), ('source-check.log', '30 complete live-rank dispatcher cases')):
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
    files = [Path(__file__).resolve(), proof_path, ROOT / 'tools/review_tainted_skill_ranks.py', ROOT / 'tools/build_skill_roadmap.py', baseline,
        ROOT / 'tests/check_tainted_ranks.py', ROOT / 'tests/check_tainted_ranks_focus.py',
        *(ROOT / 'docs' / n for n in ('tainted-ranks-checkpoint.md', 'class-skill-roadmap.md', 'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md', 'skill-effects.md', 'skill-icons.md')),
        *(STAGE / n for n in ('pre-build-1.json', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'native-run-1.log', 'audit-native-checks.log', 'audit-run-1.log',
            'content-build-1.log', 'integration-build-1.log', 'source-check.log', 'python-tests.log', 'readiness-check.log', 'catalogue-check.log', 'roadmap-check.log', 'tainted-ranks-fixtures.json')),
        *(FOCUS / n for n in ('birth-inputs.json', 'birth-native-checks.log', 'native-run-1.log', 'tainted-ranks-fixtures.json'))]
    result = dict(referenceVersion='3.3.93847', parent=PARENT, component='Shared innate NPC ranks and registered Tainted action-time refresh',
        handlerEnabled=False, goalComplete=False, completedClasses=[], verified=dict(nativeMarkers=382, retainedNativeMarkers=377, focusedNativeMarkers=50,
            boots=3, reloads=2, saves=3, reusedCompleteGetterCases=2040, newCompleteDispatcherCases=30, originalActorNaturalCommandExpiryCasts=2,
            pythonTests=71, namedDecisions=573, auditFingerprints=[979, 21], handlers=191, protectedInstalledArtifactsUnchanged=106),
        tested=native, limitations=[
            'Registered innate roster and action-time rank refresh only; mode admission, full GenericPet world/controller/owner/skill-list and extended-passive services remain separate. No learned summon or full autonomous AI is claimed.',
            'Existing explicit supplied-rank source fixtures remain distinct. Registered casts use own live Command at entry and release; emitted missiles retain their captured release rank and packet.',
            'Native reloads shorten the original own Command deadline to80ms and observe actual Command/A2/flight timers return to base rank before real damage. Saved Command/recovery deadlines are held; this is not unheld downtime evidence.',
            'No NPC gear/shrine/per-skill/oskill/elemental level suppliers are supported. Older complete getter/dispatcher bodies plus pinned current data are not current executable equivalence. Optional zero grants stay absent; source grant loop is reviewed,not newly executed.',
            'NPC420 aura/Blood Boil execution, incoming combat, learned cap/payment and full lifecycle remain unfinished. Aura selection after a positive summon grant supplies an independent next implementation path.',
            'No diagnostics,deployment,installed-save write,new handler,class completion or final asset generation. Existing effect/audio and tree/right/quick-icon placeholders remain separate.'], evidence={str(p.relative_to(ROOT)): sha(p) for p in files})
    path = ROOT / 'tools/reference/tainted-ranks-native-evidence.json'; text = json.dumps(result, indent=2) + '\n'
    if compare:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Tainted ranks:382 retained/50 focused native checks;2040 shared getter+30 dispatcher cases;two natural Command-expiry reload casts;learned summon gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true'); record(parser.parse_args().check)
