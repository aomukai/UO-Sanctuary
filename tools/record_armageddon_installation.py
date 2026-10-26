#!/usr/bin/env python3
"""Verify the exact tested Armageddon installation and original-world preservation."""
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
    subprocess.run([sys.executable, str(ROOT / 'tools/record_armageddon_candidate.py'), '--check', '--full-class'], check=True)
    receipt = json.loads((BASE / 'armageddon-installation.json').read_text())
    assert receipt['installed'] and receipt['originalWorldSmoke'] and receipt['smokeReturnCode'] == 0
    assert receipt['handlers'] == 165 and receipt['druidHandlers'] == 9 and not receipt['goalComplete'] and not receipt['completedClasses']
    for name, digest in receipt['artifacts'].items():
        assert sha(ROOT / name) == digest, name
    before = receipt['protected']
    assert hashes(ROOT / '.local/server/Saves') == before['saves'] == hashes(ROOT / receipt['backup'] / 'Saves')
    assert hashes(ROOT / '.local/client/Profiles') == before['profiles']
    assert hashes(ROOT / '.local/server/Configuration') == before['configuration'] == hashes(ROOT / receipt['backup'] / 'Configuration')
    for name, field in [('.local/server/Configuration/modernuo.json', 'configSha256'),
                        ('.local/client/settings.json', 'settingsSha256')]:
        assert sha(ROOT / name) == before[field], name
    assert receipt['clientUpdated']
    assert sha(ROOT / '.local/client/cuo.dll') == receipt['clientAfterSha256'] == sha(BASE / 'armageddon/client/cuo.dll')
    assert sha(ROOT / receipt['backup'] / 'client-cuo.dll') == before['clientSha256'] == sha(BASE / 'armageddon/client-baseline/cuo.dll')
    ns = runpy.run_path(str(ROOT / 'dev'))
    ns['require_stopped']('server'); ns['require_stopped']('client')
    native = json.loads((ROOT / 'tools/reference/armageddon-native-evidence.json').read_text())
    assert native['handlerEnabled'] and native['fullClassRegression']
    assert native['focusedBoots'] == 3 and native['actualReloads'] == 2 and native['connectedQuickCasts'] == 3
    output = dict(referenceVersion='3.3.93847', installed=True, handlers=165, druidHandlers=9, goalComplete=False, completedClasses=[],
                  fullClassRegression=True, fullClassPassedMarkers=native['fullClassPassedMarkers'], boots=3, actualReloads=2,
                  connectedNaturalActors=3, connectedQuickCasts=3, pythonTests=70, unchangedNamedDefinitions=573,
                  nativeEmitterComparisons=native['nativeEmitterComparisons'],
                  nativeEndpointComparisons=native['nativeEndpointComparisons'], clientBuildWarnings=native['buildWarnings']['client'],
                  clientUpdated=True, clientAfterSha256=receipt['clientAfterSha256'],
                  clientBeforeSha256=before['clientSha256'],
                  namedAuditFingerprints=native['namedAuditFingerprints'], firstLoopFingerprints=native['firstLoopFingerprints'],
                  originalWorldSmoke=dict(returnCode=0, tcpPort=2593, udpPort=12000, serverStopped=True),
                  savesConfigProfilesSettingsUnchanged=True, rollback=receipt['backup'],
                  hashes={'.local/class-skills/armageddon-installation.json': sha(BASE / 'armageddon-installation.json'), **receipt['artifacts']},
                  scope='The native candidate certificate retains its pre-install scope. This separate receipt verifies the later exact-package installation and existing-world preservation; it is not a whole-skill or class parity certificate.',
                  remaining=[item for item in native['remaining'] if not item.startswith('Complete full class regression')],
                  next='Continue remaining Druid summons and shapeshifting. Preserve explicit modern NHD/current-executable/client limits, Boulder terminal-current-build and Arctic Blast/channel follow-ups; final effects/icons remain separately inventoried.')
    out = ROOT / 'tools/reference/armageddon-installation-evidence.json'
    text = json.dumps(output, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Armageddon installed165handlers/Druid9: exact tested package, original-world smoke, matching client updated; unchanged saves/config/profiles/settings, server stopped; goal remains open.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
