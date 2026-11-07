#!/usr/bin/env python3
"""Complete pinned AI105/eligibility routines against ordered host services."""
import hashlib,itertools,json,random,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-warrior-ai-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 rel='source/D2Game/src/AI/AiThink.cpp';p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes()
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel)
 names=['AITHINK_ShadowWarriorCheckUseSkill','AITHINK_Fn105_ShadowWarrior'];bodies={n:function(raw.decode(),n)for n in names}
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr int16_t operator""i16(unsigned long long n){return int16_t(n);}
using BOOL=int;
constexpr int FALSE=0,TRUE=1,EVENTTYPE_AITHINK=0,SKILLSFLAGINDEX_PROGRESSIVE=0;
constexpr unsigned gdwBitMasks[]={1};
enum{SHADOWWARRIOR_AI_PARAM_MAX_TARGET_DISTANCE=0,SHADOWWARRIOR_AI_PARAM_MAX_BOSS_DISTANCE=1,SHADOWWARRIOR_AI_PARAM_ATTACK_CHANCE_PCT=2,SHADOWWARRIOR_AI_PARAM_SKILL_DECREMENT=3,SHADOWWARRIOR_AI_PARAM_SUMMONING_SKILL_MIN_MAX_TO_USE_SKILL=7};
template<class T,class U>T D2Clamp(T v,U a,U b){return std::clamp(v,T(a),T(b));}
struct D2GameStrc{int dwGameFrame;} game;
struct D2SkillsTxt{int nAiType,nAuraState,wAuraTargetState;unsigned dwFlags[1];int wAuraStat[1],dwDelay;};
struct D2SkillStrc{int id,cls,mode,ai,rank,mana,delay,aura,targetAura,progressive,range,selfState,targetState,charges;D2SkillsTxt row;} left,right,attack;
struct D2UnitStrc{int dwClassId=6,pSeed=0;} unit,owner,target;
struct AiControl{int dwAiParam[3];} control;
struct Monstats{int16_t wAiParam[8][3];} mon;
struct D2AiTickParamStrc{D2UnitStrc*pTarget;AiControl*pAiControl;Monstats*pMonstatsTxt;int nTargetDistance,bCombat;} tick;
using D2StatListStrc=D2SkillStrc;
int ctx[27],pct,assigns;bool hasAttack;
using Trace=std::vector<int>;std::vector<Trace> trace;
void add(std::initializer_list<int>x){trace.emplace_back(x);}
D2SkillStrc*skill(int id){if(id==0)return hasAttack?&attack:nullptr;return left.id==id?&left:right.id==id?&right:nullptr;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){}
D2UnitStrc*AIGENERAL_GetMinionOwner(D2UnitStrc*){return ctx[0]?&owner:nullptr;}
int AI_GetParamValue(D2GameStrc*,D2AiTickParamStrc*,int n){return n==7?0:ctx[7+n];}
int UNITS_GetDistanceToOtherUnit(D2UnitStrc*,D2UnitStrc*){return ctx[5];}
bool sub_6FCE34E0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,D2UnitStrc*,int combat,D2AiTickParamStrc*,int f,int leash){add({5,t?ctx[2]:0,combat,f,leash});return ctx[15];}
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int n){add({6,n});}
D2SkillStrc*UNITS_GetRightSkill(D2UnitStrc*){return ctx[17]?&right:nullptr;}
D2SkillStrc*UNITS_GetLeftSkill(D2UnitStrc*){return ctx[16]?&left:nullptr;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc*s,const char*,int){return s->id;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*s,int){return s->rank;}
void SKILLS_AssignSkill(D2UnitStrc*,int id,int,int,const char*,int){if(id==0&&!hasAttack){hasAttack=true;add({9,0,1});}else if(++assigns==2)add({1,left.id,right.id});}
D2SkillStrc*SKILLS_GetSkillById(D2UnitStrc*,int id,int){return skill(id);}
unsigned ITEMS_RollRandomNumber(int*){add({2,ctx[18]});return ctx[18];}
int AI_RollPercentage(D2UnitStrc*){int v=ctx[19+(pct++%4)];add({3,v});return v;}
int ITEMS_RollLimitedRandomNumber(int*,int n){int v=ctx[23]%std::max(n,1);add({4,n,v});return v;}
int SKILLS_GetClassIdFromSkillId(int id){auto*s=skill(id);return s?s->cls:-1;}
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){auto*s=skill(id);return s?&s->row:nullptr;}
bool AITHINK_ShadowMasterCheckTargetPetType(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int id){return id!=ctx[24];}
bool STATES_CheckState(D2UnitStrc*u,int state){auto*s=skill(state%1000);assert(s);return u==&unit?s->selfState:s->targetState;}
D2StatListStrc*STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int state){return skill(state%1000);}
int STATLIST_GetStatValue(D2StatListStrc*s,int,int){return s->charges;}
int SKILLS_GetShiftedManaCosts(int id,int){return skill(id)->mana;}
int D2Common_11016(D2UnitStrc*,D2SkillStrc*s){return s->range;}
void AITACTICS_RunToTargetUnit(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t){add({7,t?ctx[2]:0});}
int SKILLS_GetSkillMode(D2SkillStrc*s){return s->mode;}
void AITACTICS_UseSkill(D2GameStrc*,D2UnitStrc*,int mode,int id,D2UnitStrc*t,int,int){add({8,id,mode,t?ctx[2]:0});}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int delay,int,int){return delay;}
'''+ '\n'.join(bodies.values())+r'''
void readSkill(D2SkillStrc&s){int v[14];for(auto&x:v)assert(scanf("%d",&x)==1);s={v[0],v[1],v[2],v[3],v[4],v[5],v[6],v[7],v[8],v[9],v[10],v[11],v[12],v[13],{}};s.row={s.ai,s.aura?1000+s.id:0,s.targetAura?2000+s.id:0,{unsigned(s.progressive)},{1},s.delay};}
int main(){int count;assert(scanf("%d",&count)==1);printf("[");for(int n=0;n<count;n++){
for(auto&v:ctx)assert(scanf("%d",&v)==1);readSkill(left);readSkill(right);
attack={0,-1,4,4,1,0,0,0,0,0,1,0,0,0,{4,0,0,{0},{0},0}};hasAttack=!ctx[26];pct=assigns=0;trace.clear();
control={{ctx[13],ctx[14],ctx[6]}};mon={};mon.wAiParam[7][1]=ctx[11];mon.wAiParam[7][2]=ctx[12];game.dwGameFrame=ctx[1];
tick={ctx[2]?&target:nullptr,&control,&mon,ctx[3],ctx[4]};AITHINK_Fn105_ShadowWarrior(&game,&unit,&tick);
printf("%s[[%d,%d],[",n?",":"",control.dwAiParam[0],control.dwAiParam[1]);bool sep=false;for(auto&r:trace){printf("%s[",sep?",":"");sep=true;for(unsigned i=0;i<r.size();i++)printf("%s%d",i?",":"",r[i]);printf("]");}printf("]]");}printf("]\n");}
'''
 # Contexts exercise policy gates/cooldown/budget/RNG with synthetic query
 # results. Skill assignment ranks and actual world queries have other tests.
 base=[1,100,17,4,0,0,20,40,30,60,1,5,64,0,0,0,1,1,0,0,0,0,0,0,-1,0,0]
 bolt=[251,6,9,5,10,3,0,0,0,0,2,0,0,0];other=[256,6,9,5,8,6,30,0,0,0,2,0,0,0]
 cases=[]
 def emit(c=base,l=bolt,r=other):cases.append(dict(Context=c.copy(),Left=l.copy(),Right=r.copy()))
 emit()
 for index,values in {0:[0],2:[0],3:[40,41],4:[1],5:[30,31],6:[1,99],11:[0,129],12:[0,257],13:[99,100,101],14:[1,4,5,2048,2049,4096,4097],15:[1],16:[0],17:[0],18:[1],24:[251,256],26:[1]}.items():
  for v in values:c=base.copy();c[index]=v;emit(c)
 for ai,combat,flags,state in itertools.product([1,2,4,5,11,12,13],[0,1],[0,1,2,3],[0,1]):
  c=base.copy();c[4]=combat;l=bolt.copy();l[3]=ai;l[7]=flags&1;l[8]=flags>>1;l[9]=int(flags==3);l[11]=state;l[12]=state;l[13]=3 if state else 2;emit(c,l,l)
 rng=random.Random(105)
 for _ in range(180):
  c=base.copy();c[4]=rng.randrange(2);c[6]=rng.choice([1,20,40]);c[13]=rng.choice([0,101]);c[14]=rng.choice([0,5,100,2049]);c[18]=rng.randrange(2);c[19:23]=[rng.randrange(100)for _ in range(4)];c[23]=rng.randrange(4000);c[24]=rng.choice([-1,251,256]);c[26]=rng.randrange(2)
  l=bolt.copy();r=other.copy()
  for s in (l,r):s[1]=rng.choice([-1,6]);s[3]=rng.choice([1,4,5,13]);s[5]=rng.choice([0,3,40,80]);s[10]=rng.choice([1,2])
  emit(c,l,r)
 path=STAGE/'source.cpp';path.write_text(cpp);binary=STAGE/'probe'
 result=subprocess.run(['g++','-std=c++20','-O1','-fsanitize=address,undefined','-fno-omit-frame-pointer','-no-pie',str(path),'-o',str(binary)],capture_output=True,text=True);(STAGE/'compiler.log').write_text(result.stdout+result.stderr);assert result.returncode==0,result.stderr[-4000:]
 request=' '.join(map(str,[len(cases)]+[v for c in cases for key in ['Context','Left','Right']for v in c[key]]))+'\n'
 result=subprocess.run([str(binary)],input=request,capture_output=True,text=True);(STAGE/'sanitizer.log').write_text(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr
 for c,expected in zip(cases,json.loads(result.stdout),strict=True):c['Expected']=expected
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',olderSourceTree=tree['sha'],contexts=len(cases),completeRoutineHashes={n:hashlib.sha256(v.encode()).hexdigest()for n,v in bodies.items()},inputs={str(p.relative_to(ROOT)):sha(p),str(Path(__file__).relative_to(ROOT)):sha(Path(__file__))},artifacts={str(p.relative_to(ROOT)):sha(p)for p in [path,fixture]},scope='Whole older AI105 plus eligibility; ordered tactic/RNG results and saved parameters. Query/skill inputs are explicit synthetic service contexts. Modern parity, AI106 and intrinsic skill consumers are not established.')
 (ROOT/'tools/reference/shadow-warrior-ai-source-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n');print(f'Warrior AI105: {len(cases)} whole-source contexts; ASan/UBSan clean')
if __name__=='__main__':main()
