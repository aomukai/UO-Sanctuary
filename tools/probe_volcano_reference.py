#!/usr/bin/env python3
"""Execute complete Volcano launch, placement, emitter and terminal child bodies."""
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
    preflight = json.loads((ROOT / 'tools/reference/volcano-preflight.json').read_text())
    sources, blocks = {}, {}

    def routine(relative, name):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        sources[relative] = hashlib.sha256(raw).hexdigest()
        text = raw.decode()
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end, depth = text.index('{', match.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[match.start():end]
        blocks[name] = hashlib.sha256(body.encode()).hexdigest()
        return body.replace('0x6AC690C5i64', '0x6AC690C5ULL') + '\n'

    for name, digest in preflight['pinnedInputHashes'].items():
        assert hashlib.sha256((ROOT / f'tools/reference/d2r-named-equipment/json/{name}.json').read_bytes()).hexdigest() == digest
    cpp = r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_ASSERT assert
constexpr int UNIT_PLAYER=0,UNIT_MISSILE=3,UNITFLAG_SKSRVDOFUNC=0x100;
struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};
struct D2GameStrc{}game;struct D2ActiveRoomStrc{}room;
struct D2DynamicPathStrc{int x=210,y=220;};
struct D2MissileDataStrc{int nTotalFrames=150,nCurrentFrame=150;};
struct D2UnitStrc{int dwUnitType=UNIT_MISSILE,dwClassId=479,dwFlags=0,x=300,y=400,skill=244,rank=20;uint32_t targetX=0;D2DynamicPathStrc* pDynamicPath=nullptr;D2MissileDataStrc* pMissileData=nullptr;D2SeedStrc pSeed{};}owner,actor,target,created;
struct D2MissilesTxt{int nSize=3,wSubMissile[1]={481},wHitSubMissile[3]={483,-1,-1},dwParam[5]={0,0,2,128,30};}row;
struct D2SkillsTxt{int dwAuraRangeCalc=12,dwCalc[4]={0,0,0,2};}skill;
struct D2MissileStrc{int dwFlags=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;int nSkill=0,nSkillLevel=0,nMissile=0,nX=0,nY=0,nTargetX=0,nTargetY=0,nGfxArg=0;};
D2DynamicPathStrc path;D2MissileDataStrc clockData;
bool hasSkill=true,hasRow=true,hasRoom=true,hasOwner=true,selected=false,allocate=true;
int progressive=479,bits=0,checks=0,cx=0,cy=0,size=0,mask=0,notifications=0,formulas=0,collisions=0;
std::vector<D2MissileStrc> requests;
int CLIENTS_GetUnitX(D2UnitStrc* p){assert(p);return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){assert(p);return p->y;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc* p){return p&&hasRoom?&room:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected?&target:nullptr;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){assert(p);return p->x;}int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc* p){assert(p);return p->y;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==244);return hasSkill?&skill:nullptr;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return hasRow?&row:nullptr;}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int id){assert(id==244);return progressive;}
int COLLISION_CheckMaskWithSizeXY(D2ActiveRoomStrc*,int x,int y,int sx,int sy,uint16_t m){assert(sx==sy);checks++;cx=x;cy=y;size=sx;mask=m;return bits&m;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return hasOwner?&owner:nullptr;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){requests.push_back(*p);return allocate?&created:nullptr;}
int MISSILE_GetSkill(D2UnitStrc* p){return p->skill;}int MISSILE_GetLevel(D2UnitStrc* p){return p->rank;}
int MISSILE_GetTargetX(D2UnitStrc* p){return static_cast<int32_t>(p->targetX);}void MISSILE_SetTargetX(D2UnitStrc* p,int x){p->targetX=x;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int value,int id,int level){assert(id==244&&level==20);formulas++;return value;}
int MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc*){collisions++;return 1;}
void sub_6FCC6300(D2UnitStrc* p,int zero,int id,int level,int x,int y,uint32_t seed){assert(p==&owner&&zero==0&&id==244&&level==20&&x==cx&&y==cy&&seed==created.targetX);notifications++;}
void reset(){owner={};actor={};target={};created={};row={};skill={};path={};clockData={};owner.dwUnitType=UNIT_PLAYER;owner.pDynamicPath=&path;owner.pSeed.lSeed=0x123456789ABCDEF0ULL;actor.pMissileData=&clockData;actor.targetX=0x87654321;actor.pSeed.lSeed=0xFEDCBA9876543210ULL;target.x=210;target.y=220;hasSkill=hasRow=hasRoom=hasOwner=allocate=true;selected=false;progressive=479;bits=checks=cx=cy=size=mask=notifications=formulas=collisions=0;requests.clear();}
void printRequests(){printf("[");for(size_t i=0;i<requests.size();i++){auto p=requests[i];printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",p.dwFlags,p.nMissile,p.nX,p.nY,p.nTargetX,p.nTargetY,p.nSkill,p.nSkillLevel,p.nGfxArg,p.pOwner==&owner,p.pOrigin==&actor,p.pOwner==nullptr);}printf("]");}
'''
    for relative, name in [
        ('source/D2Common/src/D2Seed.cpp', 'SEED_InitLowSeed'),
        ('source/D2Common/src/D2Seed.cpp', 'SEED_GetLowSeed'),
        ('source/D2Common/include/D2Seed.h', 'SEED_RollRandomNumber'),
        ('source/D2Common/include/D2Seed.h', 'SEED_RollLimitedRandomNumber'),
        ('source/D2Game/src/ITEMS/Items.cpp', 'ITEMS_RollRandomNumber'),
        ('source/D2Game/src/ITEMS/Items.cpp', 'ITEMS_RollLimitedRandomNumber'),
        ('source/D2Common/src/Units/Missile.cpp', 'MISSILE_GetRemainingFrames'),
        ('source/D2Game/src/SKILLS/Skills.cpp', 'D2GAME_GetXAndYFromTargetUnit_6FD14020'),
        ('source/D2Game/src/SKILLS/Skills.cpp', 'sub_6FD15340'),
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', 'SKILLS_SrvDo123_Volcano'),
        ('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_SrvDo28_Volcano'),
        ('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_SrvHit51_VolcanoDebris'),
    ]:
        cpp += routine(relative, name)
    cpp += r'''
int main(){bool first=true;printf("{\"launch\":[");
for(int guard=0;guard<6;guard++)for(bool select:{false,true})for(int x:{0,210})for(int y:{0,220})for(bool alloc:{false,true}){
reset();hasSkill=guard!=1;progressive=guard==2?0:479;hasRoom=guard!=3;bits=guard==4?1:guard==5?0x40:0;selected=select;allocate=alloc;path.x=target.x=x;path.y=target.y=y;
int ret=SKILLS_SrvDo123_Volcano(&game,&owner,244,20);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%llu,%u,",first?"":",",guard,select,x,y,alloc,ret,owner.dwFlags,checks,cx,cy,size,mask,(unsigned long long)owner.pSeed.lSeed,created.targetX);first=false;printf("%d,",notifications);printRequests();printf("]");}
printf("],\"emitter\":[");first=true;
for(int guard=0;guard<6;guard++)for(int elapsed=0;elapsed<=150;elapsed++)for(int config:{0,1,2})for(bool alloc:{false,true}){
reset();hasOwner=guard!=1;hasRow=guard!=2;hasSkill=guard!=3;row.wSubMissile[0]=guard==4?-1:481;clockData.nCurrentFrame=150-elapsed;allocate=alloc;
if(config==1){row.dwParam[0]=3;row.dwParam[1]=4;}if(config==2){skill.dwAuraRangeCalc=0;skill.dwCalc[3]=0;}
int ret=MISSMODE_SrvDo28_Volcano(&game,guard==5?nullptr:&actor);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%u,%llu,",first?"":",",guard,elapsed,config,alloc,ret,formulas,collisions,actor.targetX,(unsigned long long)actor.pSeed.lSeed);first=false;printRequests();printf("]");}
printf("],\"terminal\":[");first=true;
for(int guard=0;guard<4;guard++)for(int children:{0,1,3})for(bool victim:{false,true})for(bool alloc:{false,true}){
reset();hasOwner=guard!=1;hasRow=guard!=2;allocate=alloc;row.wHitSubMissile[0]=children?483:-1;if(children==3){row.wHitSubMissile[1]=484;row.wHitSubMissile[2]=485;}
int ret=MISSMODE_SrvHit51_VolcanoDebris(&game,guard==3?nullptr:&actor,victim?&target:nullptr);
printf("%s[%d,%d,%d,%d,%d,",first?"":",",guard,children,victim,alloc,ret);first=false;printRequests();printf("]");}
printf("],\"sequence\":[");reset();first=true;
for(int elapsed=0;elapsed<150;elapsed++){clockData.nCurrentFrame=150-elapsed;requests.clear();int ret=MISSMODE_SrvDo28_Volcano(&game,&actor);printf("%s[%d,%d,%u,%llu,",first?"":",",elapsed,ret,actor.targetX,(unsigned long long)actor.pSeed.lSeed);first=false;printRequests();printf("]");actor.pSeed.lSeed^=0x12345678ABCDEF00ULL;}
printf("]}\n");}
'''
    path = STAGE / 'volcano-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-Wall', '-Wextra', '-Werror', '-Wno-unused-parameter',
                    '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'volcano-source-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)

    def roll(seed):
        seed = ((seed & 0xFFFFFFFF) * 0x6AC690C5 + (seed >> 32)) & 0xFFFFFFFFFFFFFFFF
        return seed, seed & 0xFFFFFFFF

    initial = 0x123456789ABCDEF0
    advanced, low = roll(initial)
    for guard, selected, x, y, allocated, result, flags, checks, cx, cy, size, mask, seed, memory, notifications, requests in data['launch']:
        gate = guard not in (1, 2) and x != 0 and y != 0 and guard != 3
        launched = gate and guard != 4
        assert (result, flags, checks, notifications) == (int(launched and allocated), 0x100 * launched, int(gate), int(launched and allocated))
        assert (seed, memory) == (advanced if launched else initial, low if launched and allocated else 0)
        if gate:
            assert (cx, cy, size, mask) == (x, y, 3, 5)
        assert requests == ([[1, 479, x, y, 0, 0, 244, 20, 0, 1, 0, 0]] if launched else [])

    def expected_emit(memory, radius):
        seed, x = roll((666 << 32) | memory)
        seed, y = roll(seed)
        return seed, y, [[1312, 481, 0, 0, 300 + x % (radius * 2 + 1) - radius,
                         400 + y % (radius * 2 + 1) - radius, 244, 20, 30, 1, 1, 0]]

    for guard, elapsed, config, allocated, result, formulas, collisions, memory, seed, requests in data['emitter']:
        valid = guard not in (2, 3, 4, 5)
        interval, radius = [(2, 12), (3, 4), (1, 1)][config]
        emitting = valid and guard != 1 and 2 < elapsed < 128 and elapsed % interval == 0
        assert (result, formulas, collisions) == (1 if valid else 2, 2 * int(valid and guard != 1 and config != 1), int(valid))
        expected = expected_emit(0x87654321, radius) if emitting else (0xFEDCBA9876543210, 0x87654321, [])
        assert (seed, memory, requests) == expected
    for guard, children, victim, allocated, result, requests in data['terminal']:
        assert result == 1
        assert requests == ([[0, child, 0, 0, 0, 0, 244, 20, 0, int(guard != 1), 1, int(guard == 1)]
                             for child in range(483, 483 + children)] if guard not in (2, 3) else [])
    memory = 0x87654321
    emits = 0
    for elapsed, result, saved, seed, requests in data['sequence']:
        if 2 < elapsed < 128 and elapsed % 2 == 0:
            expected_seed, memory, expected_requests = expected_emit(memory, 12)
            assert (seed, requests) == (expected_seed, expected_requests)
            emits += 1
        else:
            assert not requests
        assert result == 1 and saved == memory
    assert emits == 62
    counts = {key: len(value) for key, value in data.items()}
    assert counts == dict(launch=96, emitter=5436, terminal=48, sequence=150)
    fixture = STAGE / 'volcano-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
                    sourceHashes=sources, compiledUnchangedRoutines=blocks, pinnedInputHashes=preflight['pinnedInputHashes'],
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), caseCounts=counts),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'Complete do123, target resolver and Size3 placement gate use selected target before path aim, require both nonzero coordinates and test mask5. Mask0x40 control does not reject this initial gate. Skill flag and one caster RNG draw follow placement; failed allocation still consumes that draw. Successful root request flags1 stores the draw in TargetX and notifies the client.',
                        'Complete do28 with elapsed getter, seed setters/getters and actual random/item wrappers emits62 opportunities at elapsed4,6,...126. Fallback formulas clamp nonpositive values to1; positive row overrides avoid the formula calls. Missing owner skips emission but still delegates collision, while missing row/skill/child or missile returns2.',
                        'Each emission resets high666 and low from saved TargetX, draws inclusive square offsets[-12,+12], saves the second low and requests flags1312, original owner, root origin, child481, skill244/rank and graphics30. Allocation failure does not roll back that memory. Deliberate between-frame collision-seed perturbations cannot alter the62-emission pattern.',
                        'Complete hit51 makes one current child483 request with default flags0 and inherited owner/origin/skill/rank. Null victim still creates it; victim presence does not change it. Controls exercise missing owner and all three optional children; owner-null rejection belongs to the separate factory.'
                    ], services=[
                        'Declared row/formula/room/target-coordinate/collision and allocation services record boundaries; complete factory, collision/update router, damage creator, movement and native input do not execute here.',
                        'Generic collision is a recording service; this sequence proves emitter frame inputs, not clock decrement or room termination. Modern executable, modern delay grouping, source RNG/room geometry and final assets remain separate acceptance limits.'
                    ], next='Compose complete factory and generic movement/terminal router before implementing saved native vent/debris/lava mechanics.')
    out = ROOT / 'tools/reference/volcano-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == rendered
    else:
        out.write_text(rendered)
    print('Volcano:', counts, 'complete source cases pass;62 persistent-seed emissions; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
