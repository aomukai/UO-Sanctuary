#!/usr/bin/env python3
"""Compile unchanged Berserk start, Do2 self-state and final-defense routines."""
from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';BASE=STAGE/'d2moo-pinned-source'
pins=json.loads((ROOT/'tools/reference/barbarian-berserk-preflight.json').read_text())
for path,sha in pins['sourceHashes'].items():assert hashlib.sha256((BASE/path).read_bytes()).hexdigest()==sha

def block(text,start):
 end=text.index('{',start)+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[start:end]
def routine(path,name):
 text=(BASE/path).read_text();at=text.index(name+'(');return block(text,text.rfind('\n',0,at)+1)
start=routine('source/D2Game/src/SKILLS/SkillBar.cpp','SKILLS_SrvSt39_Berserk')
defense=routine('source/D2Common/src/Units/Units.cpp','UNITS_GetDefense')
tohit=routine('source/D2Common/src/D2Skills.cpp','SKILLS_GetToHitFactor')
text=routine('source/D2Game/src/SKILLS/Skills.cpp','SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws')
selfstate=block(text,text.index('    if (pSkillsTxtRecord->nAuraState > 0'))
cpp=r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define __stdcall
constexpr int DAMAGERESULTFLAG_SUCCESSFULHIT=1,EVENTTYPE_REMOVESTATE=1,STATE_HOLYSHIELD=1;
constexpr int STAT_DEXTERITY=0,STAT_ARMORCLASS=1,STAT_ITEM_ARMOR_PERCENT=2,STAT_SKILL_ARMOR_PERCENT=3,STAT_ARMOR_OVERRIDE_PERCENT=4,STAT_MODIFIERLIST_SKILL=5,STAT_MODIFIERLIST_LEVEL=6;
struct D2StatListStrc{int until=0,flag=0;};struct D2UnitStrc{int dwUnitId=1,dwUnitType=0,dex=80,armor=0,itemPercent=0,skillPercent=0,overridePercent=-100;void* pInventory=nullptr;D2StatListStrc* state=nullptr;};
struct D2GameStrc{int dwGameFrame=100;void* pMemoryPool=nullptr;};
struct D2DamageStrc{int wResultFlags=0,dwHitFlags=0,dwHitClass=0,dwEnDmgPct=0,dwConvPct=0,nConvType=0;};
struct D2SkillsTxt{int dwCalc[4]={0,1,2,3},wResultFlags=0,dwHitFlags=0,dwHitClass=0,nEType=3,nSrcDam=128,nAuraState=95,dwToHitCalc=-1,dwToHit=100,dwLevToHit=15;} row;
struct Tables{int nStatesTxtRecordCount=200;} tables;auto* sgptDataTables=&tables;
D2UnitStrc target;D2StatListStrc list;D2DamageStrc captured;bool hasRow=true,hasTarget=true;
int hit=1,length=68,allocs=0,toggles=0,refreshes=0,eventAt=0,srcUsed=0;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return hasTarget?&target:nullptr;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int){return hit;}
int __stdcall SKILLS_GetToHitFactor(D2UnitStrc*,int,int);
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int){return calc==0?150:calc==1?length:100;}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*,int,int){}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* damage,int src){captured=*damage;srcUsed=src;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* p,int){return p->state;}
D2StatListStrc* STATLIST_AllocStatList(void*,int flag,int until,int,int){allocs++;list={until,flag};return &list;}
void STATLIST_SetState(D2StatListStrc*,int){}
void sub_6FD10E50(){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)()){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc* p,D2StatListStrc* l,int){p->state=l;}
void STATES_ToggleState(D2UnitStrc*,int,int){toggles++;}
void D2COMMON_10476(D2StatListStrc* p,int until){p->until=until;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int at,int,int){eventAt=at;}
void sub_6FCFE0E0(D2UnitStrc*,D2StatListStrc*,D2SkillsTxt*,int,int){refreshes++;}
'''+start+r'''
int DoAura(D2GameStrc* pGame,D2UnitStrc* pUnit,int nSkillId,int nSkillLevel){auto* pSkillsTxtRecord=&row;
'''+selfstate+r'''
return 1;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_DEXTERITY?p->dex:stat==STAT_ARMORCLASS?p->armor:stat==STAT_ARMOR_OVERRIDE_PERCENT?p->overridePercent:0;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_ITEM_ARMOR_PERCENT?p->itemPercent:p->skillPercent;}
bool STATES_CheckState(D2UnitStrc*,int){return false;}
int STATLIST_GetStatValue(D2StatListStrc*,int,int){return 0;}
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}
void INVENTORY_GetEquippedShield(void*,D2UnitStrc**){ }
'''+defense+'\n'+tohit+r'''
int main(){std::printf("{\"startCases\":[");bool first=true;D2GameStrc game;
for(int h:{0,1})for(int existing:{0,1})for(int len:{-1,0,1,33,68})for(int src:{0,128}){
 hit=h;length=len;row.nSrcDam=src;allocs=toggles=refreshes=eventAt=0;D2UnitStrc owner;list={500,2};owner.state=existing?&list:nullptr;
 int ok=SKILLS_SrvSt39_Berserk(&game,&owner,152,1);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",h,existing,len,src,ok,allocs,toggles,refreshes,eventAt,owner.state->until,owner.state->flag,captured.dwConvPct,srcUsed);
}std::printf("],\"doAuraCases\":[");first=true;
for(int existing:{0,1})for(int flag:{2,4}){D2UnitStrc owner;list={168,flag};owner.state=existing?&list:nullptr;allocs=toggles=refreshes=eventAt=0;
 DoAura(&game,&owner,152,1);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d]",existing,flag,allocs,toggles,refreshes,eventAt,owner.state->flag,owner.state->until);
}std::printf("],\"defenseCases\":[");first=true;
for(int basis:{0,20,100,500})for(int percent:{0,50,100,500})for(int overridePct:{-100,-50,0,50}){D2UnitStrc owner;owner.dex=basis*4;owner.skillPercent=percent;owner.overridePercent=overridePct;
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d]",basis,percent,overridePct,UNITS_GetDefense(&owner));
}std::printf("],\"toHitCases\":[");first=true;
for(int calc:{-1,0})for(int rank:{0,1,20,21}){D2UnitStrc owner;row.dwToHitCalc=calc;if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d]",calc,rank,SKILLS_GetToHitFactor(&owner,152,rank));}
std::printf("],\"guards\":[");for(int guard:{0,1}){hasRow=guard!=0;hasTarget=guard!=1;D2UnitStrc owner;if(guard)std::printf(",");std::printf("%d",SKILLS_SrvSt39_Berserk(&game,&owner,152,1));}std::printf("]}\n");}
'''
path=STAGE/'berserk-source-probe.cpp';path.write_text(cpp);binary=STAGE/'berserk-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert data['guards']==[0,0]
for h,existing,length,src,ok,alloc,toggle,refresh,event,until,flag,convert,used in data['startCases']:
 assert(ok,alloc,toggle,refresh,event,until,flag,convert,used)==(1,1-existing,1-existing,1,100+(length if length>0 else 10),100+(length if length>0 else 10),2,100 if h else 0,128)
for existing,flag,alloc,toggle,refresh,event,actualFlag,until in data['doAuraCases']:
 assert(alloc,toggle,refresh,event,actualFlag,until)==(1-existing,1,1,0,flag if existing else 4,168 if existing else 0)
for calc,rank,result in data['toHitCases']:
 assert result==(0 if rank<=0 else 100+(rank-1)*15 if calc==-1 else 150)
for basis,percent,override,value in data['defenseCases']:
 subtotal=basis+basis*percent//100;expected=subtotal+int(subtotal*override/100);assert value==expected
(STAGE/'berserk-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
evidence={'referenceVersion':pins['referenceVersion'],'legacyCommit':pins['legacyCommit'],'sourceHashes':pins['sourceHashes'],'probe':{'startCases':len(data['startCases']),'doAuraCases':len(data['doAuraCases']),'defenseCases':len(data['defenseCases']),'guards':2,'toHitCases':len(data['toHitCases']),'dataPath':'.local/class-skills/berserk-source-probe.json','blockHashes':{name:hashlib.sha256(value.encode()).hexdigest() for name,value in [('start39',start),('do2SelfAura',selfstate),('getDefense',defense),('getToHit',tohit)]}},'established':['Start39 creates a timed flag2 state even on a miss, updates existing absolute expiry without a max-duration rule, and uses10frames only for nonpositive length.','Do2 refreshes an existing state without extending its expiry. If the timed state has expired before impact, its absent self-state is recreated with flag4/TEMPONLY and no removal event, lasting until the animation mode changes.','Missing ToHitCalc selects the linear ToHit/LevToHit source path; an explicit formula selects formula evaluation.','The armor override multiplies the ordinary combined defense after its percentage pool; -100 makes it zero.'],'stubs':['Hit result, skill formula values, allocation packet, state-list allocation, events and callbacks are explicit bounded stubs; the unchanged source call ordering/state flags are exercised.','Defense uses positive/zero basis, supplied percent/override and no Holy Shield. No native integer/stat compiler certificate is claimed.','Ordinary critical/conversion is separately exercised by concentrate-engine-evidence.json; source damage calculation is not compiled in this probe.'],'limits':['Legacy isolated source is supplementary, not a current D2R executable certificate.','Modern rank snapshots, exact slow/action mode changes, temporary-state edge cases and full item/proc/owner/client acceptance remain open.']}
(ROOT/'tools/reference/berserk-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print('Berserk source cases:',{k:len(v) for k,v in data.items()})
