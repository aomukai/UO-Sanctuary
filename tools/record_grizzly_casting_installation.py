#!/usr/bin/env python3
"""Verify installed learned Grizzly content, original-world smoke and preserved saves/client."""
import argparse
import hashlib
import json
from pathlib import Path
import runpy
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def hashes(folder):
    return {str(path.relative_to(folder)): sha(path) for path in folder.rglob('*') if path.is_file()}


def record(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/record_grizzly_casting.py'), '--check', '--full-class'], check=True)
    receipt_path = BASE / 'grizzly-casting-installation.json'
    receipt = json.loads(receipt_path.read_text())
    assert receipt['installed'] and receipt['smokeReturnCode'] == 0 and receipt['originalWorldSmoke']
    assert receipt['handlers'] == 172 and receipt['druidHandlers'] == 16 and receipt['genericPackageLinksUnchanged']
    assert not receipt['goalComplete'] and receipt['completedClasses'] == []
    assert receipt['clientReused'] and not receipt['clientUpdated']
    before = receipt['protected']
    backup = ROOT / receipt['backup']
    for name, digest in receipt['artifacts'].items():
        assert sha(ROOT / name) == digest, name
    assert hashes(ROOT / '.local/server/Saves') == before['saves'] == hashes(backup / 'Saves')
    assert hashes(ROOT / '.local/server/Configuration') == before['configuration'] == hashes(backup / 'Configuration')
    assert hashes(ROOT / '.local/client/Profiles') == before['profiles']
    assert {p.name: sha(p) for p in (ROOT / '.local/client').glob('*.dll')} == before['clientDlls']
    assert sha(ROOT / '.local/client/settings.json') == before['settingsSha256']
    assert sha(ROOT / '.local/client/cuo.dll') == before['clientSha256'] == sha(backup / 'client-cuo.dll')
    generic = {kind: dict(target=str((BASE / kind).resolve()), symlink=(BASE / kind).is_symlink(), sha256=sha(BASE / kind / ('Sanctuary.' + kind.title() + '.dll'))) for kind in ['content', 'integration']}
    assert generic == before['genericOutputs']
    native_path = ROOT / 'tools/reference/grizzly-casting-native-evidence.json'
    native = json.loads(native_path.read_text())
    verified = native['verified']
    assert verified['fullClassMarkers'] == 1184 and verified['fullClassBoots'] == 3 and verified['fullClassReloads'] == 2 and verified['boots'] == 3 and verified['actualReloads'] == 2
    assert verified['rightCasts'] == verified['quickCasts'] == 3
    package = json.loads((BASE / 'grizzly-casting/class-inputs.json').read_text())
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll', 'Assemblies/Sanctuary.Content.dll']:
        assert sha(ROOT / '.local/server' / name) == package['artifacts'][name], name
    for name, digest in package['copiedData'].items():
        assert sha(ROOT / '.local/server' / name) == digest, name
    ns = runpy.run_path(str(ROOT / 'dev'))
    ns['require_stopped']('server'); ns['require_stopped']('client')
    smoke = (BASE / 'grizzly-casting-installed-server.log').read_text()
    assert 'Loading world' in smoke and '2593' in smoke and '12000' in smoke and 'Shutting down' in smoke
    assert ' ERR]' not in smoke and 'create the owner account now?' not in smoke
    output = dict(referenceVersion='3.3.93847', installed=True, handlers=172, druidHandlers=16,
        fullClassRegression=True, fullClassPassMarkers=verified['fullClassMarkers'], fullClassBoots=3, namedItemRegression=dict(markers=3,boots=3,actualReloads=2,identicalCandidateAssembliesAndData=True,sameWorldAsFull=True), focused=verified,
        completedClasses=[], goalComplete=False, currentExecutableParity=False,
        clientUpdated=False, clientReusedByteIdentical=True, graphicalClientAcceptance=False,
        preservedSaveFiles=len(before['saves']), preservedConfigurationFiles=len(before['configuration']),
        savesConfigProfilesSettingsUnchanged=True, clientDllsUnchanged=True, genericPackageLinksUnchanged=True,
        originalWorldSmoke=dict(returnCode=0, tcpPort=2593, udpPort=12000, serverStopped=True), rollback=receipt['backup'],
        hashes={str(receipt_path.relative_to(ROOT)): sha(receipt_path), **receipt['artifacts'],
            'tools/record_grizzly_casting_installation.py': sha(Path(__file__))},
        remaining=['The installed learned handler does not certify full class completion or current-executable parity.', *native['remaining'][:2], 'Graphical playtesting and current-executable equivalence are not established by this server installation.'],
        next='Continue unaffected Druid Werewolf/Werebear/Lycanthropy preparation. Poison Creeper retains individual modern underground exposure/attackability and provisional Rabies/gear capture gates; do not silently enable its prepared backend. Keep separate effects/icons and remaining mechanical acceptance explicit. No class is complete.')
    path = ROOT / 'tools/reference/grizzly-casting-installation-evidence.json'
    rendered = json.dumps(output, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Learned Grizzly installed172/Druid16: full/focused accepted,original-world smoke0,saves/config/client preserved; no completed class or parity certificate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
