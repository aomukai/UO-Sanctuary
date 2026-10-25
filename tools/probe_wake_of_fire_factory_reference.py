#!/usr/bin/env python3
"""Run the shared complete factory with Wake's relative aim and current rows."""
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
    maker, wave = rows['wake of destruction maker'], rows['wake of destruction']
    assert maker['Range'] == '25 +  (lvl *  2)' and wave['Range'] == 20
    assert (maker['Vel'], maker['MaxVel'], maker['CollideType']) == (10, 10, 8)
    assert (wave['Vel'], wave['MaxVel'], wave['CollideType']) == (8, 8, 3)
    for row in [maker, wave]:
        assert row['CanSlow'] == 1 and row['DamageRate'] == 205
        assert not row.get('LevVel') and not row.get('Pierce') and not row.get('SrcMissDmg')
    before='int STATLIST_GetStatValue(D2StatListStrc*,int,int){return 50;}'
    assert before in base
    base=base.replace(before,'int slowPercent=50;int STATLIST_GetStatValue(D2StatListStrc*,int,int){return slowPercent;}',1)
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("[");
int aims[][2]={{0,0},{10,0},{0,-10},{-12,17},{99,-99},{100,0},{0,-100}};
for(int kind:{0,1})for(int rank:{1,3,20,40,255})for(int aim=0;aim<7;aim++)for(int slowed:{0,1,2})for(int present:{0,1})for(int record:{0,1})for(int roomOk:{0,1})for(int alloc:{0,1})for(int origin:{0,1}){
 reset();owner.dwUnitType=UNIT_MONSTER;hasRecord=record;hasRoom=roomOk;allocated=alloc;slow=slowed!=0;slowPercent=slowed==2?0:50;row={};
 row.nVel=row.nMaxVel=kind?8:10;row.nVelLev=0;row.wRange=kind?20:25;row.wLevRange=kind?0:2;
 row.nCollideType=kind?3:8;row.dwDamageRate=205;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_CANSLOW];
 // Origin is the trap for makers and a different missile position for waves.
 other.dwUnitType=UNIT_MISSILE;other.x=47;other.y=79;
 D2MissileStrc p;p.dwFlags=2;p.pOwner=present?&owner:nullptr;p.pOrigin=origin?(kind?&other:&owner):nullptr;
 p.nMissile=kind?518:517;p.nSkill=281;p.nSkillLevel=rank;p.nTargetX=aims[aim][0];p.nTargetY=aims[aim][1];
 auto* result=MISSILES_CreateMissileFromParams(&game,&p);
 std::printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,rank,aim,slowed,present,record,roomOk,alloc,origin,result!=nullptr,creates,result?created.x:0,result?created.y:0,result?created.path.x:0,result?created.path.y:0,result?created.total:0,result?created.path.velocity:0,result?created.skill:0,result?created.stats[STAT_DAMAGE_FRAMERATE]:0,result?created.owner==&owner:0);first=false;
}std::printf("]\n");}
'''
    path = STAGE / 'wake-of-fire-factory-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined',
                    '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'wake-of-fire-factory-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 6720
    aims = [(0, 0), (10, 0), (0, -10), (-12, 17), (99, -99), (100, 0), (0, -100)]
    for kind, rank, aim, slowed, present, record, room, allocated, origin, result, creates, x, y, tx, ty, total, velocity, skill, damage_rate, owner in data:
        dx, dy = aims[aim]
        reaches_allocation = bool(present and record and room and origin and (slowed==2 or abs(dx) < 100 and abs(dy) < 100))
        succeeds = reaches_allocation and allocated
        assert result == int(succeeds) and creates == int(reaches_allocation)
        if not succeeds:
            assert [x, y, tx, ty, total, velocity, skill, damage_rate, owner] == [0] * 9
            continue
        expected_origin = (47, 79) if kind else (40, 70)
        if slowed!=2 and dx == dy == 0:
            dx = dy = 1
        assert (x, y) == expected_origin and (tx, ty) == (x + dx, y + dy)
        assert total == (20 if kind else 25 + 2 * rank)
        assert velocity == ((8 if kind else 10) * 256 * (0 if slowed==2 else 50 if slowed else 100) // 100) * 75 // 100
        assert (skill, damage_rate, owner) == (281, 205, 1)
    fixture = STAGE / 'wake-of-fire-factory-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    name = 'MISSILES_CreateMissileFromParams'
    relative = 'source/D2Game/src/MISSILES/Missiles.cpp'
    evidence = dict(
        referenceVersion='3.3.93847', talentId='assassin.wakeoffire', handlerEnabled=False,
        legacyCommit=shared['legacyCommit'], sourceHashes={relative: shared['sourceHashes'][relative]},
        routineHashes={name: shared['blockHashes'][name]}, pinnedMissileHash=digest,
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
        established=[
            'Complete factory flags2 starts makers at the trap and waves at the maker origin, adding the supplied relative vector to that position. Original actor remains the stored owner and skill281/rank remain attached.',
            'A zero relative vector is changed locally to (+1,+1) for the moving path. This does not rewrite the caller params that do125 later rotates; zero stored perpendicular vectors therefore remain distinct from the factory path fallback.',
            'With nonzero velocity, either relative coordinate with absolute value100 or greater rejects before allocation. Zero velocity bypasses this guard and the zero-aim fallback. Missing owner, record, origin or room also prevents allocation; allocation failure returns null.',
            'Current maker lifetime is 25+2*rank and wave lifetime20. The shared source factory velocity factor75 percent gives1920/1536 FP8 respectively; declared Slow Missiles50 percent gives960/768, and the zero-percent control gives zero velocity. Both receive damage-framerate205 after creation.',
            'Maker range is explicitly projected from the current affine Range expression into the historical base/level fields (25,2). The expression text is pinned; no claim is made that the old factory executes the modern expression language.'
        ], services=[
            'World/room allocation, path construction, stat-list allocation, damage creation/writing, stat/seed/owner/event operations remain the checked Guided Arrow harness services. The complete factory body is unchanged.',
            'This fixture does not execute actual missile path movement, maker emission, child damage/stat capture or collision/NextHit/LastCollide. Separate source producer and fire getter proofs must be composed with native movement and lifecycle.',
            'Slow Missiles has declared50- and0-percent stat controls. Full live slow-state capture, actor death/stat cleanup, current executable factory and rendered graphics remain outside this proof.'
        ], sanitizers='ASan/UBSan: exit0 and empty runtime stderr required')
    out = ROOT / 'tools/reference/wake-of-fire-factory-source-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded, 'Wake factory evidence stale'
    else:
        out.write_text(encoded)
    print('Wake factory: 6,720 complete relative-aim/range/velocity/guard cases; sanitizers clean.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
