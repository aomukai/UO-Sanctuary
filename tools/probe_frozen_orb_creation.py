#!/usr/bin/env python3
"""Compose Frozen Orb generic skill wrapper/straight launcher/full factory and fresh data."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/frozen-orb-creation-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=ROOT/'tools/reference/meteor-creation-source-evidence.json';prior=json.loads(parent.read_text());original=ROOT/'.local/class-skills/meteor-creation-source/probe.cpp';retained=STAGE/'reused-meteor.cpp';expected=prior['artifacts'][str(original.relative_to(ROOT))]
 if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
 assert sha(retained)==expected;cpp=retained.read_text().split('void PrintRequests()',1)[0];cpp='#include <iterator>\n#include <cstring>\n'+cpp;tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit'];bodies={};sources={}
 routes={'source/D2Game/src/SKILLS/Skills.cpp':['D2GAME_SKILLS_Handler_6FD12BA0','sub_6FD11420'],'source/D2Game/src/MISSILES/Missiles.cpp':['MISSILES_CreateMissileFromParams','MISSILES_Initialize'],
 'source/D2Common/src/Units/Missile.cpp':['MISSILE_AllocMissileData','MISSILE_SetTargetX','MISSILE_GetTargetX','MISSILE_SetTargetY','MISSILE_GetTargetY']}
 for rel,names in routes.items():
  path=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=path.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);sources[rel]=sha(path)
  for name in names:bodies[name]=function(raw.decode(),name)
 assert function(cpp,'MISSILES_CreateMissileFromParams')==bodies['MISSILES_CreateMissileFromParams']
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
 replace('struct D2UnitStrc;struct D2DynamicPathStrc', 'struct D2MissileDataStrc{unsigned fFlags=0,dwOwnerGUID=0;struct{int nX=0,nY=0;}pTargetCoords;};\nstruct D2UnitStrc;struct D2DynamicPathStrc')
 replace('int dwUnitType=0,dwClassId=86,', 'void* pMemoryPool=nullptr;D2MissileDataStrc* pMissileData=nullptr;int dwSeqFrame=0;int dwUnitType=0,dwClassId=86,')
 replace('int wSrvMissileA=86,wSrvMissileB=86;','int wSrvMissileA=86,wSrvMissileB=86,dwFlags[2]={},wSrvDoFunc=0,wItemEffect=1,wSrvMissile=260,wSrvStartFunc=0,dwDelay=0,nAnim=0;')
 replace('int nMissilesTxtRecordCount=1000;', 'int nMissilesTxtRecordCount=1000;D2MissilesTxt pMissilesTxt[1000];')
 replace('void MISSILE_SetTargetX(D2UnitStrc* u,int v){u->mode=v;}int MISSILE_GetTargetX(D2UnitStrc* u){return u->mode;}', 'void __stdcall MISSILE_SetTargetX(D2UnitStrc*,int);int __stdcall MISSILE_GetTargetX(D2UnitStrc*);')
 replace('void MISSILE_SetTargetY(D2UnitStrc* u,int v){u->packed=v;}int MISSILE_GetTargetY(D2UnitStrc* u){return u->packed;}', 'void __stdcall MISSILE_SetTargetY(D2UnitStrc*,int);int __stdcall MISSILE_GetTargetY(D2UnitStrc*);')
 replace('int,int,int){creates++;if(!allocated)return nullptr;created={};created.pDynamicPath=&created.path;', 'int,int mode,int){creates++;if(!allocated)return nullptr;if(created.pMissileData){delete created.pMissileData;created.pMissileData=nullptr;}created={};created.mode=mode;created.pDynamicPath=&created.path;')
 replace('created.y=y;return &created;}', 'created.y=y;MISSILES_Initialize(&game,&created,123);return &created;}')
 at=cpp.index('D2UnitStrc* SUNIT_AllocUnitData(');cpp=cpp[:at]+'void __fastcall MISSILES_Initialize(D2GameStrc*,D2UnitStrc*,int);\n'+cpp[at:]
 replace('row.wRange=60;row.nVel=row.nMaxVel=0;row.nCollideType=0;row.dwMissileFlags=0;row.dwDamageRate=0;', 'row.wRange=30;row.nVel=row.nMaxVel=10;row.nCollideType=3;row.dwMissileFlags=0;row.dwDamageRate=0;')
 cpp+=r'''
constexpr int D2UnitInvalidGUID=-1,EVENTTYPE_MODECHANGE=0,SKILLSFLAGINDEX_INTOWN=0,SKILLSFLAGINDEX_LOB=1,SKILLSFLAGINDEX_DECQUANT=2,SKILLSFLAGINDEX2_USEMANAONDO=3,PLRMODE_TOWNNEUTRAL=0,PLRMODE_SEQUENCE=1;
#define D2_ALLOC_STRC_POOL(pool,T) new T
void D2GAME_DeleteTimersOnUnit_6FC34A30(D2GameStrc*,D2UnitStrc*){}void sub_6FC35570(D2GameStrc*,D2UnitStrc*,int,int,int){}
struct D2SkillStrc{int id=64,rank=1,flags=0;}used;bool hasUsed=true,hasHighest=true;int payments=0,delayCalls=0,delayValue=0,quantities=0;
using SkillDoFunc=int(*)(D2GameStrc*,D2UnitStrc*,int,int);SkillDoFunc gpSkillSrvDoFnTable_6FD40A20[1]={nullptr},gpSkillSrvStartFnTable_6FD408B0[1]={nullptr};
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return hasUsed?&used:nullptr;}int SKILLS_GetFlags(D2SkillStrc* s){return s->flags;}
void UNITS_SetUsedSkill(D2UnitStrc*,D2SkillStrc*){}void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int){}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc* s,const char*,int){return s->id;}int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc* s,int){return s->rank;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return hasHighest?&used:nullptr;}bool sub_6FD10CE0(D2UnitStrc*){return true;}
int sub_6FD118C0(D2GameStrc*,D2UnitStrc*){quantities++;return 1;}int D2GAME_SKILLMANA_Consume_6FD10A50(D2GameStrc*,D2UnitStrc*,int,int){payments++;return 1;}
void D2GAME_SKILLS_SetDelay_6FD11C00(D2GameStrc*,D2UnitStrc*,int frames){delayCalls++;delayValue=frames;}
D2UnitStrc* sub_6FD11710(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){std::abort();}
'''
 for name in ['MISSILE_AllocMissileData','MISSILE_SetTargetX','MISSILE_GetTargetX','MISSILE_SetTargetY','MISSILE_GetTargetY','MISSILES_Initialize','sub_6FD11420','D2GAME_SKILLS_Handler_6FD12BA0']:cpp+='\n'+bodies[name]+'\n'
 cpp+=r'''
void PrintRequests(){printf("[");for(size_t i=0;i<requests.size();i++){auto&o=requests[i];auto&p=o.request;printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",p.dwFlags,p.nMissile,p.nX,p.nY,p.nTargetX,p.nTargetY,p.nSkill,p.nSkillLevel,o.made,o.x,o.y,o.tx,o.ty,o.velocity,o.frames,o.rank);}printf("]");}
int main(){for(int n=0;n<32;n++)gdwBitMasks[n]=1u<<n;printf("[");bool first=true;int positions[6][2]={{50,70},{40,70},{40,90},{-5,70},{0,70},{50,0}};
for(int guard=0;guard<10;guard++)for(int w=0;w<4;w++)for(int rank:{1,20,255})for(int select:{0,1})for(int point=0;point<6;point++)for(int pay:{0,1})for(int kind:{0,1}){
 if(created.pMissileData){delete created.pMissileData;created.pMissileData=nullptr;}owner={};target={};created={};row={};skill={};used={};used.rank=rank;requests.clear();factoryCalls=creates=payments=delayCalls=delayValue=quantities=0;calc=25;world=w;
 owner.dwUnitType=kind;owner.dwUnitId=77;owner.dwFlags=0x100000;owner.x=40;owner.y=70;owner.path.x=positions[point][0];owner.path.y=positions[point][1];owner.pDynamicPath=&owner.path;target.x=positions[point][0]+3;target.y=positions[point][1]+7;selected=select?&target:nullptr;
 hasSkill=guard!=1;hasUsed=hasHighest=guard!=6;hasRecord=hasOwner=allocated=true;hasRoom=guard!=5;town=guard==4;skill.wSrvMissile=guard==7?-1:guard==8?1000:260;skill.wSrvDoFunc=guard==9?-1:0;
 int result=D2GAME_SKILLS_Handler_6FD12BA0(guard==2?nullptr:&game,guard==3?nullptr:&owner,64,rank,pay,0,0);bool made=!requests.empty()&&requests[0].made;
 printf("%s[[%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],",first?"":",",guard,w,rank,select,point,pay,kind,result,owner.dwFlags,factoryCalls,creates,payments,delayCalls,delayValue,made?MISSILE_GetTargetX(&created):-999,made?MISSILE_GetTargetY(&created):-999,made?created.mode:-999,quantities);first=false;PrintRequests();printf("]");
}if(created.pMissileData)delete created.pMissileData;printf("]\n");}
'''
 for name,body in bodies.items():assert function(cpp,name)==body,name
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==5760
 for inputs,out,requests in cases:
  guard,w,rank,selected,point,pay,kind=inputs;xy=[(50,70),(40,70),(40,90),(-5,70),(0,70),(50,0)][point];x,y=xy
  if selected:x+=3;y+=7
  admitted=guard==0;requested=admitted and x!=0 and y!=0;made=requested and w==0;tx,ty=(41,71) if (x,y)==(40,70) else(x,y)
  expected=[[33,260,40,70,x,y,64,rank,int(made),*([40,70,tx,ty,1920,30,rank] if made else [0]*7)]] if requested else []
  assert out==[int(admitted),0x100000|(64 if admitted else 0),int(requested),int(requested and w not in(1,2)),int(admitted and pay),int(admitted and pay and kind==0),25 if admitted and pay and kind==0 else 0,0 if made else -999,0 if made else -999,3 if made else -999,0] and requests==expected,(inputs,out,requests,expected)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n');tables={};pins={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
 for name in ('skills','missiles'):
  path=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(path)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(path.relative_to(ROOT))]=sha(path);tables[name]=json.loads(path.read_text())
 skill=next(r for r in tables['skills'].values() if r.get('skill')=='Frozen Orb');row=tables['missiles']['frozenorb'];assert skill['*Id']==64 and skill['srvmissile']=='frozenorb' and not skill.get('srvdofunc',0) and not skill.get('srvstfunc',0) and skill['localdelay']==25 and row['Vel']==10 and row['Range']==30 and row['CollideType']==3
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},currentTable=pins,currentRows={'skill':skill,'root':row},sanitizers='ASan/UBSan clean',
 established=['Complete generic handler,straight launcher,full factory,missile initializer/data allocator and target-coordinate accessors compose. Ordinary selected-target aim precedes path aim;launcher refuses a zero axis before allocation. Admitted generic missile rows return1and invoke once-only requested mana/delay services even when launcher/factory fails. Invalid context/row/do-index/town/no-room/missing skill prevents the request and payment.',
 'Factory requests flags33/root260/skill64/retained rank at caster coordinates,velocity1920and30frames. The complete allocator zeroes stored target coordinates and sets invalid owner GUID;the ordinary factory/launcher does not replace that index/vector. This establishes initial stored(0,0)under declared ordinary allocation services,independent of path aim. A same-point moving aim receives the full factory(+1,+1)nudge.'],
 declaredServices=['Older engine,not modern executable proof. Reused exact Meteor full-factory scaffold;skill/room/target/path/row/allocation/timer/mana-count/delay/stat-writing and world services are declared. Positive/negative coordinates are controlled services,not native map admission proof. Full unit allocation/initialization is represented by the complete missile initializer and data allocator with supplied storage/timer services.',
 'Source skill/missile flag-index shims supply logical current booleans;no lob/usemanaondo/quantity/start/do callback is selected. Payment helper arithmetic,actual initial path InitSteps/global seeds/stat capture/Slow Missiles,do15/do16/hit29/router/HP/cold/event/client/save and native learned acceptance remain separate. Failed factory payment is older-source evidence requiring modern corroboration.'],
 artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
 out=ROOT/'tools/reference/frozen-orb-creation-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Frozen Orb creation:5760complete generic wrapper/launcher/factory/initializer/data cases;initial stored target(0,0),conditional once-only payment even on factory failure;ASan/UBSan clean;runtime unchanged.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
