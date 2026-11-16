#!/usr/bin/env python3
"""Bind prepared eight-class player Fire Ball item emission and retained routes."""
import argparse,sys,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/player-item-proc-host';BASE='b1ef1043'
sys.path.insert(0,str(ROOT/'tests'))
from check_player_item_proc_host import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=count
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/player-item-proc-frames/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    compiled=e.load(STAGE/'source-snapshot-1.json')['inputs'];current=e.load(STAGE/'source-snapshot-4.json')['inputs']
    assert compiled['projectInputs']==current['projectInputs']
    assert all(current['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items() if p.startswith(('src/','shared/')))
    assert compiled['copiedData']==current['copiedData']==born['copiedData']==parent['copiedData']
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/ClassGear.cs','src/Sanctuary.Content/PlayerItemProcGraph.cs','src/Sanctuary.Content/PlayerItemProcs.cs','tests/Sanctuary.Integration/PlayerItemProcHostChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in [('Content',1),('Integration',4)]:
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected_markers(),35)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-2.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==8 and 'denied by connection filter' not in log
    warnings=[line for line in log.splitlines() if 'Warning: Attempted to delete' in line]
    assert len(warnings)==1 and all('WardrobeSet' in line for line in warnings),warnings
    for n in configuration()['extra_target_input_commands']:assert log.count(n+': windup')==3
    saved=e.load(STAGE/'player-item-fireball-fixtures.json');assert len(saved)==8
    assert {s['Flight']['PlayerItemClass'] for s in saved}==set(range(101,109))
    for s in saved:
        f=s['Flight'];sorc=f['PlayerItemClass']==102
        assert s['Generation']==2 and s['Mana']==0 and f['Rank']==20
        # Independent pinned rank20 growth,then three hard Fire Bolt points,
        # then rank4 Fire Mastery. General +skills never changes proc rank20.
        def endpoint(maximum):
            value=28 if maximum else 12
            for level in range(2,21):value+=(13 if level<=8 else 23 if level<=16 else 28)+(2 if maximum else 0)
            value*=128
            if sorc:value=value*142//100;value+=value*51//100
            return value
        assert (f['Minimum256'],f['Maximum256'])==(endpoint(False),endpoint(True))
        assert f['Modifiers']['FireMastery']==(51 if sorc else 0)
        assert s['Carrier']['Properties']==[{'Packed':(195<<16)|(47<<6)|20,'Value':10}] and s['Carrier']['ItemBase']==[]
    host=e.load(STAGE/'player-item-proc-host-fixtures.json');state=json.loads(host['State'])
    assert host['Generation']==2 and state['Version']==0 and state['ReferenceVersion']=='3.3.93847'
    assert len(state['Items'])==3 and len(state['Owners'])==2
    assert sorted(len(o['Events']['Entries']) for o in state['Owners'])==[2,4]
    assert {o['Actor'] for o in state['Owners']}=={host['First'],host['Second']}
    assert all(o['Seed']>0 for o in state['Owners']) and len({o['Seed']for o in state['Owners']})==2
    files=[ROOT/'tools/record_player_item_proc_host.py',ROOT/'docs/player-item-proc-host-checkpoint.md',ROOT/'tools/reference/consume-learned-client-evidence.json',ROOT/'tools/reference/npc-item-spell-origins-source-evidence.json',ROOT/'tools/reference/skill-execution-source-evidence.json',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/check_britain.py',STAGE/'birth-inputs.json',STAGE/'birth-native-checks.log',STAGE/'source-snapshot-1.json',STAGE/'source-snapshot-4.json',STAGE/'native-run-2.log',STAGE/'content-build-1.log',STAGE/'integration-build-4.log',STAGE/'failed-build-1.tar.gz',STAGE/'failed-build-1-native-inputs.json',STAGE/'failed-build-2-references.json',STAGE/'integration-build-2.log',STAGE/'source-snapshot-2.json',STAGE/'failed-run-1.tar.gz',STAGE/'player-item-proc-host-fixtures.json',STAGE/'failed-run-1.tar.gz',STAGE/'accepted-assemblies-1.tar.gz',STAGE/'player-item-fireball-fixtures.json',*configuration()['extra_inputs']]
    import hashlib,tarfile
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz') as archive:
        for name in ('Content','Integration'):
            assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    result=dict(component='Saved player item-owned proc providers,actor RNG and native equipment subscriptions',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=35,markers=dict(sorted(expected_markers().items())),connections=8,actualLearnedWireInputs=6,callbackOriginCases=32,preparedAllocationRefusals=8,preparedOfflinePauses=8,savedPlayerCarriers=8,gateCasesPerClassPerBoot=7,outerExceptionCasePerClassPerBoot=1,nestedFailureScopes=72,knownWardrobeSaveWarnings=1,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=4,sourceSnapshot=4,nativeRun=2,gameplaySourceChanged=True,nativeRebuilt=False,persistenceVersionChanged=False,persistedSchemaAdded=True,existingSchemasChanged=False,newProfile=dict(name='SanctuaryPlayerItemProcs',id=257,version=0),equipmentRefreshHook=True,providerItems=3,playerStreams=2,durableAssemblyArchive='.local/class-skills/player-item-proc-host/accepted-assemblies-1.tar.gz',installed=False,namedAdmissionChanged=False,automaticPlayerEventDispatch=False,wholeHellslayerAdmitted=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Registered event-only providers and player streams now persist in the native profile and follow actual equipment/hand/trade transitions. Full original proc projection,seed acquisition and normal combat phase callers remain unwired;no whole live Hellslayer admission or automatic golem registration. Existing broken-pair slot arbitration is preserved and flagged as unverified promotion behavior. One known WardrobeSet save warning;no whole-class/graphical certificate,new reference read,imagegen or deployment.')
    e.write('player-item-proc-host-native-evidence.json',result,files,check)
    print('Player proc host:35results/three original providers/two saved player streams/hand and trade subscriptions/two reloads;Hellslayer stays gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
