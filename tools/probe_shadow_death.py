#!/usr/bin/env python3
"""Compile unchanged death scheduling/rank/ring helpers with declared services."""
import hashlib,itertools,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-death-source';PIN=BASE/'d2moo-pinned-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());inputs={};bodies={}
 for rel,names in {'source/D2Game/src/MONSTER/MonsterUnique.cpp':['MONSTERUNIQUE_ColdEnchantedModeChange','MONSTERUNIQUE_CastNova'],'source/D2Game/src/SKILLS/Skills.cpp':['sub_6FD14170']}.items():
  p=PIN/rel;b=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p)
  for name in names:bodies[name]=function(b.decode(),name)
 headers=[]
 for rel in ['source/D2Common/include/DataTbls/MissilesIds.h','source/D2Common/include/DataTbls/SkillsIds.h']:
  p=PIN/rel;b=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);headers.append(b.decode().replace('#pragma once',''))
 cpp=r'''
#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
'''+ '\n'.join(headers)+r'''
constexpr int MONMODE_DEATH=0,EVENTTYPE_MONUMOD=7;
struct D2SkillStrc{int rank;};
struct D2UnitStrc{int dwAnimMode=0,x=17,y=23,life=0;D2UnitStrc*owner=nullptr;D2SkillStrc skill{0};bool hasSkill=true;};
struct D2GameStrc{int dwGameFrame=1000;};
struct D2MissileStrc{int dwFlags=0,nX=0,nY=0,nSkill=0,nMissile=0,nSkillLevel=0,nVelocity=0,nTargetX=0,nTargetY=0;D2UnitStrc*pOwner=nullptr;};
std::vector<D2MissileStrc> missiles;std::vector<int> events;
int CLIENTS_GetUnitX(D2UnitStrc*u){return u->x;}int CLIENTS_GetUnitY(D2UnitStrc*u){return u->y;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int t,int frame,int a,int b){if(t!=7||a||b)__builtin_trap();events.push_back(frame);}
D2UnitStrc*AIGENERAL_GetMinionOwner(D2UnitStrc*u){return u->owner;}
D2SkillStrc*SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*u,int id){if(id!=SKILL_SHADOWWARRIOR)__builtin_trap();return u->hasSkill?&u->skill:nullptr;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*s,int bonus){if(bonus!=1)__builtin_trap();return s->rank;}
int UNITS_GetCurrentLifePercentage(D2UnitStrc*u){return u->life;}
int DATATBLS_GetMissileVelocityFromMissilesTxt(int id,int level){if(id!=MISSILE_NOVA||level)__builtin_trap();return 24;}
void MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc*p){missiles.push_back(*p);}
'''+bodies['sub_6FD14170']+'\n'+bodies['MONSTERUNIQUE_ColdEnchantedModeChange']+'\n'+bodies['MONSTERUNIQUE_CastNova']+r'''
int main(){int count;scanf("%d",&count);printf("[");for(int c=0;c<count;c++){int unique,owner,skill,rank,life,mode;scanf("%d%d%d%d%d%d",&unique,&owner,&skill,&rank,&life,&mode);D2GameStrc game;D2UnitStrc root,actor;root.skill.rank=rank;root.hasSkill=skill;actor.owner=owner?&root:nullptr;actor.life=life;actor.dwAnimMode=mode;events.clear();missiles.clear();MONSTERUNIQUE_ColdEnchantedModeChange(&game,&actor,42,unique);MONSTERUNIQUE_CastNova(&game,&actor,42,unique);printf("%s{\"Delay\":%d,\"Count\":%zu,\"Rank\":%d,\"Directions\":[",c?",":"",events.empty()?0:events[0]-game.dwGameFrame,missiles.size(),missiles.empty()?0:missiles[0].nSkillLevel);for(size_t i=0;i<missiles.size();i++){auto m=missiles[i];if(m.pOwner!=&actor||m.nX!=17||m.nY!=23||m.dwFlags!=7||m.nSkill!=0||m.nMissile!=MISSILE_NOVA||m.nVelocity!=24)__builtin_trap();printf("%s[%d,%d]",i?",":"",m.nTargetX,m.nTargetY);}printf("]}");}printf("]\n");}
'''
 cases=[dict(Unique=u,Owner=o,Skill=s,Rank=r,Life=life,Mode=m)for u,o,s,r,life,m in itertools.product([0,1],[0,1],[0,1],[0,1,2,20,28,40,255],[0,10,11],[0,1])]
 p=STAGE/'source.cpp';p.write_text(cpp);binary=STAGE/'probe';r=subprocess.run(['g++','-std=c++20','-O1','-fsanitize=address,undefined','-fno-omit-frame-pointer','-no-pie',str(p),'-o',str(binary)],capture_output=True,text=True);(STAGE/'compiler.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stderr[-2000:]
 request=' '.join(map(str,[len(cases)]+[v for c in cases for v in c.values()]))+'\n';r=subprocess.run([str(binary)],input=request,capture_output=True,text=True);(STAGE/'sanitizer.log').write_text(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr
 for c,expected in zip(cases,json.loads(r.stdout),strict=True):
  assert expected['Delay']==(4 if c['Mode']==0 else 0)
  assert expected['Count']==(64 if c['Unique']else 0)
  assert expected['Rank']==(0 if not c['Unique']else 1 if c['Life']>10 or not(c['Owner']and c['Skill'])else min(c['Rank']//2+1,15))
  c['Expected']=expected
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 inputs[str(Path(__file__).relative_to(ROOT))]=sha(Path(__file__));e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],contexts=len(cases),completeRoutineHashes={n:hashlib.sha256(s.encode()).hexdigest()for n,s in bodies.items()},inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in [p,fixture]},scope='Unchanged complete mode-change,CastNova and64-ray helper over declared owner/effective-skill/life/event/missile allocation services. Verifies4-frame death schedule,unique guard,Warrior rank even for Master,cap15,life>10 override,actual owner/origin,skill0/flags7/velocity24 and ring directions. Does not compile the whole damage creator or prove modern executable behavior;existing typed NPC Nova damage/movement proofs are reused. Snapshot rank timing is the separately approved Sanctuary summon-birth adaptation.')
 (ROOT/'tools/reference/shadow-death-source-evidence.json').write_text(json.dumps(e,indent=2)+'\n');print(f'{len(cases)} complete Shadow death/rank/ring source contexts;ASan/UBSan clean.')
if __name__=='__main__':main()
