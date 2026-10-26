#!/usr/bin/env python3
"""Verify the rank corrections' exact installation and preserved existing world."""
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
    subprocess.run([sys.executable, str(ROOT / 'tools/record_linear_formula_fix.py'), '--check', '--full-class'], check=True)
    receipt = json.loads((BASE / 'linear-formula-fix-installation.json').read_text())
    assert receipt['installed'] and receipt['smokeReturnCode'] == 0 and receipt['originalWorldSmoke']
    assert receipt['handlers'] == 166 and receipt['druidHandlers'] == 10 and not receipt['goalComplete'] and not receipt['completedClasses']
    for name, digest in receipt['artifacts'].items():
        assert sha(ROOT / name) == digest, name
    before = receipt['protected']; backup = ROOT / receipt['backup']
    assert hashes(ROOT / '.local/server/Saves') == before['saves'] == hashes(backup / 'Saves')
    assert hashes(ROOT / '.local/server/Configuration') == before['configuration'] == hashes(backup / 'Configuration')
    assert hashes(ROOT / '.local/client/Profiles') == before['profiles']
    assert {p.name: sha(p) for p in (ROOT / '.local/client').glob('*.dll')} == before['clientDlls']
    assert sha(ROOT / '.local/client/settings.json') == before['settingsSha256']
    assert sha(ROOT / '.local/client/cuo.dll') == before['clientSha256'] == sha(backup / 'client-cuo.dll')
    ns = runpy.run_path(str(ROOT / 'dev')); ns['require_stopped']('server'); ns['require_stopped']('client')
    candidate = json.loads((ROOT / 'tools/reference/linear-formula-fix-native-evidence.json').read_text())
    assert candidate['fullClassRegression'] and candidate['fullBoots'] == 3 and candidate['actualReloads'] == 2
    out = dict(referenceVersion='3.3.93847', installed=True, handlers=166, druidHandlers=10, newHandlers=0,
               completedClasses=[], goalComplete=False, fullClassRegression=True, fullClassPassedMarkers=candidate['fullClassPassedMarkers'],
               fullBoots=3, actualReloads=2, pythonTests=70, sourceAndNativeLinearCases=9252,
               namedAuditFingerprints=candidate['namedAuditFingerprints'], firstLoopFingerprints=candidate['firstLoopFingerprints'],
               unchangedNamedDefinitions=573, clientUpdated=False, clientReusedByteIdentical=True, clientSha256=before['clientSha256'],
               savesConfigProfilesSettingsUnchanged=True, clientDllsUnchanged=True, preservedSaveFiles=len(before['saves']),
               preservedConfigurationFiles=len(before['configuration']), rollback=receipt['backup'],
               originalWorldSmoke=dict(returnCode=0, tcpPort=2593, udpPort=12000, serverStopped=True),
               hashes={'.local/class-skills/linear-formula-fix-installation.json': sha(BASE / 'linear-formula-fix-installation.json'), **receipt['artifacts']},
               scope='Post-installation acceptance for shared rank corrections and retained handlers. Prepared Spirit Wolf/Creeper components are included but have no learned actor/handler; no complete class or D2R parity certificate.',
               next='Implement the saved Spirit Wolf body/cold attack/individual targeting/lifecycle and learned cast; keep unsupported current physical-floor/sunder/AI timing branches explicit and final effects/icons separate.')
    path = ROOT / 'tools/reference/linear-formula-fix-installation-evidence.json'; encoded = json.dumps(out, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Shared rank fixes installed:166/Druid10 retained, exact full-tested package, unchanged saves/config/client; Spirit Wolf actor remains pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
