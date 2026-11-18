#!/usr/bin/env python3
"""Check committed release information; optionally smoke a copy of the current world."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import socket
import sys
import tempfile

from build_skill_roadmap import build

ROOT = Path(__file__).resolve().parents[1]
CLASSES = ('necromancer', 'sorceress', 'paladin', 'barbarian', 'amazon', 'assassin', 'druid', 'warlock')


def load(path):
    return json.loads(path.read_text())


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def hashes(folder):
    return {str(p.relative_to(folder)): sha(p) for p in folder.rglob('*') if p.is_file()}


def verify(runtime=None):
    policy = load(ROOT / 'tools/reference/class-mechanics-release-policy.json')
    summary = load(ROOT / 'tools/reference/class-skill-release-summary.json')
    ledger = load(ROOT / 'data/Sanctuary/Balance/class-skill-status.json')
    assert ledger == build()
    assert ledger['completedClasses'] == policy['completedClasses'] == ledger['classOrder']
    assert len(ledger['skills']) == 240 and all(row['complete'] for row in ledger['skills'])
    assert len({row['talentId'] for row in ledger['skills']}) == len({row['sourceId'] for row in ledger['skills']}) == 240
    assert policy['referenceVersion'] == summary['referenceVersion'] == '3.3.93847'
    authority = ROOT / policy['authority']['path']
    assert sha(authority) == policy['authority']['sha256']
    assert load(authority)['delegatedAdaptations']['approved']
    assert not policy['exactUnpublishedModernParityClaimed']
    for name, digest in policy['productionSources'].items():
        assert sha(ROOT / name) == digest, name
    assert hashes(ROOT / 'data/Sanctuary') == summary['balanceFiles']
    assert sha(ROOT / summary['historicalEvidence']) == summary['historicalEvidenceSha256']
    active = passive = 0
    for name in CLASSES:
        inputs = load(ROOT / ('tools/reference/' + name + '-current-input-coverage.json'))
        receipt = load(ROOT / ('tools/reference/' + name + '-lifecycle-acceptance-native-evidence.json'))
        assert inputs['artifacts'] == receipt['artifacts']
        identities = {row['talentId'] for row in inputs['skills']} | {row['talentId'] for row in inputs['passiveSkills']}
        assert identities == {row['talentId'] for row in ledger['skills'] if row['talentId'].startswith(name + '.')}
        assert inputs['activeTalents'] == inputs['verifiedActiveInputRoutes']
        active += inputs['activeTalents']; passive += inputs['passiveTalents']
    assert (active, passive) == (210, 30)
    for kind in ('skill-effects', 'skill-icons'):
        rows = load(ROOT / ('data/Sanctuary/Balance/' + kind + '.json'))
        assert len(rows) == 240 and all(row['placeholder'] for row in rows)
        assert {row['talentId'] for row in rows} == set(policy['talentIds'])
    if runtime is not None:
        server = runtime / '.local/server'
        for name, digest in summary['installedServerArtifacts'].items():
            assert sha(server / name) == digest, name
        assert sha(runtime / '.local/content/Sanctuary.Content.dll') == policy['installedContentSha256']
        assert hashes(server / 'Data/Sanctuary') == summary['balanceFiles']
        assert sha(runtime / '.local/client/cuo.dll') == summary['installedClientSha256']
        assert load(server / 'Data/assemblies.json') == ['UOContent.dll', 'Sanctuary.Content.dll']
        assert not (server / 'Assemblies/Sanctuary.Integration.dll').exists()
    print('Release source/data/recorded qualification:8 classes,240 talents,210 active routes,30 passives. '
          'Historical raw runs are retired;this check does not replay them.')


def smoke(runtime):
    server = runtime / '.local/server'
    protected = ('Saves', 'Configuration')
    before = {name: hashes(server / name) for name in protected}
    sys.path.insert(0, str(ROOT / 'tests'))
    from check_britain import Session
    with tempfile.TemporaryDirectory(prefix='sanctuary-release-smoke-') as folder:
        temp = Path(folder)
        for path in server.iterdir():
            if path.is_file() and path.suffix in ('.dll', '.json', '.config'):
                shutil.copy2(path, temp / path.name)
        for name in ('Configuration', 'Saves', 'Data', 'Assemblies'):
            shutil.copytree(server / name, temp / name)
        (temp / 'runtimes').symlink_to(server / 'runtimes', target_is_directory=True)
        configpath = temp / 'Configuration/modernuo.json'
        config = load(configpath)
        with socket.socket() as tcp:
            tcp.bind(('127.0.0.1', 0)); port = tcp.getsockname()[1]
        with socket.socket(type=socket.SOCK_DGRAM) as udp:
            udp.bind(('127.0.0.1', 0)); ping = udp.getsockname()[1]
        config['listeners'] = [f'127.0.0.1:{port}']
        config['settings'].update({'autosave.enabled': 'False', 'crashGuard.restartServer': 'False',
            'world.savePath': 'Saves', 'pingServer.enabled': 'True', 'pingServer.port': str(ping)})
        configpath.write_text(json.dumps(config))
        with (temp / 'smoke.log').open('w') as output:
            session = Session(temp, output)
            try:
                session.expect(f'Listening: 127.0.0.1:{port}')
                session.send('shutdown'); session.expect('Shutting down')
                assert session.process.wait(timeout=20) == 0
            finally:
                session.close()
        text = (temp / 'smoke.log').read_text()
        assert 'Loading world done (' in text and ' ERR]' not in text and 'Saving world' not in text
    assert {name: hashes(server / name) for name in protected} == before
    print('Current world copy loads/listens/shuts down without saving;actual saves/configuration unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Check committed release information (default).')
    parser.add_argument('--runtime-root', type=Path, help='Also verify the installed release at this checkout.')
    parser.add_argument('--smoke', action='store_true', help='Load a disposable copy of that runtime world.')
    args = parser.parse_args()
    runtime = args.runtime_root.resolve() if args.runtime_root else None
    if args.smoke and runtime is None:
        parser.error('--smoke requires --runtime-root')
    verify(runtime)
    if args.smoke:
        smoke(runtime)
