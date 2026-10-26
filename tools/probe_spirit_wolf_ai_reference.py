#!/usr/bin/env python3
"""Capture complete legacy Spirit Wolf AI requests with explicit world services."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative = 'source/D2Game/src/AI/AiThink.cpp'; raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    source = raw.decode(); name = 'AITHINK_Fn108_SpiritWolf'
    match = re.search(r'^void __fastcall ' + name + r'\([^\n]+\)\s*\n\{', source, re.M); assert match
    end, depth = source.index('{', match.start()) + 1, 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}'); end += 1
    body = source[match.start():end]
    pin = ROOT / 'tools/reference/d2r-named-equipment/json/monstats.json'
    manifest = json.loads((pin.parent.parent / 'sources.json').read_text())
    assert hashlib.sha256(pin.read_bytes()).hexdigest() == manifest['files']['json/monstats.json']['sha256']
    row = json.loads(pin.read_text())['spiritwolf']
    assert [row['aip' + str(i)] for i in range(1, 6)] == [22, 20, 14, 20, 26]
    assert row['Skill1'] == 'Teleport 2' and row['Sk1mode'] == 'S1' and row['AI'] == 'DruidWolf'
    cpp = r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#include <vector>
#define __fastcall
constexpr int UNIT_PLAYER=0,PLRMODE_WALK=2,PLRMODE_TOWNWALK=6,PLRMODE_RUN=3,EVENTTYPE_MODECHANGE=1,MONMODE_ATTACK1=4;
enum{DRUIDWOLF_AI_PARAM_STALL_DURATION,DRUIDWOLF_AI_PARAM_APPROACH_CHANCE_PCT,DRUIDWOLF_SPIRITWOLF_AI_PARAM_FOLLOW_OWNER_DISTANCE,DRUIDWOLF_AI_PARAM_MAX_TARGET_DISTANCE,DRUIDWOLF_AI_PARAM_MAX_OWNER_DISTANCE};
struct D2GameStrc{int dwGameFrame=1000;}game;struct D2ActiveRoomStrc{}room;
struct D2UnitStrc{int dwUnitId=17,dwAnimMode=1;}unit,owner,primary,potential;
struct D2AiControlStrc{int dwAiParam[3]={};}control;
struct D2MonStatsTxt{int nVelocity=5,nRun=9,nSkill[1]={292},nSkillMode[1]={8};}monster;
struct D2AiTickParamStrc{D2AiControlStrc*pAiControl=&control;D2MonStatsTxt*pMonstatsTxt=&monster;}ai;
int params[5]={22,20,14,20,26};
bool hasOwner=true,rememberedOwner=true,town=false,hasPrimary=true,hasPotential=false,primaryVisible=true,potentialVisible=true,petMove=false,commonMove=false,combat=false;
int ownerDistance=0,potentialDistance=19,chanceRoll=99,rolls=0;
struct Action{int kind,a=0,b=0,c=0,d=0;};std::vector<Action>actions;
D2UnitStrc* AIGENERAL_GetMinionOwner(D2UnitStrc*){return hasOwner?&owner:nullptr;}
D2UnitStrc* SUNIT_GetServerUnit(D2GameStrc*,int,int){return rememberedOwner?&owner:nullptr;}
int CLIENTS_GetUnitX(D2UnitStrc*){return 40;}int CLIENTS_GetUnitY(D2UnitStrc*){return 70;}
void AITACTICS_UseSkill(D2GameStrc*,D2UnitStrc*,int mode,int skill,int,int x,int y){actions.push_back({2,mode,skill,x,y});}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int frame,int,int){actions.push_back({3,frame-1000});}
void AITACTICS_Idle(D2GameStrc*,D2UnitStrc*,int frames){actions.push_back({4,frames});}
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int frames){actions.push_back({0,frames});}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return town;}
int sub_6FCE34E0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*target,D2UnitStrc*,int fighting,D2AiTickParamStrc*,int flag,int leash){actions.push_back({1,target?target->dwUnitId:0,fighting,flag,leash});return commonMove;}
int AI_GetParamValue(D2GameStrc*,D2AiTickParamStrc*,int index){return params[index];}
D2UnitStrc* sub_6FCCFD40(D2GameStrc*,D2UnitStrc*,D2AiControlStrc*,int*,int* fighting,int){*fighting=combat;return hasPrimary?&primary:nullptr;}
D2UnitStrc* sub_6FCF2110(D2GameStrc*,D2UnitStrc*,D2AiControlStrc*,int*,int*){return hasPotential?&potential:nullptr;}
int sub_6FCF14D0(D2UnitStrc*,D2UnitStrc*target){return target==&primary?primaryVisible:potentialVisible;}
int AIUTIL_GetDistanceToCoordinates_FullUnitSize(D2UnitStrc*,D2UnitStrc*){return potentialDistance;}
int UNITS_GetDistanceToOtherUnit(D2UnitStrc*,D2UnitStrc*){return ownerDistance;}
int D2GAME_AI_PetMove_6FCE2BA0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int mode,int run,int speed,int flag){actions.push_back({5,mode,run,speed,flag});return petMove;}
void AITACTICS_ChangeModeAndTargetUnit(D2GameStrc*,D2UnitStrc*,int mode,D2UnitStrc*target){actions.push_back({6,mode,target?target->dwUnitId:0});}
void AITACTICS_WalkInRadiusToTarget(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int first,int second){actions.push_back({7,first,second});}
void AITACTICS_SetVelocity(D2UnitStrc*,int first,int second,int third){actions.push_back({8,first,second,third});}
void AITACTICS_RunToTargetUnit(D2GameStrc*,D2UnitStrc*,D2UnitStrc*target){actions.push_back({9,target?target->dwUnitId:0});}
bool AIRollChanceParam(D2GameStrc*,D2UnitStrc*,D2AiTickParamStrc*,int parameter){rolls++;return chanceRoll<params[parameter];}
void AITACTICS_WalkCloseToUnit(D2GameStrc*,D2UnitStrc*,unsigned distance){actions.push_back({10,(int)distance});}
''' + body + r'''
bool first=true;
void emit(int family,int own,int stored,int lookup,int inTown,int p,int q,int pVis,int qVis,int distance,int ownDistance,int mode,int moved,int common,int roll,int teleport,int fighting){
hasOwner=own;rememberedOwner=lookup;town=inTown;hasPrimary=p;hasPotential=q;primaryVisible=pVis;potentialVisible=qVis;potentialDistance=distance;ownerDistance=ownDistance;owner.dwAnimMode=mode;petMove=moved;commonMove=common;chanceRoll=roll;monster.nSkill[0]=teleport?292:-1;combat=fighting;control={};control.dwAiParam[2]=stored;owner.dwUnitId=17;primary.dwUnitId=10;potential.dwUnitId=20;actions.clear();rolls=0;
AITHINK_Fn108_SpiritWolf(&game,&unit,&ai);
printf("%s[%d,[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],%d,%d,[",first?"":",",family,own,stored,lookup,inTown,p,q,pVis,qVis,distance,ownDistance,mode,moved,common,roll,teleport,fighting,control.dwAiParam[2],rolls);first=false;bool firstAction=true;
for(auto&a:actions){printf("%s[%d,%d,%d,%d,%d]",firstAction?"":",",a.kind,a.a,a.b,a.c,a.d);firstAction=false;}printf("]]");
}
int main(){printf("[");
for(int stored:{0,17})for(int lookup:{0,1})for(int skill:{0,1})emit(0,0,stored,lookup,0,1,1,1,1,19,0,1,0,0,99,skill,0);
for(int common:{0,1})emit(1,1,0,0,1,1,1,1,1,19,0,1,0,common,99,1,0);
for(int p:{0,1})for(int q:{0,1})for(int pVis:{0,1})for(int qVis:{0,1})for(int distance:{19,20})for(int common:{0,1})for(int chance:{0,99})for(int fighting:{0,1})emit(2,1,0,1,0,p,q,pVis,qVis,distance,0,1,0,common,chance,1,fighting);
for(int distance:{14,15,26,27,50,51})for(int mode:{1,2,3,6})for(int moved:{0,1})for(int common:{0,1})for(int p:{0,1})for(int skill:{0,1})for(int chance:{0,99})for(int fighting:{0,1})emit(3,1,0,1,0,p,1,1,1,19,distance,mode,moved,common,chance,skill,fighting);
printf("]\n");}
'''
    path = BASE / 'spirit-wolf-ai-source-probe.cpp'; path.write_text(cpp); binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True)
    (BASE / 'spirit-wolf-ai-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-4000:]
    cases = json.loads(result.stdout); assert len(cases) == 1802
    for family, context, remembered, rolls, actions in cases:
        own, stored, lookup, in_town, p, q, p_vis, q_vis, distance, own_distance, mode, moved, common, roll, skill, fighting = context
        expected, expected_rolls = [], 0
        def action(kind, *args):
            expected.append([kind, *args, *([0] * (4-len(args)))])
        if not own:
            assert remembered == stored
            if stored and lookup and skill:
                action(2, 8, 292, 40, 70); action(3, 6); action(4, 8)
            else:
                action(0, 10)
        else:
            assert remembered == 17
            if in_town:
                action(1, 0, 0, 0, 6)
                if not common:
                    action(0, 33)
            elif skill and own_distance > 50:
                action(2, 8, 292, 40, 70); action(3, 4); action(4, 10)
            else:
                finish = False
                if own_distance > 26:
                    action(5, 0, 1, 100, 0); finish = bool(moved)
                if not finish and own_distance > 14 and mode in [2, 6, 3]:
                    action(5, 0, int(mode == 3), 100 if mode == 3 else 0, 0); finish = bool(moved)
                if not finish:
                    target = 10 if p and p_vis else 20 if q and distance < 20 and not q_vis else 0
                    action(1, target, fighting, 1, 6)
                    if not common:
                        if target:
                            action(6, 4, target); action(4, 22)
                        else:
                            expected_rolls = 1
                            action(10, 10) if roll < 20 else action(0, 15)
        assert actions == expected and rolls == expected_rolls, (family, context, actions, expected)
    fixture = BASE / 'spirit-wolf-ai-source-probe.json'; fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
                    sourceHashes={relative: hashlib.sha256(raw).hexdigest()}, unchangedRoutineHashes={name: hashlib.sha256(body.encode()).hexdigest()},
                    pinnedMonsterHash=hashlib.sha256(pin.read_bytes()).hexdigest(), generatedSourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(cases)),
                    sanitizers='ASan/UBSan exit0 with empty runtime stderr.',
                    userObservation='Existing Spirit Wolves keep their individual current targets when another wolf is cast on a different monster.',
                    established=[
                        'Complete Spirit Wolf body retains remembered owner ID and distinct ownerless teleport/event+6/idle8, town/common-move/idle33, owner-distance>50 teleport/event+4/idle10 and strict>26/>14 follow/run boundaries. Failed far-follow then failed movement-mode follow can request the same service twice; ordered duplicates are preserved.',
                        'A visible existing primary target wins independently for that wolf. Source fallback requires owner candidate distance<20 and the negated visibility helper. This polarity differs from the primary check and Fenris; it is captured, not silently repaired or asserted modern. User observation supports retaining existing individual targets, not every fallback detail.',
                        'After shared pet movement declines handling, the nonnull-monstats branch requests A1 and22frame stall for a chosen target even when its combat flag is false. With no target, a declared20percent roll chooses walk-close10 versus idle15. These are literal legacy requests, not established modern combat/chase behavior.'
                    ], limits=[
                        'Target lookup, visibility helper, source distances, owner/mode/room, chance draw, common movement and tactics/event functions are declared services. No actual movement, monster mode entry/animation, teleport placement, damage or world event execution occurs here.',
                        'All contexts supply a valid nonnull monstats/AI control record. Full source helper LOS triple-ray behavior, owner-scanned list/order, global RNG, current executable AI and fallback-polarity equivalence remain unverified. Do not use this as a flock focus command.',
                        'Complete common-pet inspection shows a target outside town makes that helper return0until owner distance>80; it does not chase the target. This old SpiritWolf then tests pMonstatsTxt instead of bCombat, making its chase branch unreachable for valid records. The defect is retained here, not approved live AI. Separately pinned7f6d773corrects it to bCombat; see spirit-wolf-corrected-ai-source-evidence.json. Exact current D2R execution remains unverified.',
                        'Prepared saved Spirit Wolf life/ownership and native policies have separate evidence. Actual actor AI/animation/tactics/cold-hit integration, learned cast/payment/caps/input and final assets remain pending.'
                    ])
    path = ROOT / 'tools/reference/spirit-wolf-ai-source-evidence.json'; encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print(f'Spirit Wolf AI:{len(cases)}complete source contexts; ordered follow/teleport/target requests captured, actual world services separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
