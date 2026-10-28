#!/usr/bin/env python3
"""Verify approved Rabies away policy against the unchanged gameplay candidate."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import tempfile
import threading
from check_britain import ROOT, SERVER, Session
from check_class_skills import drain_fixture_client

STAGE=ROOT/'.local/class-skills/druid-rabies-away'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def check():
    receipt=json.loads((ROOT/'tools/reference/druid-rabies-native-evidence.json').read_text())
    content=ROOT/'.local/class-skills/druid-rabies/content/Sanctuary.Content.dll'
    assert sha(content)==receipt['inputInventory']['artifacts']['Assemblies/Sanctuary.Content.dll']
    (STAGE/'content').mkdir(exist_ok=True);shutil.copy2(content,STAGE/'content/Sanctuary.Content.dll')
    native=Path(os.environ.get('SANCTUARY_NATIVE_ROOT',str(ROOT)))/'upstream/ModernUO/Distribution'
    logpath=STAGE/'native-checks.log'
    with tempfile.TemporaryDirectory(prefix='sanctuary-rabies-away-',dir='/dev/shm') as temp:
        folder=Path(temp)
        for entry in SERVER.iterdir():
            if entry.is_file() and entry.suffix in ['.dll','.json','.config']:shutil.copy2(entry,folder/entry.name)
        (folder/'runtimes').symlink_to(SERVER/'runtimes',target_is_directory=True)
        shutil.copytree(SERVER/'Configuration',folder/'Configuration')
        for name in ['Assemblies','Data']:
            (folder/name).mkdir()
            for entry in (SERVER/name).iterdir():
                if entry.name not in ['Sanctuary.Content.dll','Sanctuary.Integration.dll','UOContent.dll','Sanctuary','assemblies.json']:
                    (folder/name/entry.name).symlink_to(entry,target_is_directory=entry.is_dir())
        for relative in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:shutil.copy2(native/relative,folder/relative)
        for kind in ['content','integration']:
            name='Sanctuary.'+kind.title()+'.dll';(folder/'Assemblies'/name).symlink_to(STAGE/kind/name)
        shutil.copytree(ROOT/'data/Sanctuary',folder/'Data/Sanctuary')
        assemblies=json.loads((SERVER/'Data/assemblies.json').read_text())
        for name in ['Sanctuary.Content.dll','Sanctuary.Integration.dll']:
            if name not in assemblies:assemblies.append(name)
        (folder/'Data/assemblies.json').write_text(json.dumps(assemblies))
        with socket.socket() as reservation:reservation.bind(('127.0.0.1',0));port=reservation.getsockname()[1]
        with socket.socket(type=socket.SOCK_DGRAM) as reservation:reservation.bind(('127.0.0.1',0));ping=reservation.getsockname()[1]
        configpath=folder/'Configuration/modernuo.json';config=json.loads(configpath.read_text());config['listeners']=[f'127.0.0.1:{port}']
        config['settings'].update({'autosave.enabled':'False','pingServer.port':str(ping),'pingServer.enabled':'True',
            'sanctuary.graveyard.enabled':'False','world.savePath':'Saves','crashGuard.restartServer':'False'})
        configpath.write_text(json.dumps(config))
        sources=[p for directory in ['src/Sanctuary.Content','shared','tests/Sanctuary.Integration','tests/Sanctuary.RabiesPolicy'] for p in (ROOT/directory).glob('*.cs')]
        sources += [Path(__file__).resolve(),ROOT/'tests/Sanctuary.RabiesPolicy/Sanctuary.Integration.csproj']
        inputs=dict(sourceInputs={str(p.relative_to(ROOT)):sha(p) for p in sorted(sources)},
            copiedData={str(p.relative_to(folder)):sha(p) for p in sorted((folder/'Data/Sanctuary').rglob('*')) if p.is_file()},
            artifacts={name:sha(folder/name) for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll','Assemblies/Sanctuary.Content.dll','Assemblies/Sanctuary.Integration.dll']})
        (STAGE/'inputs.json').write_text(json.dumps(inputs,indent=2)+'\n')
        with logpath.open('w') as log:
            for boot in range(3):
                session=Session(folder,log)
                try:
                    if boot==0:session.expect(r'create the owner account now\? \(y/n\):');session.send('n')
                    session.expect(f'Listening: 127.0.0.1:{port}')
                    command=['rabies-away-start','rabies-away-return','rabies-away-expire'][boot]
                    if boot<2:
                        with socket.create_connection(('127.0.0.1',port),timeout=5) as client:
                            threading.Thread(target=drain_fixture_client,args=(client,),daemon=True).start()
                            session.send(command)
                            # The fixture requests a real server disconnect;
                            # drain until native transport teardown completes.
                            assert session.expect(command+r'-result: (passed|failed)',timeout=35).group(1)=='passed',logpath.read_text()[-6000:]
                    else:
                        session.send(command);assert session.expect(command+r': (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                    session.send('save');session.expect('Writing world save snapshot done')
                    session.send('shutdown');assert session.process.wait(timeout=20)==0
                finally:session.close()
        state=json.loads((folder/'rabies-away-fixtures.json').read_text());assert state['Reloads']==2 and not any(state['State'][key] for key in ['Markers','Infections','Contagions'])
        shutil.copy2(folder/'rabies-away-fixtures.json',STAGE/'rabies-away-fixtures.json')
    text=logpath.read_text();assert ': failed' not in text and ' ERR]' not in text
    assert text.count(': passed')==5 and text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    print('Rabies away policy:5markers/3boots/2actual reloads/2real disconnects/facet departure-return; unchanged gameplay candidate.')


if __name__=='__main__':check()
