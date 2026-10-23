#!/usr/bin/env python3
"""Compile pinned Lightning Strike start/emission and shared hit12; retain reviewed factory limits."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'

def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 hashes={};blocks={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
  blocks[name]=text[m.start():e];return blocks[name]+'\n'
 ama=source('source/D2Game/src/SKILLS/SkillAma.cpp');miss=source('source/D2Game/src/MISSILES/MissMode.cpp');common=source('source/D2Common/src/Units/Missile.cpp');source('source/D2Game/src/MISSILES/Missiles.cpp')
 cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <initializer_list>
#include <vector>
#define __fastcall
#define __stdcall
constexpr int DAMAGERESULTFLAG_SUCCESSFULHIT=1;
struct D2UnitStrc{int x=0,y=0,life=100,dwClassId=34,dwUnitId=10,remaining=0,pSeed=0,skill=34,level=17;} owner,target,secondary,bolt;
struct D2GameStrc{} game;
struct D2SkillStrc{} used;
struct D2SkillsTxt{int dwCalc[4]={0,1,2,3},nEType=3,nSrcDam=0,dwAuraRangeCalc=4;} row;
struct D2MissilesTxt{int dwHitPar[1]={0};} missileRow;
struct D2DamageStrc{int wResultFlags=0,dwLtngDamage=0,dwEnDmgPct=0,dwConvPct=0,nConvType=0;} saved;
struct D2MissileStrc{int dwFlags=0,nMissile=0,nX=0,nY=0,nTargetX=0,nTargetY=0,nSkill=0,nSkillLevel=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;D2UnitStrc* pTarget=nullptr;};
struct Tables{int nMissilesTxtRecordCount=1000;} tables;auto* sgptDataTables=&tables;
bool record=true,skill=true,chosen=true,hit=true,found=true,created=true,moved=false,killed=false,projectile=true,projectileRow=true,hasOwner=true,same=false;
int missileId=232,allocs=0,drains=0,searches=0,creates=0,sets=0,ar=-1,src=-1,filter=-1,range=-1,lastRemaining=-1;
std::vector<D2MissileStrc> packets;std::vector<int> order;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return record?&row:nullptr;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return skill?&used:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return chosen?&target:nullptr;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int value,int){ar=value;order.push_back(1);return hit?1:0;}
int SKILLS_GetMinElemDamage(D2UnitStrc*,int,int,int){order.push_back(2);return 256;}
int SKILLS_GetMaxElemDamage(D2UnitStrc*,int,int,int){order.push_back(3);return 6400;}
int ITEMS_RollLimitedRandomNumber(int*,int){order.push_back(4);return 257;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int field,int,int rank){return field==0?20:field==1?rank+1:field==4?20:0;}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p,int value){order.push_back(5);allocs++;saved=*p;src=value;}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc* p,int){order.push_back(6);drains++;if(moved){p->x+=10;p->y-=6;}if(killed)p->life=0;}
D2UnitStrc* sub_6FD107F0(D2GameStrc*,D2UnitStrc*,int,int,int radius,unsigned flags,int,int){order.push_back(7);searches++;range=radius;filter=flags;return found?(same?&target:&secondary):nullptr;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}
int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int){return missileId;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){order.push_back(8);creates++;packets.push_back(*p);return created?&bolt:nullptr;}
void MISSILE_SetTargetX(D2UnitStrc*,int value){sets++;lastRemaining=value;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return projectileRow?&missileRow:nullptr;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return hasOwner?&owner:nullptr;}
int MISSILE_GetTargetX(D2UnitStrc* p){return p->remaining;}
int MISSILE_GetSkill(D2UnitStrc* p){return p->skill;}
int MISSILE_GetLevel(D2UnitStrc* p){return p->level;}
void reset(){allocs=drains=searches=creates=sets=0;ar=src=filter=range=lastRemaining=-1;saved={};packets.clear();order.clear();owner={};target={};secondary={};bolt={};target.x=34;target.y=46;secondary.x=70;secondary.y=86;bolt.x=66;bolt.y=82;}
void packet(){std::printf("[");for(size_t i=0;i<packets.size();i++){auto& p=packets[i];if(i)std::printf(",");std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",p.dwFlags,p.nMissile,p.pOwner==&owner,p.pOrigin==&target,p.pTarget==&secondary,p.nX,p.nY,p.nTargetX,p.nTargetY,p.nSkill,p.nSkillLevel);}std::printf("]");}
'''
 for text,name in [(ama,'SKILLS_SrvSt10_LightningStrike'),(ama,'SKILLS_SrvDo014_LightningStrike'),(miss,'MISSMODE_SrvHit12_ChainLightning_LightningStrike')]:cpp+=routine(text,name)
 cpp+=r'''int main(){bool first=true;std::printf("{\"flow\":[");
for(int rec:{0,1})for(int used:{0,1})for(int sel:{0,1})for(int success:{0,1})for(int exists:{0,1})for(int mid:{-1,232,1000})for(int rank:{1,2,20})for(int create:{0,1})for(int move:{0,1})for(int kill:{0,1}){
reset();record=rec;skill=used;chosen=sel;hit=success;found=exists;missileId=mid;created=create;moved=move;killed=kill;same=false;
int start=SKILLS_SrvSt10_LightningStrike(&game,&owner,34,rank);int result=SKILLS_SrvDo014_LightningStrike(&game,&owner,34,rank);
if(!first)std::printf(",");first=false;std::printf("{\"input\":[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],\"result\":[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],\"packets\":",rec,used,sel,success,exists,mid,rank,create,move,kill,start,result,allocs,drains,searches,creates,sets,lastRemaining,ar,src,saved.dwLtngDamage,saved.dwEnDmgPct,filter);packet();std::printf("}");}
std::printf("],\"children\":[");first=true;
for(int proj:{0,1})for(int mrow:{0,1})for(int own:{0,1})for(int victim:{0,1})for(int rem:{0,1,2,4,21})for(int srow:{0,1})for(int over:{0,12})for(int exists:{0,1})for(int equal:{0,1})for(int create:{0,1}){
reset();projectile=proj;projectileRow=mrow;hasOwner=own;record=srow;found=exists;same=equal;created=create;missileRow.dwHitPar[0]=over;bolt.remaining=rem;
int result=MISSMODE_SrvHit12_ChainLightning_LightningStrike(&game,proj?&bolt:nullptr,victim?&target:nullptr);
if(!first)std::printf(",");first=false;std::printf("{\"input\":[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],\"result\":[%d,%d,%d,%d,%d,%d,%d],\"packets\":",proj,mrow,own,victim,rem,srow,over,exists,equal,create,result,searches,creates,sets,lastRemaining,range,filter);packet();std::printf("}");}
std::printf("]}\n");}
'''
 def run(name,cpp):
  p=STAGE/(name+'.cpp');p.write_text(cpp);binary=STAGE/name;subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);return json.loads(subprocess.check_output([str(binary)]))
 data=run('lightning-strike-flow-probe',cpp);assert len(data['flow'])==2304 and len(data['children'])==2560
 for case in data['flow']:
  rec,used,sel,hit,found,mid,rank,create,move,kill=case['input'];valid=rec and sel;start=valid and used;search=valid;emit=valid and found and 0<=mid<1000
  assert case['result']==[int(start),int(emit),int(start),int(valid),int(search),int(emit),int(emit and create),rank+1 if emit and create else -1,0 if start else -1,128 if start else -1,513 if start and hit else 0,20 if start and hit else 0,3 if search else -1]
  assert case['packets']==([[32,mid,1,1,0,0,0,86,86,34,rank]] if emit else [])
 for case in data['children']:
  proj,mrow,own,victim,rem,srow,over,found,equal,create=case['input'];valid=proj and mrow and own and victim and rem>1 and (over>0 or srow);emit=valid and found and not equal
  assert case['result']==[1 if not(proj and mrow) else 3,int(valid),int(emit),int(emit and create),rem-1 if emit and create else -1,(over or 20) if valid else -1,0x88583 if valid else -1]
  assert case['packets']==([[33,34,1,0,1,66,82,70,86,34,17]] if emit else [])
 fixture=STAGE/'lightning-strike-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},probe=dict(startEmissionCases=len(data['flow']),childHitCases=len(data['children']),fixture=str(fixture.relative_to(ROOT))),established=[
 'Unchanged start10 requires row/used skill/target, uses explicit AR0, success-only skill-lightning min/max upper-exclusive draw and calc1ED20, then allocates melee128 when SrcDam0. Declared endpoint/formula/result/allocation services isolate control flow.',
 'Do14 requires row/target, commits ordinary melee drain before secondary search, and attempts chain creation regardless of prepared hit success or declared primary death/movement. Initial flags32, pOrigin=primary, original effective rank and remaining=rank+1; failed creation has no remaining-field write.',
 'The compiled older do14 packet sets both aim coordinates to secondary.Y=86, not X70/Y86. This source discrepancy is retained explicitly and is not a native design choice. Full initial aim/factory/LastCollide and current executable corroboration remain separate.',
 'Shared unchanged hit12 guards projectile/row/owner/victim/remaining, uses missile override radius or skill aura fallback, excludes a returned same victim, then creates directed flags33 child with retained rank and remaining-1. Target search, range/hostility/JustHit/NHD and real creation/damage are declared services.'
 ],limits=[
 'Source preparation only; no native handler or modern/class certificate.2304 flow/2560 child cases are not full engine/native gameplay comparisons.',
 'Scalar endpoints and formula values are declared; mastery/synergy interpreter, physical/critical, allocation/health/drain, world scan/selection/filter and factory/path/collision/seed/event services are not compiled by this flow fixture.',
 'Current initial aim/solo behavior requires independent primary/user evidence. Do not silently copy Y/Y or silently fix it as source-proven. Spell factory source precedence still needs a separate compiled fixture.'
 ])
 target=ROOT/'tools/reference/lightning-strike-engine-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==rendered,'Lightning Strike evidence changed'
 else:target.write_text(rendered)
 print(f'Lightning Strike:{len(data["flow"])} start/emission and{len(data["children"])} shared child-hit source cases pass; native verification separate.')

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
