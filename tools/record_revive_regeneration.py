#!/usr/bin/env python3
"""Bind regeneration acceptance to one frozen build and terminal native runs."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from snapshot_skill_build import check as check_snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/revive-regeneration-candidate'
FULL = STAGE / 'full-regression'
SNAPSHOT = STAGE / 'pre-build-attempt-7.json'
PREDECESSOR = 'ebae878'
sha = inventory.sha


def markers(log):
    return Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed', log))


def checked_log(path, count, saves=3):
    log = path.read_text()
    assert ': failed' not in log and ' ERR]' not in log, path
    assert sum(markers(log).values()) == count, path
    assert log.count('Shutting down') == 3, path
    assert log.count('Writing world save snapshot done') == saves, path
    return markers(log)


def historical(name):
    return json.loads(subprocess.check_output(['git', 'show', PREDECESSOR + ':' + name], cwd=ROOT))


def record(check=False):
    scopes = [(STAGE, 'recovery'), (STAGE, 'interaction'), (STAGE, 'stats'), (FULL, 'class')]
    inputs = []
    artifacts = [Path(__file__).resolve(), SNAPSHOT]
    for folder, name in scopes:
        inventory.STAGE = folder
        path = folder / (name + '-inputs.json')
        inputs.append(inventory.validate_inventory(path))
        check_snapshot(SNAPSHOT, path)
        artifacts += [path, folder / (name + '-native-checks.log')]
    tested = inputs[2]
    assert all(x['artifacts'] == tested['artifacts'] and x['copiedData'] == tested['copiedData'] for x in inputs)
    previous = historical('tools/reference/revive-learned-casting-evidence.json')
    assert previous['verified']['retainedMarkers'] == 338
    assert previous['verified']['nativePatchCount'] == 70
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert tested['artifacts'][name] == previous['inputInventory']['artifacts'][name]
        assert sha(STAGE / 'runtime/upstream/ModernUO/Distribution' / name) == tested['artifacts'][name]
    native_manifest = (STAGE / 'runtime/upstream/ModernUO/Distribution').resolve().parents[1] / 'native-pre-build-inputs.json'
    native = json.loads(native_manifest.read_text())
    assert len(native['patches']) == 70
    assert set(native['patches']) == {str(p.relative_to(ROOT)) for p in (ROOT / 'patches/ModernUO').glob('*.patch')}
    for name, value in native['patches'].items():
        assert sha(ROOT / name) == value
    for kind in ['content', 'integration']:
        path = STAGE / f'{kind}-build-attempt-7.log'
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    recovery = checked_log(STAGE / 'recovery-native-checks.log', 12)
    regeneration = Counter({'revive-regeneration-check': 1, 'revive-regeneration-reload-check': 2,
                            'revive-regeneration-live-check': 3, 'revive-regeneration-live-result': 3})
    assert recovery == regeneration + Counter({'revive-cast-check': 1, 'revive-cast-reload-check': 2})
    interaction = checked_log(STAGE / 'interaction-native-checks.log', 18)
    assert interaction == regeneration + Counter({'find-potion-check': 1, 'find-potion-reload-check': 2,
                                                  'find-potion-live-check': 3, 'find-potion-live-result': 3})
    retained = checked_log(STAGE / 'stats-native-checks.log', 347)
    original = STAGE / 'runtime/upstream/ModernUO/Distribution'
    previous_root = original.resolve().parents[4]
    prior_retained = (previous_root / '.local/class-skills/revive-learned-casting/stats-native-checks.log').read_text()
    assert retained == markers(prior_retained) + regeneration
    complete = checked_log(FULL / 'class-native-checks.log', 1184, 35)
    prior_full = (previous_root / '.local/class-skills/revive-learned-casting/full-regression/class-native-checks.log').read_text()
    assert complete == markers(prior_full)
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        other = json.loads((FULL / name).read_text())
        if 'ContentAssemblyPath' in audit:
            assert (ROOT / other['ContentAssemblyPath']).resolve(strict=True) == (ROOT / audit['ContentAssemblyPath']).resolve(strict=True)
            other['ContentAssemblyPath'] = audit['ContentAssemblyPath']
        assert other == audit
        assert audit['ContentAssemblySha256'] == tested['artifacts']['Assemblies/Sanctuary.Content.dll']
        for source, value in audit['InputHashes'].items():
            assert sha(ROOT / source) == value, source
        artifacts += [path, FULL / name, ROOT / 'tools/reference' / name]
        audits.append(audit)
    assert audits[0]['Definitions'] == historical('tools/reference/named-item-runtime-audit.json')['Definitions']
    assert len(audits[0]['Definitions']) == 573
    assert [len(a['InputHashes']) for a in audits] == [783, 21]
    python = STAGE / 'python-tests-attempt-7.log'
    assert 'Ran 71 tests' in python.read_text() and '\nOK\n' in python.read_text() and 'FAILED' not in python.read_text()
    installed_path = ROOT / 'tools/reference/creeper-frame-installation-evidence.json'
    installed = json.loads(installed_path.read_text())
    assert len(installed['installedArtifacts']) == 106
    for name, value in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / name) == value
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 185 and status['completedClasses'] == []
    artifacts += [python, installed_path, ROOT / 'tools/reference/revive-regeneration-source-observation.json',
                  ROOT / 'tools/reference/revive-regeneration-timing-observation.json', STAGE / 'python-upstream-reference-inputs.json']
    artifacts += [STAGE / name for name in ['isolated-revive-regeneration-fixtures.json', 'isolated-revive-cast-fixtures.json',
                                           'revive-regeneration-fixtures.json', 'revive-cast-fixtures.json',
                                           'retained-run-attempt-7.log']]
    artifacts += [ROOT / name for name in ['tests/check_revive_regeneration.py', 'tests/check_revive_regeneration_regression.py',
                                          'tests/check_revive_recovery_find_potion.py', 'tools/build_revive_regeneration.py']]
    result = dict(referenceVersion='3.3.93847', component='Positive source regeneration for learned Revive', installed=False,
        goalComplete=False, completedClasses=[], handlers=185,
        verified=dict(isolatedMarkers=12, interactionMarkers=18, retainedMarkers=347, fullClassMarkers=1184,
                      bootsPerScope=3, actualReloadsPerScope=2, fullClassSaves=35, pythonTests=71,
                      nativePatchCount=70, nativeBinariesReused=True, sourceRateRows=751,
                      inputFingerprints=[783, 21], unchangedNamedDefinitions=573, unchangedInstalledArtifacts=106),
        frozenPredecessor=dict(commit=PREDECESSOR, receipt='tools/reference/revive-learned-casting-evidence.json'),
        inputInventory=tested, isolatedInputInventory=inputs[0], interactionInputInventory=inputs[1], fullClassInputInventory=inputs[3],
        reusedNativeManifest=dict(path=str(native_manifest), sha256=sha(native_manifest)),
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))},
        established=['New learned Revives opt into pinned source rates and FP256 recovery per40ms; omitted optional fields preserve legacy recovery behavior.',
                     'Frame carry, current-maximum truncation, exact cap, prevention/inactive/full resets, native/Graveyard duplicate recovery exclusion and actual saves/reloads are tested.',
                     'Connected timer recovery and parked suppression pass alongside a native timer control; Find Potion interaction and retained/full predecessor markers are preserved.'],
        remaining=['Positive-healing acceptance does not establish complete negative-regeneration/death-event or modern executable equivalence.',
                   'Facet scaling, actual Revive respec acceptance, independent item/proc/sunder consumers and Summon Resist reference conflict remain open.',
                   'A controlled500ms pause reproduces the short-window failure with active/eligible actors and a clock age below40ms; this supports timer-wheel catch-up but does not prove the earlier uninstrumented failures had that cause.',
                   'No class completion or deployment. Final effects/audio and talent/hotkey icons remain separate placeholders.'])
    output = ROOT / 'tools/reference/revive-regeneration-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Revive regeneration:12isolated/18interaction/347retained/1184full/71Python;scoped acceptance only.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
