#!/usr/bin/env python3
"""Execute complete older item allocation/seed routines with explicit world services."""
import hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-item-allocation-source';PIN='0c0bbd1e6bef9181a3da1c046523af622a925416'
def main():
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==PIN
 hashes={};blocks={}
 def source(path):
  raw=(BASE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==path)
  hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
  end=text.index('{',m.start())+1;d=1
  while d:d+=(text[end]=='{')-(text[end]=='}');end+=1
  raw=text[m.start():end];blocks[name]=hashlib.sha256(raw.encode()).hexdigest();return raw+'\n'
 unit=source('source/D2Game/src/UNIT/SUnit.cpp');common=source('source/D2Common/src/Items/Items.cpp');seed=source('source/D2Common/src/D2Seed.cpp');mode=source('source/D2Game/src/ITEMS/ItemMode.cpp');items=source('source/D2Game/src/ITEMS/Items.cpp')
 routines=''.join(routine(seed,n)for n in ['SEED_InitSeed','SEED_InitLowSeed'])
 routines+=routine(items,'ITEMS_RollRandomNumber')
 routines+=''.join(routine(common,n)for n in ['ITEMS_AllocItemData','ITEMS_InitItemSeed','ITEMS_GetItemStartSeed','ITEMS_SetItemStartSeed'])
 routines+=routine(unit,'SUNIT_InitSeed')+routine(mode,'sub_6FC4BE80')+routine(unit,'SUNIT_AllocUnitData')
 header=source('source/D2Common/include/D2Items.h')
 for name,value in [('IDENTIFIED','00000010'),('INSTORE','00002000'),('INIT','00080000'),('NOSELL','00001000')]:assert re.search(r'IFLAG_'+name+r'\s*=\s*0x'+value,header)
 cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#define __fastcall
#define __stdcall
#define D2_ASSERT assert
#define D2_ALLOC_STRC_POOL(pool,type) (&itemData)
#define D2_CALLOC_STRC_POOL(pool,type) (&objectData)
using DWORD=uint32_t;
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_OBJECT=2,UNIT_MISSILE=3,UNIT_ITEM=4,UNIT_TILE=5;
constexpr int MONSTATSFLAGINDEX_ENABLED=0,MONSTATS2FLAGINDEX_DEADCOL=0,PLRMODE_DEAD=17,PLRMODE_DEATH=0,COLLISION_PATTERN_SMALL_NO_PRESENCE=0,COLLIDE_CORPSE=0;
// Unit flags are named synthetic services;their binary values are not claims.
constexpr uint32_t UNITFLAGEX_ISEXPANSION=1,UNITFLAGEX_SERVERUNIT=2,UNITFLAG_INITSEEDSET=4,UNITFLAG_TARGETABLE=8,UNITFLAG_ISVALIDTARGET=16;
constexpr uint32_t gdwBitMasks[]={1},D2UnitInvalidGUID=0xffffffff,IFLAG_IDENTIFIED=16;
struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};
struct D2ItemDataStrc{D2SeedStrc pSeed;uint32_t dwInitSeed,dwOwnerGUID,flags;};D2ItemDataStrc itemData;
struct D2ObjectDataStrc{void*pObjectTxt;};D2ObjectDataStrc objectData;
struct D2ActiveRoomStrc{};struct D2MonStatsTxt{uint32_t dwMonStatsFlags;};struct D2MonStats2Txt{uint32_t dwFlags;};struct D2ItemsTxt{int nQuest;};D2ItemsTxt itemRow{0};
struct D2GameStrc{void*pMemoryPool;bool bExpansion;D2SeedStrc pGameSeed;void*pAct[5];uint32_t dwLastUsedUnitGUID[6];};
struct D2UnitStrc{int dwUnitType,dwClassId,nAct,dwNodeIndex,dwAnimMode;D2GameStrc*pGame;void*pMemoryPool,*pDrlgAct,*pQuestEventList,*pDynamicPath;uint32_t dwInitSeed,dwFlags,dwFlagEx,dwUnitId;D2SeedStrc pSeed;D2ItemDataStrc*pItemData;D2ObjectDataStrc*pObjectData;};D2UnitStrc allocated;
bool failAllocation=false;int dword_6FD4DC40=0;
uint64_t SEED_RollRandomNumber(D2SeedStrc*s){return s->lSeed=(uint64_t)s->nHighSeed+0x6AC690C5ULL*s->nLowSeed;}
int SEED_GetRandomValue(int){std::abort();}
D2ItemDataStrc* ITEMS_GetItemData(D2UnitStrc*u){return u&&u->dwUnitType==4?u->pItemData:nullptr;}
D2SeedStrc* ITEMS_GetItemSeed(D2UnitStrc*u){return &u->pItemData->pSeed;}
void ITEMS_SetItemFlag(D2UnitStrc*u,int flag,int value){if(value)u->pItemData->flags|=flag;else u->pItemData->flags&=~flag;}
D2UnitStrc* UNITS_AllocUnit(void*,int){if(failAllocation)return nullptr;allocated={};return &allocated;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){std::abort();}D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){std::abort();}
int DUNGEON_GetLevelIdFromRoom(D2ActiveRoomStrc*){return 0;}int DRLG_GetActNoFromLevelId(int){return 0;}
void STATLIST_AllocStatListEx(D2UnitStrc*,int,void*,void*){}
D2ItemsTxt* DATATBLS_GetItemsTxtRecord(int){return &itemRow;}
void QUESTS_CreateChainRecord(D2GameStrc*,D2UnitStrc*,int){std::abort();}void D2GAME_DeleteTimersOnUnit_6FC34A30(D2GameStrc*,D2UnitStrc*){}
void UNITS_AllocStaticPath(D2UnitStrc*){}
void SUNIT_Add(D2UnitStrc*,int,int,D2GameStrc*,D2ActiveRoomStrc*,int){}
void PLAYER_Create(D2GameStrc*,D2UnitStrc*,uint32_t){std::abort();}void D2GAME_TARGETS_First_6FC40170(D2GameStrc*,D2UnitStrc*,int){std::abort();}
void MONSTER_Initialize(D2GameStrc*,D2ActiveRoomStrc*,D2UnitStrc*,uint32_t){std::abort();}
void* DATATBLS_GetObjectsTxtRecord(int){std::abort();}void OBJECTS_InitHandler(D2GameStrc*,D2UnitStrc*,uint32_t,D2ActiveRoomStrc*,int,int){std::abort();}
void MISSILES_Initialize(D2GameStrc*,D2UnitStrc*,uint32_t){std::abort();}
bool MONSTERS_IsDead(D2UnitStrc*){std::abort();}void PATH_RemoveCollisionFootprintForUnit(D2UnitStrc*,int){std::abort();}void PATH_SetUnitCollisionPattern(D2UnitStrc*,int){std::abort();}void PATH_SetFootprintCollisionMask(void*,int){std::abort();}
'''+routines+r'''
int main(){bool first=true;std::printf("[");
for(uint64_t initial:{0ULL,1ULL,666ULL,0xffffffffULL,0x100000000ULL,0x29a00000000ULL,0x29a00000001ULL,0x29affffffffULL,0xffffffffffffffffULL,0xdeadbeef12345678ULL})
for(int count:{1,2,5,10})for(int expansion:{0,1})for(int add:{0,1})for(uint32_t previous:{0u,0xfffffffeu,0xffffffffu})for(int fail:{0,1}){
D2GameStrc game{};game.bExpansion=expansion;game.pGameSeed.lSeed=initial;game.dwLastUsedUnitGUID[4]=previous;failAllocation=fail;
if(!first)std::printf(",");first=false;
std::printf("[\"%llu\",%d,%d,%d,%u,%d,[",(unsigned long long)initial,count,expansion,add,previous,fail);
for(int i=0;i<count;i++){
auto*u=SUNIT_AllocUnitData(4,0,0,0,&game,nullptr,add,4,0);if(i)std::printf(",");
if(u)std::printf("[%u,%u,\"%llu\",%u,\"%llu\",%u]",u->dwUnitId,u->dwInitSeed,(unsigned long long)u->pSeed.lSeed,u->pItemData->dwInitSeed,(unsigned long long)u->pItemData->pSeed.lSeed,u->pItemData->flags);else std::printf("null");
}std::printf("],\"%llu\"]",(unsigned long long)game.pGameSeed.lSeed);
}std::printf("]\n");}
'''
 cppfile=STAGE/'probe.cpp';cppfile.write_text(cpp);binary=STAGE/'probe'
 subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(cppfile),'-o',str(binary)],check=True)
 with (STAGE/'sanitizers.log').open('w')as err:r=subprocess.run([str(binary)],stdout=subprocess.PIPE,stderr=err,check=True)
 assert not(STAGE/'sanitizers.log').read_text();cases=json.loads(r.stdout);assert len(cases)==960
 def roll(s):s=(s&0xffffffff)*0x6ac690c5+(s>>32);return s,s&0xffffffff
 for initial,count,exp,add,previous,fail,rows,after in cases:
  game=int(initial);guid=previous
  for row in rows:
   if fail:assert row is None;continue
   game,u=roll(game);game,item=roll(game);guid=1 if guid==0xfffffffe else(guid+1)&0xffffffff
   assert row==[guid,u,str((666<<32)|u),item,str((666<<32)|item),16]
  assert str(game)==after
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 proof=dict(referenceVersion='3.3.93847',legacyCommit=PIN,currentExecutableParity=False,contexts=len(cases),completeRoutineHashes=blocks,sourceHashes=hashes,
  inputs={str(Path(__file__).resolve().relative_to(ROOT)):hashlib.sha256(Path(__file__).read_bytes()).hexdigest()},
  artifacts={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest()for p in [cppfile,binary,fixture,STAGE/'sanitizers.log']},
  scope=['Complete older SUNIT allocator/seed and item data/init helpers execute with item kind4,mode4,initialized game service and explicit world/table/memory/path services. Successful allocations draw game twice;unit/base and property seeds independently initialize high666. Failed native allocation returns before RNG or GUID changes.',
   'Source LCG is a declared service,independently compared in Python for zero/high/edge seeds and serial allocations. Named synthetic unit flags do not certify binary constants. Item identified16is pinned.'],
  limits=['No current allocator execution,world seed provenance,real memory/room/registration/player/monster/object/missile services or complete item-quality factory is certified. No-global-seed fallback intentionally aborts. Source helpers do not themselves generate or equip a Valkyrie item.'])
 (ROOT/'tools/reference/npc-item-allocation-source-evidence.json').write_text(json.dumps(proof,indent=2)+'\n')
 print('NPC item allocation:960complete source contexts,2160successful items/2160allocation refusals;two distinct game draws and seed high666;ASan/UBSan clean.')
if __name__=='__main__':main()
