#!/usr/bin/env python3
"""Execute complete do26/hit12 bodies with an explicit original NPC owner."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-chain-routes-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 bodies={};sources={}
 for relative,name in [('source/D2Game/src/SKILLS/SkillSor.cpp','SKILLS_SrvDo026_ChainLightning'),('source/D2Game/src/MISSILES/MissMode.cpp','MISSMODE_SrvHit12_ChainLightning_LightningStrike')]:
  p=BASE/'d2moo-pinned-source'/relative;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==relative);sources[relative]=sha(p);bodies[name]=function(raw.decode(),name)
 cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr uint32_t UNITFLAG_SKSRVDOFUNC=8;
struct D2GameStrc{}game;
struct D2UnitStrc{uint32_t dwFlags=64;int dwUnitId=231,dwClassId=357,x=380,y=680,skill=53,rank=1,budget=0;D2UnitStrc*owner=nullptr;}actor,missile,primary,other,child;
struct D2SkillsTxt{int wSrvMissileA=93,dwCalc[1]={1},dwAuraRangeCalc=2;}skill;
struct D2MissilesTxt{int dwHitPar[1]={0};}row;
struct Tables{int nMissilesTxtRecordCount=742;}tables;auto*sgptDataTables=&tables;
struct D2MissileStrc{uint32_t dwFlags=0;int nMissile=0;D2UnitStrc*pOwner=nullptr,*pTarget=nullptr;int nX=0,nY=0,nTargetX=0,nTargetY=0,nSkill=0,nSkillLevel=0;}params;
int guard=0,phase=0,evals=0,creates=0,searches=0,ownerSeen=0,rankSeen=0,skillSeen=0,originX=0,originY=0,rangeSeen=0,skipSeen=0;uint32_t filterSeen=0;
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){assert(id==53);return(phase==0&&guard==1)||(phase==1&&guard==8)?nullptr:&skill;}
D2MissilesTxt*SKILLS_GetMissilesTxtRecord(int id){assert(id==93);return guard==2?nullptr:&row;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*u,int formula,int id,int rank){assert(u==&actor&&id==53);evals++;rankSeen=rank;skillSeen=id;return formula==1?(rank+25)/5:20;}
D2UnitStrc*sub_6FD11420(D2GameStrc*,int mid,D2UnitStrc*u,int id,int rank,int a,int b,int c,int d,int e){assert(mid==93&&u==&actor&&id==53&&!(a|b|c|d|e));creates++;ownerSeen=u->dwUnitId;rankSeen=rank;skillSeen=id;return guard==4?nullptr:&child;}
void MISSILE_SetTargetX(D2UnitStrc*u,int n){u->budget=n;}
int MISSILE_GetTargetX(D2UnitStrc*u){return u->budget;}
D2UnitStrc*SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*u){return guard==3?nullptr:u->owner;}
int CLIENTS_GetUnitX(D2UnitStrc*u){return u->x;}int CLIENTS_GetUnitY(D2UnitStrc*u){return u->y;}
int MISSILE_GetSkill(D2UnitStrc*u){return u->skill;}int MISSILE_GetLevel(D2UnitStrc*u){return u->rank;}
D2UnitStrc*sub_6FD107F0(D2GameStrc*,D2UnitStrc*u,int x,int y,int radius,uint32_t filter,int skip,int flag){assert(u==&actor&&flag==0);searches++;ownerSeen=u->dwUnitId;originX=x;originY=y;rangeSeen=radius;filterSeen=filter;skipSeen=skip;return guard==5?nullptr:guard==6?&primary:&other;}
D2UnitStrc*MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc*p){params=*p;creates++;return guard==7?nullptr:&child;}
void reset(){actor={};missile={};primary={};other={};child={};params={};skill={};row={};evals=creates=searches=ownerSeen=rankSeen=skillSeen=originX=originY=rangeSeen=skipSeen=filterSeen=0;primary.dwUnitId=500;other.dwUnitId=510;other.x=410;other.y=703;missile.dwClassId=93;missile.owner=&actor;missile.x=400;missile.y=700;}
'''+bodies['SKILLS_SrvDo026_ChainLightning']+'\n'+bodies['MISSMODE_SrvHit12_ChainLightning_LightningStrike']+r'''
int main(){bool first=true;printf("[");phase=0;
for(int rank=1;rank<=63;rank++)for(int g=0;g<5;g++){
 reset();guard=g;skill.wSrvMissileA=g==2?-1:g==3?742:93;
 int result=SKILLS_SrvDo026_ChainLightning(&game,&actor,53,rank);
 printf("%s[0,[%d,%d],[%d,%u,%d,%d,%d,%d,%d,%d]]",first?"":",",rank,g,result,actor.dwFlags,evals,creates,child.budget,ownerSeen,skillSeen,rankSeen);first=false;}
phase=1;
for(int rank=1;rank<=63;rank++)for(int budget:{0,1,2,5,17})for(int g=0;g<9;g++)for(int fixed:{0,7})for(int moved:{0,1}){
 reset();guard=g;missile.rank=rank;missile.budget=budget;row.dwHitPar[0]=fixed;actor.x=moved?420:380;
 int result=MISSMODE_SrvHit12_ChainLightning_LightningStrike(&game,g==1?nullptr:&missile,g==4?nullptr:&primary);
 printf(",[1,[%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%u,%d,%d,%d,%d,%d,%d,%d,%d,%d]]",rank,budget,g,fixed,moved,
 result,evals,searches,creates,child.budget,ownerSeen,originX,originY,filterSeen,skipSeen,params.dwFlags,params.nMissile,params.pOwner?params.pOwner->dwUnitId:0,params.pTarget?params.pTarget->dwUnitId:0,params.nX,params.nY,params.nTargetX,params.nTargetY,params.nSkill,params.nSkillLevel);}
printf("]\n");}
'''
 for name,body in bodies.items():assert function(cpp,name)==body
 generated=STAGE/'probe.cpp';generated.write_text(cpp);binary=STAGE/'probe';r=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],capture_output=True);compiler=STAGE/'compiler.log';compiler.write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stderr.decode()
 r=subprocess.run([str(binary)],capture_output=True);san=STAGE/'sanitizer.log';san.write_bytes(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr.decode();rows=json.loads(r.stdout);assert len(rows)==11655
 for phase,args,out in rows:
  if phase==0:
   rank,g=args;valid=g not in(1,2,3);allocated=valid and g!=4
   want=[int(allocated),72 if valid else 64,int(valid),int(valid),(rank+25)//5 if allocated else 0,231 if valid else 0,53 if valid else 0,rank if valid else 0]
  else:
   rank,budget,g,fixed,moved=args;eligible=g not in(1,2,3,4)and budget>1;has_record=not(g==8 and fixed==0);searched=eligible and has_record;created=searched and g not in(5,6);allocated=created and g!=7
   want=[1 if g in(1,2)else 3,int(eligible and fixed==0 and has_record),int(searched),int(created),budget-1 if allocated else 0,231 if searched else 0,400 if searched else 0,700 if searched else 0,0x88583 if searched else 0,500 if searched else 0,
         33 if created else 0,93 if created else 0,231 if created else 0,510 if created else 0,400 if created else 0,700 if created else 0,410 if created else 0,703 if created else 0,53 if created else 0,rank if created else 0]
  assert out==want,(phase,args,out,want)
 refs=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((refs/'sources.json').read_text());pins={}
 for name in ['skills','missiles']:
  p=refs/f'json/{name}.json';assert sha(p)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(p.relative_to(ROOT))]=sha(p)
 skillrow=json.loads((refs/'json/skills.json').read_text())['53'];missile=json.loads((refs/'json/missiles.json').read_text())['chainlightning']
 assert skillrow['srvdofunc']==26 and skillrow['srvmissilea']=='chainlightning'and skillrow['calc1']=='ln34/par5'and skillrow['Param3']==26 and skillrow['Param4']==1 and skillrow['Param5']==5 and skillrow['Param1']==20
 assert missile['*ID']==93 and missile['pSrvHitFunc']==12 and not missile.get('sHitPar1')
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,cases=len(rows),rootCases=315,childCases=11340,sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},currentTable=pins,
  inputs={str(p.relative_to(ROOT)):sha(p)for p in [Path(__file__).resolve(),refs/'sources.json']},artifacts={str(p.relative_to(ROOT)):sha(p)for p in [generated,compiler,san,fixture]},
  established=['Complete unchanged do26/hit12 compose with an actual NPC owner at all nonzero six-bit proc ranks. Root count is(rank+25)/5;failed metadata refuses before server-do/formula/factory,while allocation failure retains its actual earlier flag/formula call.','Hit12 uses the missile owner and retained skill/rank,centers its filter0x88583 search on the missile rather than a moved caster,and excludes the struck GUID. A different selected target receives a fresh same-class child with flags0x21,explicit hit coordinates/target coordinates/owner/rank and budget-1. No target,same target,exhausted budget and allocation failure create no successful child.'],
  services=['Skill/count/radius formula,table/stat/world/target-search and allocation leaves are explicit. This executes two complete older bodies with pinned current data,not complete modern target traversal,damage/hit-delay/HP or the shared missile factory.','Child seed/RNG services are intentionally absent from these two bodies and belong to the declared allocator. A future adapter must provide allocation at each actual hop;precomputing/spending all future world draws at cast time would require separate evidence or approval.'],limits=['No runtime NPC Chain Lightning,proc callback dispatch,skill-list/owner-removal/seed-allocation or current executable parity is claimed. Existing source factory/missile numeric/step services remain separate.'])
 target=ROOT/'tools/reference/npc-chain-routes-source-evidence.json';encoded=json.dumps(out,indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 print('NPC Chain routes:11655complete do26/hit12 contexts,retained NPC owner/rank/position/budget and failure boundaries;ASan+UBSan clean;allocator/RNG/HP separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');probe(p.parse_args().check)
