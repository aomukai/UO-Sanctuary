#!/usr/bin/env python3
"""Execute complete unchanged cold-state routines with explicit engine services."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
rel='source/D2Game/src/UNIT/SUnitDmg.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
text=raw.decode();blocks={}
def routine(name):
 m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
 end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 body=text[m.start():end];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
pin=ROOT/'tools/reference/weapon-leech-difficulty-source.json';difficultyRaw=pin.read_bytes();assert hashlib.sha1(b'blob '+str(len(difficultyRaw)).encode()+b'\0'+difficultyRaw).hexdigest()=='1bf58cf8273d40a0e79db35a105141e4bf0ddf10'
difficulties=json.loads(difficultyRaw);divisors=[difficulties[x]['MonsterColdDivisor'] for x in ['Normal','Nightmare','Hell']];assert divisors==[1,2,4]
prefix=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <map>
#define __fastcall
#define D2_MAYBE_UNUSED(x) (void)(x)
constexpr int UNIT_MONSTER=1,STATE_COLD=11,STATE_SHATTER=16,STAT_VELOCITYPERCENT=1,STAT_ATTACKRATE=2,STAT_OTHER_ANIMRATE=3,EVENTTYPE_REMOVESTATE=1,MONSTATS2FLAGINDEX_DEADCOL=0;
unsigned gdwBitMasks[]={1};
struct D2GameStrc{int nDifficulty=0,dwGameFrame=1000;void* pMemoryPool=nullptr;};
struct D2StatListStrc{int expiry=0;std::map<int,int> stats;};
struct Seed{int value=0,calls=0;};
struct D2UnitStrc{int dwUnitType=1,dwClassId=0,dwUnitId=17;D2GameStrc* pGame;Seed pSeed;bool cold=false,shatter=true,dead=false,stay=false;D2StatListStrc list;};
struct D2MonStatsTxt{int nColdEffect[3];};struct D2MonStats2Txt{unsigned dwFlags=0;};struct Difficulty{int dwMonsterColdDiv;};
D2GameStrc game;D2UnitStrc attacker,defender;D2MonStatsTxt mon;D2MonStats2Txt mon2;Difficulty difficulty[3]={{1},{2},{4}};bool known=true;int animations=0;
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return known?&mon:nullptr;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return &mon2;}
Difficulty* DATATBLS_GetDifficultyLevelsTxtRecord(int d){return &difficulty[d];}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* u,int){return u->cold?&u->list:nullptr;}
int STATLIST_GetExpireFrame(D2StatListStrc* l){return l->expiry;}
void STATLIST_SetExpireFrame(D2StatListStrc* l,int at){l->expiry=at;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){}
void STATES_ToggleState(D2UnitStrc* u,int state,int set){(state==STATE_COLD?u->cold:u->shatter)=set;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int at,int,int){defender.list.expiry=at;return &defender.list;}
void STATLIST_SetState(D2StatListStrc*,int){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*)){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}
void STATLIST_SetStatIfListIsValid(D2StatListStrc* l,int stat,int value,int){l->stats[stat]=value;}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){animations++;}
unsigned ITEMS_RollRandomNumber(Seed* s){s->calls++;return s->value;}
bool SUNIT_IsDead(D2UnitStrc* u){return u->dead;}
bool STATES_CheckStateMaskStayDeathOnUnitByStateId(D2UnitStrc* u,int){return u->stay;}
'''
cpp=prefix+'\n'+routine('SUNITDMG_GetColdEffect')+'\n'+routine('SUNITDMG_RemoveShatterState')+'\n'+routine('SUNITDMG_ApplyColdState')+r'''
int main(){bool first=true;std::printf("{\"state\":[");
for(int type:{0,1,2})for(int diff:{0,1,2})for(int effect:{-50,-25,0,10,33})for(int frames:{-1,0,1,2,3,99,100,101,201,500})for(int existing:{0,1})for(int roll:{0,19,20,99})for(int deadcol:{0,1}){
 game={};game.nDifficulty=diff;attacker={};attacker.pGame=&game;defender={};defender.pGame=&game;defender.dwUnitType=type;defender.pSeed.value=roll;defender.cold=existing;defender.list.expiry=existing?1200:0;
 if(existing){defender.list.stats[STAT_VELOCITYPERCENT]=defender.list.stats[STAT_ATTACKRATE]=defender.list.stats[STAT_OTHER_ANIMRATE]=-17;}
 mon.nColdEffect[diff]=effect;mon2.dwFlags=deadcol;animations=0;SUNITDMG_ApplyColdState(&attacker,&defender,frames);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",type,diff,effect,frames,existing,roll,deadcol,defender.cold,defender.list.expiry,defender.list.stats[STAT_VELOCITYPERCENT],defender.list.stats[STAT_ATTACKRATE],defender.list.stats[STAT_OTHER_ANIMRATE],defender.shatter,defender.pSeed.calls);
}std::printf("],\"remove\":[");first=true;
for(int dead:{0,1})for(int stay:{0,1}){defender={};defender.dead=dead;defender.stay=stay;defender.cold=true;defender.shatter=true;animations=0;SUNITDMG_RemoveShatterState(&defender,STATE_COLD,&defender.list);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",dead,stay,defender.cold,defender.shatter,animations);}
std::printf("]}\n");}
'''
path=STAGE/'cold-state-source-probe.cpp';path.write_text(cpp);binary=STAGE/'cold-state-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data['state'])==7200 and len(data['remove'])==4
fixture=STAGE/'cold-state-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
source=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/monstats.json').read_text());effects={r.get(k,0) for r in source.values() for k in ['coldeffect','coldeffect(N)','coldeffect(H)']}
out=ROOT/'tools/reference/cold-state-engine-evidence.json';evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledRoutines=list(blocks),blockHashes=blocks,difficultySource=dict(path=str(pin.relative_to(ROOT)),gitBlob='1bf58cf8273d40a0e79db35a105141e4bf0ddf10',sha256=hashlib.sha256(difficultyRaw).hexdigest(),divisors=divisors),positiveCurrentSources=[r['Id'] for r in source.values() if any(r.get(k,0)>0 for k in ['coldeffect','coldeffect(N)','coldeffect(H)'])],probe=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),stateCases=7200,removalCases=4),established=['Complete unchanged cold state application/getter/removal execute, preserving source monster-only difficulty division, minimum frame, existing-state strength/expiry and shared shatter reroll/removal order.','Valid application draws defender RNG even for nonmonsters; shatter requires the source threshold and monster deadcol flag. Ordinary cold and Holy Freeze have distinct roll policies.','Pinned current Hell Frozen Horror rows contain positive33 cold effect: treating every cold effect as an absolute slow would be incorrect. Source preserves the signed velocity/attack/other rates.'],limits=['Stat-list/event/animation/seed/mode services are explicit isolated adapters, not a whole-engine or modern-executable certificate. Supplied cold length is already mitigated; pre-resistance/MDR/freeze-item order needs separate review.','Death stay-state mask is supplied in the removal harness. Pinned state masks are recorded in cold-corpse-reference-gap.json; exact native death/corpse/owner/animation/seed interactions remain independent acceptance work.'])
encoded=json.dumps(evidence,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Cold source evidence stale'
else:out.write_text(encoded)
print('Cold state:7200 application/getter cases and4 complete removal cases compiled from unchanged routines.')
