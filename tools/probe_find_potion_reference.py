#!/usr/bin/env python3
"""Compile unchanged Find Potion and shared corpse/diminishing helpers."""
from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
blobs={x['path']:x['sha'] for x in tree['tree'] if x['type']=='blob'};hashes={};blocks={}
def source(path):
 raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blobs[path],path
 hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(text,name):
 m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
 end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 blocks[name]=text[m.start():end];return blocks[name]
bar=source('source/D2Game/src/SKILLS/SkillBar.cpp');common=source('source/D2Common/src/D2Skills.cpp');ordinals=source('source/D2Common/src/UselessOrdinals.cpp');units=source('source/D2Common/src/Units/Units.cpp');header=source('source/D2Common/include/DataTbls/MonsterTbls.h');skills=source('source/D2Game/src/SKILLS/Skills.cpp')
enum=header.split('enum D2C_MonStats2TxtFlagIndices')[1].split('};')[0];names=re.findall(r'^\s*(MONSTATS2FLAGINDEX_\w+)',enum,re.M);assert names.index('MONSTATS2FLAGINDEX_CORPSESEL')==7 and names.index('MONSTATS2FLAGINDEX_SOFT')==12
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <iterator>
#include <initializer_list>
#define __fastcall
#define __stdcall
#define REMOVE_LATER_Trace(x) ((void)0)
using BOOL=int;constexpr int TRUE=1,FALSE=0,UNIT_MONSTER=1,MONMODE_DEAD=12,STATE_CORPSE_NOSELECT=1,MONSTATS2FLAGINDEX_CORPSESEL=7,MONSTATS2FLAG_SOFT=4096;
constexpr uint32_t gdwBitMasks[]={1,2,4,8,16,32,64,128};
struct D2ActiveRoomStrc{int act=0;} room;
struct D2UnitStrc{int dwUnitType=1,dwClassId=1,dwAnimMode=12,pSeed=0;bool noselect=false,udead=false;D2ActiveRoomStrc* room=nullptr;};
struct D2GameStrc{int nDifficulty=0;} game;
struct D2SkillsTxt{int dwCalc[1]={0},dwParam[4]={0,100,30,10};} row;
struct D2PotionCodeStrc{uint32_t nHealthPotionCode,nManaPotionCode,nRejuvPotionCode;};
struct D2MonStats2Txt{uint32_t dwFlags=128|4096;} modes;
bool hasRow=true,hasModes=true;uint32_t draws[2]={0,0},dropped=0;int drops=0,quality=0,refresh=0;
D2UnitStrc* selected=nullptr;D2UnitStrc* dropOwner=nullptr;
D2MonStats2Txt* UNITS_GetMonStats2TxtRecordFromMonsterId(int){return hasModes?&modes:nullptr;}
bool STATES_CheckStateMaskUdeadOnUnit(D2UnitStrc* p){return p->udead;}
bool STATES_CheckState(D2UnitStrc* p,int){return p->noselect;}
void STATES_ToggleState(D2UnitStrc* p,int,int on){p->noselect=on;if(on){p->udead=true;}}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected;}
void UNITROOM_RefreshUnit(D2UnitStrc*){refresh++;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){return SKILLS_GetSkillsTxtRecord(id);}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc* p){return p->room;}
int DUNGEON_GetLevelIdFromRoom(D2ActiveRoomStrc* p){return p->act;}
int DRLG_GetActNoFromLevelId(int id){return id;}
uint32_t ITEMS_RollRandomNumber(int* index){return draws[(*index)++];}
void OBJMODE_DropItemWithCodeAndQuality(D2GameStrc*,D2UnitStrc* owner,uint32_t code,int q){dropped=code;quality=q;dropOwner=owner;drops++;}
'''
for text,name in [(units,'UNITS_IsSoftMonster'),(common,'SKILLS_CanUnitCorpseBeSelected'),(common,'D2Common_11020'),(ordinals,'D2COMMON_11018'),(common,'D2Common_11033'),(common,'D2Common_11034')]:cpp+=routine(text,name)+'\n'
cpp+='int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int skill,int rank){return D2Common_11034(rank,skill);}\n'
for name in ['SKILLS_SrvSt33_FindPotion_GrimWard','SKILLS_SrvDo069_FindPotion']:cpp+=routine(bar,name)+'\n'
cpp+=r'''void Reset(D2UnitStrc& caster,D2UnitStrc& corpse){caster.pSeed=0;corpse.noselect=false;corpse.udead=false;dropped=drops=quality=refresh=0;dropOwner=nullptr;}
int main(){D2UnitStrc caster,corpse;caster.dwUnitType=0;caster.room=&room;selected=&corpse;bool first=true;
std::printf("{\"eligibilityCases\":[");
for(int typ:{-1,0,1,2})for(int mode:{0,1,12})for(int flags:{0,128,4096,4224})for(int undead:{0,1})for(int noselect:{0,1})for(int valid:{0,1}){Reset(caster,corpse);selected=typ<0?nullptr:&corpse;corpse.dwUnitType=typ;corpse.dwAnimMode=mode;corpse.udead=undead;corpse.noselect=noselect;hasModes=valid;modes.dwFlags=flags;int result=SKILLS_SrvSt33_FindPotion_GrimWard(&game,&caster,131,1);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",typ,mode,flags,undead,noselect,valid,result);}
selected=&corpse;corpse.dwUnitType=1;corpse.dwAnimMode=12;corpse.udead=false;hasModes=true;modes.dwFlags=4224;
std::printf("],\"chanceCases\":[");first=true;
for(int rank:{0,1,20,21,48,49,60,255})for(uint32_t draw:{0u,1u,14u,15u,83u,84u,85u,99u,100u,0xFFFFFFFFu}){Reset(caster,corpse);draws[0]=draw;draws[1]=0;int result=SKILLS_SrvDo069_FindPotion(&game,&caster,131,rank),count=caster.pSeed,second=SKILLS_SrvDo069_FindPotion(&game,&caster,131,rank);if(!first)std::printf(",");first=false;std::printf("[%d,%u,%d,%d,%d,%d,%d,%d,%d]",rank,draw,D2Common_11034(rank,131),result,corpse.noselect,drops,count,second,caster.pSeed);}
std::printf("],\"outputCases\":[");first=true;draws[0]=0;
for(int difficulty:{0,1,2})for(int act:{0,1,2,3,4})for(int rank:{1,20,21})for(int kind=0;kind<100;kind++){Reset(caster,corpse);game.nDifficulty=difficulty;room.act=act;draws[1]=kind;int result=SKILLS_SrvDo069_FindPotion(&game,&caster,131,rank);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%u,%d,%d,%d,%d]",difficulty,act,rank,kind,dropped,quality,drops,result,dropOwner==&corpse);}
std::printf("],\"contextCases\":[");first=true;
for(int present:{0,1})for(int act:{-1,0,4,5})for(int difficulty:{0,2,3,255}){Reset(caster,corpse);caster.room=present?&room:nullptr;room.act=act;game.nDifficulty=difficulty;draws[1]=0;int result=SKILLS_SrvDo069_FindPotion(&game,&caster,131,1);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%u,%d,%d]",present,act,difficulty,result,corpse.noselect,dropped,quality,caster.pSeed);}
std::printf("],\"doGuardCases\":[");first=true;caster.room=&room;room.act=0;game.nDifficulty=0;
for(int valid:{0,1})for(int present:{0,1})for(int used:{0,1}){Reset(caster,corpse);hasRow=valid;selected=present?&corpse:nullptr;corpse.noselect=used;int result=SKILLS_SrvDo069_FindPotion(&game,&caster,131,1);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",valid,present,used,result,corpse.noselect,caster.pSeed,drops);}
std::printf("]}\n");}
'''
path=STAGE/'find-potion-source-probe.cpp';path.write_text(cpp);binary=STAGE/'find-potion-source-probe';subprocess.run(['g++','-std=c++20','-Wno-multichar','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
for typ,mode,flags,undead,noselect,valid,result in data['eligibilityCases']:assert result==int(typ==1 and mode==12 and flags==4224 and not undead and not noselect and valid)
for rank,draw,chance,result,used,drops,count,second,finalcount in data['chanceCases']:
 expected=min(100,110*rank//(rank+6)) if rank>0 else 0;ok=draw%100<expected
 assert (chance,result,used,drops,count,second,finalcount)==(expected,1,1,int(ok),2 if ok else 1,0,2 if ok else 1)
rows=[('hp2','mp2','rvs'),('hp3','mp3','rvs'),('hp3','mp3','rvs'),('hp4','mp4','rvl'),('hp4','mp4','rvl'),('hp4','mp4','rvl'),('hp4','mp5','rvl')]+[('hp5','mp5','rvl')]*8
def code(value):return int.from_bytes((value+' ').encode(),'little')
for difficulty,act,rank,kind,dropped,quality,count,result,corpse in data['outputCases']:
 expected=rows[act+5*difficulty][1 if kind<30 else 2 if kind<40 else 0]
 assert (dropped,quality,count,result,corpse)==(code(expected),2,1,1,1)
for present,act,difficulty,result,used,dropped,quality,draws in data['contextCases']:
 valid=present and 0<=act<5 and 0<=act+5*difficulty<15
 assert (result,used,dropped,quality,draws)==(1,1,code(rows[act+5*difficulty][1]) if valid else 0,2,2 if valid else 1)
for valid,present,used,result,noselect,draws,drops in data['doGuardCases']:
 ok=valid and present and not used;assert (result,noselect,draws,drops)==(int(ok),int(used or ok),2 if ok else 0,int(ok))
(STAGE/'find-potion-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
for name in ['sub_6FD12950','D2GAME_SKILLS_Handler_6FD12BA0']:routine(skills,name)
state_rows=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/states.json').read_text());corpse_states=[x for x in state_rows.values() if x.get('state') in ('corpse_noselect','corpse_nodraw')];assert len(corpse_states)==2 and all(x.get('udead')==1 for x in corpse_states)
out=dict(corpseStates=corpse_states,pinnedStateTableSha256=hashlib.sha256((ROOT/'tools/reference/d2r-named-equipment/json/states.json').read_bytes()).hexdigest(),referenceVersion='3.3.93847',legacyCommit=tree['sha'],status='Bounded source proof complete; native potion output context remains an unanswered nonblocking design question.',sourceHashes=hashes,probe=dict(caseCounts={k:len(v) for k,v in data.items()},blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},dataPath='.local/class-skills/find-potion-source-probe.json'),potionRows=[dict(difficulty=i//5,act=i%5,healing=v[0],mana=v[1],rejuvenation=v[2]) for i,v in enumerate(rows)],goldens=[dict(rank=n,chance=min(100,110*n//(n+6)),mana256=512) for n in (1,20,21,60,255)],established=[
'Find Potion/Grim Ward start33 calls the shared dead selectable soft-monster helper and separately rejects corpse_noselect; full helper/wrapper/softness routines compile. The corpse must be monster/dead, source corpseSel flag set, Udead mask clear and soft flag set.',
'Pinned corpse_noselect is an Udead state but has no hide flag; corpse_nodraw has both Udead and hide. They share corpse exclusion while differing in presentation. The fixture toggle reflects the linked Udead mask for subsequent attempts.',
'Do69 marks corpse_noselect and refreshes corpse before its chance roll; failure returns1, consumes the attempt and uses only one RNG draw. A second call refuses with0 before further RNG. The corpse and existing contents are not deleted by these routines.',
'Source diminishing helpers yield chance15/84/85 at ranks1/20/21 and cap100 at60+. Under current Param0/100, the existing native SkillValues diminishing operation is arithmetically identical; this does not certify other parameter pairs.',
'Success uses a second source0..99 roll:30 mana,10 rejuvenation,60 healing outcomes per100. All15 Act/difficulty code rows, all100 type draws and ranks1/20/21 are compiled. Drops are requested from the corpse at source quality2.',
'Missing caster room, invalid Act or output index requests generic item-code0/quality2 after a successful chance, consuming only the first draw. World context comes from the caster room and game difficulty, not corpse level/difficulty.',
'The start/do dispatcher is hash-reviewed separately: Find Potion has source start33, so payment and interruption must follow the successful start path rather than copying no-start warcry post-do payment. No native timing claim follows from this probe.'],stubs=[
'Unchanged corpse/softness/wrapper/diminishing/start33/do69 routines compiled with declared target/room/stat-table/state-mask/refresh/RNG/drop services. MonStats2 flag indexes7/12 are asserted against pinned header enum order.',
'The formula service binds the pinned dm12 field to the compiled shared diminishing helper; complete expression parser/start dispatcher and native item drop implementation are reviewed or outside this executable.',
'Room Act is an explicit supplied fixture value, and item drop records its requested source code/quality/corpse. No Britannia mapping, generic fallback contents, source item generation or modern executable engine is certified.'],limits=[
'No native handler is enabled. Caster Act/game-difficulty mapping to Britannia remains an unanswered question; do not silently use corpse context or a default tier.',
'Native corpse flags/species/source state masks, legacy start/do mana and interruption, item provenance/placement, exact SC/geometry/owner/client and modern source changes remain acceptance work.',
'Final effects and talent/hotkey icons remain independent identified placeholders.'])
(ROOT/'tools/reference/find-potion-engine-evidence.json').write_text(json.dumps(out,indent=2)+'\n');print('Find Potion bounded source probes passed:',out['probe']['caseCounts'])
