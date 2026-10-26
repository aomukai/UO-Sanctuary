#!/usr/bin/env python3
"""Run complete generic update/hit router with Volcano debris hit51 and movement bridge."""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_fire_blast_landing_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/fire-blast-landing-engine-evidence.json').read_text())
    engine = json.loads((ROOT / 'tools/reference/volcano-engine-evidence.json').read_text())
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == engine['legacyCommit']
    hashes, blocks = {}, {}

    def routine(relative, name):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        source = raw.decode()
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', source, re.M)
        assert match, name
        end, depth = source.index('{', match.start()) + 1, 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}')
            end += 1
        body = source[match.start():end]
        blocks[name] = hashlib.sha256(body.encode()).hexdigest()
        return body

    base = (STAGE / 'fire-blast-landing-source-probe.cpp').read_text().split('int main()', 1)[0]
    # Keep only the shared declarations/bodies; Fire Blast's own reset/emit are not used.
    base = base[:base.index('void reset(int kind)')]

    def replace(before, after):
        nonlocal base
        assert base.count(before) == 1, before
        base = base.replace(before, after, 1)

    replace('wHitSubMissile[1]={386}', 'wHitSubMissile[3]={483,-1,-1}')
    replace('D2UnitStrc* pOwner=nullptr;}captured;', 'D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;}captured;')
    replace('int32_t MISSMODE_SrvHit51_VolcanoDebris(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return 3;}',
            routine('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_SrvHit51_VolcanoDebris'))
    replace('child.x=p->nX;child.y=p->nY;', 'child.x=p->dwFlags&1?p->nX:p->pOrigin->x;child.y=p->dwFlags&1?p->nY:p->pOrigin->y;')
    replace('int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){moves++;return moveResult;}',
            '''int stepCalls=0,validationCalls=0,roomRepairs=0;bool invalidRoom=false;
void sub_6FCBC7E0(D2GameStrc*,D2UnitStrc*){validationCalls++;}
int D2Common_10226(D2UnitStrc* p,int speed){assert(speed==1024);stepCalls++;moves++;p->x+=2;p->y-=1;return moveResult==1;}
bool UNITS_IsCurrentRoomInvalid(D2UnitStrc*){return invalidRoom;}
void sub_6FCBB190(D2GameStrc*,D2UnitStrc*,int arg){assert(arg==0);roomRepairs++;}
''' + routine('source/D2Game/src/UNIT/SUnit.cpp', 'sub_6FCBC930'))
    base = '#define D2_ASSERT assert\n' + base
    # Re-extract and verify the reused full update/router bodies against this pin.
    for name in ['MISSMODE_HandleMissileCollision', 'MISSMODE_SrvDmgHitHandler', 'MISSMODE_SrvDo01_BasicMissile']:
        body = routine('source/D2Game/src/MISSILES/MissMode.cpp', name)
        assert base.count(body) == 1
        assert blocks[name] == parent['compiledUnchangedRoutines'][name]
    base += r'''
void reset(){owner={};parent={};child={};target={};owner.dwUnitType=0;parent.dwClassId=481;parent.dwMissileMode=MISSMODE_NOCOLLIDE;parent.skill=244;parent.rank=20;parent.pDynamicPath=&parent.path;parent.pMissileData=&parent.md;parent.path.velocity=2688;parent.path.flags=64;parent.frame=27;parent.activation=16;row={};row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_EXPLOSION];row.wSrvHitFunc=51;row.nCollideKill=row.nNextHit=0;hasRecord=hasSkill=hasOwner=hasRoom=factory=true;creates=fills=flags=areas=events=commits=defenses=moves=finds=frameWrites=ar=stepCalls=validationCalls=roomRepairs=0;moveResult=1;invalidRoom=false;captured={};}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("{\"updates\":[");
for(int frame:{0,1,2,16,27})for(int movement:{1,2})for(int pathFlags:{0,1,2,4,5,64})for(bool roomExists:{false,true})for(bool own:{false,true})for(bool invalid:{false,true}){
reset();parent.frame=frame;moveResult=movement;parent.path.flags=pathFlags;hasRoom=roomExists;hasOwner=own;invalidRoom=invalid;
int result=MISSMODE_SrvDo01_BasicMissile(&game,&parent);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",frame,movement,pathFlags,roomExists,own,invalid,result,parent.frame,moves,validationCalls,stepCalls,roomRepairs,finds,creates,fills,commits,parent.x,parent.y,child.x,child.y,captured.nSkill,captured.nSkillLevel,captured.pOrigin==&parent,captured.pOwner==nullptr);first=false;
}printf("],\"routes\":[");first=true;
for(bool victim:{false,true})for(bool ending:{false,true})for(bool own:{false,true})for(bool explode:{false,true}){
reset();hasOwner=own;row.nAlwaysExplode=explode;
int result=MISSMODE_SrvDmgHitHandler(&game,&parent,victim?&target:nullptr,ending);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",victim,ending,own,explode,result,creates,fills,commits,captured.pOrigin==&parent);first=false;
}printf("],\"lifetimes\":[");first=true;
for(int duration=1;duration<=27;duration++){reset();parent.frame=duration;int calls=0,result=1;while(result==1&&calls<100){result=MISSMODE_SrvDo01_BasicMissile(&game,&parent);calls++;}
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",duration,calls,result,parent.frame,creates,child.x,child.y,finds);first=false;
}printf("]}\n");}
'''
    path = STAGE / 'volcano-landing-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'volcano-landing-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    counts = {key: len(value) for key, value in data.items()}
    assert counts == dict(updates=480, routes=16, lifetimes=27)
    for frame, movement, path_flags, room, own, invalid, result, current, moves, validation, steps, repairs, finds, creates, fills, commits, x, y, landing_x, landing_y, skill, rank, origin, null_owner in data['updates']:
        ending = movement == 2 or frame <= 1
        assert (result, current, moves, validation, steps, repairs, finds, creates, fills, commits) == (2 if ending else 1, frame-int(movement != 2), 1, 1, 1, invalid, 0, int(ending), 0, 0)
        assert (x, y) == (42, 69)
        assert (landing_x, landing_y, skill, rank, origin, null_owner) == ((42, 69, 244, 20, 1, int(not own)) if ending else (40, 70, 0, 0, 0, 1))
    for victim, ending, own, explode, result, creates, fills, commits, origin in data['routes']:
        makes_child = not victim and (ending or explode)
        assert (result, creates, fills, commits, origin) == (1 if victim or not makes_child else 2, int(makes_child), 0, 0, int(makes_child))
    for duration, calls, result, current, creates, x, y, finds in data['lifetimes']:
        assert (calls, result, current, creates, x, y, finds) == (duration, 2, 0, 1, 40+duration*2, 70-duration, 0)
    fixture = STAGE / 'volcano-landing-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
                    sourceHashes=hashes, compiledUnchangedRoutines=blocks,
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), caseCounts=counts),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'Complete do1, generic collision, hit router, hit51 and movement bridge compose. Moving NOCOLLIDE mode advances before decrement, returns before room/path-flag/unit-finder checks, and creates its terminal child at the advanced position. CurrentFrame1 still advances once before expiry. Movement-end return2 invokes the terminal route before any frame decrement.',
                        'Each complete movement bridge first validates tracked targets, calls the motion primitive with1024, and then repairs an invalid current room. Its returned1/2 follows motion success; room repair itself does not independently change the returned value.',
                        'Mode0 actor-victim controls cannot damage, invoke hit51 or create lava. Null-victim expiry/AlwaysExplode routes do create child483. Child uses flags0 and debris origin with retained skill244/rank20; missing-owner request reaches the declared factory but the independently proven full factory rejects it.',
                        'Natural lifetime1..27 terminates after exactly that many moving body calls, creating exactly one child at the actual last moved position. No native target snap, extra16-frame timer or barrier/actor explosion follows from these paths.'
                    ], services=[
                        'Source target validation and FP16 movement/room repair are declared recording services. The supplied step advances(+2,-1), allowing actual-origin versus old-position assertions; it does not prove that velocity or modern trajectory. Existing source path/velocity kernels supply separate evidence.',
                        'Factory, room/row/owner/state/stat/defense/event services are declared. Complete factory is covered separately; source allocator/path initialization/room geometry, damage creator and modern executable remain separate. Unrelated callback table entries remain unselected stubs.',
                        'No native handler, final effects/audio or icons are installed. Root/lava actor-contact and modern NHD grouping require their own native acceptance.'
                    ], next='Capture complete root/debris/lava damage, then implement saved actors using existing verified FP16 velocity/step consumers with explicit mode0 movement.')
    out = ROOT / 'tools/reference/volcano-landing-source-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == rendered
    else:
        out.write_text(rendered)
    print('Volcano:', counts, 'complete generic update/terminal compositions pass; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
