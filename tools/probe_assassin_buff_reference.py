#!/usr/bin/env python3
"""Execute unchanged defensive-buff dispatch/group/stat writers with declared statlist/formula services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 ref=ROOT/'tools/reference/d2r-named-equipment';meta=json.loads((ref/'sources.json').read_text());pins={};tables={}
 for name in ['states','skills']:
  raw=(ref/'json'/f'{name}.json').read_bytes();pins[name]=hashlib.sha256(raw).hexdigest();assert pins[name]==meta['files']['json/'+name+'.json']['sha256'];tables[name]=json.loads(raw)
 assert {(x['state'],x['*ID']) for x in tables['states'].values() if x.get('group')==2}=={('quickness',157),('fade',159)}
 assert tables['states']['venomclaws']['*ID']==31 and not tables['states']['venomclaws'].get('group') and tables['skills']['258']['srvdofunc']==18

 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^\w[^;\n]*\b'+name+r'\([^;{]*?\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  block=text[m.start():i];blocks[name]=hashlib.sha256(block.encode()).hexdigest();return block+'\n'
 sor=source('source/D2Game/src/SKILLS/SkillSor.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp');druid=source('source/D2Game/src/SKILLS/SkillDruid.cpp')
 group=routine(skills,'sub_6FD11C90');writer=routine(druid,'sub_6FCFE0E0');passive=routine(druid,'sub_6FCFE200');callback=routine(sor,'SKILLS_CurseStateCallback_DefensiveBuff');dispatch=routine(sor,'SKILLS_SrvDo018_DefensiveBuff')
 cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <map>
#define __fastcall
#define D2_MAYBE_UNUSED(x) (void)(x)
constexpr int UNIT_PLAYER=0,UNITFLAG_SKSRVDOFUNC=8,STAT_ATTACKRATE=1,STAT_OTHER_ANIMRATE=2,STAT_MODIFIERLIST_SKILL=3,STAT_MODIFIERLIST_LEVEL=4;
using D2C_UnitEventTypes=int;
struct D2UnitStrc;struct D2StatListStrc;struct D2GameStrc{int dwGameFrame=400;}game;
using Callback=void(*)(D2UnitStrc*,int,D2StatListStrc*);
struct D2StatListStrc{int state,rank,skill,expiry;std::map<int,int> stats;Callback callback=nullptr;};
struct D2UnitStrc{int dwFlags=0,dwUnitType=UNIT_PLAYER;bool dead=false;bool active[200]={};D2GameStrc* pGame=&game;std::map<int,D2StatListStrc*> lists;};
struct D2StatesTxt{int wGroup=0;}states[200];
struct Tables{int nStatesTxtRecordCount=200,nItemStatCostTxtRecordCount=16;int pItemStatCostTxt[16]={};}tables;auto* sgptDataTables=&tables;
struct D2SkillsTxt{int nAuraState=157,dwAuraLenCalc=0,wAuraStat[6]={0,1,-1,-1,-1,-1},dwAuraStatCalc[6]={1,2,0,0,0,0},nPassiveStat[5]={-1,-1,-1,-1,-1},dwPassiveCalc[5]={},wAuraEvent[3]={-1,-1,-1},wAuraEventFunc[3]={};}skill;
struct D2CurseStrc{D2UnitStrc* pUnit=nullptr;D2UnitStrc* pTarget=nullptr;int nSkill=0,nSkillLevel=0,nStat=0,nDuration=0,nState=0;Callback pStateRemoveCallback=nullptr;};
int values[3],guard=0,frees=0,updates=0,refreshes=0,unregisters=0,gfx=0,factory=0;bool stay=false;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return guard==1?nullptr:&skill;}
D2StatesTxt* SKILLS_GetStatesTxtRecord(int state){return state>=0 && state<200?&states[state]:nullptr;}
bool STATES_CheckState(D2UnitStrc* p,int state){return p->active[state];}
void STATES_ToggleState(D2UnitStrc* p,int state,int value){p->active[state]=value;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* p,int state){auto it=p->lists.find(state);return it==p->lists.end()?nullptr:it->second;}
void D2Common_10474(D2UnitStrc* p,D2StatListStrc* list){p->lists.erase(list->state);}
void STATLIST_FreeStatList(D2StatListStrc* p){frees++;delete p;}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){updates++;}
int* SKILLS_GetItemStatCostTxtRecord(int id){return id>=0 && id<16?&tables.pItemStatCostTxt[id]:nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return values[formula];}
void STATLIST_SetStat(D2StatListStrc* p,int id,int value,int){p->stats[id]=value;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc* p,int id,int value,int){if(p)p->stats[id]=value;}
void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int,int){unregisters++;}
bool SUNIT_IsDead(D2UnitStrc* p){return p->dead;}
bool STATES_CheckStateMaskStayDeathOnUnitByStateId(D2UnitStrc*,int){return stay;}
void sub_6FD14C30(D2UnitStrc*){refreshes++;}
void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int){}
void STATES_ToggleGfxStateFlag(D2UnitStrc*,int,int){gfx++;}
D2StatListStrc* sub_6FD10EC0(D2CurseStrc* c){factory++;auto* p=new D2StatListStrc{c->nState,c->nSkillLevel,c->nSkill,game.dwGameFrame+c->nDuration,{},c->pStateRemoveCallback};c->pTarget->lists[c->nState]=p;c->pTarget->active[c->nState]=true;return p;}
'''+group+writer+passive+callback+dispatch+r'''
int main(){states[157].wGroup=states[159].wGroup=2;bool first=true;std::printf("{\"dispatch\":[");
for(int old:{0,1,2,3})for(int oldRank:{1,3,20,255})for(int rank:{1,20})for(int venom:{0,1})for(int mode:{0,1,2}){
 D2UnitStrc owner;for(int state:{157,159,31})if((state==157 && (old&1)) || (state==159 && (old&2)) || (state==31 && venom)){owner.active[state]=true;owner.lists[state]=new D2StatListStrc{state,oldRank,0,999,{},nullptr};}
 guard=mode;skill.nAuraState=mode==2?200:157;values[0]=3000;values[1]=23;values[2]=21;frees=updates=refreshes=unregisters=gfx=factory=0;
 int success=SKILLS_SrvDo018_DefensiveBuff(&game,&owner,258,rank);auto* active=STATLIST_GetStatListFromUnitAndState(&owner,157);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",old,oldRank,rank,venom,mode,success,owner.active[157],owner.active[159],owner.active[31],active?active->rank:0,active?active->expiry:0,active?active->stats[0]:0,active?active->stats[1]:0,active?active->stats[2]:0,frees,factory,updates,gfx);
 for(auto& e:owner.lists)delete e.second;
}std::printf("],\"removal\":[");first=true;
for(int dead:{0,1})for(int retain:{0,1}){D2UnitStrc owner;owner.active[157]=true;owner.dead=dead;stay=retain;unregisters=updates=refreshes=0;SKILLS_CurseStateCallback_DefensiveBuff(&owner,157,nullptr);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",dead,retain,owner.active[157],unregisters,updates,refreshes);}
std::printf("]}\n");}
'''
 p=STAGE/'assassin-buff-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);fixture=json.loads(subprocess.check_output([str(binary)]));assert len(fixture['dispatch'])==192 and len(fixture['removal'])==4
 for old,oldRank,rank,venom,mode,ok,quick,fade,poison,current,expiry,velocity,attack,other,freed,factoryCalls,updates,gfx in fixture['dispatch']:
  if mode==0:assert (ok,quick,fade,poison,current,expiry,velocity,attack,other,freed,factoryCalls,updates,gfx)==(1,1,0,venom,rank,3400,23,21,21,int(bool(old&1))+int(bool(old&2)),1,2,1)
  else:assert (ok,quick,fade,poison,current,freed,factoryCalls,updates,gfx)==(0,int(bool(old&1)),int(bool(old&2)),venom,oldRank if old&1 else 0,0,0,0,0)
 for dead,retain,active,unreg,updates,refresh in fixture['removal']:assert (active,unreg,updates,refresh)==(int(bool(dead and retain)),1,int(not(dead and retain)),int(not(dead and retain)))
 out=STAGE/'assassin-buff-source-probe.json';out.write_text(json.dumps(fixture,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledUnchangedRoutines=blocks,pinnedTableHashes=pins,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),dispatchCases=192,removalCases=4),established=['Full do18 dispatch clears all source group2 states before applying its own state, including an equal/higher/lower existing Quickness; independent Venom remains.', 'Aura writers preserve supplied velocity/attack and mirror attackrate to other_animrate; source modifier skill/rank and callback are installed. Invalid skill/state returns before clearing existing state.', 'Removal callback unregisters events first, clears ordinary live/dead non-stay state, updates movement/animation and refreshes passives; synthetic stay-death exception is separate.'],services=['Skill formula results are explicit opaque inputs (3000frames,23velocity,21attack), not a newly compiled formula evaluator or rank oracle. Current rank formulas and native consumers are verified separately.', 'Statlist allocation/attach and callback registration are declared services. The unchanged full shared factory already has separate Inner Sight source evidence; no new complete stat graph/event scheduler or modern executable proof is claimed.', 'Current group membership uses Quickness157/Fade159 and independent Venom31. No Fade or Venom handler is enabled by this source probe.'],limits=['Exact modern animation/stat/event/client equivalence remains acceptance work. Current table fields take precedence over the supplementary older engine.', 'The writer mirror to other_animrate is retained as source context; native player attack/movement/cast pools are exercised independently.'])
 path=ROOT/'tools/reference/assassin-buff-engine-evidence.json';text=json.dumps(e,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print('Assassin defensive buffs:192 complete dispatch/group/writer cases and4 complete removal callback cases pass with declared services.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
