#!/usr/bin/env python3
"""Verify current delivery through explicit source bridges, preserving old receipts."""
import argparse
from collections import Counter
import hashlib
from pathlib import Path
import tarfile

import skill_checkpoint_evidence as e
from prepare_class_skills_handoff import hashes
from audit_class_skill_completion import CLASSES

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/class-closing-installation'
QUALIFIED = ROOT / '.local/class-skills/find-item-adoption'
EDGES = ROOT / '.local/class-skills/barbarian-closing-edges'
RUNTIME = Path('/media/aomukai/FILES/Sanctuary')


def audit(check=False):
    installed = e.load(STAGE / 'installed.json')
    plan = e.load(STAGE / 'plan.json')
    assert installed['installed'] and installed['productionIntegrationAbsent'] and installed['defaultStagingVerified']
    for name, digest in installed['installedFiles'].items():
        assert e.sha(RUNTIME / name) == digest, name
    previous = e.load(ROOT / '.local/class-skills/main-consolidation/installation-1/installed.json')
    for name, digest in previous['installedFiles'].items():
        candidate = STAGE / 'rollback' / name if name in plan['previousFiles'] else RUNTIME / name
        assert e.sha(candidate) == digest, name
    for name, digest in plan['previousFiles'].items():
        if digest is not None:
            assert e.sha(STAGE / 'rollback' / name) == digest, name
    original = e.load(ROOT / '.local/class-skills/class-skills-handoff/prepared-inputs.json')
    for name in ('.local/server/Saves', '.local/server/Configuration', '.local/client/Data', '.local/client/Profiles'):
        assert hashes(RUNTIME / name) == original['original'][name], name
    assert e.sha(RUNTIME / '.local/client/settings.json') == original['originalFiles']['.local/client/settings.json']
    current = e.load(QUALIFIED / 'birth-inputs.json')
    assert installed['productionContentSha256'] == current['artifacts']['Assemblies/Sanctuary.Content.dll']
    assert e.sha(RUNTIME / '.local/server/Assemblies/Sanctuary.Content.dll') == installed['productionContentSha256']
    for name, digest in current['sourceInputs'].items():
        if name.startswith(('src/', 'shared/')) and name.endswith('.cs'):
            assert e.sha(ROOT / name) == digest, name
    for name, digest in current['copiedData'].items():
        assert e.sha(ROOT / 'data' / name.removeprefix('Data/')) == digest, name
        assert e.sha(RUNTIME / '.local/server' / name) == digest, name
    changed = {'src/Sanctuary.Content/' + name for name in
               ('BarbarianFindItem.cs', 'BarbarianMelee.cs', 'PlayerWeaponContact.cs')}
    sentinel = 'src/Sanctuary.Content/BladeSentinelMissiles.cs'
    classes, files = [], []
    for index, name in enumerate(CLASSES):
        stage = ROOT / ('.local/class-skills/' + name + '-lifecycle-acceptance')
        born = e.load(stage / 'birth-inputs.json')
        receipt = e.load(ROOT / ('tools/reference/' + name + '-lifecycle-acceptance-native-evidence.json'))
        inputs = e.load(ROOT / ('tools/reference/' + name + '-current-input-coverage.json'))
        assert receipt['artifacts'] == inputs['artifacts'] == born['artifacts']
        assert e.markers(stage / 'birth-native-checks.log') == Counter(receipt['markers'])
        differences = {path for path, digest in born['sourceInputs'].items()
                       if path.startswith(('src/', 'shared/')) and path.endswith('.cs') and e.sha(ROOT / path) != digest}
        assert differences == changed | ({sentinel} if index < 5 else set()), (name, differences)
        assert inputs['verifiedActiveInputRoutes'] == inputs['activeTalents']
        assert inputs['activeTalents'] + inputs['passiveTalents'] == 30
        classes.append(dict(characterClass=name, activeRoutes=inputs['activeTalents'],
            passiveEntries=inputs['passiveTalents'], sourceChanges=sorted(differences),
            retainedNativeResults=sum(receipt['markers'].values())))
        files += [stage / 'birth-inputs.json', stage / 'birth-native-checks.log',
                  ROOT / ('tools/reference/' + name + '-lifecycle-acceptance-native-evidence.json'),
                  ROOT / ('tools/reference/' + name + '-current-input-coverage.json')]
    edges = e.load(EDGES / 'birth-inputs.json')
    assert edges['artifacts']['Assemblies/Sanctuary.Content.dll'] == installed['productionContentSha256']
    expected = Counter({'iron-golem-birth-check': 3, 'barbarian-closing-edges-check': 3,
        'iron-golem-items-check': 1, 'npc-resource-callbacks-check': 1,
        'iron-golem-items-reload-check': 2, 'npc-resource-callbacks-reload-check': 2})
    assert e.markers(EDGES / 'birth-native-checks.log') == expected
    assert e.sha(EDGES / 'integration/Sanctuary.Integration.dll') == edges['artifacts']['Assemblies/Sanctuary.Integration.dll']
    assert e.sha(ROOT / 'tests/Sanctuary.Integration/BarbarianClosingEdgesChecks.cs') == edges['sourceInputs']['tests/Sanctuary.Integration/BarbarianClosingEdgesChecks.cs']
    assert (EDGES / 'birth-native-checks.log').read_text().count('Writing world save snapshot done') == 3
    assert ' ERR]' not in (EDGES / 'birth-native-checks.log').read_text()
    with tarfile.open(EDGES / 'sources-2.tar.gz') as archive:
        for name, digest in e.load(EDGES / 'source-inputs-2.json').items():
            assert hashlib.sha256(archive.extractfile(name).read()).hexdigest() == digest
            if name.endswith('.cs'):
                assert edges['sourceInputs'][name] == digest
    for register in ('skill-effects', 'skill-icons'):
        rows = e.load(ROOT / ('data/Sanctuary/Balance/' + register + '.json'))
        assert len(rows) == 240 and all(row['placeholder'] for row in rows)
    for receipt in ('bash-flat-adoption-native-evidence', 'find-item-adoption-evidence'):
        path = ROOT / ('tools/reference/' + receipt + '.json')
        for name, digest in e.load(path)['files'].items():
            assert e.sha(ROOT / name) == digest, name
        files.append(path)
    files += [ROOT / name for name in sorted(changed | {sentinel})]
    files += [STAGE / name for name in ('installed.json', 'plan.json', 'installed-world-smoke.log')]
    files += [EDGES / name for name in ('birth-inputs.json', 'birth-native-checks.log', 'source-inputs-2.json',
        'sources-2.tar.gz', 'run-1.log', 'build-2.log', 'integration/Sanctuary.Integration.dll')]
    files += [ROOT / name for name in ('tests/Sanctuary.Integration/BarbarianClosingEdgesChecks.cs',
        'tests/check_barbarian_closing_edges.py', 'tools/install_class_closing_changes.py',
        'docs/class-closing-package-checkpoint.md')]
    files.append(Path(__file__))
    e.write('class-closing-package-evidence.json', dict(referenceVersion='3.3.93847',
        installed=True, classes=classes, handlers=240, activeRoutes=210, passiveEntries=30,
        bridgedProductionChanges=sorted(changed), sentinelBridgeRetained=True,
        currentContentSha256=installed['productionContentSha256'], clientPatches=45,
        bashQualified=True, findItemQualified=True, barbarianClosingEdgeResults=12,
        edgeProductionChanges=False, originalEntitiesLoaded=5290, originalSaveFilesUnchanged=38,
        originalConfigurationFilesUnchanged=20, finalAssetsPending=True,
        remaining=['Finish representative rendered combat checks for six other classes',
            'Reconcile supported equipment/native-species boundaries and final completion ledger'],
        goalComplete=False), files, check)
    print('Installed closing package:240 routes/entries,three scoped production changes,45client patches,12 edge results;old evidence and original saves preserved.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    audit(parser.parse_args().check)
