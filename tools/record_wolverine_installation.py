#!/usr/bin/env python3
"""Verify the tested Wolverine installation and preserved original world/client."""
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
    subprocess.run([sys.executable, str(ROOT / 'tools/record_wolverine_candidate.py'), '--check', '--full-class'], check=True)
    receipt_path = BASE / 'wolverine-installation.json'
    receipt = json.loads(receipt_path.read_text())
    assert receipt['installed'] and receipt['smokeReturnCode'] == 0 and receipt['originalWorldSmoke']
    assert receipt['handlers'] == 169 and receipt['druidHandlers'] == 13 and receipt['genericPackageLinksUnchanged']
    assert not receipt['goalComplete'] and receipt['completedClasses'] == []
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
    native_path = ROOT / 'tools/reference/wolverine-native-evidence.json'
    native = json.loads(native_path.read_text())
    assert native['fullClassRegression'] and native['fullClassBoots'] == 3
    assert native['verified']['boots'] == 3 and native['verified']['actualReloads'] == 2
    assert native['verified']['connectedRightCasts'] == native['verified']['connectedQuickCasts'] == 3
    assert native['clientReusedByteIdentical'] and not native['graphicalClientAcceptance']
    package = json.loads((BASE / 'wolverine/class-inputs.json').read_text())
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll', 'Assemblies/Sanctuary.Content.dll']:
        assert sha(ROOT / '.local/server' / name) == package['artifacts'][name], name
    for name, digest in package['copiedData'].items():
        assert sha(ROOT / '.local/server' / name) == digest, name
    ns = runpy.run_path(str(ROOT / 'dev'))
    ns['require_stopped']('server'); ns['require_stopped']('client')
    smoke = (BASE / 'wolverine-installed-server.log').read_text()
    assert 'Loading world' in smoke and '2593' in smoke and '12000' in smoke and 'Shutting down' in smoke
    assert ' ERR]' not in smoke and 'create the owner account now?' not in smoke
    output = dict(referenceVersion='3.3.93847', installed=True, handlers=169, druidHandlers=13,
        fullClassRegression=True, fullClassPassMarkers=native['fullClassPassMarkers'],
        fullClassBoots=3, focused=native['verified'], completedClasses=[], goalComplete=False,
        currentExecutableParity=False, clientUpdated=False, clientReusedByteIdentical=True, graphicalClientAcceptance=False,
        preservedSaveFiles=len(before['saves']), preservedConfigurationFiles=len(before['configuration']),
        savesConfigProfilesSettingsUnchanged=True, clientDllsUnchanged=True, genericPackageLinksUnchanged=True,
        originalWorldSmoke=dict(returnCode=0, tcpPort=2593, udpPort=12000, serverStopped=True),
        rollback=receipt['backup'], hashes={str(receipt_path.relative_to(ROOT)): sha(receipt_path), **receipt['artifacts']},
        remaining=native['remaining'],
        next='Continue Spirit of Barbs using its current preflight; verify flat-return aggregation and event order before connecting the new aura. Keep individually deferred Creeper and exact modern timing/geometry/client branches visible.')
    path = ROOT / 'tools/reference/wolverine-installation-evidence.json'
    encoded = json.dumps(output, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Wolverine installed169/Druid13: tested package, original-world smoke, unchanged saves/config/client; no complete class or parity certificate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
