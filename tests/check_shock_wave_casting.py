#!/usr/bin/env python3
"""Verify learned bear Shock Wave with saved component and native input checks."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import struct
import tempfile
import threading
from check_britain import ROOT, SERVER, Session
from check_class_skills import drain_fixture_client

STAGE=ROOT/'.local/class-skills/druid-shock-wave-casting'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def check():
    os.environ.setdefault('SANCTUARY_PROJECT_ROOT',str(ROOT))
    os.environ.setdefault('SANCTUARY_FIRST_LOOP_ROOT',str(ROOT))
    os.environ.setdefault('SANCTUARY_NAMED_AUDIT',str(STAGE/'named-item-runtime-audit.json'))
    os.environ.setdefault('SANCTUARY_FIRST_LOOP_AUDIT',str(STAGE/'first-loop-affix-audit.json'))
    native=Path(os.environ.get('SANCTUARY_NATIVE_ROOT',str(ROOT)))/'upstream/ModernUO/Distribution'
    logpath=STAGE/'native-checks.log'
    with tempfile.TemporaryDirectory(prefix='sanctuary-shock-wave-',dir='/dev/shm') as temp:
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
        for label in ['damage','stun']:
            name='druid-shock-wave-'+label+'-source-probe.json';shutil.copy2(ROOT/'.local/class-skills'/name,folder/name)
        shutil.copy2(ROOT/'.local/class-skills/druid-form-cast-clock-source-probe.json',folder/'druid-form-cast-clock-source-probe.json')
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
        sources=[p for directory in ['src/Sanctuary.Content','shared','tests/Sanctuary.Integration'] for p in (ROOT/directory).glob('*.cs')]
        sources += [Path(__file__).resolve(),ROOT/'tests/Sanctuary.Integration/Sanctuary.Integration.csproj']
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
                    casting='shock-wave-casting'+('-check' if boot==0 else '-reload-check')
                    session.send(casting);assert session.expect(casting+r': (passed|failed)',timeout=120).group(1)=='passed',logpath.read_text()[-6000:]
                    command='shock-wave-components'+('-check' if boot==0 else '-reload-check')
                    session.send(command);assert session.expect(command+r': (passed|failed)',timeout=120).group(1)=='passed',logpath.read_text()[-6000:]
                    if boot==0:
                        session.send('first-loop-catalogue-check');assert session.expect(r'first-loop-catalogue-check: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                    named='named-gear-check' if boot==0 else 'named-gear-reload-check'
                    session.send(named);assert session.expect(named+r': (passed|failed)',timeout=120).group(1)=='passed',logpath.read_text()[-6000:]
                    with socket.create_connection(('127.0.0.1',port),timeout=5) as client:
                        threading.Thread(target=drain_fixture_client,args=(client,),daemon=True).start()
                        session.send('shock-wave-live-check')
                        assert session.expect(r'shock-wave-live-result: (passed|failed)',timeout=10).group(1)=='passed',logpath.read_text()[-6000:]
                    for family in ['shock-wave-left','shock-wave-right','shock-wave-quick']:
                        with socket.create_connection(('127.0.0.1',port),timeout=5) as client:
                            threading.Thread(target=drain_fixture_client,args=(client,),daemon=True).start()
                            session.send(family+'-check');assert session.expect(family+r'-check: (passed|failed)').group(1)=='passed'
                            ready=session.expect(family+r': ready ability=(\d+) class=(\d+) target=(\d+) x=(\d+) y=(\d+) z=(-?\d+)')
                            ability,character,target,x,y,z=map(int,ready.groups())
                            if not family.endswith('quick'):client.sendall(struct.pack('>BHHBBIBIHHb',0xBF,21,0xF2,7,7 if family.endswith('left') else 4,1,ability,target,x,y,z))
                            else:client.sendall(struct.pack('>BHHBBIBBBIHHb',0xBF,23,0xF7,2,4,1,character,7,ability,target,x,y,z))
                            session.expect(family+': windup')
                            if not family.endswith('quick'):client.sendall(struct.pack('>BHHBBI',0xBF,11,0xF2,7,8 if family.endswith('left') else 5,2))
                            assert session.expect(family+r'-result: (passed|failed)',timeout=10).group(1)=='passed',logpath.read_text()[-6000:]
                    session.send('shock-wave-casting-snapshot-check');assert session.expect(r'shock-wave-casting-snapshot-check: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                    if boot<2:
                        session.send('shock-wave-expiry-stage-check');assert session.expect(r'shock-wave-expiry-stage-check: (passed|failed)').group(1)=='passed'
                    session.send('save');session.expect('Writing world save snapshot done')
                    if boot<2:
                        # Bind the actual serialized profile payload,not just
                        # the pre-save in-memory fixture description.
                        staged=json.loads((folder/'shock-wave-fixtures.json').read_text())
                        token=('"Owner":'+str(staged['ExpiryOwner'])).encode()
                        signature=b'"DelayScope":"cast-target-inferred-2.7"'
                        saved=[p for p in (folder/'Saves').rglob('*') if p.is_file() and token in p.read_bytes() and signature in p.read_bytes()]
                        assert saved,'Expiry fixture was not present in actual world save bytes'
                        (STAGE/f'expiry-save-{boot}.json').write_text(json.dumps(dict(owner=staged['ExpiryOwner'],files={str(p.relative_to(folder)):sha(p) for p in saved}),indent=2)+'\n')
                    session.send('shutdown');assert session.process.wait(timeout=20)==0
                finally:session.close()
        fixture=json.loads((folder/'shock-wave-fixtures.json').read_text());assert fixture['ExpiredChecks']==2
        for name in ['shock-wave-fixtures.json','shock-wave-casting-fixtures.json']:shutil.copy2(folder/name,STAGE/name)
    text=logpath.read_text();assert ': failed' not in text and ' ERR]' not in text
    assert text.count(': passed')==39 and text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    print('Learned Shock Wave:39markers/3boots/2actual reloads/9real left-right-quick casts/3connected natural wave sessions/2expired-save controls;3825physical/17920stun comparisons; named/first-loop checks; full regression and installation remain pending.')


if __name__=='__main__':check()
