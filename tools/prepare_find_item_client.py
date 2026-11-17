#!/usr/bin/env python3
"""Extend the qualified client with Find Item's approved shared ground-aim path."""
import difflib
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/find-item-client'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_inputs(destination):
    return {str(p.relative_to(destination)): sha(p) for p in destination.rglob('*')
            if p.is_file() and not {'obj', 'bin'}.intersection(p.relative_to(destination).parts)
            and p.suffix in ('.cs', '.csproj', '.props', '.targets')}


def verify(stage):
    manifest = json.loads((stage / 'source-inputs-1.json').read_text())
    assert manifest['allProjectSources'] == source_inputs(stage / 'source')
    dependencies = json.loads((stage / 'client/cuo.deps.json').read_text())
    assert all(any(key.startswith(name + '/') for key in dependencies['libraries'])
               for name in ('ClassicUO.Utility', 'ClassicUO.IO', 'ClassicUO.Assets', 'ClassicUO.Renderer'))
    artifacts = {str(p.relative_to(stage / 'client')): sha(p) for p in (stage / 'client').rglob('*') if p.is_file()}
    (stage / 'build-result-1.json').write_text(json.dumps(dict(patches=45, artifacts=artifacts,
        sourceInputsSha256=sha(stage / 'source-inputs-1.json'), projectDependenciesComplete=True), indent=2) + '\n')
    print('Find Item client:45 patches,fresh project graph and qualified shared ground-aim input paths built.')


def prepare():
    assert not STAGE.exists(), 'Use a fresh build stage; retain failed attempts.'
    stage = Path('/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/find-item-client-1')
    assert not stage.exists()
    manifest_path = ROOT / '.local/class-skills/consume-learned/client-inputs-1.json'
    manifest = json.loads(manifest_path.read_text())
    source = ROOT / '.local/class-skills/consume-learned/client-root'
    for name, digest in manifest['patchHashes'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in manifest['clientInputs'].items():
        assert sha(source / 'upstream/ClassicUO' / name) == digest, name
    for name, digest in manifest['sharedInputs'].items():
        assert sha(ROOT / name) == sha(source / name) == digest, name
    destination = stage / 'source'
    shutil.copytree(source, destination, ignore=shutil.ignore_patterns('.git', 'obj', 'bin', 'TestResults'))
    STAGE.symlink_to(stage, target_is_directory=True)
    relative = 'src/ClassicUO.Client/Game/ClassCombatClient.cs'
    path = destination / 'upstream/ClassicUO' / relative
    original = path.read_text()
    anchor = 'CombatAbility.CorpseExplosion or CombatAbility.PoisonExplosion'
    assert original.count(anchor) == 5
    changed = original.replace(anchor, anchor + ' or CombatAbility.FindItem')
    path.write_text(changed)
    patch = ROOT / 'patches/ClassicUO/0045-find-item-near-cursor.patch'
    assert not patch.exists()
    patch.write_text(''.join(difflib.unified_diff(original.splitlines(True), changed.splitlines(True),
        fromfile='a/' + relative, tofile='b/' + relative)))
    command = [str(ROOT / '.tools/dotnet/dotnet'), 'build',
        str(destination / 'upstream/ClassicUO/src/ClassicUO.Client/ClassicUO.Client.csproj'),
        '-c', 'Debug', '-o', str(stage / 'client'), '--nologo', '--no-incremental', '-p:PublishAot=false']
    inputs = source_inputs(destination)
    (stage / 'source-inputs-1.json').write_text(json.dumps(dict(qualifiedManifestSha256=sha(manifest_path),
        qualifiedPatchHashes=manifest['patchHashes'], addedPatch=str(patch.relative_to(ROOT)),
        addedPatchSha256=sha(patch), changedFile=relative, originalSha256=hashlib.sha256(original.encode()).hexdigest(),
        changedSha256=sha(path), allProjectSources=inputs, command=command, freshRestore=True), indent=2) + '\n')
    with (stage / 'build-1.log').open('w') as log:
        result = subprocess.run(command, cwd=destination, stdout=log, stderr=subprocess.STDOUT)
    result.check_returncode()
    verify(stage)


if __name__ == '__main__':
    import sys
    if '--verify-existing' in sys.argv:
        verify(STAGE.resolve())
    else:
        prepare()
