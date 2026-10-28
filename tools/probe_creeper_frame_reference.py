#!/usr/bin/env python3
"""Resolve Creeper elapsed-age versus countdown semantics with complete getters."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}
    def extract(relative, name):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = sha(path)
        body = function(raw.decode(), name)
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'
    cpp = r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#define __fastcall
#define __stdcall
constexpr int UNIT_MISSILE=3;
struct D2GameStrc{}game;
struct D2MissileDataStrc{int nTotalFrames=0,nCurrentFrame=0;}data;
struct D2UnitStrc{int dwUnitType=3,dwClassId=474;D2MissileDataStrc*pMissileData=&data;}head,owner,target;
struct D2MissilesTxt{int wSubMissile[1]={475},dwParam[1]={9},dwHitPar[1]={15};}row;
int emits=0,collision=0;
D2MissilesTxt*SKILLS_GetMissilesTxtRecord(int){return &row;}
D2UnitStrc*SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return &owner;}
int MISSILE_GetSkill(D2UnitStrc*){return 294;}
int MISSILE_GetLevel(D2UnitStrc*){return 3;}
int CLIENTS_GetUnitX(D2UnitStrc*){return 40;}
int CLIENTS_GetUnitY(D2UnitStrc*){return 70;}
D2UnitStrc*D2GAME_CreateMissile_6FD115E0(D2GameStrc*,D2UnitStrc*p,int skill,int rank,int id,int x,int y){assert(p==&owner&&skill==294&&rank==3&&id==475&&x==40&&y==70);emits++;return &target;}
int MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc*){collision++;return 7;}
'''
    common = 'source/D2Common/src/Units/Missile.cpp'
    for name in ['MISSILE_SetTotalFrames', 'MISSILE_SetCurrentFrame', 'MISSILE_GetTotalFrames', 'MISSILE_GetCurrentFrame', 'MISSILE_GetRemainingFrames']:
        cpp += extract(common, name)
    mode = 'source/D2Game/src/MISSILES/MissMode.cpp'
    for name in ['MISSMODE_SrvDo26_Vines_PlagueVines', 'MISSMODE_SrvHit50_PlagueVinesTrail']:
        cpp += extract(mode, name)
    cpp += r'''
int main(){bool first=true;printf("[");
for(int total:{20,125,175,600,6475})for(int age=0;age<=total;age++){
MISSILE_SetTotalFrames(&head,total);MISSILE_SetCurrentFrame(&head,total-age);emits=collision=0;
int result=MISSMODE_SrvDo26_Vines_PlagueVines(&game,&head);
int hit=MISSMODE_SrvHit50_PlagueVinesTrail(&game,&head,&target),ending=MISSMODE_SrvHit50_PlagueVinesTrail(&game,&head,nullptr);
printf("%s[[%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d]]",first?"":",",total,age,MISSILE_GetTotalFrames(&head),MISSILE_GetCurrentFrame(&head),MISSILE_GetRemainingFrames(&head),emits,collision,result,hit,ending);first=false;
}printf("]\n");}
'''
    generated = BASE / 'creeper-frame-source-probe.cpp'
    generated.write_text(cpp)
    binary = generated.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(binary)], check=True)
    process = subprocess.run([str(binary)], capture_output=True)
    sanitizer = BASE / 'creeper-frame-sanitizer.log'
    sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-3000:]
    cases = json.loads(process.stdout)
    for (total, age), actual in cases:
        expected = [total, total-age, age, int(age % 9 == 0), 1, 7, 2 if age < total-15 else 0, 0]
        assert actual == expected, (total, age, actual, expected)
    assert len(cases) == 7400, len(cases)
    output = BASE / 'creeper-frame-source-probe.json'
    output.write_bytes(process.stdout)
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], currentExecutableParity=False, nativeAccepted=False,
        contexts=len(cases), compiledRoutineHashes=routines, sourceHashes=hashes, sanitizerClean=True,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__).resolve(), generated, output, sanitizer]},
        declaredServices=['Row/owner/position/skill/rank lookups and child creation request capture.', 'Ordinary collision returns a distinct sentinel without decrement, movement or retirement; supplied ages are pre-collision callback inputs.'],
        established=[
            'Complete setters/getters establish total lifetime, current countdown and misleadingly named GetRemainingFrames=elapsed age. Full do26emits at ages divisible by9, including age0. It is not a countdown modulo check.',
            'Complete hit50returns damage bit2when a target exists and elapsed age<total-15. It is active early and excludes the last15frames, rather than excluding the first15frames. Null-target ending returns0. Contact router/mitigation/LastCollide remain separate.',
        ],
        nativeDiscrepancy=dict(fixed=False, reviewMethod='Static comparison of the prepared C# call sites with executed complete source getters/callbacks; no native C# invocation in this harness.', reviewCommit='8852bab',
            reviewedFiles={name:hashlib.sha256(subprocess.check_output(['git','show','8852bab:'+name],cwd=ROOT)).hexdigest() for name in ['src/Sanctuary.Content/DruidCreeperFields.cs', 'src/Sanctuary.Content/DruidCreeperVines.cs']},
            scope='Prepared native head emission passes countdown to the elapsed-age do26predicate; prepared mat contact also passes countdown to the elapsed-age hit50predicate, reversing its active window. Older bounded vine proofs stubbed the ambiguous getter and therefore did not detect these cross-component mismatches. Preserve the running Fury package; correct these prepared Creeper components and dependent tests only after its full acceptance.'),
        remaining=['Native prepared Creeper phase correction, save-version migration/old in-flight semantics and dedicated source/native/world regression are next. This source harness does not establish full factory/AI/event/geometry/current executable behavior, underground exposure or learned permission.'])
    path = ROOT / 'tools/reference/creeper-frame-source-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print(f'Creeper frame semantics: {len(cases)} ages, seven complete routines, ASan/UBSan clean; static review identifies two prepared native countdown/age mismatches.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
