#!/usr/bin/env python3
"""Compile pinned Battle Command rank, passive refresh, expiry and pet-cap contracts."""
from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';BASE=STAGE/'d2moo-pinned-source'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
blobs={x['path']:x['sha'] for x in tree['tree'] if x['type']=='blob'};hashes={}
def source(path):
 raw=(BASE/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blobs[path],path
 hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(text,name):
 m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
 end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[m.start():end]
common=source('source/D2Common/src/D2Skills.cpp');pets=source('source/D2Game/src/PLAYER/PlayerPets.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp');warcry=source('source/D2Game/src/SKILLS/SkillBar.cpp')
blocks={name:routine(text,name) for text,names in [(common,['SKILLS_GetBonusSkillLevel','SKILLS_GetSkillLevel','SKILLS_RefreshPassiveSkills']),(pets,['sub_6FC7D150','sub_6FC7DF40']),(skills,['sub_6FD14C30','sub_6FD10E50'])] for name in names}
apply=routine(warcry,'SKILLS_ApplyWarcryStats');assert 'sub_6FD14C30(a2);' in apply
at=pets.index('    D2PetDataStrc* pPrevious = pPetList->pPetData;');end=pets.index('    ++pPetList->nCurrent;',at);append=pets[at:end]
cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_ASSERT(x) assert(x)
#define D2_MAYBE_UNUSED(x) (void)x
using BOOL=int;constexpr int FALSE=0,TRUE=1,D2UnitInvalidGUID=-1,UNIT_PLAYER=0,UNIT_MONSTER=1,STATE_SHRINE_SKILL=50,PETTYPE_SINGLE=1,PETTYPE_HIREABLE=2;
enum {STAT_ITEM_ALLSKILLS,STAT_ITEM_ADDCLASSSKILLS,STAT_ITEM_ADDSKILL_TAB,STAT_ITEM_NONCLASSSKILL,STAT_ITEM_ELEMSKILL,STAT_ITEM_SINGLESKILL};
struct D2SkillDescTxt{int nSkillPage=1;};
struct D2SkillsTxt{int nSkillId=0,nCharClass=4,wSkillDesc=0,nEType=0;short nPassiveState=1;int nPetType=0,dwPetMax=0;};
struct D2SkillStrc{D2SkillsTxt* pSkillsTxt=nullptr;int nSkillLevel=0,nLevelBonus=0,nOwnerGUID=-1;D2SkillStrc* next=nullptr;};
struct SkillList{D2SkillStrc* first=nullptr;};struct D2PetDataStrc{int nUnitGUID=0;};
struct D2PetListStrc{int nMax=0,nCurrent=0;D2PetDataStrc* pPetData=nullptr;};
struct D2PlayerPetStrc{D2PetListStrc lists[8];};struct D2PlayerDataStrc{D2PlayerPetStrc* pPlayerPets=nullptr;};
struct D2GameStrc{} game;struct D2StatListStrc{};
struct D2UnitStrc{int dwUnitType=0,dwClassId=4;SkillList* pSkills=nullptr;D2PlayerDataStrc* player=nullptr;int stats[6]={};bool shrine=false,dead=false,stay=false,active=true;};
struct D2PetTypeTxt{int wBaseMax=0;};D2SkillsTxt rows[8];D2PetTypeTxt petrows[8];int passiveids[]={0,1,2};
struct Tables{int nPassiveSkills=3;int* pPassiveSkills=passiveids;D2SkillsTxt* pSkillsTxt=rows;int nPetTypeTxtRecordCount=8;D2PetTypeTxt* pPetTypeTxt=petrows;} tables;Tables* sgptDataTables=&tables;
D2SkillDescTxt desc;std::vector<int> refreshed,removed,events;
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){return id>=0&&id<8?&rows[id]:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return DATATBLS_GetSkillsTxtRecord(id);}
D2SkillDescTxt* DATATBLS_GetSkillDescTxtRecord(int){return &desc;}
int DATATBLS_GetMaxLevel(int){return 99;}
bool STATES_CheckState(D2UnitStrc* p,int id){return id==50?p->shrine:p->active;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int id,int){return p?p->stats[id]:0;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromSkillId(D2UnitStrc* p,int id){for(auto* s=p->pSkills?p->pSkills->first:nullptr;s;s=s->next)if(s->pSkillsTxt->nSkillId==id)return s;return nullptr;}
void SKILLS_RefreshSkill(D2UnitStrc*,int id){refreshed.push_back(id);events.push_back(4);}
D2PlayerDataStrc* UNITS_GetPlayerData(D2UnitStrc* p){return p->player;}
D2SkillStrc* SKILLS_GetFirstSkillFromSkillList(SkillList* p){return p->first;}
D2SkillStrc* SKILLS_GetNextSkill(D2SkillStrc* p){return p->next;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc* p,const char*,int){return p->pSkillsTxt->nSkillId;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int level){return formula==1?level:formula==2?(level<4?level:2+level/3):formula==3?1:0;}
D2PetListStrc* PLAYERPETS_GetPetListFromPetType(D2PlayerPetStrc* p,int type){return type>=0&&type<8?&p->lists[type]:nullptr;}
void sub_6FC7D260(D2GameStrc*,D2UnitStrc* p,int id,int){for(auto& l:p->player->pPlayerPets->lists)if(l.pPetData&&l.pPetData->nUnitGUID==id){removed.push_back(id);l.nCurrent--;return;}}
D2GameStrc* SUNIT_GetGameFromUnit(D2UnitStrc*){return &game;}
void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int,int){events.push_back(1);}
bool SUNIT_IsDead(D2UnitStrc* p){return p->dead;}
bool STATES_CheckStateMaskStayDeathOnUnitByStateId(D2UnitStrc* p,int){return p->stay;}
void STATES_ToggleState(D2UnitStrc* p,int,bool v){p->active=v;events.push_back(2);}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){events.push_back(3);}
'''+ '\n'.join(blocks[n] for n in ['SKILLS_GetBonusSkillLevel','SKILLS_GetSkillLevel','SKILLS_RefreshPassiveSkills','sub_6FC7D150','sub_6FC7DF40','sub_6FD14C30','sub_6FD10E50'])+r'''
int main(){for(int i=0;i<8;i++){rows[i].nSkillId=i;petrows[i].wBaseMax=0;}
std::printf("{\"rankCases\":[");bool first=true;
for(int type:{0,1})for(int base:{0,1,20})for(int levelbonus:{0,2})for(int all:{0,1,3})for(int own:{-1,9}){
 D2UnitStrc unit;unit.dwUnitType=type;unit.stats[0]=all;D2SkillStrc s{&rows[0],base,levelbonus,own};int result=SKILLS_GetSkillLevel(&unit,&s,TRUE);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",type,base,levelbonus,all,own,result);
}std::printf("],\"petCases\":[");first=true;
for(int rank:{1,2,3,5,6,20})for(int all:{0,1})for(int count:{0,1,3,4,10}){
 D2UnitStrc unit;unit.stats[0]=all;D2PlayerPetStrc pets;D2PlayerDataStrc player{&pets};unit.player=&player;
 D2SkillStrc s{&rows[0],rank};SkillList list{&s};unit.pSkills=&list;rows[0].nPetType=3;rows[0].dwPetMax=2;D2PetDataStrc head{10};pets.lists[3]={0,count,&head};removed.clear();sub_6FC7DF40(&game,&unit);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%zu]",rank,all,count,pets.lists[3].nMax,pets.lists[3].nCurrent,removed.size());
}std::printf("],\"applyRefreshCases\":[");first=true;
rows[0].nPetType=0;
for(int type:{0,1})for(int active:{0,1})for(int passive:{0,1})for(int exists:{0,1}){
 D2UnitStrc unit;unit.dwUnitType=type;unit.active=active;D2SkillStrc s{&rows[0],1};SkillList list{exists?&s:nullptr};unit.pSkills=&list;rows[0].nPassiveState=passive;refreshed.clear();events.clear();sub_6FD14C30(&unit);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%zu]",type,active,passive,exists,refreshed.size());
}std::printf("],\"refreshCases\":[");first=true;
rows[0].nPetType=0;rows[0].nPassiveState=1;rows[1].nPassiveState=2;
for(int type:{0,1})for(int active:{0,1})for(int dead:{0,1})for(int stay:{0,1}){
 D2UnitStrc unit;unit.dwUnitType=type;unit.active=active;unit.dead=dead;unit.stay=stay;D2SkillStrc s{&rows[0],1};SkillList list{&s};unit.pSkills=&list;refreshed.clear();events.clear();sub_6FD10E50(&unit,3,nullptr);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%zu,%zu]",type,active,dead,stay,events.size(),refreshed.size());
}std::printf("]}\n");}
'''
path=STAGE/'battle-command-source-probe.cpp';path.write_text(cpp);binary=STAGE/'battle-command-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
for kind,base,bonus,all,own,result in data['rankCases']:assert result==base+(bonus+all if own==-1 else 0)
for rank,all,count,maximum,current,removed in data['petCases']:
 level=rank+all;expected=max(1,level if level<4 else 2+level//3)
 assert (maximum,current,removed)==(expected,min(count,expected),max(0,count-expected))
for kind,active,passive,exists,refreshed in data['applyRefreshCases']:assert refreshed==int(bool(active and passive and exists))
for kind,active,dead,stay,events,refreshed in data['refreshCases']:assert events==(1 if dead and stay else 3) and refreshed==0
(STAGE/'battle-command-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,probe=dict(caseCounts={k:len(v) for k,v in data.items()},blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in {**blocks,'applyWarcry':apply,'petAppend':append}.items()},dataPath='.local/class-skills/battle-command-source-probe.json'),established=[
'Battle Command contributes item_allskills1; effective levels add it to existing own skill nodes, while hard levels and item-owned charged skill nodes stay unchanged.',
'Warcry application refreshes active passives and player pet caps. Its removal callback unregisters events, removes state, updates animation/movement, refreshes active passives and re-evaluates player pet caps unless a stay-on-death state is retained.',
'Player pet counters take the greatest formula for each shared pet type with a minimum1; types with no skill fall back to baseMax. Overflow removes the linked-list head; source pet append walks to the tail, establishing FIFO creation order.',
'Pinned Battle Command parameters remain independent from legacy source and are consumed through the checked modern table/formula compiler.'],stubs=[
'Whole source bonus-level/get-level/passive-refresh/pet-counter/pet-limit/refresh-wrapper/removal functions are compiled. Table records, skill-list enumeration, stat arrays, states, event/network/animation and pet removal are declared fixture stubs.',
'Pet fixture formulas supply rank-based skeleton cap; full game formula bytecode is verified elsewhere. Pet removal stub decrements counts only; FIFO ordering is source-inspected at append/removal, with native creation-order tests separate.',
'Expiry cases assert the callback removes state before passive refresh, so the isolated stub reports no active passive after removal. No complete source animation/event/stat dependency engine is claimed.'],limits=[
'Supplementary reconstruction is not a modern executable certificate. Native NPC innate/charged skills, modern snapshots/rank-cap/owner/item/client transitions, exact SC/FCR and missile geometry/NextHit scope remain open.',
'Native retained class histories are approved departures. Unsupported Revive/Iron Golem and future-class pet types must stay individually gated; existing NPCs without modeled own skills cannot receive an invented skill system.',
'Original creation order of pre-ledger summons is unknowable; native historical serial fallback must be identified separately from new saved FIFO order.'])
(ROOT/'tools/reference/battle-command-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n');print('Battle Command bounded probes passed:',evidence['probe']['caseCounts'])
