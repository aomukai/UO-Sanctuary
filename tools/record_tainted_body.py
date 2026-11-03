#!/usr/bin/env python3
"""Bind Tainted body/qualified hit, reused Hydra/Goatman and retained saves."""
import argparse
import json
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/tainted-body'
FOCUS = ROOT / '.local/class-skills/tainted-body-focus'
PARENT = '5d0d2c2b412b4b2428eaab6c9d5c14f3f5dd7972'
sha = inventory.sha


def record(compare=False):
    inventory.STAGE = STAGE
    native = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    audit = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    focused = inventory.validate_inventory(FOCUS / 'birth-inputs.json')
    for path in (STAGE / 'birth-inputs.json', STAGE / 'audit-inputs.json', FOCUS / 'birth-inputs.json'):
        snapshot(STAGE / 'pre-build-7.json', path)
    assert native['artifacts'] == audit['artifacts'] == focused['artifacts']
    assert native['copiedData'] == audit['copiedData'] == focused['copiedData']
    prior = json.loads((ROOT / '.local/class-skills/tainted-foundation/birth-inputs.json').read_text())
    assert {p for p in set(prior['sourceInputs']) | set(native['sourceInputs'])
            if p.endswith('.cs') and prior['sourceInputs'].get(p) != native['sourceInputs'].get(p)} == {
        'src/Sanctuary.Content/CreatureProfiles.cs', 'src/Sanctuary.Content/PlayerHydras.cs', 'src/Sanctuary.Content/WarlockGoatmanBody.cs',
        'src/Sanctuary.Content/WarlockTaintedBody.cs', 'src/Sanctuary.Content/WarlockDemonRecovery.cs',
        'src/Sanctuary.Content/NpcFireSpellHit.cs', 'src/Sanctuary.Content/TaintedFireBallHit.cs',
        'tests/Sanctuary.Integration/TaintedBodyChecks.cs', 'tests/Sanctuary.Integration/PetReconnectChecks.cs'}
    assert {p for p in set(prior['copiedData']) | set(native['copiedData']) if prior['copiedData'].get(p) != native['copiedData'].get(p)} == {
        'Data/Sanctuary/Balance/tainted-fireball-hit.json', 'Data/Sanctuary/Balance/class-skill-status.json'}
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert native['artifacts'][name] == prior['artifacts'][name] == sha(STAGE / 'native/Distribution' / name)
    review_path = ROOT / 'tools/reference/tainted-body-source-review.json'; review = json.loads(review_path.read_text())
    assert review['referenceVersion'] == '3.3.93847' and not review['handlerEnabled'] and not review['newSourceRoutinesExecuted']
    for name, digest in review['inputs'].items():
        assert sha(ROOT / name) == digest, name
    baseline = ROOT / '.local/class-skills/tainted-foundation/birth-native-checks.log'
    parent = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/tainted-foundation-native-evidence.json'], cwd=ROOT))
    assert sha(baseline) == parent['evidence'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    for family in ('tainted-body', 'hydra-world'):
        expected[family + '-check'] += 1; expected[family + '-reload-check'] += 2
    expected['hydra-world-reload-result'] += 2
    for name in ('hydra-world-right', 'hydra-world-quick'):
        expected[name + '-check'] += 3; expected[name + '-result'] += 3
    assert sum(expected.values()) == 367 and markers(STAGE / 'birth-native-checks.log') == expected
    for folder, count in ((STAGE, 367), (FOCUS, 35)):
        log = (folder / 'birth-native-checks.log').read_text()
        assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == count
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        assert log.count('tainted-body-check: passed') == 1 and log.count('tainted-body-reload-check: passed') == 2
        assert log.count('hydra-world-reload-result: passed') == 2
        saved = json.loads((folder / 'tainted-body-fixtures.json').read_text())
        assert len(saved) == 2 and {s['Female'] for s in saved} == {False, True}
        for row in saved:
            state = json.loads(row['State']); birth = state['Snapshot']['Birth']
            assert state['Profile'] == 'prepared-tainted-life-owner-v1' and state['Owner'] == row['Owner']
            assert row['Life'] == row['Maximum'] - 257 and row['Maximum'] == birth['MaximumLife256'] and row['Mana'] > 0
            assert birth['Suppliers']['PhysicalSunder'] == birth['Suppliers']['FireSunder'] == 0
        hydra = json.loads((folder / 'hydra-world-fixtures.json').read_text())
        assert hydra['Generation'] == 2 and len(hydra['Heads']) == 3 and hydra['Bolts']
        assert all(h['Source']['Actor'] > 0 for h in hydra['Heads'])
    for kind in ('content', 'integration'):
        assert all(t in (STAGE / f'{kind}-build-7.log').read_text() for t in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for name, count in (('named-item-runtime-audit.json', 974), ('first-loop-affix-audit.json', 21)):
        current = json.loads((STAGE / name).read_text())
        assert current == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert len(current['InputHashes']) == count and current['ContentAssemblySha256'] == native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path, digest in current['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        old = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/' + name], cwd=ROOT))
        skip = {'InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath'}
        assert {k: v for k, v in current.items() if k not in skip} == {k: v for k, v in old.items() if k not in skip}
    audit_log = (STAGE / 'audit-native-checks.log').read_text()
    assert audit_log.count(': passed') == 2 and ' ERR]' not in audit_log and ': failed' not in audit_log
    assert 'Ran 71 tests' in (STAGE / 'python-tests.log').read_text() and '\nOK\n' in (STAGE / 'python-tests.log').read_text()
    for name, text in (('readiness-check.log', '573 definitions accounted for'), ('catalogue-check.log', 'Eight classes, 240 source skills'),
                       ('roadmap-check.log', 'Class handler coverage, blockers, effects and icons recorded separately.'), ('source-check.log', 'owned hit01/5/2/NPC422')):
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
    old = STAGE / 'diagnostic-native-2'
    log = (old / 'focus/birth-native-checks.log').read_text()
    assert 'raw=4838,loss=4838,expected=4838' in log and '"HitClass":45' in log
    critical = STAGE / 'diagnostic-native-3'
    assert 'System.NullReferenceException' in (critical / 'focus/birth-native-checks.log').read_text()
    assert 'default, intrinsicCritical' in (critical / 'TaintedFireBallHit.cs').read_text()
    assert sha(critical / 'content/Sanctuary.Content.dll') == sha(old / 'content/Sanctuary.Content.dll')
    assert sha(critical / 'content/Sanctuary.Content.dll') != native['artifacts']['Assemblies/Sanctuary.Content.dll']
    reconnect = STAGE / 'diagnostic-full-2'
    assert 'frames=1,priorFrame=26,frame=27' in (reconnect / 'birth-native-checks.log').read_text()
    assert 'prior=(909803520,445677568,3328),current=(909541376,445415424,3328)' in (reconnect / 'birth-native-checks.log').read_text()
    assert sha(reconnect / 'content/Sanctuary.Content.dll') == native['artifacts']['Assemblies/Sanctuary.Content.dll']
    files = [Path(__file__).resolve(), review_path, ROOT / 'tools/review_tainted_body.py', ROOT / 'tools/build_class_skills.py', ROOT / 'tools/build_skill_roadmap.py',
             ROOT / 'tests/check_tainted_body.py', ROOT / 'tests/check_tainted_body_focus.py', baseline,
             *(ROOT / 'docs' / n for n in ('tainted-body-checkpoint.md', 'class-skill-roadmap.md', 'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md', 'skill-effects.md', 'skill-icons.md')),
             *(STAGE / n for n in ('pre-build-7.json', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'native-run-3.log', 'audit-native-checks.log',
                'audit-run-4.log', 'content-build-7.log', 'integration-build-7.log', 'source-check.log', 'python-tests.log', 'readiness-check.log', 'catalogue-check.log', 'roadmap-check.log',
                'tainted-body-fixtures.json', 'goatman-body-fixtures.json', 'hydra-world-fixtures.json')),
             *(FOCUS / n for n in ('birth-inputs.json', 'birth-native-checks.log', 'native-run-5.log', 'tainted-body-fixtures.json', 'hydra-world-fixtures.json')),
             *(p for pattern in ('diagnostic-native-*','diagnostic-full-*') for folder in STAGE.glob(pattern) for p in folder.rglob('*') if p.is_file() and not p.is_symlink())]
    result = dict(referenceVersion='3.3.93847', parent=PARENT, component='Saved Tainted body and qualified NPC fire hit with shared Hydra/Goatman services',
        handlerEnabled=False, goalComplete=False, completedClasses=[],
        verified=dict(nativeMarkers=367, retainedNativeMarkers=347, focusedNativeMarkers=35, boots=3, reloads=2, saves=3,
            originalTaintedBodies=2, newHydraWireCasts=6, originalHydraNaturalResumes=2, pythonTests=71, namedDecisions=573,
            auditFingerprints=[974, 21], handlers=191, protectedInstalledArtifactsUnchanged=106), tested=native,
        limitations=[
            'Prepared life/ownership and explicitly successful per-recipient fire-copy delivery only. No learned Tainted cast, automatic GenericPet controller, target/area/block/dodge/critical RNG, actual projectile factory/release/travel, aura or Blood Boil execution.',
            'Nonzero inherited sunder fields refuse atomically. New body recovery is explicitly clocked; automatic owner/death/offline/AI scheduling, complete incoming combat/return/shield/poison and modern source-stat/event/client acceptance remain separate.',
            'Native checks preserve original bodies,gear,wounds,relative facet life and held future recovery deadlines; they do not prove unheld downtime recovery. Existing Hydra tests exercise actual input, original actors/flights and natural reload damage.',
            'Both initial hit-class fixture diagnostics and the later missing-zero-poison critical defect are preserved. The first two use byte-identical production; the critical caller is corrected without changing the shared critical helper.',
            'Reconnect inspection now starts after saved-source/native-placement coordinates agree;the existing tight walking bound is unchanged. Two full diagnostics preserve the initial2tile pose correction;production movement code and the accepted Tainted production DLL remain unchanged.',
            'No final assets, full class certificate, deployment or installed-save write. Approved Succubus appearance uses native artwork; effect/audio and player talent/right/quick icons remain separate.'],
        evidence={str(p.relative_to(ROOT)): sha(p) for p in files})
    path = ROOT / 'tools/reference/tainted-body-native-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if compare:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Tainted body:367 retained/35 focused native checks;original Warlock/Witch body/wounds and Hydra input/flight reloads pass;learned summon gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
