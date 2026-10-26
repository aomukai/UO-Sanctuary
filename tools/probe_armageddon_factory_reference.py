#!/usr/bin/env python3
"""Execute the full shared factory with current Armageddon control/ground rows."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_guided_arrow_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    shared = json.loads((ROOT / 'tools/reference/guided-arrow-engine-evidence.json').read_text())
    preflight = json.loads((ROOT / 'tools/reference/druid-weather-preflight.json').read_text())
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    raw = (ROOT / 'tools/reference/d2r-named-equipment/json/missiles.json').read_bytes()
    assert hashlib.sha256(raw).hexdigest() == preflight['pinnedInputHashes']['missiles']
    rows = [json.loads(raw)[name] for name in ['armageddoncontrol', 'armageddonfire']]
    for row, lifetime, mode, collision, rate in zip(rows, [25, 37], [0, 8], [0, 1], [0, 41]):
        assert [int(row.get(k) or 0) for k in ['Range', 'CollideType', 'Collision', 'DamageRate']] == [lifetime, mode, collision, rate]
        assert row['CanSlow'] == 1 and not any(row.get(k) for k in ['Vel', 'MaxVel', 'LevVel', 'LevRange', 'Accel', 'Activate'])
    # Pin real collision constants/table8, replacing the parent's opaque sentinels.
    hashes = {}
    for relative in ['source/D2Common/include/D2Collision.h', 'source/D2Game/src/MISSILES/MissMode.cpp']:
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        if relative.endswith('.h'):
            constants = {name: int(re.search(r'\b' + name + r'\s*=\s*(0x[0-9A-Fa-f]+)', raw.decode()).group(1), 16)
                         for name in ['COLLIDE_WALL', 'COLLIDE_MISSILE_BARRIER', 'COLLIDE_MISSILE', 'COLLIDE_PLAYER', 'COLLIDE_MONSTER']}
        else:
            assert '/*[8]*/{ MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster  , COLLIDE_MONSTER | COLLIDE_PLAYER | COLLIDE_MISSILE_BARRIER | COLLIDE_WALL }' in raw.decode()
    mask = constants['COLLIDE_WALL'] | constants['COLLIDE_MISSILE_BARRIER'] | constants['COLLIDE_PLAYER'] | constants['COLLIDE_MONSTER']
    assert mask == 0x185 and constants['COLLIDE_MISSILE'] == 0x40
    cpp = (STAGE / 'guided-arrow-source-probe.cpp').read_text().split('int main()', 1)[0]

    def replace(before, after):
        nonlocal cpp
        assert cpp.count(before) == 1, before
        cpp = cpp.replace(before, after, 1)

    replace('COLLIDE_MISSILE=4,COLLIDE_NONE=0;', 'COLLIDE_MISSILE=64,COLLIDE_NONE=0;')
    replace('D2UnitStrc* SUNIT_AllocUnitData(int type,int id,int x,int y,D2GameStrc*,D2ActiveRoomStrc*,int,int,int){creates++;',
            'int allocationMode=-1,footprint=-1,moveMask=-1;D2UnitStrc* SUNIT_AllocUnitData(int type,int id,int x,int y,D2GameStrc*,D2ActiveRoomStrc*,int,int mode,int){allocationMode=mode;creates++;')
    replace('void PATH_SetFootprintCollisionMask(D2DynamicPathStrc*,int){}', 'void PATH_SetFootprintCollisionMask(D2DynamicPathStrc*,int mask){footprint=mask;}')
    replace('void PATH_SetMoveTestCollisionMask(D2DynamicPathStrc*,int){}', 'void PATH_SetMoveTestCollisionMask(D2DynamicPathStrc*,int mask){moveMask=mask;}')
    cpp += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int kind:{0,1})for(int rank:{1,20,255})for(int overrideRange:{0,1})for(int frame:{0,3})for(int loops:{0,2})for(int failure=0;failure<6;failure++)for(int slowBit:{0,1}){
reset();allocationMode=footprint=moveMask=-1;owner.dwUnitType=failure==2?UNIT_MISSILE:UNIT_PLAYER;hasRecord=failure!=3;hasRoom=failure!=4;allocated=failure!=5;slow=slowBit;
stru_6FD2E5F8[0].nCollisionMask=0;stru_6FD2E5F8[8].nCollisionMask=0x185;
row={};row.nVel=row.nVelLev=row.nMaxVel=0;row.wRange=kind?37:25;row.wLevRange=0;row.nCollideType=kind?8:0;row.nCollision=kind;row.dwDamageRate=kind?41:0;row.nSubLoop=kind;row.nSubStart=kind?12:0;row.nSubStop=kind?36:0;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_CANSLOW];
D2MissileStrc p;p.dwFlags=1|(overrideRange?0x8000:0)|(frame?0x200:0)|(loops?8:0);p.pOwner=failure==1?nullptr:&owner;p.nX=300;p.nY=400;p.nTargetX=500;p.nTargetY=600;p.nRange=7;p.nFrame=frame;p.nLoops=loops;p.nMissile=kind?623:577;p.nSkill=249;p.nSkillLevel=rank;
auto* result=MISSILES_CreateMissileFromParams(&game,&p);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,rank,overrideRange,frame,loops,failure,slowBit,result!=nullptr,creates,allocationMode,footprint,moveMask,result?created.x:0,result?created.y:0,result?created.path.x:0,result?created.path.y:0,result?created.total:0,result?created.remaining:0,result?created.path.velocity:0,result?created.nSeqCurrentFramePrecise:0,result?created.skill:0,result?created.rank:0,result?created.stats[STAT_DAMAGE_FRAMERATE]:0);first=false;
}printf("]\n");}
'''
    path = STAGE / 'armageddon-factory-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'armageddon-factory-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 576
    for kind, rank, override, frame, loops, failure, slow, success, attempts, mode, footprint, move, x, y, tx, ty, total, current, velocity, animation, skill, level, rate in data:
        eligible = failure in (0, 5)
        assert [success, attempts, mode] == [int(failure == 0), int(eligible), (8 if kind else 0) if eligible else -1]
        if not success:
            assert [footprint, move] == [-1, -1] and [x, y, tx, ty, total, current, velocity, animation, skill, level, rate] == [0]*11
            continue
        lifetime = (7 if override else (37+loops*24 if kind else 25))-frame
        assert [footprint, move, x, y, tx, ty, total, current, velocity, animation, skill, level, rate] == [64 if kind else 0, 0x185 if kind else 0, 300, 400, 300, 400, lifetime, lifetime, 0, frame*256, 249, rank, 41 if kind else 0]
    fixture = STAGE / 'armageddon-factory-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    relative = 'source/D2Game/src/MISSILES/Missiles.cpp'
    hashes[relative] = shared['sourceHashes'][relative]
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=shared['legacyCommit'], handlerEnabled=False, installed=False,
                    pinnedMissileHash=preflight['pinnedInputHashes']['missiles'], sourceHashes=hashes,
                    compiledUnchangedRoutines={'MISSILES_CreateMissileFromParams': shared['blockHashes']['MISSILES_CreateMissileFromParams']},
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'Complete factory uses current stationary control577/25frames/mode0/no footprint/no damage-frame-rate stat and ground623/37frames/mode8/Collision1/DamageRate41. Both keep owner, skill249 and requested rank1/20/255 through explicit flags1 coordinates.',
                        'Real pinned collision constants replace inherited opaque sentinels: ground footprint mask0x40 and mode8 move-test mask0x185 (monster/player/missile barrier/wall); control mode0 passes zero. This records requested masks, not actual room collision.',
                        'Current SubLoop1/12..36 does not extend ordinary flags1 field lifetime. Separate hypothetical flags8/loops2, range override7 and frame3 controls exercise loop extension/override/subtraction without attributing those flags to ordinary Armageddon requests.',
                        'Slow Missiles on/off does not move either zero-velocity row or change lifetime. Missing owner/invalid missile owner/missing row/room and allocation failure preserve distinct attempt boundaries.'
                    ], services=[
                        'Inherited Guided Arrow allocation/room/path/owner/seed/stat/damage/event services remain supplied fixtures. Actual allocation, footprint registration, current collision and complete damage capture do not execute; separate source damage evidence covers getters/creator/writer.',
                        'This current-row/older factory composition does not certify modern seed/timing/owner/room/client behavior or native masks. Field ground delivery/expiry/history/defenses/DamageRate41/MDR and player weather/lifecycle/input/reloads remain separate.',
                        'No Armageddon handler, class completion or final effects/icons are enabled.'
                    ], next='Compose field623 basic collision/damage flags and DamageRate41 with existing Boulder ground consumers, preserving differences from its do5 animation and Firestorm SoftHit mode3.')
    out = ROOT / 'tools/reference/armageddon-factory-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Armageddon:576complete control/ground factory cases pass with pinned0x40/0x185masks; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
