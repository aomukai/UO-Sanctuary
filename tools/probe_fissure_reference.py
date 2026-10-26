#!/usr/bin/env python3
"""Execute complete pinned Fissure launch/placement bodies with explicit services."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, bodies = {}, {}

    def routine(relative, name):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(
            v['sha'] for v in tree['tree'] if v['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        source = raw.decode()
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', source, re.M)
        assert match, name
        end = source.index('{', match.start()) + 1
        depth = 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}')
            end += 1
        text = source[match.start():end]
        bodies[name] = hashlib.sha256(text.encode()).hexdigest()
        return text + '\n'

    preflight = json.loads((ROOT / 'tools/reference/fissure-preflight.json').read_text())
    manifest = json.loads((ROOT / 'tools/reference/d2r-named-equipment/sources.json').read_text())
    for name, digest in preflight['pinnedInputHashes'].items():
        raw = (ROOT / f'tools/reference/d2r-named-equipment/json/{name}.json').read_bytes()
        assert hashlib.sha256(raw).hexdigest() == digest == manifest['files'][f'json/{name}.json']['sha256']
    assert preflight['skill']['Param1'] == 7 and preflight['skill']['Param2'] == 6
    assert preflight['missiles']['erruption center']['Range'] == 80
    cpp = r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_ASSERT assert
constexpr int UNIT_PLAYER=0,UNIT_MISSILE=3,UNITFLAG_SKSRVDOFUNC=0x100,MISSMODE_NOCOLLIDE=0,MISSMODE_BARRIERCOLLIDE=1;
struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};
struct D2GameStrc{}game;struct D2ActiveRoomStrc{}room;
struct D2DynamicPathStrc{int x=210,y=220;};
struct D2MissileDataStrc{int nTotalFrames=80,nCurrentFrame=80;};
struct D2UnitStrc{int dwUnitType=UNIT_MISSILE,dwClassId=461,dwFlags=0,dwMissileMode=0,x=300,y=400,skill=234,rank=20;D2DynamicPathStrc* pDynamicPath=nullptr;D2MissileDataStrc* pMissileData=nullptr;D2SeedStrc pSeed{};}actor,owner,target,created;
struct D2MissilesTxt{int nSize=1,wSubMissile[1]={462},wProgOverlay=32;}row;
struct D2SkillsTxt{int wSrvMissileA=461,dwCalc[2]={0,1};}skill;
struct DataTables{int nMissilesTxtRecordCount=600;}tables;DataTables* sgptDataTables=&tables;
struct D2MissileStrc{int dwFlags=0;D2UnitStrc* pOwner=nullptr;int nSkill=0,nSkillLevel=0,nMissile=0,nX=0,nY=0;};
struct D2DamageStrc{int dwPhysDamage=257,dwFireDamage=514;};
struct D2PathPointStrc{uint16_t X,Y;};
struct D2MissileUnitFindArgStrc{D2GameStrc* pGame;D2UnitStrc* pMissile;D2UnitStrc* pOwner;D2MissilesTxt* pMissilesTxtRecord;};
using FindCallback=bool(*)(D2UnitStrc*,void*);
struct CollisionEntry{FindCallback pfUnitFindCallback=nullptr;uint16_t nCollisionMask=0;}stru_6FD2E5F8[9];
D2DynamicPathStrc path;D2MissileDataStrc clockData;
bool hasRoom=true,hasOwner=true,hasRow=true,hasSkill=true,selected=false,allocate=true;
int collisionBits=0,distance=1,rangeValue=7,intervalValue=6,checks=0,sizeChecks=0,distanceChecks=0,terminalCalls=0,overlayCalls=0,formulaCalls=0;
int checkedX=0,checkedY=0,checkedMask=0,checkedSize=0;
std::vector<D2MissileStrc> requests;
int CLIENTS_GetUnitX(D2UnitStrc* p){assert(p);return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){assert(p);return p->y;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc* p){return p&&hasRoom?&room:nullptr;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc* p){return p&&hasOwner?&owner:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected?&target:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int n){assert(n==234);return hasSkill?&skill:nullptr;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return hasRow?&row:nullptr;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){assert(p);return p->x;}
int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc* p){assert(p);return p->y;}
int UNITS_GetDistanceToCoordinates(D2UnitStrc*,int,int){distanceChecks++;return distance;}
int COLLISION_CheckMaskWithSizeXY(D2ActiveRoomStrc*,int x,int y,int sx,int sy,uint16_t mask){assert(sx==sy);sizeChecks++;checkedX=x;checkedY=y;checkedMask=mask;checkedSize=sx;return collisionBits&mask;}
int COLLISION_CheckMask(D2ActiveRoomStrc*,int x,int y,uint16_t mask){checks++;checkedX=x;checkedY=y;checkedMask=mask;return collisionBits&mask;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){requests.push_back(*p);return allocate&&p->nMissile>=0?&created:nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int skillId,int rank){assert(skillId==234&&rank==20);formulaCalls++;return formula==0?rangeValue:intervalValue;}
int MISSILE_GetSkill(D2UnitStrc* p){return p->skill;}int MISSILE_GetLevel(D2UnitStrc* p){return p->rank;}
int MISSILE_GetCurrentFrame(D2UnitStrc* p){return p->pMissileData->nCurrentFrame;}
void MISSILE_SetCurrentFrame(D2UnitStrc* p,int n){p->pMissileData->nCurrentFrame=n;}
int PATH_GetVelocity(D2DynamicPathStrc*){return 0;}
int MISSMODE_SrvDmgHitHandler(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){terminalCalls++;return 2;}
int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){assert(false);return 0;}
int D2Common_10201(D2DynamicPathStrc*){assert(false);return 0;}
int MISSILE_GetActivateFrame(D2UnitStrc*){assert(false);return 0;}
int UNITS_GetUnitSizeX(D2UnitStrc*){assert(false);return 0;}
int D2COMMON_10198_PathGetSaveStep(D2DynamicPathStrc*,D2PathPointStrc**){assert(false);return 0;}
uint16_t COLLISION_CheckMaskWithSize(D2ActiveRoomStrc*,int,int,int,uint16_t){assert(false);return 0;}
D2UnitStrc* D2Common_10407(D2ActiveRoomStrc*,int,int,FindCallback,void*,int){assert(false);return nullptr;}
void UNITS_SetOverlay(D2UnitStrc* p,int overlay,int flag){assert(p==&target&&overlay>0&&flag==0);overlayCalls++;}
void Reset(){actor={};owner={};owner.dwUnitType=UNIT_PLAYER;target={};created={};path={};clockData={};row={};skill={};actor.pDynamicPath=&path;owner.pDynamicPath=&path;actor.pMissileData=&clockData;actor.pSeed.lSeed=0x123456789ABCDEF0ULL;hasRoom=hasOwner=hasRow=hasSkill=allocate=true;selected=false;collisionBits=0;distance=1;rangeValue=7;intervalValue=6;checks=sizeChecks=distanceChecks=terminalCalls=overlayCalls=formulaCalls=0;checkedX=checkedY=checkedMask=checkedSize=0;requests.clear();}
void PrintRequests(){printf("[");for(size_t i=0;i<requests.size();i++){auto p=requests[i];printf("%s[%d,%d,%d,%d,%d,%d,%d]",i?",":"",p.dwFlags,p.nMissile,p.nX,p.nY,p.nSkill,p.nSkillLevel,p.pOwner==&owner?1:0);}printf("]");}
'''
    routes = [
        ('source/D2Common/src/D2Seed.cpp', 'SEED_InitLowSeed'),
        ('source/D2Common/include/D2Seed.h', 'SEED_RollRandomNumber'),
        ('source/D2Common/include/D2Seed.h', 'SEED_RollLimitedRandomNumber'),
        ('source/D2Game/src/ITEMS/Items.cpp', 'ITEMS_RollLimitedRandomNumber'),
        ('source/D2Common/src/Units/Missile.cpp', 'MISSILE_GetRemainingFrames'),
        ('source/D2Game/src/SKILLS/Skills.cpp', 'D2GAME_CreateMissile_6FD115E0'),
        ('source/D2Game/src/SKILLS/Skills.cpp', 'sub_6FD15340'),
        ('source/D2Game/src/SKILLS/SkillSor.cpp', 'SKILLS_SrvDo028_Meteor_Blizzard_Eruption_BaalTaunt_Catapult'),
        ('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_HandleMissileCollision'),
        ('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_CreateMissileWithCollisionCheck'),
        ('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_SrvDo25_EruptionCenter'),
        ('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_SrvDmg08_EruptionCrack'),
    ]
    for relative, name in routes:
        cpp += routine(relative, name).replace('0x6AC690C5i64', '0x6AC690C5ULL')
    cpp += r'''
int main(){bool first=true;printf("{\"launch\":[");
for(int guard=0;guard<7;guard++)for(bool select:{false,true})for(int x:{0,210})for(int y:{0,220})for(int dist:{100,101})for(bool alloc:{false,true}){
 Reset();hasSkill=guard!=1;skill.wSrvMissileA=guard==2?-1:guard==3?600:461;hasRoom=guard!=5;collisionBits=guard==6?4:0;selected=select;allocate=alloc;distance=dist;path.x=select?17:x;path.y=select?19:y;target.x=x;target.y=y;
 int ret=SKILLS_SrvDo028_Meteor_Blizzard_Eruption_BaalTaunt_Catapult(&game,guard==4?nullptr:&owner,234,20);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,",first?"":",",guard,select,x,y,dist,alloc,ret,owner.dwFlags,sizeChecks,distanceChecks,checkedX,checkedY,checkedSize,checkedMask);first=false;PrintRequests();printf("]");
}printf("],\"coordinate\":[");first=true;
for(bool select:{false,true})for(int x:{0,210})for(int y:{0,220})for(int dist:{100,101})for(bool alloc:{false,true}){
 Reset();selected=select;allocate=alloc;distance=dist;path.x=213;path.y=217;target.x=207;target.y=223;
 auto* result=D2GAME_CreateMissile_6FD115E0(&game,&owner,234,20,461,x,y);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,",first?"":",",select,x,y,dist,alloc,result!=nullptr,distanceChecks);first=false;PrintRequests();printf("]");
}printf("],\"scatter\":[");first=true;
for(int x:{0,300,301,-300,2147480000})for(int elapsed:{0,1,6,78,80})for(int radius:{1,2,3,7,17,32})for(int interval:{0,1,6})for(int mask:{5,0x45})for(int bits:{0,1,4,0x40})for(bool alloc:{false,true}){
 Reset();actor.x=x;clockData.nCurrentFrame=80-elapsed;rangeValue=radius;intervalValue=interval;collisionBits=bits;allocate=alloc;
 auto* result=MISSMODE_CreateMissileWithCollisionCheck(&game,&actor,radius,interval,462,mask);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%llu,",first?"":",",x,elapsed,radius,interval,mask,bits,alloc,result!=nullptr,checks,checkedX,checkedY,checkedMask,(unsigned long long)actor.pSeed.lSeed);first=false;PrintRequests();printf("]");
}printf("],\"center\":[");first=true;
for(int guard=0;guard<8;guard++)for(int elapsed=0;elapsed<=80;elapsed++)for(int bits:{0,1,4,0x40})for(int interval:{0,6}){
 Reset();hasOwner=guard!=1;hasRoom=guard!=2;hasRow=guard!=3;hasSkill=guard!=4;row.wSubMissile[0]=guard==5?0:guard==7?-1:462;clockData.nCurrentFrame=80-elapsed;collisionBits=bits;intervalValue=interval;
 int ret=MISSMODE_SrvDo25_EruptionCenter(&game,guard==6?nullptr:&actor);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%llu,",first?"":",",guard,elapsed,bits,interval,ret,clockData.nCurrentFrame,terminalCalls,formulaCalls,checks,checkedX,checkedY,checkedMask,(int)requests.size(),(unsigned long long)actor.pSeed.lSeed);first=false;PrintRequests();printf("]");
}printf("],\"overlay\":[");first=true;
for(int guard=0;guard<4;guard++)for(int overlay:{-1,0,32}){Reset();hasRow=guard!=2;row.wProgOverlay=overlay;D2DamageStrc damage;
 MISSMODE_SrvDmg08_EruptionCrack(&game,guard==1?nullptr:&actor,guard==3?nullptr:&target,&damage);
 printf("%s[%d,%d,%d,%d,%d]",first?"":",",guard,overlay,overlayCalls,damage.dwPhysDamage,damage.dwFireDamage);first=false;
}printf("],\"lifecycle\":[");Reset();first=true;
for(int frame=0;frame<80;frame++){int before=(int)requests.size();int ret=MISSMODE_SrvDo25_EruptionCenter(&game,&actor);printf("%s[%d,%d,%d,%d,%d]",first?"":",",frame,ret,clockData.nCurrentFrame,(int)requests.size()-before,terminalCalls);first=false;}
printf("]}\n");}
'''
    path = STAGE / 'fissure-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-Wall', '-Wextra', '-Werror',
                    '-Wno-unused-parameter', '-fsanitize=address,undefined', '-fno-sanitize-recover=all',
                    str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'fissure-source-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    counts = {key: len(value) for key, value in data.items()}
    assert counts == dict(launch=224, coordinate=32, scatter=7200, center=5184, overlay=12, lifecycle=80), counts
    for g, selected, x, y, dist, alloc, ret, flags, sized, distances, cx, cy, size, mask, requests in data['launch']:
        valid_row = g not in (1, 2, 3)
        gate = valid_row and g not in (4, 5) and x != 0 and y != 0
        reaches_distance = gate and g != 6
        reaches_factory = reaches_distance and dist <= 100
        assert flags == (0x100 if valid_row and g != 4 else 0)
        assert (sized, distances, len(requests), ret) == (int(gate), int(reaches_distance), int(reaches_factory), int(reaches_factory and alloc))
        if gate:
            assert (cx, cy, size, mask) == (x, y, 1, 5)
        if requests:
            assert requests == [[1, 461, x, y, 234, 20, 1]]
    for selected, x, y, dist, alloc, ret, distances, requests in data['coordinate']:
        expected = (207, 223) if selected else (213, 217)
        if x or y:
            expected = x, y
        assert distances == 1 and ret == int(dist <= 100 and alloc)
        assert requests == ([[1, 461, *expected, 234, 20, 1]] if dist <= 100 else [])
    for g, elapsed, bits, interval, ret, current, terminal, formulas, checks, x, y, mask, attempts, seed, requests in data['center']:
        valid = g not in (3, 4, 5, 6)
        eligible = valid and g not in (1, 2) and interval != 0 and elapsed % interval == 0
        assert (current, terminal, formulas, checks) == (79-elapsed if valid else 80-elapsed, int(valid and elapsed >= 79), 2*int(valid), int(eligible))
        assert ret == (2 if not valid or elapsed >= 79 else 1)
        assert attempts == len(requests) == int(eligible and bits & 0x45 == 0)
        if eligible:
            assert mask == 0x45 and -6 <= x-300 <= 5 and -6 <= y-400 <= 5
        else:
            assert seed == 0x123456789ABCDEF0
    for guard, overlay, calls, physical, fire in data['overlay']:
        assert calls == int(guard == 0 and overlay > 0) and (physical, fire) == (257, 514)
    assert sum(v[3] for v in data['lifecycle']) == 14
    for frame, ret, current, attempts, terminal in data['lifecycle']:
        assert (ret, current, attempts, terminal) == (2 if frame == 79 else 1, 79-frame, int(frame % 6 == 0), int(frame == 79))
    fixture = STAGE / 'fissure-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(
        referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
        sourceHashes=hashes, routineHashes=bodies, pinnedInputHashes=preflight['pinnedInputHashes'],
        caseCounts=counts, fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),
        sanitizers='ASan/UBSan exit0 and empty runtime stderr',
        established=[
            'Complete do28, initial size gate and coordinate launcher compose: selected-target coordinates precede path aim; initial mask5 requires both nonzero coordinates, distance100 is allowed and101 is rejected. A direct coordinate creator permits a single zero axis, unlike the do28 gate.',
            'Complete do25 and shared placement helper execute with complete elapsed-frame getter and RNG/seed/item wrapper bodies. Fresh high666 seed is sourceX+elapsed, each eligible attempt consumes two positive-bound draws even if landing mask or allocation rejects it.',
            'Current range7 samples independent square offsets[-6,+5]. Mask0x45 includes0x40 unlike Blizzard mask5; there is no radial LOS query in the shared body.',
            'Child requests use flags3, child462, original owner and skill234/rank20. Negative child IDs reach the declared factory; zero child IDs stop do25 before formulas and lifetime.',
            'Complete generic collision body runs in the declared stationary NOCOLLIDE center mode. Child emission precedes decrement:80body calls create14requests at elapsed0,6,...78 and terminate on call79. Generic terminal hit callback is an explicit recording service.',
            'Complete damage8 only requests a positive fire_hit overlay on a nonnull target; the supplied physical/fire damage structure remains unchanged.'
        ],
        services=[
            'Room/row/target/formula/distance/landing collision and allocation are explicit controlled services. Formula inputs are the pinned range7/interval6 plus declared boundary controls; source evaluator and generic factory do not execute here.',
            'Stationary NOCOLLIDE mode is supplied, pending complete current-row factory composition. Other generic collision branches deliberately assert if reached. Terminal hit behavior, crack collider/LastCollide/NHD/damage/health and native learned input remain separate.',
            'Legacy source reconstruction plus pinned modern rows does not certify current executable behavior. Modern next-hit identity across cracks/casts/owners/skills remains unresolved.',
            'No native Fissure handler or final effects/audio/talent/right/quick icons are installed by this source proof.'
        ],
        next='Compare the existing production Blizzard sampler to every applicable complete-helper output, then reuse it for Fissure. Compose current-row factory, fresh damage endpoints and crack hit identity separately.')
    out = ROOT / 'tools/reference/fissure-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print(sum(counts.values()), 'complete Fissure launch/placement/lifecycle/overlay contexts pass; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
