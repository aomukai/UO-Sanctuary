#!/usr/bin/env python3
"""Exercise unchanged supplementary warcry routines with explicitly bounded stubs."""
from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'
BASE=STAGE/'d2moo-pinned-source'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text())
assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
entries={x['path']:x['sha'] for x in tree['tree'] if x['type']=='blob'}
hashes={}
def routine(path,name):
 raw=(BASE/path).read_bytes()
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==entries[path],path
 hashes[path]=hashlib.sha256(raw).hexdigest()
 text=raw.decode();match=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M)
 assert match,name
 start=match.start();end=text.index('{',start)+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[start:end]
blocks={
 'stats':routine('source/D2Game/src/SKILLS/SkillBar.cpp','SKILLS_ApplyWarcryStats'),
 'writer':routine('source/D2Game/src/SKILLS/SkillDruid.cpp','sub_6FCFE0E0'),
 'launch':routine('source/D2Game/src/SKILLS/Skills.cpp','sub_6FD14170'),
 'cast':routine('source/D2Game/src/SKILLS/SkillBar.cpp','SKILLS_SrvDo068_BasicShout'),
 'hit':routine('source/D2Game/src/MISSILES/MissMode.cpp','MISSMODE_SrvHit18_Shout_BattleCommand_BattleOrders'),
 'advance':routine('source/D2Common/src/Path/PathMisc.cpp','sub_6FD5CEB0'),
 'lastSet':routine('source/D2Common/src/Units/Missile.cpp','MISSILE_SetOwner'),
 'lastCheck':routine('source/D2Common/src/Units/Missile.cpp','MISSILE_CheckUnitIfOwner'),
}
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
#define __stdcall
using BOOL=int;
constexpr int FALSE=0,UNIT_MISSILE=3,UNITFLAG_SKSRVDOFUNC=1,EVENTTYPE_REMOVESTATE=1,STAT_ATTACKRATE=5,STAT_OTHER_ANIMRATE=6,MISSILESFLAGINDEX_LASTCOLLIDE=0,D2UnitInvalidGUID=-1;
int gdwBitMasks[]={1};
struct D2StatListStrc{int until=0,flag=0,stat=0,creator=0;};
struct MissileData{int nOwnerType=0,dwOwnerGUID=0;};
struct D2UnitStrc{int dwUnitId=1,dwUnitType=0,dwClassId=149,dwFlags=0,x=100,y=200,hard=0,rank=1;bool aligned=true;D2StatListStrc* state=nullptr;MissileData* pMissileData=nullptr;};
struct D2GameStrc{int dwGameFrame=100;void* pMemoryPool=nullptr;};
struct D2SkillsTxt{int nAuraState=26,dwAuraLenCalc=0,wAuraStat[6]={1,-1,-1,-1,-1,-1},dwAuraStatCalc[6]={1,0,0,0,0,0};} row;
struct D2MissilesTxt{int dwMissileFlags=0;} missileRow;
struct Tables{int nStatesTxtRecordCount=200,nMissilesTxtRecordCount=1000;} tables;auto* sgptDataTables=&tables;
struct Vector{int nX=4096,nY=0;};
struct D2DynamicPathStrc{int dwAcceleration=-500,dwAccelerationsCounter=0,dwMaxVelocity=7680,dwVelocity=5760;Vector tVelocityVector{},tDirectionVector{};};
struct D2MissileStrc{int dwFlags=0,nX=0,nY=0,nSkill=0,nMissile=0,nSkillLevel=0,nVelocity=0,nTargetX=0,nTargetY=0;D2UnitStrc* pOwner=nullptr;};
D2StatListStrc list;D2UnitStrc* owner=nullptr;std::vector<D2MissileStrc> launches;int allocs=0,refreshes=0,eventAt=0;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return &row;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int calc,int,int rank){return calc==0?750+250*(rank-1)+125*p->hard:100+10*(rank-1);}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* p,int){return p->state;}
D2StatListStrc* STATLIST_AllocStatList(void*,int flag,int until,int,int creator){allocs++;list={until,flag,0,creator};return &list;}
void STATLIST_SetState(D2StatListStrc*,int){}
void sub_6FD10E50(){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)()){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc* p,D2StatListStrc* l,int){p->state=l;}
void STATES_ToggleState(D2UnitStrc*,int,int){}
void STATES_ToggleGfxStateFlag(D2UnitStrc*,int,int){}
void D2COMMON_10476(D2StatListStrc* p,int until){p->until=until;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int at,int,int){eventAt=at;}
void sub_6FD14C30(D2UnitStrc*){refreshes++;}
bool SKILLS_GetItemStatCostTxtRecord(int stat){return stat>=0;}
void STATLIST_SetStat(D2StatListStrc* p,int,int value,int){p->stat=value;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*,int,int,int){}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
void MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){launches.push_back(*p);}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int){return 149;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return owner;}
bool SUNIT_AreUnitsAligned(D2GameStrc*,D2UnitStrc*,D2UnitStrc* p){return p->aligned;}
int MISSILE_GetSkill(D2UnitStrc*){return 138;}int MISSILE_GetLevel(D2UnitStrc* p){return p->rank;}
D2MissilesTxt* DATATBLS_GetMissilesTxtRecord(int){return &missileRow;}
'''+blocks['writer']+'\n'+blocks['stats']+'\n'+blocks['launch']+'\n'+blocks['cast']+'\n'+blocks['hit']+'\n'+blocks['advance']+'\n'+blocks['lastSet']+'\n'+blocks['lastCheck']+r'''
int main(){D2GameStrc game;D2UnitStrc caster,target,missile;caster.dwUnitId=42;owner=&caster;missile.dwUnitType=3;
std::printf("{\"rays\":[");SKILLS_SrvDo068_BasicShout(&game,&caster,138,7);bool first=true;
for(auto& ray:launches){if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d]",ray.nTargetX,ray.nTargetY,ray.dwFlags,ray.nX,ray.nY,ray.nSkill,ray.nSkillLevel,ray.nVelocity);}
std::printf("],\"refresh\":[");first=true;target.state=nullptr;
for(int rank:{20,1,1,30}){game.dwGameFrame+=10;caster.hard=rank==1?3:0;SKILLS_ApplyWarcryStats(&game,&target,&caster,138,rank);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",rank,caster.hard,game.dwGameFrame,target.state->stat,target.state->until,target.state->flag,target.state->creator);}
std::printf("],\"hit\":[");first=true;
for(int hasOwner:{0,1})for(int hasTarget:{0,1})for(int aligned:{0,1}){owner=hasOwner?&caster:nullptr;target.aligned=aligned;refreshes=0;int result=MISSMODE_SrvHit18_Shout_BattleCommand_BattleOrders(&game,&missile,hasTarget?&target:nullptr);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",hasOwner,hasTarget,aligned,result,refreshes);}
std::printf("],\"lastCollision\":[");first=true;MissileData md;missile.pMissileData=&md;target.dwUnitId=99;
for(int enabled:{0,1}){missileRow.dwMissileFlags=enabled;md={};MISSILE_SetOwner(&missile,&target);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d]",enabled,md.dwOwnerGUID,MISSILE_CheckUnitIfOwner(&missile,&target));}
std::printf("],\"motion\":[");first=true;D2DynamicPathStrc path;
for(int frame=1;frame<=15;frame++){sub_6FD5CEB0(&path,1024);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d]",frame,path.dwVelocity,path.dwAccelerationsCounter,path.tVelocityVector.nX);}
std::printf("]}\n");}
'''
path=STAGE/'shout-source-probe.cpp';path.write_text(cpp)
binary=STAGE/'shout-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
assert len(data['rays'])==64 and len({tuple(r[:2]) for r in data['rays']})==64
for x,y,flags,ox,oy,skill,rank,velocity in data['rays']:assert(flags,ox,oy,skill,rank,velocity)==(3,100,200,138,7,0)
for rank,hard,frame,defense,until,flag,creator in data['refresh']:assert(defense,until,flag,creator)==(100+10*(rank-1),frame+750+250*(rank-1)+125*hard,2,42)
for owner,target,aligned,result,refreshed in data['hit']:assert(result,refreshed)==(int(not owner),int(bool(owner and target and aligned)))
assert data['lastCollision']==[[0,0,0],[1,99,1]]
for frame,velocity,counter,dx in data['motion']:assert(velocity,counter,dx)==(5760-500*(frame//5),frame%5,(5760-500*(frame//5))*16)
(STAGE/'shout-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,
 probe=dict(dataPath='.local/class-skills/shout-source-probe.json',caseCounts={k:len(v) for k,v in data.items()},blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()}),
 established=[
 'Do68 launches64 distinct fixed integer-offset rays with stored skill rank and no velocity override, then applies the same state to self immediately.',
 'ApplyWarcryStats refreshes existing recipient state without a stronger-rank or longer-duration guard. Nonzero supplier formulas replace its stat; expiry is game frame plus freshly evaluated caster duration. Existing state retains its original allocation owner metadata.',
 'Hit18 applies to aligned collisions only, returns0 for any existing owner even on hostile/no-target hits, and returns1 for absent owner. Alignment is explicitly stubbed in this probe.',
 'Shout has no LastCollide flag: SetOwner/CheckUnitIfOwner do not suppress repeated recipient collisions. BO/BC differ and are not covered by the Shout conclusion.',
 'At supplied base velocity1024/direction4096, initial5760 decelerates500 on each fifth advance; frame1..4 displacement92160FP16 subtiles,5..9 is84160,10..14 is76160. Range15 collision ordering excludes frame15.',
 'Generic no-start-function cast validates mana at start and consumes at successful do-event. Shout lacks InTown; native release must reject town. These generic branches are source-reviewed, not compiled by this probe.'
 ],stubs=[
 'Formula evaluator supplies Shout modern arithmetic and current hard synergy sum; this is not an independent formula compiler proof.',
 'Stat-list allocation, SetStat, event scheduling/removal, unit refresh and owner lookup are explicit stubs; existing-state versus allocation/call ordering is exercised.',
 'Missile factory captures launch parameters only. Q12 normalization, room/path stepping, actual unit collision search, terrain, frame dispatch and alignment are outside this bounded executable.',
 'Motion begins with zero acceleration counter; path allocation/initialization and modern scheduling equivalence remain source-review tasks.'
 ],limits=[
 'Supplementary D2MOO source is not a modern D2R executable certificate.',
 'D2 subtile to native UO tile geometry, hit footprints, source collision flags versus native actor eligibility, and missile-barrier terrain equivalence remain explicit open mappings.',
 'The source PATH_D2Common_10142 body labels itself BROKEN outside its missile fast path; do not infer a complete path-engine certificate.',
 'The generic legacy consume routine has the known rank1/minmana reconstruction discrepancy, so it does not certify the modern6-mana value. The pinned modern table governs6 mana; exact modern post-start mana-loss outcome remains unverified.'
 ])
(ROOT/'tools/reference/shout-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print('Shout bounded source checks passed:',{k:len(v) for k,v in data.items()})
