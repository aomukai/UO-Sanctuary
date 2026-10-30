#!/usr/bin/env python3
"""Bind facet acceptance and mixed-binary save upgrade to terminal native runs."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from snapshot_skill_build import check as check_snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/revive-facets'
FULL = STAGE / 'full-regression'
SNAPSHOT = STAGE / 'pre-build-attempt-3.json'
PREDECESSOR = '4aee67e'
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
    scopes = [(STAGE, 'facet'), (STAGE, 'stats'), (STAGE, 'upgrade'), (FULL, 'class')]
    inputs = []
    artifacts = [Path(__file__).resolve(), SNAPSHOT]
    for folder, name in scopes:
        inventory.STAGE = folder
        path = folder / (name + '-inputs.json')
        inputs.append(inventory.validate_inventory(path))
        check_snapshot(SNAPSHOT, path)
        artifacts += [path, folder / (name + '-native-checks.log')]
    tested = inputs[1]
    assert all(x['artifacts'] == tested['artifacts'] and x['copiedData'] == tested['copiedData'] for x in inputs)
    previous = historical('tools/reference/revive-regeneration-evidence.json')
    assert previous['verified']['retainedMarkers'] == 347
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
        path = STAGE / f'{kind}-build-attempt-3.log'
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    focused = checked_log(STAGE / 'facet-native-checks.log', 9)
    facets = Counter({'revive-facet-check': 1, 'revive-facet-reload-check': 2,
                      'revive-facet-live-check': 3, 'revive-facet-live-result': 3})
    assert focused == facets
    respec = Counter({'revive-respec-check': 1, 'revive-respec-result': 1,
                      'revive-respec-reload-check': 2, 'revive-respec-live-check': 3, 'revive-respec-live-result': 3})
    retained = checked_log(STAGE / 'stats-native-checks.log', 366)
    old_stage = STAGE / 'previous-recovery'
    for name in ['stats-native-checks.log', 'full-regression/class-native-checks.log']:
        key = '.local/class-skills/revive-regeneration-candidate/' + name
        assert sha(old_stage / name) == previous['artifacts'][key], name
    assert retained == markers((old_stage / 'stats-native-checks.log').read_text()) + facets + respec
    complete = checked_log(FULL / 'class-native-checks.log', 1184, 35)
    assert complete == markers((old_stage / 'full-regression/class-native-checks.log').read_text())
    upgraded = checked_log(STAGE / 'upgrade-native-checks.log', 3)
    assert upgraded == Counter({'revive-cast-check': 1, 'revive-cast-reload-check': 2})
    upgrade = inputs[2]
    assert upgrade['oldReceipt']['commit'] == PREDECESSOR
    raw = subprocess.check_output(['git','show',PREDECESSOR + ':tools/reference/revive-regeneration-evidence.json'],cwd=ROOT)
    assert upgrade['oldReceipt']['sha256'] == hashlib.sha256(raw).hexdigest()
    assert len(upgrade['boots']) == 3
    for boot in upgrade['boots']:
        assert boot['savedFiles']
        expected = dict(tested['artifacts'])
        if boot['index'] == 0:
            for kind in ['content','integration']:
                name = 'Sanctuary.' + kind.title() + '.dll'
                assert upgrade['oldArtifacts'][name] == previous['inputInventory']['artifacts']['Assemblies/' + name]
                expected['Assemblies/' + name] = upgrade['oldArtifacts'][name]
        assert boot['artifacts'] == expected
    assert [b['index'] for b in upgrade['boots']] == [0, 1, 2]
    ledger = 'Saves/SanctuaryMonsterVariantBaselines/SanctuaryMonsterVariantBaselines.bin'
    assert ledger not in upgrade['boots'][0]['savedFiles']
    assert all(ledger in boot['savedFiles'] for boot in upgrade['boots'][1:])
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
    assert [len(a['InputHashes']) for a in audits] == [785, 21]
    python = STAGE / 'python-tests-attempt-3.log'
    assert 'Ran 71 tests' in python.read_text() and '\nOK\n' in python.read_text() and 'FAILED' not in python.read_text()
    installed_path = ROOT / 'tools/reference/creeper-frame-installation-evidence.json'
    installed = json.loads(installed_path.read_text())
    assert len(installed['installedArtifacts']) == 106
    for name, value in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / name) == value
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 185 and status['completedClasses'] == []
    artifacts += [python, installed_path, STAGE / 'python-upstream-reference-inputs.json']
    artifacts += [STAGE / name for name in ['isolated-revive-facet-fixtures.json', 'revive-facet-fixtures.json',
        'revive-respec-fixtures.json', 'upgrade-revive-cast-fixtures.json', 'retained-run-attempt-3b.log', 'upgrade-run-attempt-3b.log']]
    artifacts += [ROOT / name for name in ['tests/check_revive_facets.py', 'tests/check_revive_facets_regression.py',
        'tests/check_revive_facets_upgrade.py', 'tests/check_creeper_stats.py']]
    result = dict(referenceVersion='3.3.93847', component='Revive facet projections and retained native Normal baselines', installed=False,
        goalComplete=False, completedClasses=[], handlers=185,
        verified=dict(focusedMarkers=9, retainedMarkers=366, upgradeMarkers=3, fullClassMarkers=1184,
            bootsPerScope=3, actualReloadsPerScope=2, fullClassSaves=35, pythonTests=71,
            nativePatchCount=70, nativeBinariesReused=True, inputFingerprints=[785, 21],
            unchangedNamedDefinitions=573, unchangedInstalledArtifacts=106),
        frozenPredecessor=dict(commit=PREDECESSOR, receipt='tools/reference/revive-regeneration-evidence.json'),
        inputInventory=tested, focusedInputInventory=inputs[0], upgradeInputInventory=upgrade, fullClassInputInventory=inputs[3],
        reusedNativeManifest=dict(path=str(native_manifest), sha256=sha(native_manifest)),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},
        established=[
            'Actual Normal/Nightmare-born original actors follow owner facet designations through Normal/Nightmare/Hell using separate live plans, target levels and captured seed/caster/mastery, preserving relative FP256 life and immutable release/corpse history.',
            'Native Normal damage/resistance/channel/movement baselines survive difficulty assignment, original retention and actual reloads. Native damage/resistance/weapon-lane consumers, repeated notifications, low-life and prepared above65000boundaries, connected travel and actual Hell lightning immunity are checked.',
            'Explicit missing-baseline refusal parks without guessing; restoring the actual baseline permits retry. Malformed active plans/baselines refuse atomically. Old candidate binaries create the upgrade world; two new-candidate boots retain its active/pending learned originals and separate loot, with actual DLL/save hashes recorded per boot.',
            'Focused, retained and full candidate scopes use identical source/data and five DLLs. All predecessor retained/full markers and separate public respec coverage remain present. Fresh audits and installed hashes are checked.'],
        remaining=[
            'No class completion or deployment. Integral-only legacy setups, absent native Normal baselines in older difficulty variants, disabled/unclassified target variants and encounter-specific projections remain explicit unsupported cases.',
            'The old-world upgrade scope proves preserved loading/saving, not inferred missing native stats or universal legacy cross-difficulty projection. Prepared rank255Orders is an upper-domain fixture, not an attainable build or natural duration claim.',
            'Independent equipped-item/stat/proc/sunder consumers and Summon Resist source conflict remain unfinished. Native abilities remain inherited; global monster spell conversion is a separate later task.',
            'Complete modern executable and negative-regeneration/death-event equivalence are not asserted. Final effects/audio and talent/hotkey icons remain separately tracked placeholders.'])
    output = ROOT / 'tools/reference/revive-facets-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Revive facets:9focused/366retained/3upgrade/1184full/71Python;scoped acceptance only.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
