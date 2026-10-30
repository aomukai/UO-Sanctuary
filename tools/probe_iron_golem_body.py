#!/usr/bin/env python3
"""Reuse complete source birth/graph composition with whole noRatio HP and no starting aura."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'iron-golem-body-source'
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(exist_ok=True)
    proof = ROOT / 'tools/reference/iron-golem-birth-graph-source-evidence.json'
    prior = json.loads(proof.read_text())
    source = BASE / 'iron-golem-birth-graph-source/source.cpp'
    assert sha(source) == prior['artifacts'][str(source.relative_to(ROOT))]
    cpp = source.read_text()
    # Change only declared fixture inputs, never a source routine.
    assert cpp.count('{76,50}') == cpp.count('*256+17;') == 1
    cpp = cpp.replace('{76,50}', '{76,0}').replace('*256+17;', '*256;')
    routines = prior['compiledUnchangedRoutines'] | prior['reusedCompiledUnchangedGraphRoutines']
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest, name
    path = STAGE / 'source.cpp'; path.write_text(cpp)
    with (STAGE / 'compiler.log').open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g1', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(STAGE / 'probe')], stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(STAGE / 'probe')], capture_output=True)
    (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    cases = json.loads(run.stdout); assert len(cases) == 2016
    (STAGE / 'cases.json').write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', cases=2016, legacyCommit=prior['legacyCommit'], compiledUnchangedRoutines=routines,
        inputs={str(path.relative_to(ROOT)): sha(path) for path in [proof, source, Path(__file__).resolve()]},
        artifacts={str(path.relative_to(ROOT)): sha(path) for path in [STAGE / name for name in ['source.cpp', 'cases.json', 'compiler.log', 'sanitizer.log']]},
        scope='Complete prior graph/base/passive/resistance and MAXHP callback composition,with fixture spawn HP306/595/980whole and zero initial life-percentage aura. All other declared services and bounds remain those of the predecessor. Raw spawn/item inputs are services,not a complete current engine initializer.',
        currentExecutableParity=False, learnedHandlerEnabled=False)
    target = ROOT / 'tools/reference/iron-golem-body-source-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if check: assert target.read_text() == encoded
    else: target.write_text(encoded)
    print('Iron Golem body recipe:2016 complete source compositions,whole supplied noRatio HP and no initial life aura;all source routines unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
