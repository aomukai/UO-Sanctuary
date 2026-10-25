#!/usr/bin/env python3
"""Run complete pinned charge builder and physical finisher routines with declared services."""
import argparse, hashlib, json, re, struct, subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text()); assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 hashes={}; blocks={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes(); assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path); hashes[path]=hashlib.sha256(raw).hexdigest(); return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M); assert m,name
  end=text.index('{',m.start())+1; depth=1
  while depth: depth+=(text[end]=='{')-(text[end]=='}'); end+=1
  blocks[name]=text[m.start():end]; return blocks[name]+'\n'
 ass=source('source/D2Game/src/SKILLS/SkillAss.cpp'); skills=source('source/D2Game/src/SKILLS/Skills.cpp'); units=source('source/D2Common/src/Units/Units.cpp'); sunits=source('source/D2Game/src/UNIT/SUnit.cpp')
 for name in ['SKILLS_SrvDo001_Attack_LeftHandSwing','sub_6FD12950','D2GAME_SKILLS_Handler_6FD12BA0']: routine(skills,name)
 for name in ['sub_6FCF77E0']: routine(ass,name)
 modern=(ROOT/'tools/reference/d2r-named-equipment/json/skills.json').read_bytes(); rows=json.loads(modern)
 tiger=next(x for x in rows.values() if x.get('skill')=='Tiger Strike'); attack=next(x for x in rows.values() if x.get('skill')=='Attack')
 assert (tiger['srvstfunc'],tiger['srvdofunc'],tiger['Param3'],tiger['Param4'],tiger['SrcDam'])==(23,34,375,50,128)
 assert (attack['finishing'],attack['prgchargesconsumed'],attack['prgchargestocast'])==(1,1,0)
 cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <initializer_list>
#define __fastcall
#define D2_MAYBE_UNUSED(x) (void)(x)
constexpr int DAMAGERESULTFLAG_SUCCESSFULHIT=1,STAT_MODIFIERLIST_SKILL=1,STAT_MODIFIERLIST_LEVEL=2,EVENTTYPE_REMOVESTATE=3;
struct D2GameStrc{int dwGameFrame=100;void* pMemoryPool=nullptr;} game;
struct D2UnitStrc{int dwUnitType=0,dwUnitId=1;} owner,target;
struct D2SkillsTxt{int nAuraState=4,wAuraStat[2]={3,4},dwAuraLenCalc=375,dwAuraStatCalc[2]={0,50},nSrcDam=128,nPrgDamage=1,dwCalc[1]={0},wTgtOverlay=1;} row;
struct D2StatListStrc{int count=0,ar=0,rank=0,skill=254,expires=0;} list;
struct D2DamageStrc{int wResultFlags=0,dwEnDmgPct=0;} saved;
struct Tables{int nStatesTxtRecordCount=256,nItemStatCostTxtRecordCount=256,nProgressiveStates=1,pProgressiveStates[1]={4},nOverlayTxtRecordCount=256;} tables;auto* sgptDataTables=&tables;
bool selected=true,record=true,inRange=true,hit=true,allocation=true,exists=false,enabled=true;int currentRank=1,drains=0,allocs=0,explicitAr=-1,events=0,overlays=0;
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected?&target:nullptr;}
D2GameStrc* SUNIT_GetGameFromUnit(D2UnitStrc*){return &game;}
int UNITS_IsInMeleeRange(D2UnitStrc*,D2UnitStrc*,int){return inRange;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return record?&row:nullptr;}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int rank){return 25+10*(rank-1);}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int ar,int){explicitAr=ar;return hit?1:0;}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p,int src){if(src!=128)std::abort();saved=*p;allocs++;}
D2DamageStrc* SUNITDMG_GetDamageFromUnits(D2UnitStrc*,D2UnitStrc*){return &saved;}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){drains++;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int rank){return formula==375?375:formula==50?50:100+20*(rank-1);}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return exists?&list:nullptr;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int until,int,int){if(!allocation)return nullptr;list={};list.expires=until;exists=true;return &list;}
void STATES_ToggleState(D2UnitStrc*,int,int value){enabled=value;}
void STATES_ToggleGfxStateFlag(D2UnitStrc*,int,int){}
void STATLIST_SetState(D2StatListStrc*,int){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*)){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}
void STATLIST_SetStatIfListIsValid(D2StatListStrc* p,int stat,int value,int){if(stat==1)p->skill=value;else if(stat==2)p->rank=value;else if(stat==3)p->count=value;else if(stat==4)p->ar=value;}
void STATLIST_AddStat(D2StatListStrc* p,int,int value,int){p->ar+=value;}
int STATLIST_GetStatValue(D2StatListStrc* p,int stat,int){return stat==1?p->skill:stat==2?p->rank:stat==3?p->count:p->ar;}
void D2COMMON_10476(D2StatListStrc* p,int until){p->expires=until;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){events++;}
bool STATES_CheckState(D2UnitStrc*,int){return enabled;}
void* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return nullptr;}
int SKILLS_GetSkillLevel(D2UnitStrc*,void*,int){return currentRank;}
void UNITS_SetOverlay(D2UnitStrc*,int,int){overlays++;}
'''
 for name in ['SKILLS_SrvSt23_AssasinChargeStrikes','SKILLS_StatRemoveCallback_ProgressiveStrike','SKILLS_SrvDo034_TigerStrike_CobraStrike_RoyalStrike','sub_6FCF5680']: cpp+=routine(ass,name)
 cpp+=r'''
int main(){bool first=true;printf("{\"build\":[");
for(int count:{0,1,2,3})for(int stored:{1,20})for(int rank:{1,3,20,255})for(int success:{0,1})for(int available:{0,1})for(int hasTarget:{0,1})for(int validRow:{0,1}){
row={};list={count,count*50,stored,254,321};exists=count>0;enabled=exists;selected=hasTarget;record=validRow;allocation=available;hit=success;drains=allocs=events=0;explicitAr=-1;saved={};
int result=SKILLS_SrvDo034_TigerStrike_CobraStrike_RoyalStrike(&game,&owner,254,rank);
if(!first)printf(",");first=false;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",count,stored,rank,success,available,hasTarget,validRow,result,exists,list.count,list.ar,list.rank,list.expires,allocs,drains,events);
}printf("],\"finish\":[");first=true;
for(int count:{0,1,2,3})for(int stored:{0,1,3,20,255})for(int current:{0,1,3,20,255})for(int success:{0,1})for(int active:{0,1})for(int listPresent:{0,1})for(int validRow:{0,1})for(int kind:{0,1,2}){
row={};row.nPrgDamage=kind;list={count,count*50,stored,254,321};currentRank=current;exists=listPresent;record=validRow;enabled=active;saved={success,17};overlays=0;selected=true;sub_6FCF5680(&owner,&saved);
if(!first)printf(",");first=false;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",count,stored,current,success,active,listPresent,validRow,kind,saved.dwEnDmgPct,overlays);
}printf("],\"start\":[");first=true;for(int targetPresent:{0,1})for(int reach:{0,1}){selected=targetPresent;inRange=reach;if(!first)printf(",");first=false;printf("[%d,%d,%d]",targetPresent,reach,SKILLS_SrvSt23_AssasinChargeStrikes(&game,&owner,254,1));}printf("]}\n");}
'''
 path=STAGE/'tiger-strike-source-probe.cpp'; path.write_text(cpp); binary=path.with_suffix('')
 subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(path),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)])); fixture=STAGE/'tiger-strike-source-probe.json'; fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 anim=(ROOT/'.local/research/AnimData.d2').read_bytes(); pos=0; animations=[]
 for _ in range(256):
  count=struct.unpack_from('<I',anim,pos)[0];pos+=4
  for _ in range(count):
   r=anim[pos:pos+160];pos+=160; code=r[:8].split(b'\0')[0].decode()
   if code.startswith('AIA1'): animations.append(dict(code=code,frames=struct.unpack_from('<i',r,8)[0],speed=struct.unpack_from('<i',r,12)[0],events=[(i,k) for i,k in enumerate(r[16:]) if k]))
 # Execute the complete scheduler separately; declared animation/unit services
 # supply cached metadata. No rewritten loop or inferred event fraction.
 timing_cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <vector>
#include <array>
#include <initializer_list>
#define __fastcall
#define __stdcall
#define D2_ASSERT(x) if(!(x))std::abort()
constexpr int NUM_WEAPON_CLASSES=14,NUMBER_OF_PLAYERCLASSES=7,UNIT_TYPES_COUNT=6,UNIT_PLAYER=0,WEAPONCLASS_HTH=0,PLRMODE_ATTACK1=4,PLRMODE_ATTACK2=5,PLRMODE_SPECIAL3=15,PLRMODE_SPECIAL4=16,EVENTTYPE_MODECHANGE=8,EVENTTYPE_ENDANIM=9;
struct D2AnimSeqTxt{};
struct Anim{int pFrameFlags[144]={};} anim;
struct D2UnitStrc{int dwUnitType=0,dwClassId=6,dwAnimMode=4,dwSeqSpeed=256,dwSeqFrameCount=0,wAnimSpeed=256,dwFrameCountPrecise=0,nSeqCurrentFramePrecise=0;void* pAnimSeq=nullptr;Anim* pAnimData=&anim;} unit;
struct D2GameStrc{int dwGameFrame=100;} game;
int weaponClass=0;std::vector<std::array<int,4>> events;
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int*,int*,int*,const char*,int){}
D2UnitStrc* D2Common_10434(D2UnitStrc*,int){return &unit;}
int ITEMS_GetWeaponClassId(D2UnitStrc*){return weaponClass;}
bool UNITS_CanDualWield(D2UnitStrc*){return true;}
void DATATBLS_GetSequenceEvent(D2AnimSeqTxt*,int,int* result){*result=0;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int kind,int frame,int arg,int ordinal){events.push_back({kind,frame,arg,ordinal});}
'''
 timing_names=['UNITS_GetFrameBonus','sub_6FCBCE70']
 timing_cpp+=routine(units,timing_names[0])+routine(sunits,timing_names[1])
 timing_cpp+='int main(){bool first=true;printf("[");\n'
 codes={'HTH':0,'1HS':2,'1HT':3,'STF':4,'2HS':5,'2HT':6,'HT1':12,'HT2':13}
 for index,animation in enumerate(animations):
  code=animation['code'][4:]
  if code not in codes: continue
  timing_cpp+=f'for(int advance:{{31,104,156,208,256,312,364,416,448}}){{weaponClass={codes[code]};unit.wAnimSpeed=advance;unit.dwFrameCountPrecise={animation["frames"]}*256;anim={{}};anim.pFrameFlags[{animation["events"][0][0]}]=1;events.clear();sub_6FCBCE70(&game,&unit);int action=0,end=0;for(auto e:events){{if(e[0]==8)action=e[1]-100;if(e[0]==9)end=e[1]-100;}}if(!first)printf(",");first=false;printf("[%d,%d,%d,%d,%d]",{index},advance,UNITS_GetFrameBonus(&unit),action,end);}}\n'
 timing_cpp+='printf("]\\n");}\n'
 tp=STAGE/'tiger-strike-timing-probe.cpp';tp.write_text(timing_cpp);tb=tp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-pie','-no-pie',str(tp),'-o',str(tb)],check=True)
 timing=json.loads(subprocess.check_output([str(tb)]));data['timing']=timing;fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.tigerstrike',legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines={n:hashlib.sha256(blocks[n].encode()).hexdigest() for n in ['SKILLS_SrvSt23_AssasinChargeStrikes','SKILLS_StatRemoveCallback_ProgressiveStrike','SKILLS_SrvDo034_TigerStrike_CobraStrike_RoyalStrike','sub_6FCF5680','UNITS_GetFrameBonus','sub_6FCBCE70']},reviewedRoutines={n:hashlib.sha256(blocks[n].encode()).hexdigest() for n in ['SKILLS_SrvDo001_Attack_LeftHandSwing','sub_6FCF77E0','sub_6FD12950','D2GAME_SKILLS_Handler_6FD12BA0']},modernTableHash=hashlib.sha256(modern).hexdigest(),probe=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases={k:len(v) for k,v in data.items()},sanitizers='ASan+UBSan'),cachedAnimation=dict(sha256=hashlib.sha256(anim).hexdigest(),records=animations,provenanceComplete=False),modernPolicy=dict(source='Pinned skills.json Attack row and Blizzard field descriptions',finishing=attack['finishing'],chargesToCast=attack['prgchargestocast'],chargesConsumed=attack['prgchargesconsumed'],releasedNotes='https://news.blizzard.com/en-gb/article/23788293/diablo-ii-resurrected-patch-2-4-ladder-now-live',meaning='Successful melee Attack consumes one charge per active charge-up. Miss/block retains charges. Ordinary Attack has no finisher Param8 always-hit grant. Obsolete full-list removal is not ported.'),established=['Start23 validates selected melee reach only. Do34 resolves the hit at release, allocates ordinary weapon damage first, then increments charges before durability/commit. Successful zero-damage contacts still count. Misses do not refresh; successful hits at cap3 refresh the375-frame expiry without adding further AR. Initial level is retained on refresh.', 'Tiger physical finisher ED equals charge count times calc1(max(initial stored rank,current effective rank)). Charging attacks do not execute this finisher consumer. Ordinary Attack explicitly supplies progressive_tohit; charging do34 supplies only its own skill AR.', 'Declared target, hit-result, allocation, stat-list and formula services isolate the complete unchanged routines. They do not prove source RNG, mitigation, weapon event ordering or current executable equivalence.'],uncertain=['Exact current consumption expiry/AR-list update implementation is not in the supplementary pre2.4 source. Retaining the deadline and proportional50percent AR per remaining charge is an explicit adapter requiring modern acceptance.', 'Cached legacy A1 timing has incomplete acquisition provenance. Modern attack-mode/alternate-hand/transformed timing and modern executable acceptance remain open.', 'Native melee, actual packet input, lifecycle, real gear and save/reload integration require separate verification. No class completion or final effects/icons certificate.'])
 out=ROOT/'tools/reference/tiger-strike-source-evidence.json'; rendered=json.dumps(evidence,indent=2)+'\n'
 if check: assert out.read_text()==rendered,'Tiger Strike source evidence stale'
 else: out.write_text(rendered)
 print('Tiger Strike complete source cases:',{k:len(v) for k,v in data.items()})
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
