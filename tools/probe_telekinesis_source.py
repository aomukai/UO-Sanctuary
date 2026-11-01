#!/usr/bin/env python3
"""Execute the complete pinned older Telekinesis start12 and do21 branches."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/telekinesis-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def probe(check=False):
    tree = json.loads((ROOT / '.local/class-skills/d2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    rel = 'source/D2Game/src/SKILLS/SkillSor.cpp'
    source = ROOT / '.local/class-skills/d2moo-pinned-source' / rel
    raw = source.read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == rel)
    names = ('SKILLS_SrvSt12_Telekinesis_DragonFlight', 'SKILLS_SrvDo021_Telekinesis')
    blocks = {name: function(raw.decode(), name) for name in names}
    skills = ROOT / 'tools/reference/d2r-named-equipment/json/skills.json'
    manifest = json.loads((skills.parent.parent / 'sources.json').read_text())
    assert sha(skills) == manifest['files']['json/skills.json']['sha256']
    current = next(r for r in json.loads(skills.read_text()).values() if r.get('skill') == 'Telekinesis')
    expected = {'*Id':43, 'srvstfunc':12, 'srvdofunc':21, 'ResultFlags':5,
                'HitFlags':2, 'HitClass':109, 'Param1':25, 'Param2':35,
                'aurarangecalc':'par1', 'mana':7, 'manashift':8, 'InTown':1,
                'TargetItem':1, 'EType':'ltng', 'EMin':1, 'EMax':2, 'HitShift':8}
    assert {k:current.get(k) for k in expected} == expected
    cpp = r'''
#include <cstdint>
#include <cstdio>
#include <string>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_OBJECT=2,UNIT_ITEM=4;
constexpr int UNITFLAG_SKSRVDOFUNC=256,UNITFLAG_SENDREFRESHMSG=512,IMODE_ONGROUND=3;
constexpr int ITEMTYPE_SCROLL=0,ITEMTYPE_GOLD=1,ITEMTYPE_MISSILE_POTION=2,ITEMTYPE_MISSILE=3,ITEMTYPE_POTION=4,ITEMTYPE_KEY=5;
struct D2GameStrc{} game;
struct D2ActiveRoomStrc{bool town=false;} ownerRoom,targetRoom;
struct D2SeedStrc{} seed;
struct D2UnitStrc{int dwUnitType=0,dwFlags=0,dwUnitId=91,x=0,y=0;D2SeedStrc pSeed;D2ActiveRoomStrc* room=&ownerRoom;} owner,target;
struct D2SkillsTxt{int dwAuraRangeCalc=0,wResultFlags=5,dwHitFlags=2,dwHitClass=109,dwParam[8]={25,35};} row;
struct D2DamageStrc{int wResultFlags=0,dwHitFlags=0,dwHitClass=0;} captured;
bool hasRow=true,hasTarget=true,hostile=true,busy=false;int kind=0,draw=0,range=25;
int physical=0,elemental=0,randoms=0,events=0,damage=0,pickup=0,refresh=0,page=0,animation=0,sound=0,operate=0;
std::string order;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return hasTarget?&target:nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return range;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
int PATH_ComputeSquaredDistance(int x,int y,int a,int b){return (x-a)*(x-a)+(y-b)*(y-b);}
bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return hostile;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc* p){return p->room;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc* p){return p->town;}
bool PLAYER_IsBusy(D2UnitStrc*){return busy;}
void D2GAME_RollPhysicalDamage_6FD14EC0(D2UnitStrc*,D2DamageStrc* p,int,int){physical++;p->wResultFlags=16;p->dwHitFlags=64;order+='p';}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*,int,int){elemental++;order+='e';}
uint32_t ITEMS_RollRandomNumber(D2SeedStrc*){randoms++;order+='r';return draw;}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int type,D2DamageStrc* p){events++;captured=*p;order+=type==1?'v':'?';}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p){damage++;captured=*p;order+='d';}
bool ITEMS_CheckItemTypeId(D2UnitStrc*,int type){return type==kind;}
void D2GAME_PickupItem_6FC43340(D2GameStrc*,D2UnitStrc*,int id,int*){pickup+=id==91;order+='i';}
void DUNGEON_AllocDrlgDelete(D2ActiveRoomStrc*,int,int){refresh++;order+='f';}
void ITEMS_SetInvPage(D2UnitStrc*,unsigned n){page=n;order+='g';}
void UNITS_ChangeAnimMode(D2UnitStrc*,int n){animation=n;order+='a';}
void SUNIT_AttachSound(D2UnitStrc*,int n,D2UnitStrc*){sound=n;order+='s';}
void OBJECTS_OperateHandler(D2GameStrc*,D2UnitStrc*,int type,int id,int*){operate+=type==2&&id==91;order+='o';}
'''
    cpp += '\n'.join(blocks.values())
    cpp += r'''
int main(){owner.room=&ownerRoom;target.room=&targetRoom;bool first=true;printf("{\"start\":[");
for(int rowFound:{0,1})for(int selected:{0,1})for(int type:{0,1,2,3,4,5})for(int h:{0,1})for(int town:{0,1})for(int other:{0,1})for(int x:{0,15,24,25,26})for(int y:{0,7,20}){
hasRow=rowFound;hasTarget=selected;target.dwUnitType=type;hostile=h;ownerRoom.town=town;targetRoom.town=other;target.x=x;target.y=y;
int ret=SKILLS_SrvSt12_Telekinesis_DragonFlight(&game,&owner,43,3);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rowFound,selected,type,h,town,other,x,y,ret);first=false;}
printf("],\"do\":[");first=true;
for(int guard=0;guard<6;guard++)for(int type:{0,1,2,3,4,5})for(int h:{0,1})for(int town:{0,1})for(int other:{0,1})for(int itemKind=0;itemKind<7;itemKind++)for(int roll:{0,34,35,99,100,134,135})for(int hitclass:{0,109}){
hasRow=guard!=1;hasTarget=guard!=2;busy=guard==3;owner.dwUnitType=guard==4?1:0;target.dwUnitType=type;hostile=h;ownerRoom.town=town;targetRoom.town=other;kind=itemKind;draw=roll;row.dwHitClass=hitclass;
owner.dwFlags=8;target.dwFlags=16;captured={};physical=elemental=randoms=events=damage=pickup=refresh=page=animation=sound=operate=0;order.clear();
int ret=SKILLS_SrvDo021_Telekinesis(&game,guard==5?nullptr:&owner,43,3);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,\"%s\"]",first?"":",",guard,type,h,town,other,itemKind,roll,hitclass,ret,owner.dwFlags,target.dwFlags,physical,elemental,randoms,events,damage,pickup,refresh,page,animation,sound,operate,captured.wResultFlags,captured.dwHitFlags,captured.dwHitClass,order.c_str());first=false;}
printf("]}\n");}
'''
    STAGE.mkdir(exist_ok=True)
    p = STAGE / 'probe.cpp'
    p.write_text(cpp)
    binary = STAGE / 'probe'
    subprocess.run(['g++', '-std=c++20', '-O2', str(p), '-o', str(binary)], check=True)
    data = json.loads(subprocess.check_output([str(binary)]))
    assert len(data['start']) == 2880 and len(data['do']) == 28224
    for r in data['start']:
        row_found, selected, typ, hostile, town, other, x, y, ret = r
        assert ret == int(row_found and selected and x*x+y*y <= 625 and (typ not in (0, 1) or (hostile and not town and not other)))
    for r in data['do']:
        guard, typ, hostile, town, other, kind, roll, hitclass = r[:8]
        valid = guard == 0
        combat = valid and typ in (0, 1) and hostile and not town and not other
        pickup = valid and typ == 4 and kind < 6
        ground = valid and typ == 4 and kind == 6
        obj = valid and typ == 2
        ret = int(valid and (typ not in (0, 1) or combat))
        expected = [ret, 264 if valid else 8, 528 if ground else 16,
                    *([int(combat)]*5), int(pickup), int(ground), 255 if ground else 0,
                    3 if ground else 0, 19 if ground else 0, int(obj),
                    (29 if roll % 100 < 35 else 21) if combat else 0,
                    66 if combat else 0, hitclass if combat else 0,
                    'pervd' if combat else 'i' if pickup else 'fgas' if ground else 'o' if obj else '']
        assert r[8:] == expected, (r, expected)
    san = STAGE / 'probe-san'
    subprocess.run(['g++', '-std=c++20', '-g', '-fsanitize=address,undefined', '-fno-omit-frame-pointer', str(p), '-o', str(san)], check=True)
    log = STAGE / 'sanitizer.log'
    with log.open('wb') as out:
        assert json.loads(subprocess.check_output([str(san)], stderr=out)) == data
    assert not log.read_bytes()
    fixture = STAGE / 'cases.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceHashes={rel: sha(source)},
                    routineHashes={n: hashlib.sha256(b.encode()).hexdigest() for n, b in blocks.items()},
                    currentTable={str(skills.relative_to(ROOT)):sha(skills)}, currentRow=expected,
                    startCases=len(data['start']), releaseCases=len(data['do']), sanitizers='ASan/UBSan clean',
                    established=[
                        'Complete start12 uses inclusive squared range, then hostility and both town gates only for player/monster targets.',
                        'Complete do21 refuses missing row/owner/target, non-player or busy owner; marks the do-function flag before combat hostility/town refusal.',
                        'Combat rolls physical then elemental damage, ORs table flags, optionally replaces hit class, consumes one owner roll modulo100; rolls0..34 add flags9, then events1 precede missile damage.',
                        'All six whitelisted item categories use pickup. Other items use ground refresh/page255/ground mode/sound19. Objects call the operation service; other unit kinds succeed without an operation.'
                    ], declaredServices=[
                        'Range25, flags5/2, hit class109 and chance35 are supplied current table inputs; physical damage deliberately seeds flags16/64 to verify OR rather than assignment.',
                        'Target/room/hostility/busy/item classification/RNG/formula/damage/events/pickup/object services are explicit seams. No execution of these dependent engine services or modern executable is claimed.',
                        'Result flag4 requests the shared recipient return policy; it does not by itself prove unconditional hit recovery or a stun. Native operation permissions, object mappings and routing require separate tests.'
                    ], artifacts={str(q.relative_to(ROOT)): sha(q) for q in (Path(__file__).resolve(), p, binary, san, log, fixture)})
    out = ROOT / 'tools/reference/telekinesis-source-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded
    else:
        out.write_text(encoded)
    print(f"Telekinesis: {len(data['start'])} complete start12 / {len(data['do'])} complete do21 cases; ASan/UBSan clean; dependent services/current executable remain separate.")


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--check', action='store_true')
    probe(ap.parse_args().check)
