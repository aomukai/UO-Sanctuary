#!/usr/bin/env python3
"""Bind ordinary eight-class melee proc delivery and actual left/right input."""
import argparse,hashlib,json,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/player-basic-proc';BASE='14efe79b69bcc642a75de6200c41cc5e812ced0f'
sys.path.insert(0,str(ROOT/'tests'))
from check_player_basic_proc import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=count
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    for n in c['extra_pre_save_commands']:r[n]+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/original-item-proc-projection/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/ClassCombat.cs','src/Sanctuary.Content/PlayerBasicContact.cs','src/Sanctuary.Content/PlayerWeaponContact.cs','tests/Sanctuary.Integration/PlayerBasicProcChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name in ('Content','Integration'):
        build=1
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected_markers(),74)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==20 and 'denied by connection filter' not in log
    warnings=[line for line in log.splitlines() if 'Warning: Attempted to delete' in line]
    assert len(warnings)==1 and all('WardrobeSet' in line for line in warnings),warnings
    fixtures=e.load(STAGE/'saved-item-proc-properties-fixtures.json');assert len(fixtures)==11
    assert len({s['Item']for s in fixtures})==11
    expected=[(195,47,20,10),(198,47,1,100),(201,47,8,5),(196,47,1,5),(197,428,7,5),(199,362,1,9),(195,47,1,10),(198,47,63,17),(195,47,8,100),(195,47,20,10),(201,47,20,5)]
    for s,(stat,skill,rank,chance) in zip(fixtures,expected):
        assert s['Projected']==[dict(Packed=(stat<<16)|(skill<<6)|rank,Value=chance)]
        assert s['Roll']['Value']==(s['Property']['SourceMin']or 0) and s['Roll']['Maximum']==(s['Property']['SourceMax']or 0)
    host=e.load(STAGE/'player-item-proc-host-fixtures.json');state=json.loads(host['State'])
    assert host['Generation']==2 and state['Version']==0 and state['ReferenceVersion']=='3.3.93847'
    assert len(state['Items'])==3 and len(state['Owners'])==2
    assert sorted(len(o['Events']['Entries'])for o in state['Owners'])==[2,4]
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz') as archive:
        for name in ('Content','Integration'):
            assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    world=e.load(STAGE/'player-item-proc-world-fixtures.json');own=json.loads(world['Profile'])
    assert world['Generation']==2 and len(own['Items'])==len(own['Owners'])==1
    assert own['Items'][0]['Item']==world['Item'] and own['Owners'][0]['Actor']==world['Owner']
    assert own['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100) for stat in (195,198)]
    assert world['World']['Profile']=='sanctuary-world-explicit-seed-v1' and world['World']['Seed']>0
    files=[ROOT/'tools/record_player_basic_proc.py',ROOT/'docs/player-basic-proc-checkpoint.md',ROOT/'docs/player-item-proc-world-proposal.md',ROOT/'tools/reference/original-item-proc-projection-native-evidence.json',ROOT/'tools/reference/consume-learned-client-evidence.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    originals=e.load(STAGE/'original-item-proc-projection-fixtures.json');assert len(originals)==3
    expected_levels=[1,40,99];expected_ranks=[1,8,20]
    for saved,level,rank in zip(originals,expected_levels,expected_ranks):
        fields=saved['Fields'];assert fields['Item']==saved['Item'] and fields['ItemLevel']==level and len(saved['Rolls'])==7
        expected_raw=[dict(Packed=stat<<16,Value=value)for stat,value in [(7,6400),(17,100),(18,100),(48,150),(49,250),(219,24),(220,4),(223,4)]]
        event=dict(Packed=(195<<16)|(47<<6)|rank,Value=10)
        assert fields['Full']['Properties']==sorted([*expected_raw,event],key=lambda x:x['Packed'])
        assert fields['Events']['ItemBase']==[] and fields['Events']['Properties']==[event]
        assert json.loads(saved['Gear'])['NamedItemId'] is None
    basic=e.load(STAGE/'player-basic-proc-fixtures.json');assert len(basic)==8
    assert len({s['Owner']for s in basic})==len({s['Item']for s in basic})==8
    for saved in basic:
        assert saved['Generation']==2 and saved['Mana']==17
        profile=json.loads(saved['Profile']);assert len(profile['Owners'])==len(profile['Items'])==1
        assert profile['Owners'][0]['Actor']==saved['Owner'] and profile['Items'][0]['Item']==saved['Item']
        assert profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in (195,198)]
        assert len(profile['Owners'][0]['Events']['Entries'])==4
    result=dict(component='Ordinary human melee item procs through shared ordered contact',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=74,markers=dict(sorted(expected_markers().items())),connections=20,actualBasicWireInputs=6,actualProcZealWireInputs=6,actualRetainedFireBallWireInputs=6,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,basicClassContexts=8,directBasicSuccesses=24,directBasicMisses=24,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,durableAssemblyArchive='.local/class-skills/player-basic-proc/accepted-assemblies-1.tar.gz',installed=False,namedAdmissionChanged=False,orderedBasicMeleeDelivery=True,wholeHellslayerAdmitted=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Ordinary untransformed player-to-monster melee with active registered proc equipment uses the approved ordered source/world services and reuses its existing outer attempt scope. Eight-class component and authenticated left/right input prove weapon/proc damage,miss gates,one flat return,mana and saved streams. Explicit plain-original event providers remain;complete named/golem admission and charged/form/missile/incoming contexts remain unfinished. One known WardrobeSet save warning;no deployment or new reference read.')
    e.write('player-basic-proc-native-evidence.json',result,files,check)
    print('Ordinary melee item procs:74results/8class contexts/6authenticated basic inputs/2reloads;whole named/golem qualification remains pending.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
