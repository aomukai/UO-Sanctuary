#!/usr/bin/env python3
"""Compose complete Meteor do28/admission/creator with the retained full factory."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/meteor-creation-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=ROOT/'tools/reference/fire-wall-creation-source-evidence.json';prior=json.loads(parent.read_text());original=ROOT/'.local/class-skills/fire-wall-creation-source/probe.cpp';retained=STAGE/'reused-fire-wall.cpp';expected=prior['artifacts'][str(original.relative_to(ROOT))]
 if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
 assert sha(retained)==expected;cpp=retained.read_text().split('void PrintRequests()',1)[0]
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 blocks={};sources={}
 for rel,names in {'source/D2Game/src/SKILLS/SkillSor.cpp':('SKILLS_SrvDo028_Meteor_Blizzard_Eruption_BaalTaunt_Catapult',),'source/D2Game/src/SKILLS/Skills.cpp':('sub_6FD15340','D2GAME_CreateMissile_6FD115E0'),'source/D2Game/src/MISSILES/Missiles.cpp':('MISSILES_CreateMissileFromParams',)}.items():
  path=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=path.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);sources[rel]=sha(path)
  for n in names:
   body=function(raw.decode(),n);blocks[n]=body
   if n not in ('sub_6FD15340','MISSILES_CreateMissileFromParams'):continue
   assert function(cpp,n)==body,n
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a;cpp=cpp.replace(a,b,1)
 replace('int UNITS_GetDistanceToCoordinates(D2UnitStrc*,int,int){std::abort();}','int nativeDistance=99,distanceCalls=0;int UNITS_GetDistanceToCoordinates(D2UnitStrc*,int,int){distanceCalls++;return nativeDistance;}')
 replace('row.wRange=p->nMissile==68?7+2*p->nSkillLevel:90;row.nVel=row.nMaxVel=p->nMissile==68?12:0;row.nCollideType=p->nMissile==68?8:3;row.dwMissileFlags=0;row.dwDamageRate=p->nMissile==68?0:41;', 'row.wRange=60;row.nVel=row.nMaxVel=0;row.nCollideType=0;row.dwMissileFlags=0;row.dwDamageRate=0;')
 cpp+='\n'+blocks['D2GAME_CreateMissile_6FD115E0']+'\n'+blocks['SKILLS_SrvDo028_Meteor_Blizzard_Eruption_BaalTaunt_Catapult']+'\n'
 cpp+=r'''
void PrintRequests(){printf("[");for(size_t i=0;i<requests.size();i++){auto&o=requests[i];auto&p=o.request;printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",p.dwFlags,p.nMissile,p.nX,p.nY,p.nTargetX,p.nTargetY,p.nSkill,p.nSkillLevel,o.made,o.x,o.y,o.tx,o.ty,o.velocity,o.frames,o.rank);}printf("]");}
int main(){for(int n=0;n<32;n++)gdwBitMasks[n]=1u<<n;printf("[");bool first=true;int positions[4][2]={{50,70},{40,70},{0,60},{150,70}};
for(int guard=0;guard<9;guard++)for(int w=0;w<4;w++)for(int rank:{1,20,255})for(int select:{0,1})for(int point=0;point<4;point++)for(int distance:{99,100,101})for(int kind:{0,1}){
 owner={};target={};created={};row={};skill={};requests.clear();factoryCalls=creates=distanceCalls=0;world=w;nativeDistance=distance;owner.dwUnitType=kind;owner.dwUnitId=77;owner.dwFlags=0x100000;owner.x=40;owner.y=70;owner.pDynamicPath=&owner.path;
 int x=positions[point][0],y=positions[point][1];owner.path.x=x;owner.path.y=y;target.x=x+3;target.y=y+7;selected=select?&target:nullptr;
 hasSkill=guard!=1;hasRecord=hasOwner=allocated=true;hasRoom=guard!=4&&guard!=8;blocked=guard==5;skill.wSrvMissileA=guard==2?-1:guard==3?1000:101;if(guard==6)owner.path.x=0;if(guard==7)owner.path.y=0;
 int result=SKILLS_SrvDo028_Meteor_Blizzard_Eruption_BaalTaunt_Catapult(&game,guard==8?nullptr:&owner,56,rank);
 printf("%s[[%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d],",first?"":",",guard,w,rank,select,point,distance,kind,result,owner.dwFlags,factoryCalls,creates,distanceCalls);PrintRequests();printf("]");first=false;
}printf("]\n");}
'''
 for n,b in blocks.items():assert function(cpp,n)==b,n
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==5184
 positions=[(50,70),(40,70),(0,60),(150,70)]
 for inputs,out,requests in cases:
  guard,world,rank,select,point,distance,kind=inputs;x,y=positions[point]
  if select:x+=3;y+=7
  else:
   if guard==6:x=0
   if guard==7:y=0
  admitted=guard not in (1,2,3,4,5,8) and x!=0 and y!=0;requested=admitted and distance<=100;made=requested and world==0
  expected=[[1,101,x,y,0,0,56,rank,int(made),*([x,y,x,y,0,60,rank] if made else [0]*7)]] if requested else []
  flags=0x100000|(64 if guard not in (1,2,3,8) else 0)
  assert out==[int(made),flags,int(requested),int(requested and world not in (1,2)),int(admitted)] and requests==expected,(inputs,out,requests,expected)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 tables={};pins={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
 for name in ('skills','missiles'):
  path=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(path)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(path.relative_to(ROOT))]=sha(path);tables[name]=json.loads(path.read_text())
 skill=next(r for r in tables['skills'].values() if r.get('skill')=='Meteor');center=tables['missiles']['meteorcenter'];assert skill['srvdofunc']==28 and skill['srvmissilea']=='meteorcenter' and center['*ID']==101 and center['Range']==60 and not center.get('Vel',0) and not center.get('CollideType',0)
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},currentTable=pins,currentRows={'skill':skill,'center':center},sanitizers='ASan/UBSan clean',
 established=['Complete do28,admission helper,generic ground creator and missile factory compose. After valid skill/missile checks,the caster service flag64is set before target admission. Selected-unit coordinates precede path aim;target mask5 and distance greater than100can refuse before allocation.',
 'One flags1center101request produces a stationary60frame warning at the chosen coordinates with56/retained rank. Full factory row/room/allocation refusal returns a failed cast,unlike Fire Wall. Both player/monster controlled owner kinds follow this creation chain.'],
 declaredServices=['Older engine,not modern executable proof. Reused exact retained factory scaffold;room/coordinate/target/mask5/distance,allocation,path and damage-writing services remain explicit. The distance is a supplied source-service result,not a claim about native UO grid distance. Null caster has no source room in this fixture.',
 'No runtime handler,outer payment/animation/LOS/town gating,modern stat/global seed,warning expiry/hit14/area/field producer,damage filling/return/client/save or class acceptance is claimed. Reuse the existing Mage/Phoenix impact/field proof and shared typed controllers for those separate phases.'],artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
 out=ROOT/'tools/reference/meteor-creation-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Meteor creation:5184complete older do28/admission/ground-creator/factory cases;ASan/UBSan clean;modern world/payment/impact remain separate.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
