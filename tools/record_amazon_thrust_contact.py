#!/usr/bin/env python3
"""Bind captured Amazon lightning thrusts,source poison and ordered item/child delivery."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/amazon-thrust-contact';BASE='ccdde0cebac80702d38f5691a0dd72ae20a10ba8'
sys.path.insert(0,str(ROOT/'tests'))
from check_amazon_thrust_contact import configuration

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
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/impale-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/AmazonMelee.cs','tests/Sanctuary.Integration/AmazonThrustContactChecks.cs','tests/Sanctuary.Integration/ChargedStrikeChecks.cs','tests/Sanctuary.Integration/PowerStrikeChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',3)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==170
    log=e.cohort(STAGE,expected_markers(),170);assert 'pass across3boots/2reloads' in (STAGE/'native-run-4.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==56 and 'denied by connection filter' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'amazon-thrust-contact-fixtures.json');assert len(saved)==3
    for s in saved:
        assert s['Generation']==2 and s['Life']>0 and s['Mana']==17 and s['Durability']>0
        weapon,gloves,helm=json.loads(s['Gear']);assert weapon['NamedItemId']is None and gloves['NamedItemId']=='unique/232' and helm['NamedItemId']=='unique/207'
        profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Owners'][0]['Actor']==s['Owner']
        assert profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in (195,198)]
    compiled=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-5.json')['inputs']
    assert compiled['projectInputs']==final['projectInputs']and compiled['copiedData']==final['copiedData']
    assert all(final['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items()if p.startswith(('src/','shared/')))
    integration=e.load(STAGE/'source-snapshot-4.json')['inputs']
    assert all(final['sourceInputs'].get(p)==d for p,d in integration['sourceInputs'].items()if p.endswith('.cs'))
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    files=[ROOT/'tools/record_amazon_thrust_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/amazon-thrust-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Captured Power/Charged/Lightning Strike contacts with ordered weapon/item/child delivery',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=170,markers=dict(sorted(expected_markers().items())),connections=56,newWireInputs=18,retainedWireInputs=36,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,skills=3,contactScenarios=33,savedOriginalContexts=3,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=3,sourceSnapshot=5,nativeRun=4,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Complete older start6/start10/do11/do14/fill/drain and existing current/approved controllers reused. Skill lightning precedes weapon allocation;shared startup accuracy/shield/PMH/armor,source initial poison/clock,captured cold/drain and ordered damage/proc/HP/wear/attack/return retain payment,target,cancellation and basic fallback. Charged/chain emissions follow melee even on miss,block or lethal contact. Thirty-three focused cases,eighteen new and36retained inputs,three saved original/proc/wound/resource/durability contexts and all three retained controller/flight cohorts pass. Production is unchanged after Content1;two retained RNG-service fixture repairs and runner delayed-marker accounting are frozen. Modern calc1 ED,RNG/timing/owner/geometry/recipient/PvP and remaining missile/incoming/full named admission remain explicit. No deployment,updated reference read,new item or class certificate.')
    e.write('amazon-thrust-contact-native-evidence.json',result,files,check)
    print('Amazon thrusts:170results/18new inputs/33cases/3saved contexts/2reloads;modern limits remain explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
