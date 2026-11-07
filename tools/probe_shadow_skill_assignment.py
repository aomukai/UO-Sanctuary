#!/usr/bin/env python3
"""Execute the older whole Master initializer and Warrior rank-assignment slice."""
import hashlib,json,subprocess
from pathlib import Path
from probe_shadow_initializer import routine
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-skills-source'
PIN='0c0bbd1e6bef9181a3da1c046523af622a925416'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);source=BASE/'d2moo-pinned-source/source/D2Game/src/AI/AiThink.cpp';raw=source.read_bytes();tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==PIN
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']=='source/D2Game/src/AI/AiThink.cpp')
 text=raw.decode();master=routine(text,'D2GAME_AI_Unk106_6FCEAFE0');warrior=routine(text,'AITHINK_Fn105_ShadowWarrior')
 start=warrior.index('\t\t\tconst int32_t nRightOwnerSkillLevel =');end=warrior.index('\n\n\t\t\tD2SkillStrc* pRightSkill',start);assignment=warrior[start:end]
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdio>
#include <vector>
#include <array>
#include <cstdint>
#define __fastcall
#define UNIT_PLAYER 0
#define SHADOWMASTER_AI_PARAM_SUMMONING_SKILL 7
template<class T>T D2Clamp(T x,T low,T high){return std::clamp(x,low,high);}
struct D2SkillStrc{int id=0,hard=0,effective=0;};
struct D2UnitStrc{int dwUnitType=0,dwClassId=6;D2SkillStrc skills[281];}owner,pet;
struct D2GameStrc{}game;
struct Control{int dwAiParam[3]={};}control;
struct Mon{int wAiParam[8][3]={};}mon;
struct D2AiTickParamStrc{Control*pAiControl=&control;Mon*pMonstatsTxt=&mon;}tick;
std::vector<std::array<int,2>> requests;
D2UnitStrc*AIGENERAL_GetMinionOwner(D2UnitStrc*){return &owner;}
D2SkillStrc*SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*u,int id){return &u->skills[id];}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*s,int bonus){return bonus?s->effective:s->hard;}
D2SkillStrc*SKILLS_GetSkillById(D2UnitStrc*u,int id,int){return id==0||u==&owner?&u->skills[id]:nullptr;}
void SKILLS_AssignSkill(D2UnitStrc*u,int id,int rank,int,const char*,int){assert(u==&pet);requests.push_back({id,rank});}
void SKILLS_SetLeftActiveSkill(D2UnitStrc*,int id,int){assert(id==0);}
void SKILLS_SetRightActiveSkill(D2UnitStrc*,int id,int){assert(id==0);}
int SKILLS_GetPlayerSkillCount(int cls){assert(cls==6);return 30;}
int SKILLS_GetClassSkillId(int cls,int i){assert(cls==6);return 251+i;}
int AITHINK_ShadowMasterCheckTargetPetType(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int id){return id!=268&&id!=279;}
'''+master+r'''
void Warrior(int rank,int l,int r,int le,int re){D2UnitStrc*pUnit=&pet,*pOwner=&owner;D2AiTickParamStrc*pAiTickParam=&tick;
control.dwAiParam[2]=rank;int nLeftOwnerSkillId=l,nRightOwnerSkillId=r;
D2SkillStrc*pLeftOwnerSkill=&owner.skills[l],*pRightOwnerSkill=&owner.skills[r];pLeftOwnerSkill->effective=le;pRightOwnerSkill->effective=re;
'''+assignment+r'''
}
void Print(){printf("[");bool first=true;for(auto r:requests){printf("%s[%d,%d]",first?"":",",r[0],r[1]);first=false;}printf("]");}
int main(){mon.wAiParam[7][0]=279;printf("{\"Master\":[");bool first=true;
for(int rank=1;rank<=255;rank++){for(int id=0;id<=280;id++){owner.skills[id]={id,(id+rank)%21,0};}owner.skills[279].effective=rank;requests.clear();
D2GAME_AI_Unk106_6FCEAFE0(&game,&pet,&tick);assert(requests.size()==28);printf("%s[%d,",first?"":",",rank);Print();printf("]");first=false;}
printf("],\"Warrior\":[");first=true;
for(int rank=1;rank<=255;rank++)for(int level:{0,1,2,19,20,99}){requests.clear();Warrior(rank,251,266,level,99-level);printf("%s[%d,%d,%d,",first?"":",",rank,level,99-level);Print();printf("]");first=false;}
printf("]}\n");}
'''
 code=STAGE/'source.cpp';code.write_text(cpp)
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(code),'-o',str(STAGE/'probe')],stdout=log,stderr=log,check=True)
 with(STAGE/'sanitizers.log').open('w')as log:run=subprocess.run([str(STAGE/'probe')],stdout=subprocess.PIPE,stderr=log,check=True)
 assert not(STAGE/'sanitizers.log').read_text();cases=json.loads(run.stdout);assert len(cases['Master'])==255 and len(cases['Warrior'])==1530
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 inputs=[Path(__file__).resolve(),source,ROOT/'tools/reference/shadow-skill-metadata-data-evidence.json',ROOT/'tools/reference/npc-innate-skill-rank-source-evidence.json']
 result=dict(referenceVersion='3.3.93847',legacyCommit=PIN,masterWholeInitializerContexts=255,warriorAssignmentSliceContexts=1530,
  sourceRoutineHashes={'D2GAME_AI_Unk106_6FCEAFE0':hashlib.sha256(master.encode()).hexdigest(),'AITHINK_Fn105_ShadowWarrior.assignmentSlice':hashlib.sha256(assignment.encode()).hexdigest()},
  inputs={str(p.relative_to(ROOT)):sha(p)for p in inputs},artifacts={str(p.relative_to(ROOT)):sha(p)for p in [code,out,STAGE/'probe',STAGE/'compiler.log',STAGE/'sanitizers.log']},
  declaredServices=['Owner/player lookup and highest summoning skill;intrinsic owner hard/effective ranks;existing Attack entry;class-list order251..280;target-pet eligibility excludes both Shadow pettype16 skills;assignment records calls without passive refresh.'],
  limits=['Older initializer and a clearly identified AI105 assignment slice,not current executable AI parity. The full Warrior/Master think loop,target permissions,passive stat refresh,item-grant callbacks,animation and skill execution are not certified.','Global approved snapshot policy captures owner ranks at summon;live selection IDs can later change control inputs. Current class-list order and current assignment/getter callback parity remain unverified. No learned Shadow cast or active-world controller is enabled.'])
 (ROOT/'tools/reference/shadow-skill-assignment-source-evidence.json').write_text(json.dumps(result,indent=2)+'\n');print('Shadow assignment:255whole older Master initializers/1530Warrior assignment slices;ASan/UBSan clean;current AI parity unverified.')
if __name__=='__main__':main()
