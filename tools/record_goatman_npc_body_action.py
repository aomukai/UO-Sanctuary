#!/usr/bin/env python3
"""Authenticate persistent own-body Frenzy and native action/speed bindings."""
import argparse,json,struct,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/goatman-npc-body-action'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    parent=pin('tools/reference/goatman-npc-action-native-evidence.json');assert parent['approved']and parent['nativeMarkers']==18
    approval=pin('tools/reference/goatman-npc-action-approval.json');assert approval['approval']=='Approve this Goatman NPC action rule'
    original=subprocess.check_output(['git','show','200782d2273da8f4d25f3211a3612905865b6c14:src/Sanctuary.Content/BarbarianFrenzyRelease.cs'],cwd=ROOT)
    assert original==(ROOT/'src/Sanctuary.Content/BarbarianFrenzyRelease.cs').read_bytes(),'Shared source writer changed'
    animation=pin('tools/reference/d2r-local-animation-evidence.json');raw=STAGE.parent/'bone-barrier-retirement-source/AnimData.d2'
    assert e.sha(raw)==animation['sha256'];pin(raw);b=raw.read_bytes();c=0;records={}
    for _ in range(256):
        n=struct.unpack_from('<I',b,c)[0];c+=4
        for _ in range(n):
            row=b[c:c+160];c+=160;name=row[:8].split(b'\0')[0].decode('ascii')
            if name in ('GMA12HS','GMA22HS','GMWL2HS'):
                frames,speed=struct.unpack_from('<II',row,8);events=[(i,v)for i,v in enumerate(row[16:])if v]
                assert not events or len(events)==1 and events[0][1]==1
                records[name]=dict(code=name,frames=frames,speed=speed,actionFrame=events[0][0]if events else -1)
    assert c==len(b)
    assert pin('data/Sanctuary/Balance/goatman-animation.json')==[records[n]for n in ('GMA12HS','GMA22HS','GMWL2HS')]
    source=pin('tools/reference/d2r-named-equipment/json/monstats.json')['wargoatman'];assert source['Velocity']==source['Run']==8
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-3.json')['inputs']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=last['sourceInputs'][n]}=={'tests/Sanctuary.Integration/GoatmanNpcBodyActionChecks.cs'}
    assert {n for n in first['copiedData']if first['copiedData'][n]!=last['copiedData'][n]}=={'Data/Sanctuary/Balance/skill-effects.json'}
    for kind,num in [('content',2),('integration',3)]:
        p=STAGE/f'{kind}-build-{num}.log';pin(p)
        assert all(s in p.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'goatman-npc-body-action-check':1,
        'goatman-npc-body-action-reload-check':2,'goatman-body-check':1,'goatman-body-reload-check':2,'goatman-npc-action-check':3})
    e.cohort(STAGE,expected,18);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    fixture=pin(STAGE/'goatman-npc-body-action-fixtures.json');assert fixture['Generation']==2 and len(fixture['Rows'])==2
    for row in fixture['Rows']:
        body=json.loads(row['State']);assert body['Frenzy']['Charges']==2 and body['Frenzy']['UntilUtc']==row['Until']
        assert row['Maximum']-row['Life']==257 and body['Profile']=='prepared-goatman-life-owner-v1'
    pin(STAGE/'goatman-body-fixtures.json')
    for name in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','GoatmanNpcBodyActionChecks.cs','Sanctuary.Content.dll','Sanctuary.Integration.dll','reason.txt'):pin(STAGE/'diagnostics-1'/name)
    assert 'Absent optional state changed legacy JSON'in (STAGE/'diagnostics-1/birth-native-checks.log').read_text()
    for name in ('src/Sanctuary.Content/WarlockGoatmanBody.cs','src/Sanctuary.Content/GoatmanNpcActionProposal.cs','src/Sanctuary.Content/GoatmanNpcBodyActions.cs',
        'tests/Sanctuary.Integration/GoatmanNpcBodyActionChecks.cs','tests/Sanctuary.Integration/GoatmanNpcActionChecks.cs',
        'tests/check_goatman_npc_body_action.py','tools/record_goatman_npc_body_action.py','data/Sanctuary/Balance/skill-effects.json',
        'data/Sanctuary/Balance/skill-icons.json','docs/goatman-npc-body-action-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md'):pin(name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Goatman persistent NPC action/speed body binding',approvedNpcAction=True,
        handlers=221,warlockHandlers=12,completedClasses=[],learnedDemonSummonsEnabled=False,automaticPetAiEnabled=False,
        nativeMarkers=18,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=3,sourceSnapshot=3,nativeRun=2,
        nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,
        optionalNullOmittedMetadata='GoatmanBodyState.Frenzy',animationRecords=list(records.values()),artifacts=born['artifacts'],
        preservation={**protected,'installed':False},
        established=['Actual Warlock/Witch bodies share consecutive action charges without live gear/rank recapture.',
            'Captured birth/common attack/Frenzy rates aggregate once;source walk motion and caller-qualified other-animation rates consume own-body bonuses.',
            'Exact fractional UTC expiry,miss/no refresh,new charge after expiry,parking/no heal and atomic malformed-state refusal.',
            'Original wounded bodies retain exact JSON/deadlines/ownership/resources across2reloads;legacy absent-field bodies remain readable and byte-equivalent.',
            'Approved action controls plus legacy body/difficulty/recovery and baseline original-item/resource checks pass.'],
        limits=['GenericPet production path/clock/AI/ordered contact/events and learned summon are still unfinished.',
            'Movement kernel uses declared collision leaves;this does not admit an automatic world movement transaction or running-mode fallback.',
            'The host must qualify the source mode flag before choosing the other-animation branch.',
            'Final skill effects/audio and talent/hotkey icons remain separately pending;approved Gargoyle body is reused.'],inputs=inputs)
    path=ROOT/'tools/reference/goatman-npc-body-action-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Goatman body action receipt stale'
    else:path.write_text(encoded)
    print('Goatman body action binding:18markers/3boots/2reloads;original speed state/wounds preserved;learned host remains unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
