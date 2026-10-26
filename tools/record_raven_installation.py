#!/usr/bin/env python3
"""Verify Raven's exact tested installation and the preserved existing world."""
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
    subprocess.run([sys.executable, str(ROOT / 'tools/record_raven_candidate.py'), '--check', '--full-class'], check=True)
    receipt = json.loads((BASE / 'raven-installation.json').read_text())
    assert receipt['installed'] and receipt['originalWorldSmoke'] and receipt['smokeReturnCode'] == 0
    assert receipt['handlers'] == 166 and receipt['druidHandlers'] == 10
    assert not receipt['goalComplete'] and not receipt['completedClasses']
    assert receipt['clientReused'] and not receipt['clientUpdated']
    for name, digest in receipt['artifacts'].items():
        assert sha(ROOT / name) == digest, name
    before = receipt['protected']
    backup = ROOT / receipt['backup']
    assert hashes(ROOT / '.local/server/Saves') == before['saves'] == hashes(backup / 'Saves')
    assert hashes(ROOT / '.local/server/Configuration') == before['configuration'] == hashes(backup / 'Configuration')
    assert hashes(ROOT / '.local/client/Profiles') == before['profiles']
    assert {path.name: sha(path) for path in (ROOT / '.local/client').glob('*.dll')} == before['clientDlls']
    for name, field in [('.local/server/Configuration/modernuo.json', 'configSha256'),
                        ('.local/client/settings.json', 'settingsSha256')]:
        assert sha(ROOT / name) == before[field], name
    assert sha(ROOT / '.local/client/cuo.dll') == before['clientSha256'] == receipt['clientAfterSha256']
    assert sha(BASE / 'raven/client/cuo.dll') == sha(backup / 'client-cuo.dll') == before['clientSha256']
    ns = runpy.run_path(str(ROOT / 'dev'))
    ns['require_stopped']('server'); ns['require_stopped']('client')
    native = json.loads((ROOT / 'tools/reference/raven-native-evidence.json').read_text())
    assert native['handlerEnabled'] and native['fullClassRegression']
    assert native['focusedBoots'] == 3 and native['actualReloads'] == 2
    assert native['connectedHeldRightCasts'] == native['connectedQuickCasts'] == 3
    output = dict(
        referenceVersion='3.3.93847', installed=True, handlers=166, druidHandlers=10,
        goalComplete=False, completedClasses=[], fullClassRegression=True,
        fullClassPassedMarkers=native['fullClassPassedMarkers'], boots=3, actualReloads=2,
        connectedNaturalActors=3, connectedHeldRightCasts=3, connectedQuickCasts=3,
        sourceComponentComparisons=37640, pythonTests=70, unchangedNamedDefinitions=573,
        namedAuditFingerprints=native['namedAuditFingerprints'], firstLoopFingerprints=native['firstLoopFingerprints'],
        clientUpdated=False, clientReusedByteIdentical=True, clientSha256=before['clientSha256'],
        originalWorldSmoke=dict(returnCode=0, tcpPort=2593, udpPort=12000, serverStopped=True),
        savesConfigProfilesSettingsUnchanged=True, clientDllsUnchanged=True,
        preservedSaveFiles=len(before['saves']), preservedConfigurationFiles=len(before['configuration']),
        rollback=receipt['backup'],
        hashes={'.local/class-skills/raven-installation.json': sha(BASE / 'raven-installation.json'), **receipt['artifacts']},
        scope='Separate post-installation receipt. The immutable candidate retains its pre-install scope; neither handler coverage nor these bounded checks certify complete current D2R skill/class parity.',
        remaining=[item for item in native['remaining'] if not item.startswith('Prepare exact-package installation')],
        next='Continue Poison Creeper from its current local/source preflight and rank-one tooltip corroboration. Resolve individual source/actor/mat gaps explicitly; final effects and icons remain separate.')
    path = ROOT / 'tools/reference/raven-installation-evidence.json'
    text = json.dumps(output, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Raven installed166/Druid10: exact tested package, original-world smoke, unchanged saves/config/profiles/settings/client DLLs; server stopped, goal remains open.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
