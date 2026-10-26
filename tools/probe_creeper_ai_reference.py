#!/usr/bin/env python3
"""Capture complete pinned Vines AI decisions through declared world services."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative = 'source/D2Game/src/AI/AiThink.cpp'
    raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    source = raw.decode()
    blocks = {}
    preflight = json.loads((ROOT / 'tools/reference/creeper-preflight.json').read_text())
    for name in ['D2GAME_AI_Unk_110_111_6FCECC40', 'AITHINK_Fn110_Vines']:
        found = re.search(r'^void __fastcall ' + name + r'\([^\n]+\)\s*\n\{', source, re.M)
        assert found, name
        end, depth = source.index('{', found.start()) + 1, 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}')
            end += 1
        blocks[name] = source[found.start():end]
        assert hashlib.sha256(blocks[name].encode()).hexdigest() == preflight['reviewedRoutineHashes'][name]
    pin = ROOT / 'tools/reference/d2r-named-equipment/json/monstats.json'
    manifest = json.loads((pin.parent.parent / 'sources.json').read_text())
    assert sha(pin) == manifest['files']['json/monstats.json']['sha256']
    row = json.loads(pin.read_text())['plaguepoppy']
    for suffix in ['', '(N)', '(H)']:
        assert [row['aip' + str(i) + suffix] for i in range(1, 6)] == [100, 20, 25, 10, 35]
    assert row['AI'] == 'Vines' and row['Skill1'] == 'Vine Attack' and row['Sk1mode'] == 'S1'
    cpp = r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#include <vector>
#define __fastcall
enum{VINES_AI_PARAM_TARGET_SKILL_DELAY,VINES_AI_PARAM_TARGET_SIGHT_DISTANCE,VINES_AI_PARAM_STALL_DURATION,VINES_AI_PARAM_WALK_AWAY_DISTANCE,VINES_AI_PARAM_MOVE_BACK_TO_OWNER_DISTANCE};
constexpr int STATE_POISON=2,STAT_POISONRESIST=45;
struct D2GameStrc{int dwGameFrame=0;}game;
struct D2UnitStrc{int id;}unit{1},owner{2},target{3};struct D2ActiveRoomStrc{}room;
struct D2AiControlStrc{int dwAiParam[2]={};}control;
struct D2MonStatsTxt{int nSkill[1]={294},nSkillMode[1]={8};}monster;
struct D2AiTickParamStrc{D2AiControlStrc*pAiControl=&control;D2MonStatsTxt*pMonstatsTxt=&monster;}ai;
int params[5]={100,20,25,10,35};
int hasOwner=1,ownerDistance=0,warp=0,town=0,common=0,targetKind=1,combat=1,poisoned=0,resist=0,useResult=1;
struct Action{int kind,a=0,b=0,c=0,d=0,e=0;};std::vector<Action>actions;
D2UnitStrc*AIGENERAL_GetMinionOwner(D2UnitStrc*){return hasOwner?&owner:nullptr;}
int AI_GetParamValue(D2GameStrc*,D2AiTickParamStrc*,int index){return params[index];}
int UNITS_GetDistanceToOtherUnit(D2UnitStrc*,D2UnitStrc*){return ownerDistance;}
int D2GAME_AI_PetMove_6FCE2BA0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int mode,int run,int speed,int flag){actions.push_back({1,mode,run,speed,flag});return warp;}
D2ActiveRoomStrc*UNITS_GetRoom(D2UnitStrc*){return &room;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return town;}
D2UnitStrc*sub_6FCF2CC0(D2GameStrc*,D2UnitStrc*,int*distance,int*fighting){*distance=targetKind==1?19:targetKind==2?20:0;*fighting=combat;return targetKind?&target:nullptr;}
int sub_6FCE34E0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,D2UnitStrc*,int fighting,D2AiTickParamStrc*,int flag,int leash){actions.push_back({2,t?t->id:0,fighting,flag,leash});return common;}
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int frames){actions.push_back({0,frames});}
bool STATES_CheckState(D2UnitStrc*,int){return poisoned;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return resist;}
void D2GAME_AICORE_Escape_6FCD0560(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,int distance,int flag){actions.push_back({3,t->id,distance,flag});}
void AITACTICS_WalkToTargetUnitWithFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,int flag){actions.push_back({4,t->id,flag});}
int AITACTICS_UseSkill(D2GameStrc*,D2UnitStrc*,int mode,int skill,D2UnitStrc*t,int x,int y){actions.push_back({5,mode,skill,t?t->id:0,x,y});return useResult;}
''' + '\n'.join(blocks.values()) + r'''
bool first=true;
void emit(int elapsed,int previous){
 control.dwAiParam[0]=777;control.dwAiParam[1]=previous;game.dwGameFrame=previous+elapsed;actions.clear();
 D2GAME_AI_Unk_110_111_6FCECC40(&game,&unit,&ai);int initialized=control.dwAiParam[0];
 AITHINK_Fn110_Vines(&game,&unit,&ai);
 if(!first)std::printf(",");first=false;
 std::printf("[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d],[",hasOwner,ownerDistance,warp,town,common,targetKind,combat,poisoned,resist,monster.nSkill[0],elapsed,previous,useResult,initialized,control.dwAiParam[1]);
 bool start=true;for(auto a:actions){if(!start)std::printf(",");start=false;std::printf("[%d,%d,%d,%d,%d,%d]",a.kind,a.a,a.b,a.c,a.d,a.e);}std::printf("]]");
}
int main(){
 std::printf("[");hasOwner=0;emit(101,0);hasOwner=1;
 for(int od:{34,35,36})for(int w:{0,1})for(int t:{0,1})for(int c:{0,1})for(int k:{0,1,2})for(int b:{0,1}){
  ownerDistance=od;warp=w;town=t;common=c;targetKind=k;combat=b;emit(101,0);
 }
 ownerDistance=0;warp=town=common=0;
 for(int k:{0,1,2})for(int b:{0,1})for(int p:{0,1})for(int r:{0,99,100,101})for(int s:{-1,0,294})for(int elapsed:{99,100,101})for(int previous:{0,1000})for(int result:{0,1}){
  targetKind=k;combat=b;poisoned=p;resist=r;monster.nSkill[0]=s;useResult=result;emit(elapsed,previous);
 }
 std::printf("]\n");
}
'''
    path = BASE / 'creeper-ai-source-probe.cpp'
    path.write_text(cpp)
    exe = BASE / 'creeper-ai-source-probe'
    subprocess.run(['g++', '-std=c++17', '-O1', '-g', '-fsanitize=address,undefined', '-fno-omit-frame-pointer', '-no-pie', str(path), '-o', str(exe)], check=True)
    result = subprocess.run([str(exe)], capture_output=True, text=True, check=True)
    assert not result.stderr, result.stderr
    (BASE / 'creeper-ai-sanitizer.log').write_text(result.stderr)
    cases = json.loads(result.stdout)
    assert len(cases) == 1873
    for context, state, actions in cases:
        own, distance, moved, in_town, handled, kind, fighting, poison, resistance, skill, elapsed, previous, success = context
        assert state[0] == 0
        if not own:
            assert state[1] == previous and actions == [[0, 25, 0, 0, 0, 0]]
        if own and distance >= 35 and moved:
            assert state[1] == previous and actions == [[1, 3, 0, 0, 6, 0]]
        casts = [a for a in actions if a[0] == 5]
        if casts:
            assert kind == 1 and not in_town and not handled and not poison and resistance != 100
            assert skill >= 0 and fighting and elapsed > 100
            assert state[1] == previous + elapsed and casts == [[5, 8, skill, 3, 0, 0]]
        else:
            assert state[1] == previous
    # Distinguish exact immunity guard, strict timer, valid zero skill ID and
    # ignored UseSkill return without deriving a replacement AI implementation.
    def decision(resistance, elapsed=101, skill=294, result=1):
        return next((state, actions) for ctx, state, actions in cases
            if ctx == [1, 0, 0, 0, 0, 1, 1, 0, resistance, skill, elapsed, 0, result])
    assert decision(100)[1][-1] == [3, 3, 10, 0, 0, 0]
    assert decision(101)[1][-1] == [5, 8, 294, 3, 0, 0]
    assert decision(0, 100)[1][-1] == [0, 25, 0, 0, 0, 0]
    assert decision(0, skill=0)[1][-1] == [5, 8, 0, 3, 0, 0]
    assert decision(0, result=0) == decision(0, result=1)
    fixture = BASE / 'creeper-ai-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
        sourceHashes={relative: hashlib.sha256(raw).hexdigest()},
        unchangedRoutineHashes={k: hashlib.sha256(v.encode()).hexdigest() for k, v in blocks.items()},
        pinnedMonsterHash=sha(pin), generatedSourceSha256=sha(path),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=len(cases)),
        contextFields=['owner', 'ownerDistance', 'warpResult', 'town', 'commonMoveResult', 'targetKind0none1distance19_2distance20', 'combat', 'alreadyPoisoned', 'poisonResistance', 'skill', 'elapsed', 'previousSkillFrame', 'useSkillResult'],
        actionFields=['kind0idle1petmove2common3escape4walk5skill', 'a', 'b', 'c', 'd', 'e'],
        sanitizers='ASan/UBSan exit0 with empty runtime stderr.',
        established=[
            'The complete initializer resets AiParam0only. The complete Vines AI retains ownerless idle25, owner-distance>=35 mode3pet-move priority, town common movement with null target/combat0and idle25on refusal, and sight cutoff>=20.',
            'After common movement declines, already-poisoned or exactly100poison-resist targets request escape10. Resistance101does not match that literal guard; mitigation/immunity behavior is not inferred from this test.',
            'A non-combat target requests walk flag7. A valid skill ID includes0. Only elapsed>100requests S1skill, and the remembered skill frame updates even if the declared UseSkill service returns0. Exact elapsed100idles25without moving the remembered frame.',
            'All three current difficulty rows share100/20/25/10/35AI parameters. Fixtures preserve complete ordered requests and final remembered skill frame, ready for a native policy comparison.'],
        limits=[
            'Owner/target lookup, distances, town/poison/resistance, common pet movement, escape/path/mode dispatch and skill success are supplied services. This is not actual NPC movement, visibility/attackability, poison-field damage, timer integration or modern executable parity.',
            'Some boundary contexts deliberately vary combat and supplied scan distance independently. They validate literal outer dispatch, not physical reachability of every service combination. Frame arithmetic is bounded/nonnegative and does not prove signed overflow or game-frame wrap.',
            'Current VINE_BEAST state behavior, actual saved actor, formula-driven trail lifetime/ownership, shared vine cap/casting/payment/input and native client acceptance remain pending. Final effects and icons remain separate placeholders.'])
    output = ROOT / 'tools/reference/creeper-ai-source-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print(f'Creeper AI:{len(cases)}complete source contexts; strict delay/immunity and ordered movement/skill requests captured; live actor remains pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
