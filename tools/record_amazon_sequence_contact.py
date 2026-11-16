#!/usr/bin/env python3
"""Bind Jab/Fend event-time contacts,source poison,proc/wear order and sequence saves."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/amazon-sequence-contact';BASE='98e1d0ebba95005de832fab5921e55bd0af757fa'
sys.path.insert(0,str(ROOT/'tests'))
from check_amazon_sequence_contact import configuration

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
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/double-throw-missiles/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/AmazonFend.cs','src/Sanctuary.Content/AmazonJab.cs','src/Sanctuary.Content/AmazonSequenceContact.cs','src/Sanctuary.Content/PlayerWeaponContact.cs','tests/Sanctuary.Integration/AmazonSequenceContactChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',2)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    compiled=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-2.json')['inputs']
    assert compiled['projectInputs']==final['projectInputs']and compiled['copiedData']==final['copiedData']
    assert all(final['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items()if p.startswith(('src/','shared/')))
    assert sum(expected_markers().values())==143
    log=e.cohort(STAGE,expected_markers(),143);assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==47 and'denied by connection filter'not in log
    warnings=[line for line in log.splitlines()if'Warning: Attempted to delete'in line];assert len(warnings)==1 and'WardrobeSet'in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'amazon-sequence-contact-fixtures.json');assert len(saved)==2
    for s in saved:
        assert s['Generation']==2 and s['Life']>0 and s['Mana']>=0
        weapon,gloves,helm=json.loads(s['Gear']);assert weapon['NamedItemId']is None and[gloves['NamedItemId'],helm['NamedItemId']]==['unique/232','unique/207']
        profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Owners'][0]['Actor']==s['Owner']
        assert profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    files=[ROOT/'tools/record_amazon_sequence_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/amazon-sequence-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'first-contact-cohort-1.tar.gz']
    result=dict(component='Jab/Fend ordered contacts with event-time poison,proc streams and expansion wear',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=143,markers=dict(sorted(expected_markers().items())),connections=47,newWireInputs=12,retainedWireInputs=30,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,skills=2,contactScenarios=16,completeProviderSeries=2,savedOriginalContexts=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,transientWearCallbackAdded=True,installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Complete older do7/do13 and existing sequence/wear controllers reuse shared ordinary NPC contacts. Current event-time AR/ED/gear,shield/PMH,initial poison/clock and ordered item events retain Jab target and Fend cycles. Optional transient wear callback runs the existing expansion writer once before attack/return without native double wear. Fourteen single-event edges plus two complete proc-bearing series,12inputs,original/profile/wound/resource reloads and unchanged retained Jab/Fend/natural/item consumers pass. First-contact-only cohort is frozen separately;production unchanged after Content1. Modern/RNG/stat/geometry,exceptional/PvP recipients,Impale/lightning thrusts and remaining item/missile/incoming/full named admission stay explicit. No deployment,updated reference read,new item or class certificate.')
    e.write('amazon-sequence-contact-native-evidence.json',result,files,check)
    print('Jab/Fend:143results/12new inputs/16cases/2complete provider series/2reloads;modern and recipient gaps remain explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
