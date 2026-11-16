#!/usr/bin/env python3
"""Bind Whirlwind contact,uninterruptible proc and source-poison qualification."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/whirlwind-contact';BASE='980da1047576560bdbb700a4f038c2f1cb7b4d69'
sys.path.insert(0,str(ROOT/'tests'))
from check_whirlwind_contact import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=count
    for n in c.get('extra_checks',()):r[n]+=3
    for n in c.get('extra_pre_save_commands',()):r[n]+=3
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/barbarian-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/BarbarianWhirlwind.cs','src/Sanctuary.Content/PlayerItemProcWorld.cs','src/Sanctuary.Content/PlayerWeaponContact.cs','tests/Sanctuary.Integration/WhirlwindChecks.cs','tests/Sanctuary.Integration/WhirlwindContactChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',4)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    compiled=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-4.json')['inputs']
    assert compiled['projectInputs']==final['projectInputs'] and compiled['copiedData']==final['copiedData']
    assert all(final['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items() if p.startswith(('src/','shared/')))
    assert sum(expected_markers().values())==125
    log=e.cohort(STAGE,expected_markers(),125);assert 'pass across3boots/2reloads' in (STAGE/'native-run-3.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==41 and 'denied by connection filter' not in log
    warnings=[line for line in log.splitlines() if 'Warning: Attempted to delete' in line];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz') as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'whirlwind-contact-fixtures.json');assert saved['Generation']==2 and saved['Life']>0 and saved['Mana']>=0
    weapon,gloves=json.loads(saved['Gear']);assert weapon['NamedItemId'] is None and gloves['NamedItemId']=='unique/232'
    profile=json.loads(saved['Profile']);assert len(profile['Owners'])==1 and profile['Owners'][0]['Actor']==saved['Owner']
    assert profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in (195,198)]
    assert len(e.load(STAGE/'whirlwind-fixtures.json'))==2
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    files=[ROOT/'tools/record_whirlwind_contact.py',ROOT/'tools/review_whirlwind_contact.py',ROOT/'docs/whirlwind-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'failed-build-1.tar.gz',STAGE/'failed-run-1.tar.gz',STAGE/'failed-run-2.tar.gz']
    result=dict(component='Whirlwind ordered contacts,source poison and uninterruptible item proc casts',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=125,markers=dict(sorted(expected_markers().items())),connections=41,newWireInputs=6,retainedWireInputs=30,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,contactScenarios=8,savedOriginalContexts=1,savedFlightContexts=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=4,sourceSnapshot=4,nativeRun=3,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Complete older do76/source-state/item-trigger functions reuse shared ordered NPC contact and poison services. Live Whirlwind state blocks item execution after chance RNG,without projectile/world-child allocation;ordinary casts resume after settlement. Actual zero-life living source mode keeps contacting until deferred death. Eight scenarios,right/quick input,distinct dual originals,exact saved raw boundary original/profile/wounds and retained movement/flight cleanup pass. Compile fixture type,wire acknowledgement and prepared-corridor drift repairs are frozen;production unchanged after Content1. Player recipients retain prior scalar compatibility;modern cadence/geometry/RNG/stat/mode and remaining item/recipient/missile/incoming gaps stay explicit. No deployment,updated reference read,new named item or class certificate.')
    e.write('whirlwind-contact-native-evidence.json',result,files,check)
    print('Whirlwind:125results/6new inputs/8cases/2reloads;uninterruptible rolls and source poison qualified within scope.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
