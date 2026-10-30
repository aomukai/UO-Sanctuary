#!/usr/bin/env python3
"""Execute full source defense/stat-list composition before NPC integration."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
CACHE = Path(os.environ.get('SANCTUARY_REFERENCE_CACHE', ROOT / '.local/class-skills'))
STAGE = ROOT / '.local/class-skills/npc-equipment-defense-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    STAGE.mkdir(parents=True, exist_ok=True)
    prior_path = ROOT / 'tools/reference/item-stat-values-source-evidence.json'
    prior = json.loads(prior_path.read_text())
    cached = CACHE / 'item-stat-values-source/source.cpp'
    assert sha(cached) == prior['artifacts']['.local/class-skills/item-stat-values-source/source.cpp']
    tree = json.loads((CACHE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs = {str(prior_path.relative_to(ROOT)): sha(prior_path), str(cached): sha(cached)}
    def source(name):
        path = CACHE / 'd2moo-pinned-source' / name
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == name)
        inputs[str(path)] = sha(path)
        return raw.decode()
    stats = source('source/D2Common/src/D2StatList.cpp')
    units = source('source/D2Common/src/Units/Units.cpp')
    data_path = ROOT / 'tools/reference/d2r-named-equipment/json/itemstatcost.json'
    data = json.loads(data_path.read_text())
    assert sha(data_path) == prior['inputs'][str(data_path.relative_to(ROOT))]
    ids = {name: row['lineNumber'] for name, row in data.items()}
    cpp = cached.read_text().split('int main(){')[0]
    old = 'void*pDrlgAct=nullptr;};'
    assert cpp.count(old) == 1
    cpp = cpp.replace(old, 'void*pDrlgAct=nullptr;void*pInventory=nullptr;};')
    cpp += '\n' + '\n'.join('constexpr int ' + name + '=' + str(ids[key]) + ';' for name, key in [
        ('STAT_DEXTERITY', 'dexterity'), ('STAT_ARMORCLASS', 'armorclass'),
        ('STAT_ITEM_ARMOR_PERCENT', 'item_armor_percent'), ('STAT_SKILL_ARMOR_PERCENT', 'skill_armor_percent'),
        ('STAT_ARMOR_OVERRIDE_PERCENT', 'armor_override_percent'), ('STAT_MODIFIERLIST_SKILL', 'modifierlist_skill'),
        ('STAT_MODIFIERLIST_LEVEL', 'modifierlist_level')])
    cpp += r'''
constexpr int STATE_HOLYSHIELD=1;
struct D2SkillsTxt{int dwCalc[1];};
bool STATES_CheckState(D2UnitStrc*,int){return false;}
D2StatListStrc*STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){assert(false);return nullptr;}
D2SkillsTxt*DATATBLS_GetSkillsTxtRecord(int){assert(false);return nullptr;}
void INVENTORY_GetEquippedShield(void*,D2UnitStrc**){assert(false);}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){assert(false);return 0;}
'''
    routines = {}
    for name in ['STATLIST_GetTotalStatValue', 'STATLIST_UnitGetStatValue', 'STATLIST_UnitGetItemStatOrSkillStatValue', 'STATLIST_GetStatValue']:
        body = function(stats, name); routines[name] = hashlib.sha256(body.encode()).hexdigest(); cpp += body + '\n'
    body = function(units, 'UNITS_GetDefense'); routines['UNITS_GetDefense'] = hashlib.sha256(body.encode()).hexdigest(); cpp += body + '\n'
    cpp += r'''
int main(){Init();printf("[\n");bool first=true;
for(int kind:{0,1})for(int dex:{0,20,100})for(int armor:{0,80,1000})for(int itemArmor:{0,24,100})for(int flat:{-50,0,100})for(int percent:{-50,0,100,200}){
new(&root)D2StatListStrc;new(&item)D2StatListStrc;new(&bonus)D2StatListStrc;
actor={1,kind,0,&root,nullptr};weapon={2,4,0,&item,nullptr};root.pOwner=&actor;root.dwOwnerType=kind;item.pOwner=&weapon;item.dwOwnerType=4;bonus.dwFlags=0x40;
Raw(root.Stats,2,dex);Raw(root.FullStats,2,dex);Raw(root.Stats,31,armor);Raw(root.FullStats,31,armor);
Raw(item.Stats,31,itemArmor);Raw(item.FullStats,31,itemArmor);Raw(bonus.Stats,31,flat);Raw(bonus.Stats,16,percent);
D2COMMON_10475_PostStatToStatList(&weapon,&bonus,TRUE);STATLIST_MergeStatLists(&actor,&weapon,TRUE);
int local=STATLIST_UnitGetStatValue(&weapon,31,0),global=STATLIST_UnitGetStatValue(&actor,16,0),total=UNITS_GetDefense(&actor);
printf("%s[[%d,%d,%d,%d,%d,%d],[%d,%d,%d]]",first?"":",\n",kind,dex,armor,itemArmor,flat,percent,local,global,total);first=false;
}printf("\n]\n");}
'''
    path = STAGE / 'source.cpp'; path.write_text(cpp)
    command = ['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(path),'-o',str(STAGE/'probe')]
    run = subprocess.run(command, capture_output=True, text=True)
    (STAGE/'compiler.log').write_text(run.stdout+run.stderr)
    assert run.returncode == 0, run.stderr[-4000:]
    run = subprocess.run([str(STAGE/'probe')], capture_output=True)
    (STAGE/'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr[-4000:]
    cases = json.loads(run.stdout); assert len(cases) == 648
    (STAGE/'cases.json').write_text(json.dumps(cases, indent=2)+'\n')
    result = dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],scope='Complete older source defense getter and previously compiled stat graph with declared basic item/recipient arrays; Holy Shield is absent, with assertion stubs for its unvisited services. Not modern executable or live NPC acceptance.',inputs=inputs,compiledUnchangedRoutineHashes=routines,cases=len(cases),artifacts={str(p.relative_to(ROOT)):sha(p) for p in [path,STAGE/'compiler.log',STAGE/'sanitizer.log',STAGE/'cases.json']})
    (ROOT/'tools/reference/npc-equipment-defense-source-evidence.json').write_text(json.dumps(result,indent=2)+'\n')
    for context, output in cases:
        if context in [[0,20,80,0,100,100],[1,20,80,0,100,100],[1,20,80,24,100,100]]:
            print(context,output)
    print('Source equipment defense:',len(cases),'composed contexts; no live actor acceptance claimed.')


if __name__ == '__main__':
    build()
