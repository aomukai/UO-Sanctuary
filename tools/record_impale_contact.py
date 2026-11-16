#!/usr/bin/env python3
"""Bind Impale captured contact,post-wear poison and ordered item/wear release."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/impale-contact';BASE='69439df4ab744e0b38f9edb141c351bed5177a17'
sys.path.insert(0,str(ROOT/'tests'))
from check_impale_contact import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=count
    for n in c.get('extra_checks',()):
        r[n]+=3
        if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=3
    for n in c.get('extra_pre_save_commands',()):r[n]+=3
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/amazon-sequence-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/AmazonImpale.cs','tests/Sanctuary.Integration/ImpaleContactChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name in ('Content','Integration'):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+'-build-1.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==123
    log=e.cohort(STAGE,expected_markers(),123);assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==38 and 'denied by connection filter' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'impale-contact-fixtures.json');assert saved['Generation']==2 and saved['Life']>0 and saved['Mana']==17 and saved['Durability']>0
    weapon,gloves,helm=json.loads(saved['Gear']);assert weapon['NamedItemId']is None and gloves['NamedItemId']=='unique/232' and helm['NamedItemId']=='unique/207'
    profile=json.loads(saved['Profile']);assert len(profile['Owners'])==1 and profile['Owners'][0]['Actor']==saved['Owner']
    assert profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in (195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    files=[ROOT/'tools/record_impale_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/impale-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Impale captured contact with post-wear source poison and ordered item/wear events',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=123,markers=dict(sorted(expected_markers().items())),connections=38,newWireInputs=6,retainedWireInputs=30,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,contactScenarios=8,savedOriginalContexts=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,installed=False,namedAdmissionChanged=False,backlogRows=168,paidBlockAvoidanceVerified=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Complete older start7/fill/do2/drain and existing modern/native controller reused. Physical is captured before special wear,other lanes and poison after wear;shared source initial rate/clock,drain,HP/item/wear/return retain own-depletion and slow-before-range. Eight edges,6inputs,exact saved originals/proc/wounds/mana/durability and unchanged schedule/special-wear/depletion/slow/expiry controls pass. Both first builds and first native run pass. Paid forced-success convention remains unchanged:modern shield/avoidance is explicitly unverified,not a shield-bypass claim. Modern critical/species/stat/break/RNG/timing/owner/recipient/PvP,lightning thrusts and remaining item/missile/incoming/full named admission remain open. No deployment,updated reference read,new item or class certificate.')
    e.write('impale-contact-native-evidence.json',result,files,check)
    print('Impale:123results/6new inputs/8cases/2reloads;paid shield/avoidance and other modern limits remain explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
