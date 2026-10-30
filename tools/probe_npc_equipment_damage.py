#!/usr/bin/env python3
"""Compose source item stat attachment and complete ordinary physical damage."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
CACHE=Path(os.environ.get('SANCTUARY_REFERENCE_CACHE',ROOT/'.local/class-skills'))
STAGE=ROOT/'.local/class-skills/npc-equipment-damage-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build():
    STAGE.mkdir(parents=True,exist_ok=True)
    prior=json.loads((ROOT/'tools/reference/item-stat-values-source-evidence.json').read_text())
    cached=CACHE/'item-stat-values-source/source.cpp'
    assert sha(cached)==prior['artifacts']['.local/class-skills/item-stat-values-source/source.cpp']
    tree=json.loads((CACHE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs={str(cached):sha(cached)}
    def source(name):
        p=CACHE/'d2moo-pinned-source'/name;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==name)
        inputs[str(p)]=sha(p);return raw.decode()
    stats=source('source/D2Common/src/D2StatList.cpp')
    damage=source('source/D2Game/src/UNIT/SUnitDmg.cpp')
    unique=source('source/D2Game/src/MONSTER/MonsterUnique.cpp')
    data=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/itemstatcost.json').read_text())
    ids={name:row['lineNumber'] for name,row in data.items()}
    cpp=cached.read_text().split('int main(){')[0]
    old='void*pDrlgAct=nullptr;};';assert cpp.count(old)==1
    cpp=cpp.replace(old,'void*pDrlgAct=nullptr;void*pInventory=nullptr;int pSeed=0;};')
    names=['secondary_mindamage','secondary_maxdamage','mindamage','maxdamage','item_normaldamage','damagepercent','strength','dexterity','item_mindamage_percent','item_maxdamage_percent']
    cpp+='\n'+'\n'.join('constexpr int STAT_'+n.upper()+'='+str(ids[n])+';' for n in names)+'\n'
    cpp+=r'''
bool armed=true;int strengthCoefficient=100,dexterityCoefficient=0,edge=0,lastBound=0,wieldType=1;
D2UnitStrc*sub_6FC7C7B0(D2UnitStrc*){return armed?&weapon:nullptr;}
int INVENTORY_GetWieldType(D2UnitStrc*,void*){return wieldType;}
int ITEMS_GetStrengthBonus(D2UnitStrc*){return strengthCoefficient;}
int ITEMS_GetDexBonus(D2UnitStrc*){return dexterityCoefficient;}
int SKILLS_GetWeaponMasteryBonus(D2UnitStrc*u,D2UnitStrc*,int,int){assert(u->dwUnitType==UNIT_MONSTER);return 0;}
int ITEMS_RollLimitedRandomNumber(int*,int bound){assert(bound>0);lastBound=bound;return edge?bound-1:0;}
'''
    routines={}
    for raw,names_ in [(stats,['STATLIST_GetTotalStatValue','STATLIST_UnitGetStatValue','STATLIST_UnitGetItemStatOrSkillStatValue','STATLIST_GetStatValue']),(unique,['MONSTERUNIQUE_CalculatePercentage']),(damage,['SUNITDMG_ApplyDamageBonuses'])]:
        for name in names_:
            body=function(raw,name);routines[name]=hashlib.sha256(body.encode()).hexdigest();cpp+=body+'\n'
    cpp+=r'''
int main(){Init();printf("[\n");bool first=true;
for(int equipment:{0,1,2})for(int base:{0,2,40})for(int itemBase:{0,10,5000})for(int ed:{0,50,200})for(int flat:{0,3})for(int strength:{0,12})for(int global:{-150,0,120})for(int source:{64,128})for(int choice:{0,1}){
new(&root)D2StatListStrc;new(&item)D2StatListStrc;new(&bonus)D2StatListStrc;
actor={1,1,0,&root,nullptr};weapon={2,4,0,&item,nullptr};root.pOwner=&actor;root.dwOwnerType=1;item.pOwner=&weapon;item.dwOwnerType=4;bonus.dwFlags=0x40;
Raw(root.Stats,21,base);Raw(root.FullStats,21,base);Raw(root.Stats,22,base+4);Raw(root.FullStats,22,base+4);
Raw(root.Stats,25,global);Raw(root.FullStats,25,global);
Raw(item.Stats,21,equipment!=0?itemBase:0);Raw(item.FullStats,21,equipment!=0?itemBase:0);
Raw(item.Stats,22,equipment!=0?itemBase+7:0);Raw(item.FullStats,22,equipment!=0?itemBase+7:0);
Raw(bonus.Stats,21,flat);Raw(bonus.Stats,22,flat+1);Raw(bonus.Stats,0,strength);Raw(bonus.Stats,2,10);Raw(bonus.Stats,111,2);
if(equipment!=0){Raw(bonus.Stats,17,ed);Raw(bonus.Stats,18,ed);}else{Raw(bonus.Stats,25,ed);}
if(equipment==2){Raw(item.Stats,23,itemBase);Raw(item.FullStats,23,itemBase);Raw(item.Stats,24,itemBase+7);Raw(item.FullStats,24,itemBase+7);Raw(bonus.Stats,23,flat);Raw(bonus.Stats,24,flat+1);}
D2COMMON_10475_PostStatToStatList(&weapon,&bonus,TRUE);STATLIST_MergeStatLists(&actor,&weapon,TRUE);
armed=equipment!=0;wieldType=equipment==2?2:1;strengthCoefficient=100;dexterityCoefficient=0;edge=choice;lastBound=0;
int result=SUNITDMG_ApplyDamageBonuses(&actor,1,nullptr,0,0,0,0,source);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d]]",first?"":",\n",equipment,base,itemBase,ed,flat,strength,global,source,choice,STATLIST_UnitGetStatValue(&actor,equipment==2?23:21,0),STATLIST_UnitGetStatValue(&actor,equipment==2?24:22,0),STATLIST_UnitGetStatValue(&actor,17,0),STATLIST_UnitGetStatValue(&actor,25,0),STATLIST_UnitGetStatValue(&weapon,21,0),result,lastBound,STATLIST_UnitGetStatValue(&actor,18,0));first=false;
}printf("\n]\n");}
'''
    p=STAGE/'source.cpp';p.write_text(cpp)
    command=['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(p),'-o',str(STAGE/'probe')]
    run=subprocess.run(command,capture_output=True,text=True);(STAGE/'compiler.log').write_text(run.stdout+run.stderr);assert run.returncode==0,run.stderr[-4000:]
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr[-4000:]
    cases=json.loads(run.stdout);assert len(cases)==3888,len(cases)
    (STAGE/'cases.json').write_text(json.dumps(cases,indent=2)+'\n')
    result=dict(referenceVersion='3.3.93847',olderEngineCommit=tree['sha'],cases=len(cases),inputs=inputs,compiledUnchangedRoutineHashes=routines,
        scope='Complete older physical-damage and stat-list routines with declared item/recipient arrays, one-handed/two-handed/unarmed selection, supplied coefficients and controlled first/last random rolls. Not current executable or full actual-item birth/AI acceptance.',
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [p,STAGE/'compiler.log',STAGE/'sanitizer.log',STAGE/'cases.json']})
    (ROOT/'tools/reference/npc-equipment-damage-source-evidence.json').write_text(json.dumps(result,indent=2)+'\n')
    print('Source NPC equipment damage:',len(cases),'composition cases pass ASan/UBSan.')
    print(cases[0]);print(next(c for c in cases if c[0]==[1,2,10,50,3,12,120,128,0]))
if __name__=='__main__':build()
