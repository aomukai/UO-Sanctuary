#!/usr/bin/env python3
"""Full trap summon, pet registration and FIFO insertion source with declared services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  b=text[m.start():i];blocks[name]=hashlib.sha256(b.encode()).hexdigest();return b+'\n'
 ass=source('source/D2Game/src/SKILLS/SkillAss.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp');pets=source('source/D2Game/src/PLAYER/PlayerPets.cpp')
 cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#include <array>
#define __fastcall
#define D2_ASSERT assert
#define D2_CALLOC_STRC_POOL(pool,T) new T{}
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNITFLAG_ISREVIVE=1,UNITFLAG_NOTC=2,AISPECIALSTATE_NONE=0,STATE_UNINTERRUPTABLE=1,EVENTTYPE_AITHINK=2,SKILLSFLAGINDEX_INTOWN=3;
unsigned gdwBitMasks[32];
struct D2ActiveRoomStrc{}room;
struct D2UnitStrc{int dwUnitType=0,dwUnitId=1,dwFlags=0,x=100,y=200;void* pDynamicPath=nullptr;D2UnitStrc* owner=nullptr;}owner,minion,pet;
struct D2GameStrc{int dwGameFrame=1000;void* pMemoryPool=nullptr;}game;
struct D2SkillsTxt{int nPetType=16,dwPetMax=1;unsigned dwFlags[2]={};}row;
struct D2SummonArgStrc{int nMonMode=0,nPetType=0,nHcIdx=0;struct{int nX=0,nY=0;}pPosition;int dwFlags=0;D2UnitStrc* pOwner=nullptr;int nAiSpecialState=0,nPetMax=0;};
struct D2ModeChangeStrc{int mode=0;D2UnitStrc* pet=nullptr;};
struct D2PetInfoStrc{int nSeed=0,wName=0,unk0x06=0,nHirelingId=0;};
struct D2PetDataStrc{int nUnitGUID=0;D2PetInfoStrc petInfo;D2PetDataStrc* pNext=nullptr;};
struct D2PetListStrc{int nMax=5,nCurrent=0;D2PetDataStrc* pPetData=nullptr;}list;
struct D2PetTypeTxt{int wGroup=0;}petType;
struct Tables{int nMonStatsTxtRecordCount=500,nPetTypeTxtRecordCount=20;}tables;auto* sgptDataTables=&tables;
int summon=413,validRoom=1,town=0,firstSpawn=1,secondSpawn=1,passiveResult=1,targetValid=1;
int spawnCalls=0,baseCalls=0,passiveCalls=0,modeCalls=0,specialCalls=0,event=0,ownerStored=0,registered=0,capType=-1,baseOwner=-1,marked=0,removed=-1;
void clearList(){while(list.pPetData){auto* p=list.pPetData;list.pPetData=p->pNext;delete p;}list={};}
void fill(int maximum,int count){clearList();list.nMax=maximum;for(int i=0;i<count;i++){auto* p=new D2PetDataStrc{};p->nUnitGUID=100+i;auto** next=&list.pPetData;while(*next)next=&(*next)->pNext;*next=p;list.nCurrent++;}}
int D2GAME_GetSummonIdFromSkill_6FD15580(D2UnitStrc*,int,int id,int rank,int* mode,int,int){assert(id==257 && rank==3);*mode=8;return summon;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int id,int rank){assert(formula==1 && id==257 && rank==3);return 5;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
D2UnitStrc* AIGENERAL_GetMinionOwner(D2UnitStrc* p){return p->owner;}
int D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){*x=108;*y=208;return targetValid;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}
D2ActiveRoomStrc* D2GAME_GetRoom_6FC52070(D2ActiveRoomStrc*,int,int){return validRoom?&room:nullptr;}
int DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return town;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){assert(false);return nullptr;}
int D2COMMON_10175_PathGetFirstPointX(void*){assert(false);return 0;}int D2COMMON_10176_PathGetFirstPointY(void*){assert(false);return 0;}
D2UnitStrc* D2GAME_SpawnMonster_6FC69F10(D2GameStrc*,D2ActiveRoomStrc*,int x,int y,int id,int mode,int radius,int flags){assert(id==413 && mode==8 && flags==66);assert(radius==(spawnCalls?4:-1));spawnCalls++;pet.x=x;pet.y=y;return (spawnCalls==1?firstSpawn:secondSpawn)?&pet:nullptr;}
void sub_6FD146D0(D2GameStrc*,D2UnitStrc*){assert(false);}void D2GAME_SetNecropetFlag_6FCBD760(D2UnitStrc*,int){assert(false);}
D2PetTypeTxt* DATATBLS_GetPetTypeTxtRecord(int type){assert(type>=0 && type<20);return &petType;}
D2UnitStrc* sub_6FC7E8B0(D2GameStrc*,D2UnitStrc*,int,int){assert(false);return nullptr;}
void sub_6FC7D260(D2GameStrc*,D2UnitStrc*,int,int){assert(false);}
void sub_6FC7D150(D2GameStrc*,D2UnitStrc*,int type,int maximum){list.nMax=maximum;capType=type;}
void sub_6FC7DF40(D2GameStrc*,D2UnitStrc*){}
void sub_6FC7CF50(D2GameStrc*,int id){marked=id;}
void PLAYERPETS_RemovePetFromList(D2GameStrc*,D2PetListStrc* l,int id,int kill){assert(l==&list && l->pPetData->nUnitGUID==id && kill==1);removed=id;auto* p=l->pPetData;l->pPetData=p->pNext;delete p;l->nCurrent--;}
int sub_6FC7DA40(D2GameStrc*,D2UnitStrc*,D2PetListStrc*,D2UnitStrc*,D2PetInfoStrc*);
void sub_6FC7DBF0(D2GameStrc* g,D2UnitStrc* p,D2UnitStrc* pet,int,D2PetInfoStrc* info){registered++;assert(sub_6FC7DA40(g,p,&list,pet,info));}
void AIGENERAL_SetOwnerData(D2GameStrc*,D2UnitStrc* p,int id,int type,int a,int b){assert(p==&pet && id==owner.dwUnitId && type==UNIT_PLAYER && a==0 && b==0);ownerStored++;}
int STATES_CheckState(D2UnitStrc*,int){return 0;}
void* AIGENERAL_GetAiControlFromUnit(D2UnitStrc*){return nullptr;}
void AITHINK_ExecuteAiFn(D2GameStrc*,D2UnitStrc*,void*,int state){assert(state==0);specialCalls++;}
void MONSTER_UpdateAiCallbackEvent(D2GameStrc*,D2UnitStrc*){}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int a,int b){assert(type==EVENTTYPE_AITHINK && a==0 && b==0);event=frame;}
int D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10(D2GameStrc*,D2UnitStrc* o,D2UnitStrc* p,int zero,int rank){assert(p==&pet && zero==0 && rank==3);baseOwner=o==&owner; baseCalls++;return passiveResult;}
int D2GAME_SetSummonPassiveStats_6FD0C530(D2GameStrc*,D2UnitStrc* o,D2UnitStrc* p,int id,int rank,int level){assert(o==&owner && p==&pet && id==257 && rank==3 && level==0);passiveCalls++;return passiveResult;}
void sub_6FCBDD30(D2UnitStrc* p,unsigned a,int b){assert(p==&pet && a==2 && b==1);}
void MONSTERMODE_GetModeChangeInfo(D2UnitStrc* p,int mode,D2ModeChangeStrc* out){out->mode=mode;out->pet=p;}
void D2GAME_ModeChange_6FC65220(D2GameStrc*,D2ModeChangeStrc* mode,int one){assert(mode->pet==&pet && mode->mode==8 && one==1);modeCalls++;}
D2UnitStrc* D2GAME_SummonPet_6FD14430(D2GameStrc*,D2SummonArgStrc*);
'''
 for text,name in [(pets,'sub_6FC7DA40'),(pets,'sub_6FC7D7A0'),(skills,'D2GAME_SummonPet_6FD14430'),(ass,'sub_6FCF8610')]:cpp+=routine(text,name)
 cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("{\"factory\":[");
for(int kind:{0,1,2})for(int monster:{-1,413,500})for(int roomOk:{0,1})for(int isTown:{0,1})for(int allowed:{0,1})for(int firstOk:{0,1})for(int secondOk:{0,1})for(int passive:{0,1})for(int type:{-1,16,20})for(int aim:{0,1,2}){
 owner={};minion={};pet={};pet.dwUnitType=UNIT_MONSTER;pet.dwUnitId=303;minion.dwUnitType=UNIT_MONSTER;minion.owner=kind==1?&owner:nullptr;row={};row.nPetType=type;row.dwFlags[0]=allowed?gdwBitMasks[SKILLSFLAGINDEX_INTOWN]:0;fill(5,5);
 summon=monster;validRoom=roomOk;town=isTown;firstSpawn=firstOk;secondSpawn=secondOk;passiveResult=passive;spawnCalls=baseCalls=passiveCalls=modeCalls=specialCalls=event=ownerStored=registered=marked=0;capType=baseOwner=removed=-1;
 auto* made=sub_6FCF8610(&game,kind?&minion:&owner,aim?108:0,aim==1?208:0,&row,257,3);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,monster,roomOk,isTown,allowed,firstOk,secondOk,passive,type,aim,made!=nullptr,spawnCalls,baseCalls,passiveCalls,modeCalls,specialCalls,event,ownerStored,registered,capType,baseOwner,removed,pet.dwFlags,pet.x,pet.y,list.nMax,list.nCurrent,list.pPetData->nUnitGUID);first=false;
}
printf("],\"fifo\":[");first=true;for(int maximum:{0,1,2,5})for(int count=0;count<=maximum;count++)for(int hasPet:{0,1}){fill(maximum,count);marked=0;removed=-1;pet.dwUnitId=303;D2PetInfoStrc info{11,22,33,44};int result=sub_6FC7DA40(&game,&owner,&list,hasPet?&pet:nullptr,&info);printf("%s[%d,%d,%d,%d,%d,%d,%d,[",first?"":",",maximum,count,hasPet,result,marked,removed,list.nCurrent);first=false;for(auto* p=list.pPetData;p;p=p->pNext)printf("%s%d",p==list.pPetData?"":",",p->nUnitGUID);printf("]]");if(result){auto* tail=list.pPetData;while(tail->pNext)tail=tail->pNext;assert(tail->petInfo.nSeed==11 && tail->petInfo.wName==22 && tail->petInfo.unk0x06==33 && tail->petInfo.nHirelingId==44);}}
printf("]}\n");clearList();}
'''
 p=STAGE/'assassin-trap-summon-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data['factory'])==5184 and len(data['fifo'])==24
 for c in data['factory']:
  kind,monster,room,town,allowed,first,second,passive,typ,aim,*actual=c
  attempt=monster==413 and kind!=2 and room and (not town or allowed);made=bool(attempt and (first or second));spawns=0 if not attempt else 1 if first else 2
  expected=[int(made),spawns,int(made),int(made),int(made),int(made),1025 if made else 0,int(made),int(made),(typ if 0<=typ<20 else 0) if made else -1,1 if made else -1,100 if made else -1,3 if made else 0,108 if spawns and aim==1 else 100,208 if spawns and aim==1 else 200,5,5,101 if made else 100]
  assert actual==expected,(c,expected)
 for maximum,count,hasPet,result,marked,removed,current,ids in data['fifo']:
  success=maximum>0;expected=list(range(100,100+count));old=-1
  if success:
   if count==maximum:old=expected.pop(0)
   expected.append(303 if hasPet else -1)
  assert [result,marked,removed,current,ids]==[int(success),(303 if hasPet else -1) if not success else 0,old,len(expected),expected]
 out=STAGE/'assassin-trap-summon-source-probe.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.bladesentinel',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledUnchangedRoutines=blocks,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=dict(factory=5184,fifo=24),total=5208),established=[
 'Complete trap helper rejects invalid summon ids, validates/falls back pet type, uses caster coordinates if either explicit coordinate is zero, resolves ultimate nonmonster minion owner and rejects missing owners. Room/town gates occur before spawning. Owner-chain/base-stat/passive calls and final summon mode are checked.',
 'Complete generic summon tries spawn radius-1 then4, marks revive/no-treasure, clamps cap to at least1, registers pet with ultimate owner, initializes AI and schedules frame+25. Blade do44 subsequently replaces that event with+1, separately proved. Base/passive return failures are ignored by the trap helper; this is a source control-flow fact, not a desired modern failure policy.',
 'Complete registration helper sees pinned assassintrap group0 and forwards type/max to pet list and insertion services. Complete insertion evicts list head when at cap, appends new pet at tail and copies metadata. Five existing traps then one new leave101..104,303; current shared assassintrap cap is5. Zero max with declared no replenishment rejects/removes incoming instead.',
 'Factory controls cover player versus nested monster versus missing minion owner, invalid monster/pet type, room/town, first/second spawn success, ignored stat failures and coordinate fallback. Separate FIFO controls cover caps0/1/2/5, empty/partial/full lists and null-pet GUID.'
 ],services=[
 'Actual world spawn/stat/rank/formula, room/town, minion owner, packet/list wrappers, AI control initialization/event and mode services are explicit checked boundaries. Full base/passive helpers have separate Valkyrie evidence; current14-slot inherited data and actual stat consumers remain new work.',
 'Registration sub_6FC7DBF0 wrapper is a declared direct bridge to the full compiled insertion, not full client packet/player data execution. No source heap allocation failure, negative existing count, over-cap list corruption, pet group>0, null owner, necropet/alternate flags or full modern executable equivalence is claimed.'
 ])
 target=ROOT/'tools/reference/assassin-trap-summon-engine-evidence.json';render=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print('Assassin trap summon:5208 full factory/registration/FIFO cases pass; world/stat/packet wrappers remain declared services.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
