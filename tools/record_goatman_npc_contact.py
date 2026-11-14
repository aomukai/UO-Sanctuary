#!/usr/bin/env python3
"""Authenticate own Goatman ordered contacts,clock and durable RNG."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/goatman-npc-contact'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    parent=pin('tools/reference/goatman-npc-body-action-native-evidence.json');assert parent['nativeMarkers']==18
    approval=pin('tools/reference/goatman-npc-action-approval.json');assert approval['approved']and approval['approval']=='Approve this Goatman NPC action rule'
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-7.json',STAGE/'birth-inputs.json')
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    for kind,num in [('content',5),('integration',6)]:
        log=STAGE/f'{kind}-build-{num}.log';pin(log)
        assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'goatman-npc-body-action-check':1,
        'goatman-npc-body-action-reload-check':2,'goatman-npc-action-check':3,'goatman-npc-contact-check':1,
        'goatman-npc-contact-reload-check':2,'goatman-npc-contact-check-result':1,'goatman-npc-contact-reload-check-result':2})
    log=e.cohort(STAGE,expected,21);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-4.log')
    assert log.count('Goatman ordered contacts:actual own-body Stun/Berserk/Frenzy/Cleave+basic')==3
    fixture=pin(STAGE/'goatman-npc-contact-fixtures.json');assert fixture['Generation']==2 and fixture['Random']['Unarmed']and fixture['Random']['Actor']==fixture['Body']and fixture['Random']['Owner']==fixture['Owner']
    assert pin(STAGE/'goatman-npc-body-action-fixtures.json')['Generation']==2
    for run in (1,2):
        for name in ('birth-inputs.json','birth-native-checks.log',f'native-run-{run}.log','GoatmanNpcContactChecks.cs','Sanctuary.Integration.dll','reason.txt'):pin(STAGE/f'diagnostics-{run}'/name)
        assert "Equip Guillaume's Face"in (STAGE/f'diagnostics-{run}/birth-native-checks.log').read_text()
    for name in ('birth-inputs.json','birth-native-checks.log','native-run-3.log','GoatmanNpcContact.cs','GoatmanNpcContactChecks.cs','Sanctuary.Content.dll','Sanctuary.Integration.dll','reason.txt'):pin(STAGE/'diagnostics-3'/name)
    assert 'Original melee result needs admitted equipment' in (STAGE/'diagnostics-3/birth-native-checks.log').read_text()
    source=pin('tools/reference/d2r-named-equipment/json/skills.json')['373']
    assert source['auraevent1']=='domeleedamage'and source['auraeventfunc1']==16 and source['passivestat12']=='item_crushingblow'
    pin('data/Sanctuary/Balance/warlock-npc-skills.json');pin('data/Sanctuary/Balance/goatman-base.json')
    geometry=pin('data/Sanctuary/Balance/revive-melee-geometry.json');assert next(r for r in geometry if r['Key']=='wargoatman')==dict(Key='wargoatman',BaseId=742,Size=2,RawRange=2)
    for name in ('src/Sanctuary.Content/GoatmanNpcContact.cs','src/Sanctuary.Content/GoatmanPrepared.cs','src/Sanctuary.Content/GoatmanNpcActionProposal.cs',
        'src/Sanctuary.Content/WarlockGoatmanBody.cs','src/Sanctuary.Content/GoatmanNpcBodyActions.cs','src/Sanctuary.Content/NpcCrushing.cs','tests/Sanctuary.Integration/GoatmanNpcContactChecks.cs',
        'tests/check_goatman_npc_contact.py','tools/record_goatman_npc_contact.py','docs/goatman-npc-contact-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md',
        'data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json'):pin(name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Goatman own-body ordered NPC contact/world leaves',
        handlers=221,warlockHandlers=12,completedClasses=[],learnedDemonSummonsEnabled=False,automaticPetAiEnabled=False,
        nativeMarkers=21,markers=dict(expected),authenticatedSessions=3,boots=3,reloads=2,saves=3,
        contentBuild=5,integrationBuild=6,sourceSnapshot=7,nativeRun=4,nativeRebuilt=False,clientRebuilt=False,
        newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,
        artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Actual captured demon supplies ordinary basic and four internal NPC skill packets to existing ordered HP/events/return services.',
            'Forced melee CB uses captured chance and one actor RNG roll;native damage notification and ordinary packet commit exactly once.',
            'Miss/range/duplicate-release controls;Berserk timed startup/magic conversion;actual surviving-recipient Stun.',
            'Owner level60/Strength/real CB-deadly helmet/hard Death Mark do not rewrite demon snapshot or transfer item combat bonuses.',
            'Melee-only aura rejects missile CB without health/RNG changes;actual connected world action stops when owner is offline.',
            '20million-frame action uses local animation epoch;exhausted timelines refuse without mutating body/RNG.',
            'Original wounded parked demon retains exact birth/speed state and next combat RNG roll across2reloads.'],
        limits=['Automatic GenericPet root/search/follow/path/mode/events and world clock/rearm are not yet installed.',
            'Complete summon/cap/casting route remains unavailable;no complete class certified.',
            'Existing special-recipient/uninterruptible/unique-return refusals remain;equipped Goatman/extra magic/duplicate nonzero sunder contexts refuse.',
            'Final effects/audio and talent/hotkey icons remain separate;Gargoyle appearance is reused.'],inputs=inputs)
    path=ROOT/'tools/reference/goatman-npc-contact-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Goatman ordered contact receipt stale'
    else:path.write_text(encoded)
    print('Goatman ordered contacts:21markers/3authenticatedsessions/2reloads;actual HP/events/own saved RNG;full AI host remains unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
