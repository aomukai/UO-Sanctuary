#!/usr/bin/env python3
"""Execute the unchanged shared factory with pinned Molten Boulder rows."""
import argparse
import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_guided_arrow_reference.py'), '--check'],
                   check=True, stdout=subprocess.DEVNULL)
    shared = json.loads((ROOT / 'tools/reference/guided-arrow-engine-evidence.json').read_text())
    base = (STAGE / 'guided-arrow-source-probe.cpp').read_text().split('int main()', 1)[0]
    table = ROOT / 'tools/reference/d2r-named-equipment/json/missiles.json'
    digest = hashlib.sha256(table.read_bytes()).hexdigest()
    manifest = json.loads((table.parent.parent / 'sources.json').read_text())
    assert digest == manifest['files']['json/missiles.json']['sha256']
    rows = json.loads(table.read_bytes())
    names = ['moltenboulderemerge', 'moltenboulder', 'moltenboulderfirepath']
    for name, velocity, lifetime, initial in zip(names, [3, 8, 0], [5, 50, 37], [5, 0, 0]):
        row = rows[name]
        assert int(row.get('Vel') or 0) == velocity and row['Range'] == lifetime
        assert int(row.get('InitSteps') or 0) == initial
        assert row['CanSlow'] == 1 and row['CollideType'] == 8
        assert not row.get('LevVel') and not row.get('LevRange')
    # This schema member exists in the pinned header. It is deliberately varied
    # without changing the complete factory body, which never reads it.
    before = 'struct D2MissilesTxt{int nVel='
    assert before in base
    base = base.replace(before, 'struct D2MissilesTxt{int nInitSteps=0;int nVel=', 1)
    before = 'int STATLIST_GetStatValue(D2StatListStrc*,int,int){return 50;}'
    assert before in base
    base = base.replace(before, 'int slowPercent=50;int STATLIST_GetStatValue(D2StatListStrc*,int,int){return slowPercent;}', 1)
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("[");
int aims[][2]={{0,0},{10,0},{-12,17},{100,0}};
for(int kind:{0,1,2,3})for(int rank:{1,20,255})for(int aim=0;aim<4;aim++)for(int slowed:{0,1,2})for(int initial:{0,5})for(int frame:{0,3})for(int failure=0;failure<6;failure++){
 reset();owner.dwUnitType=failure==2?UNIT_MISSILE:UNIT_PLAYER;hasRecord=failure!=3;hasRoom=failure!=4;allocated=failure!=5;
 slow=slowed!=0;slowPercent=slowed==2?0:50;row={};row.nInitSteps=initial;
 row.nVel=row.nMaxVel=kind==0?3:kind==1?8:0;row.wRange=kind==0?5:kind==1?50:37;row.wLevRange=0;
 row.nCollideType=8;row.dwDamageRate=kind>=2?41:0;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_CANSLOW];
 D2MissileStrc p;p.dwFlags=(kind==3?0x8001:33)|(frame?0x200:0);p.pOwner=failure==1?nullptr:&owner;
 p.nX=47;p.nY=79;p.nTargetX=47+aims[aim][0];p.nTargetY=79+aims[aim][1];p.nFrame=frame;p.nRange=7;
 p.nMissile=kind==0?453:kind==1?452:455;p.nSkill=229;p.nSkillLevel=rank;
 auto* result=MISSILES_CreateMissileFromParams(&game,&p);
 std::printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,rank,aim,slowed,initial,frame,failure,result!=nullptr,creates,result?created.x:0,result?created.y:0,result?created.path.x:0,result?created.path.y:0,result?created.total:0,result?created.remaining:0,result?created.path.velocity:0,result?created.nSeqCurrentFramePrecise:0,result?created.skill:0,result?created.stats[STAT_DAMAGE_FRAMERATE]:0,result?created.owner==&owner:0);first=false;
}std::printf("]\n");}
'''
    path = STAGE / 'molten-boulder-factory-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined',
                    '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'molten-boulder-factory-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 3456
    aims = [(0, 0), (10, 0), (-12, 17), (100, 0)]
    for kind, rank, aim, slowed, initial, frame, failure, result, creates, x, y, tx, ty, total, remaining, velocity, animation, skill, rate, owner in data:
        raw_velocity = (3 if kind == 0 else 8 if kind == 1 else 0) * 256
        expected_velocity = raw_velocity * (0 if slowed == 2 else 50 if slowed else 100) // 100 * 75 // 100
        dx, dy = aims[aim] if kind != 3 else (0, 0)
        reaches_allocation = failure in (0, 5) and (expected_velocity == 0 or abs(dx) < 100 and abs(dy) < 100)
        succeeds = reaches_allocation and failure != 5
        assert (result, creates) == (int(succeeds), int(reaches_allocation))
        if not succeeds:
            assert [x, y, tx, ty, total, remaining, velocity, animation, skill, rate, owner] == [0] * 11
            continue
        if expected_velocity and dx == dy == 0:
            dx = dy = 1
        expected_range = [5, 50, 37, 7][kind] - frame
        assert (x, y, tx, ty) == (47, 79, 47 + dx, 79 + dy)
        assert (total, remaining, velocity, animation) == (expected_range, expected_range, expected_velocity, frame * 256)
        assert (skill, rate, owner) == (229, 41 if kind >= 2 else 0, 1)
    fixture = STAGE / 'molten-boulder-factory-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    name = 'MISSILES_CreateMissileFromParams'
    relative = 'source/D2Game/src/MISSILES/Missiles.cpp'
    evidence = dict(
        referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
        legacyCommit=shared['legacyCommit'], sourceHashes={relative: shared['sourceHashes'][relative]},
        routineHashes={name: shared['blockHashes'][name]}, pinnedMissileHash=digest,
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
        established=[
            'Unchanged complete shared factory executes current emergence453, rolling452, ground455 and disc455 request shapes; copied skill229/rank and declared original owner remain attached.',
            'Current default lifetimes are5/50/37frames. Disc flags0x8001 overrides the child lifetime to7frames. Current factory velocity factor75percent gives576/1536/0FP8; declared Slow Missiles50percent gives288/768/0.',
            'InitSteps is a declared schema field varied between0and5. The complete factory does not read it: every paired result is unchanged. Do not introduce five additional server updates from that field. Client/path behavior outside this body is not established.',
            'The separate flags0x200/frame3 control reduces lifetime by3and sets sequence frame768; ordinary Boulder request shapes do not set that flag and leave the allocator-supplied frame at0.',
            'Absolute flags33 aim, direct flags0x8001 ground children, same-position moving fallback, coordinate100 rejection, zero-velocity bypass and missing owner/invalid owner/row/room/allocation controls execute in the shared body.'
        ], services=[
            'Uses the existing Guided Arrow harness with declared room/path/allocation/stat/damage/owner/seed/event services. Allocation starts animation at0; this probe does not establish the complete production animation clock producer.',
            'Initial learned skill dispatch, actual movement, hit dispatcher, area/direct damage and knockback eligibility, field collision cadence, modern executable and rendered client remain separate acceptance work.',
            'Final effects/audio and talent/left/right/quick icons remain pending; no installed handler is enabled by this probe.'
        ], sanitizers='ASan/UBSan exit0 and empty runtime stderr required')
    out = ROOT / 'tools/reference/molten-boulder-factory-source-evidence.json'
    if check:
        assert json.loads(out.read_text()) == evidence, 'factory evidence changed'
    else:
        out.write_text(json.dumps(evidence, indent=2) + '\n')
    print(f'Molten Boulder: {len(data)} complete factory contexts passed with clean sanitizers.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
