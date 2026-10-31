#!/usr/bin/env python3
"""Execute complete legacy periodic clock/install/monster callback bodies."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'periodic-skill-clock-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(parents=True, exist_ok=True)
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative = 'source/D2Game/src/SKILLS/Skills.cpp'
    path = BASE / 'd2moo-pinned-source' / relative
    raw = path.read_bytes()
    blob = hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest()
    assert blob == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    reference = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((reference / 'sources.json').read_text())
    skills = reference / 'json/skills.json'
    assert sha(skills) == manifest['files']['json/skills.json']['sha256']
    row = json.loads(skills.read_text())['102']
    assert row['srvdofunc'] == 66 and row['perdelay'] == 50
    names = ['SKILL_ComputePeriodicRate', 'sub_6FD13260', 'D2GAME_MONSTERS_AiFunction09_6FD13470']
    bodies = {name: function(raw.decode(), name) for name in names}
    mode_relative = 'source/D2Game/src/MONSTER/MonsterMode.cpp'
    mode_path = BASE / 'd2moo-pinned-source' / mode_relative
    mode_raw = mode_path.read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(mode_raw)).encode() + b'\0' + mode_raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == mode_relative)
    dispatch = function(mode_raw.decode(), 'MONSTERMODE_EventHandler')
    assert dispatch.index('case EVENTTYPE_PERIODICSKILLS:') < dispatch.index('default:') < dispatch.index('STATE_FREEZE')
    cpp = r'''#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int EVENTTYPE_PERIODICSKILLS=8,STAT_MODIFIERLIST_LEVEL=1,STAT_MODIFIERLIST_SKILL=2;
constexpr uint32_t gdwBitMasks[]={1,2,4,8,16,32,64};
struct D2GameStrc{int dwGameFrame=0;} game;
struct D2SkillStrc{} skill;
struct D2StatListStrc{} statlist;
struct D2UnitStrc{} actor;
struct D2SkillsTxt{uint32_t dwFlags[1]={};int dwPerDelay=0,nAuraState=1;} row;
struct Tables{int nStatesTxtRecordCount=600;} tables;
auto*sgptDataTables=&tables;
int delay=50,rank=7,mask=127,handlerResult=1,deletes=0,sets=0,handlers=0,nextFrame=0,eventId=-999,eventRank=-999,order=0;
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){assert(id==102);return mask&1?&row:nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int id,int level){assert(id==102&&level>0);return delay;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int event,int){assert(event==8);deletes++;order=order*10+2;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int event,int frame,int id,int level){assert(event==8);sets++;nextFrame=frame;eventId=id;eventRank=level;order=order*10+3;}
D2SkillStrc*UNITS_GetRightSkill(D2UnitStrc*){return mask&2?&skill:nullptr;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc*s,const char*,int){assert(s==&skill);return 102;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*s,int){assert(s==&skill);return rank;}
bool SUNIT_IsDead(D2UnitStrc*){return mask&4;}
int D2GAME_SKILLS_Handler_6FD12BA0(D2GameStrc*,D2UnitStrc*,int id,int level,int a,int b,int c){assert(id==102&&level>0&&a==1&&b==0&&c==0);handlers++;order=order*10+1;return handlerResult;}
bool STATES_CheckState(D2UnitStrc*,int){return mask&8;}
D2StatListStrc*STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return mask&16?&statlist:nullptr;}
int STATLIST_GetStatValue(D2StatListStrc*,int stat,int){return stat==STAT_MODIFIERLIST_LEVEL?13:mask&64?102:103;}
D2SkillStrc*SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return mask&32?&skill:nullptr;}
'''
    cpp += '\n'.join(bodies.values()) + '\n'
    cpp += r'''void reset(){deletes=sets=handlers=nextFrame=order=0;eventId=eventRank=-999;}
bool first=true;
void emit(int mode,int flags,int a5,int state,int arg,int result){
 printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d]]",first?"":",",mode,game.dwGameFrame,delay,mask&1,flags,rank,a5,mask,state,arg,handlerResult,result,deletes,sets,handlers,nextFrame,eventId,eventRank,order);first=false;
}
int main(){printf("[");
for(game.dwGameFrame=0;game.dwGameFrame<=1000;game.dwGameFrame++)for(int d:{-10,0,1,5,6,25,50,100}){
 delay=d;mask=127;rank=7;row.dwFlags[0]=32;row.nAuraState=1;handlerResult=1;reset();emit(0,32,0,1,0,SKILL_ComputePeriodicRate(&game,&actor,&row,102,rank));}
for(int frame:{0,49,50,999})for(int d:{-1,5,50})for(int present:{0,1})for(int flags:{0,32,64,96})for(int r:{1,10})for(int a5:{0,1}){
 game.dwGameFrame=frame;delay=d;mask=126+present;rank=r;row.dwFlags[0]=flags;row.nAuraState=1;handlerResult=1;reset();int result=sub_6FD13260(&game,&actor,102,rank,a5);emit(1,flags,a5,1,0,result);}
for(int frame:{0,49})for(int arg:{-2,-1,0,102})for(int flags:{0,32,64,96})for(int m=0;m<128;m++)for(int state:{-1,1,600})for(int result:{0,1}){
 game.dwGameFrame=frame;delay=50;mask=m;rank=7;row.dwFlags[0]=flags;row.nAuraState=state;handlerResult=result;reset();D2GAME_MONSTERS_AiFunction09_6FD13470(&game,&actor,arg,0);emit(2,flags,0,state,arg,0);}
printf("]\n");}
'''
    source = STAGE / 'probe.cpp'
    source.write_text(cpp)
    result = subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(source), '-o', str(STAGE / 'probe')], capture_output=True)
    (STAGE / 'compiler.log').write_bytes(result.stdout + result.stderr)
    assert result.returncode == 0, result.stderr.decode()
    result = subprocess.run([str(STAGE / 'probe')], capture_output=True)
    (STAGE / 'sanitizer.log').write_bytes(result.stderr)
    assert result.returncode == 0 and not result.stderr, result.stderr.decode()
    cases = json.loads(result.stdout)
    counts = [0, 0, 0]
    for context, got in cases:
        mode, frame, delay, present, flags, rank, selected, mask, state, arg, handler_result = context
        counts[mode] += 1
        want = [0, 0, 0, 0, 0, -999, -999, 0]
        due = frame + max(5, delay)
        if mode == 0:
            want[0] = due
        elif mode == 1:
            if present and flags & 96:
                want = [1, 1, 1, 0, due, -1 if selected else 102, 0 if selected else rank, 23]
        elif arg == -1:
            if not mask & 2 or not (present and flags & 32 and not mask & 4):
                want[1], want[7] = 1, 2
            else:
                want = [0, 1, 1, 1, due, -1, 0, 123]
        elif arg > 0:
            if not (present and 0 <= state < 600 and mask & 8 and mask & 16 and mask & 32 and mask & 64):
                want[1], want[7] = 1, 2
            elif flags & 96:
                want = [0, 1, 1, 1, due, 102, 13, 123]
            else:
                want[3], want[7] = 1, 1
        assert got == want, (context, got, want)
    assert counts == [8008, 384, 24576], counts
    fixture = STAGE / 'cases.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    files = [Path(__file__).resolve(), ROOT / 'tools/review_weapon_event_order.py', path, mode_path, skills, source, fixture, STAGE / 'compiler.log', STAGE / 'sanitizer.log']
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], component='Shared periodic skill clock, installation and complete monster callback', runtimeChanged=False,
        verified=dict(clockCases=8008, installCases=384, monsterCallbackCases=24576, totalCases=sum(counts), addressUndefinedSanitizers='clean'),
        sourceBodies={name: hashlib.sha256(body.encode()).hexdigest() for name, body in bodies.items()},
        staticReviewedBodies={'MONSTERMODE_EventHandler': hashlib.sha256(dispatch.encode()).hexdigest()},
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in files},
        established=['Complete clock body returns frame+max(5,delay) throughout the bounded nonoverflow domain; it is not a global frame-bucket alignment.',
            'Complete installer replaces the selected or explicitly identified periodic event with distinct event id/rank payloads; missing rows/flags refuse without mutation.',
            'Complete monster callback distinguishes selected aura from state-list execution. Selected aura requires an active right skill, aura flag and living actor. The explicit skill branch requires valid active state/stat list/highest skill/matching modifier id and forwards stored rank.',
            'Eligible handlers execute before rescheduling; a supplied handler return of zero does not by itself suppress renewal. The positive-state branch does not add the selected-aura dead check. These are literal bounded old-source paths, not claims of modern reachability.'],
        declaredServices=['Formula result, row/flag suppliers, right/highest skill and state/stat-list queries, alive test, handler result and event queue storage are explicit deterministic services. Actual skill damage, current frame origin, initial summon aura activation, allocator and full event priority remain outside this component.'],
        staticReview=['The complete pinned monster event dispatcher lists periodic skills among the cases exempt from its ordinary frozen-unit suppression. This dispatcher was read and hashed, not executed by the three-body harness.'],
        remaining=['Current executable equivalence, initial Fire aura activation/first deadline, periodic-versus-AI/animation event priority, dynamic state/rank changes and modern owner/offline lifecycle remain unverified.',
            'Extreme signed-product overflow and negative global frames are outside the executed domain. No Fire world rollout, changed C#/data/native build, deployment, completed class or final-art claim.'])
    output = ROOT / 'tools/reference/periodic-skill-clock-source-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Periodic skill source:8008clock/384install/24576complete monster callback contexts pass;ASan/UBSan clean;initial activation/event priority remain open.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
