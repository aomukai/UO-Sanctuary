#!/usr/bin/env python3
"""Pin owned BH/A2 and execute the unchanged NPC rate writer and scheduler."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/tainted-fireball-cast-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(exist_ok=True)
    inputs = {}
    def pin(path):
        inputs[str(path.relative_to(ROOT))] = sha(path)
        return path.read_text()
    tree = json.loads(pin(ROOT / '.local/class-skills/d2moo-source-tree.json'))
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    def source(relative):
        path = ROOT / '.local/class-skills/d2moo-pinned-source' / relative; raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(r['sha'] for r in tree['tree'] if r['path'] == relative)
        return pin(path)
    owned = json.loads(pin(ROOT / 'tools/reference/d2r-local-sentry-model-evidence.json'))
    tables = {}
    for key in ('monstats', 'monstats2'):
        entry = owned['files'][key]; path = ROOT / entry['path']; assert sha(path) == entry['sha256']
        tables[key] = next(r for r in csv.DictReader(pin(path).lstrip('\ufeff').splitlines(), delimiter='\t') if r['Id'] == 'warbighead')
    monster, model = tables['monstats'], tables['monstats2']
    assert monster['Code'] == 'BH' and monster['Skill1'] == 'Tainted Fire Ball' and monster['Sk1mode'] == 'A2'
    assert model['BaseW'] == 'hth' and model['mA2'] == '1' and not model['A2mv']
    raw = (ROOT / '.local/research/d2r-3.3.93847/AnimData.d2').read_bytes()
    assert hashlib.sha256(raw).hexdigest() == '61ea70ede691e88a4bf375d872668d74d6cdca4ed5235034188ed1eb0d1e8ee5'
    inputs['.local/research/d2r-3.3.93847/AnimData.d2'] = hashlib.sha256(raw).hexdigest()
    pos = 0; animation = None
    for bucket in range(256):
        count = struct.unpack_from('<I', raw, pos)[0]; pos += 4
        for _ in range(count):
            row = raw[pos:pos + 160]; pos += 160
            if row[:8].split(b'\0')[0] == b'BHA2HTH':
                frames, speed = struct.unpack_from('<ii', row, 8)
                animation = dict(code='BHA2HTH', frames=frames, speed=speed, events=[[i, b] for i, b in enumerate(row[16:]) if b])
    assert pos == len(raw) and animation == dict(code='BHA2HTH', frames=10, speed=256, events=[[6, 2]])
    units = source('source/D2Common/src/Units/Units.cpp'); game = source('source/D2Game/src/UNIT/SUnit.cpp')
    ids = source('source/D2Common/include/DataTbls/MonsterIds.h')
    parent_path = ROOT / 'tools/probe_spirit_wolf_rate_reference.py'; parent = pin(parent_path)
    # Reuse its declared ABI/services, not rewritten source function bodies.
    prefix = parent.split("    cpp=r'''", 1)[1].split("'''+ids", 1)[0]
    cpp = prefix + ids.replace('#pragma once', '') + '\n'; routines = {}
    for name in ('gaPlayerModesAnimModulators', 'gaMonModesAnimModulators', 'gaMonModesAnimModulatorsExpansion'):
        match = re.search(r'^(?:static )?D2AnimUnkStrc ' + name + r'\[[^]]+\]\s*=\s*\{', units, re.M); assert match
        block = units[match.start():units.index('};', match.start()) + 2]
        cpp += block + '\n'; routines[name] = hashlib.sha256(block.encode()).hexdigest()
    for text, name in [(units, 'UNITS_CanAnimModeUseAttackRate'), (units, 'UNITS_UpdateAttackAnimRateAndVelocity'), (game, 'sub_6FCBCE70')]:
        block = function(text, name); cpp += block + '\n'; routines[name] = hashlib.sha256(block.encode()).hexdigest()
    cpp += r'''
int main(){printf("[");bool first=true;
for(int rate=-100;rate<=300;rate++){
owner={};anim={};owner.dwClassId=743;owner.dwAnimMode=5;owner.dwFrameCountPrecise=10*256;
anim.dwAnimSpeed=256;anim.pFrameFlags[6]=2;faster=0;attack=rate;meleeOnly=0;alternate=0;merges=0;events.clear();
assert(UNITS_CanAnimModeUseAttackRate(UNIT_MONSTER,5,&owner,743));
UNITS_UpdateAttackAnimRateAndVelocity(&owner);sub_6FCBCE70(&game,&owner);
printf("%s[%d,%d,%d,[",first?"":",",rate,owner.wAnimSpeed,merges);first=false;
bool sep=false;for(auto e:events){printf("%s[%d,%d,%d,%d]",sep?",":"",e.type,e.frame-1000,e.kind,e.param);sep=true;}printf("]]");}
printf("]\n");}
'''
    path = STAGE / 'probe.cpp'; path.write_text(cpp); binary = STAGE / 'probe'
    compiler = subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], capture_output=True)
    (STAGE / 'compiler.log').write_bytes(compiler.stdout + compiler.stderr); assert compiler.returncode == 0
    run = subprocess.run([str(binary)], capture_output=True); (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    rows = json.loads(run.stdout); assert len(rows) == 401
    for rate, speed, merges, events in rows:
        assert speed == 256 * max(15, min(175, rate)) // 100 and merges == 1
        assert len(events) == 2 and events[0][0] == 1 and events[0][2] == 2 and events[1][0] == 2
    fixture = STAGE / 'cases.json'; fixture.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    clock = [dict(Skill=422, Mode=5, Frames=10, Speed=256, Action=6, Flag=2)]
    runtime = ROOT / 'data/Sanctuary/Balance/tainted-fireball-cast.json'; encoded = json.dumps(clock, indent=2) + '\n'
    if check:
        assert runtime.read_text() == encoded
    else:
        runtime.write_text(encoded)
    pin(Path(__file__).resolve())
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], inputs=inputs, animation=animation,
        compiledUnchangedRoutineHashes=routines, fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=401),
        generatedSourceSha256=sha(path), currentExecutableParity=False, handlerEnabled=False,
        established=['Owned warbighead selects NPC422 through A2, not SC. Owned BHA2HTH has10frames,base256 and one kind2event at6.',
            'The unchanged A2 mode predicate, nonweapon attack-rate writer and initial event scheduler run together for401 total attack rates, including negative slows and15..175 clamping. No owner weapon IAS or FCR is supplied.',
            'SourceAnimationEvents and SourceAttackRate are reused; no new timing algorithm is introduced. Native checks must prove actual scheduled once-only release into the existing saved NPC missile.'],
        remaining=['Current mode initialization/admission/owner and live skill lookup services, complete GenericPet world/controller selection and current client/global-frame phase remain unfinished.',
            'The cast adapter requires an explicitly supplied effective NPC rank and qualified mode request. It does not infer Battle Command/cap behavior or invent missing owner iterator effects.',
            'Lost-target path-point fallback, mid-action rate renewal, native incoming interruption and exact current offline/reload action behavior remain separate. Unissued host actions are transient; already-issued flights retain their independent persistent lifetime.'],
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in (path, binary, fixture, STAGE / 'compiler.log', STAGE / 'sanitizer.log')})
    target = ROOT / 'tools/reference/tainted-fireball-cast-source-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Tainted A2:owned10/256/action6kind2;401 complete source rate/scheduler cases;mode admission/live rank remain explicit.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true'); build(parser.parse_args().check)
