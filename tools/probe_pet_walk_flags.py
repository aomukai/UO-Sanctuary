#!/usr/bin/env python3
"""Execute complete older walk tactic against declared mode/path/AI services."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    relative = 'source/D2Game/src/AI/AiTactics.cpp'
    source = BASE / 'd2moo-pinned-source' / relative
    raw = source.read_bytes()
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    body = function(raw.decode(), 'AITACTICS_MoveToTarget')
    cpp = r'''
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr int MONMODE_RUN=15,MONMODE_WALK=2,STATE_DECREPIFY=1,EVENTTYPE_AITHINK=0;
struct D2GameStrc{}game;
struct D2AiControlStrc{int flags=0;}ai;
struct D2UnitStrc{int pSeed=0;void*pDynamicPath=nullptr;}unit,target;
struct D2ModeChangeStrc{D2UnitStrc*pTargetUnit=nullptr;int nX=0,nY=0,mode=0;};
bool slow=false,success=false,hasAi=true;int roll=0,deletes=0,draws=0,idle=0,close=0;
std::vector<int> events;
bool STATES_CheckState(D2UnitStrc*,int){return slow;}
void D2GAME_MONSTERMODE_ResetVelocityParams_6FC62F90(D2UnitStrc*){}
void PATH_SetStepNum(void*,uint8_t){}
void MONSTERMODE_GetModeChangeInfo(D2UnitStrc*,int mode,D2ModeChangeStrc*p){p->mode=mode;}
int D2GAME_ModeChange_6FC65220(D2GameStrc*,D2ModeChangeStrc*,int){return success;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){deletes++;events.push_back(1);}
D2AiControlStrc*AIGENERAL_GetAiControlFromUnit(D2UnitStrc*){return hasAi?&ai:nullptr;}
void AIUTIL_ToggleAiControlFlag(D2AiControlStrc*p,int flag,int){p->flags|=flag;events.push_back(2);}
int ITEMS_RollRandomNumber(int*){draws++;events.push_back(3);return roll;}
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int n){idle=n;events.push_back(4);}
int AITACTICS_WalkCloseToUnit(D2GameStrc*,D2UnitStrc*,int n){close=n;events.push_back(5);return 0;}
'''+body+r'''
int main(){printf("[");bool first=true;
for(int mode:{2,15})for(int chill:{0,1})for(int ok:{0,1})for(int has:{0,1})for(int flags=0;flags<8;flags++)for(int r=0;r<100;r++){
slow=chill;success=ok;hasAi=has;roll=r;deletes=draws=idle=close=ai.flags=0;events.clear();
int result=AITACTICS_MoveToTarget(&game,&unit,&target,mode,0,0,1,flags);
printf("%s[[%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d],[",first?"":",",mode,chill,ok,has,flags,r,result,deletes,ai.flags,draws,idle,close);
for(unsigned i=0;i<events.size();i++)printf("%s%d",i?",":"",events[i]);printf("]]");first=false;
}printf("]\n");}
'''
    generated = BASE / 'pet-walk-flags-source-probe.cpp'
    generated.write_text(cpp)
    binary = generated.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(binary)], check=True)
    process = subprocess.run([str(binary)], capture_output=True)
    sanitizer = BASE / 'pet-walk-flags-sanitizer.log'
    sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-2000:]
    cases = json.loads(process.stdout)
    assert len(cases) == 12800
    for context, result, events in cases:
        mode, slow, ok, has, flags, roll = context
        failed = not ok
        branch = failed and flags & 2 != 0
        expected = [int(ok or branch), int(failed and flags & 4 != 0), 64 if failed and has and flags & 1 else 0,
                    int(branch), 10 if branch and roll >= 70 else 0, 4 if branch and roll < 70 else 0]
        assert result == expected, (context, result, expected)
        ordered = ([1] if expected[1] else []) + ([2] if expected[2] else [])
        if branch:
            ordered += [3, 4 if roll >= 70 else 5]
        assert events == ordered
    output = BASE / 'pet-walk-flags-source-probe.json'
    output.write_bytes(process.stdout)
    paths = [Path(__file__).resolve(), generated, output, sanitizer]
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], contexts=12800, nativeAccepted=False,
        currentExecutableParity=False, sanitizerClean=True, completeRoutine='AITACTICS_MoveToTarget',
        routineSha256=hashlib.sha256(body.encode()).hexdigest(), sourceHashes={relative:sha(source)},
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        declaredServices=['Mode/terrain success, Decrepify lookup, path step, AI control, RNG draw, idle and close-walk are explicit services. Ordered flag dispatch is executed unchanged.'],
        established=['Success bypasses all failure flags. Failure executes delete-think4, AI-control0x401 and70percent close-walk4/30percent idle10from flag2 in that order. Flags are not path types.'],
        remaining=['Native controller/path/vision composition and current executable behavior require separate acceptance.'])
    path = ROOT / 'tools/reference/pet-walk-flags-source-evidence.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Pet walk tactic:12800 complete-source contexts and ordered failure services pass; ASan/UBSan clean.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
