#!/usr/bin/env python3
"""Compose full Frozen Orb update/router/movement-wrapper order on the Blizzard ABI."""
import argparse,hashlib,json,re,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/frozen-orb-collision-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=ROOT/'tools/reference/blizzard-collision-source-evidence.json';prior=json.loads(parent.read_text());original=ROOT/'.local/class-skills/blizzard-collision-source/probe.cpp';retained=STAGE/'reused-blizzard.cpp';expected=prior['artifacts'][str(original.relative_to(ROOT))]
 if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
 assert sha(retained)==expected;cpp=retained.read_text().split('void reset()',1)[0];tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit'];blocks={};sources={}
 routes={'source/D2Game/src/MISSILES/MissMode.cpp':['MISSMODE_SrvDo01_BasicMissile','MISSMODE_SrvDo15_FrozenOrb','MISSMODE_SrvDo16_FrozenOrbNova','MISSMODE_SrvHit29_FrozenOrb'],
 'source/D2Common/src/Units/Missile.cpp':['MISSILE_GetRemainingFrames'],'source/D2Game/src/UNIT/SUnit.cpp':['sub_6FCBC7E0','sub_6FCBC930']}
 for rel,names in routes.items():
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);sources[rel]=sha(p)
  for name in names:blocks[name]=function(raw.decode(),name)
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
 replace('struct D2DynamicPathStrc{int velocity=2304,flags=64;};','struct D2UnitStrc;struct D2DynamicPathStrc{int velocity=2304,flags=64,x=50,y=70;D2UnitStrc* target=nullptr;};')
 replace('int nOwnerType=0,dwOwnerGUID=-1;', 'int nOwnerType=0,dwOwnerGUID=-1,nTotalFrames=30,nCurrentFrame=30;')
 replace('int dwUnitType=3,dwClassId=385,','int targetY=0,dwAnimMode=0;int dwUnitType=3,dwClassId=385,')
 replace('wHitSubMissile[1]={386},dwHitPar[1]={0};','wSubMissile[1]={261},dwParam[2]={1,19},wHitSubMissile[1]={262};unsigned dwHitPar[1]={4};')
 replace('int nSkill=0,nMissile=0,dwFlags=0,nX=0,nY=0,nSkillLevel=0;D2UnitStrc* pOwner=nullptr;','int nSkill=0,nMissile=0,dwFlags=0,nX=0,nY=0,nTargetX=0,nTargetY=0,nSkillLevel=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;')
 replace('void MISSMODE_SrvHit29_FrozenOrb(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){assert(false);}', 'int32_t __fastcall MISSMODE_SrvHit29_FrozenOrb(D2GameStrc*,D2UnitStrc*,D2UnitStrc*);') if 'void MISSMODE_SrvHit29_FrozenOrb' in cpp else replace('int32_t MISSMODE_SrvHit29_FrozenOrb(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){assert(false);return 3;}', 'int32_t __fastcall MISSMODE_SrvHit29_FrozenOrb(D2GameStrc*,D2UnitStrc*,D2UnitStrc*);')
 replace('int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){moves++;return moveResult;}', 'int32_t __fastcall sub_6FCBC930(D2GameStrc*,D2UnitStrc*);')
 replace('void MISSILE_SetCurrentFrame(D2UnitStrc* p,int f){p->frame=f;frameWrites++;}', 'void MISSILE_SetCurrentFrame(D2UnitStrc* p,int f){p->frame=f;p->pMissileData->nCurrentFrame=f;frameWrites++;}')
 replace('D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){creates++;captured=*p;child.x=p->nX;child.y=p->nY;child.skill=p->nSkill;child.rank=p->nSkillLevel;return factory?&child:nullptr;}',
 'struct Request{D2MissileStrc p;int x,y;};std::vector<Request> requests;D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){creates++;captured=*p;int x=p->pOrigin?p->pOrigin->x:p->nX,y=p->pOrigin?p->pOrigin->y:p->nY;requests.push_back({*p,x,y});child.x=x;child.y=y;child.skill=p->nSkill;child.rank=p->nSkillLevel;return factory?&child:nullptr;}')
 cpp+=r'''
#define D2_ASSERT assert
using D2UnitGUID=int;constexpr int UNIT_ITEM=4,IMODE_INBELT=1,IMODE_EQUIP=2;
int reconciles=0,rebinds=0,pathCalls=0;bool invalidRoom=false;
int MISSILE_GetTargetY(D2UnitStrc*p){return p->targetY;}void MISSILE_SetTargetY(D2UnitStrc*p,int v){p->targetY=v;}
void D2COMMON_10170_PathSetTargetPos(D2DynamicPathStrc*p,int x,int y){p->x=x;p->y=y;}int D2Common_10142(D2DynamicPathStrc*,D2UnitStrc*,int){pathCalls++;return 1;}
D2UnitStrc* PATH_GetTargetUnit(D2DynamicPathStrc*p){return p->target;}void PATH_SetTargetUnit(D2DynamicPathStrc*p,D2UnitStrc*u){p->target=u;}
void PATH_GetTargetTypeAndGUID(D2DynamicPathStrc*,int*,D2UnitGUID*){assert(false);}D2UnitStrc* SUNIT_GetServerUnit(D2GameStrc*,int,D2UnitGUID){assert(false);return nullptr;}
int D2Common_10226(D2UnitStrc*p,int flags){assert(flags==1024);moves++;if(moveResult==1)p->x++;return moveResult==1;}
bool UNITS_IsCurrentRoomInvalid(D2UnitStrc*){return invalidRoom;}void sub_6FCBB190(D2GameStrc*,D2UnitStrc*,int){rebinds++;}
'''
 # GetElapsed and movement wrappers precede do15/16; hit29 prototype above allows router table binding.
 for n in ('MISSILE_GetRemainingFrames','sub_6FCBC7E0','sub_6FCBC930','MISSMODE_SrvDo01_BasicMissile','MISSMODE_SrvDo15_FrozenOrb','MISSMODE_SrvDo16_FrozenOrbNova','MISSMODE_SrvHit29_FrozenOrb'):cpp+='\n'+blocks[n]+'\n'
 cpp+=r'''
void Reset(int kind,int remaining){owner={};parent={};target={};other={};owner.dwUnitType=0;owner.dwUnitId=77;target.dwUnitType=other.dwUnitType=1;other.dwUnitId=20;parent.dwClassId=260+kind;parent.dwMissileMode=3;parent.path.velocity=kind==0?1920:kind==1?3456:4608;parent.path.flags=64;parent.pDynamicPath=&parent.path;parent.pMissileData=&parent.md;parent.targetX=kind==2?30:0;parent.targetY=0;parent.skill=64;parent.rank=20;parent.frame=remaining;parent.md.nTotalFrames=kind==0?30:25;parent.md.nCurrentFrame=remaining;parent.activation=parent.md.nTotalFrames;row={};row.nAlwaysExplode=0;row.nCollideKill=kind!=0;row.wSrvHitFunc=kind==0?29:0;row.wSrvDmgFunc=0;row.wResultFlags=4;row.dwHitFlags=0;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE];row.dwParam[0]=kind==2?6:1;row.dwParam[1]=kind==2?2:19;hasOwner=hasRoom=hasRecord=factory=true;worldMask=0;moveResult=1;creates=fills=flags=finds=frameWrites=ar=moves=rebinds=pathCalls=0;invalidRoom=false;requests.clear();victims={&target,&other};stru_6FD2E5F8[3]={MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster,0x184};}
int Run(int kind){return kind==0?MISSMODE_SrvDo15_FrozenOrb(&game,&parent):kind==1?MISSMODE_SrvDo01_BasicMissile(&game,&parent):MISSMODE_SrvDo16_FrozenOrbNova(&game,&parent);}
void PrintRequests(){printf("[");for(size_t i=0;i<requests.size();i++){auto&r=requests[i];printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",r.p.dwFlags,r.p.nMissile,r.x,r.y,r.p.nTargetX,r.p.nTargetY,r.p.nSkill,r.p.nSkillLevel);}printf("]");}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("{\"contexts\":[");bool first=true;
for(int kind=0;kind<3;kind++)for(int r:{0,1,2,-1})for(int moved:{1,2})for(int path:{0,1,4,64})for(int room:{0,1})for(int mask:{0,4,256,260})for(int last:{-1,10,20})for(int valid:{0,3})for(int badRoom:{0,1}){
 int remaining=r<0?(kind==0?30:25):r;Reset(kind,remaining);parent.path.flags=path;hasRoom=room;worldMask=mask;parent.md.nOwnerType=1;parent.md.dwOwnerGUID=last;target.dwFlags=valid;moveResult=moved;invalidRoom=badRoom;
 int result=Run(kind);printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],",first?"":",",kind,remaining,moved,path,room,mask,last,valid,badRoom,result,parent.frame,moves,finds,fills,flags,parent.md.dwOwnerGUID,frameWrites,parent.x,parent.targetX,parent.targetY,rebinds,pathCalls);first=false;PrintRequests();printf("]");
}printf("],\"lifecycle\":[");Reset(0,30);first=true;for(int i=0;i<30;i++){int before=requests.size();int result=Run(0);printf("%s[%d,%d,%d,%d,%d,%d]",first?"":",",i,result,parent.frame,parent.x,parent.targetX,(int)requests.size()-before);first=false;}printf("]}\n");}
'''
 for n,b in blocks.items():assert function(cpp,n)==b,n
 for n,h in prior['routineHashes'].items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==h,n
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases['contexts'])==9216 and len(cases['lifecycle'])==30
 # Canonical 64-way table values are explicit inputs; source control flow and ordering execute above.
 source=(ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/MISSILES/MissMode.cpp').read_text();orb=blocks['MISSMODE_SrvDo15_FrozenOrb'];xs=[int(x) for x in re.search(r'xPositions\[\].*?\{([^}]+)',orb,re.S)[1].split(',') if x.strip()];ys=[int(x) for x in re.search(r'yPositions\[\].*?\{([^}]+)',orb,re.S)[1].split(',') if x.strip()]
 for inputs,out,requests in cases['contexts']:
  kind,remaining,moved,path,room,mask,last,valid,badRoom=inputs;early=moved==2;after=remaining if early else remaining-1;expired=not early and after<=0;find=not early and not expired and room and not(path&5) and path!=0 and (mask&0x184)!=0
  selected=10 if find and valid==3 and last!=10 else 20 if find and last!=20 else -1
  terminal=early or expired or find and selected<0 and mask&4;result=2 if terminal or not early and not expired and (not room or path&5) or kind!=0 and selected>=0 else 1
  position=40+int(not early);trailIndex=19 if kind==0 else 0;vx=15 if kind==2 and remaining==25 else 30 if kind==2 else trailIndex;vy=15 if kind==2 and remaining==25 else 0
  expected=[result,after,1,int(bool(find)),int(selected>=0),int(selected>=0),selected if selected>=0 else last,int(not early),position,vx,vy,int(badRoom),int(kind==2 and remaining==25)]
  plans=[]
  if kind==0:plans.append([2,261,40,70,30,0,64,20])
  if kind==0 and terminal and after==0:plans.extend([2,262,position,70,xs[i],ys[i],64,20] for i in range(0,64,4))
  assert out==expected and requests==plans,(inputs,out,requests,expected,plans)
 for i,result,remaining,x,index,created in cases['lifecycle']:assert (result,remaining,x,index,created)==(2 if i==29 else 1,29-i,41+i,(19*(i+1))%64,17 if i==29 else 1)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n');pin=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';manifest=json.loads((pin.parent.parent/'sources.json').read_text());assert sha(pin)==manifest['files']['json/missiles.json']['sha256']
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=9246,sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},reusedRoutineHashes=prior['routineHashes'],currentTable={str(pin.relative_to(ROOT)):sha(pin)},sanitizers='ASan/UBSan clean',
 established=['Complete do15/do16/do1,elapsed getter,source movement/target-reconciliation wrappers and the retained generic collision/finder/router/LastCollide bodies compose. Trail emission precedes attempted movement even on early path failure;ordinary movement precedes decrement/contact. Zero remaining alone permits hit29terminal requests;positive or negative remaining ending paths do not.',
 'The30-call root lifecycle attempts30trail requests from its previous positions and16terminal requests from the final moved position. Native next-step/recovery must not move before trail emission or burst from the preceding cell. Complete mode3 finder rejects most-recent/invalid targets before selection;root contacts retain it with zero-packet fill/flag services,while child CollideKill contacts remove the child. Barrier removal occurs only after no eligible recipient is found.',
 'Do16turns saved vectors/recalculates its target before movement;ordinary do1does not emit. Failed movement avoids decrement;invalid-room reconciliation still runs. Empty/blocked/path/room/expired/contact/history cases and the exact terminal coordinate are controlled separately.'],
 declaredServices=['Older engine,not modern executable proof. Existing Blizzard ABI and six retained canonical bodies are unchanged. The complete source movement wrapper calls declared path advancement,target lookup and room-rebind services. Path movement is a supplied one-X-step,not a D2 vector/cell normalization proof. Actual factory and per-child stored vectors are established by the separate full factory/emission composition;this composition records child requests.',
 'Candidate enumeration/footprints/collision masks/room/path flags and damage-fill/flag/commit services remain explicit. Full current global/animation RNG,InitSteps/activation,zero-damage root event subscribers,cold/HP/return/block/PvP/native input/save/client and assets remain separate. No C#/runtime data/install/save mutation.'],
 artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
 out=ROOT/'tools/reference/frozen-orb-collision-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Frozen Orb collision:9216complete update/movement/finder/router contexts+30call lifecycle;30trail/16terminal ordering;ASan/UBSan clean;world and damage services explicit.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
