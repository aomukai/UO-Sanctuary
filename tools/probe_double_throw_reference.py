#!/usr/bin/env python3
"""Compile pinned Double Throw emission, server hand and event-dispatch contracts."""
from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
hashes={};blocks={}
def source(path):
 raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();blob=next(v['sha'] for v in tree['tree'] if v['path']==path)
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blob
 hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(text,name):
 m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
 end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 blocks[name]=text[m.start():end];return blocks[name]+'\n'
bar=source('source/D2Game/src/SKILLS/SkillBar.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp');player=source('source/D2Game/src/PLAYER/Player.cpp');modes=source('source/D2Game/src/PLAYER/PlrModes.cpp');units=source('source/D2Common/src/Units/Units.cpp');sunit=source('source/D2Game/src/UNIT/SUnit.cpp');seq=source('source/D2Common/src/DataTbls/SequenceTbls.cpp')
sequence=re.search(r'D2AnimSeqTxt gPlayerSequenceDoubleThrow\[12\] =\s*\{(.*?)\n\};',seq,re.S).group(0)
entries=re.findall(r'\{\s*0,\s*(PLRMODE_\w+),\s*(\d+),\s*0,\s*(ANIMSEQ_EVENT_\w+)\s*\}',sequence)
events=[{'point':i,'mode':mode,'animationFrame':int(frame)} for i,(mode,frame,event) in enumerate(entries) if event=='ANIMSEQ_EVENT_MELEE_ATTACK']
assert events==[{'point':5,'mode':'PLRMODE_THROW','animationFrame':8},{'point':9,'mode':'PLRMODE_SPECIAL3','animationFrame':7}],events
cpp=r'''#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <iterator>
#include <vector>
#include <array>
#include <initializer_list>
#define __fastcall
#define __stdcall
#define D2_ASSERT(x) if(!(x))std::abort()
using BOOL=int;
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNITFLAG_SKSRVDOFUNC=64,PLRMODE_TOWNNEUTRAL=5,PLRMODE_SEQUENCE=18,
 SKILLSFLAGINDEX_INTOWN=8,SKILLSFLAGINDEX_LOB=1,SKILLSFLAGINDEX_DECQUANT=0,SKILLSFLAGINDEX2_USEMANAONDO=5,
 EVENTTYPE_PERIODICSKILLS=1,ITEMTYPE_MISSILE_POTION=2,ITEMTYPE_WEAPON=1,STAT_TOHIT=0,STAT_DAMAGEPERCENT=1;
constexpr unsigned gdwBitMasks[]={1,2,4,8,16,32,64,128,256};
struct D2GameStrc{int dwGameFrame=100;} game;
struct D2AnimSeqTxt{int id=0,mode=0,frame=0,dir=0,nEvent=0;};
struct AnimData{int pFrameFlags[144]={};} animData;
std::vector<std::array<int,4>> scheduled;
constexpr int PLRMODE_THROW=12,PLRMODE_SPECIAL3=15,ANIMSEQ_EVENT_NONE=0,ANIMSEQ_EVENT_MELEE_ATTACK=1,EVENTTYPE_MODECHANGE=8,EVENTTYPE_ENDANIM=9;
struct D2ActiveRoomStrc{} room;
struct D2SkillsTxt{int nWeapSel=3,wSrvDoFunc=74,wSrvStartFunc=0,wItemEffect=0,wSrvMissile=-1,nAnim=18,dwDelay=0,dwCalc[1]={0};unsigned dwFlags[2]={0,0};} row;
struct D2SkillStrc{int flags=0;} skill;
struct D2UnitStrc{void* pInventory=(void*)1;void* pDynamicPath=(void*)1;void* pAnimSeq=nullptr;AnimData* pAnimData=&animData;int dwSeqFrameCount=3072,dwSeqSpeed=256,wAnimSpeed=256,dwFrameCountPrecise=3072,nSeqCurrentFramePrecise=0;unsigned dwSeqFrame=0,dwFlags=0;int dwUnitType=0,id=0,missile=1,mana=4,quantity=1;bool weapon=true,equippable=true,potion=false;int stats[2]={};} owner,rightHand,leftHand,fallback,missileUnit;
struct D2MissilesTxt{} missileRecords[10];
struct Tables{int nMissilesTxtRecordCount=10;D2MissilesTxt* pMissilesTxt=missileRecords;} tables;
auto* sgptDataTables=&tables;
bool record=true,used=true,dual=true,created=true,inTown=false;int hands=3,emits=0,lastHand=0,lob=0,flat=0,costCalls=0,costPaid=0,attempts=0,toHit=20,damage=16;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return record?&row:nullptr;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return used?&skill:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecordFromSkill(D2SkillStrc*){return record?&row:nullptr;}
int SKILLS_GetFlags(D2SkillStrc* p){return p->flags;}
void SKILLS_SetFlags(D2SkillStrc* p,int value){p->flags=value;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc*,const char*,int){return 140;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int){return 1;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return used?&skill:nullptr;}
void UNITS_SetUsedSkill(D2UnitStrc*,D2SkillStrc*){used=false;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return inTown;}
bool sub_6FD10CE0(D2UnitStrc*){return owner.mana>=1;}
int sub_6FC817D0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int){return 0;}
bool UNITS_CanDualWield(D2UnitStrc*){return dual;}
void INVENTORY_GetEquippedWeapon(void*,D2UnitStrc** item,int* loc,int* left){*item=&fallback;}
D2UnitStrc* INVENTORY_GetCompositItem(void*,int hand){return hand==5?(hands&1?&rightHand:nullptr):(hands&2?&leftHand:nullptr);}
bool ITEMS_CheckItemTypeId(D2UnitStrc* p,int type){return p && (type==ITEMTYPE_MISSILE_POTION?p->potion:p->weapon);}
bool ITEMS_CanBeEquipped(D2UnitStrc* p){return p && p->equippable;}
int ITEMS_GetMissileType(D2UnitStrc* p){return p->missile;}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int){return toHit;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return damage;}
void STATLIST_AddUnitStat(D2UnitStrc* p,int stat,int value,int){p->stats[stat]+=value;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){}
int D2GAME_SKILLMANA_Consume_6FD10A50(D2GameStrc*,D2UnitStrc* p,int,int){costCalls++;if(p->mana<1)return 0;p->mana--;costPaid++;return 1;}
int sub_6FD118C0(D2GameStrc*,D2UnitStrc*){std::abort();}
void D2GAME_SKILLS_SetDelay_6FD11C00(D2GameStrc*,D2UnitStrc*,int){}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return nullptr;}
int CLIENTS_GetUnitX(D2UnitStrc*){return 100;}
int CLIENTS_GetUnitY(D2UnitStrc*){return 100;}
int D2COMMON_10175_PathGetFirstPointX(void*){return 110;}
int D2COMMON_10176_PathGetFirstPointY(void*){return 100;}
void sub_6FCBC7E0(D2GameStrc*,D2UnitStrc*){}
int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){return 1;}
bool SUNIT_IsDead(D2UnitStrc*){return false;}
void sub_6FD13260(D2GameStrc*,D2UnitStrc*,int,int,int){}
int UNITS_GetFrameBonus(D2UnitStrc*){return 0;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int arg,int ordinal){scheduled.push_back({type,frame,arg,ordinal});}
using SkillDoFunc=int(*)(D2GameStrc*,D2UnitStrc*,int,int);
SkillDoFunc gpSkillSrvDoFnTable_6FD40A20[100]={},gpSkillSrvStartFnTable_6FD408B0[100]={};
D2UnitStrc* sub_6FC7C7B0(D2UnitStrc*);
D2UnitStrc* Create(D2UnitStrc* p,bool isLob){attempts++;auto* item=sub_6FC7C7B0(p);lastHand=item?item->id:0;lob+=isLob;flat+=!isLob;if(!item || item->quantity<=0)return nullptr;item->quantity--;if(!created)return nullptr;emits++;missileUnit.stats[0]=100;missileUnit.stats[1]=50;return &missileUnit;}
D2UnitStrc* sub_6FD11420(D2GameStrc*,int,D2UnitStrc* p,int,int,int,int,int,int,int check){if(check!=1)std::abort();return Create(p,false);}
D2UnitStrc* sub_6FD11710(D2GameStrc*,int,D2UnitStrc* p,int,int,int,int,int,int,int check){if(check!=1)std::abort();return Create(p,true);}
'''
for text,name in [(units,'UNITS_SetAnimationFrame'),(player,'sub_6FC7C7B0'),(bar,'SKILLS_SrvDo074_DoubleThrow'),(skills,'D2GAME_SKILLS_Handler_6FD12BA0'),(skills,'sub_6FD13410'),(modes,'sub_6FC80EE0'),(seq,'DATATBLS_GetSequenceEvent'),(sunit,'sub_6FCBCE70')]:cpp+=routine(text,name)
cpp+=sequence+'\n'
cpp+=r'''void reset(){owner={};rightHand={};leftHand={};fallback={};rightHand.id=1;leftHand.id=2;fallback.id=3;row={};skill={};record=used=dual=created=true;inTown=false;hands=3;emits=lastHand=lob=flat=costCalls=costPaid=attempts=0;missileUnit.stats[0]=missileUnit.stats[1]=0;}
int main(){reset();owner.pAnimSeq=gPlayerSequenceDoubleThrow;sub_6FCBCE70(&game,&owner);if(scheduled.size()!=3 || scheduled[0]!=std::array<int,4>{8,105,1,0} || scheduled[1]!=std::array<int,4>{8,109,1,1} || scheduled[2]!=std::array<int,4>{9,112,0,0})std::abort();gpSkillSrvDoFnTable_6FD40A20[74]=SKILLS_SrvDo074_DoubleThrow;bool first=true;std::printf("{\"doCases\":[");
for(int present:{0,1})for(int hand:{0,1})for(int missile:{-1,0,1,9,10,11})for(int potion:{0,1})for(int allocation:{0,1}){
 reset();record=present;hands=hand;owner.pInventory=hand?(void*)1:nullptr;rightHand.missile=missile;rightHand.potion=potion;fallback.missile=missile;fallback.potion=potion;created=allocation;
 int result=SKILLS_SrvDo074_DoubleThrow(&game,&owner,140,1);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",present,hand,missile,potion,allocation,result,attempts,emits,lob,missileUnit.stats[0],missileUnit.stats[1]);}
 std::printf("],\"cycleCases\":[");first=true;
for(int mana:{0,1,2,4})for(int qty1:{0,1})for(int qty2:{0,1})for(int allocations:{0,1,2,3})for(int town:{0,1}){
 reset();owner.mana=mana;owner.dwSeqFrame=123;rightHand.quantity=qty1;leftHand.quantity=qty2;inTown=town;
 created=allocations&1;int r1=sub_6FC80EE0(&game,&owner,scheduled[0][2],scheduled[0][3]),firstHand=lastHand;
 created=allocations&2;int r2=sub_6FC80EE0(&game,&owner,scheduled[1][2],scheduled[1][3]);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",mana,qty1,qty2,allocations,town,r1,r2,firstHand,lastHand,attempts,emits,costCalls,costPaid,owner.mana,owner.dwSeqFrame);
}std::printf("]}\n");}
'''
p=STAGE/'double-throw-source-probe.cpp';p.write_text(cpp);binary=STAGE/'double-throw-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
for present,hand,missile,potion,allocation,result,attempts,emits,lob,ar,ed in data['doCases']:
 valid=bool(present and hand and 0<missile<10)
 assert (result,attempts,emits,lob,ar,ed)==(int(valid),int(valid),int(valid and allocation),int(valid and potion),120 if valid and allocation else 0,66 if valid and allocation else 0)
for mana,q1,q2,allocations,town,r1,r2,h1,h2,attempts,emits,calls,paid,left,phase in data['cycleCases']:
 assert (r1,r2)==((1,2) if town else (1,1))
 assert (h1,h2,attempts,emits,calls,paid,left,phase)==((0,0,0,0,0,0,mana,123) if town else (1,2,2,int(bool(q1 and allocations&1))+int(bool(q2 and allocations&2)),2,min(mana,2),max(0,mana-2),(1<<8)+123)),(mana,q1,q2,allocations,town,h1,h2,attempts,emits,calls,paid,left,phase)
data_path=STAGE/'double-throw-source-probe.json';data_path.write_text(json.dumps(data,indent=2)+'\n')
modern_path=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';modern=next(v for v in json.loads(modern_path.read_text()).values() if v['skill']=='Double Throw')
assert modern['weapsel']==3 and modern['srvdofunc']==74 and not modern.get('srvstfunc')
evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,modernTableHash=hashlib.sha256(modern_path.read_bytes()).hexdigest(),probe=dict(totalCases=sum(map(len,data.values())),caseCounts={k:len(v) for k,v in data.items()},dataPath=str(data_path.relative_to(ROOT)),compiledRoutines=list(blocks),blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},sequenceEvents=events,sequenceHash=hashlib.sha256(sequence.encode()).hexdigest()),established=[
'Do74 selects the server weapon, validates its missile ID and calls quantity-checked flat or potion creation. A created missile receives additive skill ToHit and damage-percent stats. Failed allocation still returns1; invalid record/item/missile returns0.',
'SQ15 has12 points with events at5 and9. The compiled scheduler supplies event ordinals0 and1, not the table art frames8/7 or raw point numbers. Player dispatch writes those ordinals into dwSeqFrame while preserving its fractional byte; server weapsel3 consequently selects primary then secondary. End animation schedules at12 for base speed256/frame bonus0.',
'The unchanged no-start wrapper invokes mana consumption after each successful do74, including quantity/allocation failure returning1. The wrapper ignores consumption failure. The declared1-mana consume service records2 calls per cycle; modern low-mana/resource outcome remains acceptance work.'
],limits=[
'Unchanged eight routines are compiled with declared inventory/equip/formula/mana/quantity/missile/allocation/target/town/periodic services. The fixture proves dispatch and return order, not full ammo creation or modern payment arithmetic.',
'Current pinned data has1mana, weapsel3, two thro requirements, UseAttackRate and updated16+8-per-rank damage with hard Double Swing8% synergy. Exact modern sequence/IAS/start/low-mana/payment, missile snapshots/procs/travel/owner/client remain acceptance work.',
'Missile-potion path is source-covered but native throwable potions lack an approved item adapter. Existing native throwing quantity/conservation/critical/pierce services retain their recorded independent limits.'
])
(ROOT/'tools/reference/double-throw-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print('Double Throw source cases:',evidence['probe']['totalCases'])
