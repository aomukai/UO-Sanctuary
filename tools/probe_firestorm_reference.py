#!/usr/bin/env python3
"""Execute complete older Firestorm launch/initializer/maker bodies.

Current positive count contradicts do117's reconstructed guard; record it rather
than editing the source. The shared launcher and maker are independently usable.
"""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 hashes={};routines={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
  hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(path,name):
  s=source(path);m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name
  end=s.index('{',m.start())+1;depth=1
  while depth:depth+=(s[end]=='{')-(s[end]=='}');end+=1
  block=s[m.start():end];routines[name]=hashlib.sha256(block.encode()).hexdigest();return block+'\n'
 manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text());pins={};tables={}
 for name in ['skills','missiles']:
  raw=(ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json').read_bytes();pins[name]=hashlib.sha256(raw).hexdigest();assert pins[name]==manifest['files'][f'json/{name}.json']['sha256'];tables[name]=json.loads(raw)
 skill=next(v for v in tables['skills'].values() if v.get('*Id')==225);maker=tables['missiles']['firestormmaker'];fire=tables['missiles']['firestorm']
 assert skill['calc1']=='ln12' and skill['Param1']==3 and skill['Param2']==0 and skill['srvdofunc']==117
 assert maker['pSrvDoFunc']==23 and maker['SubMissile1']=='firestorm' and maker['Param1']==0 and fire['pSrvDoFunc']==1
 cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
#define __stdcall
using BOOL=int;constexpr int FALSE=0,PATH_UNKNOWN_FLAG_0x00008=8;
constexpr int UNIT_MONSTER=1,STAT_TOHIT=1,UNITFLAG_SKSRVDOFUNC=1,PATHTYPE_CHARGEDBOLT=9;
struct D2GameStrc{}game;
struct D2DynamicPathStrc{int dwFlags=0,x=204,y=198,type=0,distance=0;};
struct D2SeedStrc{uint32_t low=0;};
struct D2UnitStrc{int dwFlags=0,dwUnitType=0,dwClassId=458,x=200,y=200,frames=40,skill=225,rank=1;D2DynamicPathStrc* pDynamicPath;D2SeedStrc pSeed;};
D2DynamicPathStrc path;D2UnitStrc actor{0,0,458,200,200,40,225,1,&path,{}},target{0,0,0,208,196,0,0,0,nullptr,{}};
struct D2SkillsTxt{int dwCalc[1]={};}skill;
struct D2MissilesTxt{int wSubMissile[1]={457},dwParam[1]={0};}missile;
struct Tables{int nMissilesTxtRecordCount=800;}tables;Tables* sgptDataTables=&tables;
struct D2MissileStrc{int dwFlags=0;D2UnitStrc* pOwner=nullptr;int nX=0,nY=0,nSkill=0,nSkillLevel=0,nMissile=0,nTargetX=0,nTargetY=0,nAttBonus=0,nLoops=0;void(*pInitFunc)(D2UnitStrc*,int)=nullptr;int pInitArgs=0;};
std::vector<D2MissileStrc> calls;bool row=true,missileRow=true,fanTarget=true,unitTarget=false;int count=3,missileId=458,collision=1,collisionCalls=0,pathCalls=0,currentFrames=0;
int CLIENTS_GetUnitX(D2UnitStrc* p){assert(p);return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){assert(p);return p->y;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){assert(p);return p->x;}int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc* p){assert(p);return p->y;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return unitTarget?&target:nullptr;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return 0;}int sub_6FD118C0(D2GameStrc*,D2UnitStrc*){return 1;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){calls.push_back(*p);return &actor;}
int MISSILE_GetTotalFrames(D2UnitStrc* p){return p->frames;}void MISSILE_SetTotalFrames(D2UnitStrc* p,int n){p->frames=n;}void MISSILE_SetCurrentFrame(D2UnitStrc*,int n){currentFrames=n;}
void SEED_InitLowSeed(D2SeedStrc* p,int n){p->low=uint32_t(n);}void PATH_SetType(D2DynamicPathStrc* p,int n){p->type=n;}void PATH_SetNewDistance(D2DynamicPathStrc* p,uint8_t n){p->distance=n;}
void D2Common_10142(D2DynamicPathStrc*,D2UnitStrc*,int n){assert(n==0);pathCalls++;}
int D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){*x=path.x;*y=path.y;return fanTarget;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int n){assert(n==225);return row?&skill:nullptr;}int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int){return missileId;}int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return count;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int n){assert(n==458);return missileRow?&missile:nullptr;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return &actor;}int MISSILE_GetSkill(D2UnitStrc* p){return p->skill;}int MISSILE_GetLevel(D2UnitStrc* p){return p->rank;}
int MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc*){collisionCalls++;return collision;}
'''
 cpp+=routine('source/D2Game/src/SKILLS/SkillSor.cpp','SKILLS_MissileInit_ChargedBolt')
 cpp+=routine('source/D2Game/src/SKILLS/Skills.cpp','sub_6FD11420')
 cpp+=routine('source/D2Game/src/SKILLS/SkillDruid.cpp','sub_6FCFE4C0')
 cpp+=routine('source/D2Game/src/SKILLS/SkillDruid.cpp','SKILLS_SrvDo117_Firestorm')
 cpp+=routine('source/D2Common/src/Path/Step.cpp','D2Common_10234')
 cpp+=routine('source/D2Game/src/MISSILES/MissMode.cpp','MISSMODE_SrvDo23_24_SuccFireBall_FirestormMaker')
 # Call-order inspection is separate from the compiled request fixtures.
 collision_body=routine('source/D2Game/src/MISSILES/MissMode.cpp','MISSMODE_HandleMissileCollision')
 movement_bridge=routine('source/D2Game/src/UNIT/SUnit.cpp','sub_6FCBC930')
 movement_body=routine('source/D2Common/src/Path/PathMisc.cpp','D2Common_10226')
 assert collision_body.index('sub_6FCBC930')<collision_body.index('MISSILE_GetCurrentFrame')
 assert 'D2Common_10226(pMissile, 1024)' in movement_bridge
 assert movement_body.index('dwFlags &= ~PATH_UNKNOWN_FLAG_0x00008')<movement_body.index('sub_6FDACEC0')
 cpp+=r'''
void printCalls(){printf("[");for(size_t i=0;i<calls.size();i++){auto p=calls[i];printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",p.dwFlags,p.nMissile,p.nX,p.nY,p.nTargetX,p.nTargetY,p.pInitFunc?1:0,p.pInitArgs,p.nLoops,p.nSkillLevel);}printf("]");}
int main(){bool first=true;printf("{\"launch\":[");
for(int n:{-2,-1,0,1,2,3,4,8})for(int straight:{0,1})for(int tx:{0,204})for(int ty:{0,198})for(bool available:{false,true})for(bool hasTarget:{false,true}){
 calls.clear();path.x=tx;path.y=ty;fanTarget=available;unitTarget=hasTarget;int result=sub_6FCFE4C0(&game,&actor,n,458,225,7,straight);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,",first?"":",",n,straight,tx,ty,available,hasTarget,result);first=false;printCalls();printf("]");}
printf("],\"guard\":[");first=true;unitTarget=false;fanTarget=true;path.x=204;path.y=198;
for(int n:{-2,-1,0,1,3})for(int guard:{0,1,2,3}){calls.clear();row=guard!=1;missileId=guard==2?-1:guard==3?800:458;count=n;actor.dwFlags=0;
 int result=SKILLS_SrvDo117_Firestorm(&game,&actor,225,7);printf("%s[%d,%d,%d,%d,",first?"":",",n,guard,result,actor.dwFlags);first=false;printCalls();printf("]");}
row=true;printf("],\"initializer\":[");first=true;
for(int frames:{1,40,77,78,255})for(int aimX:{0,1,65530})for(int ordinal:{0,1,2}){
 actor.frames=frames;path.x=aimX;path.type=0;path.distance=0;pathCalls=0;currentFrames=0;SKILLS_MissileInit_ChargedBolt(&actor,ordinal);
 printf("%s[%d,%d,%d,%d,%d,%u,%d,%d]",first?"":",",frames,aimX,ordinal,actor.frames,currentFrames,actor.pSeed.low,path.distance,pathCalls);first=false;assert(path.type==PATHTYPE_CHARGEDBOLT);}
printf("],\"maker\":[");first=true;
for(int guard:{0,1,2,3})for(int flags:{0,1,8,9,16})for(int loops:{-1,0,1,4})for(int endResult:{1,2}){
 calls.clear();collisionCalls=0;missileRow=guard!=2;path.dwFlags=flags;missile.dwParam[0]=loops;missile.wSubMissile[0]=guard==1?0:457;collision=endResult;
 int result=MISSMODE_SrvDo23_24_SuccFireBall_FirestormMaker(&game,guard==3?nullptr:&actor);
 printf("%s[%d,%d,%d,%d,%d,%d,",first?"":",",guard,flags,loops,endResult,result,collisionCalls);first=false;printCalls();printf("]");}
printf("]}\n");}
'''
 STAGE.mkdir(parents=True,exist_ok=True);cpppath=STAGE/'firestorm-source-probe.cpp';cpppath.write_text(cpp);binary=cpppath.with_suffix('')
 subprocess.run(['g++','-std=c++17','-O1','-g','-Wall','-Wextra','-Werror','-Wno-unused-parameter','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(cpppath),'-o',str(binary)],check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'firestorm-source-sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 rows=json.loads(run.stdout);assert {k:len(v) for k,v in rows.items()}==dict(launch=256,guard=20,initializer=45,maker=160)
 # The guard defect is observed, not repaired inside the extracted body.
 assert next(x for x in rows['guard'] if x[:2]==[3,0])==[3,0,0,1,[]]
 for n,straight,tx,ty,available,hasTarget,result,calls in rows['launch']:
  assert result==1
  expected=int(bool(straight and (hasTarget or tx and ty)))+(max(0,n-int(bool(straight))) if available else 0)
  assert len(calls)==expected
 for frames,aim,ordinal,total,current,seed,distance,rebuilds in rows['initializer']:
  assert total==distance==min(frames,77) and seed==aim+ordinal and rebuilds==1 and current==(77 if frames>=78 else 0)
 for guard,flags,loops,end,result,collisionCalls,calls in rows['maker']:
  assert result==(2 if guard else end) and collisionCalls==int(not guard)
  assert len(calls)==int(not guard and flags&8!=0)
  if calls:assert calls[0][:4]==[9 if loops>0 else 1,457,200,200] and calls[0][8]==(loops if loops>0 else 0)
 fixture=STAGE/'firestorm-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,sourceHashes=hashes,routineHashes=routines,pinnedInputHashes=pins,caseCounts={k:len(v) for k,v in rows.items()},fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),sanitizers='ASan/UBSan exit0/empty stderr',established=['Complete shared launcher and direct factory produce one ordinary emitter plus count-minus-one independently initialized Charged Bolt paths for Firestorm; current count is3. Target-unit versus ground coordinates and unavailable fan-target services are separate controls.','Charged initializer uses aimX+ordinal, caps frames at77 and rebuilds the charged path. Current Firestorm maker40frames does not need the cap. Existing verified ChargedBolts path kernel is reusable.','Complete maker do23 emits firestorm457 when path flag8 is set, then calls ordinary collision/lifetime processing. It still emits before a terminal collision return. Current Param1=0 selects flags1/no loop override; child Range15 is independent of maker Range40.','Inspected Step.cpp updates flag8 according to changed integer source cell, not every callback or every saved crossing; a subcell move must not create another patch.'],conflicts=['The complete reconstructed do117 rejects nParam>=0, including current table count3, while its shared helper correctly produces three emitters for count3. The executable modern do117 guard is not established. Do not use this older guard to disable positive current table counts or silently patch the extracted body.'],limits=['Declared engine services record creation requests/target queries and collision return values; no actual world actor, source complete factory, collision footprint, damage callback or modern executable is executed here.','Ordinary emitter path, per-emitter frame stepping, new fire damage capture, unit-search order, SoftHit, saved patch lifetime, payment/cooldown/input and native persistence require independent integration.','Final effects/audio and talent/left/right/quick icons remain in separate registers.','Separate call-order inspection of complete HandleMissileCollision, sub_6FCBC930 and D2Common_10226 establishes that do23 observes the previous generic movement flag/cell, creates any child, then moves again and decrements. The movement bridge/body are inspected, not compiled by this request probe.'],next='Compose source-backed launch and changed-cell child plans using existing path/field consumers; retain the do117 guard conflict explicitly and establish modern behavior before claiming complete parity.')
 out=ROOT/'tools/reference/firestorm-source-evidence.json';text=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==text
 else:out.write_text(text)
 print('481 complete Firestorm launcher/guard/initializer/maker contexts pass; source guard conflict retained; no handler enabled.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
