#!/usr/bin/env python3
"""Execute complete blind tick and walk-near tactics with declared world services."""
import argparse
import ast
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/dim-vision-ai-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    tree = json.loads((ROOT / '.local/class-skills/d2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    blobs = {r['path']: r['sha'] for r in tree['tree'] if r['type'] == 'blob'}
    sources, bodies = {}, {}

    def routine(relative, name):
        path = ROOT / '.local/class-skills/d2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == blobs[relative]
        sources[str(path.relative_to(ROOT))] = sha(path)
        text = raw.decode()
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end = text.index('{', match.start()) + 1
        depth = 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[match.start():end]
        bodies[name] = hashlib.sha256(body.encode()).hexdigest()
        return body

    scaffold = ROOT / 'tools/probe_howl_reference.py'
    prefix = next(n.value for n in ast.walk(ast.parse(scaffold.read_text()))
        if isinstance(n, ast.Constant) and isinstance(n.value, str) and n.value.startswith('#include <algorithm>'))
    stub = 'void AITACTICS_WalkCloseToUnit(D2GameStrc*,D2UnitStrc*,unsigned){fallback++;}'
    assert prefix.count(stub) == 1
    prefix = prefix.replace(stub, 'int32_t AITACTICS_WalkCloseToUnit(D2GameStrc*,D2UnitStrc*,int32_t);')
    cpp = prefix + r'''
int chance=0,chanceCalls=0,branch=0,small=0,flipX=0,flipY=0,rawIndex=0,limitedCalls=0,limitArg=-1;
int moved=0,endX=0,endY=0,moveMode=-1,moveStep=-1,moveFlags=-1,moveResult=1;
int AI_RollPercentage(D2UnitStrc*){chanceCalls++;return chance;}
int ITEMS_RollRandomNumber(int*){int n=rawIndex++;return n==0?branch:n==1?flipX:flipY;}
int ITEMS_RollLimitedRandomNumber(int*,int bound){limitedCalls++;limitArg=bound;return small;}
int AITACTICS_MoveToTarget(D2GameStrc*,D2UnitStrc*,D2UnitStrc* target,int mode,int x,int y,uint8_t step,uint8_t flags){
 if(target)std::abort();moved++;endX=x;endY=y;moveMode=mode;moveStep=step;moveFlags=flags;return moveResult;}
'''
    cpp += routine('source/D2Game/src/AI/AiTactics.cpp', 'AITACTICS_WalkCloseToUnit') + '\n'
    cpp += routine('source/D2Game/src/AI/AiThink.cpp', 'D2GAME_AI_SpecialState10_17_6FCE7CF0') + r'''
int main(){D2UnitStrc unit,target;unit.x=100;unit.y=200;D2AiControlStrc control;D2AiTickParamStrc tick;tick.pAiControl=&control;tick.pMonstatsTxt=&monster;tick.pTarget=&target;
bool first=true;printf("[");
for(int combat:{0,1})for(int cls:{-1,1})for(int mon:{0,1})for(int mode:{0,1})for(int interact:{0,1})for(int attackMode:{0,1})for(int roll:{0,19,20,99})
for(int axis:{0,1})for(int offset:{0,1,2})for(int xsign:{0,1})for(int ysign:{0,1})for(int success:{0,1}){
 Reset();chanceCalls=rawIndex=limitedCalls=moved=0;limitArg=moveMode=moveStep=moveFlags=-1;endX=endY=0;
 tick.bCombat=combat;unit.dwClassId=cls;hasMon=mon;hasModes=mode;monster.dwMonStatsFlags=interact?gdwBitMasks[MONSTATSFLAGINDEX_INTERACT]:0;modes.dwModeFlags=attackMode?gdwBitMasks[MONMODE_ATTACK1]:0;
 chance=roll;branch=axis;small=offset;flipX=xsign;flipY=ysign;moveResult=success;
 D2GAME_AI_SpecialState10_17_6FCE7CF0(&game,&unit,&tick);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",combat,cls,mon,mode,interact,attackMode,roll,axis,offset,xsign,ysign,success,attack,neutral,chanceCalls,moved,endX,endY,moveMode,moveStep,moveFlags,rawIndex,limitedCalls,limitArg);first=false;
}printf("]\n");}
'''
    reviewed = {}
    review_bodies = {}
    for relative, names in [
        ('source/D2Game/src/AI/AiThink.cpp', ['AITHINK_GetAiTableRecord', 'D2GAME_MONSTERS_AiFunction03_6FCF0A70']),
        ('source/D2Game/src/AI/AiTactics.cpp', ['sub_6FCCF9D0', 'AITACTICS_Idle', 'AITACTICS_IdleInNeutralMode', 'AITACTICS_MoveToTarget'])]:
        for name in names:
            review_bodies[name] = routine(relative, name)
            reviewed[name] = bodies.pop(name)
    special = review_bodies['AITHINK_GetAiTableRecord'].split('gpSpecialAiStateTable_6FD40290[] =', 1)[1].split('};', 1)[0]
    modes = re.findall(r'\{\s*(\d+),', special)
    assert len(modes) == 18 and modes[10] == '1' and modes[17] == '2'
    mode1 = review_bodies['D2GAME_MONSTERS_AiFunction03_6FCF0A70'].split('case 1:', 1)[1].split('case 2:', 1)[0]
    assert 'sub_6FCCF9D0' in mode1 and 'if (!aiTickParam.pTarget)' in mode1 and 'return;' in mode1
    wrapper = review_bodies['sub_6FCCF9D0']
    assert all(text in wrapper for text in ['sub_6FCF2110', 'nDistance >= 35', 'nDistance >= 25', 'nFrames = nDistance - 10', 'AITACTICS_WalkCloseToUnit(pGame, pUnit, 5u)'])
    STAGE.mkdir(parents=True, exist_ok=True)
    source = STAGE / 'probe.cpp'
    source.write_text(cpp)
    compiled = subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(source), '-o', str(STAGE / 'probe')], capture_output=True)
    (STAGE / 'compiler.log').write_bytes(compiled.stdout + compiled.stderr)
    assert compiled.returncode == 0, compiled.stderr.decode()
    run = subprocess.run([str(STAGE / 'probe')], capture_output=True)
    (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()
    data = json.loads(run.stdout)
    for combat, cls, mon, modes, interact, attack_mode, roll, axis, offset, flip_x, flip_y, success, attacks, idle, calls, moved, x, y, mode, step, flags, raw_calls, limited_calls, bound in data:
        attack = bool(combat and cls >= 0 and not (mon and interact) and modes and attack_mode)
        walk = not attack and roll < 20
        dx, dy = (3, offset) if axis else (offset, 3)
        point = (100 + (-dx if flip_x else dx), 200 + (-dy if flip_y else dy))
        assert (attacks, idle, calls, moved, x, y, mode, step, flags, raw_calls, limited_calls, bound) == (
            int(attack), 10 if not attack and not walk else 0, int(not attack), int(walk),
            *(point if walk else (0, 0)), 2 if walk else -1, 1 if walk else -1, 0 if walk else -1,
            3 if walk else 0, int(walk), 3 if walk else -1)
    assert len(data) == 12288
    cases = STAGE / 'cases.json'
    cases.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    wrapper_prefix = prefix.replace('int32_t AITACTICS_WalkCloseToUnit(D2GameStrc*,D2UnitStrc*,int32_t);',
        'int walkDistance=0;int32_t AITACTICS_WalkCloseToUnit(D2GameStrc*,D2UnitStrc*,int32_t d){walkDistance=d;return 1;}')
    wrapper_cpp = wrapper_prefix + r'''
bool present=false,escapeCondition=false,collisionCondition=false;int distanceInput=0;D2UnitStrc candidate;
D2UnitStrc* sub_6FCF2110(D2GameStrc*,D2UnitStrc*,D2AiControlStrc*,int* distance,int* combat){*distance=distanceInput;*combat=0;return present?&candidate:nullptr;}
int sub_6FCF2E70(D2UnitStrc*){return escapeCondition;}
void* UNITS_GetRoom(D2UnitStrc*){return &game;}
int PATH_GetUnitCollisionPattern(D2UnitStrc*){return 1;}
constexpr int COLLIDE_MISSILE=1;
int COLLISION_CheckAnyCollisionWithPattern(void*,int,int,int,int){return collisionCondition;}
'''+review_bodies['sub_6FCCF9D0']+r'''
int main(){D2UnitStrc unit;D2AiControlStrc control;bool first=true;printf("[");
for(int target:{0,1})for(int escape:{0,1})for(int collision:{0,1})for(int row:{0,1})for(int walk:{0,1})for(int distance:{-1,0,24,25,34,35,2147483647}){
 Reset();walkDistance=0;present=target;escapeCondition=escape;collisionCondition=collision;hasModes=row;modes.dwModeFlags=walk?gdwBitMasks[MONMODE_WALK]:0;distanceInput=distance;
 int observedDistance=0,combat=0;auto* result=sub_6FCCF9D0(&game,&unit,&control,&observedDistance,&combat);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",target,escape,collision,row,walk,distance,result!=nullptr,neutral,walkDistance);first=false;
}printf("]\n");}
'''
    wrapper_source = STAGE / 'target-wrapper.cpp'
    wrapper_source.write_text(wrapper_cpp)
    compiled = subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(wrapper_source), '-o', str(STAGE / 'target-wrapper')], capture_output=True)
    (STAGE / 'target-wrapper-compiler.log').write_bytes(compiled.stdout + compiled.stderr)
    assert compiled.returncode == 0, compiled.stderr.decode()
    run = subprocess.run([str(STAGE / 'target-wrapper')], capture_output=True)
    (STAGE / 'target-wrapper-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()
    wrapper_rows = json.loads(run.stdout)
    for target, escape, collision, row, walk, distance, result, neutral, moved in wrapper_rows:
        displace = not target and (escape or collision) and row and walk
        frames = 25 if distance >= 35 else distance - 10 if distance >= 25 else 10
        assert (result, neutral, moved) == (target, frames if not target and not displace else 0, 5 if displace else 0)
    assert len(wrapper_rows) == 224
    wrapper_cases = STAGE / 'target-wrapper-cases.json'
    wrapper_cases.write_text(json.dumps(wrapper_rows, separators=(',', ':')) + '\n')
    combined = ROOT / '.local/class-skills/blind-policy-source-probe.json'
    combined.write_text(json.dumps(dict(tick=data, targetWrapper=wrapper_rows), separators=(',', ':')) + '\n')
    bodies['sub_6FCCF9D0'] = reviewed.pop('sub_6FCCF9D0')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], contexts=len(data), runtimeChanged=False,
        targetWrapperContexts=len(wrapper_rows),
        compiledUnchangedRoutines=bodies, reviewedUnexecutedRoutines=reviewed,
        sourceCallerContracts=dict(dimVisionTargetMode=1,sharedState17TargetMode=2,nullTargetIdleFrames=dict(near=10,middle='distance-10',far=25),nullTargetDisplacementDistance=5),
        inputs={**sources, str(scaffold.relative_to(ROOT)): sha(scaffold), str(Path(__file__).relative_to(ROOT)): sha(Path(__file__))},
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in [source, cases, STAGE / 'compiler.log', STAGE / 'sanitizer.log',
            wrapper_source,wrapper_cases,STAGE/'target-wrapper-compiler.log',STAGE/'target-wrapper-sanitizer.log',combined]},
        established=[
            'Complete blind special-state10/17 tick attacks in combat only with nonnegative class,non-interacting species and A1 mode. This branch consumes no percentage or wandering RNG calls.',
            'Otherwise exactly one percentage call chooses20percent WalkClose(distance3) versus neutral idle10frames. WalkClose executes complete three-raw/one-limited draw chronology,axis/sign offsets,walk mode,step1 and flags0. Failed movement does not add an idle/fallback in this tick body.',
            'The complete target-mode1 acquisition wrapper executes224contexts. Found targets return without its idle/displacement service;missing targets request displacement5only on the supported escape/collision+walk branch,otherwise idle10/distance-10/25frames. No percentage call is introduced by this wrapper.',
            'The probe safely extracts the prior Howl declared service scaffold as data without running its producer or modifying accepted Howl artifacts.'],
        remaining=[
            'AI percentage/raw/limited RNG,table lookup,combat target,mode/path entry and clock/event services remain declared. Current native/source actor seed ownership,acquisition,pending movement,mode callbacks and actual scheduling are not executed here.',
            'The complete mode1 acquisition wrapper now executes224contexts with declared target selection,collision and mode-entry/idle services. Its no-target branch displaces5source units or idles10/distance-10/25frames without calling the blind tick. Full dispatcher/table/idle/mode-entry review remains static-only;state17usesmode2.',
            'Source distance3 is in source coordinates. Native half-subtile rounding/path geometry,move completion/failure scheduling and saved state integration remain implementation work. The source tick does not itself check active blindness;dispatcher/removal chronology remains separate.',
            'No C# change,native acceptance,completed skill/class or deployment. Dim Vision learned casting has its own candidate/checkpoint;this source probe must not be used to certify its unfinished blind tick.'])
    destination = ROOT / 'tools/reference/dim-vision-ai-source-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert destination.read_text() == rendered
    else:
        destination.write_text(rendered)
    print('Blind AI:12288 complete tick/walk-near and224target-wrapper contexts passed ASan/UBSan; world/RNG/scheduler services explicit.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
