#!/usr/bin/env python3
"""Whole original AI106 decision routine over ordered,explicit query/action services."""
import hashlib,itertools,json,random,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-master-ai-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/AI/AiThink.cpp';source=BASE/'d2moo-pinned-source'/rel;raw=source.read_bytes()
 assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel)
 name='AITHINK_Fn106_143_ShadowMaster';body=function(raw.decode(),name)
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <climits>
#include <cstdint>
#include <iostream>
#include <map>
#include <vector>
#define __fastcall
using WORD=uint16_t;
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,EVENTTYPE_AITHINK=0,COLLIDE_MISSILE_BARRIER=4;
constexpr unsigned gdwBitMasks[]={1,2,4};
enum{ELEMTYPE_NONE=0,ELEMTYPE_FIRE=1,ELEMTYPE_LTNG=2,ELEMTYPE_MAGIC=3,ELEMTYPE_COLD=4,ELEMTYPE_POIS=5,ELEMTYPE_FREEZE=12};
enum{STAT_DAMAGERESIST=0,STAT_FIRERESIST=1,STAT_LIGHTRESIST=2,STAT_COLDRESIST=3,STAT_POISONRESIST=4,STAT_MAGICRESIST=5};
enum{SHADOWMASTER_AI_PARAM_APPROACH_DISTANCE_MELEE_BONUS_PROGRESSIVE_BONUS=0,SHADOWMASTER_AI_PARAM_RANDOM_PICK_IGNORE_RANGE_BOSS_LEASH=1,SHADOWMASTER_AI_PARAM_ATTACK_CHANCE_PCT=2};
template<class T,class U>T D2Clamp(T v,U a,U b){return std::clamp(v,T(a),T(b));}
struct D2SkillsTxt{int nAiType,nAuraState,wAuraTargetState,nEType,wReqLevel,wAiBonus;unsigned dwFlags[1];int wSrvMissile,wSrvMissileA,wSrvDoFunc,wAuraStat[1];};
struct D2SkillStrc{int id,has,rank,chargeHas,charge,missileRange;D2SkillsTxt row;D2SkillStrc*next=nullptr;};
using D2StatListStrc=D2SkillStrc;
struct D2StatesTxt{int wGroup;};struct D2MissilesTxt{int wRange;};struct Data{int nStatesTxtRecordCount;}data;auto*sgptDataTables=&data;
struct D2MonsterStats{unsigned nDrain[3];};struct D2MonsterData{D2MonsterStats*pMonstatsTxt;};
struct D2UnitStrc{int v[19];int dwUnitType,pSeed=0;D2SkillStrc*pSkills=nullptr;D2MonsterData*pMonsterData=nullptr;D2MonsterStats stats;D2MonsterData monster;};
struct Control{int dwAiParam[3];}control;struct Monstats{int16_t wAiParam[8][3];}mon;
struct D2AiTickParamStrc{D2UnitStrc*pTarget;Control*pAiControl;Monstats*pMonstatsTxt;int bCombat,nTargetDistance;}tick;
struct D2GameStrc{int nDifficulty=0;}game;
struct D2ShadowMasterAiCallbackArgStrc{D2UnitStrc*unk0x00=nullptr,*unk0x04=nullptr;int unk0x08=INT_MAX,unk0x0C=0;D2UnitStrc*unk0x10=nullptr;int unk0x14=INT_MAX,unk0x18=0,unk0x1C=0,unk0x20=0;D2UnitStrc*unk0x24=nullptr;};
struct D2ShadowMasterSkillStrc{D2UnitStrc*pTarget;int nSkillId,nChance,nX,nY;};
std::map<int,D2UnitStrc>units;std::map<int,D2SkillStrc>skills;std::vector<D2SkillStrc*>order;std::vector<D2StatesTxt>states;
std::vector<std::vector<int>>trace;std::vector<int>rolls,outcomes;int ctx[22],scan[9],rngAt,actionAt,leftSkill;
int id(D2UnitStrc*u){return u?u->v[0]:0;}D2UnitStrc*unit(int n){return n?&units.at(n):nullptr;}void add(std::initializer_list<int>x){trace.emplace_back(x);}
int roll(){int r=rolls.at(rngAt%rolls.size());rngAt++;return r;}
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int n){add({1,n});}
D2UnitStrc*AIGENERAL_GetMinionOwner(D2UnitStrc*u){add({2,id(u)});return u?unit(u->v[4]):nullptr;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){add({3});}
int AITHINK_GetSquaredDistance(D2UnitStrc*a,D2UnitStrc*b){int d=b?b->v[6]:0;add({4,id(a),id(b),d});return d;}
bool sub_6FCE34E0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,D2UnitStrc*o,int combat,D2AiTickParamStrc*,int flags,int leash){add({5,id(t),id(o),combat,flags,leash,ctx[18]});return ctx[18];}
D2UnitStrc*SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*u){int t=u?u->v[5]:0;add({6,id(u),t});return unit(t);}
int sub_6FCECBA0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,WORD sid,int combat,D2UnitStrc*t,int x,int y,D2AiTickParamStrc*){int r=0;if(!t||(id(t)!=1&&id(t)!=units.at(1).v[4])){r=outcomes.at(actionAt%outcomes.size());actionAt++;}add({7,sid,combat,id(t),x,y,r});return r;}
D2SkillStrc*SKILLS_GetFirstSkillFromSkillList(D2SkillStrc*){add({8});return order.empty()?nullptr:order[0];}D2SkillStrc*SKILLS_GetNextSkill(D2SkillStrc*s){return s->next;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc*s,const char*,int){return s->id;}
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int sid){auto it=skills.find(sid);add({31,sid});return it==skills.end()||!it->second.has?nullptr:&it->second.row;}
bool STATES_CheckState(D2UnitStrc*u,int state){bool r=u&&state>=0&&state<31&&(u->v[11]&(1<<state));add({30,id(u),state,int(r)});return r;}
D2StatesTxt*SKILLS_GetStatesTxtRecord(int n){int g=n>=0&&n<int(states.size())?states[n].wGroup:-1;add({9,n,g});return g<0?nullptr:&states[n];}
int AI_RollPercentage(D2UnitStrc*){int r=roll()%100;add({10,r});return r;}
D2SkillStrc dummy;D2SkillStrc*UNITS_GetLeftSkill(D2UnitStrc*){add({11,leftSkill});return leftSkill<0?nullptr:&dummy;}
void SKILLS_SetLeftActiveSkill(D2UnitStrc*,int sid,int owner){assert(owner==-1);leftSkill=sid;add({12,sid});}
bool SUNIT_IsDead(D2UnitStrc*u){int r=u?u->v[2]:0;add({29,id(u),r});return r;}
bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*u){int r=u?u->v[3]:0;add({28,id(u),r});return r;}
int AI_GetParamValue(D2GameStrc*,D2AiTickParamStrc*,int param){assert(param==2);return ctx[6];}
D2UnitStrc*AITHINK_TargetCallback_ShadowMaster(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,void*){assert(false);return nullptr;}
void sub_6FCF1E80(D2GameStrc*,D2UnitStrc*,void*raw,D2UnitStrc*(*callback)(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,void*),int one){assert(callback==AITHINK_TargetCallback_ShadowMaster&&one==1);add({16});auto*a=(D2ShadowMasterAiCallbackArgStrc*)raw;a->unk0x04=unit(scan[0]);a->unk0x08=scan[1];a->unk0x0C=scan[2];a->unk0x10=unit(scan[3]);a->unk0x14=scan[4];a->unk0x18=scan[5];a->unk0x1C=scan[6];a->unk0x20=scan[7];a->unk0x24=unit(scan[8]);}
bool AITHINK_IsTargetKillableByShadowMaster(D2UnitStrc*,D2UnitStrc*t){int r=t?t->v[7]:0;add({17,id(t),r});return r;}
int UNITS_GetCurrentLifePercentage(D2UnitStrc*){add({18,ctx[14]});return ctx[14];}
bool STATES_CheckStateMaskPgsvOnUnit(D2UnitStrc*){add({13,ctx[15]});return ctx[15];}
int UNITS_TestCollisionWithUnit(D2UnitStrc*,D2UnitStrc*t,int mask){assert(mask==COLLIDE_MISSILE_BARRIER);add({15,id(t)});return ctx[16]?0:mask;}
unsigned ITEMS_RollRandomNumber(int*){int r=roll();add({14,r});return unsigned(r);}
void AITACTICS_RunToTargetUnit(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t){add({26,id(t)});}
bool sub_6FCD06D0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,unsigned flags,int mode){add({27,id(t),int(flags),mode,ctx[17]});return ctx[17];}
int STATLIST_UnitGetStatValue(D2UnitStrc*t,int stat,int layer){assert(layer==0&&stat>=0&&stat<6);int r=t?t->v[12+stat]:0;add({19,id(t),stat,r});return r;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*s,int bonus){assert(bonus==1);add({20,s->id,s->rank});return s->rank;}
int ITEMS_RollLimitedRandomNumber(int*,int n){int r=roll()%std::max(n,1);add({21,n,r});return r;}
D2StatListStrc*GetAuraStateStatList(D2UnitStrc*,D2SkillsTxt*r){int sid=r->wAuraStat[0];add({22,sid});return skills.at(sid).chargeHas?&skills.at(sid):nullptr;}
int STATLIST_GetStatValue(D2StatListStrc*s,int stat,int layer){assert(stat==s->id&&layer==0);add({23,s->id,s->charge});return s->charge;}
D2MissilesTxt missile;D2MissilesTxt*SKILLS_GetMissilesTxtRecord(int mid){int range=-1;for(auto&it:skills)if(it.second.row.wSrvMissileA==mid){range=it.second.missileRange;break;}add({24,mid,range});missile.wRange=range;return range<0?nullptr:&missile;}
bool UNITS_IsInMeleeRange(D2UnitStrc*,D2UnitStrc*t,int zero){assert(zero==0);int r=t?t->v[18]:0;add({25,id(t),r});return r;}
'''+body+r'''
int main(){int count;std::cin>>count;std::cout<<'[';for(int c=0;c<count;c++){
 for(int&i:ctx)std::cin>>i;for(int&i:scan)std::cin>>i;int n;std::cin>>n;states.resize(n);for(auto&s:states)std::cin>>s.wGroup;data.nStatesTxtRecordCount=ctx[21];
 std::cin>>n;rolls.resize(n);for(int&i:rolls)std::cin>>i;std::cin>>n;outcomes.resize(n);for(int&i:outcomes)std::cin>>i;
 units.clear();std::cin>>n;for(int i=0;i<n;i++){D2UnitStrc u{};for(int&v:u.v)std::cin>>v;u.dwUnitType=u.v[1];units[u.v[0]]=u;}
 for(auto&[key,u]:units){u.stats.nDrain[0]=unsigned(u.v[10]);u.monster.pMonstatsTxt=u.v[9]?&u.stats:nullptr;u.pMonsterData=u.v[8]?&u.monster:nullptr;}
 skills.clear();order.clear();std::cin>>n;for(int i=0;i<n;i++){int r[16];for(int&v:r)std::cin>>v;D2SkillStrc s{};s.id=r[0];s.has=r[1];s.rank=r[12];s.chargeHas=r[13];s.charge=r[14];s.missileRange=r[15];s.row={r[2],r[3],r[4],r[5],r[6],r[7],{r[8]?4u:0u},r[9],r[10],r[11],{r[0]}};skills[s.id]=s;order.push_back(&skills.at(s.id));}
 for(size_t i=1;i<order.size();i++)order[i-1]->next=order[i];D2SkillStrc empty;units.at(1).pSkills=ctx[10]?(order.empty()?&empty:order[0]):nullptr;
 mon={};mon.wAiParam[0][0]=ctx[0];mon.wAiParam[0][1]=ctx[1];mon.wAiParam[0][2]=ctx[2];mon.wAiParam[1][0]=ctx[3];mon.wAiParam[1][1]=ctx[5];mon.wAiParam[1][2]=ctx[4];control={{ctx[11],ctx[12],ctx[13]}};tick={unit(ctx[9]),&control,&mon,ctx[7],ctx[8]};
 rngAt=actionAt=0;leftSkill=ctx[19];trace.clear();AITHINK_Fn106_143_ShadowMaster(&game,&units.at(1),&tick);
 if(c)std::cout<<',';std::cout<<"{\"memory\":["<<control.dwAiParam[0]<<','<<control.dwAiParam[1]<<','<<control.dwAiParam[2]<<"],\"left\":"<<leftSkill<<",\"rng\":"<<rngAt<<",\"actions\":"<<actionAt<<",\"trace\":[";bool first=true;
 for(auto&r:trace){if(!first)std::cout<<',';first=false;std::cout<<'[';for(size_t i=0;i<r.size();i++){if(i)std::cout<<',';std::cout<<r[i];}std::cout<<']';}std::cout<<"]}";
 }std::cout<<"]\n";}
'''
 def skill(sid=261,ai=3,**v):
  d=dict(has=1,ai=ai,aura=0,targetAura=0,element=2,required=12,bonus=0,progressive=0,missile=-1,missileA=sid+2000,do=45,rank=10,chargeHas=0,charge=0,range=20);d.update(v);return[sid,*d.values()]
 def make(seed=1):
  r=random.Random(seed);units=[]
  for ident in [1,2,100,101,102,110,111,112,120]:units.append([ident,0 if ident==2 else 1,0,1,2 if ident==1 else 0,0,4 if ident==2 else 100,0,1,1,50,0,*([0]*6),0])
  return dict(context=[5,3,10,10,40,50,50,0,10,100,1,0,261,20,75,0,1,0,0,-1,0,4],scan=[110,100,1,111,100,1,3,0,112],groups=[0,2,2,0],rolls=[r.randrange(0x80000000)for _ in range(32)],outcomes=[0,1,0,1],units=units,skills=[skill(0,4,element=0,required=1,do=0),skill()])
 cases=[]
 # Isolate every AI type/health branch,with and without a progressive list.
 for ai,life,prog in itertools.product([0,1,2,3,4,5,6,7,8,9,10,11,12,13],[44,45,49,50,66,67,74,75],[0,1]):
  c=make(len(cases)+1);c['context'][14]=life;c['skills']=[skill(261,ai,aura=1 if ai in[1,2,4,12] else 0,targetAura=2 if ai==2 else 0,progressive=prog,chargeHas=prog,charge=(life%4))];c['units'][0][11]=2 if ai==1 else 0;cases.append(c)
 # Header/leash/repetition/no-target paths and precombat buff/state-group paths.
 for owner,target,repeat,has,move in itertools.product([0,1],[0,100],[0,2],[0,1],[0,1]):
  c=make(3000+len(cases));c['units'][0][4]=2 if owner else 0;c['context'][9]=target;c['context'][10]=has;c['context'][11]=repeat;c['context'][18]=move;c['units'][1][6]=1601 if move else 144
  c['units'][0][5]=101 if repeat else 0;c['skills']=[skill(258,1,aura=1,element=0,do=18),skill(252,6,element=0,do=0)];cases.append(c)
 rng=random.Random(106143)
 for _ in range(240):
  c=make(5000+len(cases));ctx=c['context'];ctx[2]=rng.choice([0,10,20]);ctx[3]=rng.choice([0,1,10,25]);ctx[7]=rng.randrange(2);ctx[8]=rng.choice([0,10,50,51]);ctx[9]=rng.choice([0,100]);ctx[11]=rng.choice([0,1,25]);ctx[12]=rng.choice([0,261]);ctx[13]=rng.choice([0,1,20,80]);ctx[14]=rng.choice([44,45,49,50,66,67,74,75,100]);ctx[15]=rng.randrange(2);ctx[16]=rng.randrange(2);ctx[17]=rng.randrange(2);ctx[18]=rng.randrange(2);ctx[19]=rng.choice([-1,0,252]);ctx[21]=rng.choice([0,4]);
  c['scan']=[110,rng.choice([0,25,26,100]),rng.choice([0,3,4,6]),rng.choice([0,111]),100,rng.choice([0,3,4]),rng.choice([0,2,3,6]),rng.choice([0,5,6,10]),rng.choice([0,112])]
  c['outcomes']=[rng.randrange(2)for _ in range(8)]
  for u in c['units']:
   u[2]=int(rng.random()<.15);u[3]=int(rng.random()>.15);u[4]=(2 if rng.random()>.2 else 0)if u[0]==1 else(120 if rng.random()<.2 else 0);u[5]=rng.choice([0,101,102])if u[0]in[1,2] else 0;u[6]=rng.choice([0,25,26,36,37,100,143,144,324,325,1023,1024,1600,1601]);u[7]=rng.randrange(2);u[8]=rng.randrange(2);u[9]=rng.randrange(2);u[10]=rng.choice([0,24,25,100]);u[11]=rng.randrange(16);u[12:18]=[rng.choice([-100,-19,-9,0,99,100,150])for _ in range(6)];u[18]=rng.randrange(2)
  c['skills']=[skill(250+i,rng.choice([0,1,2,3,4,5,6,7,8,11,12,13]),has=int(rng.random()>.1),aura=rng.choice([0,1,2,5]),targetAura=rng.choice([0,2]),element=rng.choice([0,1,2,3,4,5,6,9,12]),required=rng.choice([1,6,12,30]),bonus=rng.choice([-10,0,10]),progressive=rng.randrange(2),missile=rng.choice([-1,0]),do=rng.choice([0,18,19,45]),rank=rng.choice([1,20,40]),chargeHas=rng.randrange(2),charge=rng.randrange(4),range=rng.choice([-1,1,6,20]))for i in range(8)];cases.append(c)
 # Deterministic positive candidates prove reverse list order,not score sorting.
 c=make(9000);c['rolls']=[99]*32;c['outcomes']=[1];c['context'][7]=1;c['context'][6]=5;c['skills']=[skill(251,3,bonus=100,do=0),skill(252,3,bonus=1,do=19)];cases.append(c)
 lines=[str(len(cases))]
 for c in cases:
  lines.extend([' '.join(map(str,c['context'])),' '.join(map(str,c['scan']))])
  for key in ['groups','rolls','outcomes']:lines.extend([str(len(c[key])),' '.join(map(str,c[key]))])
  for key in ['units','skills']:lines.append(str(len(c[key])));lines.extend(' '.join(map(str,r))for r in c[key])
 code=STAGE/'source.cpp';code.write_text(cpp);binary=STAGE/'source';subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(code),'-o',str(binary)],check=True)
 run=subprocess.run([str(binary)],input='\n'.join(lines)+'\n',text=True,capture_output=True);(STAGE/'sanitizer.log').write_text(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr[-3000:]
 results=json.loads(run.stdout);assert len(results)==len(cases);assert results[-1]['memory'][:2]==[25,252] and next(t[1]for t in results[-1]['trace']if t[0]==7)==252
 path=STAGE/'cases.json';path.write_text(json.dumps([dict(input=c,expected=r)for c,r in zip(cases,results)],separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',olderSourceTree=tree['sha'],contexts=len(cases),completeRoutineHashes={name:hashlib.sha256(body.encode()).hexdigest()},inputs={str(p.relative_to(ROOT)):sha(p)for p in[source,Path(__file__).resolve()]},artifacts={str(p.relative_to(ROOT)):sha(p)for p in[code,path,STAGE/'sanitizer.log']},scope='Whole older AI106/143 branch/scoring/ordered-attempt/repeat-memory policy compiled unchanged with ASan/UBSan. Target scan/special classification and own-skill tactic are declared prequalified service boundaries;all world rows,geometry,RNG outputs,movement and action outcomes are explicit fixtures. Native C# must compare ordered traces,memory,left selection and RNG/action counts. No automatic world Master or modern-executable parity is established.')
 (ROOT/'tools/reference/shadow-master-ai-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n');print(f'Whole Master AI:{len(cases)}contexts;ASan/UBSan clean;positive candidates attempted in reverse skill-list order.')
if __name__=='__main__':main()
