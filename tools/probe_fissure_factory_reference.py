#!/usr/bin/env python3
"""Run the existing complete generic factory with current Fissure rows."""
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
    base = (STAGE / 'guided-arrow-source-probe.cpp').read_text().split('int main()', 1)[0]
    table = ROOT / 'tools/reference/d2r-named-equipment/json/missiles.json'
    digest = hashlib.sha256(table.read_bytes()).hexdigest()
    manifest = json.loads((table.parent.parent / 'sources.json').read_text())
    assert digest == manifest['files']['json/missiles.json']['sha256']
    rows = [json.loads(table.read_bytes())[name] for name in ['erruption center', 'erruption crack 1', 'erruption crack 2']]
    for row, lifetime, collision in zip(rows, [80, 84, 84], [0, 3, 3]):
        assert row['Range'] == lifetime and int(row.get('CollideType') or 0) == collision
        assert not any(row.get(k) for k in ['Vel', 'MaxVel', 'LevVel', 'LevRange', 'DamageRate', 'CanSlow'])
    before = 'D2UnitStrc* SUNIT_AllocUnitData(int type,int id,int x,int y,D2GameStrc*,D2ActiveRoomStrc*,int,int,int){creates++;'
    after = 'int allocationMode=-1;D2UnitStrc* SUNIT_AllocUnitData(int type,int id,int x,int y,D2GameStrc*,D2ActiveRoomStrc*,int,int mode,int){allocationMode=mode;creates++;'
    assert base.count(before) == 1
    base = base.replace(before, after, 1)
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int kind:{0,1,2})for(int rank:{1,20,255})for(int shape:{0,1})for(int frame:{0,3})for(int failure=0;failure<6;failure++)for(int bits:{0,1}){
 reset();allocationMode=-1;owner.dwUnitType=failure==2?UNIT_MISSILE:UNIT_PLAYER;hasRecord=failure!=3;hasRoom=failure!=4;allocated=failure!=5;slow=bits!=0;
 row={};row.nVel=row.nMaxVel=row.nVelLev=0;row.wRange=kind==0?80:84;row.wLevRange=row.dwDamageRate=0;row.nCollideType=kind==0?0:3;row.nCollision=kind==0?0:1;row.dwMissileFlags=0;
 D2MissileStrc p;p.dwFlags=(kind==0?1:3)|(shape?0x8000:0)|(frame?0x200:0);p.pOwner=failure==1?nullptr:&owner;
 p.nX=300;p.nY=400;p.nTargetX=100;p.nTargetY=-100;p.nRange=7;p.nFrame=frame;p.nMissile=461+kind;p.nSkill=234;p.nSkillLevel=rank;
 auto* result=MISSILES_CreateMissileFromParams(&game,&p);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,rank,shape,frame,failure,bits,result!=nullptr,creates,allocationMode,result?created.x:0,result?created.y:0,result?created.path.x:0,result?created.path.y:0,result?created.total:0,result?created.remaining:0,result?created.path.velocity:0,result?created.nSeqCurrentFramePrecise:0,result?created.skill:0,result?created.rank:0,result?created.stats[STAT_DAMAGE_FRAMERATE]:0,result?created.owner==&owner:0);first=false;
}printf("]\n");}
'''
    path = STAGE / 'fissure-factory-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'fissure-factory-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 432
    for kind, rank, shape, frame, failure, slow, success, attempts, mode, x, y, tx, ty, total, current, velocity, animation, skill, level, rate, owner in data:
        assert (success, attempts, mode) == (int(failure == 0), int(failure in (0, 5)), (0 if kind == 0 else 3) if failure in (0, 5) else -1)
        if not success:
            assert [x, y, tx, ty, total, current, velocity, animation, skill, level, rate, owner] == [0] * 12
            continue
        lifetime = (7 if shape else 80 if kind == 0 else 84) - frame
        assert (x, y, tx, ty) == (300, 400, 300 if kind == 0 else 400, 400 if kind == 0 else 300)
        assert (total, current, velocity, animation) == (lifetime, lifetime, 0, frame * 256)
        assert (skill, level, rate, owner) == (234, rank, 0, 1)
    fixture = STAGE / 'fissure-factory-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    relative, name = 'source/D2Game/src/MISSILES/Missiles.cpp', 'MISSILES_CreateMissileFromParams'
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=shared['legacyCommit'], handlerEnabled=False, installed=False,
                    sourceHashes={relative: shared['sourceHashes'][relative]}, routineHashes={name: shared['blockHashes'][name]},
                    pinnedMissileHash=digest, fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'Unchanged complete shared factory uses stationary80-frame center461 and84-frame crack462/463 rows. Center flags1 and emitted flags3 keep original owner and skill234/rank; no damage-frame-rate stat or Slow Missiles speed change is introduced.',
                        'Center passes collision mode0 to allocation; cracks pass mode3. The allocation service records that argument, making its supplied field-mode boundary explicit.',
                        'Flags3 treats target components as relative. Emitted requests have default zero target components; the deliberately nonzero control confirms the factory adds rather than replaces coordinates. Zero velocity avoids moving-missile coordinate100 rejection.',
                        'Ordinary requests leave allocator animation0. Separate flags0x200/frame3 and0x8000/range7 controls establish frame subtraction and explicit lifetime override, without attributing either flag to ordinary Fissure requests.',
                        'Missing owner, invalid missile owner type, missing row/room and allocation failure retain their separate early-return and attempt boundaries.'
                    ], services=[
                        'Existing Guided Arrow harness supplies allocation, room/path, owner/seed/stat/damage/event services. Actual allocator, full damage creator and native collision do not execute in this probe.',
                        'Current-row data and older reconstructed factory do not certify modern executable behavior or the crack hit/LastCollide/next-hit identity. No learned Fissure handler or assets are installed.'
                    ], next='Connect fresh skill damage/endpoints and crack collision identity; compose the saved native emitter and cracks before learned casting acceptance.')
    out = ROOT / 'tools/reference/fissure-factory-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print(len(data), 'complete Fissure factory contexts pass; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
