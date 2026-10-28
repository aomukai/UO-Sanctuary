#!/usr/bin/env python3
"""Verify prepared Shock Wave arithmetic,collision,stun,flight and saved state."""
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

STAGE=ROOT/'.local/class-skills/druid-shock-wave'


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
        shutil.copy2(folder/'shock-wave-fixtures.json',STAGE/'shock-wave-fixtures.json')
    text=logpath.read_text();assert ': failed' not in text and ' ERR]' not in text
    assert text.count(': passed')==15 and text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    print('Prepared Shock Wave:15markers/3boots/2actual reloads/3connected natural wave sessions/2expired-save controls;3825physical/17920stun comparisons; named/first-loop checks; learned casting pending.')


if __name__=='__main__':check()
