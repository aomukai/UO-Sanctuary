#!/usr/bin/env python3
"""Execute complete unchanged older GetResultFlags with explicit world services."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative='source/D2Game/src/UNIT/SUnitDmg.cpp'
    path=BASE/'d2moo-pinned-source'/relative
    raw=path.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
    body=function(raw.decode(),'SUNITDMG_GetResultFlags')
    cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
struct D2GameStrc{}game;
struct D2UnitStrc{int dwUnitType=1,dwAnimMode=0;}attacker,defender;
enum{UNIT_PLAYER=0,UNIT_MONSTER=1,PLRMODE_RUN=3,STATE_UNINTERRUPTABLE=1};
enum{DAMAGERESULTFLAG_SUCCESSFULHIT=1,DAMAGERESULTFLAG_GETHIT=4,DAMAGERESULTFLAG_BLOCK=16,DAMAGERESULTFLAG_DODGE=128,DAMAGERESULTFLAG_AVOID=256,DAMAGERESULTFLAG_WEAPONBLOCK=32768};
enum{BLOCKFLAG_BLOCK=1,BLOCKFLAG_AVOID=2,BLOCKFLAG_DODGE=4,BLOCKFLAG_WEAPONBLOCK=16};
std::vector<int>trace;int hostile,inRange,hit,action,uninterruptible,reach;
bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){trace.push_back(1);return hostile;}
int UNITS_GetMeleeRange(D2UnitStrc*){trace.push_back(2);return reach;}
bool UNITS_IsInMeleeRange(D2UnitStrc*,D2UnitStrc*,int bonus){trace.push_back(100+bonus);return inRange;}
bool SUNITDMG_IsHitSuccessful(D2UnitStrc*,D2UnitStrc*,int percent,int missile){assert(percent==17&&missile==0);trace.push_back(3);return hit;}
int SUNITDMG_ApplyBlockOrDodge(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int avoid,int block){assert(avoid==0&&block==1);trace.push_back(4);return action;}
bool STATES_CheckState(D2UnitStrc*,int state){assert(state==STATE_UNINTERRUPTABLE);trace.push_back(5);return uninterruptible;}
'''+body+r'''
int main(){bool first=true;printf("[");
for(int a:{-1,0,1,2})for(int d:{-1,0,1,2})for(hostile=0;hostile<2;hostile++)for(inRange=0;inRange<2;inRange++)for(hit=0;hit<2;hit++)for(int chosenAction:{0,1,2,4,8,16,31,32})for(uninterruptible=0;uninterruptible<2;uninterruptible++)for(int running:{0,1})for(int offset:{0,2})for(reach=0;reach<2;reach++){
action=chosenAction;attacker.dwUnitType=a;defender.dwUnitType=d;defender.dwAnimMode=running?PLRMODE_RUN:0;trace.clear();
int result=SUNITDMG_GetResultFlags(&game,a<0?nullptr:&attacker,d<0?nullptr:&defender,17,offset);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],%d,[",first?"":",",a,d,hostile,inRange,hit,action,uninterruptible,running,offset,reach,result);first=false;
for(unsigned n=0;n<trace.size();n++)printf("%s%d",n?",":"",trace[n]);printf("]]");}printf("]\n");}
'''
    generated=BASE/'melee-result-source-probe.cpp';generated.write_text(cpp)
    binary=generated.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True)
    (BASE/'melee-result-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
    cases=json.loads(run.stdout);assert len(cases)==16384,len(cases)
    fixture=BASE/'melee-result-source-probe.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceFile=relative,sourceSha256=sha(path),routine='SUNITDMG_GetResultFlags',
        unchangedRoutineSha256=hashlib.sha256(body.encode()).hexdigest(),generatedSourceSha256=sha(generated),cases=len(cases),
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture)),sanitizers='ASan+UBSan exit0;empty runtime stderr',
        scope='Complete older result-flag routine with declared hostility,range,hit/avoidance and state services;exact call order is exported. This does not execute hit arithmetic,block/dodge internals,damage allocation,contact or return and is not a current executable certificate.',
        established=['Only player/monster pairs reach hostility/range. NPC range bonus3/player1 is adjusted only for nonzero range offset. Running player success skips the hit roll but still checks range and avoidance.',
            'Avoid,Dodge,WeaponBlock and Block copy distinct bits. Any nonzero action clears success,including Evade/unknown action flags without copied bits. Successful uninterrupted targets get result4;miss/block does not query that state.'],
        remaining=['Native actor adapter and saved-world regression must corroborate its use. Source type/geometry/stat/RNG/component mapping is separate. HP-before-retaliation and deferred death/event ordering remain unfinished.'])
    target=ROOT/'tools/reference/melee-result-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==text
    else:target.write_text(text)
    print('Melee result flags:16384complete-source contexts and ordered service traces pass ASan+UBSan;actor integration is separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
