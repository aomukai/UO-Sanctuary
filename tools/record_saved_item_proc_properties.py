#!/usr/bin/env python3
"""Bind retained property11 projection and the saved native player proc host."""
import argparse,hashlib,json,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/saved-item-proc-properties';BASE='22a373f70ee7c9eb68be0a711a3d6b28bd783d76'
sys.path.insert(0,str(ROOT/'tests'))
from check_saved_item_proc_properties import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=count
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/player-item-proc-host/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/EquipmentPropertyWriter.cs','src/Sanctuary.Content/ItemSkillPropertyWriter.cs','src/Sanctuary.Content/NamedPropertyRuntime.cs','tests/Sanctuary.Integration/PlayerItemProcHostChecks.cs','tests/Sanctuary.Integration/SavedItemProcPropertiesChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name in ('Content','Integration'):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+'-build-2.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected_markers(),38)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-2.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==8 and 'denied by connection filter' not in log
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
    files=[ROOT/'tools/record_saved_item_proc_properties.py',ROOT/'docs/saved-item-proc-properties-checkpoint.md',ROOT/'tools/reference/player-item-proc-host-native-evidence.json',ROOT/'tools/reference/consume-learned-client-evidence.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'superseded-run-1.tar.gz']
    result=dict(component='Retained property11 chance/rank arguments and saved-item-level projection',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=38,markers=dict(sorted(expected_markers().items())),connections=8,actualLearnedWireInputs=6,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,projectedOriginals=11,propertyPlans=6,playerProviderItems=3,playerStreams=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=2,integrationBuild=2,sourceSnapshot=2,nativeRun=2,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,durableAssemblyArchive='.local/class-skills/saved-item-proc-properties/accepted-assemblies-1.tar.gz',installed=False,namedAdmissionChanged=False,automaticPlayerEventDispatch=False,wholeHellslayerAdmitted=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Explicit retained-property component boundary and event-only player host carriers;not a complete named GearState or full item acquisition. Full original projection,seed acquisition,normal combat callers and automatic golem registration remain unfinished. Charges and unsupported trigger bodies remain gated. Raw argument fixture manifests are not a new live named-item schema. Existing WardrobeSet save warning and broken-pair reference uncertainty retain;no deployment or new reference read.')
    e.write('saved-item-proc-properties-native-evidence.json',result,files,check)
    print('Retained proc projection:38results/11native originals/6plans/saved player host/2reloads;whole named admission remains gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
