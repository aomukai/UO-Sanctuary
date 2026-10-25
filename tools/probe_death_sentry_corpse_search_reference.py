#!/usr/bin/env python3
"""Compose complete Death Sentry corpse search, common predicate and room finder.

Rooms, ordered linked lists, model/state lookup and allocation are explicit
services. This does not certify a current executable or native room mapping.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
parser = argparse.ArgumentParser()
parser.add_argument('--check', action='store_true')
args = parser.parse_args()
tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
hashes, routines = {}, {}


def block(relative, name):
    raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    hashes[relative] = hashlib.sha256(raw).hexdigest()
    text = raw.decode()
    m = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
    assert m, name
    end, depth = text.index('{', m.start()) + 1, 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    body = text[m.start():end]
    routines[name] = hashlib.sha256(body.encode()).hexdigest()
    return body + '\n'


cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <initializer_list>
#define __fastcall
#define __stdcall
#define D2_ASSERT assert
#define REMOVE_LATER_Trace(x) ((void)0)
using BOOL=int;constexpr int TRUE=1,FALSE=0;
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_OBJECT=2,UNIT_MISSILE=3,UNIT_ITEM=4;
constexpr int MONMODE_DEAD=12,MONMODE_DEATH=0,PLRMODE_DEAD=17,PLRMODE_DEATH=0;
constexpr int MONSTATS2FLAGINDEX_CORPSESEL=7,MISSILESFLAGINDEX_EXPLOSION=0;
constexpr unsigned UNITFLAG_CANBEATTACKED=4,UNITFLAG_ISVALIDTARGET=8;
constexpr int UNIT_FIND_ARRAY_SIZE=15;
unsigned gdwBitMasks[32];
struct D2ActiveRoomStrc;
struct D2UnitStrc{int dwUnitType=1,dwAnimMode=12,dwClassId=0,dwUnitId=0,x=0,y=0;unsigned dwFlags=0;bool masked=false;D2ActiveRoomStrc* room=nullptr;D2UnitStrc* pRoomNext=nullptr;};
struct D2CoordStrc{int nX=0,nY=0;};
struct D2DrlgCoordsStrc{int nSubtileX=0,nSubtileY=0,nSubtileWidth=200,nSubtileHeight=200;};
struct D2ActiveRoomStrc{D2DrlgCoordsStrc coords;D2UnitStrc* pUnitFirst=nullptr;};
struct D2MonStats2Txt{unsigned dwFlags=0;}models[2];
struct D2MissilesTxt{unsigned dwMissileFlags=0;};
struct D2GameStrc{void* pMemoryPool=nullptr;}game;
struct D2UnitFindArgStrc;
using UNITFINDTEST=int(*)(D2UnitStrc*,D2UnitFindArgStrc*);
struct D2UnitFindArgStrc{int nFlags=0;D2UnitStrc* pUnit=nullptr;int nX=0,nY=0,nSize=0,nMaxArrayEntries=0,nIndex=0;void* pField=nullptr;UNITFINDTEST pfnUnitTest=nullptr;};
struct D2UnitFindDataStrc{void* pMemPool=nullptr;D2UnitStrc** pUnitsArray=nullptr;D2ActiveRoomStrc* pRoom=nullptr;UNITFINDTEST pfnUnitTest=nullptr;D2UnitFindArgStrc* pUnitFindArg=nullptr;int nFlags=0,nIndex=0,nMaxArrayEntries=0,nX=0,nY=0,nSize=0;};
int allocations=0,frees=0,reallocations=0;
void* Allocate(size_t size){++allocations;return std::calloc(1,size);}
void* Reallocate(void* ptr,size_t size){++reallocations;return std::realloc(ptr,size);}
void Free(void* ptr){++frees;std::free(ptr);}
#define D2_CALLOC_POOL(pool,size) Allocate(size)
#define D2_REALLOC_POOL(pool,ptr,size) Reallocate(ptr,size)
#define D2_FREE_POOL(pool,ptr) Free(ptr)
D2ActiveRoomStrc* adjacent[2];
using FARPROC=void(*)();bool IsBadCodePtr(FARPROC){return false;}
void DUNGEON_GetRoomCoordinates(D2ActiveRoomStrc* r,D2DrlgCoordsStrc* out){*out=r->coords;}
void DUNGEON_GetAdjacentRoomsListFromRoom(D2ActiveRoomStrc*,D2ActiveRoomStrc*** out,int* count){*out=adjacent;*count=2;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return false;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc* p){return p->room;}
void UNITS_GetCoords(D2UnitStrc* p,D2CoordStrc* out){out->nX=p->x;out->nY=p->y;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
D2GameStrc* SUNIT_GetGameFromUnit(D2UnitStrc*){return &game;}
D2MonStats2Txt* UNITS_GetMonStats2TxtRecordFromMonsterId(int id){return id<0?nullptr:&models[id];}
BOOL STATES_CheckStateMaskUdeadOnUnit(D2UnitStrc* p){return p->masked;}
// These generic-finder lanes are retained but cannot execute under4098 and
// the common corpse predicate. Their services are deliberately not supplied.
bool MONSTERS_IsUndead(D2UnitStrc*){assert(false);return false;}
D2MissilesTxt* DATATBLS_GetMissilesTxtRecord(int){assert(false);return nullptr;}
bool D2Common_11098(void*,int*,int*){assert(false);return false;}
int COLLISION_CheckMask(D2ActiveRoomStrc*,int,int,int){assert(false);return 0;}
int UNITFINDS_TestUnit(D2UnitStrc*,D2UnitFindArgStrc*);
'''
cpp += block('source/D2Common/src/D2Skills.cpp', 'SKILLS_CanUnitCorpseBeSelected')
cpp += block('source/D2Common/src/UselessOrdinals.cpp', 'D2COMMON_11021')
for name in ['UNITFINDS_InitializeUnitFindData', 'UNITFINDS_FreeUnitFindData', 'UNITFINDS_FindAllMatchingUnitsInNeighboredRooms', 'UNITFINDS_TestUnit']:
    cpp += block('source/D2Common/src/Units/UnitFinds.cpp', name)
for name in ['sub_6FD15320', 'sub_6FD15210']:
    cpp += block('source/D2Game/src/SKILLS/Skills.cpp', name)
cpp += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;models[0].dwFlags=gdwBitMasks[MONSTATS2FLAGINDEX_CORPSESEL];
int permutations[6][3]={{0,1,2},{0,2,1},{1,0,2},{1,2,0},{2,0,1},{2,1,0}};
std::printf("{\"cases\":[");int count=0;
for(int caller=0;caller<4;caller++)for(int rank:{-1,0,1,20})for(int haveRoom:{0,1})for(int mode:{0,1,2})
for(int order=0;order<6;order++)for(int mask=0;mask<8;mask++)for(int offset:{0,1})for(int variant=0;variant<5;variant++){
 D2ActiveRoomStrc rooms[2];D2UnitStrc actor,target,units[3];target.x=target.y=100;target.room=haveRoom?&rooms[0]:nullptr;
 for(int i=0;i<3;i++){units[i].x=100+(i==0?2:i==1?8:10+offset);units[i].y=100;units[i].dwUnitId=i+1;units[i].dwClassId=(mask&(1<<i))?0:1;units[i].room=&rooms[0];}
 if(variant==1)units[1].dwAnimMode=1;
 if(variant==2)units[1].dwUnitType=0;
 if(variant==3)units[1].masked=true;
 if(variant==4)units[1].dwClassId=-1;
 int* p=permutations[order];rooms[0].pUnitFirst=&units[p[0]];units[p[0]].pRoomNext=&units[p[1]];
 if(mode==0){units[p[1]].pRoomNext=&units[p[2]];}
 else{rooms[0].coords.nSubtileX=100;rooms[0].coords.nSubtileWidth=20;rooms[1].coords.nSubtileX=90;rooms[1].coords.nSubtileWidth=20;rooms[1].pUnitFirst=&units[p[2]];}
 adjacent[0]=&rooms[mode==2?1:0];adjacent[1]=&rooms[mode==2?0:1];
 allocations=frees=reallocations=0;
 auto* result=sub_6FD15210((caller&1)?&actor:nullptr,(caller&2)?&target:nullptr,312,rank);
 int expected=0;bool entered=caller==3&&rank>0;
 if(entered&&haveRoom){for(int i=0;i<3;i++){int index=mode==2?p[(i+2)%3]:p[i];if((mask&(1<<index))&&!(index==1&&variant>0)&&!(index==2&&offset==1)){expected=index+1;break;}}}
 assert((result?result->dwUnitId:0)==expected&&allocations==int(entered)&&frees==int(entered)&&reallocations==0);
 std::printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",count?",":"",caller,rank,haveRoom,mode,order,mask,offset,variant,expected,allocations,frees);++count;
}
assert(count==46080);std::printf("],\"growth\":[");
for(int reverse:{0,1}){
 D2ActiveRoomStrc room;D2UnitStrc actor,target,units[20];target.x=target.y=100;target.room=&room;
 for(int i=0;i<20;i++){units[i].x=100+i%10;units[i].y=100;units[i].dwUnitId=i+1;units[i].room=&room;}
 for(int i=0;i<19;i++){units[reverse?19-i:i].pRoomNext=&units[reverse?18-i:i+1];}
 room.pUnitFirst=&units[reverse?19:0];allocations=frees=reallocations=0;
 auto* result=sub_6FD15210(&actor,&target,312,1);assert(result==room.pUnitFirst&&allocations==1&&frees==1&&reallocations==1);
 std::printf("%s[%d,%d,%d,%d,%d]",reverse?",":"",reverse,result->dwUnitId,allocations,frees,reallocations);
}std::printf("]}\n");}
'''
source = STAGE / 'death-sentry-corpse-search-source-probe.cpp'
source.write_text(cpp)
binary = source.with_suffix('')
subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(source), '-o', str(binary)], check=True)
run = subprocess.run([str(binary)], capture_output=True)
(STAGE / 'death-sentry-corpse-search-sanitizer.log').write_bytes(run.stderr)
assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-2000:]
output = json.loads(run.stdout)
assert len(output['cases']) == 46080 and len(output['growth']) == 2
fixture = STAGE / 'death-sentry-corpse-search-source-probe.json'
fixture.write_text(json.dumps(output, separators=(',', ':')) + '\n')
evidence = dict(
    referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceHashes=hashes, routineHashes=routines,
    commonCorpseEvidence='tools/reference/sentry-corpse-source-evidence.json',
    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), searchCases=46080, arrayGrowthCases=2),
    established=[
        'Complete search wrapper uses a fixed10-unit inclusive squared-radius query centered on the chosen enemy, independent of positive skill rank; null caller/target or nonpositive rank returns before allocation.',
        'Complete common corpse predicate, ordinal wrapper and callback exclude live/nonmonster/undead-masked/missing-model/nonselectable units before the4098 generic finder filter.',
        'The result is the first eligible corpse in room-list/unit-linked-list order, not the nearest corpse. Adjacent-room order can change the result. The separate AI104 explosion-distance/remembered-GUID gate occurs after selection.',
        'Roomless searches return no corpse and free the allocated array. Twenty-corpse controls exercise the complete finder growth15-to30 and free path without changing first-result order.'
    ],
    limits=[
        'Rooms, coordinates, ordered lists, allocation, model/state lookup and game/pool services are explicit fixtures. This is not a modern executable or binary-layout certificate.',
        'Only flags4098 and the common corpse callback execute; unrelated generic finder flags, missile/undead/collision/custom-callback branches are retained but not certified.',
        'Actual native world/corpse eligibility, room/sector traversal order, loot/ownership rules, saved corpses, damage and retirement remain separate. Current trap/zombie model rows are corroborated by the linked common-corpse proof.'
    ], sanitizers='ASan/UBSan exit0 with empty runtime stderr required', handlerEnabled=False)
path = ROOT / 'tools/reference/death-sentry-corpse-search-source-evidence.json'
if args.check:
    assert json.loads(path.read_text()) == evidence
else:
    path.write_text(json.dumps(evidence, indent=2) + '\n')
print('Death Sentry corpse search:46080complete predicate/wrapper/room-finder cases plus2array-growth controls; clean sanitizers; native mapping remains separate.')
