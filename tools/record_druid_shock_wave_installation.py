#!/usr/bin/env python3
"""Verify the installed Shock Wave package, synchronized source and preserved original world/client."""
import argparse
import json
from pathlib import Path
import runpy
import subprocess
import sys
from install_druid_shock_wave import ROOT, BASE, STAGE, sha, hashes, generic_outputs


def record(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/record_druid_shock_wave_casting.py'), '--check', '--full-class'], cwd=ROOT, check=True)
    rawpath = BASE / 'druid-shock-wave-installation.json'
    receipt = json.loads(rawpath.read_text())
    assert receipt['installed'] and receipt['originalWorldSmoke'] and receipt['smokeReturnCode'] == 0
    assert receipt['handlers'] == 181 and receipt['druidHandlers'] == 25 and receipt['completedClasses'] == [] and not receipt['goalComplete']
    runtime = Path(receipt['runtimeRoot'])
    server, client = runtime / '.local/server', runtime / '.local/client'
    backup = runtime / receipt['backup']
    protected = receipt['protected']
    for relative, digest in receipt['sourceArtifacts'].items():
        assert sha(ROOT / relative) == digest, relative
    for relative, digest in receipt['installedArtifacts'].items():
        assert sha(runtime / relative) == digest, relative
    for relative,digest in receipt['verificationCache'].items():
        assert sha(runtime/relative)==digest,relative
    assert hashes(server / 'Saves') == protected['saves'] == hashes(backup / 'Saves')
    assert hashes(server / 'Configuration') == protected['configuration'] == hashes(backup / 'Configuration')
    assert hashes(client / 'Profiles') == protected['profiles']
    assert {path.name:sha(path) for path in client.glob('*.dll')} == protected['clientDlls']
    assert sha(client / 'settings.json') == protected['settingsSha256']
    assert sha(client / 'cuo.dll') == protected['clientSha256'] == sha(backup / 'client-cuo.dll')
    assert sha(server / 'Data/assemblies.json') == protected['assembliesSha256']
    assert generic_outputs(runtime) == protected['genericOutputs']
    inputs = json.loads((STAGE / 'class-inputs.json').read_text())
    for relative, digest in inputs['sourceInputs'].items():
        # Source synchronization follows smoke,so failed deployment can retain
        # the original main source as well as its restored binaries/data.
        assert sha(runtime / relative) == digest, ('runtime checkout source', relative)
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll', 'Assemblies/Sanctuary.Content.dll']:
        assert sha(server / name) == inputs['artifacts'][name], name
    for relative, digest in inputs['copiedData'].items():
        assert sha(server / relative) == digest, relative
        assert sha(runtime / 'data/Sanctuary' / relative.removeprefix('Data/Sanctuary/')) == digest, relative
    native = json.loads((ROOT / 'tools/reference/druid-shock-wave-casting-native-evidence.json').read_text())
    verified = native['verified']
    assert verified['passMarkers'] == 39 and verified['retainedDruidMarkers'] == 193 and verified['fullClassMarkers'] == 1184
    assert verified['boots'] == verified['fullClassBoots'] == 3 and verified['actualReloads'] == verified['fullClassReloads'] == 2
    assert verified['actualShockWaveInputs'] == 9 and verified['retainedMeleeInputs'] == 30
    ns = runpy.run_path(str(runtime / 'dev'))
    ns['require_stopped']('server')
    ns['require_stopped']('client')
    logpath = BASE / 'druid-shock-wave-installed-server.log'
    text = logpath.read_text()
    assert 'Loading world' in text and '2593' in text and '12000' in text and 'Shutting down' in text
    assert ' ERR]' not in text and ': failed' not in text and 'create the owner account now?' not in text
    result = dict(referenceVersion='3.3.93847', installed=True, handlers=181, druidHandlers=25, completedClasses=[], goalComplete=False,
        currentExecutableParity=False, graphicalClientAcceptance=False, clientUpdated=False, clientReusedByteIdentical=True,
        focused=verified, fullClassRegression=True, fullClassPassMarkers=1184, fullClassBoots=3, fullClassReloads=2,
        originalWorldSmoke=dict(returnCode=0, tcpPort=2593, udpPort=12000, serverStopped=True),
        runtimeRoot=str(runtime), originalMainSourceCommit=receipt['runtimeSourceBefore'],
        sourceMatchesVerifiedCandidate=True, runtimeDataMatchesVerifiedCandidate=True,
        preservedSaveFiles=len(protected['saves']), preservedConfigurationFiles=len(protected['configuration']),
        savesConfigProfilesSettingsUnchanged=True, clientDllsUnchanged=True, genericPackageLinksUnchanged=True,
        rollback=receipt['backup'],
        sourceArtifacts={**receipt['sourceArtifacts'], str(rawpath.relative_to(ROOT)):sha(rawpath),
            'tools/record_druid_shock_wave_installation.py':sha(Path(__file__).resolve())},
        installedArtifacts=receipt['installedArtifacts'],
        remaining=[*native['remaining'], 'The installed package adds no final assets or class-completion certificate. Original-world server smoke does not establish graphical playtesting.'],
        next='Continue remaining Druid handlers:Arctic Blast,Fury,Poison Creeper,Carrion Vine,Solar Creeper. Earlier class gaps and Warlock remain in scope.')
    path = ROOT / 'tools/reference/druid-shock-wave-installation-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'Shock Wave installed181/Druid25:39focused/193retained/1184full,original-world smoke0,{len(protected["saves"])}save/{len(protected["configuration"])}configuration files and client preserved; source/data synchronized, server stopped.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
