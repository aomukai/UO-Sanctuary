#!/usr/bin/env python3
"""Verify the adopted Bash generation without rewriting older class evidence."""
import argparse
from collections import Counter
from pathlib import Path
import hashlib
import tarfile

import skill_checkpoint_evidence as e

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/bash-flat-adoption'


def record(check=False):
    born = e.load(STAGE / 'birth-inputs.json')
    sources = e.load(STAGE / 'source-inputs-1.json')
    with tarfile.open(STAGE / 'sources-1.tar.gz') as archive:
        for name, digest in sources.items():
            assert hashlib.sha256(archive.extractfile(name).read()).hexdigest() == digest, name
            if name.endswith('.cs'):
                assert born['sourceInputs'][name] == digest, name
    assert born['sourceInputs']['tests/check_bash_flat_adoption.py'] == e.sha(STAGE / 'driver-run-2.py')
    expected = Counter({'iron-golem-birth-check': 3, 'barbarian-attacks-check': 1,
        'barbarian-attacks-reload-check': 2, 'barbarian-attacks-result': 1})
    for family in ('iron-golem-items', 'npc-resource-callbacks', 'barbarian-contact'):
        expected[family + '-check'] = 1
        expected[family + '-reload-check'] = 2
    for skill in ('bash', 'stun', 'concentrate', 'berserk'):
        for mode in ('right', 'quick'):
            expected[f'barbarian-contact-{skill}-{mode}-check'] = 3
            expected[f'barbarian-contact-{skill}-{mode}-result'] = 3
    log_path = STAGE / 'birth-native-checks.log'
    log = log_path.read_text()
    assert e.markers(log_path) == expected and sum(expected.values()) == 64
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    assert all(row['Generation'] == 2 for row in e.load(STAGE / 'barbarian-contact-fixtures.json'))
    for kind in ('content', 'integration'):
        assembly = f'Sanctuary.{kind.title()}.dll'
        assert e.sha(STAGE / kind / assembly) == born['artifacts']['Assemblies/' + assembly]
        text = (STAGE / f'{kind}-build-1.log').read_text()
        assert '0 Warning(s)' in text and '0 Error(s)' in text
    failure = STAGE / 'failed-run-1'
    for name, digest in e.load(failure / 'frozen-hashes.json').items():
        assert e.sha(failure / name) == digest, name
    assert 'barbarian-attacks-fixtures.json' in (failure / 'run-1.log').read_text()
    decision = e.load(ROOT / 'tools/reference/class-mechanics-intent-decision.json')
    assert decision['bash']['approved'] and decision['delegatedAdaptations']['approved']
    changed = ('src/Sanctuary.Content/BarbarianMelee.cs', 'src/Sanctuary.Content/PlayerWeaponContact.cs',
        'tests/Sanctuary.Integration/BarbarianAttackChecks.cs', 'tests/Sanctuary.Integration/BarbarianContactChecks.cs')
    files = [ROOT / name for name in changed]
    files += [p for p in failure.rglob('*') if p.is_file()]
    files += [STAGE / name for name in ('before-source.json', 'baseline-sources.tar.gz', 'baseline/Sanctuary.Content.dll',
        'source-inputs-1.json', 'sources-1.tar.gz', 'birth-inputs.json', 'birth-native-checks.log',
        'driver-run-2.py', 'driver-run-2.json', 'run-2.log', 'barbarian-attacks.json',
        'barbarian-contact-fixtures.json', 'content-build-1.log', 'integration-build-1.log',
        'content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll')]
    files += [ROOT / 'tools/reference/class-mechanics-intent-decision.json',
        ROOT / 'docs/bash-flat-adoption-checkpoint.md', Path(__file__)]
    e.write('bash-flat-adoption-native-evidence.json', dict(referenceVersion='3.3.93847', approved=True,
        productionChanged=list(changed[:2]), nativeResults=64, authenticatedInputs=24,
        saves=3, actualReloads=2, artifacts=born['artifacts'], baselineInstalledContent=e.sha(STAGE / 'baseline/Sanctuary.Content.dll'),
        installed=False, originalDataChanged=False, frozenNativeFailures=1, classCertified=False,
        goalComplete=False), files, check)
    print('Adopted Bash:64 results/24 authenticated inputs/two reloads;four attacks and mixed items retain;qualified generation not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
