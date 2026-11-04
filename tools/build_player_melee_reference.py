#!/usr/bin/env python3
"""Pin all weapon reach adders and exercise the original player inventory branch."""
import argparse,ast,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'player-melee-geometry-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(exist_ok=True)
    previous=json.loads((ROOT/'tools/reference/melee-geometry-source-evidence.json').read_text())
    scaffold=ROOT/'tools/probe_melee_geometry.py'
    assert sha(scaffold)==previous['inputs'][str(scaffold.relative_to(ROOT))]
    parsed=ast.parse(scaffold.read_text())
    prefix=next(n.value.value for n in ast.walk(parsed)if isinstance(n,ast.Assign)and any(isinstance(t,ast.Name)and t.id=='cpp'for t in n.targets)and isinstance(n.value,ast.Constant))
    suffix=next(n.value.value for n in ast.walk(parsed)if isinstance(n,ast.AugAssign)and isinstance(n.target,ast.Name)and n.target.id=='cpp'and isinstance(n.value,ast.Constant))
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());inputs={}
    def source(rel):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    item=source('source/D2Common/src/Items/Items.cpp')
    unit=source('source/D2Common/src/Units/Units.cpp')
    collision=source('source/D2Common/include/D2Collision.h')
    assert 'return COLLISION_UNIT_SIZE_SMALL;'in function(unit,'UNITS_GetUnitSizeX')and 'COLLISION_UNIT_SIZE_SMALL = 2'in collision
    pin=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((pin/'sources.json').read_text());weapon=pin/'json/weapons.json'
    assert sha(weapon)==manifest['files']['json/weapons.json']['sha256']and manifest['mechanicsReferenceVersion']=='3.3.93847'
    weapons=json.loads(weapon.read_text());rows=[dict(Code=k,Range=v.get('rangeadder',0))for k,v in sorted(weapons.items())]
    assert len(rows)==306 and all(0<=r['Range']<=255 for r in rows)
    bodies={n:function(unit,n)for n in previous['compiledUnchangedRoutineHashes']}
    cpp=prefix+'\n'.join(bodies.values())+suffix.split('int main(){',1)[0]
    cpp=cpp.replace('constexpr int UNIT_PLAYER=0,','constexpr int UNIT_ITEM=4,UNIT_PLAYER=0,',1)
    stub='bool INVENTORY_GetEquippedWeapon(void*,D2UnitStrc**,int*,BOOL*){assert(false);return false;}int ITEMS_GetMeleeRange(D2UnitStrc*){assert(false);return 0;}'
    replacement='''D2UnitStrc equipped;bool hasWeapon=false;struct D2ItemsTxt{uint8_t nRangeAdder=0;};D2ItemsTxt itemRow;
bool INVENTORY_GetEquippedWeapon(void*,D2UnitStrc**out,int*,BOOL*){if(hasWeapon)*out=&equipped;return hasWeapon;}
D2ItemsTxt* DATATBLS_GetItemsTxtRecord(int){return &itemRow;}
'''+function(item,'ITEMS_GetMeleeRange')
    assert stub in cpp;cpp=cpp.replace(stub,replacement,1).replace('#include <array>','#include <array>\n#include <cstdint>',1)
    bodies['ITEMS_GetMeleeRange']=function(item,'ITEMS_GetMeleeRange')
    for n,b in bodies.items():assert function(cpp,n)==b
    ranges=','.join(str(r['Range'])for r in rows)
    cpp+='''int main(){printf("{\\"items\\":[");bool first=true;int values[]={'''+ranges+'''};
for(int i=0;i<306;i++)for(int present:{0,1})for(int kind:{UNIT_ITEM,UNIT_MONSTER}){
actor={};actor.dwUnitType=UNIT_PLAYER;equipped={};equipped.dwUnitType=kind;hasWeapon=present;itemRow.nRangeAdder=values[i];
int value=UNITS_GetMeleeRange(&actor);printf("%s[%d,%d,%d,%d]",first?"":",",i,present,kind,value);first=false;}
printf("],\\"range\\":[");first=true;
for(int raw:{0,1,2,3,4,254,255})for(int x:{0,1,2,3,4,7,8,9,12})for(int y:{0,1,2,3,7,9})for(int size:{0,1,2,3,4})for(int base:{0,258,261})for(int bonus:{0,1,3})for(int roomFlag:{0,1})for(int wall:{0,1}){
Reset(x,y,2,size,roomFlag,wall);actor.dwUnitType=UNIT_PLAYER;hasWeapon=true;equipped={};equipped.dwUnitType=UNIT_ITEM;itemRow.nRangeAdder=raw;target.dwClassId=base;
int answer=UNITS_IsInMeleeRange(&actor,&target,bonus);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d],%d,",first?"":",",raw,x,y,size,base,bonus,roomFlag,wall,answer);first=false;Rays();printf("]");}
printf("]}\\n");}
'''
    p=STAGE/'source.cpp';p.write_text(cpp)
    with(STAGE/'compiler.log').open('w')as output:subprocess.run(['g++','-std=c++20','-O1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(p),'-o',str(STAGE/'probe')],stdout=output,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr
    cases=json.loads(run.stdout);assert len(cases['items'])==1224 and len(cases['range'])==68040
    for index,present,kind,result in cases['items']:assert result==(rows[index]['Range']if present and kind==4 else 0)
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    runtime=ROOT/'data/Sanctuary/Balance/player-melee-ranges.json';encoded=json.dumps(rows,indent=2)+'\n'
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,inputs={**inputs,str(weapon.relative_to(ROOT)):sha(weapon),str(scaffold.relative_to(ROOT)):sha(scaffold)},
        compiledUnchangedRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},weaponRows=len(rows),itemContexts=1224,rangeContexts=68040,playerSize=2,
        runtimePath=str(runtime.relative_to(ROOT)),runtimeSha256=hashlib.sha256(encoded.encode()).hexdigest(),
        artifacts={str(q.relative_to(ROOT)):sha(q)for q in (p,STAGE/'compiler.log',STAGE/'sanitizer.log',fixture)},
        scope='Complete old distance/reach/collision/range bodies and item getter. Player inventory selection,table lookup,size2 and the native ray/room bridge are declared services. All306 ranges use the pinned3.3.93847 cache; no owned weapons.txt corroboration is claimed. Player reach255 is literal,not the NPC composite-class sentinel. Existing NPC contexts remain unchanged.')
    out=ROOT/'tools/reference/player-melee-geometry-source-evidence.json';receipt=json.dumps(evidence,indent=2)+'\n'
    if check:assert runtime.read_text()==encoded and out.read_text()==receipt,'Player melee geometry evidence stale'
    else:runtime.write_text(encoded);out.write_text(receipt)
    print('Player melee geometry:306 pinned weapons,1224 original item-getter and68040 complete source reach/collision controls; ASan/UBSan clean.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
