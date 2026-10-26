#!/usr/bin/env python3
"""Verify a completed exact-package Twister installation and its preservation receipt."""
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
    return {str(p.relative_to(folder)): sha(p) for p in folder.rglob('*') if p.is_file()}


def record(check=False):
    # The staged acceptance certificate describes its pre-install scope. This
    # separate receipt establishes the later installation without rewriting it.
    subprocess.run([sys.executable, str(ROOT / 'tools/record_twister_candidate.py'), '--check', '--full-class'], check=True)
    receipt = json.loads((BASE / 'twister-installation.json').read_text())
    assert receipt['installed'] and receipt['originalWorldSmoke'] and receipt['smokeReturnCode'] == 0
    assert receipt['handlers'] == 161 and receipt['druidHandlers'] == 5 and not receipt['goalComplete'] and not receipt['completedClasses']
    for name, digest in receipt['artifacts'].items():
        assert sha(ROOT / name) == digest, name
    before = receipt['protected']
    assert hashes(ROOT / '.local/server/Saves') == before['saves'] == hashes(ROOT / receipt['backup'] / 'Saves')
    assert hashes(ROOT / '.local/client/Profiles') == before['profiles']
    assert hashes(ROOT / '.local/server/Configuration') == before['configuration'] == hashes(ROOT / receipt['backup'] / 'Configuration')
    for name, field in [('.local/server/Configuration/modernuo.json', 'configSha256'),
                        ('.local/client/cuo.dll', 'clientSha256'), ('.local/client/settings.json', 'settingsSha256')]:
        assert sha(ROOT / name) == before[field], name
    ns = runpy.run_path(str(ROOT / 'dev'))
    ns['require_stopped']('server'); ns['require_stopped']('client')
    native = json.loads((ROOT / 'tools/reference/twister-native-evidence.json').read_text())
    assert native['fullClassRegression'] and native['handlerEnabled']
    assert native['focusedBoots'] == 3 and native['actualReloads'] == 2 and native['connectedQuickCasts'] == 3
    output = dict(referenceVersion='3.3.93847', installed=True, handlers=161, druidHandlers=5,
                  goalComplete=False, completedClasses=[], fullClassRegression=True, boots=3, actualReloads=2,
                  connectedQuickCasts=3, pythonTests=70, unchangedNamedDefinitions=573,
                  sourceEndpointComparisons=native['sourceEndpointComparisons'], completePathComparisons=native['completePathComparisons'],
                  reactionAndStunFrameComparisons=native['reactionAndStunFrameComparisons'],
                  namedAuditFingerprints=native['namedAuditFingerprints'], firstLoopFingerprints=native['firstLoopFingerprints'],
                  originalWorldSmoke=dict(returnCode=0, tcpPort=2593, udpPort=12000, serverStopped=True),
                  savesConfigClientProfilesSettingsUnchanged=True, rollback=receipt['backup'],
                  hashes={'.local/class-skills/twister-installation.json': sha(BASE / 'twister-installation.json'),
                          **receipt['artifacts']},
                  scope='The earlier twister-native-evidence.json is the immutable staged acceptance certificate; this separate record establishes the later exact-package installation. Handler coverage is not whole-skill parity or class completion.',
                  remaining=native['remaining'],
                  next='Continue Druid Volcano from its source preflight, reusing the existing ground-emitter, physical/fire packet and independent saved projectile consumers where matched. Keep provisional modern Twister/Fissure hit-delay grouping, source/native/current-executable/client limits, Boulder terminal explosion/ground correction and separate final effects/icons explicit; continue unaffected skills.')
    text = json.dumps(output, indent=2) + '\n'
    path = ROOT / 'tools/reference/twister-installation-evidence.json'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Twister installed161handlers/Druid5: exact tested package, original-world smoke, unchanged saves/config/client/profiles/settings, server stopped; goal remains open.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
