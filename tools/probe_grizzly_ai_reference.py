#!/usr/bin/env python3
"""Execute complete Bear AI112 using the established pet-decision ABI."""
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
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative = 'source/D2Game/src/AI/AiThink.cpp'
    raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    body = function(raw.decode(), 'AITHINK_Fn112_DruidBear')
    parent = BASE / 'spirit-wolf-ai-source-probe.cpp'
    prior = json.loads((ROOT / 'tools/reference/spirit-wolf-ai-source-evidence.json').read_text())
    assert sha(parent) == prior['generatedSourceSha256']
    original = function(raw.decode(), 'AITHINK_Fn108_SpiritWolf')
    cpp = parent.read_text().split(original, 1)[0]
    changes = [
        ('enum{DRUIDWOLF_AI_PARAM_STALL_DURATION,DRUIDWOLF_AI_PARAM_APPROACH_CHANCE_PCT,DRUIDWOLF_SPIRITWOLF_AI_PARAM_FOLLOW_OWNER_DISTANCE,DRUIDWOLF_AI_PARAM_MAX_TARGET_DISTANCE,DRUIDWOLF_AI_PARAM_MAX_OWNER_DISTANCE};',
         'enum{DRUIDBEAR_AI_PARAM_STALL_DURATION,DRUIDBEAR_AI_PARAM_RUN_CHANCE_PCT,DRUIDBEAR_AI_PARAM_SKILL_OR_ATTACK_CHANCE_PCT};'),
        ('nSkill[1]={292}', 'nSkill[1]={326}'),
        ('int params[5]={22,20,14,20,26};', 'int params[3]={15,40,50};'),
        ('int AIUTIL_GetDistanceToCoordinates_FullUnitSize(D2UnitStrc*,D2UnitStrc*){return potentialDistance;}',
         'int AIUTIL_GetDistanceToCoordinates_FullUnitSize(D2UnitStrc*,D2UnitStrc*target){return target==&owner?ownerDistance:potentialDistance;}'),
        ('bool AIRollChanceParam(D2GameStrc*,D2UnitStrc*,D2AiTickParamStrc*,int parameter){rolls++;return chanceRoll<params[parameter];}',
         'bool AIRollChanceParam(D2GameStrc*,D2UnitStrc*,D2AiTickParamStrc*,int parameter){rolls++;actions.push_back({13,parameter,chanceRoll});return chanceRoll<params[parameter];}'),
    ]
    for before, after in changes:
        assert cpp.count(before) == 1, before
        cpp = cpp.replace(before, after, 1)
    reference = ROOT / 'tools/reference/d2r-named-equipment'
    path = reference / 'json/monstats.json'
    assert sha(path) == json.loads((reference / 'sources.json').read_text())['files']['json/monstats.json']['sha256']
    row = json.loads(path.read_text())['druidbear']
    for suffix in ['', '(N)', '(H)']:
        assert [row['aip' + str(i) + suffix] for i in range(1, 4)] == [15, 40, 50]
    assert [row[k] for k in ['Skill1', 'Sk1mode', 'Velocity', 'Run']] == ['BearSmite', 'seq_bearsmite', 5, 9]
    cpp += r'''
void AITACTICS_WalkToTargetUnit(D2GameStrc*,D2UnitStrc*,D2UnitStrc*target){actions.push_back({14,target->dwUnitId});}
void AITACTICS_UseSequenceSkill(D2GameStrc*,D2UnitStrc*,int skill,D2UnitStrc*target,int x,int y){actions.push_back({15,skill,target->dwUnitId,x,y});}
''' + body + r'''
bool first=true;
void emit(int family,int own,int stored,int lookup,int p,int q,int pVis,int qVis,int distance,int ownDistance,int mode,int moved,int roll,int fighting,int velocity,int run){
hasOwner=own;rememberedOwner=lookup;hasPrimary=p;hasPotential=q;primaryVisible=pVis;potentialVisible=qVis;
potentialDistance=distance;ownerDistance=ownDistance;owner.dwAnimMode=mode;petMove=moved;chanceRoll=roll;combat=fighting;monster.nVelocity=velocity;monster.nRun=run;
control={};control.dwAiParam[0]=77;control.dwAiParam[1]=88;control.dwAiParam[2]=stored;
owner.dwUnitId=17;primary.dwUnitId=10;potential.dwUnitId=20;actions.clear();rolls=0;
AITHINK_Fn112_DruidBear(&game,&unit,&ai);
int values[]={own,stored,lookup,p,q,pVis,qVis,distance,ownDistance,mode,moved,roll,fighting,velocity,run};
printf("%s[%d,[",first?"":",",family);first=false;for(int i=0;i<15;i++)printf("%s%d",i?",":"",values[i]);
printf("],[%d,%d,%d],%d,[",control.dwAiParam[0],control.dwAiParam[1],control.dwAiParam[2],rolls);bool sep=false;
for(auto&a:actions){printf("%s[%d,%d,%d,%d,%d]",sep?",":"",a.kind,a.a,a.b,a.c,a.d);sep=true;}printf("]]");
}
int main(){printf("[");
for(int stored:{0,17})for(int lookup:{0,1})for(int moved:{0,1})emit(0,0,stored,lookup,1,1,1,1,27,0,1,moved,99,0,5,9);
for(int distance:{16,17,18,19,28,29,50,51})for(int mode:{1,2,3,6})for(int moved:{0,1})for(int fighting:{0,1})emit(1,1,0,1,1,1,1,1,27,distance,mode,moved,99,fighting,5,9);
for(int p:{0,1})for(int q:{0,1})for(int pVis:{0,1})for(int qVis:{0,1})for(int distance:{27,28})for(int ownDistance:{16,17})for(int fighting:{0,1})for(int roll:{39,40,49,50})emit(2,1,0,1,p,q,pVis,qVis,distance,ownDistance,1,0,roll,fighting,5,9);
for(int velocity:{-1,0,5,10})for(int run:{0,9,20})for(int roll:{39,40})emit(3,1,0,1,1,0,1,1,27,0,1,0,roll,0,velocity,run);
printf("]\n");}
'''
    generated = BASE / 'grizzly-ai-source-probe.cpp'
    generated.write_text(cpp)
    binary = generated.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(binary)], check=True)
    process = subprocess.run([str(binary)], capture_output=True)
    (BASE / 'grizzly-ai-sanitizer.log').write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-3000:]
    cases = json.loads(process.stdout)
    assert len(cases) == 672
    for family, context, state, rolls, actions in cases:
        own, stored, lookup, primary, potential, pvis, qvis, distance, owner_distance, mode, moved, roll, fighting, velocity, run = context
        expected = []
        def action(kind, *args):
            expected.append([kind, *args, *([0] * (4 - len(args)))])
        followed = False
        if not own:
            if stored and lookup:
                action(5, 3, 0, 0, 0)
            else:
                action(0, 10)
        elif owner_distance > 50:
            action(5, 3, 0, 0, 0)
        elif owner_distance > 28:
            action(5, 0, 0, 100, 0)
        else:
            if owner_distance > 18 and mode in [2, 3, 6]:
                action(5, 0, 0, 100 if mode == 3 else 0, 0)
                followed = bool(moved)
            if not followed:
                target = 10 if primary and pvis else 20 if potential and distance < 28 and qvis else 0
                if target and fighting:
                    action(13, 2, roll)
                    if roll < 50:
                        action(15, 326, target, 0, 0)
                    else:
                        action(6, 4, target); action(4, 15)
                elif not target:
                    action(0, 15) if owner_distance <= 16 else action(5, 0, 0, 0, 0)
                else:
                    action(13, 1, roll)
                    if roll < 40:
                        # Source integer division truncates toward zero.
                        v = 100 if velocity <= 0 else min(100 * run // velocity - 100, 100)
                        action(8, 0, v, 40)
                    action(14, target)
        assert state == [77, 88, 17 if own else stored]
        assert rolls == sum(x[0] == 13 for x in expected)
        assert actions == expected, (family, context, actions, expected)
    fixture = BASE / 'grizzly-ai-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
        sourceHashes={relative: hashlib.sha256(raw).hexdigest()}, unchangedRoutineHashes={'AITHINK_Fn112_DruidBear': hashlib.sha256(body.encode()).hexdigest()},
        pinnedMonsterHash=sha(path), reusedSpiritAbiSha256=sha(parent), generatedSourceSha256=sha(generated),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=len(cases)), sanitizers='ASan+UBSan exit0 with empty runtime stderr',
        established=[
            'Complete AI112 runs672ordered request/memory contexts. Current three difficulty columns agree on15/40/50parameters,velocity5/run9,BearSmite326sequence skill. Missing owner can PetMove3via remembered-owner lookup; distance>50uses PetMove3,>28uses walk PetMove0with100velocity,returning regardless of movement result.',
            'At owner distance>18,walk/townwalk or running-owner follow returns only on successful PetMove; failed movement continues target selection. Visible primary wins; potential requires positive visibility and full-size actor distance<28. Combat flag is retained independently of lookup result.',
            'Combat+target rolls50percent for sequence326,otherwise A1with15stall; missing target idles15at owner distance<=16or follows. Noncombat+target rolls40percent to SetVelocity(0,80,40)for current5/9,then WalkToTargetUnit in either case. No corpse/rage or shared-wolf common-move branch is introduced.',
        ], limits=[
            'Owner/primary/potential/visibility/full-size distance,chance,movement result and tactics are explicit supplied services. Target/combat combinations include synthetic controls,not claims of modern reachability. No native saved actor or learned Grizzly handler enabled.',
            'BearSmite sequence/packet/knockback,first-think/busy timing,world geometry/path/event/seed ordering,save/history/casting and current executable equivalence need separate acceptance. The old outer policy has no town gate; actual target lookup/town attack restrictions remain downstream services.',
        ])
    target = ROOT / 'tools/reference/grizzly-ai-source-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert target.read_text() == rendered
    else:
        target.write_text(rendered)
    print('Grizzly AI112:672complete ordered-service contexts pass ASan+UBSan; native body/sequence/casting remain separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
