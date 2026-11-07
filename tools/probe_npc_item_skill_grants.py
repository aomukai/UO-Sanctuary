#!/usr/bin/env python3
"""Execute the complete older NPC97/107callback branch and intrinsic writers."""
import hashlib,json,random,subprocess
from pathlib import Path
from probe_shadow_initializer import routine
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-item-skill-grants-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);inputs={};tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def src(n):
  p=BASE/'d2moo-pinned-source'/n;r=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(r)).encode()+b'\0'+r).hexdigest()==next(t['sha']for t in tree['tree']if t['path']==n);inputs[str(p.relative_to(ROOT))]=sha(p);return r.decode()
 common=src('source/D2Common/src/D2Skills.cpp');server=src('source/D2Game/src/SKILLS/Skills.cpp');items=src('source/D2Game/src/ITEMS/ItemMode.cpp')
 branch=items[items.index('    case STAT_ITEM_NONCLASSSKILL:'):items.index('    case STAT_ITEM_ADDCLASSSKILLS:')]
 funcs={n:routine(common,n)for n in ['D2COMMON_SKILLS_RemoveSkill_6FDAFFF0','SKILLS_AddSkill','SKILLS_AssignSkill']}
 npcguard=routine(server,'sub_6FD108D0');assert 'if (pUnit && pUnit->dwUnitType == UNIT_PLAYER)'in npcguard
 metadata=ROOT/'data/Sanctuary/Balance/npc-skill-metadata.json';rows=json.loads(metadata.read_text());assert len(rows)==429
 modes=','.join(str(r['Mode'])for r in rows);elements=','.join(str(r['Element'])for r in rows);states=','.join(str(r['PassiveState'])for r in rows)
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#include <array>
#define __stdcall
#define __fastcall
#define TRUE 1
using BOOL=int;
#define UNIT_PLAYER 0
#define UNIT_MONSTER 1
#define PCLASS_ASSASSIN 6
#define SKILL_LEFTHANDSWING 305
#define SKILL_ATTACK 0
#define D2UnitInvalidGUID -1
#define FOG_Trace(...) ((void)0)
struct D2SkillsTxt{int nSkillId=0,nPassiveState=-1,nMonAnim=4,nAnim=0,wMaxLvl=20,nCharClass=6,nEType=0;};
struct D2SkillStrc{D2SkillsTxt*pSkillsTxt=nullptr;int nOwnerGUID=-1,nSkillLevel=0,nLevelBonus=0;unsigned dwSkillMode=0;D2SkillStrc*pNextSkill=nullptr;};
struct D2SkillListStrc{void*pMemPool=nullptr;D2SkillStrc*pFirstSkill=nullptr,*pLeftSkill=nullptr,*pRightSkill=nullptr,*pUsedSkill=nullptr;};
struct D2UnitStrc{int dwUnitType=1,dwClassId=418,dwUnitId=1;void*pMemoryPool=nullptr;D2SkillListStrc*pSkills=nullptr;};
struct D2GameStrc{};D2SkillsTxt rows[429];std::vector<D2SkillStrc*>heap;
std::vector<std::array<int,2>>trace;int all=0,os=0,single=0,element=0,changed=0,packetAttempts=0,petCalls=0;
D2SkillsTxt*DATATBLS_GetSkillsTxtRecord(int id){return id>=0&&id<429?&rows[id]:nullptr;}
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){return DATATBLS_GetSkillsTxtRecord(id);}
D2SkillStrc*SKILLS_GetSkill(D2UnitStrc*u,int id,int){for(auto*s=u->pSkills->pFirstSkill;s;s=s->pNextSkill)if(s->pSkillsTxt->nSkillId==id)return s;return nullptr;}
D2SkillStrc*SKILLS_GetSkillById(D2UnitStrc*u,int id,int owner){return SKILLS_GetSkill(u,id,owner);}
D2SkillStrc*UNITS_GetStartSkill(D2UnitStrc*u){return u->pSkills->pFirstSkill;}
void STATES_ToggleState(D2UnitStrc*,int,int){}
void SKILLS_RefreshSkill(D2UnitStrc*u,int id){auto*s=SKILLS_GetSkill(u,id,-1);trace.push_back({id,s?s->nSkillLevel:-999});}
D2SkillStrc*Allocate(){auto*p=new D2SkillStrc;heap.push_back(p);return p;}
#define D2_CALLOC_STRC_POOL(pool,type) Allocate()
// Quarantine removed entries until the case ends: the older tail reads its
// old pointer after removal. No source pool reuse or current ABI is claimed.
#define D2_FREE_POOL(pool,p) ((void)0)
void SKILLS_SetLeftActiveSkill(D2UnitStrc*u,int id,int){if(auto*s=SKILLS_GetSkill(u,id,-1))u->pSkills->pLeftSkill=s;}
void SKILLS_SetRightActiveSkill(D2UnitStrc*u,int id,int){if(auto*s=SKILLS_GetSkill(u,id,-1))u->pSkills->pRightSkill=s;}
void SKILLS_SetUsedSkillInSkillList(D2SkillListStrc*l,D2SkillStrc*s){l->pUsedSkill=s;}
'''+ '\n'.join(funcs.values())+r'''
D2SkillStrc*UNITS_GetLeftSkill(D2UnitStrc*u){return u->pSkills->pLeftSkill;}
D2SkillStrc*UNITS_GetRightSkill(D2UnitStrc*u){return u->pSkills->pRightSkill;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*s,int bonuses){if(!s)return 0;int n=s->nSkillLevel;if(bonuses){n+=s->nLevelBonus+all+std::min(os,s->nSkillLevel>0?3:os)+single;if(s->pSkillsTxt->nEType>0)n+=element;}return std::clamp(n,0,99);}
void*SUNIT_GetClientFromPlayer(D2UnitStrc*u,const char*,int){assert(u->dwUnitType==1);return nullptr;}
void D2GAME_PACKETS_SendPacket0x21_UpdateSkills_6FC3DB50(void*c,D2UnitStrc*,int,int,int){assert(!c);packetAttempts++;}
void sub_6FD108D0(D2GameStrc*,D2UnitStrc*u,int){assert(u->dwUnitType==1);petCalls++;}
void D2GAME_AssignSkill_6FD13800(D2UnitStrc*u,int left,int id,int flags){assert(id==0);if(left)SKILLS_SetLeftActiveSkill(u,id,flags);else SKILLS_SetRightActiveSkill(u,id,flags);}
void Callback(D2GameStrc*pGame,D2UnitStrc*pOwner,int nStatId,int nLayer_StatId,int nNewValue){enum{STAT_ITEM_NONCLASSSKILL=97,STAT_ITEM_SINGLESKILL=107};switch(nStatId){
'''+branch+r'''
}}
int Id(D2SkillStrc*s){return s?s->pSkillsTxt->nSkillId:-1;}
int main(){int modes[429]={'''+modes+r'''},elements[429]={'''+elements+r'''},states[429]={'''+states+r'''};
for(int i=0;i<429;i++){rows[i].nSkillId=i;rows[i].nMonAnim=modes[i];rows[i].nEType=elements[i];rows[i].nPassiveState=states[i];}
int count;assert(scanf("%d",&count)==1);printf("[");for(int i=0;i<count;i++){int a[10];for(int&v:a)assert(scanf("%d",&v)==1);
D2SkillListStrc list;D2UnitStrc unit;unit.pSkills=&list;D2GameStrc game;auto*attack=SKILLS_AddSkill(&unit,0);attack->dwSkillMode=5;
int id=a[1];if(a[2]>=0&&id!=0){SKILLS_AssignSkill(&unit,id,a[2],0,__FILE__,__LINE__);auto*s=SKILLS_GetSkill(&unit,id,-1);s->nLevelBonus=a[3];if(id==266)s->dwSkillMode=14;}
auto*selected=SKILLS_GetSkill(&unit,id,-1);list.pLeftSkill=list.pRightSkill=attack;if(selected){if(a[9]&1)list.pLeftSkill=selected;if(a[9]&2)list.pRightSkill=selected;if(a[9]&4)list.pUsedSkill=selected;}
all=a[4];single=a[5];element=a[6];os=a[7];trace.clear();packetAttempts=petCalls=0;Callback(&game,&unit,a[0],(id&65535),a[8]);
printf("%s{\"Args\":[",i?",":"");for(int j=0;j<10;j++)printf("%s%d",j?",":"",a[j]);printf("],\"Entries\":[");bool first=true;
for(auto*s=list.pFirstSkill;s;s=s->pNextSkill){printf("%s[%d,%d,%d,%u]",first?"":",",s->pSkillsTxt->nSkillId,s->nSkillLevel,s->nLevelBonus,s->dwSkillMode);first=false;}
printf("],\"Selection\":[%d,%d,%d],\"Refresh\":[",Id(list.pLeftSkill),Id(list.pRightSkill),Id(list.pUsedSkill));first=true;for(auto t:trace){printf("%s[%d,%d]",first?"":",",t[0],t[1]);first=false;}
printf("],\"PacketAttempts\":%d,\"PetCalls\":%d}",packetAttempts,petCalls);for(auto*p:heap)delete p;heap.clear();}printf("]\n");}
'''
 rng=random.Random(0x97107);cases=[]
 for i in range(4096):
  stat=97 if i%3 else 107;id=rng.choice([0,36,252,263,266,362]);value=rng.choice([-4,0,1,3,7]);cases.append([stat,id,rng.choice([-1,0,1,20]),rng.choice([-4,0,2]),rng.choice([-2,0,2]),rng.choice([-1,0,2]),rng.choice([0,3]),value,value,rng.randrange(8)])
 path=STAGE/'source.cpp';path.write_text(cpp);request=STAGE/'input.txt';request.write_text(' '.join(map(str,[len(cases),*[n for c in cases for n in c]]))+'\n')
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(path),'-o',str(STAGE/'probe')],stdout=log,stderr=log,check=True)
 with(STAGE/'sanitizers.log').open('w')as log:run=subprocess.run([str(STAGE/'probe')],input=request.read_bytes(),stdout=subprocess.PIPE,stderr=log,check=True)
 assert not(STAGE/'sanitizers.log').read_text();out=STAGE/'cases.json';out.write_bytes(run.stdout);assert len(json.loads(run.stdout))==4096
 inputs[str(metadata.relative_to(ROOT))]=sha(metadata);inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__).resolve())
 proof=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceContexts=4096,completeItemSkillBranchSha256=hashlib.sha256(branch.encode()).hexdigest(),completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in funcs.items()},playerOnlyPetHelperSha256=hashlib.sha256(npcguard.encode()).hexdigest(),currentExecutableParityVerified=False,
  declaredServices=['NPC stat/rank getter already qualified separately;real current429row modes/elements/states;intrinsic lookup/selections/passive refresh trace;NPC null client and player-only pet-helper guard;inactive aura/channel fallback. Removed source entries remain quarantined until case end to observe the old tail without allocator reuse.'],inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in [path,request,out,STAGE/'probe',STAGE/'compiler.log',STAGE/'sanitizers.log']},limits=['Older complete97/107callback branch plus complete add/assign/remove writers;current callback ABI,active aura/channel fallback and pool reuse are not certified. Foreign passives outside the two admitted Shadow rows remain explicitly unsupported;all429entries can be retained without enabling their execution.'])
 (ROOT/'tools/reference/npc-item-skill-grants-source-evidence.json').write_text(json.dumps(proof,indent=2)+'\n');print('NPC item skills:4096whole older97/107branch/add/assign/remove contexts;ASan/UBSan clean;current ABI and execution remain qualified.')
if __name__=='__main__':main()
