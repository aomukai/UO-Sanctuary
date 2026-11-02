#!/usr/bin/env python3
"""Compose Hydra AI86 with full tactics and seed percentage routines."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/hydra-ai-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 sources={};bodies={}
 for rel,names in [('source/D2Common/include/D2Seed.h',['SEED_RollRandomNumber','SEED_RollPercentage']),('source/D2Game/src/AI/AiThink.cpp',['AI_RollPercentage','AITHINK_Fn086_Hydra']),('source/D2Game/src/AI/AiTactics.cpp',['AITACTICS_Idle','AITACTICS_IdleInNeutralMode','AITACTICS_UseSkill','AITACTICS_ChangeModeAndTargetCoordinates'])]:
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel);sources[rel]=sha(p)
  for name in names:bodies[name]=function(raw.decode(),name)
 cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#include <initializer_list>
#define __fastcall
// Preserve the canonical Microsoft integer literal with an equivalent ABI
// literal adapter; none of the extracted function bodies is rewritten.
constexpr unsigned long long operator""i64(unsigned long long x){return x;}
constexpr int MONMODE_NEUTRAL=1,MONMODE_DEATH=0,UNITFLAG_SKSRVDOFUNC=64,EVENTTYPE_AITHINK=2;
struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};
struct D2SkillStrc{}skill;
struct D2UnitStrc{int dwAnimMode=1,dwFlags=0,id=1;D2SeedStrc pSeed{};void* pDynamicPath=nullptr;D2SkillStrc* used=nullptr;};
struct D2GameStrc{int dwGameFrame=1000;};
struct D2ModeChangeStrc{D2UnitStrc* pUnit=nullptr;int mode=0,nX=0,nY=0;D2UnitStrc* pTargetUnit=nullptr;};
struct D2AiControlStrc{int dwAiParam[3]={};};struct Monstats{uint8_t nSkillMode[1]={4};int nSkill[1]={337};};
struct D2AiTickParamStrc{D2AiControlStrc* pAiControl;D2UnitStrc* pTarget;int nTargetDistance;Monstats* pMonstatsTxt;};
struct Request{int mode,target,x,y,force,result;};std::vector<Request> requests;
int modeSuccess=1,hasSkill=1,nextAi=-1,events=0,deletes=0,lookups=0,steps=0;
void MONSTERMODE_GetModeChangeInfo(D2UnitStrc* p,int mode,D2ModeChangeStrc* out){out->pUnit=p;out->mode=mode;}
int D2GAME_ModeChange_6FC65220(D2GameStrc*,D2ModeChangeStrc* m,int force){requests.push_back({m->mode,m->pTargetUnit?m->pTargetUnit->id:0,m->nX,m->nY,force,modeSuccess});if(modeSuccess)m->pUnit->dwAnimMode=m->mode;return modeSuccess;}
D2SkillStrc* SKILLS_GetSkillById(D2UnitStrc*,int id,int owner){assert(id==337&&owner==-1);lookups++;return hasSkill?&skill:nullptr;}
void UNITS_SetUsedSkill(D2UnitStrc* p,D2SkillStrc* s){p->used=s;}
void PATH_SetStepNum(void*,int value){assert(value==1);steps++;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int zero){assert(type==EVENTTYPE_AITHINK&&zero==0);deletes++;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int a,int b){assert(type==EVENTTYPE_AITHINK&&a==0&&b==0);nextAi=frame;events++;}
'''
 cpp+='\n'.join(bodies[n] for n in ('SEED_RollRandomNumber','SEED_RollPercentage','AI_RollPercentage','AITACTICS_Idle','AITACTICS_IdleInNeutralMode','AITACTICS_UseSkill','AITACTICS_ChangeModeAndTargetCoordinates','AITHINK_Fn086_Hydra'))
 cpp+=r'''
int main(){bool first=true;printf("[");
for(uint32_t low:{0u,1u,2u,3u,17u,29u,59u,60u,61u,99u,0x80000000u,0xffffffffu})for(uint32_t high:{0u,666u,0xffffffffu})for(int expiry:{999,1000,1001})for(int target:{0,1})for(int distance:{24,25,26})for(int mode:{1,4,9})for(int success:{0,1})for(int skillMode:{4,16})for(int known:{0,1}){
 D2GameStrc game;D2UnitStrc actor,enemy;actor.dwAnimMode=mode;actor.pSeed.nLowSeed=low;actor.pSeed.nHighSeed=high;enemy.id=7;D2AiControlStrc control;control.dwAiParam[0]=expiry;Monstats row;row.nSkillMode[0]=skillMode;D2AiTickParamStrc tick{&control,target?&enemy:nullptr,distance,&row};
 modeSuccess=success;hasSkill=known;nextAi=-1;events=deletes=lookups=steps=0;requests.clear();AITHINK_Fn086_Hydra(&game,&actor,&tick);
 printf("%s[[%u,%u,%d,%d,%d,%d,%d,%d,%d],[%llu,%d,%d,%d,%d,%d,%d,%d,%d],[",first?"":",",low,high,expiry,target,distance,mode,success,skillMode,known,(unsigned long long)actor.pSeed.lSeed,actor.dwAnimMode,actor.dwFlags,actor.used!=nullptr,nextAi,events,deletes,lookups,steps);first=false;
 for(size_t i=0;i<requests.size();i++){auto r=requests[i];printf("%s[%d,%d,%d,%d,%d,%d]",i?",":"",r.mode,r.target,r.x,r.y,r.force,r.result);}printf("]]");
}printf("]\n");}
'''
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe'
 subprocess.run(['g++','-std=c++20','-Wno-literal-suffix','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==15552
 changed=0
 for inputs,out,requests in cases:
  low,high,expiry,target,distance,mode,success,skillmode,known=inputs;expired=expiry<1000;roll=not expired and target and distance<25;seed=high+0x6AC690C5*low if roll else (high<<32)|low;shoot=roll and seed%100<60;validshoot=shoot and skillmode<16;idle=not expired and (not shoot or validshoot and not success)
  changed+=int(bool(roll) and (seed%100<60)!=((seed&0xffffffff)%100<60));expected=[];finalmode=mode
  if expired:expected.append([0,0,0,0,1,success]);finalmode=0 if success else mode
  elif validshoot:
   expected.append([skillmode,7,0,0,0,success])
   if success:finalmode=skillmode
  if idle and finalmode!=1:expected.append([1,1,0,0,1,success]);finalmode=1 if success else finalmode
  assert requests==expected and out==[seed,finalmode,64 if validshoot else 0,int(validshoot and known),1010 if idle else -1,int(idle),int(idle),int(validshoot),int(validshoot)],(inputs,out,requests,expected)
 assert changed>0
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-Wno-literal-suffix','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 rowpath=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json';rows=json.loads(rowpath.read_text());manifest=json.loads((rowpath.parent.parent/'sources.json').read_text());assert sha(rowpath)==manifest['files']['json/monstats.json']['sha256']
 for key in ('hydra1','hydra2','hydra3'):assert rows[key]['AI']=='Hydra' and rows[key]['Skill1']=='HydraMissile' and rows[key]['Sk1mode']=='A1'
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},currentTable={str(rowpath.relative_to(ROOT)):sha(rowpath)},sanitizers='ASan/UBSan clean',percentageDecisionDifferencesFromLow32=changed,
  established=['Complete AI86,tactics use-skill/death/neutral/idle,and seed/percentage routines compose. Only expiry strictly before current frame requests death. At equality the ordinary target/chance branch still runs. Missing/out-of-range targets idle10without drawing RNG. Distance24is eligible;25and26are not.',
  'The canonical percentage routine uses the full64bit rolled value modulo100,not a narrowed low32bit result. The matrix includes high seeds0/666/UINT_MAX and low boundary values and demonstrates different decisions under the incorrect narrowing.',
  'Successful chance requests configured HydraMissile337in A1. Full tactics select its skill,set server-do flag64 and path step1 before mode change;failure requests neutral then schedules AI+10. A missing selected skill is forwarded as null rather than rejected in tactics. Invalid mode16returns without those writes or idle;this is a guarded helper control,not current Hydra gameplay.'],
  declaredServices=['Older engine,current pinned rows and canonical seed operations,not modern executable proof. The Microsoft i64literal has an equivalent C++ literal adapter;canonical function bodies are unchanged. Target/distance supplied by outer acquisition;skill lookup,mode transition acceptance/path/event storage remain explicit services.',
  'Full mode/animation dispatch,actual projectile creation/contact/HP,caster/head removal and final GUID lifetime,modern AI event order/global entropy and native input/save/client acceptance remain separate. Do not infer a sentry shot budget or target-search radius from the AI attack eligibility test. No C#/runtime/install/save change.'],
  artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),p,binary,san,log,fixture)})
 target=ROOT/'tools/reference/hydra-ai-source-evidence.json';render=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print(f'Hydra AI:15552complete AI/tactics/seed contexts;strict expiry/range,64bit percentage ({changed} low32decision differences);ASan/UBSan clean;outer world/mode separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');probe(p.parse_args().check)
