#!/usr/bin/env python3
"""Execute complete corpse-vine AI111 with declared world/movement services."""
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
    preflight_path = ROOT / 'tools/reference/recycler-vines-preflight.json'
    preflight = json.loads(preflight_path.read_text())
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == preflight['legacyCommit']
    relative = 'source/D2Game/src/AI/AiThink.cpp'
    raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    bodies = {name: function(raw.decode(), name) for name in ['D2GAME_AI_Unk_110_111_6FCECC40', 'AITHINK_Fn111_CycleOfLife']}
    routines = {name: hashlib.sha256(body.encode()).hexdigest() for name, body in bodies.items()}
    assert all(preflight['completeRoutineHashes'][name] == digest for name, digest in routines.items())
    cpp = r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
enum{CYCLEOFLIFE_AI_PARAM_TARGET_SKILL_DELAY,CYCLEOFLIFE_AI_PARAM_TARGET_SIGHT_DISTANCE,CYCLEOFLIFE_AI_PARAM_STALL_DURATION,CYCLEOFLIFE_AI_PARAM_WALK_AWAY_DISTANCE,CYCLEOFLIFE_AI_PARAM_MOVE_BACK_TO_OWNER_DISTANCE};
constexpr int MONSTER_CYCLEOFLIFE=426,MONSTER_VINECREATURE=427,STAT_HITPOINTS=0,STAT_MANA=1,MONMODE_SKILL1=8;
struct D2GameStrc{int dwGameFrame=0;}game;
struct D2UnitStrc{int dwClassId=0,id=0;bool dead=false;}unit,owner,primary,corpse;
struct D2AiControlStrc{int dwAiParam[2]={};}control;
struct D2MonStatsTxt{int nSkill[1]={307};}monster;
struct D2AiTickParamStrc{D2UnitStrc*pTarget=nullptr;int bCombat=0;D2AiControlStrc*pAiControl=&control;D2MonStatsTxt*pMonstatsTxt=&monster;}ai;
struct D2SkillsTxt{int dwAuraRangeCalc=1;}skill;
struct D2SkillStrc{}innateSkill;
struct Action{int kind,a=0,b=0,c=0,d=0;};std::vector<Action>actions;
int params[5]={50,20,25,10,35};
int hasOwner=1,ownerDistance=0,warp=0,found=1,corpseDistance=19,melee=1,common=0,kind=1,lifeFull=0,manaFull=0,roll=99,record=1,innate=1,radius=10,useResult=1;
D2UnitStrc*AIGENERAL_GetMinionOwner(D2UnitStrc*){return hasOwner?&owner:nullptr;}
int AI_GetParamValue(D2GameStrc*,D2AiTickParamStrc*,int index){return params[index];}
int UNITS_GetDistanceToOtherUnit(D2UnitStrc*a,D2UnitStrc*b){assert(a==&unit);return b==&owner?ownerDistance:corpseDistance;}
int D2GAME_AI_PetMove_6FCE2BA0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int mode,int run,int speed,int flag){actions.push_back({1,mode,run,speed,flag});return warp;}
bool SUNIT_IsDead(D2UnitStrc*p){return p->dead;}
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int){return record?&skill:nullptr;}
D2SkillStrc*SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return innate?&innateSkill:nullptr;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int){return 3;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return radius;}
int D2Clamp(int value,int minimum,int maximum){return std::clamp(value,minimum,maximum);}
D2UnitStrc*SKILLS_FindUseableCorpse(D2GameStrc*,D2UnitStrc*p,D2UnitStrc*q,int range){assert(p==&unit&&q==&owner);actions.push_back({2,range});return found?&corpse:nullptr;}
int UNITS_IsInMeleeRange(D2UnitStrc*p,D2UnitStrc*q,int bonus){assert(p==&unit&&q==&corpse&&bonus==0);return melee;}
int sub_6FCE34E0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,D2UnitStrc*,int inMelee,D2AiTickParamStrc*,int flag,int leash){actions.push_back({3,t?t->id:0,inMelee,flag,leash});return common;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){return (stat==STAT_MANA?manaFull:lifeFull)?100:99;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*){return 100;}
int STATLIST_GetMaxManaFromUnit(D2UnitStrc*){return 100;}
int AITACTICS_UseSkill(D2GameStrc*,D2UnitStrc*,int mode,int id,D2UnitStrc*t,int x,int y){assert(x==0&&y==0);actions.push_back({4,mode,id,t->id});return useResult;}
int AI_RollPercentage(D2UnitStrc*){actions.push_back({8,roll});return roll;}
void D2GAME_AICORE_Escape_6FCD0560(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,int distance,int flag){actions.push_back({5,t->id,distance,flag});}
void AITACTICS_WalkToTargetUnitWithFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,int flag){actions.push_back({6,t->id,flag});}
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int frames){actions.push_back({7,frames});}
''' + '\n'.join(bodies.values()) + r'''
bool first=true;
void emit(int primaryKind,int combat,int elapsed,int previous){
unit.dwClassId=kind==1?MONSTER_CYCLEOFLIFE:kind==2?MONSTER_VINECREATURE:0;unit.id=1;owner.id=2;primary.id=3;corpse.id=4;
primary.dead=primaryKind==2;ai.pTarget=primaryKind?&primary:nullptr;ai.bCombat=combat;control.dwAiParam[0]=777;control.dwAiParam[1]=previous;game.dwGameFrame=previous+elapsed;actions.clear();
D2GAME_AI_Unk_110_111_6FCECC40(&game,&unit,&ai);AITHINK_Fn111_CycleOfLife(&game,&unit,&ai);
int ctx[]={hasOwner,ownerDistance,warp,found,corpseDistance,melee,common,kind,lifeFull,manaFull,roll,record,innate,radius,monster.nSkill[0],useResult,primaryKind,combat,elapsed,previous};
printf("%s[[",first?"":",");first=false;for(int i=0;i<20;i++)printf("%s%d",i?",":"",ctx[i]);printf("],[%d,%d],[",control.dwAiParam[0],control.dwAiParam[1]);bool sep=false;for(auto a:actions){printf("%s[%d,%d,%d,%d,%d]",sep?",":"",a.kind,a.a,a.b,a.c,a.d);sep=true;}printf("]]");
}
int main(){printf("[");hasOwner=0;emit(1,1,51,1000);hasOwner=1;
for(int distance:{34,35,36})for(int move:{0,1})for(int handled:{0,1})for(int corpseFound:{0,1}){ownerDistance=distance;warp=move;common=handled;found=corpseFound;emit(1,1,51,1000);}
ownerDistance=0;warp=common=0;
for(int corpseFound:{0,1})for(int distance:{19,20})for(int inMelee:{0,1})for(int type:{0,1,2})for(int hp:{0,1})for(int mp:{0,1})for(int elapsed:{49,50,51})for(int p:{0,1,2})for(int combat:{0,1})for(int chance:{24,25})for(int result:{0,1}){
found=corpseFound;corpseDistance=distance;melee=inMelee;kind=type;lifeFull=hp;manaFull=mp;roll=chance;useResult=result;emit(p,combat,elapsed,1000);}
found=1;corpseDistance=19;melee=1;kind=1;lifeFull=manaFull=0;roll=99;useResult=1;
for(int id:{-1,0,307})for(int valid:{0,1})for(int trained:{0,1})for(int range:{0,10,99})for(int handled:{0,1}){monster.nSkill[0]=id;record=valid;innate=trained;radius=range;common=handled;emit(1,1,51,0);}
printf("]\n");}
'''
    generated = BASE / 'recycler-vines-ai-source-probe.cpp'
    generated.write_text(cpp)
    binary = generated.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(binary)], check=True)
    process = subprocess.run([str(binary)], capture_output=True)
    sanitizer = BASE / 'recycler-vines-ai-sanitizer.log'
    sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-3000:]
    cases = json.loads(process.stdout)
    for ctx, state, actions in cases:
        own, distance, warp, found, corpse_distance, melee, common, kind, hp, mp, roll, record, innate, radius, skill, result, primary, combat, elapsed, previous = ctx
        expected, last = [], previous
        active = bool(own)
        if active and distance >= 35:
            expected.append([1, 3, 0, 0, 6])
            active = not warp
        if active:
            searched = skill > 0 and record and innate
            if searched:
                expected.append([2, max(5, min(radius, 50)), 0, 0, 0])
            corpse = bool(searched and found and corpse_distance < 20)
            in_melee = bool(corpse and melee)
            expected.append([3, 4 if corpse else 0, int(in_melee), 0, 6])
            if not common:
                needs = not (kind == 1 and hp or kind == 2 and mp)
                if corpse and in_melee and needs and elapsed > 50:
                    expected.append([4, 8, skill, 4, 0])
                    last = previous + elapsed
                else:
                    fighting = primary == 1 and combat
                    if fighting:
                        expected.append([8, roll, 0, 0, 0])
                    if fighting and roll < 25:
                        expected.append([5, 3, 10, 0, 0])
                    elif corpse:
                        expected.append([6, 4, 7, 0, 0])
                    else:
                        expected.append([7, 25, 0, 0, 0])
        assert state == [0, last] and actions == expected, (ctx, state, actions, expected)
    assert len(cases) == 7009, len(cases)
    output = BASE / 'recycler-vines-ai-source-probe.json'
    output.write_bytes(process.stdout)
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, nativeAccepted=False, currentExecutableParity=False,
        contexts=len(cases), compiledRoutineHashes=routines, sourceHashes={relative:hashlib.sha256(raw).hexdigest()}, preflightSha256=sha(preflight_path),
        sanitizerClean=True, artifacts={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__).resolve(), generated, output, sanitizer]},
        declaredServices=['Owner and primary-target lookups; corpse finder result and distance/melee geometry.', 'Current fixed AI parameters and scalar aura-range evaluation with complete source clamping.', 'Pet/common movement results, skill dispatch result and ordered action capture.', 'Resource fullness, innate skill lookup and percentage RNG inputs; no game-wide RNG stream or native actor is executed.'],
        established=[
            'Complete initializer and AI111 match the independent action/state oracle. Missing owner returns without a neutral stall. Owner distance>=35 attempts follow before corpse search; successful follow stops processing. Search requires positive innate skill plus valid row/learned skill, clamps radius5..50, drops a corpse at distance>=20 and passes the melee result to common movement.',
            'Relevant full owner life/mana suppresses eating but does not suppress walking to a usable corpse. Actual corpse skill dispatch requires melee and a strictly elapsed>50frame interval. Failed UseSkill return still stores the current frame. Unknown-class controls do not acquire an invented resource-fullness restriction.',
            'Live combat targets permit an independent25percent escape branch when not consuming. Dead primary targets clear combat before that roll. No corpse and no escape gives25frame idle; selected corpse otherwise walks with flag7. These decisions are not an actor-animation or wall-clock certificate.'],
        remaining=['Source finder/usable-corpse/target interaction, full movement/room/order/timers/animation, corpse reservations, delayed resource callbacks and native actors/saves/input remain separate. Shared underground exposure and Solar formula-slot conflict remain explicit; final effects/audio/icons are separately inventoried.'])
    path = ROOT / 'tools/reference/recycler-vines-ai-source-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print(f'Recycler AI111: {len(cases)} bounded decisions, complete initializer/AI, ASan/UBSan clean; native actors remain separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
