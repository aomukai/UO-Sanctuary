#!/usr/bin/env python3
"""Execute both complete older NecroPet AI67 branches with ordered query/tactics services."""
import argparse
import hashlib
import itertools
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative = 'source/D2Game/src/AI/AiThink.cpp'
    path = BASE / 'd2moo-pinned-source' / relative
    raw = path.read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    routines = {name: function(raw.decode(), name) for name in ['sub_6FCE3740', 'AITHINK_Fn067_NecroPet']}
    sentinel_relative = 'source/D2Game/src/SKILLS/SkillNec.cpp'
    sentinel_raw = (BASE / 'd2moo-pinned-source' / sentinel_relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(sentinel_raw)).encode() + b'\0' + sentinel_raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == sentinel_relative)
    sentinel_setup = function(sentinel_raw.decode(), 'D2GAME_SetUnitComponent_6FD0C3A0')
    assert 'AITHINK_SetAiControlParams(pPet->pMonsterData->pAiControl, 1, 0, -666);' in sentinel_setup
    ref = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((ref / 'sources.json').read_text())
    monster_path = ref / 'json/monstats.json'
    assert sha(monster_path) == manifest['files']['json/monstats.json']['sha256']
    iron = json.loads(monster_path.read_text())['irongolem']
    assert [iron[k] for k in ['AI', 'Code', 'Velocity', 'Run']] == ['NecroPet', 'G4', 9, 9]
    cpp = r'''
#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
enum {EVENTTYPE_AITHINK=0,UNIT_PLAYER=0,UNITFLAG_PETIGNORE=1,COLLIDE_MISSILE_BARRIER=1,MONMODE_ATTACK1=4};
struct D2GameStrc{} game;
struct D2UnitStrc{int dwUnitId=0,dwFlags=0;} unit{1},owner{17},primary{10},potential{20};
struct AiControl{int dwAiParam[3];} control;
struct Monstats{int nVelocity,nRun,nSkillMode[1],nSkill[1];} monster;
struct D2AiTickParamStrc{AiControl*pAiControl;Monstats*pMonstatsTxt;} tick{&control,&monster};
struct Action{int k,a=0,b=0,c=0,d=0;};std::vector<Action> actions;
int ctx[20],rollIndex;
void add(int k,int a=0,int b=0,int c=0,int d=0){actions.push_back({k,a,b,c,d});}
int id(D2UnitStrc*u){return u?u->dwUnitId:0;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){add(0);}
D2UnitStrc*AIGENERAL_GetMinionOwner(D2UnitStrc*){add(1);return ctx[0]?&owner:nullptr;}
D2UnitStrc*SUNIT_GetServerUnit(D2GameStrc*,int,int remembered){add(2,remembered);return ctx[2]?&owner:nullptr;}
int AIUTIL_GetDistanceToCoordinates_FullUnitSize(D2UnitStrc*,D2UnitStrc*t){add(3,id(t));return t==&owner?ctx[3]:ctx[7];}
D2UnitStrc*sub_6FCF2110(D2GameStrc*,D2UnitStrc*o,AiControl*,int*d,int*c){add(4,id(o));*d=999;*c=1;return ctx[6]?&potential:nullptr;}
D2UnitStrc*sub_6FCCFD40(D2GameStrc*,D2UnitStrc*,AiControl*,int*d,int*c,int radius){add(5,0,radius);*d=ctx[5];*c=ctx[12];return ctx[4]?&primary:nullptr;}
D2UnitStrc*sub_6FCF2CC0(D2GameStrc*,D2UnitStrc*,int*d,int*c){add(5,1);*d=ctx[5];*c=ctx[12];return ctx[4]?&primary:nullptr;}
bool UNITS_TestCollisionWithUnit(D2UnitStrc*,D2UnitStrc*t,int){add(7,id(t));return ctx[9];}
bool sub_6FCF14D0(D2UnitStrc*,D2UnitStrc*t){add(8,id(t));return ctx[10];}
int AI_RollPercentage(D2UnitStrc*){int roll=ctx[15+rollIndex++];add(9,roll);return roll;}
bool sub_6FCE34E0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,D2UnitStrc*,int combat,D2AiTickParamStrc*,int flag,int leash){add(10,id(t),combat,flag,leash);return ctx[14];}
void*UNITS_GetRoom(D2UnitStrc*){return nullptr;}
bool DUNGEON_IsRoomInTown(void*){add(11);return ctx[13];}
int D2GAME_AI_PetMove_6FCE2BA0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int mode,int run,int velocity,int leash){add(13,mode,run,velocity,leash);return 0;}
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int frames){add(12,frames);}
void AITACTICS_WanderToTarget(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,unsigned radius){add(14,id(t),radius);}
void AITACTICS_ChangeModeAndTargetUnit(D2GameStrc*,D2UnitStrc*,int mode,D2UnitStrc*t){add(15,mode,id(t));}
void AITACTICS_SetVelocity(D2UnitStrc*,int a,int b,int c){add(16,a,b,c);}
void sub_6FCD0410(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,int a){add(17,id(t),a);}
void AITACTICS_UseSkill(D2GameStrc*,D2UnitStrc*,int mode,int skill,D2UnitStrc*t,int,int){add(18,mode,skill,id(t));}
void sub_6FCD0E80(D2GameStrc*,D2UnitStrc*,D2UnitStrc*t,unsigned a,int b){add(19,id(t),a,b);}
'''
    # PETIGNORE is a direct source field read rather than a callback; leave it
    # unchanged and do not invent an observable query in the trace.
    cpp += '\n'.join(routines.values()) + r'''
int main(){
bool first=true;printf("[");
while(scanf("%d",&ctx[0])==1){
for(int i=1;i<20;i++)if(scanf("%d",&ctx[i])!=1)return 2;
control={{ctx[11],88,ctx[1]}};monster={ctx[18],ctx[19],{8},{123}};potential.dwFlags=ctx[8]?UNITFLAG_PETIGNORE:0;
actions.clear();rollIndex=0;AITHINK_Fn067_NecroPet(&game,&unit,&tick);
printf("%s[[",first?"":",");first=false;
for(int i=0;i<20;i++)printf("%s%d",i?",":"",ctx[i]);
printf("],[%d,%d,%d],%d,[",control.dwAiParam[0],control.dwAiParam[1],control.dwAiParam[2],rollIndex);
for(unsigned i=0;i<actions.size();i++){auto a=actions[i];printf("%s[%d,%d,%d,%d,%d]",i?",":"",a.k,a.a,a.b,a.c,a.d);}
printf("]]");}printf("]\n");}
'''
    cases = []
    # owner,remembered,lookup,ownerDist,primary,primaryDist,potential,
    # potentialDist,ignore,barrier,valid,parameter0,combat,town,common,
    # three sequential percentage draws,velocity,run.
    base = [1,0,1,0,1,0,1,19,0,0,1,0,1,0,0,15,79,74,9,9]
    def emit(**changes):
        row = base.copy()
        for index, value in changes.items(): row[int(index[1:])] = value
        cases.append(row)
    for ranged, stored, lookup in itertools.product([0,1], [0,17,-666], [0,1]):
        emit(i0=0, i1=stored, i2=lookup, i11=ranged)
    for ranged in [0,1]: emit(i1=-666, i11=ranged)
    for distance, velocity, run in itertools.product([28,29,50,51], [-1,0,9,10], [0,9,30]):
        emit(i3=distance, i18=velocity, i19=run)
    for ranged, p, q, pd, qd, ignore, barrier, valid in itertools.product(
            [0,1], [0,1], [0,1], [0,6,7,15,16], [19,20,35,36], [0,1], [0,1], [0,1]):
        emit(i11=ranged, i4=p, i6=q, i5=pd, i7=qd, i8=ignore, i9=barrier, i10=valid)
    for ranged, target, town, common, combat, r0, r1, r2 in itertools.product(
            [0,1], [0,1], [0,1], [0,1], [0,1], [14,15], [79,80], [74,75]):
        emit(i11=ranged, i4=target, i6=0, i13=town, i14=common, i12=combat, i15=r0, i16=r1, i17=r2)
    generated = BASE / 'necromancer-pet-ai-source-probe.cpp'
    generated.write_text(cpp)
    binary = generated.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(binary)], check=True)
    process = subprocess.run([str(binary)], input='\n'.join(' '.join(map(str,row)) for row in cases).encode(), capture_output=True)
    (BASE / 'necromancer-pet-ai-sanitizer.log').write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-3000:]
    result = json.loads(process.stdout)
    assert len(result) == len(cases) and [x[0] for x in result] == cases
    for context, state, rolls, actions in result:
        assert state == [context[11],88,17 if context[0] else context[1]]
        assert rolls == sum(x[0] == 9 for x in actions) <= 3
        assert actions[0] == [0,0,0,0,0]
    assert set(range(20)) - {6} == {a[0] for row in result for a in row[3]}
    fixture = BASE / 'necromancer-pet-ai-source-probe.json'
    fixture.write_text(json.dumps(result, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceFile=relative, sourceSha256=sha(path),
        unchangedRoutineHashes={name: hashlib.sha256(body.encode()).hexdigest() for name,body in routines.items()},
        sentinelSetupReview=dict(sourceFile=sentinel_relative, sourceSha256=hashlib.sha256(sentinel_raw).hexdigest(),
            routine='D2GAME_SetUnitComponent_6FD0C3A0', routineSha256=hashlib.sha256(sentinel_setup.encode()).hexdigest(),
            scope='Static review establishes negative remembered-owner sentinel; component setup is not executed by this probe.'),
        monsterTableSha256=sha(monster_path), generatedSourceSha256=sha(generated),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=len(result)),
        sanitizers='ASan+UBSan exit0 with empty runtime stderr', learnedHandlerEnabled=False, movingActorEnabled=False,
        established=[
            'Both complete unchanged older AI67 routines execute with ordered event deletion,owner/remembered lookup,distances,target queries,visibility,common movement,town,percentage draws and tactics requests.',
            'Ordinary melee:owner distance>50 requests mode3;>28 requests mode0 with min(100*run/velocity-100,100),or100 for velocity<=0. Primary range24 survives only at distance<=6;otherwise nonignored owner target requires distance<36 and a separate validity gate.',
            'Ranged branch:primary distance<=15;fallback requires no missile barrier and distance<20. Both retain the primary lookup combat flag through target replacement and use15percent closer-follow selection. Town/no target wanders4. Melee combat requests80percent A1 or idle10;noncombat sets velocity(0,0,12)and approaches7. Ranged uses80percent skill,then25percent reposition3,else idle10.',
            'Pinned current Iron Golem metadata selects NecroPet,codeG4,velocity9/run9. Source skill slots are supplied controls for the nonzero-parameter branch,not an invented Iron Golem spell.'
        ], remaining=[
            'Target selection,full-size distance,visibility,owner lookup,common movement and tactics are supplied services. Synthetic combinations test branch semantics,not current-world reachability. PETIGNORE remains an unchanged direct field read.',
            'Default AI parameter initialization,current executable parity,world movement/path/geometry/action clocks,saved shared seed/item IAS,attacks/recovery/lifecycle and learned casting still require integration and acceptance.'
        ])
    target = ROOT / 'tools/reference/necromancer-pet-ai-source-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check: assert target.read_text() == rendered
    else: target.write_text(rendered)
    print(f'Necromancer pet AI67:{len(result)} complete unchanged-source ordered-service contexts pass ASan+UBSan; live actor/casting remain separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
