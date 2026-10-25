#!/usr/bin/env python3
"""Run the complete stationary unit-target initializer and live aim getter.

Room/grid/footprint services retain the existing bounded path probe contract.
The source's general initializer is marked BROKEN; this is supplemental source
evidence, not a comparison with the pinned current executable.
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    import probe_toward_path_reference
    probe_toward_path_reference.build(True)
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    hashes, routines = {}, {}

    def extract(rel, name):
        raw = (STAGE / 'd2moo-pinned-source' / rel).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == rel)
        hashes[rel] = hashlib.sha256(raw).hexdigest()
        text = raw.decode()
        m = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert m, name
        i, depth = text.index('{', m.start()) + 1, 1
        while depth:
            depth += (text[i] == '{') - (text[i] == '}')
            i += 1
        body = text[m.start():i]
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    cpp = (STAGE / 'toward-path-source-probe.cpp').read_text()
    cpp = cpp[:cpp.index('int main()')]
    # ABI declarations and explicit services only; source bodies stay unchanged.
    cpp = cpp.replace('dwFlags=0;void* pStatListEx=', 'dwFlags=0,dwAnimMode=0,dwUnitId=0;void* pStatListEx=')
    cpp = cpp.replace('dwSpeed=0,nDirOffset=0;', 'dwSpeed=0,nDirOffset=0,dwTargetType=0,dwTargetId=0;')
    cpp = cpp.replace('int UNITS_GetUnitSizeX(D2UnitStrc*){assert(false);return 0;}', 'int UNITS_GetUnitSizeX(D2UnitStrc*){return 1;}')
    assert 'dwTargetId=0' in cpp and 'dwAnimMode=0' in cpp
    cpp += r'''
using D2UnitGUID=int;
constexpr int IMODE_INBELT=2,IMODE_EQUIP=1;
struct D2GameStrc{};
D2UnitStrc* registered=nullptr;
D2UnitStrc* SUNIT_GetServerUnit(D2GameStrc*,int type,int id){return registered && registered->dwUnitType==type && registered->dwUnitId==id?registered:nullptr;}
int CLIENTS_GetUnitX(D2UnitStrc* u){return UNITS_GetXPosition(u);}
int CLIENTS_GetUnitY(D2UnitStrc* u){return UNITS_GetYPosition(u);}
'''
    path = 'source/D2Common/src/Path/Path.cpp'
    unit = 'source/D2Game/src/UNIT/SUnit.cpp'
    skill = 'source/D2Game/src/SKILLS/Skills.cpp'
    for name in ['PATH_SetTargetUnit', 'PATH_GetTargetUnit', 'PATH_GetTargetTypeAndGUID', 'D2COMMON_10175_PathGetFirstPointX', 'D2COMMON_10176_PathGetFirstPointY']:
        cpp += extract(path, name)
    for name in ['sub_6FCBC7E0', 'SUNIT_GetTargetUnit']:
        cpp += extract(unit, name)
    cpp += extract(skill, 'D2GAME_GetXAndYFromTargetUnit_6FD14020')
    cpp += r'''
int main(){printf("[");bool first=true;int positions[][2]={{110,104},{0,104},{110,0},{100,100},{120,110}};
for(int wall:{0,1})for(auto& position:positions)for(int old:{0,1})for(int transition=0;transition<5;transition++){
 D2GameStrc game;D2UnitStrc actor,target;D2DynamicPathStrc p,tp;
 actor.pDynamicPath=&p;p.pUnit=&actor;target.pDynamicPath=&tp;tp.pUnit=&target;target.dwUnitId=19;actor.dwUnitId=7;
 p.dwFlags=0x100;p.dwPathType=13;p.nDistMax=14;p.dwSpeed=0;p.dwVelocity=p.dwMaxVelocity=0;p.dwPathPoints=0;
 p.tGameCoords.dwPrecisionX=p.tGameCoords.dwPrecisionY=(100<<16)+32768;
 tp.tGameCoords.dwPrecisionX=(position[0]<<16)+32768;tp.tGameCoords.dwPrecisionY=(position[1]<<16)+32768;
 p.tTargetCoord={uint16_t(old?88:0),uint16_t(old?89:0)};
 PATH_SetTargetUnit(&p,&target);registered=&target;
 // Assignment itself must preserve the old path coordinate.
 assert(p.tTargetCoord.X==(old?88:0) && p.tTargetCoord.Y==(old?89:0));
 std::fill(std::begin(cells),std::end(cells),0);
 if(wall)for(int y=80;y<128;y++)cells[(y-80)*48+23]=1;
 int count=D2Common_10142(&p,&actor,0);int cachedX=p.tTargetCoord.X,cachedY=p.tTargetCoord.Y;
 if(position[0]&&position[1])assert(cachedX==position[0] && cachedY==position[1]);
 else assert(cachedX==(old?88:0) && cachedY==(old?89:0));
 // Live movement, dead-but-present, missing registry and self-target differ.
 if(transition==1){tp.tGameCoords.dwPrecisionX=(115<<16)+32768;tp.tGameCoords.dwPrecisionY=(109<<16)+32768;}
 if(transition==2)target.dwAnimMode=12;
 if(transition==3)registered=nullptr;
 if(transition==4){PATH_SetTargetUnit(&p,&actor);registered=&actor;}
 int x=-1,y=-1;int ok=D2GAME_GetXAndYFromTargetUnit_6FD14020(&game,&actor,&x,&y);
 assert(p.tTargetCoord.X==cachedX && p.tTargetCoord.Y==cachedY);
 printf("%s{\"input\":[%d,%d,%d,%d,%d],\"cached\":[%d,%d],\"resolved\":[%d,%d],\"success\":%d,\"pathCount\":%d}",first?"":",",wall,position[0],position[1],old,transition,cachedX,cachedY,x,y,ok,count);first=false;
}printf("]\n");}
'''
    source = STAGE / 'sentry-aim-source-probe.cpp'
    source.write_text(cpp)
    binary = source.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(source), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'sentry-aim-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-4000:]
    data = json.loads(result.stdout)
    assert len(data) == 100
    fixture = STAGE / 'sentry-aim-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    e = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceHashes=hashes, routineHashes=routines,
             initializerEvidence='tools/reference/toward-path-engine-evidence.json',
             fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=100),
             established=[
                 'PATH_SetTargetUnit stores unit identity without caching its coordinates. Complete D2Common_10142/PATH_UpdateTargetUnit cache a nonzero stationary unit target before geometry, retain that target coordinate for both successful and failed paths, and reject either-zero unit coordinates before updating the cache.',
                 'Complete registry refresh, SUNIT_GetTargetUnit and target-coordinate getter use current live coordinates without refreshing the path cache, accept a dead-but-present monster, clear a deleted registry target, exclude self-target and fall back to retained path coordinates. Either-zero result suppresses the individual do17 missile through the separately verified launcher.'
             ], limits=[
                 'The reconstructed initializer is marked BROKEN. This source proof is supplementary; exact current executable behavior, native room/collision/sector/coordinate projection and animation timing remain separately scoped.',
                 'The server registry lookup is a declared type/GUID service with present/absent/self controls; full hash-list iteration, replacement of a different pointer under the same GUID and native serial reuse are not exercised.',
                 'Stationary speed0, unit target, path13, zero actor footprint, source unit size1 and synthetic rooms/grid are exercised. This does not verify moving-target lead, unit footprint projection, town refusal, low-threat path probe integration or full monster mode-change execution.',
                 'A current native save without a retained path coordinate must stay explicitly unsupported when its pending target disappears; do not reconstruct it from a later position.'
             ], sanitizers='ASan/UBSan; exit0 and empty runtime stderr required')
    out = ROOT / 'tools/reference/sentry-aim-source-evidence.json'
    rendered = json.dumps(e, indent=2) + '\n'
    if check:
        assert out.read_text() == rendered
    else:
        out.write_text(rendered)
    print('100 complete stationary unit-path/target-refresh/aim getter cases pass; sanitizers clean.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
