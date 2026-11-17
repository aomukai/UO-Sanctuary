#!/usr/bin/env python3
"""Attribute real Necromancer quick input and natural pet combat on the installed package."""
import argparse
from collections import Counter
from pathlib import Path

import skill_checkpoint_evidence as e

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/necromancer-rendered'
RUN = STAGE / 'run-9'


def record(check=False):
    inputs = e.load(STAGE / 'source-inputs-9.json')
    for name, digest in inputs.items():
        assert e.sha(ROOT / name) == digest, name
    for name, digest in e.load(STAGE / 'before-source.json').items():
        assert e.sha(ROOT / name) == digest, name
    result = e.load(RUN / 'result.json')
    assert result == dict(wireClass=103, realClient=True, f1Raise=True, f2TeethDamage=True,
        naturalOriginalWarriorDamage=True, finalAssets=False, allNecromancerTalentsRendered=False)
    expected = Counter({'necromancer-visual-' + name: 1 for name in ('setup', 'raised', 'teeth', 'fight', 'fought')})
    assert e.markers(RUN / 'server.log') == expected
    text = (RUN / 'server.log').read_text()
    assert ': failed' not in text and ' ERR]' not in text
    assert text.count('Writing world save snapshot done') == text.count('Shutting down') == 1
    assert 'F1 learned Warrior,F2 isolated Teeth damage and original Warrior natural combat passed.' in (RUN / 'ui.log').read_text()
    scene = e.load(RUN / 'scene.json')
    assert scene['pet']['serial'] != scene['player']['serial'] and scene['target']['serial'] != scene['pet']['serial']
    assert '0 Warning(s)' in (STAGE / 'build-9.log').read_text()
    assert '0 Error(s)' in (STAGE / 'build-9.log').read_text()
    runtime = Path('/media/aomukai/FILES/Sanctuary')
    installed = e.load(ROOT / '.local/class-skills/main-consolidation/installation-1/installed.json')
    for name, digest in installed['installedFiles'].items():
        assert e.sha(runtime / name) == digest, name
    world = e.load(ROOT / '.local/class-skills/class-skills-handoff/prepared-inputs.json')
    from prepare_class_skills_handoff import hashes
    for name in ('.local/server/Saves', '.local/server/Configuration', '.local/client/Data', '.local/client/Profiles'):
        assert hashes(runtime / name) == world['original'][name], name
    assert e.sha(runtime / '.local/client/settings.json') == world['originalFiles']['.local/client/settings.json']
    artifacts = {name: e.sha(runtime / '.local/server' / name) for name in (
        'ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll', 'Assemblies/Sanctuary.Content.dll')}
    artifacts['client/cuo.dll'] = e.sha(runtime / '.local/client/cuo.dll')
    artifacts['Assemblies/Sanctuary.Integration.dll'] = e.sha(STAGE / 'integration-9/Sanctuary.Integration.dll')
    files = [ROOT / name for name in inputs]
    files += [p for p in RUN.rglob('*') if p.is_file()]
    for folder in sorted(STAGE.glob('failed-*')):
        if (folder / 'frozen-hashes.json').exists():
            for name, digest in e.load(folder / 'frozen-hashes.json').items():
                assert e.sha(folder / name) == digest, name
        files += [p for p in folder.rglob('*') if p.is_file()]
    files += [STAGE / name for name in ('before-source.json', 'source-inputs-9.json', 'sources-9.tar.gz',
        'build-9.log', 'run-9.log', 'integration-9/Sanctuary.Integration.dll')]
    files += [ROOT / 'docs/necromancer-rendered-checkpoint.md', Path(__file__)]
    e.write('necromancer-rendered-evidence.json', dict(referenceVersion='3.3.93847',
        artifacts=artifacts, realClient=True, learnedF1Raise=True, learnedF2TeethDamage=True,
        naturalOriginalWarriorCombat=True, nativeSceneResults=5, disposableSaves=1,
        originalSaveFilesUnchanged=38, originalConfigurationUnchanged=20,
        productionChanged=False, allNecromancerTalentsRendered=False, finalAssetsVerified=False,
        acceptedIntegration=9, acceptedRun=9, frozenCompileFailures=2, frozenNativeFailures=7,
        classCertified=False, goalComplete=False), files, check)
    print('Rendered Necromancer:F1 summon/F2 player damage/original natural Warrior combat;five scene results;installed package and original saves preserved.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
