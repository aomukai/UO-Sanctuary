#!/usr/bin/env python3
"""Execute complete Strafe start/do resource wrappers with actual ammo helpers.

Mana eligibility/debit, room/LOS, inventory metadata, missile creation and
rollback are declared services; this does not certify the modern binary.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_strafe_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
raw=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/skills.json').read_text())['26'];assert raw['mana']==11 and raw['manashift']==8 and raw['lvlmana']==0 and not any(raw.get(k) for k in ['decquant','usemanaondo','periodic','srvmissile'])
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(i['sha'] for i in tree['tree'] if i['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();text=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
 body=text[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
rel='source/D2Game/src/SKILLS/Skills.cpp'
base=(STAGE/'strafe-source-probe.cpp').read_text().split('int main()',1)[0]
base=base.replace('#include <vector>','#include <vector>\n#include <iterator>\n#include <string>')
base=base.replace('struct D2SkillsTxt{','struct D2SkillsTxt{unsigned dwFlags[2]={};int nLineOfSight=0,wSrvStartFunc=8,wSrvDoFunc=12,wSrvMissile=-1,wItemEffect=0,dwDelay=4,nAnim=4;')
base=base.replace('struct D2UnitStrc{','struct D2ActiveRoomStrc{};struct D2UnitStrc{void* pInventory=this;void* pDynamicPath=nullptr;unsigned dwFlags=0,dwSeqFrame=0;int kind=0,stack=0,ammunition=0,maxDur=20,stats[64]={};')
base=base.replace('struct Tables{int nMissilesTxtRecordCount=1000;}', 'struct Tables{int nMissilesTxtRecordCount=1000,pMissilesTxt[1000]={};}')
old='int sub_6FD119C0(D2UnitStrc*){ammoChecks++;return ammo;}void sub_6FC80A30(D2GameStrc*,D2UnitStrc*){fallbacks++;}void sub_6FC80E10(D2GameStrc*,D2UnitStrc*){fallbacks++;}\nint sub_6FD118C0(D2GameStrc*,D2UnitStrc*){quantity++;return 1;}'
assert old in base;base=base.replace(old,'int sub_6FD119C0(D2UnitStrc*);int sub_6FD118C0(D2GameStrc*,D2UnitStrc*);void sub_6FC80A30(D2GameStrc*,D2UnitStrc*);void sub_6FC80E10(D2GameStrc*,D2UnitStrc*);')
old='int STATLIST_UnitGetStatValue(D2UnitStrc* u,int,int){return u->damagePercent;}void STATLIST_SetUnitStat(D2UnitStrc* u,int,int v,int){u->damagePercent=v;}'
assert old in base;base=base.replace(old,'int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int);void STATLIST_SetUnitStat(D2UnitStrc*,int,int,int);')
base+=r'''
constexpr int UNIT_PLAYER=0,ITEMTYPE_BOW=1,ITEMTYPE_CROSSBOW=2,BODYLOC_RARM=1,BODYLOC_LARM=2,STAT_QUANTITY=18,STAT_DURABILITY=19,STAT_ITEM_MAGICARROW=20,STAT_ITEM_THROWABLE=21;
constexpr int SKILLSFLAGINDEX_INTOWN=0,SKILLSFLAGINDEX_PERIODIC=1,SKILLSFLAGINDEX_LOB=2,SKILLSFLAGINDEX_DECQUANT=3,SKILLSFLAGINDEX2_USEMANAONDO=4;
constexpr int SKILLSLOS_NONE=0,SKILLSLOS_MISSILE_BARRIER=1,SKILLSLOS_PLAYER_PATH=2,SKILLSLOS_PLAYER_MONSTER=3,SKILLSLOS_PLAYER_FLYING=4,SKILLSLOS_RADIAL_BARRIER=5;
constexpr int COLLIDE_MISSILE_BARRIER=1,COLLIDE_MASK_PLAYER_PATH=2,COLLIDE_PLAYER=4,COLLIDE_MONSTER=8,COLLIDE_DOOR=16,COLLIDE_WALL=32;
constexpr int PLRMODE_TOWNNEUTRAL=1,PLRMODE_SEQUENCE=18,UNITFLAG_SKSRVDOFUNC=1,EVENTTYPE_PERIODICSKILLS=1;
unsigned gdwBitMasks[32];D2ActiveRoomStrc room;D2UnitStrc weapon,leftArm;bool town=false;int mana=0,manaCalls=0,manaPaid=0;std::string order;
using SkillStartFunc=int32_t(*)(D2GameStrc*,D2UnitStrc*,int32_t,int32_t);using SkillDoFunc=SkillStartFunc;
SkillStartFunc gpSkillSrvStartFnTable_6FD408B0[70]={};SkillDoFunc gpSkillSrvDoFnTable_6FD40A20[160]={};
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}int DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return town;}
void UNITS_SetUsedSkill(D2UnitStrc*,D2SkillStrc* p){hasSkill=p!=nullptr;}void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int){}
int SKILLS_GetFlags(D2SkillStrc*){return 0;}int SKILLS_GetSkillIdFromSkill(D2SkillStrc*,const char*,int){return 26;}int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int){return 20;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return hasSkill?&used:nullptr;}
int sub_6FD10CE0(D2UnitStrc*){return mana>=2816;}
int D2GAME_SKILLMANA_Consume_6FD10A50(D2GameStrc*,D2UnitStrc*,int,int){manaCalls++;order+='M';if(mana<2816)return 0;mana-=2816;manaPaid++;return 1;}
int D2COMMON_10175_PathGetFirstPointX(void*){return 100;}int D2COMMON_10176_PathGetFirstPointY(void*){return 200;}
int D2Common_11026(int,int,D2UnitStrc*,unsigned){return 1;}void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){}
void sub_6FD11710(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){}void sub_6FD11420(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){}
void D2GAME_SKILLS_SetDelay_6FD11C00(D2GameStrc*,D2UnitStrc*,int){}
D2UnitStrc* sub_6FC7C7B0(D2UnitStrc*){return &weapon;}
int ITEMS_GetAmmoType(D2UnitStrc* u){return u->ammunition;}int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* u,int stat,int){return u->stats[stat];}
int STATLIST_UnitGetStatValue(D2UnitStrc* u,int stat,int){return stat==STAT_DAMAGEPERCENT?u->damagePercent:u->stats[stat];}
void STATLIST_SetUnitStat(D2UnitStrc* u,int stat,int value,int){if(stat==STAT_DAMAGEPERCENT)u->damagePercent=value;else{u->stats[stat]=value;if(stat==STAT_QUANTITY){quantity++;order+='Q';}}}
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(void* inv,int hand){return inv?(hand==BODYLOC_RARM?&weapon:&leftArm):nullptr;}
int ITEMS_CheckItemTypeId(D2UnitStrc* u,int type){return u&&u->kind==type;}int ITEMS_CheckIfStackable(D2UnitStrc* u){return u->stack;}int ITEMS_CheckIfThrowable(D2UnitStrc*){return 0;}
int ITEMS_CanItemBeUsedForThrowSkill(D2UnitStrc* u){ammoChecks++;return u->stats[STAT_QUANTITY]>0;}
void sub_6FC51310(D2GameStrc*,D2UnitStrc*){}void* SUNIT_GetClientFromPlayer(D2UnitStrc*,const char*,int){return nullptr;}
void D2GAME_PACKETS_SendPacket0x3E_6FC3EC20(void*,D2UnitStrc*,int,int,int,int){}int STATLIST_GetMaxDurabilityFromUnit(D2UnitStrc* u){return u->maxDur;}
void sub_6FC80A30(D2GameStrc*,D2UnitStrc*){order+='A';}void sub_6FC80E10(D2GameStrc*,D2UnitStrc*){order+='E';}
'''
for name in ['sub_6FD11340','sub_6FD118C0','sub_6FD119C0','sub_6FD12950','D2GAME_SKILLS_Handler_6FD12BA0']:base+=routine(rel,name)+'\n'
base+=r'''
void reset(D2UnitStrc& owner,int family,int q,int m){owner={};owner.dwUnitType=0;weapon={};leftArm={};weapon.kind=family;weapon.ammunition=family;leftArm.stack=1;leftArm.stats[STAT_QUANTITY]=q;leftArm.stats[STAT_DURABILITY]=20;row={};used={};mana=m;manaCalls=manaPaid=quantity=created=rollbacks=0;order.clear();town=false;hasRow=hasSkill=factory=true;gpSkillSrvStartFnTable_6FD408B0[8]=SKILLS_SrvSt08_Strafe;gpSkillSrvDoFnTable_6FD40A20[12]=SKILLS_SrvDo012_Strafe;}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;D2GameStrc game;D2UnitStrc owner,targets[15];for(int i=0;i<15;i++)targets[i].dwUnitId=10*(i+1);bool first=true;std::printf("{\"start\":[");
for(int record:{0,1})for(int skill:{0,1})for(int inTown:{0,1})for(int m:{0,2815,2816,5632})for(int q:{0,1,2,20})for(int skip:{0,1})for(int target:{0,1})for(int count:{0,3,15})for(int rank:{1,20})for(int family:{1,2}){
 reset(owner,family,q,m);hasRow=record;hasSkill=skill;town=inTown;candidates.clear();for(int i=count-1;i>=0;i--)candidates.push_back(&targets[i]);selected=target?&targets[0]:nullptr;
 int result=sub_6FD12950(&game,&owner,26,rank,skip);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,\"%s\"]",record,skill,inTown,m,q,skip,target,count,rank,family,result,leftArm.stats[STAT_QUANTITY],mana,used.p[0],manaCalls,manaPaid,order.c_str());
}
std::printf("],\"do\":[");first=true;
for(int family:{1,2})for(int remaining:{1,2,7})for(int m:{0,2816})for(int q:{0,1,20})for(int count:{0,1,3})for(int phase:{0,1})for(int make:{0,1}){
 reset(owner,family,q,m);factory=make;used.p[0]=remaining;used.p[1]=1;used.p[2]=10;candidates.clear();lookup.clear();for(int i=count-1;i>=0;i--){candidates.push_back(&targets[i]);lookup.push_back(&targets[i]);}weaponClass=family;
 int result=D2GAME_SKILLS_Handler_6FD12BA0(&game,&owner,26,20,phase,0,0);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,\"%s\"]",family,remaining,m,q,count,phase,make,result,created,leftArm.stats[STAT_QUANTITY],mana,used.p[0],manaCalls,quantity,order.c_str());
}std::printf("]}\n");}
'''
path=STAGE/'strafe-action-source-probe.cpp';path.write_text(base);binary=STAGE/'strafe-action-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data['start'])==6144 and len(data['do'])==432
for record,skill,town,m,q,skip,target,count,rank,family,result,left,after,shots,calls,paid,order in data['start']:
 ok=bool(record and skill and not town and (skip or m>=2816) and q)
 cap=min(3+rank,10);expected=min(cap,max(min(2+rank//4,cap),count if target else 0)) if ok else 0
 assert(result,left,after,shots,calls,paid)==(int(ok),q-int(ok),m-2816*int(ok and not skip),expected,int(ok and not skip),int(ok and not skip))
 assert order==('QA'+('M' if not skip else '') if ok else 'AE' if record and skill and not town and (skip or m>=2816) else '')
for family,remaining,m,q,count,phase,make,result,created,left,after,shots,calls,quantity,order in data['do']:
 assert (result,created,left,after,shots,calls,quantity,order)==(int(not count or remaining>1),int(bool(count)),q,m,remaining-int(bool(count)),0,0,'')
fixture=STAGE/'strafe-action-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',talentId='amazon.strafe',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes=blocks,sharedCallbacks='tools/reference/strafe-engine-evidence.json',probe=dict(startCases=6144,doCases=432,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete generic start wrapper invokes full start8 and full inventory validation/quantity selection/decrement. Successful normal start subtracts exactly one Arrow/Bolt before one declared11mana debit. Empty/no-target successful starts still consume both; low mana/empty ammo/town reject before debit. The skip-start-payment caller control bypasses mana validation/debit but does not bypass ammunition.','Complete generic do wrapper invokes full do12. Current Strafe has a start function, no usemanaondo and no decquant flag: later do events neither validate/debit mana nor consume more ammo, including zero remaining ammunition, zero current mana, absent targets and failed missile-factory controls. Last do12 emits before returning0; generic wrapper returns0 after the emitted final arrow.','Complete quantity decrement subtracts/clamps one stack, refreshes durability/packets/cleanup before mana payment. Actual stack metadata, equipped-quiver/reload/destruction and client packet services remain declared; the shared bow ammunition probe provides wider selection coverage.'],limits=['Mana eligibility and exact debit routine are declared11mana services here; complete stat/mana callback/charge/player-mode wrapper and modern caller flags remain outside this proof. Room/town/LOS, inventory type/use metadata, formula interpreter and missile factory are declared services.','Complete start/do/quantity bodies execute without patching, but this does not establish modern Strafe sequence timing, lock/interrupt/input/gear/owner/save lifecycle, target-selection differences, AR/stat/RNG/next-hit/geometry/current-client parity. No native handler is enabled.'])
out=ROOT/'tools/reference/strafe-action-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Strafe action evidence stale'
else:out.write_text(encoded)
print('Strafe:6144 complete start-wrapper/ammunition and432 generic do-wrapper resource cases compiled.')
