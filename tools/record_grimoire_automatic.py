#!/usr/bin/env python3
"""Bind shared automatic-affix selection and retained original-item acceptance."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/grimoire-automatic'
FOCUS = ROOT / '.local/class-skills/grimoire-automatic-focus'
PARENT = '8e6af8480a7adc06217f30e60b637f9722135551'
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
    previous = ROOT / '.local/class-skills/grimoire-equip'
    prior = json.loads((previous / 'birth-inputs.json').read_text())
    changed = {n for n in set(prior['sourceInputs']) | set(native['sourceInputs'])
               if n.endswith('.cs') and prior['sourceInputs'].get(n) != native['sourceInputs'].get(n)}
    assert changed == {'src/Sanctuary.Content/SourceAffixPicker.cs', 'src/Sanctuary.Content/GrimoireAutomaticSelection.cs',
                       'src/Sanctuary.Content/ValkyrieAffixes.cs', 'tests/Sanctuary.Integration/GrimoireAutomaticChecks.cs',
                       'tests/Sanctuary.Integration/GrimoireEquipChecks.cs'}
    assert native['copiedData'] == prior['copiedData']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert native['artifacts'][name] == prior['artifacts'][name] == sha(STAGE / 'native/Distribution' / name)
    parentraw = subprocess.check_output(['git', 'show', PARENT + ':tools/reference/grimoire-equip-native-evidence.json'], cwd=ROOT)
    parent = json.loads(parentraw)
    manifests = []
    for folder, old, count in ((STAGE, 'grimoire-equip', 411), (FOCUS, 'grimoire-equip-focus', 79)):
        baseline = ROOT / '.local/class-skills' / old / 'birth-native-checks.log'
        assert sha(baseline) == parent['evidence'][str(baseline.relative_to(ROOT))]
        expected = markers(baseline)
        for family in ('grimoire-automatic', 'valkyrie-affix'):
            expected[family + '-check'] += 1
            expected[family + '-reload-check'] += 2
        assert markers(folder / 'birth-native-checks.log') == expected and sum(expected.values()) == count
        log = (folder / 'birth-native-checks.log').read_text()
        assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        assert log.count('11880 complete source pool/weight/selection/final-seed comparisons') == 1
        saved = json.loads((folder / 'grimoire-automatic-fixtures.json').read_text())
        assert len(saved) == 2 and {r['Female'] for r in saved} == {True, False}
        for row in saved:
            assert row['Owner'] != row['Item'] and (row['Life'], row['Mana']) == (64256, 42752)
            capture = json.loads(row['Capture']); gear = json.loads(row['Gear'])
            assert capture == dict(Code='wa1', ItemLevel=99, Defense=5, Automatic='of Inferno' if row['Female'] else 'of Nightmares',
                                   Grants=[], Profile='captured-normal-grimoire-v1')
            assert gear['BaseCode'] == 'wa1' and gear['Defense'] == 5 and gear['Roll']['ItemLevel'] == 99
            assert (row['Seed'], row['Next'], row['NextSeed']) == ((1459869932608754093, 'of Nightmares', 3648638951188725782)
                     if row['Female'] else (1207803688153527087, 'of Ember', 5200726946371023464))
        manifests.append(saved)
    # Separate disposable worlds allocate different serials. Compare captured
    # inputs/results;each world's identity is checked across its own reloads.
    assert [{k: v for k, v in row.items() if k not in ('Owner', 'Item')} for row in manifests[0]] == [
        {k: v for k, v in row.items() if k not in ('Owner', 'Item')} for row in manifests[1]]
    for kind in ('content', 'integration'):
        assert all(s in (STAGE / (kind + '-build-1.log')).read_text() for s in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    sourcepath = ROOT / 'tools/reference/grimoire-automatic-source-evidence.json'
    source = json.loads(sourcepath.read_text())
    assert source['cases'] == 11880 and len(source['compiledUnchangedRoutines']) == 9 and not source['currentExecutableParity']
    for group in ('inputs', 'artifacts'):
        for name, digest in source[group].items():
            assert sha(ROOT / name) == digest, name
    sourcestage = ROOT / '.local/class-skills/grimoire-automatic-source'
    assert not (sourcestage / 'sanitizer.log').read_bytes()
    assert '11880 complete source' in (sourcestage / 'source-check.log').read_text()
    valkyriepath = ROOT / 'tools/reference/valkyrie-affix-engine-evidence.json'
    valkyrie = json.loads(valkyriepath.read_text())
    assert valkyrie['compiledUnchangedRoutines'] == source['compiledUnchangedRoutines']
    assert len(json.loads((ROOT / '.local/class-skills/valkyrie-affix-source-probe.json').read_text())) == 5184
    assert len(json.loads((ROOT / '.local/class-skills/valkyrie-equipment-source-probe.json').read_text())['equipment']) == 80
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
    for name, expected in (('readiness-check.log', '573 definitions accounted for'), ('catalogue-check.log', 'Eight classes, 240 source skills'),
                           ('roadmap-check.log', 'Class handler coverage, blockers, effects and icons recorded separately.')):
        assert expected in (STAGE / name).read_text()
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
    files = [Path(__file__).resolve(), sourcepath, valkyriepath,
             *(ROOT / 'tests' / name for name in ('check_grimoire_automatic.py', 'check_grimoire_automatic_focus.py')),
             *(ROOT / 'docs' / name for name in ('grimoire-automatic-checkpoint.md', 'grimoire-equip-checkpoint.md', 'class-skill-roadmap.md',
                                                'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md')),
             *(STAGE / name for name in ('pre-build-1.json', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'native-run-1.log',
                                        'content-build-1.log', 'integration-build-1.log', 'audit-native-checks.log', 'audit-run-1.log', 'python-tests.log',
                                        'readiness-check.log', 'catalogue-check.log', 'roadmap-check.log', 'grimoire-automatic-fixtures.json')),
             *(FOCUS / name for name in ('birth-inputs.json', 'birth-native-checks.log', 'native-run-1.log', 'grimoire-automatic-fixtures.json')),
             sourcestage / 'source-check.log', ROOT / '.local/class-skills/grimoire-existing-affix-source-check.log',
             STAGE / 'recorder-diagnostic-1/record.py', STAGE / 'recorder-diagnostic-1/run.log']
    result = dict(referenceVersion='3.3.93847', parent=PARENT, component='Shared normal Grimoire automatic bonus selection',
                  handlerEnabled=False, goalComplete=False, completedClasses=[],
                  parentReceiptSha256=hashlib.sha256(parentraw).hexdigest(),
                  verified=dict(nativeMarkers=411, retainedNativeMarkers=405, focusedNativeMarkers=79, retainedFocusedMarkers=73,
                                boots=3, reloads=2, saves=3, sourceAutomaticCases=11880, unchangedSourceRoutines=9,
                                retainedValkyrieSelectionCasesPerBoot=5184, retainedValkyrieEquipmentCasesPerBoot=80,
                                originalSelectedBooks=2, pythonTests=71, namedDecisions=573, auditFingerprints=[992, 21], handlers=191,
                                protectedInstalledArtifactsUnchanged=106, protectedSavesUnchanged=38, protectedConfigurationsUnchanged=20),
                  tested=native, limitations=[
                      'Normal automatic group306 only,with caller-supplied seed at this phase;no seed allocation,property assignment,defense,staffmod or complete factory/acquisition rollout.',
                      'Complete unchanged older selector and declared current table/type services are supplementary execution,not current executable parity.',
                      'Warlock IDs373..402are tree-ordered and contiguous five-ID groups mix required levels. Applicability of old linear-ID staffmods is uncertain;no sorted map or grant-free public factory is invented.',
                      'Native test books use explicitly supplied empty captured grants. Saved caller seeds belong to fixture manifests,not a new production item RNG persistence format.',
                      'Actual original captures,gear,durability and fractional resources persist;no production save layout change. Earlier actual old-build upgrade evidence remains separate.',
                      'No new handler,class completion or deployment;effects/audio and talent/hotkey icons remain separate;stock book art remains an identified gear placeholder.',
                  ], evidence={str(p.relative_to(ROOT)): sha(p) for p in files})
    target = ROOT / 'tools/reference/grimoire-automatic-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Grimoire automatic:411retained/79focused checks;11880source cases;existing5184Valkyrie/80equipment controls;staffmods deferred;protected build/saves preserved.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
