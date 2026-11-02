#!/usr/bin/env python3
"""Execute complete older FoH startup and capped child-factory callback."""
import hashlib,json,subprocess
from pathlib import Path
from probe_blessed_hammer_damage import routine
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/heaven-fist-contact/start-source'
def main():
 paths={'.local/class-skills/SkillPal.cpp.reference':'source/D2Game/src/SKILLS/SkillPal.cpp','.local/class-skills/MissMode.cpp.reference':'source/D2Game/src/MISSILES/MissMode.cpp'}
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';entries={r['path']:r['sha'] for r in tree['tree']}
 texts={};hashes={}
 for local,upstream in paths.items():
  data=(ROOT/local).read_bytes();assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==entries[upstream];texts[upstream]=data.decode();hashes[local]=hashlib.sha256(data).hexdigest()
 names=['SKILLS_SrvDo080_FistOfTheHeavens','MISSMODE_FistOfTheHeavensDelay_AuraCallback']
 bodies=[routine(texts[upstream],name) for upstream,name in zip(paths.values(),names)]
 cpp=r'''#include <cstdint>
#include <cstdio>
#define __fastcall
struct D2GameStrc{};
struct D2UnitStrc{int dwUnitType=1,dwUnitId=777,x=101,y=202;};
struct D2SkillsTxt{int wSrvOverlay=0;}row;
struct Tables{int nOverlayTxtRecordCount=3;}tables;Tables* sgptDataTables=&tables;
struct D2MissileStrc{int dwFlags=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;int nX=0,nY=0,nSkillLevel=0,nSkill=0,nMissile=0,nTargetX=0,nTargetY=0;};
struct D2HitFunc22ParamStrc{D2UnitStrc* pOwner;D2UnitStrc* pOrigin;int nSkillId,nLevel,nParam,nHitSubMissile;};
struct D2AuraCallbackStrc{void* pArgs;int nCounter;D2GameStrc* pGame;};
D2UnitStrc target,missile,owner;bool hasSkill=true,hasTarget=true,created=true;int progressive=7,calls=0,overlays=0,storedType=-1,storedId=-1;D2MissileStrc request;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasSkill?&row:nullptr;}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int){return progressive;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return hasTarget?&target:nullptr;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){calls++;request=*p;return created?&missile:nullptr;}
void MISSILE_SetTargetX(D2UnitStrc*,int v){storedType=v;}void MISSILE_SetTargetY(D2UnitStrc*,int v){storedId=v;}
void UNITS_SetOverlay(D2UnitStrc*,int,int){overlays++;}
void reset(){hasSkill=hasTarget=created=true;progressive=7;calls=overlays=0;storedType=storedId=-1;request={};}
'''+''.join(bodies)+r'''
int main(){D2GameStrc game;bool first=true;printf("{\"start\":[");
for(int guard=0;guard<5;guard++)for(int overlay:{-1,0,2,3}){
 reset();row.wSrvOverlay=overlay;if(guard==1)hasSkill=false;if(guard==2)progressive=0;if(guard==3)hasTarget=false;if(guard==4)created=false;
 int result=SKILLS_SrvDo080_FistOfTheHeavens(&game,&owner,121,7);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",guard,overlay,result,calls,request.dwFlags,request.nX,request.nY,request.nSkill,request.nSkillLevel,storedType,storedId,overlays,request.pOwner==&owner);first=false;
}
printf("],\"callback\":[");first=true;
for(int cap:{-1,0,1,3,6,25})for(int counter:{0,1,2,3,6,25})for(int failure=0;failure<2;failure++){
 reset();created=!failure;D2HitFunc22ParamStrc args{&owner,&missile,121,7,cap,998};D2AuraCallbackStrc cb{&args,counter,&game};
 int result=MISSMODE_FistOfTheHeavensDelay_AuraCallback(&cb,&target);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",cap,counter,failure,result,calls,request.dwFlags,request.nTargetX,request.nTargetY,request.nSkill,request.nSkillLevel,request.pOwner==&owner,request.pOrigin==&missile);first=false;
}printf("]}\n");}
'''
 STAGE.mkdir(parents=True,exist_ok=True);path=STAGE/'heaven-fist-start.cpp';path.write_text(cpp);binary=path.with_suffix('')
 subprocess.run(['g++','-std=c++17','-O1','-g','-include','initializer_list','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr
 rows=json.loads(result.stdout);assert len(rows['start'])==20 and len(rows['callback'])==72
 for guard,overlay,result,calls,flags,x,y,skill,rank,kind,identity,overlays,owns in rows['start']:
  factory=guard in (0,4);success=guard==0
  assert (result,calls,flags,x,y,skill,rank,kind,identity,overlays,owns)==(int(success),int(factory),int(factory),101 if factory else 0,202 if factory else 0,121 if factory else 0,7 if factory else 0,1 if success else -1,777 if success else -1,int(success and 0<=overlay<3),int(factory))
 for cap,counter,failure,result,calls,flags,x,y,skill,rank,owns,origin in rows['callback']:
  emitted=not(cap>0 and counter>=cap)
  assert (result,calls,flags,x,y,skill,rank,owns,origin)==(int(emitted),int(emitted),32 if emitted else 0,101 if emitted else 0,202 if emitted else 0,121 if emitted else 0,7 if emitted else 0,int(emitted),int(emitted))
 out=STAGE/'cases.json';out.write_text(json.dumps(rows,indent=2)+'\n')
 evidence=dict(referenceVersion='3.3.93847',sourceCommit=tree['sha'],completeFunctions=names,sourceHashes=hashes,counts={k:len(v) for k,v in rows.items()},
  established=['Do80creates flags1at target coordinates,stores target type/id on success,and applies only an in-range overlay. Missing skill/progressive missile/target/factory refuses;failed factory does not write identity/overlay.','The complete child callback uses flags32with original owner/origin,stored skill/rank and candidate coordinates. Positive cap refuses when counter reaches cap;nonpositive cap does not limit this callback. A failed child factory still returns1.'],
  declaredServices=['Table/target/coordinate/factory/overlay callbacks are controlled services. This does not execute missile allocation,world candidate search,counter advancement,hit22damage,mastery,potion,RNG,movement,lifecycle or current executable.','Skill121/rank7and progressive7/child998are opaque harness inputs;current table mapping and runtime skill ranks remain separately pinned. No older/current ID equivalence is inferred from these sentinels.'],
  artifacts={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (Path(__file__).resolve(),ROOT/'tools/probe_blessed_hammer_damage.py',path,binary,out,STAGE/'sanitizer.log')})
 (ROOT/'tools/reference/heaven-fist-start-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
 print('FoH complete startup/capped child callback:20+72cases pass sanitizers;flags1/32,stored target identity/coordinates/overlay guards and failed-child return established with declared services.')
if __name__=='__main__':main()
