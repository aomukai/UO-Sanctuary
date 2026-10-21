#!/usr/bin/env python3
"""Inspect and round-trip a disposable copy of the installed world's save.

Build Sanctuary.Integration first. No fixture commands, live console commands,
installation, or writes to the source runtime are performed.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import tempfile

from check_britain import ROOT, SERVER, CONTENT, INTEGRATION, Session


def hashes(folder):
    return {str(p.relative_to(folder)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(folder.rglob('*')) if p.is_file()}


def port(kind=socket.SOCK_STREAM):
    with socket.socket(type=kind) as reservation:
        reservation.bind(('127.0.0.1', 0))
        return reservation.getsockname()[1]


def check(output):
    source = ROOT / '.local/server'
    runtime = SERVER
    output.mkdir(parents=True, exist_ok=True)
    assert not (source / 'Assemblies/Sanctuary.Integration.dll').exists(), 'Live runtime contains integration assembly'
    config = json.loads((source / 'Configuration/modernuo.json').read_text())
    assert config['settings']['world.savePath'] == 'Saves', 'Review nonstandard live save path first'
    with tempfile.TemporaryDirectory(prefix='sanctuary-working-baseline-', dir='/dev/shm') as temporary:
        folder = Path(temporary)
        for entry in runtime.iterdir():
            if entry.is_file() and entry.suffix in ('.dll', '.json', '.config'):
                shutil.copy2(entry, folder / entry.name)
        (folder / 'runtimes').symlink_to(runtime / 'runtimes', target_is_directory=True)
        shutil.copytree(source / 'Configuration', folder / 'Configuration')
        for name in ('Assemblies', 'Data'):
            (folder / name).mkdir()
            for entry in (runtime / name).iterdir():
                if name == 'Data' and entry.name == 'assemblies.json':
                    continue
                target = entry
                if runtime != source and entry.name == 'Sanctuary.Content.dll':
                    target = CONTENT
                if runtime != source and entry.name == 'Sanctuary':
                    target = ROOT / 'data/Sanctuary'
                (folder / name / entry.name).symlink_to(target, target_is_directory=target.is_dir())
        if not (folder / 'Assemblies/Sanctuary.Content.dll').exists():
            (folder / 'Assemblies/Sanctuary.Content.dll').symlink_to(CONTENT)
        if not (folder / 'Data/Sanctuary').exists():
            (folder / 'Data/Sanctuary').symlink_to(ROOT / 'data/Sanctuary', target_is_directory=True)
        probe = INTEGRATION
        assert probe.is_file(), 'Build the integration project first'
        (folder / 'Assemblies/Sanctuary.Integration.dll').symlink_to(probe)
        assemblies = json.loads((source / 'Data/assemblies.json').read_text())
        assert 'Sanctuary.Integration.dll' not in assemblies
        assemblies.append('Sanctuary.Integration.dll')
        (folder / 'Data/assemblies.json').write_text(json.dumps(assemblies))
        # An autosave could replace files during copying. Require one stable,
        # byte-identical snapshot before booting the disposable world.
        for attempt in range(3):
            before = hashes(source / 'Saves')
            shutil.copytree(source / 'Saves', folder / 'Saves', dirs_exist_ok=True)
            if before == hashes(source / 'Saves') == hashes(folder / 'Saves'):
                break
            shutil.rmtree(folder / 'Saves')
        else:
            raise AssertionError('Live saves changed during all three copy attempts')
        (output / 'copied-save-hashes.json').write_text(json.dumps(before, indent=2))
        listen, ping = port(), port(socket.SOCK_DGRAM)
        config['listeners'] = [f'127.0.0.1:{listen}']
        config['settings'].update({'autosave.enabled': 'False', 'world.savePath': 'Saves',
                                   'pingServer.port': str(ping), 'crashGuard.restartServer': 'False',
                                   'accountHandler.enableAutoAccountCreation': 'False'})
        if os.environ.get('SANCTUARY_TEST_PROFESSIONS') == '1':
            config['settings']['sanctuary.professions.enabled'] = 'True'
        (folder / 'Configuration/modernuo.json').write_text(json.dumps(config))
        processing = os.environ.get('SANCTUARY_TEST_PROCESSING') == '1'
        snapshots = []
        progression = []
        with (output / 'copied-world-server.log').open('w') as log:
            for iteration in range(3 if processing else 2):
                if processing:
                    config['settings']['sanctuary.professions.enabled'] = 'False' if iteration == 0 else 'True'
                    (folder / 'Configuration/modernuo.json').write_text(json.dumps(config))
                session = Session(folder, log)
                try:
                    session.expect(f'Listening: 127.0.0.1:{listen}')
                    session.send('working-baseline-inspect')
                    assert session.expect(r'working-baseline-inspect: (passed|failed)').group(1) == 'passed'
                    snapshot = json.loads((folder / 'working-baseline.json').read_text())
                    snapshots.append(snapshot)
                    (output / f'copied-world-{iteration + 1}.json').write_text(json.dumps(snapshot, indent=2))
                    session.send('save')
                    session.expect('Writing world save snapshot done')
                    progression.append({'Characters': hashes(folder / 'Saves/SanctuaryCharacters'),
                                        'Bindings': hashes(folder / 'Saves/SanctuaryBindings'),
                                        'Titles': hashes(folder / 'Saves/SanctuaryEarnedTitles')})
                    session.send('shutdown')
                    session.expect('Shutting down')
                    session.process.wait(timeout=20)
                    assert session.process.returncode == 0
                finally:
                    session.close()
        for key in ('Players', 'Gear', 'Gold', 'Architects', 'Gamblers'):
            assert snapshots[-2][key] == snapshots[-1][key], f'{key} changed after copied-world restart'
        assert all(p == progression[0] for p in progression), 'Serialized class/talent/allocation history changed on restart'
        if os.environ.get('SANCTUARY_TEST_PROFESSIONS') == '1':
            for player in snapshots[-1]['Players']:
                expected = {'Craft': 0, 'Confirmed': False} if player['Profile'] and player['Access'] == 'Player' else None
                assert player['Profession'] == expected, 'Installed character was assigned a confirmed craft or staff restricted'
        claims = {c['Serial']: c['SanctuaryNoRewards'] for c in snapshots[1]['Corpses']}
        for corpse in snapshots[0]['Corpses']:
            if corpse['SanctuaryNoRewards']:
                assert claims.get(corpse['Serial']) is True, 'Spent corpse claim lost on restart'
        death_claims = {c['Serial']: c['SanctuaryDeathCharged'] for c in snapshots[-1]['Corpses']}
        for corpse in snapshots[-2]['Corpses']:
            assert death_claims.get(corpse['Serial']) == corpse['SanctuaryDeathCharged'], 'Existing death claim changed on restart'
        if processing:
            for before, after in zip(snapshots[0]['Players'], snapshots[1]['Players'], strict=True):
                assert {k: v for k, v in before.items() if k != 'Profession'} == {k: v for k, v in after.items() if k != 'Profession'}
            def identities(rows):
                return [{k: v for k, v in row.items() if k not in ('Weight', 'CanonicalWeight')} for row in rows]
            assert identities(snapshots[0]['Materials']) == identities(snapshots[1]['Materials']) == identities(snapshots[2]['Materials'])
            assert snapshots[1]['Materials'] == snapshots[2]['Materials']
            assert all(m['CanonicalWeight'] is None or m['Weight'] == m['CanonicalWeight'] for m in snapshots[1]['Materials'])
            print(f"Processing migration: disabled/enabled/enabled boots; {len(snapshots[1]['Materials'])} existing material/bandage identities, amounts, subtypes and origins retained; canonical weights stable.")
        print('Copied installed save: boots/save/shutdowns; player profiles, allocations, all native skill values/caps/locks, worn identities and gear rolls/origins preserved.')
        print(f"Players={len(snapshots[0]['Players'])}; gear={len(snapshots[0]['Gear'])}; origins={snapshots[0]['Origins']}; corpses={snapshots[0]['Corpses']}; raised={snapshots[0]['Raised']}")
        print(f'Local evidence: {output}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / '.local/logs/working-baseline-check')
    check(parser.parse_args().output.resolve())
