#!/usr/bin/env python3
"""Native class-skill checks in a disposable world; never writes the installed runtime/saves."""
import json
import os
from pathlib import Path
import shutil
import socket
import tempfile
import time
import threading
from check_britain import ROOT, SERVER, Session

STAGE = ROOT / '.local/class-skills'


def drain_fixture_client(client):
    """Read ordinary server output while the fixture's real connection stays attached."""
    try:
        while client.recv(65536):
            pass
    except OSError:
        pass


def check():
    logpath = STAGE / 'native-checks.log'
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
            for boot in range(3):
                session = Session(folder, log)
                try:
                    if boot == 0:
                        session.expect(r'create the owner account now\? \(y/n\):'); session.send('n')
                    session.expect(f'Listening: 127.0.0.1:{port}')
                    command = 'class-skills-check' if boot == 0 else 'class-skills-reload-check'
                    session.send(command)
                    assert session.expect(command+r': (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                    if boot == 0:
                        session.send('class-skills-ai-check')
                        assert session.expect(r'class-skills-ai-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('sorceress-skills-check')
                        start = session.expect(r'sorceress-skills-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'sorceress-skills-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('enchant-check')
                        assert session.expect(r'enchant-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('fire-ground-check')
                        start = session.expect(r'fire-ground-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'fire-ground-result: (passed|failed)',timeout=8).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('meteor-check')
                        start = session.expect(r'meteor-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'meteor-result: (passed|failed)',timeout=8).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('blizzard-check')
                        start = session.expect(r'blizzard-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'blizzard-result: (passed|failed)',timeout=10).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('frozen-orb-check')
                        start = session.expect(r'frozen-orb-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'frozen-orb-result: (passed|failed)',timeout=8).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('hydra-check')
                        start = session.expect(r'hydra-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'hydra-result: (passed|failed)',timeout=15).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('inferno-primitives-check')
                        start = session.expect(r'inferno-primitives-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'inferno-primitives-result: (passed|failed)',timeout=8).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('chain-lightning-check')
                        start = session.expect(r'chain-lightning-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'chain-lightning-result: (passed|failed)',timeout=8).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-aura-check')
                        start = session.expect(r'paladin-aura-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-aura-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-resistance-check')
                        start = session.expect(r'paladin-resistance-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-resistance-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-travel-check')
                        start = session.expect(r'paladin-travel-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-travel-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-offense-check')
                        start = session.expect(r'paladin-offense-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-offense-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-element-check')
                        start = session.expect(r'paladin-element-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-element-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('holy-bolt-check')
                        start = session.expect(r'holy-bolt-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'holy-bolt-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('heaven-fist-check')
                        start = session.expect(r'heaven-fist-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'heaven-fist-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('blessed-hammer-check')
                        start = session.expect(r'blessed-hammer-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'blessed-hammer-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-thorns-check')
                        start = session.expect(r'paladin-thorns-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-thorns-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-conviction-check')
                        start = session.expect(r'paladin-conviction-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-conviction-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('holy-shield-check')
                        start = session.expect(r'holy-shield-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'holy-shield-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('vengeance-check')
                        assert session.expect(r'vengeance-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('sacrifice-check')
                        assert session.expect(r'sacrifice-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('smite-check')
                        assert session.expect(r'smite-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        assert session.expect(r'smite-result: (passed|failed)',timeout=12).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('zeal-check')
                        assert session.expect(r'zeal-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('charge-check')
                        assert session.expect(r'charge-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('charge-motion-check')
                        assert session.expect(r'charge-motion-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('damage-rounding-check')
                        assert session.expect(r'damage-rounding-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('resource-load-check')
                        assert session.expect(r'resource-load-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-resource-check')
                        start = session.expect(r'paladin-resource-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-resource-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('charged-bolt-check')
                        start = session.expect(r'charged-bolt-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'charged-bolt-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('sorceress-lightning-check')
                        start = session.expect(r'sorceress-lightning-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'sorceress-lightning-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('thunderstorm-check')
                        start = session.expect(r'thunderstorm-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'thunderstorm-result: (passed|failed)',timeout=20).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('bone-barriers-check')
                        start = session.expect(r'bone-barriers-(check|result): (passed|failed)',timeout=45)
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        assert session.expect(r'bone-barriers-result: (passed|failed)',timeout=45).group(1) == 'passed', logpath.read_text()[-5000:]
                    else:
                        session.send('sorceress-skills-reload-check')
                        assert session.expect(r'sorceress-skills-reload-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('enchant-reload-check')
                        assert session.expect(r'enchant-reload-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('fire-ground-reload-check')
                        start = session.expect(r'fire-ground-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'fire-ground-reload-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('meteor-reload-check')
                        start = session.expect(r'meteor-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'meteor-reload-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('blizzard-reload-check')
                        start = session.expect(r'blizzard-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'blizzard-reload-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('frozen-orb-reload-check')
                        start = session.expect(r'frozen-orb-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'frozen-orb-reload-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('hydra-reload-check')
                        start = session.expect(r'hydra-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'hydra-reload-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('inferno-primitives-reload-check')
                        start = session.expect(r'inferno-primitives-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'inferno-primitives-reload-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('chain-lightning-reload-check')
                        start = session.expect(r'chain-lightning-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'chain-lightning-reload-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-aura-reload-check')
                        start = session.expect(r'paladin-aura-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-aura-reload-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-resistance-reload-check')
                        start = session.expect(r'paladin-resistance-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-resistance-reload-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-travel-reload-check')
                        start = session.expect(r'paladin-travel-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-travel-reload-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-offense-reload-check')
                        start = session.expect(r'paladin-offense-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-offense-reload-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-element-reload-check')
                        start = session.expect(r'paladin-element-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-element-reload-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('holy-bolt-reload-check')
                        start = session.expect(r'holy-bolt-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'holy-bolt-reload-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('heaven-fist-reload-check')
                        start = session.expect(r'heaven-fist-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'heaven-fist-reload-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('blessed-hammer-reload-check')
                        start = session.expect(r'blessed-hammer-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'blessed-hammer-reload-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-thorns-reload-check')
                        start = session.expect(r'paladin-thorns-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-thorns-reload-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-conviction-reload-check')
                        start = session.expect(r'paladin-conviction-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-conviction-reload-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('holy-shield-reload-check')
                        start = session.expect(r'holy-shield-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'holy-shield-reload-result: (passed|failed)',timeout=14).group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('vengeance-reload-check')
                        assert session.expect(r'vengeance-reload-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('sacrifice-reload-check')
                        assert session.expect(r'sacrifice-reload-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('smite-reload-check')
                        assert session.expect(r'smite-reload-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('zeal-reload-check')
                        assert session.expect(r'zeal-reload-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('charge-reload-check')
                        assert session.expect(r'charge-reload-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('charge-motion-check')
                        assert session.expect(r'charge-motion-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('damage-rounding-reload-check')
                        assert session.expect(r'damage-rounding-reload-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('resource-load-reload-check')
                        assert session.expect(r'resource-load-reload-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('paladin-resource-reload-check')
                        start = session.expect(r'paladin-resource-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'paladin-resource-reload-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('bone-barriers-reload-check')
                        assert session.expect(r'bone-barriers-reload-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        session.send('thunderstorm-reload-check')
                        start = session.expect(r'thunderstorm-reload-(check|result): (passed|failed)')
                        assert start.group(2) == 'passed', logpath.read_text()[-5000:]
                        if start.group(1) == 'check':
                            assert session.expect(r'thunderstorm-reload-result: (passed|failed)',timeout=8).group(1) == 'passed', logpath.read_text()[-5000:]
                    if boot == 0 and os.environ.get('SANCTUARY_FIRST_LOOP_AUDIT'):
                        session.send('first-loop-catalogue-check')
                        assert session.expect(r'first-loop-catalogue-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                    if os.environ.get('SANCTUARY_TEST_NAMED_GEAR') == '1':
                        named = 'named-gear-check' if boot == 0 else 'named-gear-reload-check'
                        session.send(named)
                        assert session.expect(named+r': (passed|failed)', timeout=120).group(1) == 'passed', logpath.read_text()[-6000:]
                    names = 'class-names-check' if boot == 0 else 'class-names-reload-check'
                    session.send(names)
                    assert session.expect(names+r': (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                    if boot == 0:
                        session.send('class-skills-projectiles-check')
                        assert session.expect(r'class-skills-projectiles-result: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                    if boot==0:
                        session.send('sanctuary-aura-check')
                        assert session.expect(r'sanctuary-aura-check: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                        assert session.expect(r'sanctuary-aura-result: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                        session.send('holy-freeze-check')
                        assert session.expect(r'holy-freeze-check: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                        assert session.expect(r'holy-freeze-result: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                        session.send('conversion-check')
                        assert session.expect(r'conversion-check: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                        assert session.expect(r'conversion-result: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                    else:
                        session.send('sanctuary-aura-reload-check')
                        assert session.expect(r'sanctuary-aura-reload-check: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                        assert session.expect(r'sanctuary-aura-reload-result: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                        session.send('holy-freeze-reload-check')
                        assert session.expect(r'holy-freeze-reload-check: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                        assert session.expect(r'holy-freeze-reload-result: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                        session.send('conversion-reload-check')
                        assert session.expect(r'conversion-reload-check: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                    passive_command='barbarian-passives-check' if boot==0 else 'barbarian-passives-reload-check'
                    session.send(passive_command)
                    assert session.expect(passive_command+r': (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                    if boot==0:
                        assert session.expect(r'barbarian-passives-result: (passed|failed)',timeout=9).group(1)=='passed',logpath.read_text()[-6000:]
                    mastery_command='barbarian-masteries-check' if boot==0 else 'barbarian-masteries-reload-check'
                    session.send(mastery_command)
                    assert session.expect(mastery_command+r': (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                    attack_command='barbarian-attacks-check' if boot==0 else 'barbarian-attacks-reload-check'
                    session.send(attack_command)
                    assert session.expect(attack_command+r': (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                    if boot==0:
                        assert session.expect(r'barbarian-attacks-result: (passed|failed)',timeout=15).group(1)=='passed',logpath.read_text()[-6000:]
                    # An actual disposable connection keeps the normal class combat loop active.
                    with socket.create_connection(('127.0.0.1',port),timeout=5) as concentrate_client:
                        threading.Thread(target=drain_fixture_client,args=(concentrate_client,),daemon=True).start()
                        time.sleep(.15)
                        concentrate_command='concentrate-check' if boot==0 else 'concentrate-reload-check'
                        session.send(concentrate_command)
                        assert session.expect(concentrate_command+r': (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                        assert session.expect(r'concentrate-result: (passed|failed)',timeout=8).group(1)=='passed',logpath.read_text()[-6000:]
                        session.send('save'); session.expect(r'Writing world save snapshot done')
                        concentrate_client.shutdown(socket.SHUT_RDWR)
                    time.sleep(.15)
                    session.send('concentrate-disconnect-check')
                    assert session.expect(r'concentrate-disconnect-check: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                    with socket.create_connection(('127.0.0.1',port),timeout=5) as berserk_client:
                        threading.Thread(target=drain_fixture_client,args=(berserk_client,),daemon=True).start()
                        time.sleep(.15)
                        berserk_command='berserk-check' if boot==0 else 'berserk-reload-check'
                        session.send(berserk_command)
                        assert session.expect(berserk_command+r': (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                        assert session.expect(r'berserk-result: (passed|failed)',timeout=10).group(1)=='passed',logpath.read_text()[-6000:]
                        session.send('save'); session.expect(r'Writing world save snapshot done')
                        berserk_client.shutdown(socket.SHUT_RDWR)
                    time.sleep(.15)
                    session.send('berserk-disconnect-check')
                    assert session.expect(r'berserk-disconnect-check: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                    basis_command='resource-basis-check' if boot==0 else 'resource-basis-reload-check'
                    session.send(basis_command)
                    assert session.expect(basis_command+r': (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                    shout_command='shout-check' if boot==0 else 'shout-reload-check'
                    session.send(shout_command)
                    assert session.expect(shout_command+r': (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                    assert session.expect(r'shout-result: (passed|failed)',timeout=8).group(1)=='passed',logpath.read_text()[-6000:]
                    session.send('save'); session.expect(r'Writing world save snapshot done')
                    orders_command='battle-orders-check' if boot==0 else 'battle-orders-reload-check'
                    session.send(orders_command)
                    assert session.expect(orders_command+r': (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
                    assert session.expect(r'battle-orders-result: (passed|failed)',timeout=8).group(1)=='passed',logpath.read_text()[-6000:]
                    session.send('save'); session.expect(r'Writing world save snapshot done')
                    session.send('shutdown'); session.process.wait(timeout=20)
                finally:
                    session.close()
    print('Native class skills and projectile checks passed, including two save/reload cycles.')


if __name__ == '__main__':
    check()
