#!/usr/bin/env python3
"""Compose complete older Fire Wall admission/cast/maker with the saved factory."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/fire-wall-creation-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True)
 parent=ROOT/'tools/reference/chilling-armor-source-evidence.json';prior=json.loads(parent.read_text())
 original=ROOT/'.local/class-skills/chilling-armor-source/probe.cpp';retained=STAGE/'reused-chilling.cpp';expected=prior['artifacts'][str(original.relative_to(ROOT))]
 if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
 assert sha(retained)==expected
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 blocks={};sources={}
 for rel,names in {
  'source/D2Game/src/SKILLS/SkillSor.cpp':('SKILLS_SrvDo024_FireWall',),
  'source/D2Game/src/SKILLS/Skills.cpp':('sub_6FD15340',),
  'source/D2Game/src/MISSILES/MissMode.cpp':('MISSMODE_SrvDo06_MoltenBoulder_FireWallMaker',),
  'source/D2Game/src/MISSILES/Missiles.cpp':('MISSILES_CreateMissileFromParams',),
  'source/D2Common/src/Path/Step.cpp':('D2Common_10234',)}.items():
  raw=(ROOT/'.local/class-skills/d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
  sources[rel]=sha(ROOT/'.local/class-skills/d2moo-pinned-source'/rel)
  for n in names:blocks[n]=function(raw.decode(),n)
 cpp=retained.read_text().split('int main()',1)[0]
 assert function(cpp,'MISSILES_CreateMissileFromParams')==blocks['MISSILES_CreateMissileFromParams']
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a;cpp=cpp.replace(a,b,1)
 replace('#define __stdcall','#define __stdcall\n#define D2_ASSERT assert\nusing BOOL=int;\nconstexpr int FALSE=0,PATH_UNKNOWN_FLAG_0x00008=8;')
 replace('int x=0,y=0,velocity=0,flags=0;','int x=0,y=0,velocity=0,flags=0,dwFlags=0;')
 replace('struct D2MissilesTxt{','struct D2MissilesTxt{int nSize=1;int wSubMissile[1]={69};')
 replace('D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc* u){return hasOwner?u->owner:nullptr;}','D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc* u){return hasOwner&&u?u->owner:nullptr;}')
 wrapper='int factoryCalls=0;D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*g,D2MissileStrc*p){factoryCalls++;captured=*p;return ArmorFactory::MISSILES_CreateMissileFromParams(g,p);}'
 replace(wrapper,r'''
int factoryCalls=0,world=0,collisionResult=1;bool blocked=false;
struct Observation{D2MissileStrc request;bool made;int x,y,tx,ty,velocity,frames,rank;};std::vector<Observation> requests;
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*g,D2MissileStrc*p){
 factoryCalls++;captured=*p;auto old=row;bool roomBefore=hasRoom,recordBefore=hasRecord,allocationBefore=allocated;
 row={};row.wRange=p->nMissile==68?7+2*p->nSkillLevel:90;row.nVel=row.nMaxVel=p->nMissile==68?12:0;row.nCollideType=p->nMissile==68?8:3;row.dwMissileFlags=0;row.dwDamageRate=p->nMissile==68?0:41;
 hasRoom=world!=1;hasRecord=world!=2;allocated=world!=3;
 auto result=ArmorFactory::MISSILES_CreateMissileFromParams(g,p);
 requests.push_back({*p,result!=nullptr,result?result->x:0,result?result->y:0,result?result->path.x:0,result?result->path.y:0,result?result->path.velocity:0,result?result->total:0,result?result->rank:0});
 row=old;hasRoom=roomBefore;hasRecord=recordBefore;allocated=allocationBefore;return result;
}
''')
 replace('int MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc*){collisions++;return 1;}','int MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc*);')
 # Collision/lifetime is a declared service in this creation composition.
 cpp+='\nint MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc*){collisions++;return collisionResult;}\n'
 cpp+='\nint D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc*p){return p->x;}\nint D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc*p){return p->y;}\nint COLLISION_CheckMaskWithSizeXY(D2ActiveRoomStrc*,int,int,int,int,unsigned mask){assert(mask==5);return blocked?5:0;}\nint MISSILE_GetSkill(D2UnitStrc*p){return p->skill;}\n'
 for n in ('D2Common_10234','sub_6FD15340','SKILLS_SrvDo024_FireWall','MISSMODE_SrvDo06_MoltenBoulder_FireWallMaker'):cpp+='\n'+blocks[n]+'\n'
 cpp+=r'''
void PrintRequests(){printf("[");for(size_t i=0;i<requests.size();i++){auto&o=requests[i];auto&p=o.request;printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",p.dwFlags,p.nMissile,p.nX,p.nY,p.nTargetX,p.nTargetY,p.nSkill,p.nSkillLevel,o.made,o.x,o.y,o.tx,o.ty,o.velocity,o.frames,o.rank);}printf("]");}
void Reset(){owner={};target={};other={};created={};row={};skill={};hasSkill=hasRecord=hasRoom=hasOwner=allocated=true;town=blocked=slow=false;selected=nullptr;factoryCalls=creates=collisions=0;requests.clear();owner.dwUnitType=0;owner.dwUnitId=77;owner.x=40;owner.y=70;owner.pDynamicPath=&owner.path;skill.wSrvMissileA=68;skill.wSrvMissileB=69;}
int main(){for(int n=0;n<32;n++)gdwBitMasks[n]=1u<<n;printf("{\"cast\":[");bool first=true;
int positions[6][2]={{50,70},{40,70},{40,60},{55,85},{150,70},{-1,60}};
for(int guard=0;guard<12;guard++)for(int w=0;w<4;w++)for(int rank:{-1,0,1,20,255})for(int point=0;point<6;point++)for(int select:{0,1})for(int slowed:{0,1}){
 Reset();world=w;slow=slowed;int x=positions[point][0],y=positions[point][1];owner.path.x=x;owner.path.y=y;target.x=x+3;target.y=y+7;if(select)selected=&target;
 hasSkill=guard!=1;skill.wSrvMissileA=guard==2?-1:guard==3?1000:68;hasRoom=guard!=4;blocked=guard==5;if(guard==6)owner.path.x=0;if(guard==7)owner.path.y=0;town=guard==8;skill.wSrvMissileB=guard==9?-1:guard==10?1000:guard==11?0:69;
 int ret=SKILLS_SrvDo024_FireWall(&game,&owner,51,rank);
 printf("%s[[%d,%d,%d,%d,%d,%d],[%d,%d,%d],",first?"":",",guard,w,rank,point,select,slowed,ret,factoryCalls,creates);PrintRequests();printf("]");first=false;
}
printf("],\"maker\":[");first=true;
for(int guard=0;guard<7;guard++)for(int flags:{0,8,9})for(int w=0;w<4;w++)for(int rank:{-1,0,1,20,255})for(int result:{1,2}){
 Reset();world=w;collisionResult=result;hasOwner=guard!=1;hasRecord=guard!=3;row.wSubMissile[0]=guard==4?-1:guard==6?0:69;other.owner=&owner;other.skill=51;other.rank=rank;other.x=50;other.y=60;other.path.dwFlags=flags;other.pDynamicPath=guard==5?nullptr:&other.path;
 int ret=MISSMODE_SrvDo06_MoltenBoulder_FireWallMaker(&game,guard==2?nullptr:&other);
 printf("%s[[%d,%d,%d,%d,%d],[%d,%d,%d],",first?"":",",guard,flags,w,rank,result,ret,collisions,creates);PrintRequests();printf("]");first=false;
}printf("]}\n");}
'''
 for n,b in blocks.items():assert function(cpp,n)==b,n
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
 cases=json.loads(subprocess.check_output([str(binary)]));assert {k:len(v) for k,v in cases.items()}=={'cast':5760,'maker':840}
 def factory(req,world):
  flags,missile,x,y,tx,ty,skill,rank=req;moving=missile==68
  if moving and (tx,ty)==(x,y):tx+=1;ty+=1
  made=world==0 and (not moving or abs(tx-x)<100 and abs(ty-y)<100)
  # The factory invokes allocation only after row,room and distance guards.
  allocated=int(world not in (1,2) and (not moving or abs(tx-x)<100 and abs(ty-y)<100))
  effective=rank
  return [*req,int(made),*([x,y,tx if flags&32 else x,ty if flags&32 else y,2304 if moving else 0,7+2*rank if moving else 90,effective] if made else [0]*7)],allocated
 positions=[(50,70),(40,70),(40,60),(55,85),(150,70),(-1,60)]
 for inputs,out,actual in cases['cast']:
  guard,world,rank,point,select,slowed=inputs;x,y=positions[point]
  if select:x+=3;y+=7
  else:
   if guard==6:x=0
   if guard==7:y=0
  accepted=guard not in (1,2,3,4,5,8) and x!=0 and y!=0
  expected=[];allocations=0
  if accepted:
   dx,dy=40-x,70-y
   reqs=[[33,68,x,y,x-dy,y+dx,51,rank],[33,68,x,y,x+dy,y-dx,51,rank]]
   if guard not in (9,10):reqs.append([1,0 if guard==11 else 69,x,y,0,0,51,rank])
   for req in reqs:
    obs,alloc=factory(req,world);expected.append(obs);allocations+=alloc
  assert out==[int(accepted),len(expected),allocations] and actual==expected,(inputs,out,actual,expected)
 for inputs,out,actual in cases['maker']:
  guard,flags,world,rank,result=inputs;accepted=guard in (0,6);expected=[];allocations=0
  if accepted and flags&8:
   obs,allocations=factory([33,0 if guard==6 else 69,50,60,50,60,51,rank],world);expected=[obs]
  assert out==[result if accepted else 2,int(accepted),allocations] and actual==expected,(inputs,out,actual,expected)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 tables={};pins={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
 for name in ('skills','missiles'):
  path=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(path)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(path.relative_to(ROOT))]=sha(path);tables[name]=json.loads(path.read_text())
 skill=next(r for r in tables['skills'].values() if r.get('skill')=='Fire Wall');maker=tables['missiles']['firewallmaker'];field=tables['missiles']['firewall']
 assert skill['srvdofunc']==24 and skill['srvmissilea']=='firewallmaker' and skill['srvmissileb']=='firewall' and skill['localdelay']==35
 assert maker['*ID']==68 and maker['Range']=='7 +  (lvl *  2)' and maker['Vel']==12 and not maker.get('CanSlow',0) and maker['pSrvDoFunc']==6 and maker['SubMissile1']=='firewall'
 assert field['*ID']==69 and field['Range']==90 and field['DamageRate']==41 and field['pSrvDmgFunc']==3
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=6600,caseCounts={k:len(v) for k,v in cases.items()},sanitizers='ASan/UBSan clean',sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},currentTable=pins,currentRows={'skill':skill,'maker':maker,'field':field},
  established=['Complete do24 and admission collision helper resolve selected unit before path aim,require nonzero coordinates,a room and clear target mask5,nontown target;two flags33 perpendicular makers precede optional flags1center field. Each factory refusal retains outer success and subsequent requests.',
  'Complete shared factory uses supplied evaluated maker lifetime7+2*rank,velocity2304 unaffected by Slow Missiles because current maker CanSlow0. A same-point maker target is nudged(+1,+1);two self-aim makers therefore share the same diagonal direction in this older chain. The full modern cast/factory must still corroborate it.',
  'Complete do6 requires owner,row,nonnegative child and path. Previous flag8 emits one stationary flags33child at the current maker position before the collision/lifetime service. Child0 is valid,negative child refuses. The child retains skill/rank and90frame lifetime.'],
  declaredServices=['Older engine,not modern executable proof. Complete do24,admission helper,do6,movement-flag accessor and missile factory are unmodified. Room/stat/target/path coordinates,mask5 query,allocation,damage-writing and generic collision/lifetime are supplied services. Current Range expression is supplied evaluated into an older literal slot;modern expression evaluation is not executed.',
  'Zero-ID and negative-rank cases are controlled ABI inputs with supplied row/formula services,not claims about the real missile0 row or modern negative-rank handling. No native gameplay route,full movement/flag chronology,modern self-target behavior,terrain/barrier/cell geometry,damage filling/global seed/stat/event/PvP/client/save or class certificate is claimed. Reuse existing MoltenBoulderPackets.Trail for do6 and Blaze field03/flags/return with Fire Wall DamageRate41;do not duplicate or treat a floating normalized vector as source-exact.'],
  artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
 out=ROOT/'tools/reference/fire-wall-creation-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Fire Wall creation:6600complete older admission/do24/do6/factory compositions;ASan/UBSan clean;modern movement,self-aim,damage and native route remain separate.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
