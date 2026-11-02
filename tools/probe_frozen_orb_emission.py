#!/usr/bin/env python3
"""Compose complete Frozen Orb emission/turn/burst with the retained full factory."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/frozen-orb-emission-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=ROOT/'tools/reference/frozen-orb-creation-source-evidence.json';prior=json.loads(parent.read_text());original=ROOT/'.local/class-skills/frozen-orb-creation-source/probe.cpp';retained=STAGE/'reused-creation.cpp';expected=prior['artifacts'][str(original.relative_to(ROOT))]
 if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
 assert sha(retained)==expected;cpp='#include <deque>\n'+retained.read_text().split('void PrintRequests()',1)[0];tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit'];bodies={};sources={}
 for rel,names in {'source/D2Game/src/MISSILES/MissMode.cpp':['MISSMODE_SrvDo15_FrozenOrb','MISSMODE_SrvDo16_FrozenOrbNova','MISSMODE_SrvHit29_FrozenOrb'],'source/D2Common/src/Units/Missile.cpp':['MISSILE_GetRemainingFrames']}.items():
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);sources[rel]=sha(p)
  for n in names:bodies[n]=function(raw.decode(),n)
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
 replace('unsigned fFlags=0,dwOwnerGUID=0;', 'unsigned fFlags=0,dwOwnerGUID=0;int nTotalFrames=0,nCurrentFrame=0;')
 replace('int wSubMissile[1]={69};','int wSubMissile[1]={261},wHitSubMissile[1]={262};unsigned dwHitPar[1]={4};')
 replace('void MISSILE_SetTotalFrames(D2UnitStrc* u,int v){u->total=v;}void MISSILE_SetCurrentFrame(D2UnitStrc* u,int v){u->remaining=v;}',
 'void MISSILE_SetTotalFrames(D2UnitStrc* u,int v){u->total=v;u->pMissileData->nTotalFrames=v;}void MISSILE_SetCurrentFrame(D2UnitStrc* u,int v){u->remaining=v;u->pMissileData->nCurrentFrame=v;}')
 allocation=next(line for line in cpp.splitlines() if line.startswith('D2UnitStrc* SUNIT_AllocUnitData('));cpp=cpp.replace(allocation,'std::deque<D2UnitStrc> pool;\nD2UnitStrc* SUNIT_AllocUnitData(int type,int id,int x,int y,D2GameStrc*,D2ActiveRoomStrc*,int,int mode,int){creates++;if(!allocated)return nullptr;pool.emplace_back();auto&u=pool.back();u.dwUnitType=type;u.dwClassId=id;u.x=x;u.y=y;u.mode=mode;u.pDynamicPath=&u.path;MISSILES_Initialize(&game,&u,123);return &u;}')
 replace('row.wRange=30;row.nVel=row.nMaxVel=10;', 'row.wRange=p->nMissile==260?30:25;row.nVel=row.nMaxVel=p->nMissile==260?10:p->nMissile==261?18:24;')
 replace('allocated=world!=3;', 'allocated=world!=3&&(world!=4||factoryCalls%3!=0);')
 cpp+='\nint __stdcall MISSILE_GetRemainingFrames(D2UnitStrc*);\n'
 for body in bodies.values():cpp+='\n'+body+'\n'
 cpp+=r'''
D2UnitStrc* Reset(int rank){for(auto&u:pool)delete u.pMissileData;pool.clear();owner={};target={};row={};skill={};used={};used.rank=rank;requests.clear();world=0;calc=25;owner.dwUnitType=0;owner.x=40;owner.y=70;owner.path.x=50;owner.path.y=70;owner.pDynamicPath=&owner.path;selected=nullptr;hasSkill=hasRecord=hasOwner=hasRoom=allocated=hasUsed=hasHighest=true;town=false;
 assert(D2GAME_SKILLS_Handler_6FD12BA0(&game,&owner,64,rank,0,0,0)==1&&pool.size()==1);auto*p=&pool.front();factoryCalls=creates=collisions=repaths=0;requests.clear();return p;}
void Emit(int kind,int guard,int w,int rank,int elapsed,int index,int y,int parameter,int result,D2UnitStrc*p,bool&first){
 printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d],",first?"":",",kind,guard,w,rank,elapsed,index,y,parameter,result,collisions,MISSILE_GetTargetX(p),MISSILE_GetTargetY(p),p->path.x,p->path.y,repaths);first=false;
 printf("[");for(size_t i=0;i<requests.size();i++){auto&o=requests[i];auto&r=o.request;printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",r.dwFlags,r.nMissile,r.nX,r.nY,r.nTargetX,r.nTargetY,r.nSkill,r.nSkillLevel,o.made,o.x,o.y,o.tx,o.ty,o.velocity,o.frames,o.rank);}printf("],[");
 for(size_t i=1;i<pool.size();i++){auto&u=pool[i];printf("%s[%d,%d,%d,%d,%d]",i>1?",":"",u.dwClassId,MISSILE_GetTargetX(&u),MISSILE_GetTargetY(&u),u.total,u.rank);}printf("]]");}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int g=0;g<5;g++)for(int w=0;w<5;w++)for(int rank:{1,20,255})for(int elapsed:{0,1,2,29})for(int index:{-65,-1,0,1,63,64,129})for(int interval:{0,1,2}){
 auto*p=Reset(rank);world=w;hasRecord=g!=2;hasOwner=g!=4;row.wSubMissile[0]=g==3?-1:261;row.dwParam[0]=interval;row.dwParam[1]=19;MISSILE_SetTargetX(p,index);MISSILE_SetTargetY(p,17);MISSILE_SetCurrentFrame(p,30-elapsed);
 int result=MISSMODE_SrvDo15_FrozenOrb(&game,g==1?nullptr:p);Emit(0,g,w,rank,elapsed,index,17,interval,result,p,first);
}
int vectors[6][2]={{30,0},{0,30},{-29,29},{1,-2},{-1,-1},{0,0}};
for(int g=0;g<3;g++)for(int rank:{1,20,255})for(int elapsed=0;elapsed<=6;elapsed++)for(auto&v:vectors){
 auto*p=Reset(rank);p->dwClassId=262;MISSILE_SetTotalFrames(p,25);MISSILE_SetCurrentFrame(p,25-elapsed);MISSILE_SetTargetX(p,v[0]);MISSILE_SetTargetY(p,v[1]);row.dwParam[0]=6;row.dwParam[1]=2;hasRecord=g!=2;
 int result=MISSMODE_SrvDo16_FrozenOrbNova(&game,g==1?nullptr:p);Emit(1,g,0,rank,elapsed,v[0],v[1],2,result,p,first);
}
for(int g=0;g<5;g++)for(int w=0;w<5;w++)for(int rank:{1,20,255})for(int remaining:{-1,0,1,30})for(int step:{0,1,4,64}){
 auto*p=Reset(rank);world=w;hasRecord=g!=2;hasOwner=g!=4;row.wHitSubMissile[0]=g==3?-1:262;row.dwHitPar[0]=step;MISSILE_SetCurrentFrame(p,remaining);
 int result=MISSMODE_SrvHit29_FrozenOrb(&game,g==1?nullptr:p,nullptr);Emit(2,g,w,rank,remaining,0,0,step,result,p,first);
}for(auto&u:pool)delete u.pMissileData;printf("]\n");}
'''
 for name,body in bodies.items():assert function(cpp,name)==body,name
 # The retained creation bodies still execute in Reset; preserve their exact text.
 for name,digest in prior['routineHashes'].items():assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==digest,name
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==7878
 # Reuse the production direction data only as independent table inputs; body arithmetic is compared separately.
 raw=(ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/MISSILES/MissMode.cpp').read_text();import re
 def array(name):return [int(x) for x in re.search(r'const(?:expr)? int32_t '+name+r'\[.*?\]\s*=\s*\{([^}]+)',raw[raw.index('int32_t __fastcall MISSMODE_SrvDo15_FrozenOrb'):],re.S)[1].replace('\n','').split(',') if x.strip()]
 xs,ys=array('xPositions'),array('yPositions');assert len(xs)==len(ys)==64
 def made(w,n):return w==0 or w==4 and n%3!=0
 for inputs,out,requests,children in cases:
  kind,g,w,rank,elapsed,x,y,param=inputs;vectors=[];expectedOut=None
  if kind==0:
   valid=g==0;emit=valid and elapsed%max(param,1)==0;idx=abs(x)%64
   if emit:vectors=[(261,xs[idx],ys[idx])]
   expectedOut=[1 if valid else 2,int(valid),(idx+19)%64 if emit else x,y,50,70,0]
  elif kind==1:
   valid=g==0;turn=valid and elapsed<6 and elapsed%2==0;nx,ny=(int((x-y)/2),int((x+y)/2)) if turn else(x,y)
   expectedOut=[1 if valid else 2,int(valid),nx,ny,40+nx if turn else 50,70+ny if turn else 70,int(turn)]
  else:
   valid=g==0;emit=valid and elapsed==0
   if emit:vectors=[(262,xs[i],ys[i]) for i in range(0,64,max(param,1))]
   result=1 if g in(1,2,3) else 2 if elapsed!=0 else 1 if g==4 else 3;expectedOut=[result,0,0,0,50,70,0]
  expectedRequests=[];expectedChildren=[]
  for n,(missile,dx,dy) in enumerate(vectors,1):
   success=made(w,n);expectedRequests.append([2,missile,0,0,dx,dy,64,rank,int(success),*([40,70,40+dx,70+dy,3456 if missile==261 else 4608,25,rank] if success else[0]*7)])
   if success:expectedChildren.append([missile,0 if kind==0 else dx,0 if kind==0 else dy,25,rank])
  expectedOut[-1]+=len(expectedChildren) # Full child factory also recalculates its path.
  assert out==expectedOut and requests==expectedRequests and children==expectedChildren,(inputs,out,requests,children,expectedOut,expectedRequests,expectedChildren)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n');pin=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';manifest=json.loads((pin.parent.parent/'sources.json').read_text());assert sha(pin)==manifest['files']['json/missiles.json']['sha256'];rows={n:json.loads(pin.read_text())[n] for n in ('frozenorb','frozenorbbolt','frozenorbnova')}
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},reusedRoutineHashes=prior['routineHashes'],currentTable={str(pin.relative_to(ROOT)):sha(pin)},currentRows=rows,sanitizers='ASan/UBSan clean',
 established=['Complete do15/do16/hit29 and elapsed getter compose with all nine retained generic wrapper/launcher/full-factory/initializer/data/accessor bodies. Do15advances abs(stored index%64)by19at eligible elapsed frames,including failed allocations,and emits flags2origin-relative261requests with retained64/rank. Their fresh stored target coordinates remain0while their physical path aim uses the direction table.',
 'Hit29only attempts the terminal262loop when current frame is exactly0. Current step4gives16directions;zero/1/64boundary controls follow source clamping. Full factory room/row/allocation and mixed failures do not prevent later requests. Every successfully allocated terminal child receives its own stored relative direction through the complete setters,distinct from absolute path aim.',
 'Do16turns stored vectors at elapsed0/2/4under6/2using signed truncating halves,updates the physical path target and saved vector,and invokes the shared collision service afterward. Negative/small/zero vector controls are explicit. Full child factories produce velocity3456/4608and25frame lifetimes.'],
 declaredServices=['Older engine,not modern executable proof. The retained factory storage service now supplies stable deque units so children cannot overwrite the emitting parent. All canonical bodies remain unchanged. Table/owner/row/path/coordinate/timer/seed/stat-writing/allocation services are declared;provided nova actor states and controlled remaining frames isolate arithmetic and callback order.',
 'Generic collision is a counting service here. This does not yet prove movement/decrement/router/terminal interleaving,InitSteps,actual path normalization/cells,zero-damage root events,damage fill/cold/HP/return/PvP,modern global seed/lifecycle/client or native saves/input. No runtime C#/data/install/save change.'],
 artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
 out=ROOT/'tools/reference/frozen-orb-emission-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Frozen Orb emission:7878complete do15/do16/hit29/elapsed+retained factory cases;trail index/terminal vectors/partial failures;ASan/UBSan clean;motion/collision and runtime separate.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
