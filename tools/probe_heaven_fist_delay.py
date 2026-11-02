#!/usr/bin/env python3
"""Execute complete older FoH hit22 and capped callback with declared services."""
import hashlib,json,subprocess
from pathlib import Path
from probe_blessed_hammer_damage import routine
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/heaven-fist-contact/delay-source'
def main():
 rel='source/D2Game/src/MISSILES/MissMode.cpp';p=ROOT/'.local/class-skills/MissMode.cpp.reference';data=p.read_bytes();tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel)
 names=['MISSMODE_FistOfTheHeavensDelay_AuraCallback','MISSMODE_SrvHit22_FistOfTheHeavensDelay'];bodies=[routine(data.decode(),n) for n in names]
 cpp=r'''#include <cstdint>
#include <cstdio>
#include <algorithm>
#include <initializer_list>
#include <vector>
#define __fastcall
constexpr int UNIT_MONSTER=1,STAT_ITEM_DAMAGETARGETAC=1,STAT_ARMORCLASS=2;
struct D2GameStrc{};struct D2UnitStrc{int dwClassId=1,dwUnitType=1,x=130,y=240;};
struct D2MissilesTxt{int wHitSubMissile[1]={998},dwHitPar[2]={0,0};}row;
struct D2SkillsTxt{int dwAuraRangeCalc=1,dwCalc[4]={0,0,0,2},dwAuraFilter=0;}skill;
struct D2DamageStrc{int lightning=0;};
struct D2MissileStrc{int dwFlags=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;int nSkill=0,nSkillLevel=0,nMissile=0,nTargetX=0,nTargetY=0;};
struct D2HitFunc22ParamStrc{D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;int nSkillId=0,nLevel=0,nParam=0,nHitSubMissile=0;};
struct D2AuraCallbackStrc{void* pArgs;int nCounter;D2GameStrc* pGame;};
D2UnitStrc target,owner,missile;bool hasRow=true,hasOwner=true,hasSkill=true,hasTarget=true;int formula=9,armor=100,armorDelta=0,hireling=0,fills=0,flags=0,searches=0,children=0,rangeSeen=0,filterSeen=0,originX=0,originY=0,capSeen=0;std::vector<int> trace;
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return hasRow?&row:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasSkill?&skill:nullptr;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return hasOwner?&owner:nullptr;}
D2UnitStrc* SUNIT_GetServerUnit(D2GameStrc*,int,int){return hasTarget?&target:nullptr;}
int MISSILE_GetSkill(D2UnitStrc*){return 121;}int MISSILE_GetLevel(D2UnitStrc*){return 7;}int MISSILE_GetTargetX(D2UnitStrc*){return 1;}int MISSILE_GetTargetY(D2UnitStrc*){return 77;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int id,int,int){return formula+(id==2?2:0);}
void MISSMODE_FillDamageParams(D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p){fills++;trace.push_back(1);p->lightning=25600;}
void MISSMODE_SetDamageFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){flags++;trace.push_back(2);}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return hireling;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int id,int){return id==STAT_ITEM_DAMAGETARGETAC?armorDelta:armor;}
void STATLIST_SetUnitStat(D2UnitStrc*,int,int n,int){armor=n;trace.push_back(3);}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){children++;trace.push_back(5);if(p->dwFlags!=32||p->pOwner!=&owner||p->pOrigin!=&missile||p->nSkill!=121||p->nSkillLevel!=7||p->nMissile!=998)__builtin_trap();return &missile;}
void sub_6FD0FE80(D2GameStrc* game,D2UnitStrc*,int x,int y,int radius,int filter,int(*callback)(D2AuraCallbackStrc*,D2UnitStrc*),void* args,int,const char*,int){searches++;trace.push_back(4);originX=x;originY=y;rangeSeen=radius;filterSeen=filter;capSeen=((D2HitFunc22ParamStrc*)args)->nParam;D2AuraCallbackStrc cb{args,0,game};for(int i=0;i<6;i++){if(callback(&cb,&target)){cb.nCounter++;}}}
'''+''.join(bodies)+r'''
int main(){D2GameStrc game;bool first=true;printf("[");
for(int guard=0;guard<7;guard++)for(int range:{-1,0,7})for(int cap:{-1,0,3})for(int calc:{-5,9})for(int filter:{0,0x1234})for(int delta:{-150,0,25})for(int hire:{0,1}){
 hasRow=hasOwner=hasSkill=hasTarget=true;row={};skill={};missile={};missile.x=100;missile.y=200;armor=100;armorDelta=delta;hireling=hire;formula=calc;row.dwHitPar[0]=range;row.dwHitPar[1]=cap;skill.dwAuraFilter=filter;fills=flags=searches=children=rangeSeen=filterSeen=originX=originY=capSeen=0;trace.clear();
 if(guard==2)hasRow=false;if(guard==3)row.wHitSubMissile[0]=-1;if(guard==4)hasOwner=false;if(guard==5)hasSkill=false;if(guard==6)hasTarget=false;
 int result=MISSMODE_SrvHit22_FistOfTheHeavensDelay(&game,guard==1?nullptr:&missile,nullptr);
 printf("%s[[%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],[",first?"":",",guard,range,cap,calc,filter,delta,hire,result,fills,flags,searches,children,armor,rangeSeen,filterSeen,originX,originY,capSeen);for(size_t i=0;i<trace.size();i++)printf("%s%d",i?",":"",trace[i]);printf("]]");first=false;
}printf("]\n");}
'''
 STAGE.mkdir(parents=True,exist_ok=True);path=STAGE/'heaven-fist-delay.cpp';path.write_text(cpp);binary=path.with_suffix('')
 subprocess.run(['g++','-std=c++17','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr
 rows=json.loads(result.stdout);assert len(rows)==1512
 for inputs,out,trace in rows:
  guard,radius,cap,calc,filter,delta,hire=inputs;good=guard==0;r=radius if radius>0 else max(1,calc);c=cap if cap>0 else max(1,calc+2);n=min(6,c)
  assert out==([1,1,1,1,n,max(0,100+delta) if delta and not hire else 100,r,filter or 0xA683,100,200,c] if good else [0 if guard==6 else 1,0,0,0,0,100,0,0,0,0,0]),(inputs,out)
  assert trace==([1,2]+([3] if delta and not hire else [])+[4]+[5]*n if good else []),(inputs,trace)
 out=STAGE/'cases.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',sourceCommit=tree['sha'],completeFunctions=names,sourceHashes={str(p.relative_to(ROOT)):hashlib.sha256(data).hexdigest()},cases=1512,
  established=['Complete hit22guards preserve target-missing return0versus other refusal return1. Positive row radius/cap override formulas;otherwise each formula clamps to at least1. Zero aura filter selects0xA683.','Fill then flags/damage service precedes optional nonhireling monster armor change,then original delay-origin search and capped child callbacks. Positive/negative armor changes clamp at0. Search origin stays100/200while the resolved target is130/240.'],
  declaredServices=['Fill/SetDamageFlags are declared trace services,not executed damage/HP pipelines. The search service supplies six ordered candidates and increments counter for callback return1. This does not prove native search order or current engine behavior.','Skill121/rank7,formula IDs and child998are controlled inputs. Nested table/stat/target/factory services and owner/room/element/potion/seed/event/lifecycle contracts remain separately reviewed.'],
  artifacts={str(q.relative_to(ROOT)):hashlib.sha256(q.read_bytes()).hexdigest() for q in (Path(__file__).resolve(),ROOT/'tools/probe_blessed_hammer_damage.py',path,binary,out,STAGE/'sanitizer.log')})
 (ROOT/'tools/reference/heaven-fist-delay-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
 print('FoH complete hit22/callback:1512guard/range/cap/filter/armor/order cases pass ASan/UBSan;damage/search remain declared nested services.')
if __name__=='__main__':main()
