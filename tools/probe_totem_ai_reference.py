#!/usr/bin/env python3
"""Execute complete pinned Totem AI with explicit target/RNG/movement services."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative = 'source/D2Game/src/AI/AiThink.cpp'
    raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    text = raw.decode(); name = 'AITHINK_Fn109_Totem'
    match = re.search(r'^void __fastcall ' + name + r'\([^\n]+\)\s*\n\{', text, re.M)
    assert match
    end, depth = text.index('{', match.start()) + 1, 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}'); end += 1
    body = text[match.start():end]
    preflight = json.loads((ROOT / 'tools/reference/oak-sage-preflight.json').read_text())
    assert hashlib.sha256(body.encode()).hexdigest() == preflight['reviewedRoutineHashes'][name]
    row = preflight['currentRows']['monstats']['oaksage']
    assert all([int(row['aip' + str(i) + suffix]) for i in range(1, 5)] == [20, 30, 30, 20] for suffix in ['', '(N)', '(H)'])
    cpp = r'''#include <cstdint>
#include <cstdio>
#include <vector>
#include <initializer_list>
#define __fastcall
enum{TOTEM_AI_PARAM_WALK_AWAY_CHANCE_PCT,TOTEM_AI_PARAM_IGNORE_TARGET_CHANCE_PCT,TOTEM_AI_PARAM_TELEPORT_TO_OWNER_DISTANCE,TOTEM_AI_PARAM_FOLLOW_OWNER_DISTANCE};
constexpr int PLRMODE_WALK=2,PLRMODE_RUN=3,PLRMODE_TOWNWALK=6;
struct D2GameStrc{}game;struct D2ActiveRoomStrc{}room;
struct D2AiControlStrc{}control;struct D2AiTickParamStrc{D2AiControlStrc*pAiControl=&control;}ai;
struct D2UnitStrc{int id,dwAnimMode=1;}actor{1},owner{2},target{3};
int hasOwner=1,hasTarget=1,combat=0,ownerDistance=0,escapeResult=0,teleportResult=0,moveResult=0,commonResult=0,random0=0,random1=0;
struct Request{int kind,a=0,b=0,c=0,d=0;};std::vector<Request>calls;
D2UnitStrc*AIGENERAL_GetMinionOwner(D2UnitStrc*){return hasOwner?&owner:nullptr;}
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int frames){calls.push_back({0,frames});}
D2UnitStrc*sub_6FCCFD40(D2GameStrc*,D2UnitStrc*,D2AiControlStrc*,int*distance,int*fighting,int scan){calls.push_back({1,scan});*distance=9;*fighting=combat;return hasTarget?&target:nullptr;}
bool AIRollChanceParam(D2GameStrc*,D2UnitStrc*,D2AiTickParamStrc*,int index){int roll=index==0?random0:random1;calls.push_back({2,index,roll});return roll<(index==0?20:30);}
bool D2GAME_AICORE_Escape_6FCD0560(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,unsigned distance,int flag){calls.push_back({3,t->id,int(distance),flag});return escapeResult;}
int UNITS_GetDistanceToOtherUnit(D2UnitStrc*,D2UnitStrc*){calls.push_back({4});return ownerDistance;}
int AI_GetParamValue(D2GameStrc*,D2AiTickParamStrc*,int index){return index==2?30:20;}
D2ActiveRoomStrc*UNITS_GetRoom(D2UnitStrc*){return &room;}int CLIENTS_GetUnitX(D2UnitStrc*){return 40;}int CLIENTS_GetUnitY(D2UnitStrc*){return 70;}
bool sub_6FCBDFE0(D2GameStrc*,D2UnitStrc*,D2ActiveRoomStrc*,int x,int y,int a,int b){calls.push_back({5,x,y,a,b});return teleportResult;}
bool D2GAME_AI_PetMove_6FCE2BA0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int mode,int run,int speed,int leash){calls.push_back({6,mode,run,speed,leash});return moveResult;}
bool sub_6FCE34E0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,D2UnitStrc*,int fighting,D2AiTickParamStrc*,int flag,int leash){calls.push_back({7,t?t->id:0,fighting,flag,leash});return commonResult;}
''' + body + r'''
bool first=true;
void emit(){calls.clear();AITHINK_Fn109_Totem(&game,&actor,&ai);if(!first)printf(",");first=false;
printf("[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],[",hasOwner,hasTarget,combat,ownerDistance,owner.dwAnimMode,random0,random1,escapeResult,teleportResult,moveResult,commonResult);bool start=true;
for(auto c:calls){if(!start)printf(",");start=false;printf("[%d,%d,%d,%d,%d]",c.kind,c.a,c.b,c.c,c.d);}printf("]]");}
int main(){printf("[");hasOwner=0;emit();hasOwner=1;
for(int distance:{19,20,21,30,31})for(int mode:{1,2,3,6})for(int t:{0,1})for(int b:{0,1})for(int r0:{19,20})for(int r1:{29,30})for(int e:{0,1})for(int warp:{0,1})for(int move:{0,1})for(int common:{0,1}){
ownerDistance=distance;owner.dwAnimMode=mode;hasTarget=t;combat=b;random0=r0;random1=r1;escapeResult=e;teleportResult=warp;moveResult=move;commonResult=common;emit();
}printf("]\n");}
'''
    path = STAGE / 'totem-ai-source-probe.cpp'; path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True, check=True)
    assert not result.stderr, result.stderr.decode()
    rows = json.loads(result.stdout); assert len(rows) == 5121
    for context, calls in rows:
        own, target, combat, distance, mode, r0, r1, escape, warp, move, common = context
        if not own:
            assert calls == [[0, 10, 0, 0, 0]]
            continue
        expected = [[1, 24, 0, 0, 0]]
        if combat and target:
            expected.append([2, 0, r0, 0, 0])
            if r0 < 20:
                expected.append([3, 3, 6, 1, 0])
                if escape:
                    assert calls == expected
                    continue
        expected += [[2, 1, r1, 0, 0], [4, 0, 0, 0, 0]]
        if distance > 30:
            expected.append([5, 40, 70, 0, 0])
            if warp:
                assert calls == expected + [[0, 25, 0, 0, 0]]
                continue
        if distance > 20 and mode in [2, 3, 6]:
            expected.append([6, 0, 0, 60 if mode == 3 else 0, 0])
            if move:
                assert calls == expected
                continue
        expected.append([7, 3 if target and r1 >= 30 else 0, combat if r1 >= 30 else 0, 0, 6])
        if not common:
            expected.append([0, 25, 0, 0, 0])
        assert calls == expected, (context, calls, expected)
    fixture = STAGE / 'totem-ai-source-probe.json'; fixture.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourcePath=relative,
        sourceSha256=hashlib.sha256(raw).hexdigest(), unchangedRoutineSha256=hashlib.sha256(body.encode()).hexdigest(),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(rows), generatedSourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(), sanitizers='ASan+UBSan'),
        established=[
            '5121complete Totem decision contexts retain lazy escape chance20percent,then ignore-target chance30percent,strict owner-distance>30teleport with25frame idle on success,strict>20walking/townwalk/run following,shared common move and25frame fallback.',
            'No owner idles10frames. Threat scan requests24. The first RNG service is skipped without a combat target; successful escape returns before the ignore roll. Ignore clears both target and combat. Owner distance is requested after both chance branches.',
            'Run follow passes60speed,walk/townwalk0; both use PetMove mode0. Failed movement continues to common move with flag0/leash6. Source town behavior is delegated rather than guessed from an explicit branch in this routine.'
        ], services=[
            'The complete outer routine executes unchanged. Target scan,random percentage draws,owner distance,room teleport,escape and shared movement are recorded services; no world geometry or modern RNG/event ordering is claimed.',
            'All three current Oak difficulty parameter columns agree. The prepared stationary Oak body does not yet consume this policy; native motion/controller integration and acceptance remain next work.'
        ])
    output = ROOT / 'tools/reference/totem-ai-engine-evidence.json'; rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Totem AI:5121complete outer decision contexts pass ASan/UBSan; native movement/controller integration remains unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
