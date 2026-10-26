#!/usr/bin/env python3
"""Verify the exact tested wolf installation and preserved original world."""
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
    subprocess.run([sys.executable, str(ROOT / 'tools/record_spirit_wolf_casting.py'), '--check', '--full-class'], check=True)
    receipt_path = BASE / 'spirit-wolf-installation.json'
    receipt = json.loads(receipt_path.read_text())
    assert receipt['installed'] and receipt['smokeReturnCode'] == 0 and receipt['originalWorldSmoke']
    assert receipt['handlers'] == 167 and receipt['druidHandlers'] == 11
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
    native = json.loads((ROOT / 'tools/reference/spirit-wolf-casting-native-evidence.json').read_text())
    assert native['fullClassRegression'] and native['fullClassBoots'] == 3
    assert native['boots'] == 3 and native['actualReloads'] == 2
    assert native['connectedRightCasts'] == native['connectedQuickCasts'] == 3
    assert native['clientReusedByteIdentical'] and not native['graphicalClientAcceptance']
    package = json.loads((BASE / 'spirit-wolf/class-inputs.json').read_text())
    assert sha(ROOT / '.local/server/Assemblies/Sanctuary.Content.dll') == package['artifacts']['Assemblies/Sanctuary.Content.dll']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(ROOT / '.local/server' / name) == package['artifacts'][name], name
    for name, digest in package['copiedData'].items():
        assert sha(ROOT / '.local/server' / name) == digest, name
    ns = runpy.run_path(str(ROOT / 'dev'))
    ns['require_stopped']('server'); ns['require_stopped']('client')
    smoke = (BASE / 'spirit-wolf-installed-server.log').read_text()
    assert 'Loading world' in smoke and '2593' in smoke and '12000' in smoke and 'Shutting down' in smoke
    assert ' ERR]' not in smoke and 'create the owner account now?' not in smoke
    output = dict(referenceVersion='3.3.93847', installed=True, handlers=167, druidHandlers=11,
        fullClassRegression=True, fullClassPassedMarkers=native['fullClassPassedMarkers'],
        fullClassBoots=3, focusedBoots=3, actualReloads=2, focusedPassedMarkers=native['passedMarkers'],
        connectedRightCasts=3, connectedQuickCasts=3, pythonTests=70,
        completedClasses=[], goalComplete=False, currentExecutableParity=False,
        clientUpdated=False, clientReusedByteIdentical=True, graphicalClientAcceptance=False,
        preservedSaveFiles=len(before['saves']), preservedConfigurationFiles=len(before['configuration']),
        savesConfigProfilesSettingsUnchanged=True, clientDllsUnchanged=True,
        originalWorldSmoke=dict(returnCode=0, tcpPort=2593, udpPort=12000, serverStopped=True),
        rollback=receipt['backup'],
        hashes={str(receipt_path.relative_to(ROOT)): sha(receipt_path), **receipt['artifacts']},
        remaining=[
            'Cold-only physical-floor omission and busy-A1neutral-delay continuation remain explicitly provisional; exact current cadence, room/placement/path repair, cold sunder, recipient passive avoidance and graphical client acceptance remain unfinished.',
            'No class is complete. Final Spirit Wolf body/effects/audio and talent/right/quick icons remain separately pending named placeholders.'],
        next='Continue Poison Creeper from prepared stats/vine components; retain unresolved reference details and proceed with unaffected Druid skills.')
    path = ROOT / 'tools/reference/spirit-wolf-installation-evidence.json'
    encoded = json.dumps(output, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Spirit Wolf installed167/Druid11: exact tested package, original-world smoke, unchanged saves/config/client; no complete-class/parity claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
