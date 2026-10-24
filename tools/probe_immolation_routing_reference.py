#!/usr/bin/env python3
"""Execute the complete missile hit router around the established Immolation callbacks."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_immolation_arrow_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(i['sha'] for i in tree['tree'] if i['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();text=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
 body=text[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
mode='source/D2Game/src/MISSILES/MissMode.cpp';common='source/D2Common/src/Units/Missile.cpp';router=routine(mode,'MISSMODE_SrvDmgHitHandler');setOwner=routine(common,'MISSILE_SetOwner');checkOwner=routine(common,'MISSILE_CheckUnitIfOwner')
base=(STAGE/'immolation-arrow-source-probe.cpp').read_text().split('int main()',1)[0]
base=base.replace('#define __fastcall','#include <iterator>\n#include <map>\n#define __stdcall\n#define __fastcall')
base=base.replace('struct D2GameStrc{};','struct D2GameStrc{int dwGameFrame=1000;void* pMemoryPool=nullptr;};\nstruct D2MissileDataStrc{int nOwnerType=0,dwOwnerGUID=-1;};\nstruct D2StatListStrc{};')
base=base.replace('int dwClassId=85,','int dwUnitType=3,dwUnitId=10,dwMissileMode=3;std::map<int,int> stats;D2GameStrc* pGame=nullptr;D2MissileDataStrc md;D2MissileDataStrc* pMissileData=nullptr;void* pDynamicPath=nullptr;int dwClassId=85,')
base=base.replace('unsigned dwHitFlags=2,wResultFlags=5;','unsigned dwHitFlags=2,wResultFlags=5,dwMissileFlags=4;int nCollideKill=1,nToHit=1,nNextHit=0,nNextDelay=0,nAlwaysExplode=1,wSrvHitFunc=9,wSrvDmgFunc=0,nCollideFriend=0;')
base=base.replace('unsigned dwHitFlags=0,wResultFlags=0;','unsigned dwHitFlags=0,wResultFlags=0;int dwPhysDamage=0;')
base=base.replace('clears++;if(stat==STAT_COLDLENGTH)p->cold=value;else p->freeze=value;','if(stat==STAT_COLDLENGTH){clears++;p->cold=value;}else if(stat==STAT_ITEM_FREEZE){clears++;p->freeze=value;}else p->stats[stat]=value;')
base+=r'''
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_MISSILE=3,D2UnitInvalidGUID=-1,TRUE=1,FALSE=0;
using BOOL=int;constexpr int MISSILESFLAGINDEX_EXPLOSION=0,MISSILESFLAGINDEX_PIERCE=1,MISSILESFLAGINDEX_LASTCOLLIDE=2;
constexpr int MISSMODE_NOCOLLIDE=0,MISSMODE_PLAYERKILL=1,MISSMODE_MONSTERKILL=2,STATE_JUSTHIT=1,EVENTTYPE_REMOVESTATE=1,UNITEVENT_HITBYMISSILE=1;
constexpr int STAT_SKILL_PIERCE=3,STAT_ITEM_PIERCE=4,STAT_PIERCE_IDX=5,STAT_TOHIT=6,STAT_ITEM_DAMAGETARGETAC=7,STAT_ARMORCLASS=8;
unsigned gdwBitMasks[32];D2UnitStrc target;bool hit=true;int arCalls=0,fillCalls=0,flagCalls=0,coldAtFill=-1,freezeAtFill=-1,resets=0;
D2MissilesTxt* DATATBLS_GetMissilesTxtRecord(int){return &row;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return p->stats[stat];}int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int stat,int){return p->stats[stat];}
bool STATES_CheckState(D2UnitStrc*,int){return false;}bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return true;}
bool SUNITDMG_IsHitSuccessful(D2UnitStrc*,D2UnitStrc*,int,int){arCalls++;return hit;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int,int,int){return nullptr;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){}void STATLIST_SetState(D2StatListStrc*,int){}
void MISSMODE_ToggleStateOff(D2UnitStrc*,int,D2StatListStrc*){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*)){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}void STATES_ToggleState(D2UnitStrc*,int,int){}
void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,void*){}
void MISSMODE_FillDamageParams(D2UnitStrc* p,D2UnitStrc*,D2DamageStrc*){fillCalls++;coldAtFill=p->cold;freezeAtFill=p->freeze;}
void MISSMODE_SetDamageFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){flagCalls++;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return 0;}
int UNITS_GetUnitSizeX(D2UnitStrc*){return 1;}
void COLLISION_ResetMaskWithSize(D2ActiveRoomStrc*,int,int,int,unsigned){resets++;}
'''
base+=setOwner+'\n'+checkOwner+'\n'
existing={'MISSMODE_SrvHit09_ImmolationArrow','MISSMODE_SrvDmg03_Blaze_FireWall_ImmolationFire_MeteorFire'}
for name in sorted(set(re.findall(r'\bMISSMODE_Srv(?:Hit|Dmg)\w+',router)) - existing):
 # Only table entries are declared adapter callbacks; the complete router itself is appended below.
 if name=='MISSMODE_SrvDmgHitHandler':continue
 kind='void' if 'SrvDmg' in name else 'int32_t';params='D2GameStrc*,D2UnitStrc*,D2UnitStrc*'+(',D2DamageStrc*' if kind=='void' else '')
 base+=f'{kind} {name}({params}){{'+('' if kind=='void' else 'return 3;')+'}\n'
base+=router+r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("[");
for(int field:{0,1})for(int victim:{0,1})for(int a4:{0,1})for(int success:{0,1})for(int pierce:{0,100})for(int budget:{0,1,4})for(int repeat:{0,1}){
 parent={};owner={};target={};parent.pGame=&game;parent.pMissileData=&parent.md;target.dwUnitType=1;row={};row.dwHitCalc=100;parent.dwClassId=field?230:85;row.dwMissileFlags=field?0:gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE]|gdwBitMasks[MISSILESFLAGINDEX_PIERCE];
 row.nCollideKill=field?0:1;row.nToHit=field?0:1;row.nAlwaysExplode=field?0:1;row.wSrvHitFunc=field?0:9;row.wSrvDmgFunc=field?3:0;
 owner.stats[STAT_SKILL_PIERCE]=pierce;parent.stats[STAT_PIERCE_IDX]=budget;hasRecord=hasSkill=hasOwner=true;terrain=0;hit=success;arCalls=fillCalls=flagCalls=clears=damageCalls=areaCalls=resets=0;coldAtGetter=freezeAtGetter=coldAtFill=freezeAtFill=-1;created.clear();randomValue=0;randomCalls=0;
 if(repeat){parent.md.nOwnerType=1;parent.md.dwOwnerGUID=10;}
 int result=MISSMODE_SrvDmgHitHandler(&game,&parent,victim?&target:nullptr,a4);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%zu,%d,%d,%d,%d,%d]",field,victim,a4,success,pierce,budget,repeat,result,arCalls,fillCalls,flagCalls,clears,damageCalls,areaCalls,created.size(),parent.stats[STAT_PIERCE_IDX],coldAtFill,freezeAtFill,resets,randomCalls);
}std::printf("]\n");}
'''
path=STAGE/'immolation-routing-source-probe.cpp';path.write_text(base);binary=STAGE/'immolation-routing-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==192
for field,victim,a4,success,pierce,budget,repeat,result,ar,fill,flags,clears,damage,area,cells,newBudget,cold,freeze,resets,draws in data:
 rejected=bool(not field and victim and repeat);miss=bool(not field and victim and not success and not rejected)
 callback=not field and not rejected
 direct=bool(victim and not rejected and not miss)
 assert ar==int(bool(not field and victim and not rejected)) and fill==flags==int(direct)
 assert clears==2*callback and damage==area==int(callback) and cells==29*callback
 assert(cold,freeze)==((100,1) if field and direct else(0,0) if direct else(-1,-1))
 assert newBudget==budget-int(bool(not field and victim and not rejected and pierce and budget))
 if rejected:expected=1
 elif miss:expected=2
 elif field:expected=2 if a4 else 1
 else:expected=1 if victim and pierce and budget else 2
 assert result==expected,(field,victim,a4,success,pierce,budget,repeat,result,expected)
 assert draws==int(bool(field and direct))
fixture=STAGE/'immolation-routing-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',talentId='amazon.immolationarrow',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes=blocks,sharedCallbacks='tools/reference/immolation-arrow-engine-evidence.json',probe=dict(cases=len(data),path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete unchanged hit router invokes current Immolation hit09 before its successful direct FillDamageParams/SetDamageFlags path. The immediate area and29 ground creations also occur on an AR miss; the miss skips direct damage and removes the parent. Null collision still invokes hit09 through AlwaysExplode.','Parent hit09 clears cold length/item freeze before direct full-damage parameter reads. Positive current owner Pierce plus saved index permits continuing after a successful contact; index is consumed before AR. Parent LastCollide rejects a repeated most-recent victim.','Complete source LastCollide setters/checkers respect the row flag. ImmolationFire has no LastCollide, ToHit or CollideKill: repeated same-target contacts can use the direct field damage callback with no AR and keep the field active. Field removal/a4 ends it; SOFTHIT selection remains a separate complete callback.'],limits=['Direct damage fill/flags, AR/hostility/stat/room and NHD/event services are explicit adapters. This establishes complete routing order and current row distinctions, not full prepared-damage/block/event/mitigation or modern collision execution. Unselected function-table callbacks are declared stubs.','Current localdelay, factory/creator packets, field do5 movement/collision/recipient footprint/cadence, DamageRate41, source RNG and modern owner/quiver/geometry/client behavior remain separate work. No handler is enabled.'])
out=ROOT/'tools/reference/immolation-routing-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Immolation routing evidence stale'
else:out.write_text(encoded)
print('Immolation:192 complete hit-router/LastCollide cases compiled; immediate-area/direct/field ordering established.')
