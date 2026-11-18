#!/usr/bin/env python3
"""List or restore one locally compacted rollback snapshot, verifying every file."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import tarfile

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def restore(runtime, snapshot=None):
    cold = runtime / '.local/cold-storage'
    metadata = json.loads((cold / 'legacy-backups-20261118.json').read_text())
    if snapshot is None:
        print('\n'.join(metadata['snapshots']))
        return
    if snapshot not in metadata['snapshots'] or Path(snapshot).name != snapshot:
        raise ValueError('Choose an exact snapshot name from --list.')
    destination = runtime / '.local/backups' / snapshot
    if destination.exists() or destination.is_symlink():
        raise ValueError(f'Refusing to overwrite an existing snapshot: {destination}')
    archive = cold / metadata['archiveFile']
    if sha(archive) != metadata['archiveSha256']:
        raise ValueError('The local rollback archive checksum does not match.')
    manifest = json.loads((cold / metadata['manifestFile']).read_text())
    expected = {name: value for name, value in manifest.items() if name.startswith(snapshot + '/')}
    if not expected:
        raise ValueError('The requested snapshot has no retained file manifest.')
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging = destination.parent / ('.' + snapshot + '.restore')
    staging.mkdir()  # Never reuse a partial earlier restoration.
    try:
        with tarfile.open(archive, 'r:gz') as saved:
            members = [member for member in saved
                       if member.name == snapshot or member.name.startswith(snapshot + '/')]
            for member in members:
                path = Path(member.name)
                if path.is_absolute() or '..' in path.parts or not (member.isfile() or member.isdir()):
                    raise ValueError('Unsafe member in the local rollback archive.')
            saved.extractall(staging, members=members, filter='data')
        actual = {str(path.relative_to(staging)): dict(size=path.stat().st_size, sha256=sha(path))
                  for path in (staging / snapshot).rglob('*') if path.is_file()}
        if actual != expected:
            raise ValueError('Restored snapshot does not match its retained file manifest.')
        (staging / snapshot).rename(destination)
        print(f'Restored {snapshot}: {len(actual)} files verified; existing saves and settings untouched.')
    finally:
        shutil.rmtree(staging)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime-root', type=Path, default=ROOT)
    selection = parser.add_mutually_exclusive_group(required=True)
    selection.add_argument('--list', action='store_true')
    selection.add_argument('--snapshot')
    args = parser.parse_args()
    try:
        restore(args.runtime_root.resolve(), args.snapshot)
    except (OSError, ValueError, tarfile.TarError) as error:
        parser.exit(1, str(error) + '\n')
