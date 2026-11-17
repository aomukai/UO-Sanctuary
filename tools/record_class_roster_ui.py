#!/usr/bin/env python3
"""Attribute eighth-class rendered creation evidence to its exact installed package."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re

import skill_checkpoint_evidence as e

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/class-roster-ui'
RUN = STAGE / 'run-2'


def record(check=False):
    result = e.load(RUN / 'result.json')
    assert result['variants'] == [dict(wireClass=108, female=female, boots=2, passed=True)
                                  for female in (False, True)]
    assert result['allEightChoicesClicked'] and result['bothSexControlsClicked']
    assert not result['combatVerified']
    assert e.load(RUN / 'protection-result.json') == {'changed': []}
    originals = e.load(RUN / 'protected-before.json')
    assert len(originals) == result['originalFilesUnchanged'] == 109
    runtime = Path('/media/aomukai/FILES/Sanctuary')
    for name, digest in originals.items():
        assert e.sha(runtime / name) == digest, name
    for name, digest in e.load(RUN / 'artifacts.json').items():
        assert e.sha(Path(name)) == digest, name
    inputs = e.load(STAGE / 'source-inputs.json')
    for name, digest in inputs.items():
        if name != 'tests/check_class_roster_ui.py':
            assert e.sha(ROOT / name) == digest, name
    assert e.sha(STAGE / 'driver-run-2.py') == e.sha(ROOT / 'tests/check_class_roster_ui.py')
    assert '0 Warning(s)' in (STAGE / 'build.log').read_text()
    assert '0 Error(s)' in (STAGE / 'build.log').read_text()
    inventories = {}
    for sex in ('male', 'female'):
        folder = RUN / sex
        log = (folder / 'server.log').read_text()
        markers = Counter(re.findall(r'class-names-ui-check: (passed|failed)', log))
        assert markers == {'passed': 3}, markers
        assert log.count('Writing world save snapshot done') == 2
        assert not re.search(r'\bERR\b|Exception|: failed', log), sex
        assert 'all eight class choices' in (folder / 'ui.log').read_text()
        for wire_id in range(101, 109):
            for suffix in ('male', 'female'):
                assert (folder / f't03-creation-{wire_id}-{suffix}.png').is_file()
            assert (folder / f't03-back-preserved-{wire_id}.png').is_file()
        assert (folder / f't03-paperdoll-{sex}.png').is_file()
        assert (folder / f't03-talents-{sex}.png').is_file()
        assert any((folder / 'Saves').rglob('*.bin'))
        inventories[sex] = dict(nativeChecks=dict(markers), boots=2, saves=2,
                                screenshots=len(list(folder.glob('*.png'))))
    failure = e.load(STAGE / 'run-1-failure.json')
    assert not failure['gameplayChanged']
    assert 'Server.Network.PingServer.Shutdown()' in (STAGE / 'run-1/male/server.log').read_text()
    files = [ROOT / name for name in inputs]
    files += [p for p in RUN.rglob('*') if p.is_file()]
    files += [p for p in (STAGE / 'run-1').rglob('*') if p.is_file()]
    files += [STAGE / name for name in ('source-inputs.json', 'sources.tar.gz', 'driver-run-2.py',
        'run-1-failure.json', 'run-1.log', 'build.log', 'integration/Sanctuary.Integration.dll', 'run-2.log')]
    files += [ROOT / 'docs/class-roster-ui-checkpoint.md', Path(__file__)]
    e.write('class-roster-ui-evidence.json', dict(referenceVersion='3.3.93847',
        allEightChoicesClicked=True, appearanceVariantsInspected=16,
        createdWireClass=108, variants=inventories, nativeChecks=6,
        originalFilesUnchanged=109, installedGameplayChanged=False,
        nativeTitleVerified=True, renderedTalentHeadingVerified=True, renderedPaperdollVerified=False,
        failureRetained=True, renderedCreationVerified=True, combatVerified=False,
        finalAssetsVerified=False, classCertified=False, goalComplete=False), files, check)
    print('Roster UI: eight choices/16 appearance summaries;Warlock+Witch creation and reload;109 protected files unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
