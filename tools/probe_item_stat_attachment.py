#!/usr/bin/env python3
"""Run complete item stat attachment/detachment policies with explicit services."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'item-stat-attachment-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(exist_ok=True);treepath=BASE/'d2moo-source-tree.json';tree=json.loads(treepath.read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs={str(treepath.relative_to(ROOT)):sha(treepath)}
    def source(name):
        p=BASE/'d2moo-pinned-source'/name;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==name)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    raw=source('source/D2Common/src/D2StatList.cpp');header=source('source/D2Common/include/D2StatList.h')
    inventory=source('source/D2Common/include/D2Inventory.h');enum=re.search(r'enum D2C_PlayerBodyLocs\s*\{(.*?)\}',inventory,re.S).group(1);names=re.findall(r'^\s*(BODYLOC_\w+)',enum,re.M);assert names.index('BODYLOC_SWRARM')==11 and names.index('BODYLOC_SWLARM')==12
    for name,value in [('STATLIST_TEMPONLY','0x4'),('STATLIST_NEWLENGTH','0x2'),('STATLIST_SET','0x2000'),('STATLIST_PERMANENT','0x20000000'),('STATLIST_DYNAMIC','0x40000000'),('STATLIST_EXTENDED','0x80000000')]:assert re.search(name+r'\s*=\s*'+value,header)
    cpp=r'''
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __stdcall
#define __fastcall
#define D2_ASSERT assert
#define FALSE 0
#define TRUE 1
using BOOL=int;
constexpr unsigned STATLIST_SET=0x2000,STATLIST_PERMANENT=0x20000000,STATLIST_DYNAMIC=0x40000000,STATLIST_EXTENDED=0x80000000,STATLIST_TEMPONLY=4,STATLIST_NEWLENGTH=2;
constexpr int UNIT_ITEM=4,BODYLOC_SWRARM=11,BODYLOC_SWLARM=12,ITEMSTATCOSTFLAGINDEX_DAMAGERELATED=2;unsigned gdwBitMasks[]={1,2,4};
struct D2SLayerStatIdStrc{union{uint32_t nPackedValue;struct{uint16_t nStat,nLayer;};};D2SLayerStatIdStrc():nPackedValue(0){}};
struct D2StatStrc:D2SLayerStatIdStrc{int nValue=0;};
struct D2StatsArrayStrc{D2StatStrc*pStat=nullptr;int nStatCount=0;};struct D2UnitStrc;
struct D2StatListStrc{unsigned dwFlags=0;int dwStateNo=13;D2StatsArrayStrc Stats,FullStats;D2StatListStrc*pParent=nullptr,*pNextLink=nullptr,*pPrevLink=nullptr,*pMyLastList=nullptr,*pMyStats=nullptr;D2UnitStrc*pOwner=nullptr,*pUnit=nullptr;void(*fpStatRemove)(D2UnitStrc*,int,D2StatListStrc*)=nullptr;};using D2StatListExStrc=D2StatListStrc;
struct D2UnitStrc{int id=0,dwUnitType=4,body=4;D2StatListStrc*pStatListEx=nullptr;};
struct D2ItemStatCostTxt{unsigned dwItemStatFlags=0;bool bHasOpApplyingToItem=false;};
std::vector<std::array<int,5>>trace;int sums[3][3]{};D2UnitStrc a,b,item;D2StatListStrc astats,bstats,node;D2StatStrc base[3],full[3];D2ItemStatCostTxt records[3];
void T(int id,int a=0,int b=0,int c=0,int d=0){trace.push_back({id,a,b,c,d});}
bool STATLIST_IsExtended(D2StatListStrc*p){return p&&(p->dwFlags&STATLIST_EXTENDED);}
D2StatListExStrc*STATLIST_StatListExCast(D2StatListStrc*p){return STATLIST_IsExtended(p)?p:nullptr;}
D2ItemStatCostTxt*ITEMS_GetItemStatCostTxtRecord(int id){return id>=0&&id<3?&records[id]:nullptr;}
int ITEMS_GetBodyLocation(D2UnitStrc*p){T(1,p->id,p->body);return p->body;}
void sub_6FDB64A0(D2StatListExStrc*p,uint32_t packed,D2ItemStatCostTxt*,int){T(2,p->pUnit?p->pUnit->id:0,int(packed));}
void sub_6FDB6C10(D2StatListExStrc*p,uint32_t packed,int value,D2UnitStrc*owner){int target=p==&astats?1:2;assert(packed<3);sums[target][packed]+=value;T(3,target,int(packed),value,owner?owner->id:0);}
void Removed(D2UnitStrc*p,int state,D2StatListStrc*q){T(4,p->id,state,q->pParent!=nullptr,q->pUnit!=nullptr);}
bool BITMANIP_SetBitsValueForMask(unsigned&flags,unsigned mask,bool value){unsigned old=flags;flags=value?flags|mask:flags&~mask;return flags!=old;}
void D2Common_ExpireStatList_6FDB6E30(D2StatListStrc*);
void STATLIST_MergeStatLists(D2UnitStrc*,D2UnitStrc*,BOOL);
BOOL D2Common_11274(D2UnitStrc*,D2UnitStrc*);BOOL D2Common_11275(D2UnitStrc*,D2UnitStrc*);
'''
    routines={}
    for name in ['D2Common_ExpireStatList_6FDB6E30','D2COMMON_10475_PostStatToStatList','STATLIST_MergeStatLists','STATLIST_GetOwner','D2Common_11274_11275_Impl','D2Common_11274','D2Common_11275']:
        body=function(raw,name);cpp+=body+'\n';routines[name]=hashlib.sha256(body.encode()).hexdigest()
    cpp+=r'''
int Parent(D2StatListStrc*p){return p==&astats?1:p==&bstats?2:0;}
void Snapshot(D2UnitStrc*source){BOOL full=7;auto owner=STATLIST_GetOwner(source,&full);assert(owner==STATLIST_GetOwner(source,nullptr));
printf("[%u,%u,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",node.dwFlags,astats.dwFlags,Parent(node.pParent),node.pUnit?node.pUnit->id:0,sums[1][0],sums[1][1],sums[1][2],sums[2][0],sums[2][1],sums[2][2],owner?owner->id:0,full);}
void Reset(int type,int body,int nodeEx,int targetEx,int prior,int flags,int hasNode){
a={};b={};item={};astats={};bstats={};node={};a.id=1;b.id=2;item.id=29;item.dwUnitType=type;item.body=body;a.pStatListEx=&astats;b.pStatListEx=&bstats;item.pStatListEx=hasNode?&node:nullptr;
astats.dwFlags=targetEx?STATLIST_EXTENDED:0;bstats.dwFlags=STATLIST_EXTENDED;astats.pOwner=&a;bstats.pOwner=&b;node.pOwner=&item;node.fpStatRemove=Removed;
node.dwFlags=(nodeEx?STATLIST_EXTENDED:0)|(flags&1?STATLIST_SET:0)|(flags&2?STATLIST_DYNAMIC:0)|(flags&4?STATLIST_PERMANENT:0)|(flags&8?STATLIST_TEMPONLY:0);
for(int i=0;i<3;i++){base[i]={};full[i]={};base[i].nStat=full[i].nStat=i;base[i].nValue=(i+1)*10;full[i].nValue=(i+1)*12;records[i]={};records[i].dwItemStatFlags=i?4:0;records[i].bHasOpApplyingToItem=i==2;}
node.Stats={base,3};node.FullStats={full,3};for(auto&row:sums)for(int&n:row)n=0;
if(prior){node.pParent=&bstats;node.pUnit=&b;if(node.dwFlags&STATLIST_SET){bstats.pMyStats=&node;}else{bstats.pMyLastList=&node;auto*p=nodeEx?full:base;for(int i=0;i<3;i++)if(!(node.dwFlags&STATLIST_DYNAMIC)||i==0)sums[2][i]=p[i].nValue;}}
trace.clear();}
int main(){bool first=true;printf("[");
for(int type:{1,4})for(int body:{4,11,12})for(int target:{0,1})for(int source:{0,1})for(int hasNode:{0,1})for(int targetEx:{0,1})for(int nodeEx:{0,1})for(int prior:{0,1})for(int flags=0;flags<16;flags++)for(int include:{0,1}){
Reset(type,body,nodeEx,targetEx,prior,flags,hasNode);auto*t=target?&a:nullptr;auto*s=source?&item:nullptr;
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],[",first?"":",",type,body,target,source,hasNode,targetEx,nodeEx,prior,flags,include);first=false;Snapshot(s);
STATLIST_MergeStatLists(t,s,include);printf(",");Snapshot(s);STATLIST_MergeStatLists(t,s,include);printf(",");Snapshot(s);
int changed=include?D2Common_11275(t,s):D2Common_11274(t,s);printf(",");Snapshot(s);D2Common_ExpireStatList_6FDB6E30(&node);printf(",");Snapshot(s);
assert(!node.pParent&&!node.pUnit&&!node.pPrevLink&&!node.pNextLink);
printf("],%d,[",changed);bool comma=false;for(auto x:trace){printf("%s[%d,%d,%d,%d,%d]",comma?",":"",x[0],x[1],x[2],x[3],x[4]);comma=true;}printf("]]");
}printf("]\n");}
'''
    for name,digest in routines.items():assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==digest
    path=STAGE/'source.cpp';path.write_text(cpp);compiler=STAGE/'compiler.log';sanitizer=STAGE/'sanitizer.log'
    with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(STAGE/'probe')],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);sanitizer.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
    data=json.loads(run.stdout);assert len(data)==12288
    for i,states,changed,trace in data:
        # Detachment clears both raw pointers; owner lookup is independently
        # guarded by the supplied source pointer/stat-list presence.
        assert states[-1][2:4]==[0,0] and states[-1][-2:]==[0,0],i
    fixture=BASE/'item-stat-attachment-source-probe.json';encoded=json.dumps(data,separators=(',',':'))+'\n'
    if check:assert fixture.read_text()==encoded
    else:fixture.write_text(encoded)
    inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],component='Item stat attachment,detachment,ownership and damage inclusion policy',currentExecutableParity=False,nativeConsumerAccepted=False,
        inputs=inputs,compiledUnchangedRoutineHashes=routines,verified=dict(contexts=len(data),stagesPerContext=5,completeRoutines=7),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [path,compiler,sanitizer,fixture,*sorted((STAGE/'diagnostics').rglob('*'))] if p.is_file()},
        established=['Seven complete unchanged routines execute both reset-flag post modes,repeat,damage inclusion/exclusion and final detach against real fixture pointer graphs. Extended/nonextended lists,flags,alternate body slots,old parents,missing units/statlists and unusable target roots retain exact source topology and ordered callback/delta requests. STATLIST_SET is an internal list flag,not named set-item quality.',
            'Owner lookup follows an extended parent statlist;it is distinct from the stored pUnit recipient. In particular,merge writes the new recipient even if posting refuses a nonextended target. Repeat posting and dynamic-damage toggles remain separate paths. Detach clears parent,recipient and sibling pointers before removal callback.'],
        remaining=['Fixture stat IDs0/1/2and their contrasting flags/values are abstract declared metadata,not the current player stat-ID catalogue. Delta propagation,operation recalculation and removal callbacks are declared trace services,not the complete stat graph or native HP/resistance/damage consumers. Applying-to-item recalculation is observed without inventing numeric output. Sibling insertion coverage is bounded to one item;cycles/multi-item ordering,full callback reentrancy and actual NPC/save lifecycle remain unfinished. No learned golem or current executable parity is claimed.'])
    out=ROOT/'tools/reference/item-stat-attachment-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Item stat attachment:',len(data),'contexts/5stages/7complete routines;native full stat graph remains unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
