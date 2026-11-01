#!/usr/bin/env python3
"""Execute the complete older do17 emitter; reuse prior initializer/path evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_player_charged_bolt_reuse import ROOT, SOURCE, block, review

STAGE = ROOT / '.local/class-skills/player-charged-bolt-emitter-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def probe(check=False):
    review(True)
    STAGE.mkdir(exist_ok=True)
    emitter = block(SOURCE.read_text(), 'SKILLS_SrvDo017_ChargedBolt_BoltSentry')
    cpp = r'''
#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr int UNITFLAG_SKSRVDOFUNC=64;
struct D2UnitStrc{int dwFlags=0,x=22,y=38;} owner,created;
struct D2GameStrc{} game;
struct D2SkillsTxt{int dwCalc[4]={0};} row;
struct Tables{int nMissilesTxtRecordCount=1000;} tables;auto* sgptDataTables=&tables;
struct D2MissileStrc{int dwFlags=0;D2UnitStrc* pOwner=nullptr;int nX=0,nY=0,nMissile=0,nSkillLevel=0,nSkill=0;void(*pInitFunc)(D2UnitStrc*,int32_t)=nullptr;int pInitArgs=0,nTargetX=0,nTargetY=0;};
bool hasRow=true,succeeds=true;int mid=56,mode=3,queries=0,formulas=0;
std::vector<D2MissileStrc> packets;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int){return mid;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p?p->x:0;}
int CLIENTS_GetUnitY(D2UnitStrc* p){return p?p->y:0;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int rank){formulas++;return std::min(24,rank+2);}
void SKILLS_MissileInit_ChargedBolt(D2UnitStrc*,int32_t){} // identity-only service; existing45-case proof executes its real body.
int D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc* p,int* x,int* y){int i=queries++;*x=mode==1?0:34+i;*y=mode==2?0:46-i;return p&&mode!=0&&(mode!=4||i%2==1);}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){packets.push_back(*p);return succeeds?&created:nullptr;}
'''
    cpp += emitter + '\n'
    cpp += r'''
int main(){printf("[");bool first=true;
for(int present:{0,1})for(int unit:{0,1})for(int missile:{-1,0,56,999,1000})for(int rank:{1,20,22,255})for(int target:{0,1,2,3,4})for(int success:{0,1}){
hasRow=present;succeeds=success;mid=missile;mode=target;queries=formulas=0;packets.clear();owner.dwFlags=0;
int result=SKILLS_SrvDo017_ChargedBolt_BoltSentry(&game,unit?&owner:nullptr,38,rank);
printf("%s{\"input\":[%d,%d,%d,%d,%d,%d],\"result\":[%d,%d,%d,%d],\"packets\":[",first?"":",",present,unit,missile,rank,target,success,result,owner.dwFlags,queries,formulas);first=false;
for(size_t i=0;i<packets.size();i++){auto p=packets[i];if(p.pInitFunc!=SKILLS_MissileInit_ChargedBolt)return 2;printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",p.dwFlags,p.pOwner==&owner,p.nX,p.nY,p.nMissile,p.nSkill,p.nSkillLevel,p.pInitArgs,p.nTargetX,p.nTargetY);}printf("]}");
}printf("]\n");}
'''
    source = STAGE / 'probe.cpp'; source.write_text(cpp)
    binary = STAGE / 'probe'
    subprocess.run(['g++', '-std=c++20', '-O2', '-fwrapv', str(source), '-o', str(binary)], check=True)
    cases = json.loads(subprocess.check_output([str(binary)]))
    assert len(cases) == 800
    for case in cases:
        present, unit, missile, rank, target, success = case['input']
        valid = present and 0 <= missile < 1000
        count = min(24, rank + 2)
        assert case['result'] == [int(bool(valid)), 64 if unit else 0, count if valid else 0, int(bool(valid))]
        ordinals = [i for i in range(count) if unit and target in (3, 4) and (target != 4 or i % 2 == 1)] if valid else []
        assert case['packets'] == [[33, 1, 22, 38, missile, 38, rank, i, 34+i, 46-i] for i in ordinals]
    sanitized = STAGE / 'probe-san'
    subprocess.run(['g++', '-std=c++20', '-g', '-fwrapv', '-fsanitize=address,undefined', '-fno-omit-frame-pointer', str(source), '-o', str(sanitized)], check=True)
    sanitizer_log = STAGE / 'sanitizer.log'
    with sanitizer_log.open('wb') as log:
        assert json.loads(subprocess.check_output([str(sanitized)], stderr=log)) == cases
    assert not sanitizer_log.read_bytes()
    fixture = STAGE / 'cases.json'; fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    reused = ROOT / 'tools/reference/player-charged-bolt-reuse-review.json'
    result = dict(referenceVersion='3.3.93847', legacyCommit='0c0bbd1e6bef9181a3da1c046523af622a925416',
                  compiledRoutine='SKILLS_SrvDo017_ChargedBolt_BoltSentry', sourceBodySha256=hashlib.sha256(emitter.encode()).hexdigest(), cases=800, sanitizers='ASan/UBSan clean',
                  declaredServices=['Current pinned count formula min(24,rank+2); row/missile bounds and owner coordinates are explicit services.', 'Target service supplies failed/zero/valid/alternating coordinates independently for each ordinal. Factory records attempts and returns success/null. Null-owner coordinate/target behavior is a service declaration, not complete game-engine behavior.', 'Initializer is identity-only here; the unchanged real initializer45 cases and complete charged path2160 cases are reused via the prior review.'],
                  established=['Complete emitter body executes missing-row/missile bounds, owner flags, ranks1/20/22/255, failed/zero/alternating target reads, ordinal/owner/origin/skill/rank/flags33 packets and continuation after creation failure.', 'Count/cap is supplied from the pinned current table; no mana, client input, saved flight, factory RNG/initial update or modern executable acceptance is inferred.'],
                  remaining=['Native player emission/capture, actual payment/input, charged movement/collision and save/reload remain separate work. Global RNG/factory/room/path/coordinate ordering and current executable parity remain open.'],
                  artifacts={str(p.relative_to(ROOT)): sha(p) for p in (Path(__file__).resolve(), SOURCE, reused, source, fixture, binary, sanitized, sanitizer_log)})
    output = ROOT / 'tools/reference/player-charged-bolt-emitter-source.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Player Charged Bolt:800 complete older emitter cases; ASan/UBSan clean; existing initializer45/path2160 proofs reused; native acceptance remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    probe(parser.parse_args().check)
