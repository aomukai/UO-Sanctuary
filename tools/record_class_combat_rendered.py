#!/usr/bin/env python3
"""Retain the six final rendered consumers without relabeling prior class tests."""
import argparse
from collections import Counter
import hashlib
import tarfile
from pathlib import Path
import skill_checkpoint_evidence as e
from prepare_class_skills_handoff import hashes

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/class-combat-rendered'


def record(check=False):
    sources = {}
    files = []
    for generation in (3, 5):
        sources[generation] = e.load(STAGE / f'source-inputs-{generation}.json')
        with tarfile.open(STAGE / f'sources-{generation}.tar.gz') as archive:
            for name, digest in sources[generation].items():
                assert hashlib.sha256(archive.extractfile(name).read()).hexdigest() == digest
                if name.startswith(('src/', 'shared/')):
                    assert e.sha(ROOT / name) == digest
        text = (STAGE / f'build-{generation}.log').read_text()
        assert '0 Warning(s)' in text and '0 Error(s)' in text
        files += [STAGE / f'source-inputs-{generation}.json', STAGE / f'sources-{generation}.tar.gz',
                  STAGE / f'build-{generation}.log', STAGE / f'integration-{generation}/Sanctuary.Integration.dll']
    assert e.sha(ROOT / 'tests/check_class_combat_rendered.py') == sources[5]['tests/check_class_combat_rendered.py']
    assert e.sha(ROOT / 'tests/Sanctuary.Integration/ClassCombatVisualChecks.cs') == sources[5]['tests/Sanctuary.Integration/ClassCombatVisualChecks.cs']
    scenes = []
    for run in ('101', '102-2', '104', '106', '107', '108'):
        folder = STAGE / ('run-' + run)
        result, scene = e.load(folder / 'result.json'), e.load(folder / 'scene.json')
        assert result['realClient'] and result['learnedF1Consumer'] and result['onceOnlyPayment'] and result['noWalking']
        assert result['wireClass'] == scene['wireClass'] == int(run.split('-')[0])
        assert e.markers(folder / 'server.log') == Counter({'class-combat-visual-setup': 1, 'class-combat-visual-cast': 1})
        text = (folder / 'server.log').read_text()
        assert ' ERR]' not in text and text.count('Writing world save snapshot done') == text.count('Shutting down') == 1
        assert 'actual F1 consumer/payment/no walking passed.' in (folder / 'ui.log').read_text()
        scenes.append(dict(wireClass=scene['wireClass'], ability=scene['ability'], integration=3 if run == '101' else 5,
                           originalRun=run, nativeResults=2, disposableSaves=1))
        files += [folder / name for name in ('result.json', 'scene.json', 'server.log', 'ui.log',
            'class-combat-before.png', 'class-combat-after.png')]
    original = e.load(ROOT / '.local/class-skills/class-skills-handoff/prepared-inputs.json')
    runtime = Path('/media/aomukai/FILES/Sanctuary')
    for name in ('.local/server/Saves', '.local/server/Configuration', '.local/client/Data', '.local/client/Profiles'):
        assert hashes(runtime / name) == original['original'][name]
    for folder in ('failed-build-1', 'failed-build-2', 'failed-native-102-1'):
        for name, digest in e.load(STAGE / folder / 'frozen-hashes.json').items():
            assert e.sha(STAGE / folder / name) == digest
        files.append(STAGE / folder / 'frozen-hashes.json')
    files += [ROOT / name for name in ('tests/check_class_combat_rendered.py',
        'tests/Sanctuary.Integration/ClassCombatVisualChecks.cs', 'docs/class-combat-rendered-checkpoint.md')]
    files.append(Path(__file__))
    e.write('class-combat-rendered-evidence.json', dict(referenceVersion='3.3.93847', realClient=True,
        scenes=scenes, nativeSceneResults=12, disposableSaves=6, productionChanged=False,
        previousNecromancerBarbarianRetained=True, finalAssets=False, originalSavesUnchanged=True,
        fullModernParityClaimed=False), files, check)
    print('Six additional classes:12 real-client consumer results/six saves;all eight representative class scopes now retained;no production changes.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
