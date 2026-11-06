#!/usr/bin/env python3
"""Execute complete property23, ethereality and durability guards unchanged."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
from probe_npc_damage_fill import enum
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'saved-item-ethereality-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());inputs={};bodies={}
    def read(rel):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    mods=read('source/D2Common/src/Items/ItemMods.cpp');items=read('source/D2Common/src/Items/Items.cpp');stats=read('source/D2Common/src/D2StatList.cpp')
    header=read('source/D2Common/include/D2Items.h');statids=enum(read('source/D2Common/include/D2StatList.h'),'D2C_ItemStats')
    kinds=enum(read('source/D2Common/include/Units/Units.h'),'D2C_UnitTypes');types=enum(header,'D2C_ItemTypes');flags=enum(header,'D2C_ItemFlags')
    constants={k:statids[k]for k in ['STAT_MINDAMAGE','STAT_MAXDAMAGE','STAT_SECONDARY_MINDAMAGE','STAT_SECONDARY_MAXDAMAGE','STAT_ITEM_THROW_MINDAMAGE','STAT_ITEM_THROW_MAXDAMAGE','STAT_ARMORCLASS','STAT_MAXDURABILITY','STAT_ITEM_INDESCTRUCTIBLE','STAT_ITEM_MAXDURABILITY_PERCENT']}
    assert list(constants.values())==[21,22,23,24,159,160,31,73,152,75]
    constants.update(UNIT_ITEM=kinds['UNIT_ITEM'],ITEMTYPE_WEAPON=types['ITEMTYPE_WEAPON'],IFLAG_ETHEREAL=flags['IFLAG_ETHEREAL']);assert constants['UNIT_ITEM']==4 and constants['IFLAG_ETHEREAL']==0x400000
    costs=ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json';owned=json.loads(costs.read_text());inputs[str(costs.relative_to(ROOT))]=sha(costs)
    for name,stat in constants.items():
        if name.startswith('STAT_'):assert next(r for r in owned if r['Id']==stat)['Name']==name.removeprefix('STAT_').lower()

    for raw,names in [(mods,['ITEMMODS_ApplyEthereality','ITEMMODS_PropertyFunc23']),(items,['ITEMS_HasDurability','ITEMS_IsEthereal']),(stats,['STATLIST_GetMaxDurabilityFromUnit'])]:
        for name in names:bodies[name]=function(raw,name)
    bodies['ITEMS_GetItemData']=next(line for line in header.splitlines()if line.startswith('inline D2ItemDataStrc* ITEMS_GetItemData('))
    cpp='''#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __stdcall
#define __fastcall
#define D2_ASSERT(x) assert(x)
using BOOL=int;constexpr int TRUE=1,FALSE=0;
'''+''.join(f'constexpr int {k}={v};\n'for k,v in constants.items())+'''
struct D2ItemDataStrc{uint32_t dwItemFlags=0;};struct D2UnitStrc{int dwUnitType=4,dwClassId=0;D2ItemDataStrc*pItemData=nullptr;int stats[368]{};bool weapon=false;};
struct D2ItemsTxt{int nNoDurability=0,nDurability=0;};struct D2PropertyStrc{};
D2ItemsTxt row;bool rowExists=false;int maximum=0,indestructible=0,maxReads=0,indReads=0,flagWrites=0,baseWrites=0;
D2ItemsTxt* DATATBLS_GetItemsTxtRecord(int){return rowExists?&row:nullptr;}
int D2Common_11248(D2UnitStrc*,D2UnitStrc*,int stat){assert(stat==73);maxReads++;return maximum;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int layer){assert(stat==152&&layer==0);indReads++;return indestructible;}
int STATLIST_GetUnitBaseStat(D2UnitStrc*p,int stat,int layer){assert(layer==0);return p->stats[stat];}
void STATLIST_SetUnitStat(D2UnitStrc*p,int stat,int value,int layer){assert(layer==0&&(p->pItemData->dwItemFlags&IFLAG_ETHEREAL));p->stats[stat]=value;baseWrites++;}
int ITEMS_CheckItemTypeId(D2UnitStrc*p,int type){assert(type==ITEMTYPE_WEAPON);return p->weapon;}
void ITEMS_SetItemFlag(D2UnitStrc*p,int flag,int enabled){assert(flag==IFLAG_ETHEREAL&&enabled==1);p->pItemData->dwItemFlags|=flag;flagWrites++;}
'''+bodies['ITEMS_GetItemData']+'\n'+bodies['STATLIST_GetMaxDurabilityFromUnit']+'\n'+bodies['ITEMS_IsEthereal']+'\n'+bodies['ITEMS_HasDurability']+'\n'+bodies['ITEMMODS_ApplyEthereality']+'\n'+bodies['ITEMMODS_PropertyFunc23']+r'''
void State(int result,D2UnitStrc&item){printf("[%d,%u,%d,%d,%d,%d",result,item.pItemData->dwItemFlags,maxReads,indReads,flagWrites,baseWrites);for(int stat:{21,22,23,24,159,160,31})printf(",%d",item.stats[stat]);printf("]");}
int main(){printf("[");bool first=true;D2PropertyStrc property;
for(int present:{0,1})for(int kind:{0,1,4})for(int prop:{0,1})for(int eth:{0,1})for(int record:{0,1})for(int no:{0,1})for(int durable:{0,1,40})for(int max:{-1,0,1,255})for(int ind:{-1,0,1})for(int weapon:{0,1})for(int odd:{0,1}){
D2ItemDataStrc data;data.dwItemFlags=eth?IFLAG_ETHEREAL:0;D2UnitStrc item;item.pItemData=&data;item.dwUnitType=kind;item.weapon=weapon;
int i=0;for(int stat:{21,22,23,24,159,160,31})item.stats[stat]=(i++)*10+odd;
rowExists=record;row={no,durable};maximum=max;indestructible=ind;maxReads=indReads=flagWrites=baseWrites=0;
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],",first?"":",",present,kind,prop,eth,record,no,durable,max,ind,weapon,odd);first=false;
int a=ITEMMODS_PropertyFunc23(3,nullptr,present?&item:nullptr,prop?&property:nullptr,0,-1,0,0,0,0x40,nullptr);State(a,item);printf(",");
int b=ITEMMODS_PropertyFunc23(3,nullptr,present?&item:nullptr,prop?&property:nullptr,0,-1,0,0,0,0x40,nullptr);State(b,item);printf("]");}printf("]\n");}
'''
    for name,body in bodies.items():assert body in cpp,name
    src=STAGE/'source.cpp';src.write_text(cpp);binary=STAGE/'probe'
    with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(src),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr
    cases=json.loads(run.stdout);assert len(cases)==13824
    pool=ROOT/'data/Sanctuary/Balance/npc-item-affix-catalogue.json';inputs[str(pool.relative_to(ROOT))]=sha(pool);data=json.loads(pool.read_text())
    bases=[[r['code'],r['sourceFields'].get('durability',0),r['sourceFields'].get('nodurability',0)]for r in data['bases']];assert len(bases)==692
    out=STAGE/'cases.json';out.write_text(json.dumps(dict(cases=cases,bases=bases),separators=(',',':'))+'\n');inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__))
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,contexts=13824,ownedBases=692,constants=constants,completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in [src,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',out]},
        services=['Complete property23,ethereality,HasDurability,IsEthereal,GetMaxDurability and inline item-data getter. Item row/type predicate,flag setter,base-stat reads/writes and operative max-durability stat query are declared services. Source flag precedes all physical writes;second application tests idempotent guard.'],
        limits=['No current-executable/global item-factory or arbitrary native ethereal-flag reconstruction certificate. Original saved NPC projection and the shared op13 durability-query binding require separate native acceptance.'])
    target=ROOT/'tools/reference/saved-item-ethereality-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Saved item ethereality:13824 complete callback/guard/physical contexts;692owned bases;6unchanged bodies;ASan/UBSan pass.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
