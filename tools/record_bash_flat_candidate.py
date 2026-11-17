#!/usr/bin/env python3
"""Bind the tested Bash flat-damage proposal without declaring it adopted."""
import argparse
from collections import Counter
from pathlib import Path

import skill_checkpoint_evidence as e

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/bash-flat-candidate'


def record(check=False):
    born = e.load(STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    for name, digest in e.load(STAGE / 'source-inputs-1.json').items():
        assert e.sha(ROOT / name) == digest, name
    assert all(e.sha(ROOT / name) == digest for name, digest in e.load(STAGE / 'before-source.json').items())
    for name, digest in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / name.removeprefix('Data/')) == digest, name
    runtime = Path('/media/aomukai/FILES/Sanctuary')
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll', 'Assemblies/Sanctuary.Content.dll'):
        assert e.sha(runtime / '.local/server' / name) == born['artifacts'][name], name
    assert e.sha(STAGE / 'integration/Sanctuary.Integration.dll') == born['artifacts']['Assemblies/Sanctuary.Integration.dll']
    expected = Counter({'iron-golem-birth-check': 3, 'bash-flat-candidate-check': 3,
        'iron-golem-items-check': 1, 'iron-golem-items-reload-check': 2,
        'npc-resource-callbacks-check': 1, 'npc-resource-callbacks-reload-check': 2})
    log_path = STAGE / 'birth-native-checks.log'
    log = log_path.read_text()
    assert e.markers(log_path) == expected and sum(expected.values()) == 12
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Bash flat candidate:16 rank/resistance/critical cases') == 3
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    text = (STAGE / 'build-1.log').read_text()
    assert '0 Warning(s)' in text and '0 Error(s)' in text
    proposal = ROOT / 'docs/bash-flat-damage-proposal.md'
    reviewed = ROOT / 'tools/reference/bash-flat-reviewed-proposal.md'
    assert proposal.read_bytes() == reviewed.read_bytes()
    files = [Path(__file__), proposal, reviewed, ROOT / 'tests/check_bash_flat_candidate.py',
        ROOT / 'tests/Sanctuary.Integration/BashFlatCandidateChecks.cs',
        ROOT / 'src/Sanctuary.Content/BarbarianMelee.cs', ROOT / 'src/Sanctuary.Content/PlayerWeaponContact.cs',
        ROOT / 'src/Sanctuary.Content/GearEffects.cs', ROOT / 'tools/reference/barbarian-attack-engine-evidence.json',
        ROOT / 'tools/reference/barbarian-contact-source-review.json']
    files += [STAGE / name for name in ('before-source.json', 'source-inputs-1.json', 'sources-1.tar.gz',
        'birth-inputs.json', 'birth-native-checks.log', 'build-1.log', 'run-1.log',
        'integration/Sanctuary.Integration.dll')]
    e.write('bash-flat-candidate-evidence.json', dict(referenceVersion='3.3.93847',
        status='tested-pending-user-decision', approved=False, learnedRouteChanged=False,
        productionChanged=False, installedGameplayChanged=False, currentD2RCallbackVerified=False,
        distinctCandidateCases=16, markers=dict(expected), saves=3, reloads=2,
        artifacts=born['artifacts'], proposalSha256=e.sha(reviewed),
        verified=['Existing post-critical hook before mitigation', 'Rank1/rank20 flat table values',
            'Ordinary/critical weapon contribution', 'Physical resistance -50/0/50/100',
            'Actual contact health matches mitigated physical component', 'Miss skips allocation callback'],
        notQualified=['Learned Bash candidate startup/release', 'All shield/dodge/avoidance recipients',
            'Item leech/proc and poison combinations', 'Saved pending-candidate actions', 'Modern D2R equivalence'],
        classCertified=False, goalComplete=False), files, check)
    print('Bash flat candidate:16 distinct cases/actual native health;12 results/two reloads;unadopted and learned route unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
