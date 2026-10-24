#!/usr/bin/env python3
"""Compose complete source event/mode/AI/path/movement routines for blade clocks."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'

def build(check=False):
 import probe_toward_motion_reference
 probe_toward_motion_reference.build(True)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  s=text[m.start():i];blocks[name]=hashlib.sha256(s.encode()).hexdigest();return s+'\n'
 mode=source('source/D2Game/src/MONSTER/MonsterMode.cpp');tactics=source('source/D2Game/src/AI/AiTactics.cpp');server=source('source/D2Game/src/UNIT/SUnit.cpp');ai=source('source/D2Game/src/AI/AiThink.cpp');items=source('source/D2Game/src/ITEMS/Items.cpp');seed=source('source/D2Common/include/D2Seed.h');paths=source('source/D2Common/src/Path/Path.cpp');event=source('source/D2Game/src/GAME/Event.cpp');eh=source('source/D2Game/include/GAME/Event.h');mh=source('source/D2Game/include/MONSTER/MonsterMode.h')
 strip=lambda s:re.sub(r'^#(?:include|pragma)[^\n]*\n','',s,flags=re.M)
 original=(STAGE/'toward-motion-source-probe.cpp').read_text();cpp=original[:original.index('int main()')]
 extra=r'''
#include <cstdlib>
#define D2_CALLOC_STRC_POOL(pool,T) static_cast<T*>(calloc(1,sizeof(T)))
#define D2_ALLOC_STRC_POOL(pool,T) static_cast<T*>(malloc(sizeof(T)))
#define D2_FREE_POOL(pool,p) free(p)
constexpr uint8_t operator""ui8(unsigned long long x){return uint8_t(x);}
constexpr int64_t operator""i64(unsigned long long x){return int64_t(x);}
struct D2GameStrc;struct D2UnitStrc;struct D2MonsterDataStrc;
struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};
'''+strip(eh)+'\n'
 cpp=cpp.replace('struct D2ActiveRoomStrc', 'struct D2ActiveRoomStrc',1)
 marker='struct D2UnitStrc{';assert marker in cpp;cpp=cpp.replace(marker,extra+'\n'+marker,1)
 cpp=cpp.replace('int velocityPercent=75,faster=0;', 'int velocityPercent=75,faster=0,dwAnimMode=1,dwUnitId=1,aiState=1;bool dead=false;void* pMemoryPool=nullptr;D2MonsterDataStrc* pMonsterData=nullptr;D2EventTimerStrc* timer=nullptr;D2SeedStrc pSeed{};')
 cpp=cpp.replace('dwSpeed=0,nDirOffset=0;', 'dwSpeed=0,nDirOffset=0,dwPrevPathType=0,nPreviousVelocity=0,nDistance=20;')
 cpp=cpp.replace('int nVelocity=12,nWalkAnimSpeed=128,nRunAnimSpeed=256;', 'int nVelocity=12,nWalkAnimSpeed=128,nRunAnimSpeed=256,nAIdel[3]={15,15,15},nBaseId=413,nSplEndGeneric=0,nSkill[1]={257};')
 cpp+=r'''
constexpr int UNIT_TILE=5,UNIT_TYPES_COUNT=6,D2UnitInvalidGUID=-1,MONMODE_DEATH=0,MONMODE_NEUTRAL=1,MONMODE_GETHIT=3,MONMODE_DEAD=12,MONMODE_KNOCKBACK=13,MONMODE_SKILL1=8,MONMODE_SKILL3=10,MONMODE_SKILL4=11,MONMODE_SEQUENCE=14;
constexpr int STATE_UNINTERRUPTABLE=1,STATE_STUNNED=2,STATE_DECREPIFY=3,STATE_FREEZE=4,STATE_BLAZE=5,STATE_SPIDERLAY=6;
constexpr int UNITFLAG_DOUPDATE=1,UNITFLAG_MONMODEISCHANGING=2,STAT_TOHIT=7,STAT_ITEM_TOHIT_PERCENT=8;
constexpr int MONSTER_VULTURE1=1001,MONSTER_WILLOWISP1=1002,MONSTER_BATDEMON1=1003,MONSTER_FROGDEMON1=1004,MONSTER_FIREBEAST=1005,MONSTER_ICEGLOBE=1006,MONSTER_TRAPPEDSOUL1=1007;
struct D2AiParamStrc{uint8_t unk0x00=0,unk0x01=0;D2UnitStrc* pTarget=nullptr;int unk0x08=0,unk0x0C=0,unk0x10=0,unk0x14=0,unk0x18=0,nVelocity=0;uint8_t unk0x20=0;};
struct D2MonsterDataStrc{D2AiParamStrc* pAiParam;D2MonStatsTxt* pMonstatsTxt=&mon;};
struct D2GameStrc{int dwGameFrame=1000,nGameType=0,dwGameType=0,nDifficulty=0,dwPathTypesCount[18]={},nTotalPathTypesCount=0;D2EventTimerQueueStrc* pTimerQueue=nullptr;void* pMemoryPool=nullptr;};
struct D2StatListStrc{};struct D2SkillStrc{} learned;
struct D2MonStats2Txt{int nModeEnabledWhenMovingFlags[2]={};} mon2;
struct D2AiControlStrc{int dwAiParam[3]={-1,1,0};} control;
struct D2AiTickParamStrc{D2MonStatsTxt* pMonstatsTxt=&mon;D2AiControlStrc* pAiControl=&control;} tick;
struct D2AiCmdStrc{int nCmdParam[5]={0,100,100,108,100};} command;
struct D2SkillsTxt{int dwCalc[4]={0,0,0,1};int wSrvMissileA=392;} row;
struct Tables{int nMissilesTxtRecordCount=1000;} tables;Tables* sgptDataTables=&tables;
D2UnitStrc* active=nullptr;D2UnitStrc* owner=nullptr;D2UnitStrc dummy;
int rank=1,make=0,hasCommand=1,creates=0,thinks=0,moves=0,refreshes=0;
'''
 for name in ['D2ModeChangeStrc','D2MonModeCallbackTableStrc']:cpp+=re.search(r'struct '+name+r'\s*\{.*?^\};',mh,re.M|re.S)[0]+'\n'
 cpp=cpp.replace('struct D2MonModeCallbackTableStrc', 'using MonModeCallback1=int(*)(D2GameStrc*,D2ModeChangeStrc*);using MonModeCallback2=int(*)(D2GameStrc*,D2UnitStrc*);using MonModeCallback3=void(*)(D2GameStrc*,D2UnitStrc*);\nstruct D2MonModeCallbackTableStrc')
 cpp+=re.search(r'struct D2MonModeInfoTableStrc\s*\{.*?^\};',mode,re.M|re.S)[0]+'\n'+re.search(r'constexpr D2MonModeInfoTableStrc stru_6FD28738\[16\]\s*=\s*\{.*?^\};',mode,re.M|re.S)[0]+'\n'
 for name in ['gaPathTypeFlags_6FDD2088','gaOffsetForPathType']:cpp+=re.search(r'static const [^\n]*\b'+name+r'\[\]\s*=\s*\{.*?^\};',paths,re.M|re.S)[0]+'\n'
 selected=[(paths,n) for n in ['PATH_SetType','PATH_SetNewDistance','PATH_SetDistance','D2COMMON_10170_PathSetTargetPos']]+[(server,n) for n in ['SUNIT_SetCombatMode','sub_6FCBC930','sub_6FCBD3A0']]+[(mode,n) for n in ['D2GAME_ModeChange_6FC65220','sub_6FC65680','MONSTERMODE_GetModeChangeInfo','sub_6FC64310','sub_6FC64450','sub_6FC64480','sub_6FC63680','D2GAME_RemoveModeChangeEventCallback_6FC63B20','D2GAME_MONSTERMODE_ResetVelocityParams_6FC62F90','D2GAME_MONSTERMODE_SetVelocityParams_6FC62FC0','D2GAME_MONSTERS_AiFunction01_6FC65080','MONSTERMODE_EventHandler']]+[(seed,n) for n in ['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber']]+[(items,n) for n in ['ITEMS_RollRandomNumber','ITEMS_RollLimitedRandomNumber']]+[(tactics,n) for n in ['AITACTICS_IdleInNeutralMode','AITACTICS_Idle','AITACTICS_SetVelocity','AITACTICS_MoveToTarget','AITACTICS_WalkToTargetCoordinatesDeleteAiEvent','AITACTICS_WalkCloseToUnit','AITACTICS_WanderToTarget','D2GAME_AICORE_WalkToOwner_6FCD0B60']]+[(ai,n) for n in ['D2GAME_AI_Unk102_6FCE9FB0','AITHINK_Fn102_BladeCreeper']]
 for text,name in selected:
  s=routine(text,name);cpp+=s[:s.index('{')].strip()+';\n'
 cpp+=r'''
using MonEventFunc=void(*)(D2GameStrc*,D2UnitStrc*,int,int);
void ThinkEvent(D2GameStrc* g,D2UnitStrc* u,int,int){thinks++;AITHINK_Fn102_BladeCreeper(g,u,&tick);}
MonEventFunc gpMonsterEventFunctions[15]={D2GAME_MONSTERS_AiFunction01_6FC65080,nullptr,ThinkEvent};
D2EventTimerStrc* SUNIT_GetTimerFromUnit(D2UnitStrc* u){assert(u);return u->timer;}void SUNIT_SetTimerOnUnit(D2UnitStrc* u,D2EventTimerStrc* t){assert(u);u->timer=t;}
int STATES_CheckState(D2UnitStrc*,int){return 0;}void sub_6FCBDE90(D2UnitStrc*,int){assert(false);}
int SUNIT_IsDead(D2UnitStrc* u){return u->dead;}int MONSTER_GetAiState(D2UnitStrc* u){return u->aiState;}void MONSTER_SetAiState(D2UnitStrc* u,int x){u->aiState=x;}
void PATH_SetTargetUnit(D2DynamicPathStrc* p,D2UnitStrc* t){p->pTargetUnit=t;}D2UnitStrc* PATH_GetTargetUnit(D2DynamicPathStrc* p){motionPhase=0;return p->pTargetUnit;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){return p->tTargetCoord.X;}int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc* p){return p->tTargetCoord.Y;}
int PATH_GetNumberOfPathPoints(D2DynamicPathStrc* p){return p->dwPathPoints;}void UNITROOM_RefreshUnit(D2UnitStrc*){}
void PATH_SetStepNum(D2DynamicPathStrc* p,int x){p->nStepNum=x;}
void sub_6FC627B0(D2UnitStrc*,int){}void sub_6FC6E770(D2GameStrc*,D2UnitStrc*){}void sub_6FC6E860(D2GameStrc*,D2UnitStrc*){}
void UNITS_SetUsedSkill(D2UnitStrc*,void*){}
void sub_6FCBC4D0(D2UnitStrc* u){motionPhase=1;if(u->dwAnimMode==2){refreshes++;UNITS_UpdateRunWalkAnimRateAndVelocity(u,2,1,413,__FILE__,__LINE__);}}
D2StatListStrc* STATLIST_GetStatListFromUnitAndFlag(D2UnitStrc*,unsigned){assert(false);return nullptr;}
void STATLIST_SetStat(D2StatListStrc*,int,int,int){assert(false);}void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){assert(false);}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int,int,int){assert(false);return nullptr;}void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){assert(false);}
void D2GAME_DeletePlayerPerFrameEvents_6FCBCE50(D2GameStrc* g,D2UnitStrc* u){D2GAME_EVENTS_Delete_6FC34840(g,u,EVENTTYPE_MODECHANGE,0);D2GAME_EVENTS_Delete_6FC34840(g,u,EVENTTYPE_ENDANIM,0);}
void sub_6FCBCE70(D2GameStrc*,D2UnitStrc*){assert(false);}
int gdwBitMasks[8]={1,2,4,8,16,32,64,128};D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return &mon2;}
void SUNITDMG_FreeAttackerCombatList(D2GameStrc*,D2UnitStrc*){}void UNITS_ChangeAnimMode(D2UnitStrc* u,int m){u->dwAnimMode=m;}
int DeathEntry(D2GameStrc* g,D2ModeChangeStrc* m){m->pUnit->dead=true;SUNIT_SetCombatMode(g,m->pUnit,0);return 1;}
const D2MonModeCallbackTableStrc* MONSTERMODE_GetCallbackTableRecord(D2UnitStrc*,int mode){static const D2MonModeCallbackTableStrc walk{sub_6FC64450,sub_6FC64480,nullptr,1},idle{sub_6FC64310,D2GAME_RemoveModeChangeEventCallback_6FC63B20,nullptr,0},death{DeathEntry,nullptr,nullptr,0};assert(mode==0||mode==1||mode==2);return mode==2?&walk:mode==0?&death:&idle;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return &mon;}
D2AiControlStrc* AIGENERAL_GetAiControlFromUnit(D2UnitStrc*){return &control;}void AIUTIL_ToggleAiControlFlag(D2AiControlStrc*,unsigned,int){assert(false);}
void sub_6FCBC7E0(D2GameStrc*,D2UnitStrc*){motionPhase=1;moves++;}int UNITS_IsCurrentRoomInvalid(D2UnitStrc*){return 0;}void sub_6FCBB190(D2GameStrc*,D2UnitStrc*,int){assert(false);}
void SKILLS_CreateBlazeMissile(D2GameStrc*,D2UnitStrc*){assert(false);}void SKILLS_CreateSpiderLayMissile(D2GameStrc*,D2UnitStrc*){assert(false);}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return nullptr;}
void AITACTICS_ChangeModeAndTargetCoordinates(D2GameStrc* g,D2UnitStrc* u,int mode,int x,int y){D2ModeChangeStrc m{};MONSTERMODE_GetModeChangeInfo(u,mode,&m);m.nX=x;m.nY=y;D2GAME_ModeChange_6FC65220(g,&m,1);}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int x){assert(x==257);return &row;}D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int x){assert(x==257);return &learned;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int){return rank;}int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int r){return 100+12*(r-1);}
D2UnitStrc* AIGENERAL_GetMinionOwner(D2UnitStrc*){return owner;}
D2UnitStrc* D2GAME_CreateMissile_6FD115E0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int){creates++;return make?&dummy:nullptr;}
void UNITS_StoreOwner(D2UnitStrc*,D2UnitStrc*){}int UNITS_GetAttackRate(D2UnitStrc*){return 80;}D2UnitStrc* sub_6FC7C7B0(D2UnitStrc*){return nullptr;}int SKILLS_GetWeaponMasteryBonus(D2UnitStrc*,D2UnitStrc*,int,int){assert(false);return 0;}
void STATLIST_AddUnitStat(D2UnitStrc*,int,int,int){}
D2AiCmdStrc* AIGENERAL_GetCurrentAiCommandFromUnit(D2UnitStrc*){return hasCommand?&command:nullptr;}
int CLIENTS_GetUnitX(D2UnitStrc* u){return u->pDynamicPath->tGameCoords.wPosX;}int CLIENTS_GetUnitY(D2UnitStrc* u){return u->pDynamicPath->tGameCoords.wPosY;}
void D2GAME_EVENTS_Callback_6FC81BD0(D2GameStrc*,D2UnitStrc*,D2C_EventTypes,int,int){assert(false);}
void D2GAME_OBJMODE_InvokeEventFunction_6FC75250(D2GameStrc*,D2UnitStrc*,D2C_EventTypes){assert(false);}void MISSMODE_SrvDoHandler(D2GameStrc*,D2UnitStrc*,D2C_EventTypes){assert(false);}void D2GAME_Items_EventsHandler_6FC4A460(D2GameStrc*,D2UnitStrc*,D2C_EventTypes){assert(false);}
'''
 cpp+=strip(event)+'\n'
 for text,name in selected:cpp+=routine(text,name)
 cpp+=r'''
int main(){bool first=true;printf("[");for(int wall:{0,1,2,3})for(int dx:{2,8,19,21})for(int r:{1,3})for(int commandPresent:{0,1}){
 D2GameStrc g;D2AiParamStrc params;D2MonsterDataStrc md{&params};D2DynamicPathStrc p,op;D2UnitStrc u,o;u.pDynamicPath=&p;p.pUnit=&u;u.pMonsterData=&md;o.pDynamicPath=&op;op.pUnit=&o;p.tGameCoords.dwPrecisionX=(100<<16)+32768;p.tGameCoords.dwPrecisionY=(100<<16)+32768;p.dwFlags=0x800;p.dwPathPoints=0;p.dwVelocity=p.dwMaxVelocity=2304;op.tGameCoords.dwPrecisionX=(96<<16)+32768;op.tGameCoords.dwPrecisionY=(100<<16)+32768;u.pSeed.lSeed=(uint64_t(666)<<32)|19;rank=r;hasCommand=commandPresent;make=0;creates=thinks=moves=refreshes=0;owner=&o;active=&u;command={};command.nCmdParam[3]=100+dx;command.nCmdParam[4]=100;
 std::fill(std::begin(cells),std::end(cells),0);if(wall==1)for(int y=80;y<128;y++)cells[(y-80)*48+23]=1;if(wall==2)for(int x=97;x<=103;x++)for(int y=97;y<=103;y++)if(x==97||x==103||y==97||y==103)cells[(y-80)*48+x-80]=1;if(wall==3)cells[20*48+24]=0x1000;
 EVENT_AllocEventQueue(&g);D2GAME_AI_Unk102_6FCE9FB0(&g,&u,&tick);EVENT_SetEvent(&g,&u,EVENTTYPE_AITHINK,1001,0,0);
 if(!first)printf(",");first=false;printf("{\"input\":[%d,%d,%d,%d],\"frames\":[",wall,dx,r,commandPresent);
 for(int f=1;f<=180;f++){g.dwGameFrame=1000+f;motionPhase=1;queries.clear();EVENT_IterateEvents(&g);if(f>1)printf(",");printf("[%d,%u,%u,%u,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%llu,",u.dwAnimMode,p.tGameCoords.dwPrecisionX,p.tGameCoords.dwPrecisionY,p.dwFlags,p.dwPathType,p.dwPathPoints,p.dwCurrentPointIdx,p.dwVelocity,p.nDist,control.dwAiParam[0]-1000,control.dwAiParam[1],control.dwAiParam[2],EVENT_GetEventFrame(&g,&u,EVENTTYPE_AITHINK)?EVENT_GetEventFrame(&g,&u,EVENTTYPE_AITHINK)-1000:0,u.dead,creates,thinks,moves,p.tDirectionVector.nX,p.tDirectionVector.nY,p.tVelocityVector.nX,p.tVelocityVector.nY,(unsigned long long)u.pSeed.lSeed);points(queries);printf("]");}
 printf("]}");D2GAME_DeleteTimersOnUnit_6FC34A30(&g,&u);EVENT_FreeEventQueue(&g);
}printf("]\n");}
'''
 p=STAGE/'blade-clock-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'blade-clock-sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr.decode()[-4000:]
 data=json.loads(result.stdout);assert len(data)==64 and all(len(x['frames'])==180 for x in data)
 out=STAGE/'blade-clock-source-probe.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutineHashes=blocks,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),contexts=64,frames=11520),sanitizers='ASan/UBSan; successful exit and zero runtime stderr required',limits=['Prepared single blade context: base velocity12/75percent, point targets, zero-footprint room/grid and no freeze/special states. Whole source queue, ModeChange, AI, path and movement routines are composed. Stat/animation/callback routing services are explicit; run/walk rate math uses its unchanged verified routine.','Missile allocation is explicitly failed; death entry marks death mode without corpse/animation cleanup. This proves the prepared AI/movement clock, not full live spawn/weapon/missile/combat or current executable equivalence.'])
 p=ROOT/'tools/reference/blade-clock-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Blade clock:64 complete source queue/mode/AI/path contexts /11,520 frames pass sanitizers.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
