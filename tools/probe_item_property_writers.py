#!/usr/bin/env python3
"""Execute complete numeric property writers, dispatcher, base reset and ethereality."""
import argparse
import csv
import hashlib
import json
import subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'item-property-writers-source'
SUPPORTED={1,2,3,4,5,6,7,8,15,16,17}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(exist_ok=True);treepath=BASE/'d2moo-source-tree.json';tree=json.loads(treepath.read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs={str(treepath.relative_to(ROOT)):sha(treepath)}
    def source(name):
        p=BASE/'d2moo-pinned-source'/name;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==name)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    mods=source('source/D2Common/src/Items/ItemMods.cpp');header=source('source/D2Common/include/D2StatList.h');assert 'PROPMODE_AFFIX,\n\tPROPMODE_GRADE,\n\tPROPMODE_GEM,\n\tPROPMODE_UNIQUE,' in header
    priorpath=ROOT/'tools/reference/npc-item-suppliers-source-evidence.json';prior=json.loads(priorpath.read_text());inputs[str(priorpath.relative_to(ROOT))]=sha(priorpath)
    propspath=ROOT/'tools/reference/d2r-named-equipment/json/properties.json';ownedpath=BASE/'npc-item-suppliers-source/properties.txt'
    assert sha(propspath)==prior['inputs'][str(propspath.relative_to(ROOT))];assert sha(ownedpath)==prior['inputs'][str(ownedpath.relative_to(ROOT))]
    inputs[str(propspath.relative_to(ROOT))]=sha(propspath);inputs[str(ownedpath.relative_to(ROOT))]=sha(ownedpath)
    props=json.loads(propspath.read_text());owned=list(csv.DictReader(ownedpath.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t'))
    assert len(props)==284 and len(owned)==285 and owned[121]['code']=='Expansion'
    plan=[];compared=0
    for code,row in sorted(props.items(),key=lambda x:x[1]['lineNumber']):
        n=row['lineNumber'];assert owned[n]['code']==code
        steps=[]
        for i in range(1,8):
            for field in ['func','stat','set','val']:
                key=field+str(i);assert str(row.get(key,''))==owned[n].get(key,''),(code,key);compared+=1
            if row.get('func'+str(i)):steps.append([row['func'+str(i)],row.get('stat'+str(i)),row.get('set'+str(i),0),row.get('val'+str(i),0)])
        plan.append(dict(Code=code,Ordinal=n,Steps=steps,Supported=bool(steps) and all(x[0] in SUPPORTED for x in steps)))
    costs=json.loads((ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json').read_text());ids={r['Name']:r['Id'] for r in costs}
    costproofpath=ROOT/'tools/reference/item-stat-values-source-evidence.json';costproof=json.loads(costproofpath.read_text());inputs[str(costproofpath.relative_to(ROOT))]=sha(costproofpath)
    for name,digest in costproof['inputs'].items():
        if name.endswith('/itemstatcost.txt'):assert sha(ROOT/name)==digest;inputs[name]=digest
    costpath=ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json';inputs[str(costpath.relative_to(ROOT))]=sha(costpath)
    costfixture=ROOT/'.local/class-skills/item-stat-values-source-probe.json'
    assert sha(costfixture)==costproof['artifacts'][str(costfixture.relative_to(ROOT))]
    assembled=json.loads(costfixture.read_text())['plans']
    assert [(c['Id'],c['Shift']) for c in costs]==[(r[0],r[4]) for r in assembled]
    inputs[str(costfixture.relative_to(ROOT))]=sha(costfixture)
    for row in plan:
        row['Steps']=[[f,ids.get(s,-1),n,v] for f,s,n,v in row['Steps']]
    localpath=ROOT/'tools/reference/iron-golem-local-items-evidence.json';local=json.loads(localpath.read_text());inputs[str(localpath.relative_to(ROOT))]=sha(localpath)
    bases=[]
    for table,code in [('weapons','bsd'),('weapons','7ga'),('weapons','tkf'),('armor','xng')]:
        r=next(x for x in local['tables'] if x['archivePath'].endswith('/'+table+'.txt'));p=ROOT/r['path'];assert sha(p)==r['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p)
        a=next(x for x in csv.DictReader(p.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t') if x.get('code')==code)
        cachepath=ROOT/'tools/reference/d2r-named-equipment/json'/(table+'.json');assert sha(cachepath)==local['inputs'][str(cachepath.relative_to(ROOT))];inputs[str(cachepath.relative_to(ROOT))]=sha(cachepath)
        cached=next(x for x in json.loads(cachepath.read_text()).values() if x['code']==code);fields=['mindam','maxdam','2handmindam','2handmaxdam','minmisdam','maxmisdam','minac','maxac']
        values=[]
        for f in fields:assert int(cached.get(f) or 0)==int(a.get(f) or 0),(code,f);values.append(int(a.get(f) or 0))
        bases.append(dict(Code=code,Weapon=table=='weapons',Throwable=code=='tkf',Values=values))
    cpp=r'''
#include <algorithm>
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <map>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_ASSERT assert
#define D2_MAYBE_UNUSED(x) (void)(x)
#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))
#define TRUE 1
constexpr int UNIT_ITEM=4,IFLAG_ETHEREAL=1,ITEMTYPE_WEAPON=1,ITEMTYPE_ANY_ARMOR=2;
constexpr int STAT_MINDAMAGE=21,STAT_MAXDAMAGE=22,STAT_SECONDARY_MINDAMAGE=23,STAT_SECONDARY_MAXDAMAGE=24,STAT_ITEM_THROW_MINDAMAGE=159,STAT_ITEM_THROW_MAXDAMAGE=160,STAT_ITEM_MINDAMAGE_PERCENT=18,STAT_ITEM_MAXDAMAGE_PERCENT=17,STAT_ARMORCLASS=31,STAT_ITEM_ARMOR_PERCENT=16,STAT_POISONMAXDAM=58,STAT_POISON_COUNT=326;
struct Seed{int count=0;};struct D2StatListStrc{std::map<uint32_t,int>values;};
struct D2UnitStrc{int dwUnitType=4,dwClassId=0;Seed pSeed;bool weapon=false,throwable=false,ethereal=false;D2StatListStrc base;};
struct D2PropertyStrc{int nProperty=0,nMin=0,nMax=0,nLayer=0;};
struct D2ItemsTxt{int nMinDam=0,nMaxDam=0,n2HandMinDam=0,n2HandMaxDam=0,nMinMisDam=0,nMaxMisDam=0,dwMinAc=0,dwMaxAc=0;}basis;
struct D2ItemStatCostTxt{int nValShift=0;};std::array<D2ItemStatCostTxt,368>costs;
struct D2PropertiesTxt{int nFunc[7]{},nSet[7]{};short wStat[7]{};int wVal[7]{};};std::array<D2PropertiesTxt,285>properties;
using PROPERTYASSIGNFN=int(*)(int,D2UnitStrc*,D2UnitStrc*,const D2PropertyStrc*,int,short,int,int,int,int,D2UnitStrc*);
PROPERTYASSIGNFN off_6FDE3920[40]{};int dword_6FDE39B0=40;
struct Tables{int nPropertiesTxtRecordCount=285;D2PropertiesTxt*pPropertiesTxt=properties.data();}tables;Tables*sgptDataTables=&tables;
D2StatListStrc child;std::vector<std::array<int,4>>trace;bool allocate=true;
uint32_t Key(int id,int layer){return(uint32_t(id)<<16)|uint16_t(layer);}
int Read(D2StatListStrc&list,int stat,int layer=0){auto it=list.values.find(Key(stat,layer));return it==list.values.end()?0:it->second;}
int ITEMS_GetBaseCode(D2UnitStrc*){return 1;}D2ItemsTxt*DATATBLS_GetItemRecordFromItemCode(int,int*id){*id=0;return&basis;}D2ItemsTxt*DATATBLS_GetItemsTxtRecord(int){return&basis;}
bool ITEMS_CheckItemTypeId(D2UnitStrc*u,int t){return t==ITEMTYPE_WEAPON?u->weapon:!u->weapon;}bool ITEMS_CheckIfThrowable(D2UnitStrc*u){return u->throwable;}
void ITEMS_SetItemFlag(D2UnitStrc*u,int,int value){u->ethereal=value;trace.push_back({1,0,value,0});}
int STATLIST_GetUnitBaseStat(D2UnitStrc*u,int id,int layer){return Read(u->base,id,layer);}
void STATLIST_SetUnitStat(D2UnitStrc*u,int id,int value,int layer){u->base.values[Key(id,layer)]=value;trace.push_back({2,id,value,layer});}
D2ItemStatCostTxt*ITEMS_GetItemStatCostTxtRecord(int id){return id>=0&&id<368?&costs[id]:nullptr;}
D2StatListStrc*ITEMMODS_GetOrCreateStatList(D2UnitStrc*,D2UnitStrc*,int state,int flags){trace.push_back({3,state,flags,allocate});return allocate?&child:nullptr;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*l,int id,int value,int layer){l->values[Key(id,layer)]=value;trace.push_back({4,id,value,layer});}
void STATLIST_AddStat(D2StatListStrc*l,int id,int value,int layer){l->values[Key(id,layer)]+=value;trace.push_back({5,id,value,layer});}
int STATLIST_GetStatValue(D2StatListStrc*l,int id,int layer){return Read(*l,id,layer);}
Seed*ITEMS_GetItemSeed(D2UnitStrc*u){return&u->pSeed;}
int SEED_RollLimitedRandomNumber(Seed*s,int bound){assert(bound>0);int value=s->count++%bound;trace.push_back({6,bound,value,0});return value;}
int ITEMMODS_AddPropertyToItemStatList(int,D2UnitStrc*,D2UnitStrc*,const D2PropertyStrc*,int,short,int,int,int,int,D2UnitStrc*);
int ITEMMODS_PropertyFunc06(int,D2UnitStrc*,D2UnitStrc*,const D2PropertyStrc*,int,short,int,int,int,int,D2UnitStrc*);
'''
    routines={}
    names=['sub_6FD92CF0','ITEMMODS_ApplyEthereality','ITEMMODS_AddPropertyToItemStatList']+['ITEMMODS_PropertyFunc'+str(n).zfill(2) for n in sorted(SUPPORTED)]+['D2COMMON_11292_ItemAssignProperty']
    for name in names:
        body=function(mods,name);cpp+=body+'\n';routines[name]=hashlib.sha256(body.encode()).hexdigest()
    cpp+='void Init(){\n'
    for c in costs:cpp+=f'costs[{c["Id"]}].nValShift={c["Shift"]};\n'
    for row in plan:
        for i,(f,s,n,v) in enumerate(row['Steps']):cpp+=f'properties[{row["Ordinal"]}].nFunc[{i}]={f};properties[{row["Ordinal"]}].nSet[{i}]={n};properties[{row["Ordinal"]}].wStat[{i}]={s};properties[{row["Ordinal"]}].wVal[{i}]={v};\n'
    for n in sorted(SUPPORTED):cpp+=f'off_6FDE3920[{n}]=ITEMMODS_PropertyFunc{n:02d};\n'
    cpp+='}\nint eligible[]={'+','.join(str(r['Ordinal']) for r in plan if r['Supported'])+'};\nD2ItemsTxt bases[]={'+','.join('{'+','.join(map(str,r['Values']))+'}' for r in bases)+'};\n'
    cpp+=r'''
void Array(D2StatListStrc&l){printf("[");bool first=true;for(auto [id,value]:l.values){printf("%s[%u,%d]",first?"":",",id,value);first=false;}printf("]");}
int main(){Init();printf("[");bool first=true;
for(int prop:eligible)for(int b=0;b<4;b++)for(int mode:{0,1,3})for(int scalar:{0,1,5,29,200})for(int eth:{0,1})for(int alloc:{0,1}){
D2UnitStrc item;item.weapon=b!=3;item.throwable=b==2;basis=bases[b];allocate=alloc;child.values.clear();trace.clear();
for(auto pair:{std::array{21,basis.nMinDam},std::array{22,basis.nMaxDam},std::array{23,basis.n2HandMinDam},std::array{24,basis.n2HandMaxDam},std::array{159,basis.nMinMisDam},std::array{160,basis.nMaxMisDam},std::array{31,basis.dwMinAc}}){if(pair[1])item.base.values[Key(pair[0],0)]=pair[1];}
D2PropertyStrc p{prop,scalar,scalar,8};D2COMMON_11292_ItemAssignProperty(mode,nullptr,&item,nullptr,0,0,&p,0,0x40,nullptr);if(eth)ITEMMODS_ApplyEthereality(&item);
printf("%s[[%d,%d,%d,%d,%d,%d],",first?"":",",prop,b,mode,scalar,eth,alloc);first=false;Array(item.base);printf(",");Array(child);printf(",%d,[",item.pSeed.count);bool comma=false;for(auto t:trace){printf("%s[%d,%d,%d,%d]",comma?",":"",t[0],t[1],t[2],t[3]);comma=true;}printf("]]");}printf("]\n");}
'''
    for name,digest in routines.items():assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==digest
    path=STAGE/'source.cpp';path.write_text(cpp);compiler=STAGE/'compiler.log';sanitizer=STAGE/'sanitizer.log'
    with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(STAGE/'probe')],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);sanitizer.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
    cases=json.loads(run.stdout);supported=sum(r['Supported'] for r in plan);assert len(cases)==supported*4*3*5*2*2
    fixture=BASE/'item-property-writers-source-probe.json';encoded=json.dumps(dict(plans=plan,bases=bases,cases=cases),separators=(',',':'))+'\n'
    if check:assert fixture.read_text()==encoded
    else:fixture.write_text(encoded)
    inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__).resolve())
    receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],component='Numeric property dispatch/base reset/ethereality source composition',currentExecutableParity=False,nativeConsumerAccepted=False,inputs=inputs,compiledUnchangedRoutineHashes=routines,verified=dict(propertyRows=284,propertyFields=compared,supportedNumericPlans=supported,sourceCases=len(cases),completeRoutines=len(routines),ownedBases=4,ownedBaseFields=32),artifacts={str(p.relative_to(ROOT)):sha(p) for p in [path,compiler,sanitizer,fixture,*sorted((STAGE/'diagnostics').rglob('*'))] if p.is_file()},established=['Complete dispatcher preserves the first function result for all later property slots. Eleven complete numeric writers compose with full base-reset,stat-list write and ethereality routines. Metadata includes all seven func/stat/set/val slots for284owned properties,with the Expansion marker retained as an unused ordinal.','The owned extra-skill-dmg%-rand row names dmg%,which has no itemstatcost row;its func7ignores that stat except in the verified fallback. This declared unresolved-name link is represented as-1 and remains an explicit metadata caveat.','Cases execute ordinary/grade/unique modes,zero/small/high fixed rolls,allocation failure and optional post-property ethereality across owned one-hand,two-hand,throwing and armor bases. Base/stat-list writes and ordered service traces remain explicit;RNG/allocation are declared fixture services.'],remaining=['Native writer/projection integration,original saved rolls/ranges and creation order,base initialization and current executable corroboration remain unfinished. Skill/event/special property functions are explicitly outside the numeric function set. These fixed-roll cases do not certify arbitrary roll ranges or every modifier combination. Armor type and throwability are declared fixture classifications,not a complete item-type resolver.'])
    out=ROOT/'tools/reference/item-property-writers-source-evidence.json';encoded=json.dumps(receipt,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Item property writers:',json.dumps(receipt['verified']))
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
