#!/usr/bin/env python3
"""Complete Phoenix meteor center expiry, nested hit4/hit14 router and field producer."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_fire_blast_landing_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/MISSILES/MissMode.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);text=raw.decode();blocks={}
 def routine(name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;d=1
  while d:d+=(text[i]=='{')-(text[i]=='}');i+=1
  body=text[m.start():i];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
 base=(STAGE/'fire-blast-landing-source-probe.cpp').read_text().split('void reset(int kind)',1)[0]
 def replace(a,b):
  nonlocal base
  assert base.count(a)==1,a;base=base.replace(a,b)
 replace('wHitSubMissile[1]={386},dwHitPar[1]={0};}row;','wHitSubMissile[4]={-1,-1,-1,-1};uint32_t dwHitPar[2]={0,0};}row,meteorRow,fireRow;')
 replace('struct D2SkillsTxt{int dwAuraRangeCalc=5;}skill;','struct D2SkillsTxt{int dwParam[4]={0,0,30,15},dwAuraRangeCalc=5;}skill;')
 replace('nSkillLevel=0;D2UnitStrc* pOwner=nullptr;}captured;','nSkillLevel=0,nRange=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;}captured;')
 replace('D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return hasRecord?&row:nullptr;}D2MissilesTxt* DATATBLS_GetMissilesTxtRecord(int){return &row;}', 'D2MissilesTxt* Table(int id){return id==563?&meteorRow:id==567?&fireRow:&row;}D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int id){return hasRecord?Table(id):nullptr;}D2MissilesTxt* DATATBLS_GetMissilesTxtRecord(int id){return Table(id);}')
 replace('assert(id==251 && rank>0);','assert(id==280);')
 old='D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){creates++;captured=*p;child.x=p->nX;child.y=p->nY;child.skill=p->nSkill;child.rank=p->nSkillLevel;return factory?&child:nullptr;}'
 replace(old, r'''std::vector<std::vector<int>> emitted;int failure=0;D2UnitStrc fields[18];
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){bool ok=p->nMissile==563?failure!=1:failure!=2||emitted.size()%2==0;emitted.push_back({p->nMissile,p->nX,p->nY,p->dwFlags,p->nRange,p->nSkill,p->nSkillLevel,int(p->pOrigin!=nullptr),int(p->pOwner!=nullptr),int(ok)});creates++;captured=*p;if(!ok)return nullptr;auto* out=p->nMissile==563?&child:&fields[(emitted.size()-2)%18];*out={};out->dwClassId=p->nMissile;out->x=p->pOrigin?p->pOrigin->x:p->nX;out->y=p->pOrigin?p->pOrigin->y:p->nY;out->skill=p->nSkill;out->rank=p->nSkillLevel;return out;}''')
 for name in ['MISSMODE_SrvHit04_ExplodingArrow_FreezingArrow_RoyalStrikeMeteorCenter','MISSMODE_SrvHit14_MeteorCenter_CatapultMeteor_RoyalStrikeMeteor']:
  replace(f'int32_t {name}(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){{return 3;}}',f'int32_t {name}(D2GameStrc*,D2UnitStrc*,D2UnitStrc*);')
 base+='\nvoid MISSMODE_CreateMeteor_MoltenBoulderSubmissiles(D2GameStrc*,D2UnitStrc*,int32_t,int32_t,int32_t);\n'
 for name in ['MISSMODE_SrvHit04_ExplodingArrow_FreezingArrow_RoyalStrikeMeteorCenter','MISSMODE_SrvHit14_MeteorCenter_CatapultMeteor_RoyalStrikeMeteor','MISSMODE_CreateMeteor_MoltenBoulderSubmissiles']:base+=routine(name)+'\n'
 base+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool comma=false;printf("[");for(int frame:{1,2,59,60})for(int rank:{0,1,3,20,40,255})for(int own:{0,1})for(int fail:{0,1,2})for(int skillKnown:{0,1})for(int recordKnown:{1}){
owner={};owner.dwUnitType=0;parent={};parent.dwClassId=564;parent.skill=280;parent.rank=rank;parent.frame=frame;parent.path.velocity=0;parent.dwMissileMode=0;parent.pDynamicPath=&parent.path;parent.pMissileData=&parent.md;row={};row.wResultFlags=row.dwHitFlags=0;row.wSrvHitFunc=4;row.nAlwaysExplode=1;row.nCollideKill=0;row.wHitSubMissile[0]=563;row.dwHitPar[0]=1;meteorRow={};meteorRow.wSrvHitFunc=14;meteorRow.nAlwaysExplode=0;meteorRow.nCollideKill=1;meteorRow.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE];meteorRow.dwHitPar[0]=6;meteorRow.dwHitPar[1]=1;meteorRow.wHitSubMissile[0]=567;fireRow={};fireRow.wSrvHitFunc=0;fireRow.nAlwaysExplode=0;fireRow.nCollideKill=0;fireRow.wResultFlags=fireRow.dwHitFlags=0;hasOwner=own;hasRecord=recordKnown;hasSkill=skillKnown;hasRoom=true;failure=fail;creates=fills=flags=areas=events=commits=defenses=ar=0;areaRadius=filter=0;noAura=-1;emitted.clear();victims={&target,&other};resultFlags.clear();fireValues.clear();target={};other={};target.dwUnitType=other.dwUnitType=1;
int result=MISSMODE_SrvDo01_BasicMissile(&game,&parent);if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",frame,rank,own,fail,skillKnown,recordKnown,result,parent.frame,areas,areaRadius,filter,noAura);for(size_t i=0;i<emitted.size();i++){if(i)printf(",");printf("[");for(size_t j=0;j<emitted[i].size();j++)printf("%s%d",j?",":"",emitted[i][j]);printf("]");}printf("]]");}printf("]\n");}'''
 cpp=STAGE/'phoenix-meteor-source-probe.cpp';cpp.write_text(base);binary=cpp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(cpp),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==288
 x=[2,-2,0,0,-3,0,3,-1,1,-1,2,-4,-3,-1,0,1,3,4];y=[-2,-2,2,5,3,3,3,2,1,-1,-1,-2,-2,-3,-4,-3,-3,-2]
 for frame,rank,own,fail,skillKnown,recordKnown,result,remaining,areas,radius,filt,noAura,calls in rows:
  ending=frame==1 and recordKnown;active=ending and own and fail!=1 and skillKnown
  assert remaining==(frame-1 if recordKnown else frame)
  assert (areas,radius,filt,noAura)==((1,6,0x8583,0) if active else (0,0,0,-1))
  expected=[]
  if ending:expected.append([563,0,0,0,0,280,rank,1,own,int(fail!=1)])
  if active:
   length=30+(rank-1)*15 if rank>0 else 0
   for i in range(18):expected.append([567,40+x[i],70+y[i],0x8001 if length>0 else 1,length,280,rank,0,own,int(fail!=2 or (i+1)%2==0)])
  assert calls==expected,(frame,rank,own,fail,skillKnown,recordKnown,calls,expected)
 fixture=STAGE/'phoenix-meteor-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n');e=dict(referenceVersion='3.3.93847',talentId='assassin.phoenixstrike',installed=False,handlerEnabled=False,reusedCompleteComposition='tools/reference/fire-blast-landing-source-evidence.json',sourceHash=hashlib.sha256(raw).hexdigest(),compiledAdditionalRoutines=blocks,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(cpp.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Complete generic do1/collision/router decrements a stationary center and invokes its AlwaysExplode hit4only on expiry. Current Range60therefore expires after60updates;positive remaining frames do not create the damaging meteor.','Full hit4creates child563from the center origin with retained280/rank,then immediately calls its full damage router with ending1. Full hit14rolls one copied area packet at radius6/filter0x8583,bCheckMonAuraFlag0.','Full field producer emits18source567positions with skill/rank/owner and30+15*(rank-1)override,flags0x8001;rank0control uses default range/no override. Failed meteor allocation prevents area/fields;failed individual fields do not suppress later attempts.'],services=['Factory records parent-origin inheritance,exact request flags/coordinates/rank/range and controlled allocation failure;complete numeric creator/mastery/length/writer is separate1152-case evidence.','Copied area/filter/defense/health services inherit the bounded Fire Blast composition;actual geometry/PvP/NPC/life/procs are not executed here.','Current60frame constant and selected hit4/hit14table routes are supplied. Generic server frame scheduling,full owner/stat mutation and native warning/impact/field persistence remain separate.'],limits=['Malformed missing-table routing is excluded:the complete older generic router dereferences its table after the collision wrapper delegates a missing record. Valid current rows are supplied; native validation must guard absent/changed tables rather than port that crash.'],next='Use a saved60frame warning;create impact/field packets from retained rank and current owner stats at expiry,then verify actual source fields and native input/lifecycle. No learned handler enabled.')
 out=ROOT/'tools/reference/phoenix-meteor-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Phoenix meteor evidence stale'
 else:out.write_text(encoded)
 print('Phoenix meteor:288 complete expiry/hit4/hit14/field-producer contexts pass;native and factory/damage services remain scoped.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
