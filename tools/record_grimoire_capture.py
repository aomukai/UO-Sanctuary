#!/usr/bin/env python3
"""Bind Grimoire catalogue/capture acceptance to frozen native inputs."""
import argparse
import json
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/grimoire-capture'
FOCUS = ROOT / '.local/class-skills/grimoire-capture-focus'
PARENT = '524328631d6b8eae7e2e040660bf0dfc34ffa11e'
sha = inventory.sha


def record(check=False):
    inventory.STAGE = STAGE
    native = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    focused = inventory.validate_inventory(FOCUS / 'birth-inputs.json')
    audit = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    for folder, name in ((STAGE, 'birth-inputs.json'), (FOCUS, 'birth-inputs.json'), (STAGE, 'audit-inputs.json')):
        snapshot(STAGE / 'pre-build-1.json', folder / name)
    assert native['artifacts'] == focused['artifacts'] == audit['artifacts']
    assert native['copiedData'] == focused['copiedData'] == audit['copiedData']
    prior = json.loads((ROOT / '.local/class-skills/defiler-body/birth-inputs.json').read_text())
    changed = {name for name in set(prior['sourceInputs']) | set(native['sourceInputs'])
               if name.endswith('.cs') and prior['sourceInputs'].get(name) != native['sourceInputs'].get(name)}
    assert changed == {'src/Sanctuary.Content/BalanceCatalog.cs', 'src/Sanctuary.Content/GrimoirePrepared.cs',
                       'src/Sanctuary.Content/WarlockGrimoire.cs', 'tests/Sanctuary.Integration/GrimoireCaptureChecks.cs'}
    assert {name for name in set(prior['copiedData']) | set(native['copiedData'])
            if prior['copiedData'].get(name) != native['copiedData'].get(name)} == {
                'Data/Sanctuary/Balance/warlock-grimoire-bases.json', 'Data/Sanctuary/Balance/warlock-grimoire-automatic.json'}
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert native['artifacts'][name] == prior['artifacts'][name] == sha(STAGE / 'native/Distribution' / name)
    parent = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/defiler-body-native-evidence.json'], cwd=ROOT))
    baseline = ROOT / '.local/class-skills/defiler-body/birth-native-checks.log'
    assert sha(baseline) == parent['evidence'][str(baseline.relative_to(ROOT))]
    for folder, old, count in ((STAGE, 'defiler-body', 396), (FOCUS, 'defiler-body-focus', 64)):
        expected = markers(ROOT / '.local/class-skills' / old / 'birth-native-checks.log')
        expected['grimoire-capture-check'] += 1
        expected['grimoire-capture-reload-check'] += 2
        assert markers(folder / 'birth-native-checks.log') == expected and sum(expected.values()) == count
        log = (folder / 'birth-native-checks.log').read_text()
        assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        assert log.count('Grimoire:15owned bases/eight automatic rows/720capture contexts/12malformed controls;no random generation or equip certificate') == 1
        saved = json.loads((folder / 'grimoire-capture-fixtures.json').read_text())
        assert len(saved) == 2 and {r['Female'] for r in saved} == {False, True}
        for row in saved:
            assert len({row[n] for n in ('Owner', 'Item', 'Helm')}) == 3
            assert row['Mana'] == 22784 and row['Life'] == 37632 and row['Durability'] == 17
            capture = json.loads(row['Capture'])
            assert capture['Profile'] == 'captured-normal-grimoire-v1' and capture['ItemLevel'] == 99
            assert capture['Code'] == ('waf' if row['Female'] else 'wa1')
            assert capture['Defense'] == (148 if row['Female'] else 5)
            assert capture['Automatic'] == ('of Inferno' if row['Female'] else 'of Shadows')
            assert capture['Grants'] == [dict(Skill=383, Rank=3), dict(Skill=374, Rank=2)]
    for kind in ('content', 'integration'):
        assert all(s in (STAGE / (kind + '-build-1.log')).read_text() for s in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    proofpath = ROOT / 'tools/reference/grimoire-catalogue-evidence.json'
    proof = json.loads(proofpath.read_text())
    assert proof['referenceVersion'] == '3.3.93847' and (proof['bases'], proof['normalFamilies'], proof['automaticRows']) == (15, 5, 8)
    for name, digest in {**proof['inputs'], **proof['outputs']}.items():
        assert sha(ROOT / name) == digest, name
    for name, count in (('named-item-runtime-audit.json', 987), ('first-loop-affix-audit.json', 21)):
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
    for name, text in (('source-check.log', '15bases/five families/eight automatic rows'),
                       ('readiness-check.log', '573 definitions accounted for'), ('catalogue-check.log', 'Eight classes, 240 source skills'),
                       ('roadmap-check.log', 'Class handler coverage, blockers, effects and icons recorded separately.')):
        assert text in (STAGE / name).read_text()
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for name, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / name) == digest, name
    protected = json.loads((ROOT / '.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key, relative, count in (('saves', '.local/server/Saves', 38), ('configuration', '.local/server/Configuration', 20)):
        folder = Path(installed['runtimeRoot']) / relative
        assert len(protected[key]) == count
        assert {str(p.relative_to(folder)): sha(p) for p in folder.rglob('*') if p.is_file()} == protected[key]
    state = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not state['completedClasses'] and sum(row['implementation'] == 'handler-present' for row in state['skills']) == 191
    files = [Path(__file__).resolve(), proofpath, ROOT / 'tools/build_grimoire_catalogue.py',
             ROOT / 'tests/check_grimoire_capture.py', ROOT / 'tests/check_grimoire_capture_focus.py', baseline,
             *(ROOT / 'docs' / name for name in ('grimoire-capture-checkpoint.md', 'warlock-hands-reference-checkpoint.md', 'class-skill-roadmap.md', 'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md')),
             *(STAGE / name for name in ('pre-build-1.json', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'native-run-1.log',
                                        'content-build-1.log', 'integration-build-1.log', 'audit-native-checks.log', 'audit-run-1.log', 'python-tests.log',
                                        'source-check.log', 'readiness-check.log', 'catalogue-check.log', 'roadmap-check.log', 'grimoire-capture-fixtures.json')),
             *(FOCUS / name for name in ('birth-inputs.json', 'birth-native-checks.log', 'native-run-1.log', 'grimoire-capture-fixtures.json'))]
    result = dict(referenceVersion='3.3.93847', parent=PARENT, component='Grimoire catalogue and original saved captures',
                  handlerEnabled=False, goalComplete=False, completedClasses=[],
                  verified=dict(nativeMarkers=396, retainedNativeMarkers=393, focusedNativeMarkers=64, retainedFocusedMarkers=61,
                                boots=3, reloads=2, saves=3, sourceBases=15, automaticRows=8, captureContexts=720, malformedControls=12,
                                originalItems=2, pythonTests=71, namedDecisions=573, auditFingerprints=[987, 21], handlers=191,
                                protectedInstalledArtifactsUnchanged=106, protectedSavesUnchanged=38, protectedConfigurationsUnchanged=20),
                  tested=native, limitations=[
                      'Prepared normal-quality captures only. CanEquip remains false and the item stays outside converted/random acquisition;no current-game generation or hand-rule execution is claimed.',
                      'Existing property representations preserve source magic/fire endpoints and supplied distinct Warlock skill ranks without selecting or rerolling them. Gear,skill,raw/frozen and damage consumers remain to bind.',
                      'Original item/helm identities,captures,durability and owner resources persist through both reloads. This is not an old-content DLL migration or a live/client equip certificate.',
                      'No installed build/save write,new handler or completed class. Stock book art/name is an identified gear placeholder;skill effects/audio and talent/hotkey icons remain separate.',
                  ], evidence={str(p.relative_to(ROOT)): sha(p) for p in files})
    target = ROOT / 'tools/reference/grimoire-capture-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Grimoire captures:396 retained/64 focused checks;two original saved books;equip/generation remain gated;installed build/saves preserved.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
