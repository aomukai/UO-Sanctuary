#!/usr/bin/env python3
"""Compile the unchanged Life Tap defender event with explicit stat/state services."""
from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
source='source/D2Game/src/SKILLS/SkillNec.cpp';raw=(STAGE/'d2moo-pinned-source'/source).read_bytes()
assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==source)
text=raw.decode();start=text.index('int32_t __fastcall D2GAME_EventFunc05_6FD0EDE0(');end=text.index('{',start)+1;depth=1
while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
body=text[start:end]
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,STAT_HITPOINTS=0;
struct D2UnitStrc{int dwUnitType,hp,maxHp;bool dead;};struct D2DamageStrc{int dwPhysDamage;};struct D2GameStrc{};
struct D2SkillsTxt{int wAuraTargetState=1,dwCalc[1]={0},wPrgOverlay=12;};struct D2StatListStrc{};
struct Tables{int nStatesTxtRecordCount=5,nOverlayTxtRecordCount=20;}tables;auto* sgptDataTables=&tables;
int mode=0,percent=50,observedRank=-99,overlays=0,formulaOwner=0;D2SkillsTxt skill;D2StatListStrc list;D2UnitStrc caster{0,1,1,false};
bool SUNIT_IsDead(D2UnitStrc* p){return p->dead;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return mode==7?nullptr:&skill;}
bool STATES_CheckState(D2UnitStrc*,int){return mode!=10;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return mode==11?nullptr:&list;}
int STATLIST_GetOwnerType(D2StatListStrc*){return 0;}int STATLIST_GetOwnerGUID(D2StatListStrc*){return 1;}
D2UnitStrc* SUNIT_GetServerUnit(D2GameStrc*,int,int){return mode==12?nullptr:&caster;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int,int,int rank){observedRank=rank;formulaOwner=p?1:0;return percent;}
int MONSTERUNIQUE_CalculatePercentage(int value,int n,int d){return int(int64_t(value)*n/d);}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int,int){return p->hp;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc* p){return p->maxHp;}
void STATLIST_SetUnitStat(D2UnitStrc* p,int,int value,int){p->hp=value;}
void UNITS_SetOverlay(D2UnitStrc*,int,int){overlays++;}
'''+body+r'''
int main(){bool first=true;std::printf("[");
for(int physical:{0,1,255,257,12864,1280000})for(int hp:{0,1,257,100000})for(int maximum:{0,256,1280000})for(int pct:{0,1,50,100})for(int guard=0;guard<13;guard++){
 if(maximum>0 && hp>maximum)continue;
 for(int rank:{-1,0,7}){
 D2UnitStrc victim{guard==3?2:UNIT_MONSTER,1000,1000,false},healed{guard==4?2:UNIT_PLAYER,hp,maximum,guard==5};D2DamageStrc damage{physical};
 mode=guard;percent=pct;observedRank=-99;overlays=0;formulaOwner=0;skill.wAuraTargetState=guard==8?-1:guard==9?5:1;
 int result=D2GAME_EventFunc05_6FD0EDE0(nullptr,17,guard==1?nullptr:&victim,guard==2?nullptr:&healed,guard==6?nullptr:&damage,82,rank);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",physical,hp,maximum,pct,guard,rank,healed.hp,result,observedRank,overlays,formulaOwner);
 }}std::printf("]\n");}
'''
path=STAGE/'life-tap-source-probe.cpp';path.write_text(cpp);binary=STAGE/'life-tap-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));accepted=0
for physical,hp,maximum,pct,guard,rank,remaining,result,observed,overlays,owner in data:
 evaluated=guard in (0,12) and physical>0
 success=evaluated and hp>0 and maximum>0
 expected=min(maximum,max(1,hp+physical*pct//100)) if success else hp
 assert (remaining,result,observed,overlays,owner)==(expected,int(success),(1 if rank<0 else rank) if evaluated else -99,int(success),int(evaluated and guard!=12))
 accepted+=success
fixture=STAGE/'life-tap-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
table=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';row=json.loads(table.read_text())['82'];assert row['auraeventfunc1']==5 and row['auraeventfunc2']==5 and row['Param5']==50 and row['Param6']==0
e={'referenceVersion':'3.3.93847','legacyCommit':tree['sha'],'source':{'path':source,'sha256':hashlib.sha256(raw).hexdigest(),'bodySha256':hashlib.sha256(body.encode()).hexdigest()},'probe':{'rows':len(data),'acceptedRows':accepted,'dataPath':str(fixture.relative_to(ROOT)),'dataSha256':hashlib.sha256(fixture.read_bytes()).hexdigest()},'pinnedTable':{'path':str(table.relative_to(ROOT)),'sha256':hashlib.sha256(table.read_bytes()).hexdigest(),'skillId':82,'events':[row['auraevent1'],row['auraevent2']],'eventFunctions':[row['auraeventfunc1'],row['auraeventfunc2']],'calculation':row['calc1'],'parameters':[row['Param5'],row['Param6']]},'established':['Complete unchanged callback heals the attacking recipient from positive mitigated event dwPhysDamage in FP256, caps only to recipient maximum, and uses the defender curse statlist caster/formula context.','Callback has no monster drain, difficulty, victim-life or central-arrow limit. Parent ExecuteEvents calls defender DAMAGEDBYMISSILE after central attacker callbacks and before ordinary leech caps dwPhysDamage to victim life.','Null/invalid actor kinds, dead recipient, missing/nonpositive damage, absent skill/state/statlist, nonpositive recipient current/max life reject. Missing caster pointer still reaches the supplied formula; negative rank normalizes to1, rank0 remains0.'],'services':['Stat/state lookup, caster resolution, formula percent, supplied unit kinds/dead flags, percentage helper and overlay are declared services. This is not the entire engine or formula compiler.','Arithmetic uses nonnegative bounded healing percentages and nonoverflowing input; recipient current life never exceeds a positive maximum. Negative/overflow stat states are not certified.'],'limits':['Supplementary older engine callback is not a current3.3.93847 executable certificate. Full mitigation/callback/retaliation/owner/PvP/special-mode/client order remains acceptance work.','Current native integration is verified separately; this source probe does not execute Sanctuary weapons or prove class completion.']}
e['officialGuide']={'url':'https://classic.battle.net/diablo2exp/skills/necromancer-curses.shtml','reviewedDate':'2026-10-23','scope':'Published50% healing at all ranks and rank1/rank2 durations16/18.4 seconds agree with pinned tables. This legacy guide does not settle modern hidden event/overkill behavior.'}
(ROOT/'tools/reference/life-tap-engine-evidence.json').write_text(json.dumps(e,indent=2)+'\n')
print(f'Life Tap:{len(data)} unchanged callback rows ({accepted} accepted), state/actor guards and pinned events verified.')
