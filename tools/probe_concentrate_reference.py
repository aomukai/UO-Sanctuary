#!/usr/bin/env python3
"""Bounded original mode guard, temporary-state cleanup and conversion arithmetic."""
from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';BASE=STAGE/'d2moo-pinned-source'
pins=json.loads((ROOT/'tools/reference/barbarian-concentrate-preflight.json').read_text())
for path,sha in pins['sourceHashes'].items():assert hashlib.sha256((BASE/path).read_bytes()).hexdigest()==sha

def block(text,start):
 end=text.index('{',start)+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[start:end]
def routine(path,name):
 text=(BASE/path).read_text();at=text.index(name+'(');return block(text,text.rfind('\n',0,at)+1)
mode=routine('source/D2Game/src/PLAYER/PlrModes.cpp','D2GAME_PLRMODES_First_6FC7F340')
clean=routine('source/D2Common/src/D2StatList.cpp','STATLIST_DeactivateTemporaryStates')
change=routine('source/D2Common/src/Units/Units.cpp','UNITS_ChangeAnimMode')
text=(BASE/'source/D2Game/src/UNIT/SUnitDmg.cpp').read_text();conversion=block(text,text.index('\tif (pDamage->nConvType)'))
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#define __fastcall
#define __stdcall
using BOOL=int;
constexpr int TRUE=1,FALSE=0,UNIT_TILE=5,UNIT_MONSTER=1,UNITFLAG_DOUPDATE=1,MONMODE_NEUTRAL=1;
constexpr int PLRMODE_DEATH=0,PLRMODE_NEUTRAL=1,PLRMODE_WALK=2,PLRMODE_RUN=3,PLRMODE_GETHIT=4,PLRMODE_ATTACK1=7,PLRMODE_ATTACK2=8,PLRMODE_BLOCK=9,PLRMODE_CAST=10,PLRMODE_THROW=11,PLRMODE_SPECIAL1=13,PLRMODE_SEQUENCE=18,PLRMODE_DEAD=17;
constexpr int STATE_UNINTERRUPTABLE=1,STATE_CONCENTRATION=2,STATE_CONCENTRATE=3,STAT_SKILL_CONCENTRATION=0,SKILLSFLAGINDEX_INTERRUPT=0,EVENTTYPE_ENDANIM=1;
constexpr int STATLIST_NEWLENGTH=2,STATLIST_TEMPONLY=4;
struct D2StatListStrc{int dwFlags=0,dwStateNo=0;D2StatListStrc* pPrevLink=nullptr;D2StatListStrc* pMyLastList=nullptr;};
struct D2UnitStrc{int dwAnimMode=7,dwUnitType=0,dwFlags=0,pSeed=0;bool hasSkill=true,concentrate=true;D2StatListStrc* pStatListEx=nullptr;};
struct D2GameStrc{int dwGameFrame=100;};
struct D2SkillStrc{} skill;
struct D2SkillsTxt{int wSrvDoFunc=2,dwFlags[1]={0};} row;
int gdwBitMasks[1]={1},endFrame=110,frees=0,off=0,refreshes=0,resets=0;
D2UnitStrc* cleaning=nullptr;
int STATES_CheckState(D2UnitStrc* p,int state){return p&&state==STATE_CONCENTRATE&&p->concentrate;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc* p){return p->hasSkill?&skill:nullptr;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc*,const char*,int){return 144;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return &row;}
int EVENT_GetEventFrame(D2GameStrc*,D2UnitStrc*,int){return endFrame;}
void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,void*,int,int,int,int){resets++;}
int ITEMS_RollRandomNumber(int*){return 99;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return nullptr;}
int STATLIST_GetStatValue(D2StatListStrc*,int,int){return 0;}
void STATES_ToggleState(D2UnitStrc*,int,int value){if(!value)off++;}
bool STATLIST_IsExtended(D2StatListStrc*){return false;}
void D2Common_STATLIST_FreeStatListImpl_6FDB7050(D2StatListStrc* p){
 auto** link=&cleaning->pStatListEx->pMyLastList;while(*link && *link!=p)link=&(*link)->pPrevLink;if(*link)*link=p->pPrevLink;frees++;}
void UNITROOM_RefreshUnit(D2UnitStrc*){refreshes++;}
void UNITS_SetAnimStartFrame(D2UnitStrc*){}
'''+mode+'\n'+clean+'\n'+change+r'''
constexpr int ELEMTYPE_FIRE=1,ELEMTYPE_LTNG=2,ELEMTYPE_MAGIC=3,ELEMTYPE_COLD=4,ELEMTYPE_POIS=5,ELEMTYPE_BURN=6,ELEMTYPE_FREEZE=7,ELEMTYPE_RAND=8;
struct D2DamageStrc{int nConvType=3,dwConvPct=0,dwPhysDamage=0,dwFireDamage=0,dwLtngDamage=0,dwMagDamage=0,dwColdDamage=0,dwColdLen=0,dwPoisDamage=0,dwPoisLen=0,dwBurnLen=0,dwFrzLen=0;};
int MONSTERUNIQUE_CalculatePercentage(int value,int pct,int base){return int(int64_t(value)*pct/base);}
void Convert(D2DamageStrc* pDamage){D2UnitStrc owner;auto* pAttacker=&owner;
'''+conversion+r'''
}
int main(){std::printf("{\"modeCases\":[");bool first=true;
for(int current:{PLRMODE_NEUTRAL,PLRMODE_ATTACK1,PLRMODE_DEAD})for(int requested:{PLRMODE_WALK,PLRMODE_RUN,PLRMODE_GETHIT,PLRMODE_BLOCK,PLRMODE_DEATH,PLRMODE_ATTACK1,PLRMODE_CAST})for(int frame:{100,115,116}){
 D2UnitStrc p;p.dwAnimMode=current;D2GameStrc game;game.dwGameFrame=frame;int result=D2GAME_PLRMODES_First_6FC7F340(&game,&p,requested,1,nullptr);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d]",current,requested,frame,result);
}std::printf("],\"stateCases\":[");first=true;
for(int list:{0,1})for(int flag:{0,2})for(int next:{7,1}){
 D2UnitStrc p;D2StatListStrc root,temp1,keep,temp2;root.dwFlags=flag;temp1={4,15,&keep};keep={0,99,&temp2};temp2={4,16};root.pMyLastList=&temp1;p.pStatListEx=list?&root:nullptr;cleaning=&p;frees=off=refreshes=0;
 int result=UNITS_ChangeAnimMode(&p,next);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",list,flag,next,result,frees,off,root.dwFlags);
}std::printf("],\"conversionCases\":[");first=true;
for(int physical:{0,1,255,256,12345,32768000})for(int pct:{0,1,20,99,100})for(int magic:{0,129})for(int crit:{1,2}){
 D2DamageStrc d;d.dwPhysDamage=physical*crit;d.dwMagDamage=magic;d.dwConvPct=pct;Convert(&d);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",physical,pct,magic,crit,d.dwPhysDamage,d.dwMagDamage);
}std::printf("]}\n");}
'''
path=STAGE/'concentrate-source-probe.cpp';path.write_text(cpp);binary=STAGE/'concentrate-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
for current,requested,frame,result in data['modeCases']:
 expected=0 if current==17 else 1 if requested==0 or current==1 else int(requested in [7,10] and frame<=115)
 assert result==expected,(current,requested,frame,result)
for exists,flag,nextMode,result,freed,off,left in data['stateCases']:
 removed=exists and flag==2 and nextMode!=7
 assert (result,freed,off,left)==(int(nextMode!=7),2 if removed else 0,2 if removed else 0,0 if removed else flag)
for physical,pct,magic,crit,left,converted in data['conversionCases']:
 part=physical*crit*pct//100;assert(left,converted)==(physical*crit-part,magic+part)
(STAGE/'concentrate-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
evidence={'referenceVersion':pins['referenceVersion'],'legacyCommit':pins['legacyCommit'],'sourceHashes':pins['sourceHashes'],'probe':{'modeCases':len(data['modeCases']),'temporaryStateCases':len(data['stateCases']),'conversionCases':len(data['conversionCases']),'dataPath':'.local/class-skills/concentrate-source-probe.json','blockHashes':{name:hashlib.sha256(value.encode()).hexdigest() for name,value in [('modeGuard',mode),('temporaryCleanup',clean),('modeChange',change),('conversion',conversion)]}},
'established':['No-interrupt attack mode blocks walk/run/gethit/block requests; death is allowed. Other attack/cast queue timing remains the source mode guard, not a general freedom-to-cancel rule.','An actual mode change removes TEMPONLY states when NEWLENGTH is present; unchanged modes do not remove them. Persistent state survives.','Physical-to-magic split truncates the converted FP256 fraction after ordinary critical, preserves existing magic and conserves the physical-plus-converted total.'],
'stubs':['Skill row/used skill/state membership and event frame are explicit stubs. Native movement/input queue timing is separately tested; modern mode equivalence is not certified.','Temporary statlist is a bounded three-node fixture; free/toggle callbacks and extended lists are stubs.','Percentage helper uses source integer semantics; ordinary critical is a supplied1x/2x precursor. Only magic conversion is exercised.'],
'limits':['Legacy isolated blocks do not prove current D2R executable parity.','Complete proc/statlist removal, queued modes, modern start/do rank snapshots, low-mana AttackNoMana and owner/client acceptance remain open.']}
(ROOT/'tools/reference/concentrate-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print('Concentrate source mode/state/conversion cases:',{k:len(v) for k,v in data.items()})
