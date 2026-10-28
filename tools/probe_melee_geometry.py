#!/usr/bin/env python3
"""Execute complete melee distance/reach/collision-endpoint source routines."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'melee-geometry-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(exist_ok=True)
    treepath = BASE / 'd2moo-source-tree.json'; tree = json.loads(treepath.read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs = {str(treepath.relative_to(ROOT)): sha(treepath)}

    def source(name):
        p = BASE / 'd2moo-pinned-source' / name; raw = p.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == name)
        inputs[str(p.relative_to(ROOT))] = sha(p)
        return raw.decode()

    unit = source('source/D2Common/src/Units/Units.cpp')
    collision = source('source/D2Common/include/D2Collision.h')
    weapon = source('source/D2Common/include/D2Composit.h')
    monsters = source('source/D2Common/include/DataTbls/MonsterIds.h')
    def enum_values(raw, name):
        body = re.search(r'enum\s+' + name + r'\s*\{(.*?)\}', raw, re.S).group(1)
        body = re.sub(r'^\s*#.*$', '', re.sub(r'//[^\n]*', '', body), flags=re.M)
        values = {}; current = -1
        for item in body.split(','):
            item = item.strip()
            if not item:
                continue
            if '=' in item:
                key, value = item.split('=', 1); value = value.strip()
                current = values[value] if value in values else int(value, 0)
            else:
                key = item; current += 1
            values[key.strip()] = current
        return values
    ids = enum_values(monsters, 'D2C_MonsterIds')
    assert [ids[n] for n in ['MONSTER_TENTACLE1', 'MONSTER_TENTACLEHEAD1']] == [258, 261]
    assert enum_values(weapon, 'D2WeaponClasses')['WEAPONCLASS_2HT'] == 6
    assert 'COLLIDE_MASK_PLAYER_FLYING = COLLIDE_DOOR | COLLIDE_MISSILE_BARRIER' in collision
    assert re.search(r'COLLIDE_DOOR\s*=\s*0x0800', collision) and re.search(r'COLLIDE_MISSILE_BARRIER\s*=\s*0x0004', collision)
    assert weapon.index('WEAPONCLASS_2HS,') < weapon.index('WEAPONCLASS_2HT,')
    names = ['D2Common_10399', 'UNITS_GetMeleeRange', 'UNITS_TestCollision', 'UNITS_TestCollisionWithUnit', 'UNITS_IsInMeleeRange']
    bodies = {n: function(unit, n) for n in names}
    cpp = r'''
#include <algorithm>
#include <array>
#include <cassert>
#include <cstdio>
#include <vector>
#define __stdcall
#define __fastcall
#define D2_ASSERT assert
using BOOL=int;constexpr BOOL TRUE=1,FALSE=0;
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,MONSTER_TENTACLE1=258,MONSTER_TENTACLEHEAD1=261,WEAPONCLASS_2HT=6,COLLIDE_MASK_PLAYER_FLYING=0x804;
struct D2CoordStrc{int nX=0,nY=0;};struct D2ActiveRoomStrc{};D2ActiveRoomStrc room;
struct D2UnitStrc{int dwUnitType=1,dwClassId=0,x=40,y=40,size=2;void*pInventory=nullptr;};D2UnitStrc actor,target;
struct D2MonStats2Txt{int nMeleeRng=0;};D2MonStats2Txt row;int rawRange=0,weaponClass=0;bool roomPresent=true,blocked=false;
std::vector<std::array<int,5>> rays;
void UNITS_GetCoords(D2UnitStrc*u,D2CoordStrc*c){*c={u->x,u->y};}int UNITS_GetUnitSizeX(D2UnitStrc*u){return u->size;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*u){assert(u==&actor);return roomPresent?&room:nullptr;}
D2MonStats2Txt* UNITS_GetMonStats2TxtRecordFromMonsterId(int){return rawRange<0?nullptr:&row;}
int MONSTERS_GetBaseIdFromMonsterId(int id){return id;}
int COMPOSIT_GetWeaponClassId(D2UnitStrc*,void*,int*out,int,int){*out=weaponClass;return 0;}
bool INVENTORY_GetEquippedWeapon(void*,D2UnitStrc**,int*,BOOL*){assert(false);return false;}int ITEMS_GetMeleeRange(D2UnitStrc*){assert(false);return 0;}
BOOL COLLISION_RayTrace(D2ActiveRoomStrc*r,D2CoordStrc*a,D2CoordStrc*b,int mask){assert(r==&room&&mask==0x804);rays.push_back({a->nX,a->nY,b->nX,b->nY,mask});return blocked;}
'''
    cpp += '\n'.join(bodies.values())
    for name, body in bodies.items():
        assert function(cpp, name) == body
    cpp += r'''
void Rays(){printf("[");for(size_t i=0;i<rays.size();i++){auto t=rays[i];printf("%s[%d,%d,%d,%d,%d]",i?",":"",t[0],t[1],t[2],t[3],t[4]);}printf("]");}
void Reset(int x,int y,int s1,int s2,int roomFlag,int wall){actor={};target={};target.x+=x;target.y+=y;actor.size=s1;target.size=s2;roomPresent=roomFlag;blocked=wall;rays.clear();}
void Range(int presence,int kind,int base,int s1,int s2,int raw,int thrust,int bonus,int roomFlag,int wall,int x,int y,bool&first){
Reset(x,y,s1,s2,roomFlag,wall);rawRange=raw;row.nMeleeRng=raw;weaponClass=thrust?WEAPONCLASS_2HT:5;target.dwUnitType=kind;target.dwClassId=base;
int answer=UNITS_IsInMeleeRange(presence&1?&actor:nullptr,presence&2?&target:nullptr,bonus);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],%d,",first?"":",",presence,kind,base,s1,s2,raw,thrust,bonus,roomFlag,wall,x,y,answer);first=false;Rays();printf("]");}
int main(){printf("{\"geometry\":[");bool first=true;
for(int x:{-9,-8,-7,-4,-3,-2,-1,0,1,2,3,4,7,8,9})for(int y:{-9,-8,-7,-4,-3,-2,-1,0,1,2,3,4,7,8,9})for(int s1:{0,1,2,3,4,255})for(int s2:{0,1,2,3,4,255})for(int r:{0,1})for(int b:{0,1}){
Reset(x,y,s1,s2,r,b);int d=D2Common_10399(&actor,&target);int hit=UNITS_TestCollisionWithUnit(&actor,&target,COLLIDE_MASK_PLAYER_FLYING);
printf("%s[[%d,%d,%d,%d,%d,%d],%d,%d,",first?"":",",x,y,s1,s2,r,b,d,hit);first=false;Rays();printf("]");}
printf("],\"range\":[");first=true;
const std::array<int,2> positions[]={{0,0},{0,1},{2,0},{2,2},{3,0},{3,3},{4,0},{4,4},{7,0},{7,7},{8,0},{8,8},{9,0},{9,9},{12,0},{-9,-3},{3,-9}};
const std::array<int,2> sizes[]={{0,2},{2,2},{3,3},{4,255}};
for(auto p:positions)for(auto s:sizes)for(int raw:{-1,0,1,2,6,254,255})for(int thrust:{0,1})for(int base:{0,258,261,999})for(int bonus:{-1,0,3})for(int r:{0,1})for(int b:{0,1})for(int kind:{0,1})Range(3,kind,base,s[0],s[1],raw,thrust,bonus,r,b,p[0],p[1],first);
for(int present:{0,1,2})for(int bonus:{-1,0,3})for(int b:{0,1})for(int kind:{0,1})for(int base:{258,999})Range(present,kind,base,2,2,255,1,bonus,1,b,3,3,first);
printf("]}\n");}
'''
    path = STAGE / 'source.cpp'; path.write_text(cpp)
    compiler = STAGE / 'compiler.log'; sanitizer = STAGE / 'sanitizer.log'
    with compiler.open('w') as output:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(STAGE / 'probe')], stdout=output, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(STAGE / 'probe')], capture_output=True); sanitizer.write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-3000:]
    result = json.loads(run.stdout)
    assert len(result['geometry']) == 32400 and len(result['range']) == 91464
    out = BASE / 'melee-geometry-source-probe.json'
    profiles = []
    for name in ['monstats', 'monstats2']:
        p = ROOT / 'tools/reference/d2r-named-equipment/json' / (name + '.json'); inputs[str(p.relative_to(ROOT))] = sha(p)
    mon = json.loads((ROOT / 'tools/reference/d2r-named-equipment/json/monstats.json').read_text())
    models = json.loads((ROOT / 'tools/reference/d2r-named-equipment/json/monstats2.json').read_text())
    assert [mon[n]['*hcIdx'] for n in ['tentacle1', 'tentaclehead1']] == [258, 261]
    for name in ['warbighead', 'wargoatman', 'warputriddefiler']:
        m = mon[name]; model = models[m['MonStatsEx']]
        assert m['BaseId'] == name and model['SizeX'] == model['SizeY'] and model['MeleeRng'] != 255
        profiles.append(dict(Key=name, BaseId=m['*hcIdx'], Size=model['SizeX'], RawRange=model.get('MeleeRng', 0)))
    p = ROOT / 'data/Sanctuary/Balance/npc-melee-geometry.json'; encoded = json.dumps(profiles, indent=2) + '\n'
    if check:
        assert p.read_text() == encoded
    else:
        p.write_text(encoded)
    result['profiles'] = profiles
    encoded = json.dumps(result, separators=(',', ':')) + '\n'
    if check:
        assert out.read_text() == encoded
    else:
        out.write_text(encoded)
    inputs[str(Path(__file__).relative_to(ROOT))] = sha(Path(__file__))
    receipt = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], component='Complete melee distance, NPC reach, collision endpoints and range predicate', currentExecutableParity=False, nativeConsumerAccepted=False, handlerEnabled=False,
        compiledUnchangedRoutineHashes={n: hashlib.sha256(b.encode()).hexdigest() for n, b in bodies.items()}, inputs=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in [path, compiler, sanitizer, out, p, *sorted((STAGE / 'diagnostics').glob('*.log'))]},
        verified=dict(geometryContexts=32400, rangeContexts=91464, currentNpcProfiles=profiles, flyingCollisionMask=0x804, tentacleBaseIds=[258, 261]),
        established=['Five complete unchanged routines compose source melee distance, NPC row reach including255weapon-class sentinel, collision endpoint shrinking and final range. Collision-ray service records exact endpoint requests and mask0x804;overlap or missing attacker room avoids the ray. Tentacle special reach uses a strict comparison and bypasses ordinary collision on success;zero distance also bypasses collision.',
            'The distance table uses distinct near-field size rules and a separate far/size>=4 branch. Full-axis source sizes0..255 are tested,including size0 and255. Current owned rows pin the three demon size/reach/base-ID profiles and corroborate special target IDs.'],
        remaining=['The ray body,collision grids/doors/barriers,world facet/height mapping,source base-ID resolution,live monster metadata and current executable linkage remain explicit external services. Player weapon range is not exercised;its inventory branch is retained unchanged but unreachable in these NPC-attacker fixtures. No learned summon or class completion is certified.'])
    p = ROOT / 'tools/reference/melee-geometry-source-evidence.json'; encoded = json.dumps(receipt, indent=2) + '\n'
    if check:
        assert p.read_text() == encoded
    else:
        p.write_text(encoded)
    print('Melee geometry:32400distance/collision and91464range contexts;five complete routines,three current NPC profiles;ray/world adapter remains explicit.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
