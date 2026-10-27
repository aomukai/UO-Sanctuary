#!/usr/bin/env python3
"""Execute the complete Fenris AI with explicit target/movement/corpse services."""
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
    source = raw.decode()
    old_body = function(source, 'AITHINK_Fn108_SpiritWolf')
    body = function(source, 'AITHINK_Fn108_Fenris')
    finder_relative = 'source/D2Game/src/SKILLS/SkillDruid.cpp'
    finder_raw = (BASE / 'd2moo-pinned-source' / finder_relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(finder_raw)).encode() + b'\0' + finder_raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == finder_relative)
    finder = function(finder_raw.decode(), 'SKILLS_FindUseableCorpse')
    assert 'nFlags = 0x1002' in finder and 'UNITS_IsCorpseUseable(pResult) && sub_6FCBD900(pGame, pUnit, pResult)' in finder
    assert finder.count('pOwner') == 1  # Signature only: interaction uses the actor.
    parent = BASE / 'spirit-wolf-ai-source-probe.cpp'
    prior = json.loads((ROOT / 'tools/reference/spirit-wolf-ai-source-evidence.json').read_text())
    assert sha(parent) == prior['generatedSourceSha256'] and parent.read_text().count(old_body) == 1
    cpp = parent.read_text().split(old_body, 1)[0]
    replacements = [
        ('DRUIDWOLF_SPIRITWOLF_AI_PARAM_FOLLOW_OWNER_DISTANCE', 'DRUIDWOLF_FENRIS_AI_PARAM_EAT_CORPSE_CHANCE_PCT'),
        ('unit,owner,primary,potential;', 'unit,owner,primary,potential,corpse;'),
        ('nRun=9,nSkill[1]={292},nSkillMode[1]={8}', 'nRun=10,nSkill[2]={314,292},nSkillMode[2]={4,8}'),
        ('int params[5]={22,20,14,20,26};', 'int params[5]={22,20,25,24,30};'),
        ('int ownerDistance=0,potentialDistance=19,chanceRoll=99,rolls=0;', 'int ownerDistance=0,potentialDistance=23,ownerTargetDistance=0,corpseDistance=11,chanceRoll=99,rolls=0;bool rage=false,found=false,corpseMelee=false;'),
        ('void AITACTICS_UseSkill(D2GameStrc*,D2UnitStrc*,int mode,int skill,int,int x,int y){actions.push_back({2,mode,skill,x,y});}',
         'void AITACTICS_UseSkill(D2GameStrc*,D2UnitStrc*,int mode,int skill,D2UnitStrc*target,int x,int y){if(target)actions.push_back({11,mode,skill,target->dwUnitId});else actions.push_back({2,mode,skill,x,y});}'),
        ('D2UnitStrc* sub_6FCF2110(D2GameStrc*,D2UnitStrc*,D2AiControlStrc*,int*,int*){return hasPotential?&potential:nullptr;}',
         'D2UnitStrc* sub_6FCF2110(D2GameStrc*,D2UnitStrc*,D2AiControlStrc*,int*distance,int*){*distance=potentialDistance;return hasPotential?&potential:nullptr;}'),
        ('int UNITS_GetDistanceToOtherUnit(D2UnitStrc*,D2UnitStrc*){return ownerDistance;}',
         'int UNITS_GetDistanceToOtherUnit(D2UnitStrc*a,D2UnitStrc*b){if(a==&owner&&b==&unit)return ownerDistance;if(a==&owner&&(b==&primary||b==&potential))return ownerTargetDistance;if(a==&corpse&&b==&unit)return corpseDistance;__builtin_trap();}'),
        ('bool AIRollChanceParam(D2GameStrc*,D2UnitStrc*,D2AiTickParamStrc*,int parameter){rolls++;return chanceRoll<params[parameter];}',
         'bool AIRollChanceParam(D2GameStrc*,D2UnitStrc*,D2AiTickParamStrc*,int parameter){rolls++;actions.push_back({13,parameter,chanceRoll});return chanceRoll<params[parameter];}'),
    ]
    for before, after in replacements:
        assert cpp.count(before) == 1, before
        cpp = cpp.replace(before, after, 1)
    reference = ROOT / 'tools/reference/d2r-named-equipment'
    path = reference / 'json/monstats.json'
    assert sha(path) == json.loads((reference / 'sources.json').read_text())['files']['json/monstats.json']['sha256']
    row = json.loads(path.read_text())['fenris']
    for suffix in ['', '(N)', '(H)']:
        assert [row['aip' + str(i) + suffix] for i in range(1, 6)] == [22, 20, 25, 24, 30]
    assert [row[k] for k in ['Skill1', 'Sk1mode', 'Skill2', 'Sk2mode', 'Velocity', 'Run']] == ['fenris rage', 'A1', 'Teleport 2', 'S1', 5, 10]
    cpp += r'''
constexpr int STATE_FENRIS_RAGE=138;
bool STATES_CheckState(D2UnitStrc*,int state){if(state!=138)__builtin_trap();return rage;}
D2UnitStrc*SKILLS_FindUseableCorpse(D2GameStrc*,D2UnitStrc*,D2UnitStrc*who,int size){actions.push_back({12,who->dwUnitId,size});return found?&corpse:nullptr;}
bool UNITS_IsInMeleeRange(D2UnitStrc*,D2UnitStrc*target,int bonus){if(target!=&corpse||bonus!=0)__builtin_trap();return corpseMelee;}
''' + body + r'''
bool first=true;
void emit(int family,int own,int stored,int lookup,int inTown,int p,int q,int pVis,int qVis,int distance,int ownDistance,int targetDistance,int mode,int moved,int common,int roll,int teleport,int fighting,int eat,int activeRage,int pursuing,int hasCorpse,int cDistance,int melee){
hasOwner=own;rememberedOwner=lookup;town=inTown;hasPrimary=p;hasPotential=q;primaryVisible=pVis;potentialVisible=qVis;
potentialDistance=distance;ownerDistance=ownDistance;ownerTargetDistance=targetDistance;owner.dwAnimMode=mode;petMove=moved;commonMove=common;
chanceRoll=roll;monster.nSkill[0]=eat?314:-1;monster.nSkill[1]=teleport?292:-1;combat=fighting;rage=activeRage;found=hasCorpse;corpseDistance=cDistance;corpseMelee=melee;
control={};control.dwAiParam[0]=pursuing;control.dwAiParam[1]=77;control.dwAiParam[2]=stored;
owner.dwUnitId=17;primary.dwUnitId=10;potential.dwUnitId=20;corpse.dwUnitId=30;actions.clear();rolls=0;
AITHINK_Fn108_Fenris(&game,&unit,&ai);
int values[]={own,stored,lookup,inTown,p,q,pVis,qVis,distance,ownDistance,targetDistance,mode,moved,common,roll,teleport,fighting,eat,activeRage,pursuing,hasCorpse,cDistance,melee};
printf("%s[%d,[",first?"":",",family);first=false;for(int i=0;i<23;i++)printf("%s%d",i?",":"",values[i]);
printf("],[%d,%d,%d],%d,[",control.dwAiParam[0],control.dwAiParam[1],control.dwAiParam[2],rolls);bool sep=false;
for(auto&a:actions){printf("%s[%d,%d,%d,%d,%d]",sep?",":"",a.kind,a.a,a.b,a.c,a.d);sep=true;}printf("]]");
}
int main(){printf("[");
for(int stored:{0,17})for(int lookup:{0,1})for(int teleport:{0,1})emit(0,0,stored,lookup,0,1,1,1,1,23,0,23,1,0,0,99,teleport,0,1,0,1,1,11,1);
for(int common:{0,1})emit(1,1,0,0,1,1,1,1,1,23,0,23,1,0,common,99,1,0,1,0,1,1,11,1);
for(int p:{0,1})for(int q:{0,1})for(int pVis:{0,1})for(int qVis:{0,1})for(int distance:{23,24})for(int ownDistance:{0,25})for(int targetDistance:{23,25})for(int fighting:{0,1})for(int common:{0,1})emit(2,1,0,1,0,p,q,pVis,qVis,distance,ownDistance,targetDistance,1,0,common,99,1,fighting,0,0,1,1,11,1);
for(int distance:{24,25,30,31,50,51})for(int mode:{1,2,3,6})for(int moved:{0,1})for(int common:{0,1})for(int teleport:{0,1})emit(3,1,0,1,0,1,1,1,1,23,distance,23,mode,moved,common,99,teleport,0,1,0,0,1,11,1);
for(int p:{0,1})for(int fighting:{0,1})for(int eat:{0,1})for(int activeRage:{0,1})for(int pursuing:{0,1})for(int hasCorpse:{0,1})for(int distance:{11,12})for(int melee:{0,1})for(int roll:{24,25})emit(4,1,0,1,0,p,0,1,1,23,0,23,1,0,0,roll,1,fighting,eat,activeRage,pursuing,hasCorpse,distance,melee);
printf("]\n");}
'''
    generated = BASE / 'dire-wolf-ai-source-probe.cpp'
    generated.write_text(cpp)
    binary = generated.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(binary)], check=True)
    process = subprocess.run([str(binary)], capture_output=True)
    (BASE / 'dire-wolf-ai-sanitizer.log').write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-3000:]
    cases = json.loads(process.stdout)
    assert len(cases) == 1226
    for family, context, state, rolls, actions in cases:
        own, stored, lookup, in_town, primary, potential, pvis, qvis, distance, owner_distance, target_distance, mode, moved, common, roll, teleport, fighting, eat, active_rage, pursuing, found, corpse_distance, melee = context
        expected, count, memory = [], 0, [pursuing, 77, 17 if own else stored]
        def action(kind, *args):
            expected.append([kind, *args, *([0] * (4 - len(args)))])
        def chance(parameter):
            action(13, parameter, roll)
            return roll < [22, 20, 25, 24, 30][parameter]
        if not own:
            if stored and lookup and teleport:
                action(2, 8, 292, 40, 70); action(3, 6); action(4, 8)
            else:
                action(0, 10)
        elif in_town:
            action(1, 0, 0, 0, 6)
            if not common:
                action(0, 33)
        else:
            target = 10 if primary and pvis else 20 if potential and distance < 24 and qvis else 0
            if target and owner_distance > 24 and target_distance > 24:
                target = 0
            if teleport and owner_distance > 50:
                action(2, 8, 292, 40, 70); action(3, 2); action(4, 10)
            elif owner_distance > 30 or owner_distance > 24 and mode in [2, 3, 6]:
                action(5, 0, int(owner_distance > 30 or mode == 3), 100 if owner_distance > 30 or mode == 3 else 0, 0)
            else:
                action(1, target, fighting, 1, 6)
                if not common:
                    handled = False
                    if eat and not active_rage and (chance(2) or not target or pursuing):
                        action(12, 17, 10)
                        if found and corpse_distance < 12:
                            if melee:
                                memory[0] = 0; action(11, 4, 314, 30); handled = True
                            elif not fighting:
                                action(9, 30); memory[:2] = [1, 30]; handled = True
                            else:
                                action(6, 4, target); action(4, 22); handled = True
                    else:
                        memory[0] = 0
                    if not handled:
                        if fighting:
                            action(6, 4, target); action(4, 22)
                        elif target:
                            action(8, 0, 100, 0); action(9, target)
                        elif chance(1):
                            action(10, 10)
                        else:
                            action(0, 15)
        count = sum(x[0] == 13 for x in expected)
        assert [state, rolls, actions] == [memory, count, expected], (family, context, state, rolls, actions, memory, expected)
    fixture = BASE / 'dire-wolf-ai-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
        sourceHashes={relative: hashlib.sha256(raw).hexdigest(), finder_relative: hashlib.sha256(finder_raw).hexdigest()},
        unchangedRoutineHashes={'AITHINK_Fn108_Fenris': hashlib.sha256(body.encode()).hexdigest()},
        reviewedOnlyRoutines={'SKILLS_FindUseableCorpse': hashlib.sha256(finder.encode()).hexdigest()},
        pinnedMonsterHash=sha(path), reusedSpiritAbiSha256=sha(parent), generatedSourceSha256=sha(generated),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=len(cases)), sanitizers='ASan+UBSan exit0 with empty runtime stderr',
        established=[
            'Complete Fenris AI executes1,226ordered-service contexts with current parameters22/20/25/24/30,velocity5/run10 and distinct skill slots314A1/292S1. All three current difficulty parameter columns agree. Ownerless warp uses event+6/idle8; distance>50warp uses event+2/idle10. Far/moving-owner follow returns regardless of the supplied movement result.',
            'Visible primary wins; fallback requires positive potential visibility and distance<24. A target is discarded only when both owner-to-wolf and owner-to-target distances exceed24. Shared pet movement precedes corpse work. This is distinct from the old Spirit Wolf fallback/combat defect.',
            'Available rage and absent rage state test the25percent roll before the no-target/pursuing alternatives,so RNG is still consumed in those alternatives. Corpse lookup requests size10; the selected corpse must be at distance<12. Melee uses314and clears pursuit; noncombat chase remembers corpse ID; combat attacks the current target instead. Rage/unavailable-skill clears pursuit; a failed eligible search does not necessarily clear it.',
            'Separately reviewed complete corpse finder uses the actor position/room,size10and flags0x1002,returning the first usable corpse accepted by its actor-interaction service. The owner argument is unused. No nearest-distance sort or release-time soft/dead-collision filter is silently added to this finder.',
        ], limits=[
            'Owner/target lookup,visibility,distances,room,common movement,chance,corpse finder and tactics/event execution are explicit services. The complete finder/predicates/release have separate evidence; this does not implement native corpse ownership/loot,physical combat,saved AI or learned casting.',
            'Current native CorpseSkillRules supplies selectable/no-select/shattered/ownership/loot gates; it does not yet expose the distinct source soft/dead-collision release predicates. Extend or explicitly map those traits before a native Fenris consumption claim. Existing Sanctuary corpse/loot protection remains a separate project rule.',
            'Source lookup/combat flags include independent synthetic controls,including contradictory target/combat inputs; their literal requests are captured,not asserted reachable modern world states. Current first-think/busy-mode dispatch,geometry/path/room/interaction/RNG order and executable equivalence remain separate. No native Fenris actor is enabled.',
        ])
    target = ROOT / 'tools/reference/dire-wolf-ai-source-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert target.read_text() == rendered
    else:
        target.write_text(rendered)
    print('Fenris AI:1226complete ordered-service contexts pass ASan+UBSan; native actor/corpse/rage/casting remain separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
