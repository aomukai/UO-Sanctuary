#!/usr/bin/env python3
"""Compile unchanged resource stat-operation/callback blocks from pinned D2MOO."""
from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';BASE=STAGE/'d2moo-pinned-source'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
blobs={x['path']:x['sha'] for x in tree['tree'] if x['type']=='blob'};hashes={}
def source(path):
 raw=(BASE/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blobs[path],path
 hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
def block(text,start):
 end=text.index('{',start)+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[start:end]
def routine(text,name):
 m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;return block(text,m.start())
stat=source('source/D2Common/src/D2StatList.cpp');query=routine(stat,'sub_6FDB5830')
helper=routine(stat,'ComputeStatPercentage');cases={}
for op in [1,2,8,9,11]:
 at=query.index('\t\tcase '+str(op)+':');end=query.find('\n\t\tcase ',at+1);cases[op]=query[at:end]
callback=source('source/D2Game/src/ITEMS/ItemMode.cpp');full=routine(callback,'D2GAME_ITEMMODE_ServerStatlistCallback_6FC41910');resource=full[full.index('    case STAT_MAXHP:'):full.index('    case STAT_ITEM_NONCLASSSKILL:')]
summon=routine(source('source/D2Game/src/SKILLS/SkillNec.cpp'),'D2GAME_SetSummonPassiveStats_6FD0C530')
at=summon.index('    const int32_t nMaxHp = STATLIST_GetMaxLifeFromUnit(pPet);')
summonwrite=summon[at:summon.index('\n\n',at)]
loader=source('source/D2Common/src/DataTbls/ItemsTbls.cpp')
# This captures table dependency assembly/order; full loader is not linked into this probe.
at=loader.index('\tfor (int nStatId = 0;');loaderblock=block(loader,at)
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define __stdcall
using Packed=int;
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_ITEM=4,STAT_MAXHP=7,STAT_HITPOINTS=6,STAT_MAXMANA=9,STAT_MANA=8,STAT_MAXSTAMINA=11,STAT_STAMINA=10,STAT_HPREGEN=74;
struct D2SLayerStatIdStrc{int nPackedValue;static D2SLayerStatIdStrc MakeFromStatId(int stat){return {stat};}};
struct D2StatStrc{int nValue=0;};struct D2StatsArrayStrc{int value=0;};
struct D2CharStatsTxt{int nManaPerMagic=4,nStaminaPerVitality=4,nLifePerVitality=16;} chars;
struct D2MonStatsTxt{int dwDamageRegen=2;} monrow;struct MonsterData{D2MonStatsTxt* pMonstatsTxt=&monrow;};
struct D2StatListExStrc;
struct D2UnitStrc{int dwUnitType=0,dwClassId=4,hp=0,mana=0,stam=0,regen=0,maxhp=0;MonsterData* pMonsterData=nullptr;D2StatListExStrc* pStatListEx=nullptr;};
struct D2StatListExStrc{D2UnitStrc* pOwner=nullptr;int dwOwnerType=0;D2StatsArrayStrc Stats{},FullStats{};int extra=0,percent=0,level=1,scaled=0;};
struct D2ItemStatCostTxt{int nValShift=0;};D2ItemStatCostTxt levelrow;
struct Op{int nOp=0,nStat=1;uint16_t nOpBase=12;int nOpParam=3;};
int sub_6FDB5830(D2StatListExStrc* p,int stat){return stat==1?p->extra:stat==2?p->percent:stat==3?p->scaled:0;}
D2StatStrc scratch;
D2StatStrc* STATLIST_FindStat_6FDB6920(D2StatsArrayStrc* p,int){scratch.nValue=p->value;return &scratch;}
D2ItemStatCostTxt* ITEMS_GetItemStatCostTxtRecord(int){return &levelrow;}
bool STATLIST_IsExtended(D2StatListExStrc*){return true;}
int STATLIST_GetBaseStat_6FDB6340(D2StatListExStrc*,int,D2ItemStatCostTxt*){return 0;}
D2CharStatsTxt* UNITS_GetCharStatsTxtRecord(int){return &chars;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_HITPOINTS?p->hp:stat==STAT_MANA?p->mana:p->stam;}
int writes=0;
void STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int){writes++;if(stat==STAT_MAXHP)p->maxhp=value;else if(stat==STAT_HITPOINTS)p->hp=value;else if(stat==STAT_MANA)p->mana=value;else if(stat==STAT_STAMINA)p->stam=value;else p->regen=value;}
template<class T>T D2Clamp(T x,T lo,T hi){return std::clamp(x,lo,hi);}
'''+helper+r'''
int Query(D2StatListExStrc* pStatListEx,int nStatId,int base,int bonus,int percent,int perLevel,int level){
 D2ItemStatCostTxt* pItemStatCostTxtRecord=&levelrow;int nAccumulatedValue=base,nPreviousOpStatValue=nAccumulatedValue;
 for(Op opStatData: {Op{nStatId==STAT_MAXMANA?8:9,1},Op{nStatId==STAT_MAXSTAMINA?1:11,2},Op{2,3}}){
 pStatListEx->extra=bonus;pStatListEx->percent=percent;pStatListEx->FullStats.value=level;pStatListEx->scaled=perLevel;
 switch(opStatData.nOp){
'''+''.join(cases.values())+r'''
 default:break;}}
 return nAccumulatedValue;}
void Changed(D2UnitStrc* pOwner,int nStatId,int nPreviousValue,int nNewValue){switch(nStatId){
'''+resource+r'''
 default:break;}}
struct D2SkillsTxt{int dwCalc[1]={0};};int summonPercent;
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return summonPercent;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc* p){return p->maxhp;}
int MONSTERUNIQUE_CalculatePercentage(int v,int p,int b){return int(int64_t(v)*p/b);}
void FinishSummon(D2UnitStrc* pPet,int percent){D2UnitStrc* pUnit=pPet;D2SkillsTxt row;D2SkillsTxt* pSkillsTxtRecord=&row;int nSkillId=70,nSkillLevel=1;summonPercent=percent;
'''+summonwrite+r'''
}
int main(){std::printf("{\"statCases\":[");bool first=true;
for(int kind:{7,9,11})for(int type:{0,1})for(int base:{256,32000,64064})for(int bonus:{0,7})for(int percent:{0,35,92,300})for(int perlevel:{0,8}){
 D2UnitStrc owner;owner.dwUnitType=type;D2StatListExStrc list;list.pOwner=&owner;list.dwOwnerType=type;owner.pStatListEx=&list;
 int result=Query(&list,kind,base,bonus,percent,perlevel*256,37);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",kind,type,base,bonus,percent,perlevel,result);
}std::printf("],\"poolCases\":[");first=true;
for(int stat:{7,9,11})for(int type:{0,1})for(int oldmax:{0,128,256,25600,34567})for(int next:{0,256,32000,65000})for(int current:{0,1,128,12345}){
 if(next==0&&oldmax>0&&current>0)continue; // Invalid clamp bounds excluded, never silently redefined.
 D2UnitStrc owner;MonsterData md;owner.dwUnitType=type;owner.pMonsterData=type?&md:nullptr;owner.hp=owner.mana=owner.stam=current;writes=0;Changed(&owner,stat,oldmax,next);
 int value=stat==7?owner.hp:stat==9?owner.mana:owner.stam;
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d]",stat,type,oldmax,next,current,value,writes,owner.regen);
}std::printf("],\"summonCases\":[");first=true;
for(int base:{21,100,328})for(int flat:{0,160})for(int creation:{0,50,850})for(int bo:{35,92}){
 D2UnitStrc owner;owner.dwUnitType=1;owner.maxhp=(base+flat)*256;FinishSummon(&owner,creation);
 D2StatListExStrc list;list.pOwner=&owner;list.dwOwnerType=1;owner.pStatListEx=&list;int result=Query(&list,7,owner.maxhp,0,bo,0,37);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",base,flat,creation,bo,owner.maxhp,owner.hp,result);
}std::printf("]}\n");}
'''
path=STAGE/'battle-orders-source-probe.cpp';path.write_text(cpp);binary=STAGE/'battle-orders-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
for kind,type,base,bonus,percent,perlevel,result in data['statCases']:
 bonusfp=0 if type else bonus*(4 if kind in [9,11] else 16)*64
 # Keep the actual floating percent and source truncation; whole-FP integer division can differ.
 expected=base+bonusfp+int(base*(percent/100.0))+(37*perlevel*256>>3)
 assert result==expected,(kind,type,base,bonus,percent,perlevel,result,expected)
for stat,type,old,new,current,result,writes,regen in data['poolCases']:
 changed=new!=old and old>0 and current>0
 expect=max(1,min(new,int(current/max(old,256)*new))) if changed else current
 assert result==expect,(stat,type,old,new,current,result,expect)
 assert writes==int(changed)*(2 if stat==7 and type else 1)
 if stat==7 and type and changed:assert regen==2*(new>>8)>>4
for base,flat,creation,bo,maximum,life,result in data['summonCases']:
 initial=(base+flat)*256;expected=initial+initial*creation//100
 assert maximum==life==expected and result==expected+int(expected*(bo/100.0))
(STAGE/'battle-orders-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
rows=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/itemstatcost.json').read_text())
resourceops=[{k:v for k,v in r.items() if k in ['Stat','*ID','ValShift','op','op param','op base','op stat1','op stat2']} for r in sorted(rows.values(),key=lambda r:r.get('*ID',9999)) if any(r.get('op stat'+str(i)) in ['maxhp','maxmana','maxstamina'] for i in range(1,4))]
evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,resourceOps=resourceops,
 probe=dict(caseCounts={k:len(v) for k,v in data.items()},blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in [('percent',helper),('statQuery',query),('resourceCallback',resource),('dependencyLoader',loaderblock),('summonBaseWrite',summonwrite)]},dataPath='.local/class-skills/battle-orders-source-probe.json'),
 established=['Resource percentages use the pre-operation resource subtotal; equipment energy/vitality and per-level resource operations are added separately and are not multiplied by BO.',
 'The resource callback preserves the current fraction on an actual maximum change, truncates its double result to FP256, clamps positive pools to[1,newMax] and leaves zero pools/zero previous maxima unchanged. Equal maxima do not refresh/refill.',
 'Monster maximum-life changes also recompute HP regeneration from source regeneration and new whole maximum life.',
 'Summon passive maxhp is added directly to its base; the compiled creation block writes its full FP256 life scaling back as base maximum and current life. Later BO multiplies that stored base, retaining fractional summon creation life before projection.',
 'Modern stat dependencies are assembled in source ID order by the supplementary loader; bonus energy/vitality, percentage resources and per-level additions are distinct.'],
 stubs=['The probe links unchanged relevant switch cases and helper/callback blocks. It supplies base/additional stat arrays, class coefficients, raw percentage/per-level values and op ordering; no full statlist world/event engine is claimed.',
 'Only player/monster resource cases are evaluated; item-owned op1 behavior is present but excluded from the assertions. Floating percent boundaries tested are bounded.',
 'Summon creation cases supply the already-added flat mastery life and creation formula percentage; the unchanged final base/current write is compiled, while the passive-stat loop and full summon world pipeline are source-reviewed.',
 'Modern ItemStatCost field projection is retained; the complete original table loader is hashed/reviewed rather than compiled.'],
 limits=['Legacy source does not certify current D2R executable behavior.','By-time op6, negative/overflow extremes, zero-next-maximum callback bounds, complete item stat graphs, delayed callback ordering, native geometry and owner/client acceptance remain open.','Native BO maximum/current-resource and missile consumers are verified separately in battle-orders-native-evidence.json; this probe alone establishes only bounded supplementary source contracts.'])
(ROOT/'tools/reference/battle-orders-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print('Battle Orders bounded resource probes passed:',{k:len(v) for k,v in data.items()})
