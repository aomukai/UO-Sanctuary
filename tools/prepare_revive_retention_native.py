#!/usr/bin/env python3
"""Replay the pinned native patch series in a separate, noninstalled tree."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tarfile

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/revive-original-retention'
PIN = 'eff9de028136b11e544fd81375d1e0e710e22dd6'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inputs(native):
    return dict(upstream=PIN, archiveSha256=sha(STAGE / 'upstream.tar'),
        patches={str(p.relative_to(ROOT)): sha(p) for p in sorted((ROOT / 'patches/ModernUO').glob('*.patch'))},
        nativeSources={str(p.relative_to(native)): sha(p) for p in sorted(native.rglob('*')) if p.is_file() and p.suffix in ['.cs', '.csproj', '.props', '.targets', '.json'] and 'obj' not in p.relative_to(native).parts and 'bin' not in p.relative_to(native).parts and 'Distribution' not in p.relative_to(native).parts},
        targetsSha256=sha(ROOT / 'build/ModernUO.targets'))


def prepare(source, check=False):
    native = STAGE / 'native'
    manifest = STAGE / 'native-pre-build-inputs.json'
    if check:
        assert json.loads(manifest.read_text()) == inputs(native), 'Native source or patch inputs changed'
    else:
        assert not native.exists() and not manifest.exists(), 'Preserve previous attempts before preparing a fresh native tree'
        STAGE.mkdir(parents=True, exist_ok=True)
        native.mkdir()
        with (STAGE / 'upstream.tar').open('wb') as output:
            subprocess.run(['git', '-C', str(source), 'archive', PIN], stdout=output, check=True)
        with tarfile.open(STAGE / 'upstream.tar') as archive:
            archive.extractall(native, filter='data')
        # git apply inside an ignored subdirectory otherwise finds the parent
        # worktree and can silently skip paths outside its directory prefix.
        subprocess.run(['git', 'init', '--quiet', str(native)], check=True)
        for patch in sorted((ROOT / 'patches/ModernUO').glob('*.patch')):
            subprocess.run(['git', 'apply', str(patch)], cwd=native, check=True)
        assert 'RetainOriginal' in (native / 'Projects/UOContent/Mobiles/ClassLootHooks.cs').read_text()
        manifest.write_text(json.dumps(inputs(native), indent=2) + '\n')
    print('Pinned native source and complete patch-series inputs match; no installed files modified.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path, nargs='?', default=Path('/media/aomukai/FILES/Sanctuary/upstream/ModernUO'))
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    prepare(args.source, args.check)
