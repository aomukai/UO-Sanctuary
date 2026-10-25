#!/usr/bin/env python3
"""Reuse complete update/router/finder bodies for Wake's moving, lasting rows.

Historical JUSTHIT remains explicit; this does not establish modern cast scope.
"""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path

import probe_shock_web_field_reference as basis

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    basis.build(check=True)
    parent = json.loads((ROOT / 'tools/reference/shock-web-field-engine-evidence.json').read_text())
    cpp = (STAGE / 'shock-web-field-source-probe.cpp').read_text().split('void reset()', 1)[0]
    before = 'int UNITS_GetUnitSizeX(D2UnitStrc*){return 1;}'
    assert before in cpp
    cpp = cpp.replace(before, 'int UNITS_GetUnitSizeX(D2UnitStrc* p){return p->dwClassId==517?2:1;}', 1)
    before = 'int COLLISION_CheckMaskWithSize(D2ActiveRoomStrc*,int,int,int,int){return worldMask;}'
    assert before in cpp
    cpp=cpp.replace(before,'int COLLISION_CheckMaskWithSize(D2ActiveRoomStrc*,int,int,int,int mask){return worldMask&mask;}',1)
    pin = ROOT / 'tools/reference/d2r-named-equipment/json/missiles.json'
    digest = hashlib.sha256(pin.read_bytes()).hexdigest()
    manifest = json.loads((pin.parent.parent / 'sources.json').read_text())
    assert digest == manifest['files']['json/missiles.json']['sha256']
    rows = json.loads(pin.read_bytes())
    relative='source/D2Common/include/D2Collision.h'
    raw=(STAGE/'d2moo-pinned-source'/relative).read_bytes()
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text())
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
    text=raw.decode()
    for symbol,value in [('COLLIDE_WALL','0x0001'),('COLLIDE_MISSILE_BARRIER','0x0004'),('COLLIDE_PLAYER','0x0080'),('COLLIDE_MONSTER','0x0100')]:
        assert symbol+' = '+value in text
    source_hashes=parent['sourceHashes']|{relative:hashlib.sha256(raw).hexdigest()}
    for name, size, mode in [('wake of destruction maker', 2, 8), ('wake of destruction', 1, 3)]:
        row = rows[name]
        assert row['Size'] == size and row['CollideType'] == mode and row['NextHit'] == 1 and row['NextDelay'] == 4 and row['LastCollide'] == 1
        assert not any(row.get(key) for key in ['CollideKill', 'ToHit', 'AlwaysExplode', 'pSrvHitFunc', 'pSrvDmgFunc'])
    cpp += r'''
void reset(int kind){
 owner={};target={};other={};parent={};owner.dwUnitType=UNIT_MONSTER;owner.dwUnitId=77;target.dwUnitType=other.dwUnitType=1;other.dwUnitId=20;
 parent.dwClassId=kind?518:517;parent.dwMissileMode=kind?3:8;parent.path.velocity=1536;parent.path.flags=64;parent.pDynamicPath=&parent.path;parent.pMissileData=&parent.md;
 parent.frame=20;parent.activation=535;row={};row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE];row.nCollideKill=0;row.nAlwaysExplode=0;row.wSrvHitFunc=0;row.nNextHit=1;row.nNextDelay=4;
 allocations=0;delay=-1;finds=fills=flags=areas=creates=frameWrites=moves=0;hasOwner=hasRoom=allocate=true;hasRecord=true;worldMask=64;victims={&target,&other};
 stru_6FD2E5F8[parent.dwMissileMode].nCollisionMask=kind?0x184:0x185;
 stru_6FD2E5F8[parent.dwMissileMode].pfUnitFindCallback=MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster;
}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int kind:{0,1})for(int remaining:{1,2,20})for(int velocity:{0,1536})for(int moved:{1,2})for(int room:{0,1})for(int mask:{0,1,4,256,260})for(int last:{-1,10,20,77})for(int firstHit:{0,1})for(int secondHit:{0,1})for(int allocation:{0,1}){
 reset(kind);parent.frame=remaining;parent.path.velocity=velocity;moveResult=moved;hasRoom=room;worldMask=mask;parent.md.nOwnerType=UNIT_MONSTER;parent.md.dwOwnerGUID=last;target.justHit=firstHit;other.justHit=secondHit;allocate=allocation;
 int result=MISSMODE_SrvDo01_BasicMissile(&game,&parent);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,remaining,velocity,moved,room,mask,last,firstHit,secondHit,allocation,result,parent.frame,moves,finds,fills,flags,allocations,delay,parent.md.dwOwnerGUID,int(target.justHit),int(other.justHit));first=false;
}printf("]\n");}
'''
    path = STAGE / 'wake-of-fire-collision-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined',
                    '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'wake-of-fire-collision-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 7680
    for kind, remaining, velocity, moved, room, mask, last, hit1, hit2, alloc, result, after, moves, finds, fills, flags, allocations, delay, history, now1, now2 in data:
        early = velocity != 0 and moved == 2
        expired = not early and remaining <= 1
        effective_mask = mask & (0x184 if kind else 0x185)
        search = not early and not expired and bool(room and effective_mask)
        selected = 10 if search and not hit1 and last!=10 else 20 if search and not hit2 and last!=20 else -1
        delivered = selected >= 0
        expected_result = 2 if early or expired or not room or search and selected < 0 and effective_mask & 4 else 1
        assert result == expected_result and after == (remaining if early else remaining - 1)
        assert moves == int(velocity != 0) and finds == int(search)
        assert fills == flags == allocations == int(delivered)
        assert delay == (1004 if delivered else -1)
        assert history == (selected if delivered else last)
        assert now1 == int(bool(hit1 or delivered and selected == 10 and alloc))
        assert now2 == int(bool(hit2 or delivered and selected == 20 and alloc))
    fixture = STAGE / 'wake-of-fire-collision-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(
        referenceVersion='3.3.93847', talentId='assassin.wakeoffire', handlerEnabled=False,
        legacyCommit=parent['legacyCommit'], sourceHashes=source_hashes,
        compiledRoutineHashes=parent['compiledUnchangedRoutines'], pinnedMissileHash=digest,
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
        established=[
            'Both moving maker mode8/Size2 and wave mode3/Size1 use the same full player/monster finder; their terrain mask distinction remains supplied by the respective route. Neither row uses AR, CollideKill or a special hit/damage callback.',
            'The complete generic update moves before lifetime decrement. A failed move ends without decrement; otherwise lifetime0 ends before searching. Valid collider selection has priority over the barrier bit, and no collider plus barrier ends the missile.',
            'Both NextHit and LastCollide are checked in the finder, allowing a delayed or repeated first candidate to expose the second. LastCollide is checked again in the router before updating history; a repeated first candidate must not hide another eligible actor.',
            'A fresh selected target updates LastCollide and starts the four-frame historical JUSTHIT lease before damage fill/flags. Failed lease allocation still routes the current hit. Previous history survives delayed/repeated/no-target/expiry paths.',
            'Producer do31, verified separately, attempts one opposite pair before this update using the prior path-change flag. Multiple crossed cells do not themselves imply one pair per cell or a time-only emission fan.'
        ], services=[
            'Actual path movement, rooms, terrain masks, saved-step geometry, candidate enumeration and final damage fill/commit are explicit services. Source Size1/2 values are declared at the footprint service; native spatial projection remains separate.',
            'This uses historical global JUSTHIT for source ordering only. Current D2R same-skill supplier/cast identity and shared maker/wave delay scope remain acceptance work; no new native lease policy is invented here.',
            'The maker-specific opposite emission, complete factory and fire getter proofs are separate. This does not establish an actual world wave, connected damage, retired owner/stat lifetime or current executable equivalence.'
        ], sanitizers='ASan/UBSan: exit0 and empty runtime stderr required')
    out = ROOT / 'tools/reference/wake-of-fire-collision-source-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded, 'Wake collision evidence stale'
    else:
        out.write_text(encoded)
    print('Wake collision: 7,680 complete update/router/finder/history/delay cases; sanitizers clean; modern delay scope remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
