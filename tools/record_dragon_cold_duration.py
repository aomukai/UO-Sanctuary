#!/usr/bin/env python3
"""Bind Raven Frost/Ice replacement through four learned Dragon finishers."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/dragon-cold-duration';BASE='503cfa784fbeb961a78c73dd70d521a104cd8c40'
sys.path.insert(0,str(ROOT/'tests'))
from check_dragon_cold_duration import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=count
    for n in c['extra_checks']:r[n]+=3
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/assassin-weapon-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/DragonClaws.cs','src/Sanctuary.Content/DragonKickPackets.cs','tests/Sanctuary.Integration/DragonClawChecks.cs','tests/Sanctuary.Integration/DragonColdDurationChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',2)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    compiled=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-2.json')['inputs']
    assert compiled['projectInputs']==final['projectInputs'] and compiled['copiedData']==final['copiedData']
    assert all(final['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items() if p.startswith(('src/','shared/')))
    log=e.cohort(STAGE,expected_markers(),150);assert 'pass across3boots/2reloads' in (STAGE/'native-run-2.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==51 and 'denied by connection filter' not in log
    warnings=[line for line in log.splitlines() if 'Warning: Attempted to delete' in line];assert not warnings,warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz') as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'dragon-cold-duration-fixtures.json');assert len(saved)==4
    for row in saved:
        assert row['Generation']==2 and row['Life']>0 and row['TargetLife']>0
        gear=json.loads(row['Gear']);assert gear['NamedItemId']=='unique/275'
        state=json.loads(row['State']);assert state['Owner']==row['Target'] and state['Cold']['Source']==row['Owner'] and state['Control']['Freeze']['Source']==row['Owner'] and state['ChillUntilUtc']==state['Control']['FreezeUntil']
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');assert len(e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json')['items'])==168
    files=[ROOT/'tools/record_dragon_cold_duration.py',ROOT/'docs/dragon-cold-duration-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'failed-run-1.tar.gz',ROOT/'tools/reference/dragon-cold-duration-source-review.json']
    result=dict(component='Raven Frost/progressive Ice duration on four Dragon finishers',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=150,markers=dict(sorted(expected_markers().items())),connections=51,newWireInputs=15,retainedWireInputs=36,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=0,skills=4,durationScenarios=36,savedContexts=4,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Claw and shared kick progressive Ice replace item cold duration;charge3 derives freeze from replaced length. Actual Raven/Bartuc equipment,no/one/three Ice charges,cold0/50/100resistance,recipient deadlines,once-only payment,real input and exact retained status/gear/charge/wound saves qualify this correction. One Tail kick-plus-burst packet-count fixture repair is frozen;production unchanged after Content1. Current executable/RNG/stat-graph,recipient guards,player recipients,remaining item/proc/missile/incoming and final graphical parity remain unfinished. No deployment or newer client read.')
    e.write('dragon-cold-duration-native-evidence.json',result,files,check)
    print('Dragon cold duration:150results/15new inputs/36combinations/4saved statuses/2reloads;broader gaps explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
