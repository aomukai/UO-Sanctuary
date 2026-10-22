#!/usr/bin/env python3
"""Compile unchanged Battle Cry hit/filter/curse/writer and shared stat consumers.
World, event, allocation and stat services are explicit fixtures, not an engine.
"""
from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
blobs={x['path']:x['sha'] for x in tree['tree'] if x['type']=='blob'};hashes={};blocks={}
def source(path):
 raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blobs[path],path
 hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(text,name):
 m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
 end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 blocks[name]=text[m.start():end];return blocks[name]
skills=source('source/D2Game/src/SKILLS/Skills.cpp');miss=source('source/D2Game/src/MISSILES/MissMode.cpp');druid=source('source/D2Game/src/SKILLS/SkillDruid.cpp');units=source('source/D2Common/src/Units/Units.cpp');damage=source('source/D2Game/src/UNIT/SUnitDmg.cpp');sunit=source('source/D2Game/src/UNIT/SUnit.cpp');nec=source('source/D2Game/src/SKILLS/SkillNec.cpp')
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <map>
#include <initializer_list>
#define __fastcall
#define __stdcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_OBJECT=2,UNIT_MISSILE=3,UNIT_ITEM=4,PLRMODE_DEAD=12,PLRMODE_DEATH=0,MONMODE_DEAD=12,MONMODE_DEATH=0;
constexpr int UNITFLAG_CANBEATTACKED=1,UNITFLAG_ISVALIDTARGET=2,MONSTATSFLAGINDEX_NPC=2,MONSTATS2FLAGINDEX_ISATT=0,EVENTTYPE_REMOVESTATE=3,STATE_ATTRACT=57,STATE_JUSTHIT=9,STATE_HOLYSHIELD=101;
constexpr int STAT_STRENGTH=0,STAT_DEXTERITY=1,STAT_ARMORCLASS=2,STAT_ITEM_ARMOR_PERCENT=3,STAT_SKILL_ARMOR_PERCENT=4,STAT_ARMOR_OVERRIDE_PERCENT=5,STAT_MODIFIERLIST_SKILL=6,STAT_MODIFIERLIST_LEVEL=7,STAT_CURSE_RESISTANCE=8,STAT_ATTACKRATE=9,STAT_OTHER_ANIMRATE=10,STAT_SECONDARY_MINDAMAGE=11,STAT_SECONDARY_MAXDAMAGE=12,STAT_MINDAMAGE=13,STAT_MAXDAMAGE=14,STAT_ITEM_NORMALDAMAGE=15,STAT_DAMAGEPERCENT=16,STAT_ITEM_MINDAMAGE_PERCENT=17,STAT_ITEM_MAXDAMAGE_PERCENT=18;
constexpr uint32_t gdwBitMasks[]={1,2,4,8};
struct D2ActiveRoomStrc{bool town=false;};struct D2CoordStrc{int nX=0,nY=0;};
struct D2StatListStrc{int state=0,skill=0,rank=0,expire=0,creator=0;uint32_t flags=0;std::map<int,int> stats;};
struct D2UnitStrc{int dwUnitType=1,dwClassId=1,dwFlags=3,dwAnimMode=1,dwUnitId=42,pSeed=0;void* pMemoryPool=nullptr;void* pInventory=nullptr;D2UnitStrc* owner=nullptr;D2StatListStrc* list=nullptr;std::map<int,int> stats;bool attract=false,aligned=false,pvp=false;D2ActiveRoomStrc* room=nullptr;};
struct D2GameStrc{int dwGameFrame=100;} game;
using Callback=void(*)(D2UnitStrc*);
struct D2CurseStrc{D2UnitStrc* pUnit=nullptr;D2UnitStrc* pTarget=nullptr;int nSkill=0,nSkillLevel=0,nDuration=0,nState=0,nStat=0,nStatValue=0;Callback pStateRemoveCallback=nullptr;};
struct D2SkillsTxt{int wAuraTargetState=89,dwAuraFilter=98304,dwAuraLenCalc=1,wAuraStat[6]={4,16,-1,-1,-1,-1},dwAuraStatCalc[6]={2,3},dwCalc[1]={4};} row;
struct D2StatesTxt{int nStateFlags[1]={0};} state;
struct D2MonStatsTxt{uint32_t dwMonStatsFlags=0;} mon;
struct D2MonStats2Txt{uint32_t dwFlags=1;} modes;
struct D2MissilesTxt{uint32_t dwMissileFlags=0;} missileRow;
struct Tables{int nStatesTxtRecordCount=256;} tables;Tables* sgptDataTables=&tables;
D2ActiveRoomStrc room;D2StatListStrc storage;bool validRow=true,validState=true,validMon=true,validModes=true,curseMask=true,blocked=false;int removals=0,events=0,writer=0,updates=0,formulaUnit=-1,roll=0;
D2GameStrc* SUNIT_GetGameFromUnit(D2UnitStrc*){return &game;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc* p){return p->owner;}
D2UnitStrc* AIGENERAL_GetMinionOwner(D2UnitStrc* p){return p->owner;}
int PLAYERLIST_CheckFlag(D2UnitStrc* p,D2UnitStrc*,int){return p->pvp;}
int STATLIST_AreUnitsAligned(D2UnitStrc*,D2UnitStrc* p){return p->aligned;}
int MISSILE_GetSkill(D2UnitStrc*){return 146;}int MISSILE_GetLevel(D2UnitStrc* p){return p->pSeed;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return validRow?&row:nullptr;}
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){return SKILLS_GetSkillsTxtRecord(id);}
D2StatesTxt* SKILLS_GetStatesTxtRecord(int){return validState?&state:nullptr;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return validMon?&mon:nullptr;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return validModes?&modes:nullptr;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return &missileRow;}
bool MONSTERS_IsUndead(D2UnitStrc*){return true;}bool MONSTERS_IsBoss(void*,D2UnitStrc*){return true;}bool MONSTERS_IsPrimeEvil(D2UnitStrc*){return true;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc* p){return p?p->room:nullptr;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc* p){return p&&p->town;}
bool SUNIT_AreUnitsAligned(D2GameStrc*,D2UnitStrc*,D2UnitStrc* p){return p->aligned;}
bool UNITS_IsInMeleeRange(D2UnitStrc*,D2UnitStrc*,int){return true;}
int CLIENTS_GetUnitX(D2UnitStrc*){return 0;}int CLIENTS_GetUnitY(D2UnitStrc*){return 0;}
bool COLLISION_RayTrace(D2ActiveRoomStrc*,D2CoordStrc*,D2CoordStrc*,unsigned){return blocked;}
int STATES_CheckStateMaskCurseByStateId(int){return curseMask;}int STATES_CheckStateMaskExpByStateId(int){return 0;}
int STATES_CheckState(D2UnitStrc* p,int s){return s==STATE_ATTRACT?p->attract:0;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int s,int){return p->stats[s];}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int s,int){return p->stats[s];}
int MONSTERUNIQUE_CalculatePercentage(int v,int p,int d){return int(int64_t(v)*p/d);}
D2StatListStrc* STATLIST_GetStatListFromUnitAndFlag(D2UnitStrc* p,unsigned){return p->list;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* p,int s){return p->list&&p->list->state==s?p->list:nullptr;}
int STATLIST_GetState(D2StatListStrc* p){return p->state;}int STATLIST_GetSkillId(D2StatListStrc* p){return p->skill;}int STATLIST_GetSkillLevel(D2StatListStrc* p){return p->rank;}
int STATLIST_GetStatValue(D2StatListStrc* p,int s,int){return p->stats[s];}
void STATLIST_SetExpireFrame(D2StatListStrc* p,int n){p->expire=n;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){events++;}
void D2Common_10474(D2UnitStrc* p,D2StatListStrc*){p->list=nullptr;removals++;}
void STATLIST_FreeStatList(D2StatListStrc*){}void UNITROOM_RefreshUnit(D2UnitStrc*){}void STATES_ToggleState(D2UnitStrc*,int,int){}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned f,int e,int,int id){storage={};storage.flags=f;storage.expire=e;storage.creator=id;return &storage;}
void STATLIST_SetState(D2StatListStrc* p,int n){p->state=n;}void STATLIST_SetSkillId(D2StatListStrc* p,int n){p->skill=n;}void STATLIST_SetSkillLevel(D2StatListStrc* p,int n){p->rank=n;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc* p,int s,int v,int){p->stats[s]=v;}
void STATLIST_SetStat(D2StatListStrc* p,int s,int v,int){p->stats[s]=v;writer++;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc* p,D2StatListStrc* s,int){p->list=s;}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,Callback){}void sub_6FD10E50(D2UnitStrc*){}
bool SKILLS_GetItemStatCostTxtRecord(int s){return s>=0;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int f,int,int r){formulaUnit=p->dwUnitId;return f==1?300+60*(r-1):f==2?-50-2*(r-1):f==3?-25-(r-1):0;}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){updates++;}
void INVENTORY_GetEquippedShield(void*,D2UnitStrc** p){*p=nullptr;}
D2UnitStrc* sub_6FC7C7B0(D2UnitStrc*){return nullptr;}int INVENTORY_GetWieldType(D2UnitStrc*,void*){return 0;}
int ITEMS_GetStrengthBonus(D2UnitStrc*){return 100;}int ITEMS_GetDexBonus(D2UnitStrc*){return 50;}
int SKILLS_GetWeaponMasteryBonus(D2UnitStrc*,D2UnitStrc*,int,int){return 10;}
int ITEMS_RollLimitedRandomNumber(int*,int span){return std::clamp(roll,0,span-1);}
'''
for text,name in [(sunit,'sub_6FCBD900'),(skills,'sub_6FD0FA00'),(skills,'sub_6FD10EC0'),(druid,'sub_6FCFE0E0'),(miss,'MISSMODE_SrvHit21_BattleCry'),(units,'UNITS_GetDefense'),(damage,'SUNITDMG_ApplyDamageBonuses')]:cpp+=routine(text,name)+'\n'
cpp+=r'''int main(){D2UnitStrc owner;owner.dwUnitType=0;owner.dwUnitId=7;owner.room=&room;D2UnitStrc target;target.room=&room;target.dwUnitId=42;D2UnitStrc missile;missile.owner=&owner;
bool first=true;std::printf("{\"replacementCases\":[");
for(int old:{0,1,20,21})for(int rank:{1,20,21})for(int other:{0,1})for(int resist:{-50,0,50,99,100})for(int attract:{0,1}){
 storage={};storage.state=other?56:89;storage.skill=other?77:146;storage.rank=old;storage.creator=999;storage.expire=777;target.list=old?&storage:nullptr;target.attract=attract;target.stats[8]=resist;missile.pSeed=rank;writer=removals=events=0;
 int result=MISSMODE_SrvHit21_BattleCry(&game,&missile,&target);auto* p=target.list;if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",old,rank,other,resist,attract,result,writer,removals,p?p->rank:0,p?p->creator:0,p?p->expire:0,p?p->stats[4]:0,p?p->stats[16]:0,formulaUnit);
}
std::printf("],\"filterCases\":[");first=true;target.attract=false;target.stats[8]=0;
for(int type:{0,1,2,3,4})for(int mode:{0,1,12})for(int flags:{0,1,2,3})for(int town:{0,1})for(int los:{0,1})for(int aligned:{0,1}){
 target.dwUnitType=type;target.dwAnimMode=mode;target.dwFlags=flags;room.town=town;blocked=los;target.aligned=aligned;owner.pvp=!aligned;
 int result=sub_6FD0FA00(&owner,&target,42883);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",type,mode,flags,town,los,aligned,result);
}
target.dwUnitType=1;target.dwAnimMode=1;target.dwFlags=3;room.town=blocked=target.aligned=false;
std::printf("],\"curseGates\":[");first=true;
for(int npc:{0,1})for(int att:{0,1})for(int valid:{0,1}){mon.dwMonStatsFlags=npc?4:0;modes.dwFlags=att;validState=valid;target.list=nullptr;missile.pSeed=1;writer=0;int result=MISSMODE_SrvHit21_BattleCry(&game,&missile,&target);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",npc,att,valid,result,writer);}
std::printf("],\"defenseCases\":[");first=true;
for(int basis:{0,1,47,100,999})for(int bonus:{0,50,200})for(int penalty:{-50,-88,-90,-150})for(int over:{0,-100}){target.stats[STAT_DEXTERITY]=0;target.stats[STAT_ARMORCLASS]=basis;target.stats[STAT_ITEM_ARMOR_PERCENT]=bonus;target.stats[STAT_SKILL_ARMOR_PERCENT]=penalty;target.stats[STAT_ARMOR_OVERRIDE_PERCENT]=over;int result=UNITS_GetDefense(&target);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",basis,bonus,penalty,over,result);}
std::printf("],\"damageCases\":[");first=true;
for(int min:{1,17,103})for(int bonus:{0,50,200})for(int penalty:{0,-25,-44,-50,-150})for(int draw:{0,255,1000}){target.stats.clear();target.stats[STAT_DAMAGEPERCENT]=penalty;roll=draw;int result=SUNITDMG_ApplyDamageBonuses(&target,0,nullptr,min*256,(min+2)*256,bonus,0,128);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",min,bonus,penalty,draw,result);}
std::printf("]}\n");}
'''
path=STAGE/'battlecry-source-probe.cpp';path.write_text(cpp);binary=STAGE/'battlecry-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
for old,rank,other,resist,attract,result,writes,removes,stored,creator,expire,defense,ed,context in data['replacementCases']:
 accept=not attract and resist<100 and (not old or other or rank>=old);replace=accept and (not old or other or rank>old)
 assert result==0 and writes==2*accept and removes==int(bool(old and replace))
 frames=300+60*(rank-1);duration=frames-int(frames*resist/100)
 assert (stored,creator,expire)==((rank,7 if replace else 999,100+duration) if accept else (old,999,777) if old else (0,0,0))
 if accept:assert (defense,ed,context)==(-50-2*(rank-1),-25-(rank-1),42)
for typ,mode,flags,town,los,aligned,result in data['filterCases']:assert result==int(typ in (0,1) and mode==1 and flags==3 and not(town or los or aligned))
for npc,att,valid,result,writes in data['curseGates']:assert result==0 and writes==2*int(not npc and att and valid)
for basis,bonus,penalty,over,result in data['defenseCases']:
 subtotal=basis+int(basis*(bonus+penalty)/100);assert result==subtotal+int(subtotal*over/100)
for minimum,bonus,penalty,draw,result in data['damageCases']:
 pct=max(-90,bonus+penalty);lo=minimum*256+int(minimum*256*pct/100);hi=(minimum+2)*256+int((minimum+2)*256*pct/100)
 assert result==lo+min(draw,hi-lo-1)
(STAGE/'battlecry-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
for text,name in [(miss,'MISSMODE_HandleMissileCollision'),(nec,'sub_6FD0D000'),(nec,'SKILLS_SrvDo055_CorpseExplosion'),(damage,'SUNITDMG_ExecuteMissileDamage')]:routine(text,name)
out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,probe=dict(caseCounts={k:len(v) for k,v in data.items()},blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},dataPath='.local/class-skills/battlecry-source-probe.json'),established=[
'Hit21 replaces nonzero aura filter with42883; compiled filter includes player/monster living/attackable/valid/nontown/LOS and hostile relationship. It does not exclude bosses or prime evils. PvP flag8 and minion ownership are source relationship rules.',
'Full shared curse function compiles: NPC/ISATT/state and curse-resistance gates; Attract protection; one curse lane; lower same-skill rank rejected, equal rank refresh retains original creator, stronger/different curse replaces. Hit21 writer uses target formula context and can update equal-rank list stats.',
'Battle Cry zero-initialized curse writes a harmless zero stat0 entry at allocation; defense/damage stats are then supplied by aura writer. Writer skips zero formula values and mirrors attackrate to other_animrate, with animation refresh.',
'Defense adds item and skill armor percentages before any final armor override. Source can return a negative subtotal; native hit defense remains clamped nonnegative at its existing boundary.',
'Weapon ED is additive with STAT_DAMAGEPERCENT before the global-90 floor, endpoint truncation, random draw and source-damage scaling. Battle Cry, Weaken and Decrepify share this stat; they must not multiply the final physical packet.',
'Corpse Explosion prepares a separate physical spell packet and executes missile damage directly; it does not use ApplyDamageBonuses. Physical spells/retaliation must not inherit weapon ED penalties solely by damage channel.'],stubs=[
'Unchanged compiled hit21/filter/relationship/full curse/aura writer/GetDefense/ApplyDamageBonuses routines with fixture tables, allocation/stat lists, owner/player flags, room/LOS, events, RNG and animation services.',
'Explicit endpoint damage cases use bGetStats=false, no item, SrcDam128. Remaining branches compile but do not receive a comprehensive item/dual-wield/strength/mastery/minmax-stat matrix.',
'Missile collision and physical spell dispatch are hash-checked source reviews; geometry, collision traversal and source final damage engine are not executed by this probe.'],limits=[
'Bounded supplementary engine reconstruction; not modern executable certification. Native projection, town/hostility/LOS adapters and frame/animation/NHD exact scope remain separate acceptance.',
'Nonzero native curse resistance, player PvP, neutral/aligned/minion graphs and unmapped source monster stats require explicit support decisions. No native handler follows from this source-only evidence.'])
(ROOT/'tools/reference/battlecry-engine-evidence.json').write_text(json.dumps(out,indent=2)+'\n');print('Battle Cry bounded source probes passed:',out['probe']['caseCounts'])
