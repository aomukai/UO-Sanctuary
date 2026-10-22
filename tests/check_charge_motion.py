#!/usr/bin/env python3
"""Native class-skill checks in a disposable world; never writes the installed runtime/saves."""
import json
import os
from pathlib import Path
import shutil
import socket
import struct
import time
import tempfile
from check_britain import ROOT, SERVER, Session

STAGE = ROOT / '.local/class-skills'


def check():
    logpath = STAGE / 'charge-motion-native.log'
    with tempfile.TemporaryDirectory(prefix='sanctuary-class-skills-', dir='/dev/shm') as temp:
        folder = Path(temp)
        for entry in SERVER.iterdir():
            if entry.is_file() and entry.suffix in ('.dll', '.json', '.config'):
                shutil.copy2(entry, folder / entry.name)
        (folder / 'runtimes').symlink_to(SERVER / 'runtimes', target_is_directory=True)
        shutil.copytree(SERVER / 'Configuration', folder / 'Configuration')
        for name in ('Assemblies', 'Data'):
            (folder / name).mkdir()
            for entry in (SERVER / name).iterdir():
                if entry.name not in ('Sanctuary.Content.dll', 'UOContent.dll', 'Sanctuary', 'assemblies.json'):
                    (folder / name / entry.name).symlink_to(entry, target_is_directory=entry.is_dir())
        native = ROOT / 'upstream/ModernUO/Distribution'
        shutil.copy2(native / 'Server.dll', folder / 'Server.dll')
        shutil.copy2(native / 'Assemblies/UOContent.dll', folder / 'Assemblies/UOContent.dll')
        for name, rel in [('Sanctuary.Content.dll', 'content'), ('Sanctuary.Integration.dll', 'integration')]:
            (folder / 'Assemblies' / name).symlink_to(STAGE / rel / name)
        shutil.copytree(ROOT / 'data/Sanctuary', folder / 'Data/Sanctuary')
        assemblies = json.loads((SERVER / 'Data/assemblies.json').read_text())
        for name in ('Sanctuary.Content.dll', 'Sanctuary.Integration.dll'):
            if name not in assemblies:
                assemblies.append(name)
        (folder / 'Data/assemblies.json').write_text(json.dumps(assemblies))
        configpath = folder / 'Configuration/modernuo.json'
        config = json.loads(configpath.read_text())
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0)); port = reservation.getsockname()[1]
        with socket.socket(type=socket.SOCK_DGRAM) as reservation:
            reservation.bind(('127.0.0.1', 0)); ping_port = reservation.getsockname()[1]
        config['listeners'] = [f'127.0.0.1:{port}']
        config['settings'].update({'autosave.enabled':'False','pingServer.enabled':'True','pingServer.port':str(ping_port),
            'sanctuary.graveyard.enabled':'False','world.savePath':'Saves','crashGuard.restartServer':'False',
            'sanctuary.professions.enabled':'True' if os.environ.get('SANCTUARY_TEST_NAMED_GEAR') == '1' else 'False'})
        configpath.write_text(json.dumps(config))
        with logpath.open('w') as log:
            session = Session(folder, log)
            client = None
            try:
                session.expect(r'create the owner account now\? \(y/n\):'); session.send('n')
                session.expect(f'Listening: 127.0.0.1:{port}')
                def command(name):
                    session.send(name)
                    result = session.expect(name+r': (passed|failed)')
                    assert result.group(1) == 'passed', logpath.read_text()[-6000:]
                command('charge-motion-check')
                client = socket.create_connection(('127.0.0.1', port), timeout=5)
                time.sleep(.15)
                command('charge-motion-network-setup')
                state = json.loads((folder/'charge-motion-network.json').read_text())
                def drain():
                    data = bytearray(); client.settimeout(.15)
                    while True:
                        try:
                            chunk = client.recv(65536)
                            if not chunk: break
                            data.extend(chunk)
                        except socket.timeout: break
                    return bytes(data)
                drain()
                command('charge-motion-network-step')
                data = drain()
                needle = bytes([0x20])+struct.pack('>I',state['Serial'])
                updates=[]; position=0
                while True:
                    position=data.find(needle,position)
                    if position<0: break
                    packet=data[position:position+19]
                    assert len(packet)==19,'Truncated owning-player position correction'
                    updates.append([int.from_bytes(packet[11:13],'big'),int.from_bytes(packet[13:15],'big'),int.from_bytes(packet[18:19],'big',signed=True)])
                    position+=19
                expected=[[state['X']+i,state['Y'],state['Z']]for i in range(1,5)]
                compact=[point for i,point in enumerate(updates) if i==0 or point!=updates[i-1]]
                assert compact==expected,{'updates':updates,'expected':expected,'bytes':len(data)}
                # DirectionEast, sequence0, legacy fast-walk key0: a real ordinary client step.
                client.sendall(bytes([0x02,0x02,0x00])+bytes(4))
                time.sleep(.10)
                normal=drain()
                assert bytes([0x22,0x00]) in normal,'No ordinary movement acknowledgement after owned motion'
                command('charge-motion-network-finish')
                (STAGE/'charge-motion-network-proof.json').write_text(json.dumps({'ownerUpdates':updates,'ownerStepPositions':compact,'ordinaryMovementAcknowledged':True,'checks':'native geometry/occupancy/guards; actual ground Charge phase/mana/finish through connected-owner movement/reset/position packets','bootstrap':'Fixture actor attachment/game protocol state; no real login or graphical-client proof'},indent=2)+'\n')
                client.close();client=None
                session.send('shutdown');session.process.wait(timeout=20)
            finally:
                if client is not None: client.close()
                session.close()
    print('Charge passed native collision/guards and connected-owner ground phase/mana/position/debt checks.')


if __name__ == '__main__':
    check()
