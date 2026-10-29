#!/usr/bin/env python3
"""Execute complete recursive stat query/update bodies with owned operation data."""
import argparse
import csv
import hashlib
import json
import subprocess
from pathlib import Path
from review_weapon_event_order import function
from probe_item_maximum_operations import block
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'item-stat-values-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(exist_ok=True);treepath=BASE/'d2moo-source-tree.json';tree=json.loads(treepath.read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs={str(treepath.relative_to(ROOT)):sha(treepath)}
    def source(name):
        p=BASE/'d2moo-pinned-source'/name;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==name)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    key_header=source('source/D2Common/include/D2StatList.h');assert 'uint16_t nLayer;' in key_header and 'uint16_t nStat;' in key_header
    raw=source('source/D2Common/src/D2StatList.cpp');loader=source('source/D2Common/src/DataTbls/ItemsTbls.cpp');header=source('source/D2Common/include/DataTbls/ItemsTbls.h')
    compile_source=source('source/D2Common/src/DataTbls/DataTbls.cpp');assert 'pTxt = D2_CALLOC_POOL(nullptr, dwSize * nRecordCount);' in function(compile_source,'DATATBLS_CompileTxt')
    env=source('source/D2Common/include/D2Environment.h');mods=source('source/D2Common/src/Items/ItemMods.cpp')
    assert 'uint16_t unk0x5E[64]' in header and 'D2OpStatDataStrc pOpStatData[16]' in header
    loop=block(loader,loader.index('\tfor (int nStatId = 0;'))
    priorpath=ROOT/'tools/reference/item-maximum-operations-source-evidence.json';prior=json.loads(priorpath.read_text());inputs[str(priorpath.relative_to(ROOT))]=sha(priorpath)
    ownedpath=BASE/'npc-item-suppliers-source/itemstatcost.txt';assert sha(ownedpath)==prior['inputs'][str(ownedpath.relative_to(ROOT))];inputs[str(ownedpath.relative_to(ROOT))]=sha(ownedpath)
    p=ROOT/'tools/reference/d2r-named-equipment/json/itemstatcost.json';assert sha(p)==prior['inputs'][str(p.relative_to(ROOT))];inputs[str(p.relative_to(ROOT))]=sha(p)
    data=json.loads(p.read_text());owned={r['Stat']:r for r in csv.DictReader(ownedpath.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t') if r.get('Stat')}
    fields=['*ID','op','op param','op base','op stat1','op stat2','op stat3','ValShift','damagerelated','fMin','fCallback','MinAccr','keepzero'];compared=0
    for name,row in data.items():
        for field in fields:assert str(row.get(field,''))==owned[name].get(field,''),(name,field);compared+=1
    assert compared==4784 and len(data)==368
    ordinal={name:i for i,name in enumerate(owned)}
    assert all(row['lineNumber']==ordinal[name] for name,row in data.items())
    comment_mismatches=[name for name,row in data.items() if row['*ID']!=ordinal[name]]
    assert comment_mismatches==['passive_mastery_gethit_rate']
    assert '{ "stat", TXTFIELD_NAMETOINDEX, 0, 0,' in loader and '"*ID"' not in function(loader,'DATATBLS_LoadItemStatCostTxt')
    ids=ordinal
    cpp=r'''
#include <algorithm>
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <new>
#include <vector>
#define __fastcall
using BOOL=int;
#define __stdcall
#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))
#define TRUE 1
#define FALSE 0
#define D2_ASSERT assert
#define D2_VERIFY assert
#define D2_ASSERTM(x,msg) assert(x)
#define D2_REALLOC_POOL(pool,p,size) (p)
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_ITEM=4,STAT_MAXHP=7,STAT_MAXMANA=9,STAT_MAXSTAMINA=11;
constexpr int ITEMSTATCOSTFLAGINDEX_DAMAGERELATED=2,ITEMSTATCOSTFLAGINDEX_HP_MANA_STAMINA=5,ITEMSTATCOSTFLAGINDEX_HP=6,ITEMSTATCOSTFLAGINDEX_MANA=7,ITEMSTATCOSTFLAGINDEX_STAMINA=8,ITEMSTATCOSTFLAGINDEX_FMIN=10,ITEMSTATCOSTFLAGINDEX_FCALLBACK=11;
constexpr unsigned STATLIST_SET=0x2000,STATLIST_PERMANENT=0x20000000,STATLIST_DYNAMIC=0x40000000,STATLIST_EXTENDED=0x80000000,STATLIST_TEMPONLY=4,STATLIST_NEWLENGTH=2;
constexpr int BODYLOC_SWRARM=11,BODYLOC_SWLARM=12;
constexpr int STAT_OP_NONE=0,STAT_OP_APPLY_TO_ITEM=4,STAT_OP_APPLY_TO_ITEM_PCT=5,STAT_OP_BY_TIME=6,STAT_OP_BY_TIME_PCT=7,STAT_OP_ADD_ITEM_STAT_PCT=13;
constexpr int ENV_FULL_CIRCLE_DEGREES=360,ENV_HALF_CIRCLE_DEGREES=180,NUM_ENVIRONMENT_PERIODS_OF_DAY=4,NUM_ENVIRONMENT_CYCLES=6;
constexpr auto gdwBitMasks=[](){std::array<unsigned,32>a{};for(int i=0;i<32;i++)a[i]=1u<<i;return a;}();
struct D2SLayerStatIdStrc{using PackedType=uint32_t;union{uint32_t nPackedValue=0;struct{uint16_t nLayer,nStat;};};static D2SLayerStatIdStrc Make(int layer,int stat){D2SLayerStatIdStrc r;r.nStat=stat;r.nLayer=layer;return r;}static D2SLayerStatIdStrc MakeFromStatId(int n){return Make(0,n);}static D2SLayerStatIdStrc FromPackedType(uint32_t n){D2SLayerStatIdStrc r;r.nPackedValue=n;return r;}};
struct D2StatStrc:D2SLayerStatIdStrc{int nValue=0;};
struct D2StatsArrayStrc{D2StatStrc storage[512]{};D2StatStrc*pStat=storage;int nStatCount=0,nCapacity=512;static constexpr int nGrowthAmount=16,nShrinkThreshold=32;};
struct D2UnitStrc;
struct D2StatListStrc{D2StatsArrayStrc Stats,FullStats;void*pMemPool=nullptr;int id=0,dwOwnerType=4,dwStateNo=13;D2UnitStrc*pOwner=nullptr,*pUnit=nullptr;unsigned dwFlags=STATLIST_EXTENDED;D2StatListStrc*pParent=nullptr,*pNextLink=nullptr,*pPrevLink=nullptr,*pMyLastList=nullptr,*pMyStats=nullptr;void*pGame=nullptr;void(*pfOnValueChanged)(void*,D2UnitStrc*,D2UnitStrc*,uint32_t,int,int)=nullptr;void(*fpStatRemove)(D2UnitStrc*,int,D2StatListStrc*)=nullptr;};using D2StatListExStrc=D2StatListStrc;
struct D2UnitStrc{int id=0,dwUnitType=4,dwClassId=0;D2StatListExStrc*pStatListEx=nullptr;void*pDrlgAct=nullptr;};
struct D2OpStatDataStrc{uint16_t nOpBase=0,nStat=0;uint8_t nOp=0,nOpParam=0;};
struct D2ItemStatCostTxt{uint16_t wStatId=0;unsigned dwItemStatFlags=0;bool bHasOpStatData=false,bIsBaseOfOtherStatOp=false,bHasOpApplyingToItem=false;D2OpStatDataStrc pOpStatData[16];int nValShift=0,nKeepZero=0,dwMinAccr=0;uint8_t nOp=0,nOpParam=0;uint16_t wOpBase=65535,wOpStat[3]={65535,65535,65535},unk0x5E[64];};std::vector<D2ItemStatCostTxt>rows;
struct Tables{int nItemStatCostTxtRecordCount=0;D2ItemStatCostTxt*pItemStatCostTxt=nullptr;bool bCompileTxt=true;void*pItemStatCostLinker=nullptr;}tables;Tables*sgptDataTables=&tables;
struct D2CharStatsTxt{int nManaPerMagic=4,nStaminaPerVitality=4,nLifePerVitality=16;}charstats;
std::vector<std::array<int,5>>trace;int minute=0;
const char*FOG_10255(void*,int,int){return "stat";}void FOG_Trace(const char*,int,const char*){assert(false);}
bool STATLIST_IsExtended(D2StatListStrc*p){return p&&(p->dwFlags&STATLIST_EXTENDED);}
D2StatListExStrc*STATLIST_StatListExCast(D2StatListStrc*p){return STATLIST_IsExtended(p)?p:nullptr;}
D2ItemStatCostTxt*ITEMS_GetItemStatCostTxtRecord(int n){return n>=0&&n<int(rows.size())?&rows[n]:nullptr;}
int StatArray_FindInsertionIndex(D2StatsArrayStrc*a,uint32_t packed,bool*found){int i=0;while(i<a->nStatCount&&a->pStat[i].nPackedValue<packed)i++;*found=i<a->nStatCount&&a->pStat[i].nPackedValue==packed;return i;}
int StatArray_DichotomicSearch(D2StatsArrayStrc*a,uint32_t packed){bool found;int i=StatArray_FindInsertionIndex(a,packed,&found);return found?i:-1;}
D2StatStrc*StatArray_InsertStat(void*,D2StatsArrayStrc*a,uint32_t packed,int i){assert(a->nStatCount<512);memmove(a->pStat+i+1,a->pStat+i,(a->nStatCount-i)*sizeof(D2StatStrc));a->nStatCount++;a->pStat[i]={};a->pStat[i].nPackedValue=packed;return&a->pStat[i];}
D2StatStrc*STATLIST_GetOrInsertStat(void*pool,D2StatsArrayStrc*a,uint32_t packed){bool found;int i=StatArray_FindInsertionIndex(a,packed,&found);return found?&a->pStat[i]:StatArray_InsertStat(pool,a,packed,i);}
int ENVIRONMENT_GetPeriodOfDayFromAct(void*,int*time){*time=minute;return minute/90;}
D2CharStatsTxt*UNITS_GetCharStatsTxtRecord(int){return&charstats;}
void Changed(void*,D2UnitStrc*owner,D2UnitStrc*source,uint32_t packed,int old,int value){trace.push_back({owner?owner->id:0,source?source->id:0,int(packed),old,value});}
void STATLIST_InsertStatModOrFail_6FDB7690(D2StatListStrc*,uint32_t){assert(false);}
int ITEMS_GetBodyLocation(D2UnitStrc*){return 4;}
bool BITMANIP_SetBitsValueForMask(unsigned&flags,unsigned mask,bool value){unsigned old=flags;flags=value?flags|mask:flags&~mask;return old!=flags;}
int sub_6FDB5830(D2StatListExStrc*,uint32_t);
int sub_6FDB64A0(D2StatListExStrc*,uint32_t,D2ItemStatCostTxt*,D2UnitStrc*);
int STATLIST_FindStatIndex_6FDB6300(D2StatsArrayStrc*,uint32_t);
D2StatStrc*STATLIST_FindStat_6FDB6920(D2StatsArrayStrc*,uint32_t);
D2StatStrc*STATLIST_InsertStatOrFail_6FDB6970(void*,D2StatsArrayStrc*,uint32_t);
void STATLIST_RemoveStat_6FDB6A30(void*,D2StatsArrayStrc*,D2StatStrc*);
int STATLIST_GetBaseStat_6FDB6340(D2StatListStrc*,uint32_t,D2ItemStatCostTxt*);
int STATLIST_GetTotalStat_6FDB63E0(D2StatListStrc*,uint32_t,D2ItemStatCostTxt*);
void STATLIST_UpdateUnitStat_6FDB6AB0(D2StatListExStrc*,uint32_t,int,D2ItemStatCostTxt*,D2UnitStrc*);
void sub_6FDB6C10(D2StatListExStrc*,uint32_t,int,D2UnitStrc*);
void D2Common_ExpireStatList_6FDB6E30(D2StatListStrc*);
void STATLIST_MergeStatLists(D2UnitStrc*,D2UnitStrc*,int);
int D2Common_11274(D2UnitStrc*,D2UnitStrc*);int D2Common_11275(D2UnitStrc*,D2UnitStrc*);
'''
    routines={}
    names=['STATLIST_NotifyUnitOfStatValueChange','STATLIST_SetUnitStatNewValue','ComputeStatPercentage','sub_6FDB5830','STATLIST_FindStatIndex_6FDB6300','STATLIST_ApplyMinValue','STATLIST_GetBaseStat_6FDB6340','STATLIST_GetTotalStat_6FDB63E0','sub_6FDB64A0','STATLIST_FindStat_6FDB6920','STATLIST_InsertStatOrFail_6FDB6970','STATLIST_RemoveStat_6FDB6A30','STATLIST_UpdateUnitStat_6FDB6AB0','sub_6FDB6C10','STATLIST_SetBaseStat','D2Common_ExpireStatList_6FDB6E30','D2COMMON_10475_PostStatToStatList','STATLIST_MergeStatLists','STATLIST_GetOwner','D2Common_11274_11275_Impl','D2Common_11274','D2Common_11275']
    # The complete by-time numeric routine is compiled too; only its world clock
    # and the player class coefficients are explicit fixture services.
    body=function(mods,'ITEMMODS_GetByTimeAdjustment');cpp+=body+'\n';routines['ITEMMODS_GetByTimeAdjustment']=hashlib.sha256(body.encode()).hexdigest()
    for name in names:
        body=function(raw,name);cpp+=body+'\n';routines[name]=hashlib.sha256(body.encode()).hexdigest()
    cpp+='void Assemble(){int nOpBase=0;\n'+loop+'\n}\nvoid Init(){rows.assign(368,{});\n'
    for name,row in data.items():
        n=ids[name];flags=(4 if row.get('damagerelated',0) else 0)|(1024 if row.get('fMin',0) else 0)|(2048 if row.get('fCallback',0) else 0)
        cpp+=f'rows[{n}].wStatId={n};rows[{n}].nOp={row.get("op",0)};rows[{n}].nOpParam={row.get("op param",0)};rows[{n}].wOpBase={ids[row["op base"]] if row.get("op base") else 65535};rows[{n}].nValShift={row.get("ValShift",0)};rows[{n}].dwItemStatFlags={flags};rows[{n}].dwMinAccr={row.get("MinAccr",0)};rows[{n}].nKeepZero={row.get("keepzero",0)};\n'
        for i in range(3):cpp+=f'rows[{n}].wOpStat[{i}]={ids[row["op stat"+str(i+1)]] if row.get("op stat"+str(i+1)) else 65535};\n'
    cpp+=r'''
for(auto&r:rows)std::fill(std::begin(r.unk0x5E),std::end(r.unk0x5E),65535);tables.nItemStatCostTxtRecordCount=rows.size();tables.pItemStatCostTxt=rows.data();Assemble();}
D2StatListStrc root,item,bonus;D2UnitStrc actor,weapon;
void Raw(D2StatsArrayStrc&a,int id,int value){if(value)STATLIST_GetOrInsertStat(nullptr,&a,D2SLayerStatIdStrc::MakeFromStatId(id).nPackedValue)->nValue=value;}
void Reset(int kind,int level,int dynamic,int clock){new(&root)D2StatListStrc;new(&item)D2StatListStrc;new(&bonus)D2StatListStrc;actor={1,kind,0,&root,clock?&minute:nullptr};weapon={2,4,0,&item,clock?&minute:nullptr};root.id=1;item.id=2;bonus.id=3;root.pOwner=&actor;root.dwOwnerType=kind;item.pOwner=&weapon;item.dwOwnerType=4;root.pfOnValueChanged=item.pfOnValueChanged=Changed;
for(auto pair:{std::array{0,100},std::array{1,20},std::array{2,50},std::array{3,30},std::array{7,1000*256},std::array{9,300*256},std::array{11,100*256},std::array{12,level},std::array{21,7},std::array{22,14},std::array{31,80}}){Raw(root.Stats,pair[0],pair[1]);Raw(root.FullStats,pair[0],pair[1]);}
Raw(item.Stats,21,59);Raw(item.Stats,22,94);Raw(item.Stats,31,167);
// Model an ordinary item's attached property list, with current metadata.
item.pMyLastList=&bonus;bonus.pParent=&item;bonus.dwFlags=STATLIST_EXTENDED|(dynamic?STATLIST_DYNAMIC:0);
for(auto pair:{std::array{7,32*256},std::array{31,30},std::array{17,200},std::array{18,200},std::array{216,320},std::array{218,5},std::array{219,20},std::array{214,4},std::array{269,(300<<12)|(256<<2)},std::array{272,(280<<12)|(256<<2)},std::array{273,(270<<12)|(256<<2)}}){Raw(bonus.FullStats,pair[0],pair[1]);}
for(auto v:{std::array{0,3},std::array{1,5},std::array{2,7},std::array{3,9}}){Raw(bonus.FullStats,v[0],v[1]);}
trace.clear();}
void Array(D2StatsArrayStrc&a){printf("[");for(int i=0;i<a.nStatCount;i++)printf("%s[%u,%d]",i?",":"",a.pStat[i].nPackedValue,a.pStat[i].nValue);printf("]");}
void State(){printf("[%u,%u,",root.dwFlags,item.dwFlags);Array(root.Stats);printf(",");Array(root.FullStats);printf(",");Array(item.Stats);printf(",");Array(item.FullStats);printf("]");}
void Trace(){printf("[");bool first=true;for(auto v:trace){printf("%s[%d,%d,%d,%d,%d]",first?"":",",v[0],v[1],v[2],v[3],v[4]);first=false;}printf("]");}
int main(){Init();printf("{\"plans\":[");bool first=true;for(int id=0;id<368;id++){auto&r=rows[id];printf("%s[%d,%u,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",id,r.dwItemStatFlags,r.nKeepZero,r.dwMinAccr,r.nValShift,r.bIsBaseOfOtherStatOp,r.bHasOpStatData,r.bHasOpApplyingToItem,r.nOp,r.nOpParam);first=false;for(int i=0;i<3;i++)printf("%s%d",i?",":"",r.wOpStat[i]);printf("],[");bool comma=false;for(auto n:r.unk0x5E){if(n==65535)break;printf("%s%d",comma?",":"",n);comma=true;}printf("],[");comma=false;for(auto op:r.pOpStatData){if(!op.nOp)break;printf("%s[%d,%d,%d,%d]",comma?",":"",op.nOp,op.nStat,op.nOpBase,op.nOpParam);comma=true;}printf("]]");}
printf("],\"queries\":[");first=true;for(int kind:{0,1,4})for(int level:{1,37,99})for(int dynamic:{0,1})for(int clock:{0,1})for(int time:{0,91,180,359}){minute=time;Reset(kind,level,dynamic,clock);item.pUnit=&actor;printf("%s[[%d,%d,%d,%d,%d],[",first?"":",",kind,level,dynamic,clock,time);first=false;for(int id=0;id<368;id++)printf("%s%d",id?",":"",sub_6FDB5830(&item,D2SLayerStatIdStrc::MakeFromStatId(id).nPackedValue));printf("],[");for(int id=0;id<368;id++)printf("%s%d",id?",":"",sub_6FDB5830(&root,D2SLayerStatIdStrc::MakeFromStatId(id).nPackedValue));printf("]]");}
printf("],\"updates\":[");first=true;for(int level:{1,37,99})for(int include:{0,1})for(int stat:{7,12,17,18,21,22,31,216,218,219})for(int value:{0,1,23,256})for(int layer:{0,3}){minute=91;Reset(1,level,0,0);printf("%s[[%d,%d,%d,%d,%d],[",first?"":",",level,include,stat,value,layer);first=false;
// Construct item full stats through actual source recalculation before posting.
for(int id:{7,17,18,21,22,31,214,216,218,219})sub_6FDB64A0(&item,D2SLayerStatIdStrc::MakeFromStatId(id).nPackedValue,&rows[id],nullptr);
trace.clear();State();STATLIST_MergeStatLists(&actor,&weapon,include);printf(",");State();int changed=STATLIST_SetBaseStat(&item,stat,value,layer,&weapon);printf(",");State();int repeat=STATLIST_SetBaseStat(&item,stat,value,layer,&weapon);printf(",");State();STATLIST_SetBaseStat(&root,12,level+1,0,&actor);printf(",");State();D2Common_ExpireStatList_6FDB6E30(&item);printf(",");State();printf("],[%d,%d],",changed,repeat);Trace();printf("]");}printf("]}\n");}
'''
    for name,digest in routines.items():assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==digest
    p=STAGE/'source.cpp';p.write_text(cpp);compiler=STAGE/'compiler.log';sanitizer=STAGE/'sanitizer.log'
    with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(STAGE/'probe')],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);sanitizer.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
    result=json.loads(run.stdout);assert len(result['plans'])==368 and len(result['queries'])==144 and len(result['updates'])==480
    fixture=BASE/'item-stat-values-source-probe.json';encoded=json.dumps(result,separators=(',',':'))+'\n'
    if check:assert fixture.read_text()==encoded
    else:fixture.write_text(encoded)
    inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__).resolve())
    receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],component='Recursive item stat values,recalculation,propagation and setters',currentExecutableParity=False,nativeConsumerAccepted=False,inputs=inputs,compiledUnchangedRoutineHashes=routines,compiledLoaderBlockHash=hashlib.sha256(loop.encode()).hexdigest(),verified=dict(ownedComparedFields=compared,ordinalRows=368,commentIdMismatches=comment_mismatches,statRows=368,queryContexts=144,queryResults=144*368*2,updateContexts=480,updateStages=6,completeRoutines=len(routines)),artifacts={str(p.relative_to(ROOT)):sha(p) for p in [STAGE/'source.cpp',compiler,sanitizer,fixture,*sorted((STAGE/'diagnostics').rglob('*'))] if p.is_file()},established=['Complete recursive query,recalculation,propagation,setter,stat-array update and existing attachment bodies compose under owned operation metadata. Current MinAccr,shift,callback,keepzero and operation fields are corroborated. Full by-time numeric routine uses an explicit fixture clock; player class coefficients are declared services. Memory lookup/insertion/allocation are declared bounded array services.','The ignored *ID comment duplicates213at passive_mastery_gethit_rate;the owned row order and datamined lineNumber identify212. The loader maps names to row indices,so this projection uses all368corroborated ordinals. Prior selected operation probes did not exercise this passive.','Unused incoming operation slots retain zero-filled nOpBase,matching the table compiler allocation;unlike missing outgoing targets,they are not filled with65535. The earlier fixture sentinel initialization is retained in diagnostics.','Packed keys use the actual header ordering:low16bits layer,high16bits stat. Layer0and3updates exercise independent keys and ordered arrays. Prior attachment trace fixtures use abstract IDs;the native compatibility check explicitly translates those IDs.','Both item and recipient queries use current operation plans. Update cases include posting,changing/repeating an item base stat,changing recipient level and detaching with exact full/base arrays,flags and value-change callbacks. No live NPC actor stat application is certified.'],remaining=['Native numeric integration,actual actor effects,source level mapping,multi-item sibling/lifecycle,reentrant callbacks and current executable parity remain unfinished. Fixture character coefficients are not an accepted player-class adapter. Fixture clock is not Sanctuary world-time mapping. Existing player base-ED rounding remains unchanged.'])
    path=ROOT/'tools/reference/item-stat-values-source-evidence.json';encoded=json.dumps(receipt,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Item stat values:',json.dumps(receipt['verified']))
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
