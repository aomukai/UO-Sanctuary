#!/usr/bin/env python3
"""Compose complete Blizzard do10 with the existing ground-scatter and lifetime source."""
import argparse,ast,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/blizzard-center-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=ROOT/'tools/probe_fissure_reference.py';tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 # Reuse the existing ABI/service fixture verbatim before adapting its data.
 fn=next(n for n in ast.parse(parent.read_text()).body if isinstance(n,ast.FunctionDef) and n.name=='build');assignment=next(n for n in fn.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='cpp' for t in n.targets));cpp=ast.literal_eval(assignment.value);retained=STAGE/'reused-fissure-fixture.cpp';retained.write_text(cpp)
 replacements=[('nTotalFrames=80,nCurrentFrame=80','nTotalFrames=100,nCurrentFrame=100'),('dwClassId=461','dwClassId=158'),('skill=234','skill=59'),('wSubMissile[1]={462}','wSubMissile[1]={159}'),('wSrvMissileA=461','wSrvMissileA=158'),('intervalValue=6','intervalValue=4'),('assert(n==234)','assert(n==59)'),('assert(skillId==234&&rank==20)','assert(skillId==59&&rank>=1&&rank<=255)')]
 for a,b in replacements:assert a in cpp,a;cpp=cpp.replace(a,b)
 sources={};bodies={}
 routes={'source/D2Common/src/D2Seed.cpp':['SEED_InitLowSeed'],'source/D2Common/include/D2Seed.h':['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber'],'source/D2Game/src/ITEMS/Items.cpp':['ITEMS_RollLimitedRandomNumber'],'source/D2Common/src/Units/Missile.cpp':['MISSILE_GetRemainingFrames'],
 'source/D2Game/src/MISSILES/MissMode.cpp':['MISSMODE_HandleMissileCollision','MISSMODE_CreateMissileWithCollisionCheck','MISSMODE_SrvDo10_BlizzardCenter']}
 for rel,names in routes.items():
  path=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=path.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);sources[rel]=sha(path)
  for name in names:
   body=function(raw.decode(),name);bodies[name]=body;cpp+='\n'+body.replace('0x6AC690C5i64','0x6AC690C5ULL')+'\n'
 cpp+=r'''
int main(){printf("{\"center\":[");bool first=true;
for(int guard=0;guard<9;guard++)for(int elapsed=0;elapsed<=100;elapsed++)for(int bits:{0,1,4,0x40})for(int interval:{0,1,4})for(int rank:{1,20,255})for(int x:{300,301,-300,2147480000}){
 Reset();actor.x=x;actor.rank=rank;hasOwner=guard!=1;hasRoom=guard!=2;hasRow=guard!=3;hasSkill=guard!=4;row.wSubMissile[0]=guard==5?0:guard==7?-1:159;allocate=guard!=8;clockData.nCurrentFrame=100-elapsed;collisionBits=bits;intervalValue=interval;
 int result=MISSMODE_SrvDo10_BlizzardCenter(&game,guard==6?nullptr:&actor);
 printf("%s[[%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%llu],",first?"":",",guard,elapsed,bits,interval,rank,x,result,clockData.nCurrentFrame,terminalCalls,formulaCalls,checks,checkedX,checkedY,checkedMask,(unsigned long long)actor.pSeed.lSeed);first=false;PrintRequests();printf("]");
}printf("],\"lifecycle\":[");Reset();first=true;
for(int frame=0;frame<100;frame++){int before=requests.size();int result=MISSMODE_SrvDo10_BlizzardCenter(&game,&actor);printf("%s[%d,%d,%d,%d,%d]",first?"":",",frame,result,clockData.nCurrentFrame,(int)requests.size()-before,terminalCalls);first=false;}printf("]}\n");}
'''
 for name,body in bodies.items():assert function(cpp,name)==body.replace('0x6AC690C5i64','0x6AC690C5ULL'),name
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases['center'])==130896 and len(cases['lifecycle'])==100
 for inputs,out,requests in cases['center']:
  guard,elapsed,bits,interval,rank,x=inputs;valid=guard not in(3,4,6);eligible=valid and guard not in(1,2) and interval!=0 and elapsed%interval==0
  seed=0x123456789ABCDEF0;px=py=mask=0
  if eligible:
   low=(x+elapsed)&0xffffffff;high=666;seed=(low*0x6ac690c5+high)&0xffffffffffffffff;px=x+(seed&0xffffffff)%12-6
   seed=((seed&0xffffffff)*0x6ac690c5+(seed>>32))&0xffffffffffffffff;py=400+(seed&0xffffffff)%12-6;mask=5
  expected=[[3,0 if guard==5 else -1 if guard==7 else 159,px,py,59,rank,1]] if eligible and bits&5==0 else []
  assert out==[2 if not valid or elapsed>=99 else 1,99-elapsed if valid else 100-elapsed,int(valid and elapsed>=99),2*int(valid),int(eligible),px,py,mask,seed] and requests==expected,(inputs,out,requests,expected)
 assert sum(r[3] for r in cases['lifecycle'])==25
 for frame,result,current,attempts,terminal in cases['lifecycle']:assert (result,current,attempts,terminal)==(2 if frame==99 else 1,99-frame,int(frame%4==0),int(frame==99))
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n');pins={};tables={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
 for name in ('skills','missiles'):
  path=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(path)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(path.relative_to(ROOT))]=sha(path);tables[name]=json.loads(path.read_text())
 skill=next(r for r in tables['skills'].values() if r.get('skill')=='Blizzard');center=tables['missiles']['blizzardcenter'];child=tables['missiles']['blizzard1'];assert center['pSrvDoFunc']==10 and center['Range']==100 and center['SubMissile1']=='blizzard1' and skill['Param1']==7 and skill['Param3']==4 and child['*ID']==159
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=130996,sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},currentTable=pins,currentRows={'skill':skill,'center':center,'child':child},sanitizers='ASan/UBSan clean',
 established=['Complete do10,shared scatter,elapsed getter,seed/RNG/item wrappers and full generic collision body compose. Valid row/skill do10 evaluates two current formulas at retained rank and calls the mask5 helper BEFORE lifetime decrement;100body calls emit25flags3child159requests at elapsed0,4,...96 and terminate on call99.',
 'Missing owner/room suppresses sampling but not ordinary center lifetime. Missing row/skill or null center returns2before lifetime/formulas. Unlike Fissure do25,zero or negative child IDs do not stop do10 early:eligible requests reach the declared factory. Failed landing mask or allocation does not suppress later updates. Every eligible sample resets both seed halves from sourceX+elapsed,consumes two draws and produces offsets[-6,+5] with exact64-bit seed;mask0x40 alone is not blocked by Blizzard mask5.'],
 declaredServices=['Older source,not modern executable proof. Existing Fissure ABI/services are reused;declared stationary NOCOLLIDE mode,table/formula/owner/room/mask/allocation/terminal services remain explicit. Full canonical source bodies are unchanged except i64literal suffix spelling for the C++ compiler. Factory is a recording service here;its actual creation composition is separate.',
 'Current outer cast/payment/right-only/native grid/stat/seed creation,child damage capture,cold/LastCollide/hit routing/return/fire callbacks,offline/save/client/assets remain unaccepted. This proof changes no gameplay C#,runtime data,installation or saves.'],
 artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
 out=ROOT/'tools/reference/blizzard-center-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Blizzard center:130896complete do10/scatter/lifetime cases+100frame lifecycle;25requests;ASan/UBSan clean;runtime unchanged.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
