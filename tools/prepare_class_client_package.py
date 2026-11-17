#!/usr/bin/env python3
"""Rebuild qualified client sources with fresh project-reference metadata."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def prepare(stage):
    sys.path.insert(0, str(ROOT / 'tools'))
    from prepare_class_skills_handoff import sha, write
    source = ROOT / '.local/class-skills/consume-learned/client-root'
    manifest_path = ROOT / '.local/class-skills/consume-learned/client-inputs-1.json'
    manifest = json.loads(manifest_path.read_text())
    destination = stage / 'source'
    assert not stage.exists(), 'Keep each source/build attempt separate'
    for path, digest in manifest['patchHashes'].items():
        assert sha(ROOT / path) == digest, path
    for path, digest in manifest['clientInputs'].items():
        assert sha(source / 'upstream/ClassicUO' / path) == digest, path
    for path, digest in manifest['sharedInputs'].items():
        assert sha(ROOT / path) == sha(source / path) == digest, path
    shutil.copytree(source, destination, ignore=shutil.ignore_patterns('.git', 'obj', 'bin', 'TestResults'))
    for path, digest in manifest['clientInputs'].items():
        assert sha(destination / 'upstream/ClassicUO' / path) == digest, path
    for path, digest in manifest['sharedInputs'].items():
        assert sha(destination / path) == digest, path
    files = {str(p.relative_to(destination)): sha(p) for p in sorted(destination.rglob('*'))
             if p.is_file() and p.suffix in ('.cs', '.csproj', '.props', '.targets')}
    command = [str(ROOT / '.tools/dotnet/dotnet'), 'build',
               str(destination / 'upstream/ClassicUO/src/ClassicUO.Client/ClassicUO.Client.csproj'),
               '-c', 'Debug', '-o', str(stage / 'client'), '--nologo', '--no-incremental',
               '-p:PublishAot=false']
    write(stage / 'source-inputs-1.json', dict(qualifiedManifestSha256=sha(manifest_path),
          clientInputs=manifest['clientInputs'], sharedInputs=manifest['sharedInputs'],
          patchHashes=manifest['patchHashes'], allProjectSources=files, command=command,
          freshRestore=True, excludedStaleDirectories=['obj', 'bin']))
    with (stage / 'build-1.log').open('w') as log:
        result = subprocess.run(command, cwd=destination, stdout=log, stderr=subprocess.STDOUT)
    assert {str(p.relative_to(destination)): sha(p) for p in sorted(destination.rglob('*'))
            if p.is_file() and p.suffix in ('.cs', '.csproj', '.props', '.targets')
            and not {'obj', 'bin'}.intersection(p.relative_to(destination).parts)} == files
    write(stage / 'build-result-1.json', dict(exitCode=result.returncode,
          artifacts={p.name: sha(p) for p in sorted((stage / 'client').glob('*')) if p.is_file()}))
    assert result.returncode == 0, 'Preserve failed sources/build log before correcting'
    deps = json.loads((stage / 'client/cuo.deps.json').read_text())
    libraries = next(iter(deps['targets'].values()))
    required = ('ClassicUO.Utility', 'ClassicUO.IO', 'ClassicUO.Assets', 'ClassicUO.Renderer')
    assert all(any(name.startswith(needed + '/') for name in libraries) for needed in required)
    print('Fresh client graph includes all four ClassicUO libraries; qualified source and44patches unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage', type=Path)
    prepare(parser.parse_args().stage.resolve())
