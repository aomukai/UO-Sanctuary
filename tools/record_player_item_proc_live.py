#!/usr/bin/env python3
"""Bind approved player melee item procs and authenticated learned input."""
import argparse,hashlib,json,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/player-item-proc-live';BASE='6b091d587e65503b92d82080fe20c1ded40be744'
sys.path.insert(0,str(ROOT/'tests'))
from check_player_item_proc_live import configuration

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
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/player-item-proc-world/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    first=e.load(STAGE/'source-snapshot-1.json')['inputs'];last=e.load(STAGE/'source-snapshot-3.json')['inputs']
    assert all(last['sourceInputs'].get(p)==d for p,d in first['sourceInputs'].items() if p.startswith(('src/','shared/')))
    assert first['projectInputs']==last['projectInputs'] and first['copiedData']==last['copiedData']
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/PlayerItemProcWorld.cs','tests/Sanctuary.Integration/PlayerItemProcWorldChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name in ('Content','Integration'):
        build=1 if name=='Content' else 3
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected_markers(),56)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-3.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==14 and 'denied by connection filter' not in log
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
    files=[ROOT/'tools/record_player_item_proc_live.py',ROOT/'docs/player-item-proc-live-checkpoint.md',ROOT/'docs/player-item-proc-world-proposal.md',ROOT/'tools/reference/player-item-proc-world-native-evidence.json',ROOT/'tools/reference/consume-learned-client-evidence.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'failed-run-1.tar.gz',STAGE/'failed-run-2.tar.gz']
    result=dict(component='Approved player melee proc world binding and authenticated learned input',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=56,markers=dict(sorted(expected_markers().items())),connections=14,actualRetainedLearnedWireInputs=6,actualProcZealWireInputs=6,learnedZealReleases=9,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,projectedOriginals=11,worldCandidateActors=1,retainedPlayerProviderItems=3,retainedPlayerStreams=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=3,sourceSnapshot=3,nativeRun=3,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,durableAssemblyArchive='.local/class-skills/player-item-proc-live/accepted-assemblies-1.tar.gz',installed=False,namedAdmissionChanged=False,worldPolicyApproved=True,approval="Adopt the tested melee world rule",productionOrderedMeleeEventDispatch=True,wholeHellslayerAdmitted=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='User-approved world rule is enabled for registered providers in the ordered melee route. Actual authenticated right/quick Zeal input preserves the ordinary sequence,target,once-only mana and distinct exact RNG domains. Complete named originals,normal basic/missile/incoming callers,other proc bodies and automatic golem registration remain unfinished. The existing saved prevent-heal state isolates target wounds from normal source regeneration;gameplay regeneration is unchanged. One known WardrobeSet save warning;no deployment or newer reference read.')
    e.write('player-item-proc-live-native-evidence.json',result,files,check)
    print('Approved player melee item procs:56results/6authenticated Zeal inputs/independent saved streams/2reloads;whole named admission remains gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
