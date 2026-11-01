#!/usr/bin/env python3
"""Review player do17 dependencies; reuse existing complete initializer/path proofs."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.local/class-skills/d2moo-pinned-source/source/D2Game/src/SKILLS/SkillSor.cpp'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def block(text, name):
    start = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
    assert start, name
    end = text.index('{', start.start()) + 1
    depth = 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start.start():end]


def review(check=False):
    tree = json.loads((ROOT / '.local/class-skills/d2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    raw = SOURCE.read_bytes()
    relative = 'source/D2Game/src/SKILLS/SkillSor.cpp'
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    text = raw.decode()
    initializer = block(text, 'SKILLS_MissileInit_ChargedBolt')
    emitter = block(text, 'SKILLS_SrvDo017_ChargedBolt_BoltSentry')
    strike_path = ROOT / 'tools/reference/charged-strike-engine-evidence.json'
    strike = json.loads(strike_path.read_text())
    assert strike['sourceHashes'][relative] == sha(SOURCE)
    assert strike['blockHashes']['SKILLS_MissileInit_ChargedBolt'] == hashlib.sha256(initializer.encode()).hexdigest()
    assert strike['probe']['initializerRows'] == 45
    old_fixture = ROOT / strike['probe']['fixture']
    initializers = json.loads(old_fixture.read_text())['init']
    assert len(initializers) == 45
    for frames, aim, ordinal, total, current, seed, kind, distance, recomputed in initializers:
        assert total == current == min(frames, 77)
        assert (seed, kind, distance, recomputed) == (aim + ordinal, 7, min(frames, 77), 1)
    path_reference = ROOT / 'tools/reference/charged-bolt-path-source-evidence.json'
    paths = json.loads(path_reference.read_text())
    assert paths['legacyCommit'] == tree['sha'] and paths['cases'] == 2160
    for name, digest in paths['sourceHashes'].items():
        assert sha(ROOT / '.local/class-skills/d2moo-pinned-source' / name) == digest, name
    assert sha(ROOT / paths['fixture']['path']) == paths['fixture']['sha256']
    skill_path = ROOT / 'tools/reference/d2r-named-equipment/json/skills.json'
    skill = json.loads(skill_path.read_text())['38']
    assert skill['skill'] == 'Charged Bolt' and skill['srvdofunc'] == 17 and skill['srvmissilea'] == 'chargedbolt'
    assert skill['Param1'] == 3 and skill['Param2'] == 1
    for token in ['missileParams.dwFlags = 0x21;', 'missileParams.pOwner = pUnit;', 'missileParams.pInitFunc = SKILLS_MissileInit_ChargedBolt;', 'missileParams.pInitArgs = i;', 'missileParams.nTargetX && missileParams.nTargetY']:
        assert token in emitter
    inputs = [SOURCE, strike_path, old_fixture, path_reference, ROOT / paths['fixture']['path'], skill_path, Path(__file__).resolve()]
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], reviewKind='Read-only complete-body review and reuse identity checks; no new compiled emitter or modern executable proof.',
                  sourceBodyHashes={'SKILLS_SrvDo017_ChargedBolt_BoltSentry': hashlib.sha256(emitter.encode()).hexdigest(), 'SKILLS_MissileInit_ChargedBolt': hashlib.sha256(initializer.encode()).hexdigest()},
                  reused=dict(initializerCases=45, initializerReceipt=str(strike_path.relative_to(ROOT)), pathCases=2160, pathReceipt=str(path_reference.relative_to(ROOT))),
                  pinnedCount=dict(formula=skill['calc1'], first=skill['Param1'], increment=skill['Param2'], maximum=24),
                  inputHashes={str(p.relative_to(ROOT)): sha(p) for p in inputs},
                  established=['The complete older do17 body sets flags33, owner/origin/skill/rank and the shared initializer, then queries aim once per ordinal. Failed or zero-coordinate targets skip that attempt; missile creation failure does not abort remaining attempts.',
                               'The exact initializer body is already compiled in the existing45-case proof. It caps total/current frames at77 when initial range is78 or more, seeds from target-path-X plus ordinal, selects charged path and recomputes it. Preserve this dependency despite the current missile Range98.',
                               'Existing2160-case complete heading/seed/path evidence and native sentry/Mage helpers should be reused. Their declared geometry/path adapters are not a complete modern room/movement proof.'],
                  remaining=['Compile do17 emission with declared formula/target/factory services before claiming its event-order parity. This review does not execute that emitter or its factory.',
                             'Player capture, saved charged motion/deep-copy validation, actual input/payment, collision and restart acceptance remain unfinished. Global RNG allocation, initial dispatch/target projection, room/path rebuild, special recipients and client/assets remain explicit limits.'])
    output = ROOT / 'tools/reference/player-charged-bolt-reuse-review.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Player Charged Bolt: do17 reviewed; exact initializer45/path2160 evidence reused; emission/native acceptance still separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    review(parser.parse_args().check)
