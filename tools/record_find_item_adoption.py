#!/usr/bin/env python3
"""Bind the approved Find Item server/client generation to its actual checks."""
import argparse
from collections import Counter
import hashlib
import tarfile

import skill_checkpoint_evidence as e
from prepare_class_skills_handoff import hashes

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/find-item-adoption'
CLIENT = ROOT / '.local/class-skills/find-item-client'


def record(check=False):
    born = e.load(STAGE / 'birth-inputs.json')
    sources = e.load(STAGE / 'source-inputs-2.json')
    with tarfile.open(STAGE / 'sources-2.tar.gz') as archive:
        for name, digest in sources.items():
            assert hashlib.sha256(archive.extractfile(name).read()).hexdigest() == digest, name
            if name.endswith('.cs'):
                assert born['sourceInputs'][name] == digest, name
    expected = Counter({'iron-golem-birth-check': 3, 'find-item-targeting-check': 3,
                        'find-item-result': 3, 'barbarian-attacks-result': 1})
    for family in ('iron-golem-items', 'npc-resource-callbacks', 'find-item', 'barbarian-attacks', 'barbarian-contact'):
        expected[family + '-check'] = 1
        expected[family + '-reload-check'] = 2
    for mode in ('right', 'quick'):
        expected['find-item-ground-' + mode + '-check'] = 3
        expected['find-item-ground-' + mode + '-result'] = 3
    log = STAGE / 'birth-native-checks.log'
    assert e.markers(log) == expected and sum(expected.values()) == 37
    assert ': failed' not in log.read_text() and ' ERR]' not in log.read_text()
    assert log.read_text().count('Shutting down') == log.read_text().count('Writing world save snapshot done') == 3
    for kind in ('content', 'integration'):
        name = f'Sanctuary.{kind.title()}.dll'
        assert e.sha(STAGE / kind / name) == born['artifacts']['Assemblies/' + name]
    production = ROOT / 'src/Sanctuary.Content/BarbarianFindItem.cs'
    assert e.sha(production) == sources[str(production.relative_to(ROOT))]
    decision = e.load(ROOT / 'tools/reference/class-mechanics-intent-decision.json')
    assert decision['findItem']['approved'] and decision['findItem']['cursorRadiusTiles'] == 2
    client = e.load(CLIENT / 'build-result-1.json')
    assert client['patches'] == 45 and client['projectDependenciesComplete']
    for name, digest in client['artifacts'].items():
        assert e.sha(CLIENT / 'client' / name) == digest, name
    for name, digest in e.load(STAGE / 'visual-inputs-1.json').items():
        assert e.sha(ROOT / name) == digest, name
    visual = STAGE / 'run-1'
    result = e.load(visual / 'result.json')
    assert result['realClient'] and result['rightGround'] and result['quickGround']
    assert e.markers(visual / 'server.log') == Counter({'find-item-visual-' + suffix: 1 for suffix in ('setup', 'right', 'quick')})
    assert ' ERR]' not in (visual / 'server.log').read_text()
    assert 'Rendered Find Item:right ground and F1' in (visual / 'ui.log').read_text()
    original = e.load(ROOT / '.local/class-skills/class-skills-handoff/prepared-inputs.json')
    runtime = ROOT.__class__('/media/aomukai/FILES/Sanctuary')
    for name in ('.local/server/Saves', '.local/server/Configuration', '.local/client/Data', '.local/client/Profiles'):
        assert hashes(runtime / name) == original['original'][name], name
    assert e.sha(runtime / '.local/client/settings.json') == original['originalFiles']['.local/client/settings.json']
    files = [production, ROOT / 'patches/ClassicUO/0045-find-item-near-cursor.patch',
             ROOT / 'tests/Sanctuary.Integration/FindItemChecks.cs',
             ROOT / 'tests/Sanctuary.Integration/FindItemTargetingChecks.cs',
             ROOT / 'tests/Sanctuary.Integration/FindItemVisualChecks.cs',
             ROOT / 'tests/check_find_item_adoption.py', ROOT / 'tests/check_find_item_rendered.py',
             ROOT / 'tools/prepare_find_item_client.py', ROOT / 'docs/find-item-adoption-checkpoint.md']
    files += [STAGE / name for name in ('source-inputs-2.json', 'sources-2.tar.gz', 'birth-inputs.json',
              'birth-native-checks.log', 'driver-run-2.py', 'run-2.log', 'find-item.json',
              'barbarian-attacks.json', 'barbarian-contact-fixtures.json', 'content-build-1.log',
              'integration-build-2.log', 'integration-build-3.log', 'visual-inputs-1.json',
              'content/Sanctuary.Content.dll', 'integration-2/Sanctuary.Integration.dll',
              'integration-3/Sanctuary.Integration.dll')]
    files += [CLIENT / name for name in ('source-inputs-1.json', 'build-result-1.json', 'build-1.log')]
    files += [p for folder in (STAGE / 'failed-run-1', CLIENT / 'failed-verification-1', visual)
              for p in folder.rglob('*') if p.is_file()]
    e.write('find-item-adoption-evidence.json', dict(referenceVersion='3.3.93847', approved=True,
        productionChanged=True, installed=False, nativeResults=37, authenticatedGroundInputs=6,
        reloads=2, renderedSceneResults=3, renderedRightGround=True, renderedQuickGround=True,
        clientPatches=45, originalSavesUnchanged=True, finalAssets=False,
        chanceCurveChanged=False, treasureRollsChanged=False, numericalDataChanged=False,
        artifacts=born['artifacts'], goalComplete=False), files, check)
    print('Find Item:37 native results/six inputs/two reloads;actual right/F1 client ground casts;original saves unchanged;qualified,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
