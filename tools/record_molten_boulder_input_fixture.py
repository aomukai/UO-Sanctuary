#!/usr/bin/env python3
"""Bind the reproduced moving-target test failure and forced-motion fixture checks."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/boulder-diagnostic'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inventory.STAGE=STAGE
    inputs=inventory.validate_inventory(STAGE/'class-inputs.json')
    # The diagnostic assembly was built from this copied project, not the
    # canonical project directory. Bind every copied C# input as well.
    copied={p.name:sha(p) for p in (STAGE/'project').glob('*.cs')}
    assert copied=={p.name:sha(p) for p in (ROOT/'tests/Sanctuary.Integration').glob('*.cs')}
    build=(STAGE/'fixed-build.log').read_text()
    assert all(x in build for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    log=(STAGE/'class-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('molten-boulder-check: passed')==1
    assert log.count('molten-boulder-reload-check: passed')==2
    assert log.count('molten-boulder-input-result: passed')==16
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    run=(STAGE/'run.log').read_text()
    assert run.count('Fixed fixture boot/trial')==run.count('passed')==16 and 'Traceback' not in run
    unfixed=(STAGE/'unfixed/class-native-checks.log').read_text()
    assert unfixed.count('molten-boulder-input-result: failed')==1
    assert 'damaged=False pushed=True mana=52736/52736 ready=True' in unfixed
    assert 'target=(6863, 3526, 0) life=2560000' in unfixed
    failed=ROOT/'.local/class-skills/druid-rabies/failed-full-boulder'
    assert 'Natural damage/knockback/once-only mana/lifetime' in (failed/'class-native-checks.log').read_text()
    # There was no content/runtime-data correction: only the input fixture changed.
    old=json.loads(subprocess.check_output(['git','show','9ea1312:tools/reference/druid-rabies-native-evidence.json'],cwd=ROOT))['inputInventory']
    assert old['copiedData']==inputs['copiedData']
    assert old['artifacts']['Assemblies/Sanctuary.Content.dll']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
    for name,digest in old['sourceInputs'].items():
        if name.startswith(('src/','shared/')):assert sha(ROOT/name)==digest,name
    artifacts=[STAGE/name for name in ['class-inputs.json','class-native-checks.log','run.log','run.py','fixed-build.log','unfixed/class-native-checks.log','unfixed/run.log','integration/Sanctuary.Integration.dll']]
    artifacts += [failed/name for name in ['class-inputs.json','class-native-checks.log','full-run.log']]
    artifacts += [ROOT/'tests/Sanctuary.Integration/MoltenBoulderChecks.cs',Path(__file__).resolve()]
    result=dict(referenceVersion='3.3.93847',gameplayChanged=False,installed=False,currentExecutableParity=False,
        verified=dict(actualQuickCasts=16,boots=3,actualReloads=2,saveSnapshots=3,passMarkers=35,zeroWarningBuild=True),
        inputInventory=inputs,copiedIntegrationSources=copied,artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        established=[
            'The diagnostic run reproduced the combined assertion failure once in six casts: mana and cooldown were correct, the target moved, but no damage occurred. Before projectile release, the skeleton had wandered two tiles off the aimed row. Its life remained unchanged while the projectile passed along the original row.',
            'Stopping AITimer before attaching the player does not isolate voluntary movement: native sector activation restarts the AI. The transient fixture now denies voluntary movement while permitting CombatDisplacement.Active Mobile.Move steps. Damage and actual outward movement remain required; ordinary AI motion cannot satisfy the knockback assertion.',
            'Sixteen real quick casts pass across two actual reloads, with the original physical/fire/contact/motion/lifetime/source comparison and saved-state checks retained. The fixture has a serialization constructor and versioned serialization because the world type index retains its type even after the temporary body is deleted.',
            'Content assembly, gameplay C# and runtime data match the focused-accepted Rabies candidate at9ea1312. The correction changes only the native test fixture; it does not alter collision, damage, resources, AI or knockback in gameplay.',
        ],remaining=[
            'The original full-run assertion did not expose its individual booleans; the diagnostic establishes a real test isolation defect but cannot reconstruct every value in that original failed cast. Full regression must pass with the corrected fixture before installation.',
            'This is bounded server input/forced-motion verification, not a certificate for current D2R geometry, timing, graphical effects, item-only/PvP or every possible recipient. Final effects and icons remain separate.',
        ])
    assert log.count(': passed')==35
    path=ROOT/'tools/reference/molten-boulder-input-isolation-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Boulder input fixture:16actual casts/3boots/2reloads; gameplay and content unchanged; full regression still separate.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
