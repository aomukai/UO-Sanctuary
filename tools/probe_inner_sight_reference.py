#!/usr/bin/env python3
"""Pin Inner Sight target data and compile its shared state/defense/range contracts."""
from pathlib import Path
import argparse,hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';REF=ROOT/'tools/reference/d2r-named-equipment'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 hashes={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,token):
  start=text.index(token);end=text.index('{',start)+1;depth=1
  while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
  return text[start:end]
 skills=source('source/D2Game/src/SKILLS/Skills.cpp');state=routine(skills,'D2StatListStrc* __fastcall sub_6FD10EC0(')
 ama=source('source/D2Game/src/SKILLS/SkillAma.cpp');do6=routine(ama,'int32_t __fastcall SKILLS_SrvDo006_InnerSight_SlowMissiles(');callback=routine(ama,'int32_t __fastcall SKILLS_AuraCallback_InnerSight_SlowMissiles(')
 defense=routine(source('source/D2Common/src/Units/Units.cpp'),'int __stdcall UNITS_GetDefense(')
 distance=routine(source('source/D2Common/src/Path/Path.cpp'),'int __stdcall PATH_ComputeSquaredDistance(')
 cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define __stdcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,MONSTATSFLAGINDEX_NPC=0,MONSTATS2FLAGINDEX_ISATT=0,STAT_CURSE_RESISTANCE=0,STATE_ATTRACT=2,STATE_HOLYSHIELD=1,EVENTTYPE_REMOVESTATE=0,STAT_DEXTERITY=1,STAT_ARMORCLASS=2,STAT_ITEM_ARMOR_PERCENT=3,STAT_SKILL_ARMOR_PERCENT=4,STAT_ARMOR_OVERRIDE_PERCENT=5,STAT_MODIFIERLIST_SKILL=6,STAT_MODIFIERLIST_LEVEL=7;
int gdwBitMasks[32]={1,2};
struct D2StatListStrc{int state=17,skill=8,rank=0,flags=2,expire=500,creator=200,stat=31,value=-99;};
struct D2UnitStrc{int dwUnitType=1,dwClassId=0,dwUnitId=100;void* pMemoryPool=nullptr;void* pInventory=nullptr;D2StatListStrc* list=nullptr;int armor=0,percent=0;bool attract=false;};
struct D2GameStrc{int dwGameFrame=400;}game;
struct D2StatesTxt{int nStateFlags[1]={0};}stateRow;struct D2MonStatsTxt{int dwMonStatsFlags=0;}mon;struct D2MonStats2Txt{int dwFlags=1;}mon2;
struct Tables{int nStatesTxtRecordCount=256,nItemStatCostTxtRecordCount=512;}tables;auto* sgptDataTables=&tables;
struct D2SkillsTxt{int dwCalc[1]={0};};
struct D2CurseStrc{D2UnitStrc* pTarget=nullptr;D2UnitStrc* pUnit=nullptr;int nSkill=8,nSkillLevel=1,nDuration=200,nStat=31,nStatValue=-40,nState=17;void(*pStateRemoveCallback)()=nullptr;};
D2StatListStrc fresh;int guard=0,freed=0,events=0;
D2GameStrc* SUNIT_GetGameFromUnit(D2UnitStrc*){return &game;}
D2StatesTxt* SKILLS_GetStatesTxtRecord(int){return guard==1?nullptr:&stateRow;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return guard==2?nullptr:&mon;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return guard==4?nullptr:&mon2;}
int STATES_CheckStateMaskCurseByStateId(int){return 0;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_ARMORCLASS?p->armor:stat==STAT_CURSE_RESISTANCE?100:0;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_SKILL_ARMOR_PERCENT?p->percent:0;}
int MONSTERUNIQUE_CalculatePercentage(int a,int b,int d){return int(int64_t(a)*b/d);}
bool STATES_CheckState(D2UnitStrc* p,int state){return state==STATE_ATTRACT && p->attract;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndFlag(D2UnitStrc* p,unsigned){return p->list;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* p,int state){return state==17?p->list:nullptr;}
int STATLIST_GetState(D2StatListStrc* p){return p->state;}int STATLIST_GetSkillId(D2StatListStrc* p){return p->skill;}int STATLIST_GetSkillLevel(D2StatListStrc* p){return p->rank;}
void STATLIST_SetExpireFrame(D2StatListStrc* p,int n){p->expire=n;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){events++;}
void D2Common_10474(D2UnitStrc* p,D2StatListStrc*){p->list=nullptr;}void STATLIST_FreeStatList(D2StatListStrc*){freed++;}
void UNITROOM_RefreshUnit(D2UnitStrc*){}void STATES_ToggleState(D2UnitStrc*,int,int){}
bool STATES_CheckStateMaskExpByStateId(int){return false;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned flags,int expiry,int,int owner){fresh={};fresh.flags=flags;fresh.expire=expiry;fresh.creator=owner;return &fresh;}
void STATLIST_SetState(D2StatListStrc* p,int n){p->state=n;}void STATLIST_SetSkillId(D2StatListStrc* p,int n){p->skill=n;}void STATLIST_SetSkillLevel(D2StatListStrc* p,int n){p->rank=n;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc* p,int stat,int value,int){p->stat=stat;p->value=value;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc* p,D2StatListStrc* list,int){p->list=list;}
void sub_6FD10E50(){}void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)()){}
int STATLIST_GetStatValue(D2StatListStrc*,int,int){return 0;}D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){return nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return 0;}void INVENTORY_GetEquippedShield(void*,D2UnitStrc**){}
'''+state+'\n'+defense+'\n'+distance+r'''
int main(){bool first=true;std::printf("{\"states\":[");
for(int oldRank:{0,1,4})for(int rank:{1,4,7})for(int duration:{0,100,300})for(int mode=0;mode<6;mode++)for(int attract:{0,1}){
 D2StatListStrc old;old.rank=oldRank;D2UnitStrc target,owner;target.list=oldRank?&old:nullptr;target.attract=attract;owner.dwUnitType=UNIT_PLAYER;
 guard=mode;mon.dwMonStatsFlags=mode==3?1:0;mon2.dwFlags=mode==5?0:1;freed=0;events=0;
 D2CurseStrc curse;curse.pTarget=&target;curse.pUnit=&owner;curse.nSkillLevel=rank;curse.nDuration=duration;
 auto* result=sub_6FD10EC0(&curse);auto* current=target.list;
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",oldRank,rank,duration,mode,attract,result!=nullptr,current?current->rank:0,current?current->expire:0,current?current->creator:0,current?current->value:0,freed,events);
 }std::printf("],\"defense\":[");first=true;
for(int basis:{0,1,39,40,100,1000})for(int loss:{0,40,65,815})for(int percent:{-100,-50,0,50,200}){
 D2UnitStrc target;target.armor=basis-loss;target.percent=percent;
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d]",basis,loss,percent,UNITS_GetDefense(&target));
 }std::printf("],\"radius\":[");first=true;
for(int x=-15;x<=15;x++)for(int y=-15;y<=15;y++){
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d]",x,y,PATH_ComputeSquaredDistance(0,0,x*2,y*2)<=27*27);
 }std::printf("]}\n");}
'''
 path=STAGE/'inner-sight-source-probe.cpp';path.write_text(cpp);binary=STAGE/'inner-sight-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)]));assert len(data['states'])==324 and len(data['defense'])==120 and len(data['radius'])==961
 for old,rank,duration,mode,attract,success,current,expiry,creator,value,freed,events in data['states']:
  rejected=mode!=0 or old>rank;equal=old==rank and old>0
  expected=(old,500,200,-99) if rejected or equal else (rank,400+duration if duration else 0,100,-40)
  if equal and not rejected and duration:expected=(old,400+duration,200,-99)
  if old==0 and rejected:expected=(0,0,0,0)
  assert (success,current,expiry,creator,value,freed,events)==(int(not rejected),*expected,int(not rejected and old>0 and not equal),int(not rejected and duration>0))
 fixture=STAGE/'inner-sight-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 manifest=json.loads((REF/'sources.json').read_text())['files'];tables={};pins={}
 for name in ['skills','states','monstats','monstats2','itemstatcost']:
  p=REF/'json'/f'{name}.json';sha=hashlib.sha256(p.read_bytes()).hexdigest();assert manifest['json/'+name+'.json']['sha256']==sha;tables[name]=json.loads(p.read_text());pins[name]=sha
 row=tables['skills']['8'];assert row['aurastatcalc1']=='-edmn' and row['Param5']==27 and tables['states']['innersight'].get('curse',0)==0
 rules=[]
 for key,r in tables['monstats'].items():
  if not r.get('MonStatsEx'):continue
  child=tables['monstats2'].get(r['MonStatsEx']);assert child is not None,(key,r['MonStatsEx'])
  rules.append(dict(source=key,npc=bool(r.get('npc')),noAura=bool(r.get('noAura')),attackable=bool(child.get('isAtt'))))
 target=ROOT/'data/Sanctuary/Balance/source-aura-targets.json';out=json.dumps(rules,indent=2)+'\n'
 if check:assert target.read_text()==out
 else:target.write_text(out)
 evidence={'referenceVersion':'3.3.93847','legacyCommit':tree['sha'],'sourceHashes':hashes,'pinnedTableHashes':pins,'compiledBlocks':{k:hashlib.sha256(v.encode()).hexdigest() for k,v in [('state',state),('defense',defense),('distance',distance)]},'reviewedBlocks':{'do6':hashlib.sha256(do6.encode()).hexdigest(),'auraCallback':hashlib.sha256(callback.encode()).hexdigest()},'probe':{'stateRows':324,'defenseRows':120,'radiusRows':961,'dataPath':str(fixture.relative_to(ROOT)),'dataSha256':hashlib.sha256(fixture.read_bytes()).hexdigest()},'runtimeTargetRules':{'path':str(target.relative_to(ROOT)),'rows':len(rules),'sha256':hashlib.sha256(out.encode()).hexdigest()},'established':['Inner Sight state17 is not a curse-mask state; it coexists with curse resistance/Attract and ordinary curses. NPC or missing/unattackable MonStats2 branches reject; noAura is filtered by the reviewed parent enumeration.','Same skill/state and equal rank refreshes expiry only, retaining original creator and value. Lower rank is rejected without expiry changes; higher rank replaces. Source zero-duration semantics are in the probe but the real Inner Sight table always supplies positive duration.','Armorclass reduction participates before defense percentages. Unchanged GetDefense retains its signed nonpositive-basis branch; final Sanctuary hit defense clamps nonnegative. Full modern stat graph and native creature basis projection remain acceptance.','Reviewed parent uses squared distance and filter34179 (0x8583), which has no optional LOS0x200 flag. Self is excluded and non-aura species are filtered.27 source subtiles become13.5 UO tiles under the shared provisional mapping.'],'services':['State table flags, statlist/owner storage, explicit NPC/attackability guards, clock/event scheduling and allocation are declared services. Actual scheduled state removal, full filter/alignment/room traversal and formula evaluation are not compiled.','Defense receives aggregate supplied raw basis, flat reduction and combined percentage, no Holy Shield state or armor override; signed branch is retained rather than invented rounding.'],'conflicts':{'officialGuide':'https://classic.battle.net/diablo2exp/skills/amazon-passive.shtml','published':'Legacy guide says last caster wins and radius13.3yards.','policy':'Use pinned27-subtile radius. Retain reviewed legacy rank priority pending current executable confirmation; published last-caster statement is a nonblocking modern acceptance gap.'},'limits':['This is supplementary older-engine proof, not a3.3.93847 executable certificate. Exact modern multi-caster/stat/owner/client behavior remains acceptance.','Native geometry/height/visibility/hostility and explicit native encounter adapters are provisional; unsupported unmapped source species are skipped explicitly.']}
 evidence['currentTooltipEvidence']='tools/reference/inner-sight-current-tooltip.json'
 p=ROOT/'tools/reference/inner-sight-engine-evidence.json';s=json.dumps(evidence,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print(f'Inner Sight:324 compiled state,120 defense and961 radius rows;{len(rules)} pinned aura target rules verified.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
