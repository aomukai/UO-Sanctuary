#!/usr/bin/env python3
"""Authenticate original-body timed NPC attacks and source-position guards."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/goatman-native-combat'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    snapshots=[pin(STAGE/f'source-snapshot-{n}.json')['inputs']for n in (1,2,3,4)]
    assert all(s['copiedData']==snapshots[0]['copiedData']and s['projectInputs']==snapshots[0]['projectInputs']for s in snapshots)
    assert {n for n in snapshots[0]['sourceInputs']if snapshots[0]['sourceInputs'][n]!=snapshots[1]['sourceInputs'][n]}=={'tests/Sanctuary.Integration/GoatmanNativeCombatChecks.cs'}
    assert {n for n in snapshots[1]['sourceInputs']if snapshots[1]['sourceInputs'][n]!=snapshots[2]['sourceInputs'][n]}=={
        'src/Sanctuary.Content/GoatmanNativeCombat.cs','src/Sanctuary.Content/GoatmanNpcContact.cs'}
    assert {n for n in snapshots[2]['sourceInputs']if snapshots[2]['sourceInputs'][n]!=snapshots[3]['sourceInputs'][n]}=={'tests/Sanctuary.Integration/GoatmanNativeCombatChecks.cs'}
    for kind,num in [('content',2),('integration',4)]:
        log=STAGE/f'{kind}-build-{num}.log';pin(log)
        assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/goatman-native-walking-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'goatman-native-combat-check':1,
        'goatman-native-combat-reload-check':2,'goatman-native-combat-check-result':1,'goatman-native-combat-reload-check-result':2,
        'goatman-native-walking-check':1,'goatman-native-walking-reload-check':2,'goatman-native-decision-check':1,'goatman-native-decision-reload-check':2})
    e.cohort(STAGE,expected,21);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    saved=pin(STAGE/'goatman-native-combat-fixtures.json')
    assert saved['Generation']==2 and saved['Controller']['Mode']==1
    assert json.loads(saved['Birth'])['Snapshot']['Birth']['MaximumLife256']-saved['Life']==257
    assert pin(STAGE/'goatman-native-walking-fixtures.json')['Generation']==2 and pin(STAGE/'goatman-native-decision-fixtures.json')['Generation']==2
    for name in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','GoatmanNativeCombat.cs','GoatmanNpcActionProposal.cs','GoatmanNpcContact.cs','GoatmanNativeCombatChecks.cs','Sanctuary.Integration.dll','Sanctuary.Content.dll','reason.txt'):pin(STAGE/'diagnostics-1'/name)
    assert 'Timed Goatman attack needs an idle active registered actor'in (STAGE/'diagnostics-1/birth-native-checks.log').read_text()
    assert born['artifacts']['Assemblies/Sanctuary.Content.dll']==e.sha(STAGE/'diagnostics-1/Sanctuary.Content.dll')
    for name in ('src/Sanctuary.Content/GoatmanNativeCombat.cs','src/Sanctuary.Content/GoatmanNpcActionProposal.cs','src/Sanctuary.Content/GoatmanNpcContact.cs',
        'tests/Sanctuary.Integration/GoatmanNativeCombatChecks.cs','tests/check_goatman_native_combat.py','tools/record_goatman_native_combat.py',
        'docs/goatman-native-combat-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md',
        'data/Sanctuary/Balance/goatman-animation.json','data/Sanctuary/Balance/warlock-npc-skills.json',
        'data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/goatman-npc-action-approval.json'):pin(name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Goatman original-body timed combat executor and precise source pose',
        handlers=221,warlockHandlers=12,completedClasses=[],learnedDemonSummonsEnabled=False,automaticPetAiEnabled=False,
        nativeMarkers=21,markers=dict(expected),authenticatedSessions=3,boots=3,reloads=2,saves=3,
        contentBuild=2,integrationBuild=4,sourceSnapshot=4,nativeRun=2,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,
        newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Actual timed basic/Stun/Berserk/Frenzy/Cleave actions through GenericPet action-service binding and original native owner/body.',
            'Basic/do2 startup packets remain unchanged through windup;approved Frenzy/Cleave allocate on contact;native HP/notifications occur once with no mana cost.',
            'Local animation epoch supports frame20000000;UTC/source-frame phase is checked before delivery.',
            'Precise FP16 source movement,offline/external mode/clock guards cancel safely and consume stale packets;Cleave uses source geometry.',
            'Original idle controller,birth,Frenzy,wounds and combat seed survive2reloads;fresh transient action scenes run on each boot.',
            'Real native walking/room geometry and source decision-root regressions pass in the same cohort.'],
        limits=['AI entry,actual search/traversal,native birth/path/mode/warp/event/world clock/suspension and summon cap/casting still require host integration.',
            'Frame observations and birth paths are explicit acceptance inputs,not production constructor/default/global clock proof.',
            'In-flight packet/event persistence and server-load policy are not admitted;transient actions are not serialized.',
            'No learned summon route or complete class certification;effects/audio/icons remain separate.'],inputs=inputs)
    p=ROOT/'tools/reference/goatman-native-combat-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded,'Timed combat evidence stale'
    else:p.write_text(encoded)
    print('Goatman timed combat:21markers/3authenticatedsessions/2reloads;all5actualactions;full AI host remains unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
