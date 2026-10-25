#!/usr/bin/env python3
"""Execute S1 entry, ordinary scheduling, full generic end and neutral handoff.

Cached animation provenance is incomplete. Mode-change, registry/state and event
list services are explicit boundaries; this is not current executable proof.
"""
import argparse,hashlib,json,re,struct,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'

def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};routines={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def extract(s,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;depth=1
  while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
  body=s[m.start():i];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
 ref=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((ref/'sources.json').read_text());tables={}
 for name in ['monstats','monstats2','skills']:
  p='json/'+name+'.json';raw=(ref/p).read_bytes();assert hashlib.sha256(raw).hexdigest()==manifest['files'][p]['sha256'];tables[name]=json.loads(raw)
 monster=tables['monstats']['chargeboltsentry'];model=tables['monstats2']['chargeboltsentry'];skill=next(x for x in tables['skills'].values() if x.get('skill')=='Charged Bolt Sentry')
 assert monster['Code']=='lg' and monster['aidel']==15 and monster.get('SplEndGeneric',0)==0 and monster.get('SplGetModeChart',0)==0
 assert model['SizeX']==0 and model['mS1']==1 and not model.get('S1mv') and model['BaseW']=='hth' and skill['summode']=='S1'
 raw=(ROOT/'.local/research/AnimData.d2').read_bytes();animhash=hashlib.sha256(raw).hexdigest();assert animhash=='e3a54b9ab71df7ccdf3655bbba5868c9d53cd8716b0aad25e8c3d5621579cecb';pos=0;animation=None
 for bucket in range(256):
  count=struct.unpack_from('<I',raw,pos)[0];pos+=4
  for _ in range(count):
   row=raw[pos:pos+160];pos+=160;name=row[:8].split(b'\0')[0].decode()
   if name=='LGS1HTH':
    frames,speed=struct.unpack_from('<ii',row,8);animation=dict(code=name,frames=frames,speed=speed,events=[(i,x) for i,x in enumerate(row[16:]) if x])
 assert pos==len(raw) and animation==dict(code='LGS1HTH',frames=15,speed=256,events=[])
 mode=source('source/D2Game/src/MONSTER/MonsterMode.cpp');unit=source('source/D2Game/src/UNIT/SUnit.cpp');common=source('source/D2Common/src/Units/Units.cpp')
 start=mode.index('constexpr D2MonModeInfoTableStrc stru_6FD28738[16] =');end=mode.index('};',start)+2;table=mode[start:end]
 flags=re.findall(r'\{\s*(\d),\s*(\d),\s*(\d)\s*\}',table);assert flags[8]==('0','0','1')
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <vector>
#include <initializer_list>
#define __fastcall
#define __stdcall
#define D2_ASSERT assert
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_TYPES_COUNT=6,NUMBER_OF_PLAYERCLASSES=7,NUM_WEAPON_CLASSES=14,WEAPONCLASS_HTH=0;
constexpr int PLRMODE_ATTACK1=4,PLRMODE_ATTACK2=5,PLRMODE_SPECIAL3=14,PLRMODE_SPECIAL4=15;
constexpr int MONMODE_DEATH=0,MONMODE_NEUTRAL=1,MONMODE_WALK=2,MONMODE_DEAD=12,MONMODE_RUN=15,MONMODE_SKILL1=8,MONMODE_SKILL3=10,MONMODE_SKILL4=11,MONMODE_SEQUENCE=14;
constexpr int MONSTER_VULTURE1=1,MONSTER_WILLOWISP1=2,MONSTER_BATDEMON1=3,MONSTER_FROGDEMON1=4,MONSTER_FIREBEAST=5,MONSTER_ICEGLOBE=6,MONSTER_TRAPPEDSOUL1=7,STATE_FREEZE=1,STATE_UNINTERRUPTABLE=2,STATE_STUNNED=3;
constexpr int EVENTTYPE_MODECHANGE=0,EVENTTYPE_ENDANIM=1,EVENTTYPE_AITHINK=2;
struct D2GameStrc{int dwGameFrame=100,nGameType=0,dwGameType=0,nDifficulty=0;};
struct D2AnimSeqTxt{};struct Anim{uint8_t pFrameFlags[144]={};};
struct D2MonStatsTxt{int nBaseId=411,nSplEndGeneric=0,nAIdel[3]={15,15,15};}record;
struct D2MonStats2Txt{uint8_t nModeEnabledWhenMovingFlags[2]={};}model;
struct MonsterData{D2MonStatsTxt* pMonstatsTxt=&record;};
struct D2UnitStrc{int dwUnitType=1,dwClassId=411,dwAnimMode=1,dwSeqSpeed=256,dwSeqFrameCount=0,wAnimSpeed=256,dwFrameCountPrecise=15*256,nSeqCurrentFramePrecise=0;D2AnimSeqTxt* pAnimSeq=nullptr;Anim* pAnimData=nullptr;MonsterData* pMonsterData=nullptr;bool stunned=false;};
struct D2ModeChangeStrc{D2UnitStrc* pUnit=nullptr;int nMode=0;D2UnitStrc* pTargetUnit=nullptr;};
struct D2MonModeInfoTableStrc{int unk0x00,unk0x04,unk0x08;};
struct D2MonModeCallbackTableStrc{int(*unk0x00)(D2GameStrc*,D2ModeChangeStrc*);int(*unk0x04)(D2GameStrc*,D2UnitStrc*);void(*unk0x08)(D2GameStrc*,D2UnitStrc*);};
unsigned gdwBitMasks[32];struct Event{int type,frame,arg,index;};std::vector<Event> events;int nextAi=125;
using MonEventFunc=void(*)(D2GameStrc*,D2UnitStrc*,int,int);MonEventFunc gpMonsterEventFunctions[3]={};
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int* type,int*,int*,const char*,int){assert(*type==UNIT_MONSTER);}
D2UnitStrc* D2Common_10434(D2UnitStrc*,int){assert(false);return nullptr;}int ITEMS_GetWeaponClassId(D2UnitStrc*){assert(false);return 0;}bool UNITS_CanDualWield(D2UnitStrc*){assert(false);return false;}
void DATATBLS_GetSequenceEvent(D2AnimSeqTxt*,int,int*){assert(false);}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int arg,int index){if(type==EVENTTYPE_AITHINK)nextAi=frame;else events.push_back({type,frame,arg,index});}
int EVENT_GetEventFrame(D2GameStrc*,D2UnitStrc*,int type){assert(type==EVENTTYPE_AITHINK);return nextAi;}
void SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc* u,int mode){u->dwAnimMode=mode;}
bool STATES_CheckState(D2UnitStrc* u,int state){return state==STATE_STUNNED&&u->stunned;}
bool SUNIT_IsDead(D2UnitStrc*){return false;}
void UNITS_ChangeAnimMode(D2UnitStrc*,int){assert(false);}void MONSTERMODE_EventHandler(D2GameStrc*,D2UnitStrc*,int,int,int){assert(false);}
void sub_6FCBDE90(D2UnitStrc*,int){assert(false);}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int id){assert(id==411);return &record;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int id){assert(id==411);return &model;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return nullptr;}
void UNITS_SetUsedSkill(D2UnitStrc*,void*){}void* UNITS_GetUsedSkill(D2UnitStrc*){return nullptr;}
int sub_6FD12FD0(D2GameStrc*,D2UnitStrc*){assert(false);return 0;}
int PATH_GetNumberOfPathPoints(void*){assert(false);return 0;}void D2Common_10142(void*,D2UnitStrc*,int){assert(false);}
void MONSTERMODE_GetModeChangeInfo(D2UnitStrc* u,int mode,D2ModeChangeStrc* m){*m={u,mode,nullptr};}
int sub_6FC64310(D2GameStrc*,D2ModeChangeStrc*);
int D2GAME_ModeChange_6FC65220(D2GameStrc* g,D2ModeChangeStrc* m,int a){assert(m->nMode==1&&a==1);return sub_6FC64310(g,m);}
void sub_6FC63680(D2GameStrc*,D2UnitStrc*);
const D2MonModeCallbackTableStrc* MONSTERMODE_GetCallbackTableRecord(D2UnitStrc*,int mode){assert(mode==8);static const D2MonModeCallbackTableStrc table{nullptr,nullptr,sub_6FC63680};return &table;}
'''
 # S1's nonmoving branch does not consult a dynamic path; declare its ABI slot.
 cpp=cpp.replace('bool stunned=false;','void* pDynamicPath=nullptr;bool stunned=false;')
 cpp+=table+'\n'
 cpp+=extract(common,'UNITS_GetFrameBonus')
 cpp+=extract(unit,'sub_6FCBCE70')
 for name in ['sub_6FC64310','sub_6FC645E0','sub_6FC63680','D2GAME_MONSTERS_AiFunction02_6FC65150']:
  cpp+=extract(mode,name)
 cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int speed:{0,128,256,341,512})for(int initial:{0,10,25,100})for(int stun:{0,1}){
 D2GameStrc game;Anim animation;MonsterData data;D2UnitStrc actor;actor.pAnimData=&animation;actor.pMonsterData=&data;actor.stunned=stun;actor.wAnimSpeed=speed;D2ModeChangeStrc change{&actor,8};nextAi=initial?100+initial:0;events.clear();
 int entered=sub_6FC645E0(&game,&change);assert(entered&&actor.dwAnimMode==8);int bonus=UNITS_GetFrameBonus(&actor);assert(!bonus);sub_6FCBCE70(&game,&actor);
 assert(events.size()==1&&events[0].type==EVENTTYPE_ENDANIM);int end=events[0].frame-100;game.dwGameFrame=events[0].frame;
 D2GAME_MONSTERS_AiFunction02_6FC65150(&game,&actor,0,0);assert(actor.dwAnimMode==1);
 if(speed==256&&initial==25&&!stun)assert(end==15&&nextAi==125);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",speed,initial,stun,entered,end,actor.dwAnimMode,nextAi-100,bonus);first=false;
}printf("]\n");}
'''
 p=STAGE/'sentry-emergence-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sentry-emergence-sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:];rows=json.loads(run.stdout);assert len(rows)==40
 out=STAGE/'sentry-emergence-source-probe.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',handlerEnabled=False,nativeVerifiedByThisProbe=False,nativeEvidence='tools/reference/sentry-emergence-native-evidence.json',legacyCommit=tree['sha'],sourceHashes=hashes,routineHashes=routines,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=40),cachedAnimation=dict(path='.local/research/AnimData.d2',sha256=animhash,provenanceComplete=False,record=animation if isinstance(animation,dict) else None),pinnedTables=dict(repository=manifest['repository'],commit=manifest['commit'],records={'monster':monster,'model':model,'skill':skill}),established=['Pinned current trap is size0, S1-enabled and nonmoving, with generic mode chart/end and AI delay15. Cached LGS1HTH is15frames/speed256/no action events.','Complete S1 entry, NPC frame-bonus routine, ordinary scheduler, ENDANIM callback, generic end and neutral handoff produce end+15 and retain initial AI+25 at the declared cached rate. No extra25-frame delay is added after emergence.','Control rates and existing AI deadlines exercise zero-speed/minimum scheduler behavior, earlier/later AI preservation, ordinary15-frame neutral delay and the distinct stunned45-frame delay.'],limits=['Cached animation acquisition provenance is incomplete; exact current executable animation/rate remains unverified. Current pinned tables and supplementary reconstructed engine source have separate authority.','Mode-change bridge, callback lookup, state flags, unit identity, event list and table services are declared checked boundaries; the complete mode-change/world/event engine is not executed. Nonmonster/player/transformed frame branches assert rather than substitute NPC results.','Earlier-AI deadline controls inspect neutral rescheduling only; no AI execution interleaved before ENDANIM is claimed. Actual initial AI+25 is later than the cached15-frame emergence. Full learned do45/player cast, actors, offlining, save/reload and client graphics are separate native work.'],sanitizers='ASan/UBSan; exit0 and empty runtime stderr required')
 target=ROOT/'tools/reference/sentry-emergence-source-evidence.json';render=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print('40 complete S1 entry/scheduler/end/neutral source cases pass; cached emergence15 preserves AI25; native integration is verified separately.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
