#!/usr/bin/env python3
"""Correct the declared form probe's CAST ABI from the pinned player-mode header."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    predecessor_path = ROOT / 'tools/reference/druid-forms-engine-evidence.json'
    predecessor = json.loads(predecessor_path.read_text())
    original = BASE / 'druid-forms-source-probe.cpp'
    assert sha(original) == predecessor['generatedSourceSha256']
    relative = 'source/D2Common/include/Units/Player.h'
    header = BASE / 'd2moo-pinned-source' / relative
    raw = header.read_bytes()
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == predecessor['legacyCommit']
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(row['sha'] for row in tree['tree'] if row['path'] == relative)
    enum = raw.decode().split('enum D2C_PlayerModes', 1)[1].split('};', 1)[0]
    modes = re.findall(r'\bPLRMODE_\w+', enum)
    assert modes.index('PLRMODE_CAST') == 10 and modes.index('PLRMODE_ATTACK1') == 7
    cpp = original.read_text()
    for name, expected in predecessor['sourceRoutineHashes'].items():
        body = function(cpp, name)
        assert hashlib.sha256(body.encode()).hexdigest() == expected
    for before, after in [('PLRMODE_CAST=7', 'PLRMODE_CAST=10'), ('int mode=7,nCharges=0', 'int mode=10,nCharges=0')]:
        assert cpp.count(before) == 1, before
        cpp = cpp.replace(before, after, 1)
    for name, expected in predecessor['sourceRoutineHashes'].items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == expected
    generated = BASE / 'druid-form-modes-source-probe.cpp';generated.write_text(cpp)
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(generated.with_suffix(''))], check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    log = BASE / 'druid-form-modes-sanitizer.log';log.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()
    cases = json.loads(process.stdout)
    assert len(cases) == 1000 and all(row[10] == 10 for row in cases)
    assert sum(row[3] == 0 and row[4] not in [3, 4, 6] for row in cases) == 350
    fixture = BASE / 'druid-form-modes-source-probe.json';fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', sourcePreparation=True, nativeAccepted=False, legacyCommit=tree['sha'], contexts=1000, supportedPlayerContexts=350,
        header=dict(path=relative, sha256=sha(header), playerCast=10, playerAttack1=7),
        predecessor=dict(path=str(predecessor_path.relative_to(ROOT)), sha256=sha(predecessor_path), generatedSourceSha256=predecessor['generatedSourceSha256']),
        completeRoutineHashes=predecessor['sourceRoutineHashes'], generatedSourceSha256=sha(generated),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)), sanitizerLogSha256=sha(log),
        established=[
            'Pinned Player.h assigns player CAST10and ATTACK1 7. The older declared fixture used7as CAST; this separate probe corrects only that ABI constant and the declared initial cast mode. All seven complete legacy routine bodies retain their predecessor hashes.',
            'All1000contexts execute under ASan/UBSan;350supported player cases retain the same state/stat/allocation/expiry contracts with header-backed mode10. Skill presence still controls whether a mode-write event occurs; equal numeric input/output is not evidence that the callback ran.',
        ], remaining=[
            'This prepares the native unused mode-result correction. It does not change the immutable earlier packages,drive a client animation or establish current cross-form transitions. The saved world component did not consume this mode result.',
            'Allocation,event,formula and conversion services retain the predecessor declared scope. Current mode/sequence/rate/input/event integration still requires its own consumers and acceptance.',
        ])
    output = ROOT / 'tools/reference/druid-form-modes-engine-evidence.json';rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Druid form modes:headerCAST10/ATTACK1 7,1000complete source contexts/350supported players,ASan/UBSan clean; no live animation consumer.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
