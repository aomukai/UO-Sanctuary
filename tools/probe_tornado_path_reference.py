#!/usr/bin/env python3
"""Compose source charged initialization with its actual seventy-five-frame path.

Only the generic path-dispatch wrapper and ABI field setters are services;
source seed initialization, target-X accessor, initializer and path bodies run.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_charged_bolt_path_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/charged-bolt-path-source-evidence.json').read_text())
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == parent['legacyCommit'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = dict(parent['sourceHashes']), dict(parent['routineHashes'])

    def read(relative):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        return raw.decode()

    def routine(relative, name):
        source = read(relative)
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', source, re.M)
        assert match, name
        end, depth = source.index('{', match.start()) + 1, 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}')
            end += 1
        body = source[match.start():end]
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    header = read('source/D2Common/include/Path/Path.h')
    assert re.search(r'PATHTYPE_CHARGEDBOLT\s*=\s*10\b', header)
    cpp = (STAGE / 'charged-bolt-path-source-probe.cpp').read_text().split('int main()', 1)[0]
    assert cpp.count('int dwPathPoints;') == 1
    cpp = cpp.replace('int dwPathPoints;', 'int dwPathPoints;int nType=0;')
    cpp += r'''
constexpr int PATHTYPE_CHARGEDBOLT=10;
struct D2UnitStrc{D2DynamicPathStrc* pDynamicPath=nullptr;D2SeedStrc pSeed{};int total=75,current=75;}missile;
int MISSILE_GetTotalFrames(D2UnitStrc* p){return p->total;}
void MISSILE_SetTotalFrames(D2UnitStrc* p,int n){p->total=n;}
void MISSILE_SetCurrentFrame(D2UnitStrc* p,int n){p->current=n;}
void PATH_SetType(D2DynamicPathStrc* p,int type){p->nType=type;}
void PATH_SetNewDistance(D2DynamicPathStrc* p,uint8_t distance){p->nDistMax=distance;}
void D2Common_10142(D2DynamicPathStrc* p,D2UnitStrc* actor,int zero){assert(p->nType==PATHTYPE_CHARGEDBOLT&&zero==0);PATH_ComputePathChargedBolt_6FDAB4A0(p,&actor->pSeed);}
'''
    cpp += routine('source/D2Common/src/D2Seed.cpp', 'SEED_InitLowSeed')
    cpp += routine('source/D2Common/src/Path/Path.cpp', 'D2COMMON_10175_PathGetFirstPointX')
    cpp += routine('source/D2Game/src/SKILLS/SkillSor.cpp', 'SKILLS_MissileInit_ChargedBolt')
    cpp += r'''
int main(){printf("[");bool first=true;
for(int origin:{100,300,14200,65500})for(int dx:{-10,-5,-2,-1,0,1,2,5,10,12})for(int dy:{-10,-1,0,1,10})for(int ordinal:{0}){
 D2DynamicPathStrc path{};path.tGameCoords={uint16_t(origin),uint16_t(origin)};path.tTargetCoord={uint16_t(origin+dx),uint16_t(origin+dy)};
 missile={};missile.pDynamicPath=&path;SKILLS_MissileInit_ChargedBolt(&missile,ordinal);
 int heading[3];PATH_GetDirections_6FDAB790(heading,{uint16_t(origin),uint16_t(origin)},path.tTargetCoord);
 assert(path.nType==10&&path.nDistMax==75&&path.dwPathPoints==38&&missile.total==75&&missile.current==75);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%llu,[",first?"":",",origin,dx,dy,ordinal,missile.total,missile.current,heading[0],(unsigned long long)missile.pSeed.lSeed);first=false;
 for(int i=0;i<path.dwPathPoints;i++)printf("%s[%u,%u]",i?",":"",path.PathPoints[i].X,path.PathPoints[i].Y);printf("]]");
}printf("]\n");}
'''
    path = STAGE / 'tornado-path-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'tornado-path-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-3000:]
    rows = json.loads(run.stdout)
    assert len(rows) == 200 and all(len(r[8]) == 38 for r in rows)
    fixture = STAGE / 'tornado-path-source-probe.json'
    fixture.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceHashes=hashes, routineHashes=routines,
                    reusedPathEvidence='tools/reference/charged-bolt-path-source-evidence.json',
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(rows)),
                    sanitizers='ASan/UBSan exit0 with empty runtime stderr',
                    established=['Complete source seed initializer, target-X accessor, Charged Bolt initializer and charged-path/direction bodies compose for75frames and the single Tornado ordinal0.',
                                 'Each case records38 actual source waypoints, final missile RNG,75total/current frames and actual charged path enum10. Paths include uint16 coordinate wrap and the source zero-direction branch.',
                                 'Path generation advances the same missile seed used by later hit damage and knockback; resetting it at contact would discard37source draws.'],
                    limits=['The generic path-dispatch wrapper is a service that asserts charged type10 and invokes the complete charged-path body; full dispatcher, room/collision, factory and world timing are not executed.',
                            'Raw initialization inputs are supplied directly. Exact-self aim adjustment belongs to the separately proven generic factory, not this initializer.',
                            'Native UO motion/rounding, generic movement, collision order, current executable acceptance and ordinary-client presentation remain separate. No handler or installation is enabled by this source-only probe.'])
    output = ROOT / 'tools/reference/tornado-path-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == text
    else:
        output.write_text(text)
    print('Tornado:200complete75-frame initializer/path/final-RNG source compositions; ASan/UBSan clean.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
