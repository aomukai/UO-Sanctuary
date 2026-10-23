#!/usr/bin/env python3
"""Compile unchanged pinned do8 and its signed integer fan helpers."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
PIN = '0c0bbd1e6bef9181a3da1c046523af622a925416'
tree = json.loads((STAGE/'d2moo-source-tree.json').read_text())
assert tree['sha'] == PIN
hashes, blocks = {}, {}

def source(path):
    raw = (STAGE/'d2moo-pinned-source'/path).read_bytes()
    blob = next(v['sha'] for v in tree['tree'] if v['path'] == path)
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest() == blob
    hashes[path] = hashlib.sha256(raw).hexdigest()
    return raw.decode()

def routine(text, name):
    match = re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{', text, re.M)
    assert match, name
    end = text.index('{', match.start())+1
    depth = 1
    while depth:
        depth += (text[end] == '{')-(text[end] == '}')
        end += 1
    blocks[name] = text[match.start():end]
    return blocks[name]+'\n'

ama = source('source/D2Game/src/SKILLS/SkillAma.cpp')
skills = source('source/D2Game/src/SKILLS/Skills.cpp')
factory = source('source/D2Game/src/MISSILES/Missiles.cpp')
assert 'else if (nTargetX == nX && nTargetY == nY)' in factory
assert re.search(r'else if \(nTargetX == nX && nTargetY == nY\)\s*\{\s*\+\+nTargetX;\s*\+\+nTargetY;',factory)
assert 'else if (missileParams->dwFlags & 32)' in factory
cpp = r'''#include <cstdint>
#include <cstdio>
#include <vector>
#include <initializer_list>
#define __fastcall
constexpr int UNITFLAG_SKSRVDOFUNC=64;
struct D2GameStrc{} game;
struct D2UnitStrc{unsigned dwFlags=0;int x=100,y=100;} owner;
struct D2SkillsTxt{int dwCalc[3]={0,1,2},wSrvMissileA=114,wSrvMissileB=-1;} row;
struct D2MissileStrc{unsigned dwFlags=0;int nSkillLevel=0,nSkill=0,nMissile=0,nActivateFrame=0,nTargetX=0,nTargetY=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;};
struct Tables{int nMissilesTxtRecordCount=700;} tables;
auto* sgptDataTables=&tables;
bool record=true,target=true;int tx=110,ty=100,weapon=1,calc[3]={2,0,0},allocations=0;
std::vector<D2MissileStrc> missiles;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return record?&row:nullptr;}
int D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){*x=tx;*y=ty;return target;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return calc[formula];}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}
int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
int UNITS_GetWeaponClass(D2UnitStrc*){return weapon;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){missiles.push_back(*p);return allocations?&owner:nullptr;}
'''
cpp += routine(skills, 'sub_6FD140D0')
cpp += routine(skills, 'sub_6FD14120')
cpp += routine(ama, 'SKILLS_SrvDo008_MultipleShot_Teeth_ShockWave')
cpp += r'''void emit(int dx,int dy,int count,int middle,int active){
tx=owner.x+dx;ty=owner.y+dy;calc[0]=count;calc[1]=active;calc[2]=middle;missiles.clear();owner.dwFlags=0;
int result=SKILLS_SrvDo008_MultipleShot_Teeth_ShockWave(&game,&owner,67,7);
std::printf("[%d,%d,%d,%d,%d,%d,%u,[",dx,dy,count,middle,active,result,owner.dwFlags);
bool first=true;for(auto& m:missiles){if(!first)std::printf(",");first=false;std::printf("[%d,%d,%u,%d,%d,%d,%d,%d]",m.nTargetX-owner.x,m.nTargetY-owner.y,m.dwFlags,m.nMissile,m.nActivateFrame,m.nSkill,m.nSkillLevel,m.pOwner==&owner && m.pOrigin==&owner);}
std::printf("]]");}
int main(){std::printf("{\"fanCases\":[");bool first=true;
for(int dx=-8;dx<=8;dx++)for(int dy=-8;dy<=8;dy++)for(int count:{1,2,3,5,12,24})for(int mode:{0,1}){
if(!first)std::printf(",");first=false;emit(dx,dy,count,mode?(count<2?count:2):0,mode);}
std::printf("],\"guardCases\":[");first=true;
for(int rec:{0,1})for(int tgt:{0,1})for(int w:{0,1})for(int a:{-1,114,700})for(int b:{-1,214,700}){
record=rec;target=tgt;weapon=w;row.wSrvMissileA=a;row.wSrvMissileB=b;
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,",rec,tgt,w,a,b);emit(10,0,5,2,1);std::printf("]");}
std::printf("]}\n");}
'''
cpp_path = STAGE/'fan-source-probe.cpp'
cpp_path.write_text(cpp)
binary = STAGE/'fan-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(cpp_path),'-o',str(binary)],check=True)
data = json.loads(subprocess.check_output([str(binary)]))

def trunc(a, b):
    return (abs(a)//abs(b)) * (-1 if (a<0) != (b<0) else 1)

def expected(dx, dy, count, middle):
    x, y = dx, dy
    if x*x+y*y < 4:
        x, y = 4*x, 4*y
    if x*x+y*y < 16:
        x, y = 2*x, 2*y
    x, y = y, -x
    while x*x+y*y > 3:
        x, y = trunc(x,2), trunc(y,2)
    first = trunc(count*x,-2), trunc(count*y,-2)
    central = middle or count
    edge = trunc(count-central,2)
    return [(dx+first[0]+i*x,dy+first[1]+i*y,2080+(0 if edge <= i < edge+central else 65536)) for i in range(count)]

for dx,dy,count,middle,active,result,flags,missiles in data['fanCases']:
    assert (result,flags) == (1,64)
    assert [(v[0],v[1],v[2]) for v in missiles] == expected(dx,dy,count,middle)
    assert all(v[3:] == [114,active,67,7,1] for v in missiles)
for rec,tgt,weapon,a,b,case in data['guardCases']:
    chosen = b if weapon != 1 and b >= 0 else a
    valid = 0 <= chosen < 700
    assert case[5] == (0 if not rec else 1 if not tgt else int(valid))
    assert len(case[7]) == (5 if rec and tgt and valid else 0)
    assert all(v[3] == chosen for v in case[7])
path = STAGE/'fan-source-probe.json'
path.write_text(json.dumps(data,separators=(',',':'))+'\n')
modern_path = ROOT/'tools/reference/d2r-named-equipment/json/skills.json'
rows = json.loads(modern_path.read_text()).values()
current = {r['skill']:{k:v for k,v in r.items() if v not in ('',None)} for r in rows if r.get('skill') in ('Teeth','Multiple Shot','Shock Wave')}
assert all(r['srvdofunc'] == 8 for r in current.values())
assert current['Teeth']['calc3'] == 0 and current['Multiple Shot']['calc3'] == 2
evidence = dict(referenceVersion='3.3.93847',legacyCommit=PIN,sourceHashes=hashes,
    modernTableHash=hashlib.sha256(modern_path.read_bytes()).hexdigest(),currentSkills=current,
    probe=dict(totalCases=sum(map(len,data.values())),caseCounts={k:len(v) for k,v in data.items()},
        compiledRoutines=list(blocks),blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},
        dataPath=str(path.relative_to(ROOT)),dataHash=hashlib.sha256(path.read_bytes()).hexdigest(),
        services='Formula cells, target lookup, weapon class, record lookup and missile allocation are declared fixture services. Allocation always fails: the unchanged emission loop still attempts every missile.'),
    established=[
        'Do8 creates integer aim points across a line through the initial target, perpendicular to a near-target-expanded signed vector. Repeated signed /2 reduction leaves a step whose squared length is at most3. Spread depends on cursor distance; no fixed angle or endpoint normalization is present.',
        'The first aim is target+trunc(count*step/-2), not -(count-1)/2. Odd counts/signed axes retain this source bias; self aim produces coincident endpoints, not a manufactured fan. The missile factory separately changes zero-length target to origin+(1,1).',
        'calc3=0 makes every missile central. Otherwise the outer (count-central)/2 and remaining tail carry flag0x10000, while central missiles clear it. Pinned Multiple Shot has2 central missiles; Teeth has0 meaning all. Flags are preserved without assuming all downstream item/proc semantics.',
        'Weapon class1 uses srvmissilea; another class selects valid/nonnegative srvmissileb. No target returns success without emission, invalid record/missile fails; failed missile allocation does not stop later attempts.'
    ],limits=[
        'Legacy source reconstruction is evidence, not a modern executable certificate. Full start4 mana/quantity, current calc5 synergy, damage/proc filtering, Pierce, per-cast next-hit delay and missile factory/path/collision/owner/client behavior remain separate.',
        'The native two-source-subtile-per-tile mapping and integer cursor conversion remain provisional. Source guard cases include zero coordinates through a declared successful target service; the real target helper has its own coordinate validity and fallback rules.',
        'The source function TODO names identify undeclared local semantics; the numeric emission/flags are compiled unchanged. This evidence does not enable Multiple Shot or Shock Wave.'
    ])
(ROOT/'tools/reference/fan-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print('Shared do8 source cases:', evidence['probe']['totalCases'])
