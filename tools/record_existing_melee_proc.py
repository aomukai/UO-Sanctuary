#!/usr/bin/env python3
"""Qualify existing shared Sacrifice/Vengeance/Cleave item-proc consumers without production edits."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/existing-melee-proc';BASE='52e86242120dc3c2553b72a977f3823dac5fcb9e'
sys.path.insert(0,str(ROOT/'tests'))
from check_existing_melee_proc import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):
                waits=c['extra_wait_results'][n]
                for result in ((waits,)if isinstance(waits,str)else waits):r[result]+=count
    for n in c.get('extra_checks',()):
        r[n]+=3
        if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=3
    for n in c.get('extra_pre_save_commands',()):r[n]+=3
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/amazon-thrust-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    assert changed==['tests/Sanctuary.Integration/ExistingMeleeProcChecks.cs'],changed
    for p,d in parent['sourceInputs'].items():
        if p.startswith(('src/','shared/'))and p.endswith('.cs'):assert born['sourceInputs'][p]==d
    assert e.sha(STAGE/'content/Sanctuary.Content.dll')==parent['artifacts']['Assemblies/Sanctuary.Content.dll']
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name in ('Content','Integration'):assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    text=(STAGE/'integration-build-3.log').read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==161
    log=e.cohort(STAGE,expected_markers(),161);assert 'pass across3boots/2reloads' in (STAGE/'native-run-3.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==50 and 'denied by connection filter' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'existing-melee-proc-fixtures.json');assert len(saved)==3
    for s in saved:
        assert s['Generation']==2 and s['Life']>0 and s['Mana']==17 and s['Durability']>0
        weapon,gloves,helm=json.loads(s['Gear']);assert weapon['NamedItemId']is None and gloves['NamedItemId']=='unique/232' and helm['NamedItemId']=='unique/207'
        profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Owners'][0]['Actor']==s['Owner']
        assert profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in (195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    files=[ROOT/'tools/record_existing_melee_proc.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/existing-melee-proc-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Existing Sacrifice/Vengeance/Cleave learned contacts with actual item proc providers',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=161,markers=dict(sorted(expected_markers().items())),connections=50,newWireInputs=18,retainedWireInputs=30,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,skills=3,contactScenarios=26,savedOriginalContexts=3,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=None,contentReusedFrom='amazon-thrust-contact/content-build-1',integrationBuild=3,sourceSnapshot=3,nativeRun=3,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Existing learned routes already use the shared player contact pipeline;remaining direct Strike matches are compatibility/test helpers,not missing learned implementations. Verified unchanged production DLL and all production sources. Twenty-six provider cases cover captured Sacrifice/Vengeance versus event-time Cleave poison,actual hit/attempt callbacks and flags,independent saved actor/world streams,once-only release resources,cancellation/range,block/miss/immunity,Sacrifice self-cost before proc damage,and two-recipient Cleave. Eighteen new inputs and three exact saved original/proc/wound/mana/durability contexts pass with retained controllers and contact checks. Earlier null-world fixture setup attempts are frozen. Modern/PvP/exceptional recipient and remaining missile/incoming/full named admission limits remain explicit. No gameplay rewrite,deployment,updated reference read,new item or class certificate.')
    e.write('existing-melee-proc-native-evidence.json',result,files,check)
    print('Existing melee:161results/18new inputs/26cases/3saved contexts/2reloads;production unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
