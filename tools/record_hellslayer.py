#!/usr/bin/env python3
"""Bind complete Hellslayer acquisition,player/learned-golem input and saved originals."""
import argparse,hashlib,json,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/hellslayer';BASE='0a415413a0b71205e991b57c488c30c78eeff2e0'
sys.path.insert(0,str(ROOT/'tests'))
from check_hellslayer import configuration

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
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/player-basic-proc/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    changed_data=sorted(p for p,d in born['copiedData'].items() if parent['copiedData'].get(p)!=d)
    assert changed_data==['Data/Sanctuary/Balance/named-proc-items.json','Data/Sanctuary/Balance/named-skill-backlog.json'],changed_data
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/ItemSkillPropertyWriter.cs','src/Sanctuary.Content/NamedGear.cs','src/Sanctuary.Content/NamedItemProcs.cs','tests/Sanctuary.Integration/HellslayerChecks.cs','tests/Sanctuary.Integration/IronGolemFireBallChecks.cs','tests/Sanctuary.Integration/OriginalItemProcProjectionChecks.cs','tests/Sanctuary.Integration/PlayerItemFireBallChecks.cs','tests/Sanctuary.Integration/PlayerItemProcHostChecks.cs','tests/Sanctuary.Integration/PlayerItemProcWorldChecks.cs','tests/Sanctuary.Integration/SavedItemProcPropertiesChecks.cs','tests/Sanctuary.Integration/SorceressStaffGrantChecks.cs','tests/Sanctuary.Integration/WarpspearGrantChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name in ('Content','Integration'):
        build=1 if name=='Content' else 3
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected_markers(),104)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-4.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==32 and 'denied by connection filter' not in log
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
    files=[ROOT/'tools/record_hellslayer.py',ROOT/'docs/hellslayer-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'docs/deferred-named-skills.md',ROOT/'tools/build_named_readiness.py',ROOT/'tools/build_named_skill_backlog.py',ROOT/'tests/check_sorceress_staff_grants.py',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/named-item-pre-proc-baseline.json',ROOT/'tools/reference/player-basic-proc-native-evidence.json',ROOT/'tools/reference/consume-learned-client-evidence.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'failed-run-1.tar.gz',STAGE/'failed-run-2.tar.gz',STAGE/'superseded-input-run-3.tar.gz']
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
    compiled=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-3.json')['inputs']
    assert compiled['projectInputs']==final['projectInputs'] and compiled['copiedData']==final['copiedData']
    assert all(final['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items() if p.startswith(('src/','shared/')))
    old=e.load(ROOT/'tools/reference/named-item-pre-proc-baseline.json');audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json')
    assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    prior={'unique/186','unique/56','unique/57','unique/58','unique/54','unique/185','piece/76','piece/77','piece/78','piece/79','piece/80'}
    before={r['Id']:r for r in old['Definitions']};after={r['Id']:r for r in audit['Definitions']}
    assert len(before)==len(after)==573 and {k for k in before if before[k]['Available']!=after[k]['Available']}==prior|{'unique/254'}
    assert after['unique/254']['Available'] and after['unique/254']['Reasons']==[]
    backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert backlog['qualifiedProcItems']==['unique/254'] and len(backlog['items'])==168 and len(backlog['sets'])==4 and all(i['id']!='unique/254' for i in backlog['items'])
    whole=e.load(STAGE/'hellslayer-fixtures.json');assert whole['Generation']==2
    for name in ('PlayerGear','GolemGear'):
        gear=json.loads(whole[name]);assert gear['NamedItemId']=='unique/254' and gear['BaseCode']=='7bt' and gear['Roll']['ItemLevel']==99 and len(gear['NamedProperties'])==7
    recipe=json.loads(whole['Recipe']);assert recipe['Item']==whole['GolemItem'] and len(recipe['Properties'])==9
    npc=json.loads(whole['Events']);assert npc['Actor']==whole['Body'] and npc['Items'][0]['Item']==whole['GolemItem']
    assert npc['Items'][0]['State']['Properties']==[dict(Packed=(195<<16)|(47<<6)|20,Value=10)]
    assert whole['Random']['Actor']==whole['Body'] and whole['Random']['Item']==whole['GolemItem']
    result=dict(component='Complete named Hellslayer player and learned Iron Golem return',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=104,markers=dict(sorted(expected_markers().items())),connections=32,actualHellslayerPlayerWireInputs=6,actualHellslayerGolemWireInputs=6,retainedWireInputs=18,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,completeNamedOriginals=2,sourcePropertiesPerOriginal=7,rawPropertyFields=9,catalogueDefinitions=573,newlyAdmittedItems=['unique/254'],priorDirectGrantDeltas=sorted(prior),backlogRows=168,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=changed_data,contentBuild=1,integrationBuild=3,sourceSnapshot=3,nativeRun=4,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,durableAssemblyArchive='.local/class-skills/hellslayer/accepted-assemblies-1.tar.gz',installed=False,namedAdmissionChanged=True,wholeHellslayerAdmitted=True,positiveAutomaticLearnedGolemQualified=True,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Complete normal named selection/save/equip/identification/trade and learned original Iron Golem/AI/input are qualified with the real10percent chance and no manual event enrollment. Only the exact Hellslayer return is admitted beyond11previously qualified direct-grant returns;fresh573definition audit and168row backlog agree. Charged/form/missile/incoming player contexts,other trigger bodies,modern whole-engine/graphical parity and old-content migration remain unfinished. One known WardrobeSet save warning;no deployment or newer reference read.')
    e.write('hellslayer-native-evidence.json',result,files,check)
    print('Complete Hellslayer return:104results/12actual item inputs/2reloads/573definition audit;human/shared melee and learned original golem qualified.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
