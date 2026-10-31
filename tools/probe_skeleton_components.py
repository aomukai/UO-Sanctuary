#!/usr/bin/env python3
"""Execute old component initialization/override/blocking without inventing a distribution."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'skeleton-component-source'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs={'tools/review_weapon_event_order.py':sha(ROOT/'tools/review_weapon_event_order.py')};bodies={}
    def source(relative):
        p=BASE/'d2moo-pinned-source'/relative;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    monster=source('source/D2Game/src/MONSTER/Monster.cpp')
    nec=source('source/D2Game/src/SKILLS/SkillNec.cpp')
    units=source('source/D2Common/src/Units/Units.cpp')
    for text,name in [(monster,'MONSTER_SetComponents'),(nec,'D2GAME_SetUnitComponent_6FD0C3A0'),(units,'UNITS_GetBlockRate')]:bodies[name]=function(text,name)
    caller=function(nec,'SKILLS_SrvDo031_RaiseSkeleton_Mage')
    declaration=nec[nec.index('struct D2NecroSkeletonComponentStrc'):];declaration=declaration[:declaration.index('};')+2]
    ref=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((ref/'sources.json').read_text());tables={}
    for name in ['skills','monstats','monstats2','armor']:
        p=ref/f'json/{name}.json';assert sha(p)==manifest['files'][f'json/{name}.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())
    mon=tables['monstats']['necroskeleton'];codes=tables['monstats2']['necroskeleton']['SHv'].strip('"').split(',')
    assert codes==['nil','buc','lrg','kit','sml'] and tables['skills']['70']['Param1']==5
    assert [mon.get(n,0) for n in ['ToBlock','ToBlock(N)','ToBlock(H)']]==[3,3,3] and not mon.get('noShldBlock')
    assert all(tables['armor'][code]['type']=='shie' for code in codes[1:])
    cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstring>
#define __fastcall
#define __stdcall
#define ARRAY_SIZE(x) (sizeof(x)/sizeof(x[0]))
using BOOL=int;
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,MONSTATSFLAGINDEX_NOSHLDBLOCK=0,MONSTER_DIABLO=243,MONSTER_DOOMKNIGHT1=310,MONSTER_DIABLOCLONE=333,MONSTER_ACT3HIRE=359,ITEMTYPE_SHIELD=2,STAT_TOBLOCK=20,STAT_LEVEL=12,STAT_DEXTERITY=2;
int gdwBitMasks[32]={1,2};
struct D2SeedStrc{};
struct D2MonsterDataStrc{uint8_t nComponent[16];void* pAiControl=nullptr;};
struct D2UnitStrc{int dwUnitType=UNIT_MONSTER,dwClassId=363;D2MonsterDataStrc* pMonsterData=nullptr;D2SeedStrc pSeed;void* pInventory=nullptr;int block=3;};
struct D2MonRegDataStrc{int nComponentVariantsMax=5;uint8_t nComponentVariants[5][16];};
struct D2CharStatsTxt{int nBlockFactor=0;};struct D2MonStatsTxt{int dwMonStatsFlags=0;}mon;
struct D2ItemsTxt{int wType[1]={ITEMTYPE_SHIELD};}shield;
struct Tables{int nCharStatsTxtRecordCount=7,nMonStatsTxtRecordCount=1000;D2CharStatsTxt pCharStatsTxt[7];}tables;auto* sgptDataTables=&tables;
int selected=0,limitedCalls=0;
int ITEMS_RollLimitedRandomNumber(D2SeedStrc*,int bound){int call=limitedCalls++;assert(bound==1||bound==5);return bound==5?selected:0;}
int D2COMMON_11068_GetCompInfo(D2UnitStrc*,int component){return component==7?5:1;}
uint32_t ITEMS_RollRandomNumber(D2SeedStrc*){assert(false);return 0;}
void AITHINK_SetAiControlParams(void*,int,int,int){assert(false);}
void* INVENTORY_GetEquippedShield(void*,void*){assert(false);return nullptr;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){assert(stat==STAT_TOBLOCK);return p->block;}
D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int id){assert(id==363);return &mon;}
int componentCodes[5];
int D2Common_11069(D2UnitStrc*,int component,int choice){assert(component==7&&choice>=0&&choice<5);return componentCodes[choice];}
D2ItemsTxt* DATATBLS_GetItemRecordFromItemCode(int code,int* id){assert(code!=0);*id=1;for(int i=1;i<5;i++)if(componentCodes[i]==code)return &shield;assert(false);return nullptr;}
'''+declaration+'\n'+'\n'.join(bodies.values())+'\nint main(){\n'
    for i,code in enumerate(codes):cpp+=f'componentCodes[{i}]={0 if code=="nil" else int.from_bytes((code+" ").encode(),"little")};\n'
    cpp+=r'''bool first=true;printf("[");
for(int region:{0,1})for(int choice=0;choice<5;choice++)for(int level:{0,1,2,3,4,5,6,7,8,9,10,20})for(int overrideShield:{0,1})for(int basis:{3,100}){
 D2MonsterDataStrc data;memset(data.nComponent,9,16);D2UnitStrc pet,owner;pet.pMonsterData=&data;pet.block=basis;owner.dwUnitType=UNIT_PLAYER;
 D2MonRegDataStrc appearance;for(int i=0;i<5;i++)memset(appearance.nComponentVariants[i],i,16);
 selected=choice;limitedCalls=0;MONSTER_SetComponents(region?&appearance:nullptr,&pet);
 int initial=data.nComponent[7],calls=limitedCalls;uint8_t untouched[4];memcpy(untouched,data.nComponent+12,4);
 D2GAME_SetUnitComponent_6FD0C3A0(&owner,&pet,level,overrideShield,0);
 assert(memcmp(untouched,data.nComponent+12,4)==0&&limitedCalls==calls);
 int block=UNITS_GetBlockRate(&pet,1);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",region,choice,level,overrideShield,basis,initial,data.nComponent[7],block,calls);first=false;
}printf("]\n");}
'''
    p=STAGE/'probe.cpp';p.write_text(cpp);compiler=STAGE/'compiler.log';sanitizer=STAGE/'sanitizer.log'
    with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-Wno-multichar','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(STAGE/'probe')],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);sanitizer.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
    rows=json.loads(run.stdout);assert len(rows)==480
    overrides=[0,0,1,1,1,1,1,2,2,3,3]
    for region,choice,level,override,basis,initial,final,block,calls in rows:
        assert initial==choice and final==(overrides[min(level,10)] if override else choice)
        assert block==(min(basis,75) if final else 0) and calls==(1 if region else 12)
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    paths=[Path(__file__).resolve(),p,compiler,sanitizer,fixture]
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=480,inputs=inputs,
        executed={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},reviewedCallerSha256=hashlib.sha256(caller.encode()).hexdigest(),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},currentShieldCodes=codes,currentBlock=[3,3,3],
        established=['Three complete unchanged bodies compose initial components,the skill component override and final block classification in480contexts. Both independent and supplied-region branches,all5shield choices,12cosmetic levels,override true/false and3/100block inputs execute with clean ASan/UBSan.',
            'False override retains the initialized SH component. True override selects shield0at cosmetic0/1,shield1at2..6,shield2at7/8 andshield3at9/10;levels above10clamp. Nil gives0block;the4pinned shield item codes give3current base block;synthetic100caps75.',
            'Initializer makes12limited-random service requests without a regional pool,or1request and copies all16components with a supplied pool. The regular override consumes no additional random request and preserves components12..15. These are service calls,not certified seed advances.'],
        services=['Component bounds outside SH are supplied1;SH bound5and shield item types are pinned. Regional variants are five synthetic vectors,not evidence of actual regional appearance provenance or distribution.',
            'RNG returns,metadata/item lookup,monster row/stat storage and unused player/special branches are declared services. The complete Raise Skeleton caller is statically reviewed only;owner chance/full-set state/spawn initialization/global RNG ordering are not composed here.'],
        remaining=['The user observation of all visible shields at effective12does not establish a distribution or block probability. This older composition does not justify replacing the learned initializer with uniform choices,5percent final shields or guaranteed shields.',
            'Current initializer/region/full-set and owner/pet seed order,actual incoming saved-component blocking,AI67integration and client/effects remain unfinished. No production/data/save changes or class completion are claimed.'])
    receipt=ROOT/'tools/reference/skeleton-component-source-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert receipt.read_text()==rendered
    else:receipt.write_text(rendered)
    print('Skeleton components:480complete initializer/override/block contexts;modern final distribution remains unverified.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
