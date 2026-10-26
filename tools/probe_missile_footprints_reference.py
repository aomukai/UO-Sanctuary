#!/usr/bin/env python3
"""Execute full source footprint-size/bounding-box bodies on a recording grid."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    relative = 'source/D2Common/src/D2Collision.cpp'
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    text, bodies = raw.decode(), {}

    def routine(name):
        m = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert m, name
        end, depth = text.index('{', m.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[m.start():end]
        bodies[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
#define __stdcall
enum{COLLISION_UNIT_SIZE_NONE,COLLISION_UNIT_SIZE_POINT,COLLISION_UNIT_SIZE_SMALL,COLLISION_UNIT_SIZE_BIG};
struct D2ActiveRoomStrc{}room;struct D2BoundingBoxStrc{int nLeft,nRight,nBottom,nTop;};
struct Cell{int x,y;};std::vector<Cell> writes;
void COLLISION_SetCollisionMask(D2ActiveRoomStrc*,int x,int y,uint16_t mask){assert(mask==0x40);writes.push_back({x,y});}
void COLLISION_SetCollisionMaskForBoundingBoxRecursively(D2ActiveRoomStrc* p,D2BoundingBoxStrc* box,uint16_t mask){
 for(int x=box->nLeft;x<=box->nRight;x++)for(int y=box->nBottom;y<=box->nTop;y++)COLLISION_SetCollisionMask(p,x,y,mask);}
'''
    cpp += routine('COLLISION_CreateBoundingBox') + routine('COLLISION_SetMaskWithSize')
    cpp += r'''
int main(){bool first=true;printf("[");for(int size:{0,1,2,3,4})for(int coordinate:{100,300,65500}){
 writes.clear();COLLISION_SetMaskWithSize(&room,coordinate,coordinate+1,size,0x40);
 printf("%s[%d,%d,%d,[",first?"":",",size,coordinate,coordinate+1);first=false;
 for(size_t i=0;i<writes.size();i++)printf("%s[%d,%d]",i?",":"",writes[i].x,writes[i].y);printf("]]");}printf("]\n");}
'''
    path = STAGE / 'missile-footprints-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'missile-footprints-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-2000:]
    rows = json.loads(run.stdout)
    assert len(rows) == 15
    for size, x, y, cells in rows:
        expected = {1: 1, 2: 5, 3: 9}.get(size, 0)
        assert len(cells) == len({tuple(c) for c in cells}) == expected
    fixture = STAGE / 'missile-footprints-source-probe.json'
    fixture.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False,
                    sourceHashes={relative: hashlib.sha256(raw).hexdigest()}, routineHashes=bodies,
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), shapes=15),
                    established=['Complete SetMaskWithSize and CreateBoundingBox bodies produce1point/5cross/9square cells for source sizes1/2/3; sizes0/4write no cells.',
                                 'Native point-against-footprint comparison can reuse CombatHitboxes.SourceTouches(1,size) rather than circle/radius geometry.'],
                    limits=['Room/grid writes and recursive room splitting are explicit recording services on a single bounded grid. Native terrain, cell rounding, lifecycle/allocation and cross-producer event order remain separate.',
                            'Current Collision1 source inventory and actual provider snapshots are checked in native integration tests.'],
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr')
    out = ROOT / 'tools/reference/missile-footprints-source-evidence.json'
    result = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == result
    else:
        out.write_text(result)
    print('15complete source footprint shapes pass; native provider comparison remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
