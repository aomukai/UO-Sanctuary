#!/usr/bin/env python3
"""Compile full Strafe start8/do12 and shared count/ID-selection/init bodies.

Inventory validation/quantity, aura eligibility, formula, missile factory and
rollback scheduler are explicit services. This is not a full Strafe handler.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
hashes={};blocks={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(i['sha'] for i in tree['tree'] if i['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();text=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
 body=text[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
rawPath=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';manifest=json.loads((rawPath.parent.parent/'sources.json').read_text());assert hashlib.sha256(rawPath.read_bytes()).hexdigest()==manifest['files']['json/skills.json']['sha256']
skills=json.loads(rawPath.read_text());row=next(v for v in skills.values() if v.get('*Id')==26);assert row['*Id']==26 and row['SrcDam']==128 and row['Param6']==50
assert row['calc1']=='"min(par3 + lvl - 1, par4)"' and row['calc3']=='2+lvl/4'
assert row['calc2']=="ln12 + (skill('Guided Arrow'.blvl)*par8) + (skill('Multiple Shot'.blvl)*par9)"
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr int UNIT_MONSTER=1,STAT_DAMAGEPERCENT=17;
struct D2SkillsTxt{int dwAuraRangeCalc=3,dwCalc[4]={0,1,2,3},dwParam[8]={0,0,0,0,0,50},wSrvMissileA=682,wSrvMissileB=683;};
struct D2SkillStrc{int p[3]={};};struct D2UnitStrc{int dwUnitType=1,dwUnitId=0,x=0,y=0,damagePercent=0;bool eligible=true;};struct D2GameStrc{};
struct D2AuraCallbackStrc{void* pArgs;};struct D2_6FD10880_Strc{D2UnitStrc* unk0x00=nullptr;uint32_t unk0x04=0;D2UnitStrc* unk0x08=nullptr;uint32_t unk0x0C=0,unk0x10=0;int unk0x14=0;};
struct D2MissileStrc{unsigned dwFlags=0;D2UnitStrc* pOwner=nullptr;int nX=0,nY=0,nSkillLevel=0,nTargetX=0,nTargetY=0,nSkill=0,nMissile=0,pInitArgs=0;void(*pInitFunc)(D2UnitStrc*,int)=nullptr;};
struct Tables{int nMissilesTxtRecordCount=1000;};Tables tables;Tables* sgptDataTables=&tables;
std::vector<D2UnitStrc*> candidates,lookup;D2SkillsTxt row;D2SkillStrc used;D2UnitStrc* selected=nullptr;D2UnitStrc missile;
bool hasRow=true,hasSkill=true,ammo=true,factory=true;int ammoChecks=0,quantity=0,fallbacks=0,weaponClass=1,created=0,emittedTarget=-1,rollbacks=0,lastRollback=-1,hardGuided=1,hardMulti=1;
D2MissileStrc captured;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return hasSkill?&used:nullptr;}
int sub_6FD119C0(D2UnitStrc*){ammoChecks++;return ammo;}void sub_6FC80A30(D2GameStrc*,D2UnitStrc*){fallbacks++;}void sub_6FC80E10(D2GameStrc*,D2UnitStrc*){fallbacks++;}
int sub_6FD118C0(D2GameStrc*,D2UnitStrc*){quantity++;return 1;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected;}
void SKILLS_SetParam1(D2SkillStrc* s,int v){s->p[0]=v;}void SKILLS_SetParam2(D2SkillStrc* s,int v){s->p[1]=v;}void SKILLS_SetParam3(D2SkillStrc* s,int v){s->p[2]=v;}
int SKILLS_GetParam1(D2SkillStrc* s){return s->p[0];}int SKILLS_GetParam2(D2SkillStrc* s){return s->p[1];}int SKILLS_GetParam3(D2SkillStrc* s){return s->p[2];}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int level){return calc==0?std::min(4+level-1,10):calc==1?5*level+hardGuided*10+hardMulti*5:calc==2?2+level/4:35;}
void sub_6FD0FE80(D2GameStrc*,D2UnitStrc*,int,int,int,uint32_t,int(*fn)(D2AuraCallbackStrc*,D2UnitStrc*),void* data,int,const char*,int){D2AuraCallbackStrc cb{data};for(auto* u:candidates)if(u->eligible)fn(&cb,u);}
D2UnitStrc* SUNIT_GetServerUnit(D2GameStrc*,int type,int id){for(auto* u:lookup)if(u->dwUnitType==type&&u->dwUnitId==id)return u;return nullptr;}
void UNITS_SetTargetUnitForDynamicUnit(D2UnitStrc*,D2UnitStrc* u){selected=u;}
void D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){*x=selected->x;*y=selected->y;}
int CLIENTS_GetUnitX(D2UnitStrc* u){return u->x;}int CLIENTS_GetUnitY(D2UnitStrc* u){return u->y;}int UNITS_GetWeaponClass(D2UnitStrc*){return weaponClass;}
int STATLIST_UnitGetStatValue(D2UnitStrc* u,int,int){return u->damagePercent;}void STATLIST_SetUnitStat(D2UnitStrc* u,int,int v,int){u->damagePercent=v;}
void sub_6FD15080(D2UnitStrc*,int v){rollbacks++;lastRollback=v;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){created++;captured=*p;emittedTarget=selected->dwUnitId;if(!factory)return nullptr;missile.damagePercent=7;p->pInitFunc(&missile,p->pInitArgs);return &missile;}
'''
ama='source/D2Game/src/SKILLS/SkillAma.cpp';common='source/D2Game/src/SKILLS/Skills.cpp'
for rel,name in [(common,'sub_6FD107E0'),(common,'sub_6FD10790'),(common,'sub_6FD10880'),(common,'sub_6FD107F0'),(ama,'SKILLS_AddDamagePercentBonus'),(ama,'SKILLS_SrvSt08_Strafe'),(ama,'SKILLS_SrvDo012_Strafe')]:cpp+=routine(rel,name)+'\n'
cpp+=r'''
int main(){D2GameStrc game;D2UnitStrc owner,targets[20];for(int i=0;i<20;i++){targets[i].dwUnitId=10*(i+1);targets[i].x=100+i;targets[i].y=200-i;}bool first=true;std::printf("{\"start\":[");
for(int record:{0,1})for(int skill:{0,1})for(int ammunition:{0,1})for(int target:{0,1})for(int count:{0,1,2,5,10,13,20})for(int rank:{1,4,8,20}){
 hasRow=record;hasSkill=skill;ammo=ammunition;used={};row={};ammoChecks=quantity=fallbacks=0;selected=target?&targets[0]:nullptr;candidates.clear();for(int i=count-1;i>=0;i--)candidates.push_back(&targets[i]);int result=SKILLS_SrvSt08_Strafe(&game,&owner,26,rank);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",record,skill,ammunition,target,count,rank,result,used.p[0],used.p[1],used.p[2],ammoChecks,quantity,fallbacks);
}
std::printf("],\"do\":[");first=true;
for(int record:{0,1})for(int skill:{0,1})for(int count:{0,1,2,5})for(int cached:{0,1,2})for(int remaining:{0,1,2,10})for(int weapon:{1,2})for(int bad:{0,1})for(int make:{0,1}){
 hasRow=record;hasSkill=skill;factory=make;used={};used.p[0]=remaining;used.p[1]=1;used.p[2]=10;row={};if(bad)row.wSrvMissileA=row.wSrvMissileB=-1;weaponClass=weapon;ammoChecks=quantity=created=rollbacks=0;lastRollback=emittedTarget=-1;captured={};missile={};lookup.clear();candidates.clear();for(auto& u:targets)u.eligible=true;
 for(int i=count-1;i>=0;i--){candidates.push_back(&targets[i]);if(i || cached)lookup.push_back(&targets[i]);}if(cached==2)targets[0].eligible=false;int result=SKILLS_SrvDo012_Strafe(&game,&owner,26,20);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",record,skill,count,cached,remaining,weapon,bad,make,result,created,emittedTarget,used.p[0],used.p[2],rollbacks,lastRollback,captured.nMissile,captured.dwFlags,captured.pInitArgs,missile.damagePercent,quantity+ammoChecks);
}
std::printf("]}\n");}
'''
path=STAGE/'strafe-source-probe.cpp';path.write_text(cpp);binary=STAGE/'strafe-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data['start'])==448 and len(data['do'])==1536
for record,skill,ammunition,target,count,rank,result,left,kind,guid,checks,quantity,fallback in data['start']:
 valid=bool(record and skill);ok=valid and ammunition
 assert(result,checks,quantity,fallback)==(int(ok),int(valid),int(ok),2*int(valid and not ammunition))
 if ok:
  cap=min(4+rank-1,10);minimum=min(2+rank//4,cap);expected=min(cap,max(minimum,count if target else 0))
  assert(left,kind,guid)==(expected,1,10 if target or count else -1)
 else:assert(left,kind,guid)==(0,0,0)
for record,skill,count,cached,remaining,weapon,bad,make,result,created,hit,left,nxt,rollbacks,rollback,missile,flags,ed,missileEd,totalQuantityCalls in data['do']:
 valid=bool(record and skill and remaining);eligible=[10*(i+1) for i in range(count) if i or cached!=2];old=bool(count and cached)
 actual=10 if old else next((i for i in eligible if i>10),eligible[0] if eligible else -1)
 emitted=bool(valid and actual>=0 and not bad);assert created==int(emitted) and totalQuantityCalls==0
 if emitted:
  successor=next((i for i in eligible if i>actual),eligible[0] if eligible else -1);again=remaining>1 and successor>=0
  assert(result,hit,left,nxt,rollbacks,rollback,missile,flags,ed)==(int(remaining>1),actual,remaining-1,successor if again else 10,int(again),50 if again else -1,682 if weapon==1 else 683,0x21,115)
  assert missileEd==(122 if make else 0)
 else:assert result==int(valid and actual<0) and left==remaining and rollbacks==0
 # Factory failure is ignored by do12: count/selection/rollback still advance.

fixture=STAGE/'strafe-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',talentId='amazon.strafe',handlerEnabled=False,legacyCommit=tree['sha'],inputHashes={'tools/reference/d2r-named-equipment/json/skills.json':hashlib.sha256((ROOT/'tools/reference/d2r-named-equipment/json/skills.json').read_bytes()).hexdigest()},sharedQuantityEvidence='tools/reference/bow-projectile-engine-evidence.json',sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes=blocks,probe=dict(startCases=448,doCases=1536,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete start8 validates ammo once, invokes quantity service once at successful start even with no target, and stores target/count. Explicit selected targets count eligible recipients, clamped between the current2+rank/4 minimum and min(4+rank-1,10) cap.','The complete selector receives-1 for initial ground selection, represented unsigned in its callback. It returns the lowest eligible identity through the fallback branch without copying its count to the optional output; start8 ground mode retains minimum shots even with many eligible targets. This is supplementary source behavior requiring current-build corroboration.','Complete do12 reuses an existing cached identity without rerunning aura eligibility, otherwise selects a cyclic successor. No-target returns1 without decrement/rollback; zero remaining returns0. It emits factory flags0x21 arrow682/bolt683 with additive ED initializer, decrements count even when the declared missile factory fails, then requests50percent rollback only if shots remain and a successor exists. No per-do quantity validation/decrement call occurs.','The complete additive initializer adds skill ED to prior missile ED. Declared current formula with rank20/hard Guided1/Multi1 adds115percent; shared factory/stat transfer remains separate.'],limits=['Current raw table/formulas are pinned; older complete callbacks do not certify the modern executable. Aura range/filter/LOS/world eligibility, identity lifetime and target coordinates are explicit services. Cached dead/invalid entity lookup behavior needs caller-level source/current verification.','Inventory validation/quantity, formula interpreter, missile creator/stat capture/AR/Pierce/NHD, rollback scheduler and wrapper mana/event/animation lifecycle are declared services. Once-only quantity invocation at start does not yet certify full decrement/payment/cancellation behavior.','Ground-selected recipient count and source fallback behavior are explicit source/current questions. Full sequence cadence/lock/interrupt/catch-up and modern next-hit-delay scope must be established before enabling a native handler.'])
out=ROOT/'tools/reference/strafe-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Strafe evidence stale'
else:out.write_text(encoded)
print('Strafe:448 complete start/count and1536 do12 target/emission/rollback-request cases; full scheduler/resource chain pending.')
