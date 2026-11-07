#!/usr/bin/env python3
"""Execute both complete older startup routines against declared host services."""
import argparse
import hashlib
import itertools
import json
from pathlib import Path
import random
import re
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'skill-startup-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(exist_ok=True)
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs = {}

    def read(relative):
        p = BASE / 'd2moo-pinned-source' / relative
        raw = p.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(r['sha'] for r in tree['tree'] if r['path'] == relative)
        inputs[str(p.relative_to(ROOT))] = sha(p)
        return raw.decode()

    source = read('source/D2Game/src/SKILLS/Skills.cpp')
    names = ['sub_6FD12950', 'sub_6FD12FD0']
    bodies = [function(source, name) for name in names]
    at = source.index('constexpr SkillStartFunc gpSkillSrvStartFnTable_6FD408B0[]')
    initializer = re.sub(r'//[^\n]*|/\*.*?\*/', '', source[at:source.index('};', at)], flags=re.S).split('{', 1)[1]
    count = len([v for v in initializer.split(',') if v.strip()])
    header = read('source/D2Common/include/DataTbls/SkillsTbls.h')
    collision = read('source/D2Common/include/D2Collision.h')
    monsters = read('source/D2Common/include/D2Monsters.h')
    players = read('source/D2Common/include/Units/Player.h')
    flags = re.search(r'enum D2C_SkillsTxtFlags\s*\{(.*?)\};', header, re.S)
    # Use the actual index enum, retaining its separate flags2 restart.
    indices = re.search(r'enum D2C_SkillsTxtFlagIndices\s*\{.*?\};', header, re.S).group()
    los = re.search(r'enum \w+\s*\{\s*SKILLSLOS_NONE.*?\};', header, re.S).group()
    masks = re.search(r'enum D2C_CollisionMaskFlags : uint16_t\s*\{.*?\};', collision, re.S).group()
    assert monsters[monsters.index('MONMODE_DEATH'):monsters.index('MONMODE_DEAD')].count(',') == 12
    assert players[players.index('PLRMODE_DEATH,'):players.index('PLRMODE_TOWNNEUTRAL,')].count(',') == 5
    cpp = r'''
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#include <iterator>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,MONMODE_DEAD=12,PLRMODE_TOWNNEUTRAL=5,EVENTTYPE_PERIODICSKILLS=9;
'''+indices+'\n'+los+'\n'+masks+r'''
uint32_t gdwBitMasks[32];
struct D2GameStrc{}game;
struct D2ActiveRoomStrc{}room;
struct D2SkillStrc{int id=66;}used;
struct D2DynamicPathStrc{}path;
struct D2UnitStrc{int dwUnitType=1,dwAnimMode=1;uint32_t dwFlags=0;D2DynamicPathStrc*pDynamicPath=&path;}unit,target;
constexpr uint32_t UNITFLAG_TARGETABLE=2;
struct D2SkillsTxt{uint32_t dwFlags[2]={};int nLineOfSight=0;int16_t wSrvStartFunc=0;}record;
std::array<int,30> c;bool targetPresent=false,usedCleared=false,targetCleared=false;std::vector<std::vector<int>>trace;
D2SkillStrc*UNITS_GetUsedSkill(D2UnitStrc*){trace.push_back({0});return c[4]?&used:nullptr;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc*s,const char*,int){return s->id;}
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int skill){trace.push_back({1,skill});return c[5]?&record:nullptr;}
D2UnitStrc*SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){trace.push_back({2});return targetPresent?&target:nullptr;}
int sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){trace.push_back({3});return c[10];}
int SUNIT_CanPetBeTargetedBySkill(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int skill){trace.push_back({4,skill});return c[11];}
int SUNIT_CanAllyBeTargetedBySkill(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int skill){trace.push_back({5,skill});return c[12];}
void UNITS_SetTargetUnitForDynamicUnit(D2UnitStrc*,D2UnitStrc*){trace.push_back({6});targetPresent=false;targetCleared=true;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int){trace.push_back({7});return c[13];}
int SKILLS_GetSkillInfo(D2SkillStrc*,void*,void*,int*rank,int*charges){trace.push_back({8});if(!c[14])return 0;*rank=c[16];*charges=c[15];return 1;}
D2ActiveRoomStrc*UNITS_GetRoom(D2UnitStrc*){trace.push_back({9});return c[17]?&room:nullptr;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*r){return r&&c[17]==1;}
void UNITS_SetUsedSkill(D2UnitStrc*,D2SkillStrc*){trace.push_back({10});usedCleared=true;}
void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,int,int mode,int,int,int){assert(mode==5);trace.push_back({11});}
int sub_6FD10CE0(D2UnitStrc*){trace.push_back({12});return c[19];}
int CLIENTS_GetUnitX(D2UnitStrc*){return c[21]==1?0:23;}
int CLIENTS_GetUnitY(D2UnitStrc*){return c[21]==2?0:29;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc*){trace.push_back({13});return c[21]==1?0:17;}
int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc*){return c[21]==2?0:19;}
int D2Common_11026(int x,int y,D2UnitStrc*,uint32_t mask){trace.push_back({14,x,y,(int)mask});return c[22];}
using SkillStartFunc=int(*)(D2GameStrc*,D2UnitStrc*,int,int);
int Start(D2GameStrc*,D2UnitStrc*,int skill,int rank){trace.push_back({15,record.wSrvStartFunc,skill,rank});return c[25];}
std::array<SkillStartFunc,START_COUNT>gpSkillSrvStartFnTable_6FD408B0;
void D2GAME_SKILLMANA_Consume_6FD10A50(D2GameStrc*,D2UnitStrc*,int skill,int rank){trace.push_back({16,skill,rank});}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int event,int data){assert(event==EVENTTYPE_PERIODICSKILLS&&data==0);trace.push_back({17});}
'''.replace('START_COUNT', str(count))+'\n'.join(bodies)+r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;int n;assert(scanf("%d",&n)==1);printf("[");
for(int i=0;i<n;i++){for(int&v:c)assert(scanf("%d",&v)==1);unit.dwUnitType=c[3];record={};
record.dwFlags[0]=(c[7]?gdwBitMasks[SKILLSFLAGINDEX_TARGETABLEONLY]:0)|(c[8]?gdwBitMasks[SKILLSFLAGINDEX_TARGETCORPSE]:0)|(c[9]?gdwBitMasks[SKILLSFLAGINDEX_TARGETALLY]:0)|(c[18]?gdwBitMasks[SKILLSFLAGINDEX_INTOWN]:0)|(c[26]?gdwBitMasks[SKILLSFLAGINDEX_PERIODIC]:0);
record.dwFlags[1]=c[27]?gdwBitMasks[SKILLSFLAGINDEX2_USEMANAONDO]:0;record.nLineOfSight=c[20];record.wSrvStartFunc=c[23];
target.dwUnitType=c[6]==3?0:1;target.dwAnimMode=c[6]==2?12:1;target.dwFlags=c[6]==4?0:UNITFLAG_TARGETABLE;
targetPresent=c[6]!=0;targetCleared=usedCleared=false;gpSkillSrvStartFnTable_6FD408B0.fill(c[24]?&Start:nullptr);trace.clear();
auto*g=c[1]?&game:nullptr;auto*u=c[2]?&unit:nullptr;
int result=c[0]?sub_6FD12FD0(g,u):sub_6FD12950(g,u,66,c[29],c[28]);
printf("%s[",i?",":"");printf("[");for(int j=0;j<30;j++)printf("%s%d",j?",":"",c[j]);printf("],[%d,%d,%d],[",result,usedCleared?1:0,targetCleared?1:0);
bool first=true;for(auto&t:trace){printf("%s[",first?"":",");first=false;for(size_t j=0;j<t.size();j++)printf("%s%d",j?",":"",t[j]);printf("]");}printf("]]");}printf("]\n");}
'''
    path = STAGE / 'startup.cpp'
    path.write_text(cpp)
    compiler = ['g++', '-std=c++17', '-O1', '-g', '-fsanitize=address,undefined', '-fno-omit-frame-pointer', '-fno-pie', '-no-pie', str(path), '-o', str(STAGE / 'startup')]
    subprocess.run(compiler, check=True)
    baseline = [0,1,1,1,1,1,1,0,0,0,1,0,0,3,0,0,7,2,0,1,0,0,1,0,0,1,0,0,0,3]
    cases = []
    def add(**values):
        case = baseline.copy()
        for i, value in values.items():
            case[int(i)] = value
        cases.append(case)
    # Exhaust the target permission/cleanup short-circuit combinations.
    for target_kind, only, corpse, ally, enemy_ok, pet_ok, ally_ok in itertools.product(range(5), *([range(2)]*6)):
        add(**{'0':1,'6':target_kind,'7':only,'8':corpse,'9':ally,'10':enemy_ok,'11':pet_ok,'12':ally_ok})
    # Exhaust direct startup's dispatch/payment/periodic and LOS boundaries.
    for item, has_start, result, periodic, on_do, line, zero in itertools.product(range(2),range(2),[-1,0,1,7],range(2),range(2),range(-1,8),range(3)):
        add(**{'28':item,'24':has_start,'25':result,'26':periodic,'27':on_do,'20':line,'21':zero})
    for entry, room_state, unit_type, present, row, mana, in_town in itertools.product(range(2),range(3),[0,1,3],range(3),range(2),range(2),range(2)):
        add(**{'0':entry,'17':room_state,'3':unit_type,'1':int(present!=1),'2':int(present!=2),'5':row,'19':mana,'18':in_town})
    for entry, owned, charges, effective, base in itertools.product(range(2),range(2),[-1,0,1,5],[0,1,3,99],[0,7,63]):
        add(**{'0':entry,'14':owned,'15':charges,'13':effective,'16':base})
    for entry, start, used in itertools.product(range(2),[-32768,-1,0,1,count-1,count,32767],range(2)):
        add(**{'0':entry,'23':start,'4':used,'24':1})
    rng = random.Random(93847)
    domains = [[0,1],[0,1],[0,1],[0,1,3],[0,1],[0,1],list(range(5)),*[([0,1])]*6,[0,1,20,99],[0,1],[-1,0,1],[0,7,63],[0,1,2],[0,1],[0,1],list(range(-1,8)),[0,1,2],[0,1],[-1,0,1,count-1,count],[0,1],[-1,0,1,7],[0,1],[0,1],[0,1],[0,1,20,99]]
    assert len(domains) == 30
    cases.extend([[rng.choice(d) for d in domains] for _ in range(4096)])
    data = str(len(cases))+'\n'+'\n'.join(' '.join(map(str,c)) for c in cases)+'\n'
    result = subprocess.run([str(STAGE / 'startup')], input=data, text=True, capture_output=True, check=True)
    assert not result.stderr, result.stderr
    controls = json.loads(result.stdout)
    assert len(controls) == len(cases)
    (STAGE / 'cases.json').write_text(json.dumps(controls, separators=(',',':'))+'\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], inputs=inputs,
        routines={name:hashlib.sha256(body.encode()).hexdigest() for name,body in zip(names,bodies)},
        startFunctionCount=count, cases=len(cases), casesSha256=sha(STAGE/'cases.json'),
        cppSha256=sha(path), compiler=compiler[:9], sanitizers='address,undefined:clean',
        scope='Both complete pinned older routines execute. Nested permission, rank, charged ownership, room, collision, start-body, payment and event services are declared inputs, not source implementations or current executable parity.',
        currentExecutableParity=False, liveNpcControllerEnabled=False,
        remaining=['Real NPC permission and collision adapters','Nested skill start bodies','Mode/sequence/action events','Ordinary effect/combat hosts','AI105/106 and learned Shadow casts'])
    out = ROOT/'tools/reference/skill-startup-source-evidence.json'
    rendered = json.dumps(evidence, indent=2)+'\n'
    if check:
        assert out.read_text() == rendered, 'Startup source evidence is stale'
    else:
        out.write_text(rendered)
    print(f'Complete older startup wrapper/dispatcher: {len(cases)} cases, {count} start slots, ASan/UBSan clean')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
