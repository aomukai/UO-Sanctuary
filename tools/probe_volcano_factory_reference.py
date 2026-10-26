#!/usr/bin/env python3
"""Execute the complete shared missile factory with Volcano's three server rows."""
import argparse
import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_guided_arrow_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    shared = json.loads((ROOT / 'tools/reference/guided-arrow-engine-evidence.json').read_text())
    preflight = json.loads((ROOT / 'tools/reference/volcano-preflight.json').read_text())
    raw = (ROOT / 'tools/reference/d2r-named-equipment/json/missiles.json').read_bytes()
    assert hashlib.sha256(raw).hexdigest() == preflight['pinnedInputHashes']['missiles']
    table = json.loads(raw)
    rows = [table[name] for name in ['volcano', 'volcano debris 2', 'volcano small fire']]
    for row, expected in zip(rows, [(150, 0, 3, 1), (16, 14, 0, 0), (18, 0, 3, 0)]):
        assert tuple(int(row.get(k) or 0) for k in ['Range', 'Vel', 'CollideType', 'Collision']) == expected
        assert not any(row.get(k) for k in ['LevRange', 'LevVel', 'DamageRate'])
        assert int(row.get('SubLoop') or 0) == int(row['*ID'] == 479)
        assert int(row.get('MaxVel') or 0) == (14 if row['*ID'] == 481 else 0)
        assert int(row.get('CanSlow') or 0) == int(row['*ID'] != 481)
        assert not any(row.get(k) for k in ['Activate', 'Accel'])
        assert tuple(int(row.get(k) or 0) for k in ['SubStart', 'SubStop']) == ((17, 18) if row['*ID'] == 479 else (0, 0))
    base = (STAGE / 'guided-arrow-source-probe.cpp').read_text().split('int main()', 1)[0]

    def replace(before, after):
        nonlocal base
        assert base.count(before) == 1, before
        base = base.replace(before, after, 1)

    replace('nAttBonus=0,pInitArgs=0;', 'nAttBonus=0,pInitArgs=0,nGfxArg=0;')
    replace('D2UnitStrc* SUNIT_AllocUnitData(int type,int id,int x,int y,D2GameStrc*,D2ActiveRoomStrc*,int,int,int){creates++;',
            'int allocationMode=-1,footprint=-1,moveMask=-1;D2UnitStrc* SUNIT_AllocUnitData(int type,int id,int x,int y,D2GameStrc*,D2ActiveRoomStrc*,int,int mode,int){allocationMode=mode;creates++;')
    replace('void PATH_SetFootprintCollisionMask(D2DynamicPathStrc*,int){}', 'void PATH_SetFootprintCollisionMask(D2DynamicPathStrc*,int v){footprint=v;}')
    replace('void PATH_SetMoveTestCollisionMask(D2DynamicPathStrc*,int){}', 'void PATH_SetMoveTestCollisionMask(D2DynamicPathStrc*,int v){moveMask=v;}')
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int kind:{0,1,2})for(int rank:{1,20,255})for(int dx:{-12,0,1,12,99,100})for(int dy:{-12,0,12})for(int fail=0;fail<7;fail++)for(bool slowBit:{false,true})for(int gfx:{0,30}){
reset();allocationMode=footprint=moveMask=-1;owner.dwUnitType=fail==2?UNIT_MISSILE:UNIT_PLAYER;hasRecord=fail!=3;hasRoom=fail!=4;allocated=fail!=5;slow=slowBit;
// Current source mode0 has no move-test mask. Mode3 mask records the supplied actor/barrier boundary.
stru_6FD2E5F8[0].nCollisionMask=0;stru_6FD2E5F8[3].nCollisionMask=0x804;
row={};row.nVel=row.nMaxVel=kind==1?14:0;row.nSubLoop=kind==0;row.nSubStart=kind==0?17:0;row.nSubStop=kind==0?18:0;row.wRange=kind==0?150:kind==1?16:18;row.nCollideType=kind==1?0:3;row.nCollision=kind==0;row.dwMissileFlags=kind==1?0:gdwBitMasks[MISSILESFLAGINDEX_CANSLOW];
D2MissileStrc p;p.dwFlags=kind==0?1:kind==1?1312:0;p.nX=300;p.nY=400;p.pOwner=fail==1?nullptr:&owner;p.pOrigin=fail==6?nullptr:&target;target.x=300;target.y=400;
p.nTargetX=300+dx;p.nTargetY=400+dy;p.nMissile=kind==0?479:kind==1?481:483;p.nSkill=244;p.nSkillLevel=rank;p.nGfxArg=gfx;
auto* result=MISSILES_CreateMissileFromParams(&game,&p);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,rank,dx,dy,fail,slowBit,gfx,result!=nullptr,creates,allocationMode,footprint,moveMask,result?created.x:0,result?created.y:0,result?created.path.x:0,result?created.path.y:0,result?created.total:0,result?created.remaining:0,result?created.path.velocity:0,result?created.skill:0,result?created.rank:0,result?created.owner==&owner:0,result?created.stats[STAT_DAMAGE_FRAMERATE]:0);first=false;
}printf("]\n");}
'''
    path = STAGE / 'volcano-factory-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'volcano-factory-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 4536
    durations = set()
    for kind, rank, dx, dy, fail, slow, gfx, success, attempts, mode, footprint, mask, x, y, tx, ty, total, current, velocity, skill, level, owner, rate in data:
        eligible = fail not in (1, 2, 3, 4) and (fail != 6 or kind == 0) and (kind != 1 or abs(dx) < 100 and abs(dy) < 100)
        assert (success, attempts, mode) == (int(eligible and fail != 5), int(eligible), (0 if kind == 1 else 3) if eligible else -1)
        if not success:
            assert (footprint, mask) == (-1, -1)
            assert [x, y, tx, ty, total, current, velocity, skill, level, owner, rate] == [0] * 11
            continue
        ax, ay = dx, dy
        if kind == 1 and dx == dy == 0:
            ax = ay = 1
        target = (300 + ax, 400 + ay) if kind == 1 else (300, 400)
        lifetime = max(1, max(abs(ax), abs(ay)) + min(abs(ax), abs(ay)) // 2) * 65536 // (16 * 2688) if kind == 1 else (150 if kind == 0 else 18)
        assert (x, y, tx, ty) == (300, 400, *target)
        assert (footprint, mask) == (4 if kind == 0 else 0, 0 if kind == 1 else 0x804)
        assert (total, current, velocity, skill, level, owner, rate) == ([150, 16, 18][kind], lifetime, 2688 if kind == 1 else 0, 244, rank, 1, 0)
        if kind == 1 and abs(dx) <= 12:
            durations.add(lifetime)
    assert min(durations) == 1 and max(durations) == 27
    fixture = STAGE / 'volcano-factory-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=shared['legacyCommit'], handlerEnabled=False, installed=False,
                    sourceHashes={name: shared['sourceHashes'][name] for name in ['source/D2Game/src/MISSILES/Missiles.cpp', 'source/D2Common/src/Units/Units.cpp']},
                    compiledUnchangedRoutines={name: shared['blockHashes'][name] for name in ['MISSILES_CreateMissileFromParams', 'UNITS_GetDistanceToCoordinates']},
                    pinnedMissileHash=preflight['pinnedInputHashes']['missiles'], fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'Complete factory gives root479 flags1 explicit coordinates, total/current150, collision mode3 and Collision1 footprint. Small-fire483 flags0 requires origin, uses its coordinates, total/current18, mode3 and no footprint.',
                        'Debris481 flags1312 requires origin, keeps absolute target coordinates and has velocity2688=(14<<8)*75/100. TotalFrames remains16; CurrentFrames is floor(max(1,octile distance)*65536/(16*2688)), ranging1..27 for the current square offsets. Exact-self aim nudges both target axes by1. It passes collision mode0 and zero footprint/move-test mask.',
                        'Graphics argument0 versus30 has no effect on factory output. This factory does not establish client height or arc. Rank, original owner and skill244 survive all three routes; supplied damage/stat services remain separate.',
                        'Slow Missiles does not change debris speed because its current row lacks CanSlow; stationary vent/lava rows retain zero speed. Per-axis100 moving-target rejection and missing owner/type/row/room/origin/allocation controls retain distinct attempt boundaries.'
                    ], services=[
                        'Complete generic factory and distance helper execute. Existing Guided Arrow room/allocation/path/stat/damage/seed/event services are supplied, with explicitly recorded mode/footprint/move masks. Actual path movement and damage creator are not executed.',
                        'The mode0 zero move mask is supplied from the separately reviewed mode table. Other mode3 mask here is a boundary sentinel and is not a complete geometry certificate. No modern executable, native handler or final assets are installed.'
                    ], next='Compose complete moving NOCOLLIDE update/terminal route and capture source damage for all three rows; use actual landing coordinates rather than snapping to sampled aim.')
    out = ROOT / 'tools/reference/volcano-factory-source-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == rendered
    else:
        out.write_text(rendered)
    print(len(data), 'complete Volcano factory cases pass; debris current duration differs from total16; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
