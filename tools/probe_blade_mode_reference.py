#!/usr/bin/env python3
"""Execute complete blade walk mode dispatch with explicit path/stat services."""
import argparse, hashlib, json, re, subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'

def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  s=text[m.start():i];blocks[name]=hashlib.sha256(s.encode()).hexdigest();return s+'\n'
 mode=source('source/D2Game/src/MONSTER/MonsterMode.cpp');tactics=source('source/D2Game/src/AI/AiTactics.cpp');units=source('source/D2Game/src/UNIT/SUnit.cpp');items=source('source/D2Game/src/ITEMS/Items.cpp');seed=source('source/D2Common/include/D2Seed.h');header=source('source/D2Game/include/MONSTER/MonsterMode.h')
 cpp=r'''#include <cassert>
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <algorithm>
#include <vector>
#include <array>
#define __fastcall
#define __stdcall
#define D2_ASSERT assert
constexpr uint8_t operator""ui8(unsigned long long x){return uint8_t(x);}
constexpr int64_t operator""i64(unsigned long long x){return int64_t(x);}
constexpr int UNIT_MONSTER=1,MONMODE_DEATH=0,MONMODE_NEUTRAL=1,MONMODE_WALK=2,MONMODE_GETHIT=3,MONMODE_DEAD=12,MONMODE_KNOCKBACK=13,MONMODE_RUN=15;
constexpr int STATE_UNINTERRUPTABLE=1,STATE_STUNNED=2,STATE_DECREPIFY=3,STAT_VELOCITYPERCENT=1,UNITFLAG_DOUPDATE=1,UNITFLAG_MONMODEISCHANGING=2;
constexpr int PATHTYPE_IDASTAR=0,PATHTYPE_ASTAR=1,PATHTYPE_TOWARD=2,PATHTYPE_STRAIGHT=7,PATHTYPE_LEAP=9,PATHTYPE_TOWARD_FINISH=13,PATHTYPE_WALL_FOLLOW=15;
constexpr int EVENTTYPE_AITHINK=2,EVENTTYPE_MODECHANGE=0;
struct D2UnitStrc;struct D2GameStrc;
struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};
struct D2AiParamStrc{uint8_t unk0x00=0,unk0x01=0;D2UnitStrc* pTarget=nullptr;int unk0x08=0,unk0x0C=0,unk0x10=0,unk0x14=0,unk0x18=0,nVelocity=0;uint8_t unk0x20=0;};
struct D2MonStatsTxt{int nAIdel[3]={15,15,15};} mon;
struct D2MonsterDataStrc{D2AiParamStrc* pAiParam;D2MonStatsTxt* pMonstatsTxt=&mon;};
struct D2DynamicPathStrc{int dwPathType=0,nDistMax=0,points=0,nX=0,nY=0;D2UnitStrc* target=nullptr;};
struct D2UnitStrc{int dwUnitType=1,dwAnimMode=1,dwFlags=0,dwUnitId=1,dwClassId=413,aiState=1,x=100,y=100;D2DynamicPathStrc* pDynamicPath=nullptr;D2MonsterDataStrc* pMonsterData=nullptr;void* pMemoryPool=nullptr;D2SeedStrc pSeed{};};
struct D2GameStrc{int dwGameFrame=1000,nGameType=0,dwGameType=0,nDifficulty=0,dwPathTypesCount[18]={},nTotalPathTypesCount=0;} game;
struct D2StatListStrc{};struct D2AiControlStrc{};
struct D2MonStats2Txt{int nModeEnabledWhenMovingFlags[2]={};} mon2;
'''
 for name in ['D2ModeChangeStrc','D2MonModeCallbackTableStrc']:
  cpp+=re.search(r'struct '+name+r'\s*\{.*?^\};',header,re.M|re.S)[0]+'\n'
 cpp=cpp.replace('struct D2MonModeCallbackTableStrc', 'using MonModeCallback1=int(*)(D2GameStrc*,D2ModeChangeStrc*);using MonModeCallback2=int(*)(D2GameStrc*,D2UnitStrc*);using MonModeCallback3=void(*)(D2GameStrc*,D2UnitStrc*);\nstruct D2MonModeCallbackTableStrc')
 cpp+=re.search(r'struct D2MonModeInfoTableStrc\s*\{.*?^\};',mode,re.M|re.S)[0]+'\n'+re.search(r'constexpr D2MonModeInfoTableStrc stru_6FD28738\[16\]\s*=\s*\{.*?^\};',mode,re.M|re.S)[0]+'\n'
 names=['D2GAME_ModeChange_6FC65220','sub_6FC65680','MONSTERMODE_GetModeChangeInfo','sub_6FC64310','sub_6FC64450','D2GAME_MONSTERMODE_ResetVelocityParams_6FC62F90','D2GAME_MONSTERMODE_SetVelocityParams_6FC62FC0']
 for name in names:
  b=routine(mode,name);cpp+=b[:b.index('{')].strip()+';\n'
 cpp+=r'''
int eventFrame=0,deleted=0,bits=0,pathCalls=0,modeEvents=0;std::vector<std::array<int,4>> paths;
int STATES_CheckState(D2UnitStrc*,int){return 0;}void sub_6FCBDE90(D2UnitStrc*,int){assert(false);}
int SUNIT_IsDead(D2UnitStrc*){return 0;}void PATH_AddCollisionFootprintForUnit(D2UnitStrc*){}
int MONSTER_GetAiState(D2UnitStrc* u){return u->aiState;}void MONSTER_SetAiState(D2UnitStrc* u,int x){u->aiState=x;}
void PATH_SetTargetUnit(D2DynamicPathStrc* p,D2UnitStrc* t){p->target=t;}
void D2COMMON_10170_PathSetTargetPos(D2DynamicPathStrc* p,int x,int y){p->target=nullptr;p->nX=x;p->nY=y;}
void PATH_SetDistance(D2DynamicPathStrc*,int x){assert(x==20);}void PATH_SetStepNum(D2DynamicPathStrc*,int x){assert(x==1);}
D2UnitStrc* PATH_GetTargetUnit(D2DynamicPathStrc* p){return p->target;}
void PATH_SetNewDistance(D2DynamicPathStrc* p,int x){p->nDistMax=x;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){return p->nX;}int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc* p){return p->nY;}
void PATH_SetType(D2DynamicPathStrc* p,int x){p->dwPathType=x;}
int D2Common_10142(D2DynamicPathStrc* p,D2UnitStrc*,int){paths.push_back({p->dwPathType,p->nDistMax,p->nX,p->nY});return p->points=(bits>>pathCalls++)&1;}
int PATH_GetNumberOfPathPoints(D2DynamicPathStrc* p){return p->points;}void UNITROOM_RefreshUnit(D2UnitStrc*){}
void sub_6FC627B0(D2UnitStrc*,int){}void sub_6FC6E770(D2GameStrc*,D2UnitStrc*){}void sub_6FC6E860(D2GameStrc*,D2UnitStrc*){}
void UNITS_SetUsedSkill(D2UnitStrc*,void*){}void sub_6FCBC4D0(D2UnitStrc*){}
D2StatListStrc* STATLIST_GetStatListFromUnitAndFlag(D2UnitStrc*,unsigned){assert(false);return nullptr;}
void STATLIST_SetStat(D2StatListStrc*,int,int,int){assert(false);}void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){assert(false);}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int,int,int){assert(false);return nullptr;}void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){assert(false);}
void D2GAME_DeletePlayerPerFrameEvents_6FCBCE50(D2GameStrc*,D2UnitStrc*){modeEvents=0;}
void sub_6FCBCE70(D2GameStrc*,D2UnitStrc*){assert(false);}void sub_6FCBD3A0(D2GameStrc*,D2UnitStrc*){modeEvents=1;}
int gdwBitMasks[8]={1,2,4,8,16,32,64,128};D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return &mon2;}
void SUNITDMG_FreeAttackerCombatList(D2GameStrc*,D2UnitStrc*){}void UNITS_ChangeAnimMode(D2UnitStrc* u,int mode){u->dwAnimMode=mode;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int){assert(type==EVENTTYPE_AITHINK);eventFrame=0;deleted++;}
int EVENT_GetEventFrame(D2GameStrc*,D2UnitStrc*,int){return eventFrame;}void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int,int){assert(type==EVENTTYPE_AITHINK);eventFrame=frame;}
const D2MonModeCallbackTableStrc* MONSTERMODE_GetCallbackTableRecord(D2UnitStrc*,int mode){static const D2MonModeCallbackTableStrc walk{sub_6FC64450,nullptr,nullptr,1},idle{sub_6FC64310,nullptr,nullptr,0};assert(mode==1||mode==2);return mode==2?&walk:&idle;}
D2AiControlStrc* AIGENERAL_GetAiControlFromUnit(D2UnitStrc*){assert(false);return nullptr;}void AIUTIL_ToggleAiControlFlag(D2AiControlStrc*,unsigned,int){assert(false);}
int32_t AITACTICS_WalkCloseToUnit(D2GameStrc*,D2UnitStrc*,int32_t);
int32_t AITACTICS_MoveToTarget(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int32_t,int32_t,int32_t,uint8_t,uint8_t);
void AITACTICS_Idle(D2GameStrc*,D2UnitStrc*,int32_t);
int CLIENTS_GetUnitX(D2UnitStrc* u){return u->x;}int CLIENTS_GetUnitY(D2UnitStrc* u){return u->y;}
'''
 cpp+=routine(seed,'SEED_RollRandomNumber')+routine(seed,'SEED_RollLimitedRandomNumber')
 for name in ['ITEMS_RollRandomNumber','ITEMS_RollLimitedRandomNumber']:cpp+=routine(items,name)
 cpp+=routine(units,'SUNIT_SetCombatMode')
 for name in names:cpp+=routine(mode,name)
 for name in ['AITACTICS_IdleInNeutralMode','AITACTICS_Idle','AITACTICS_SetVelocity','AITACTICS_MoveToTarget','AITACTICS_WalkToTargetCoordinatesDeleteAiEvent','AITACTICS_WalkCloseToUnit','AITACTICS_WanderToTarget','D2GAME_AICORE_WalkToOwner_6FCD0B60']:cpp+=routine(tactics,name)
 cpp+=r'''
int main(){printf("{\"modes\":[");bool first=true;for(int outcome=0;outcome<8;outcome++){
 D2AiParamStrc ai;D2MonsterDataStrc md{&ai};D2DynamicPathStrc path;D2UnitStrc u;u.pMonsterData=&md;u.pDynamicPath=&path;game={};eventFrame=1001;deleted=modeEvents=pathCalls=0;bits=outcome;paths.clear();
 AITACTICS_SetVelocity(&u,15,0,20);int one=AITACTICS_WalkToTargetCoordinatesDeleteAiEvent(&game,&u,108,100),two=-1;if(!one)two=AITACTICS_WalkToTargetCoordinatesDeleteAiEvent(&game,&u,100,100);
 printf("%s{\"input\":%d,\"result\":[%d,%d,%d,%d,%d,%d,%d,%d],\"paths\":[",first?"":",",outcome,one,two,u.dwAnimMode,ai.unk0x18,ai.nVelocity,ai.unk0x20,eventFrame,deleted);first=false;for(size_t i=0;i<paths.size();i++){auto p=paths[i];printf("%s[%d,%d,%d,%d]",i?",":"",p[0],p[1],p[2],p[3]);}printf("]}");
}printf("],\"owner\":[");first=true;for(int hasOwner:{0,1})for(uint32_t low=0;low<256;low++){
 D2AiParamStrc ai;D2MonsterDataStrc md{&ai};D2DynamicPathStrc path;D2UnitStrc u,owner;u.pMonsterData=&md;u.pDynamicPath=&path;u.pSeed.lSeed=(uint64_t(666)<<32)|low;owner.x=110;owner.y=112;bits=1;pathCalls=0;paths.clear();eventFrame=0;
 D2GAME_AICORE_WalkToOwner_6FCD0B60(&game,&u,hasOwner?&owner:nullptr,5);assert(paths.size()==1);auto p=paths[0];printf("%s[%d,%u,%d,%d,%llu]",first?"":",",hasOwner,low,p[2],p[3],(unsigned long long)u.pSeed.lSeed);first=false;
}printf("]}\n");}
'''
 p=STAGE/'blade-mode-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'blade-mode-sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr.decode()[-4000:]
 data=json.loads(result.stdout);assert len(data['modes'])==8 and len(data['owner'])==512
 for x in data['modes']:
  bits=x['input'];assert x['paths'][0]==[15,20,108,100]
  if bits&1:assert len(x['paths'])==1 and x['result'][:2]==[1,-1]
  else:
   assert x['paths'][1]==[13,5,100,100]
   if not bits&2:assert x['paths'][2]==[15,5,100,100]
  assert x['result'][3:6]==[0,0,0]
 fixture=STAGE/'blade-mode-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutineHashes=blocks,handlerEnabled=False,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),modeCases=8,ownerCases=512),sanitizers='ASan/UBSan; successful exit and zero runtime stderr required',established=['Complete ModeChange, walk path preparation, mode-info initializer, neutral/walk entry, velocity setter and AI movement/idle/wander helpers execute unchanged with explicit path/stat services. The original mode-info table is retained.','A single SetVelocity(15,0,20) applies only to the first walk. ModeChange consumes/reset overrides before path calculation even when it fails. The reverse attempt defaults to path13/distance5 and falls back to path15/distance5 if13 fails. Reapplying15/20 to every AI walk would be incorrect.','Owner fallback uses four exact source random draws around the owner at distance5 or self at distance2 when ownerless; final seed and coordinates are retained for512 contexts.'],limits=['General path calculation, stats/animation collectors, callback lookup, room refresh and event storage remain declared services; exact full PathWF and event queue are proved separately. This is a complete mode-dispatch control-flow proof, not actual autonomous actor/missile delivery.','Microsoft ui8/i64 literals use equivalent typed literal operators in the ABI shim; routine bodies are unchanged. Legacy reconstruction is supplementary evidence, not modern executable equivalence. Unsupported stat override/state/callback branches assert rather than returning invented results.'])
 p=ROOT/'tools/reference/blade-mode-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Blade mode:8 complete dispatch contexts and512 owner fallback RNG contexts pass; override consumption requires path13/5 on reverse.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
