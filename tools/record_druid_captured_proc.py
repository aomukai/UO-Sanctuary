#!/usr/bin/env python3
"""Bind captured Maul/Feral/Fire Claws contact,whole item input and saves."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/druid-captured-proc';BASE='1a93a91da6543b87d85e16b18a44ff311164a474'
sys.path.insert(0,str(ROOT/'tests'))
from check_druid_captured_proc import configuration

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
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/druid-basic-proc/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/DruidChargedAttacks.cs','src/Sanctuary.Content/DruidFireClaws.cs','src/Sanctuary.Content/DruidWeaponContact.cs','src/Sanctuary.Content/PlayerWeaponContact.cs','tests/Sanctuary.Integration/DruidBasicProcChecks.cs','tests/Sanctuary.Integration/DruidCapturedProcChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',2)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    compiled=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-2.json')['inputs']
    assert compiled['projectInputs']==final['projectInputs'] and compiled['copiedData']==final['copiedData']
    assert all(final['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items() if p.startswith(('src/','shared/')))
    log=e.cohort(STAGE,expected_markers(),203)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==68 and 'denied by connection filter' not in log
    warnings=[line for line in log.splitlines() if 'Warning: Attempted to delete' in line]
    assert len(warnings)==1 and all('WardrobeSet' in line for line in warnings),warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz') as archive:
        for name in ('Content','Integration'):
            assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'druid-captured-proc-fixtures.json');assert len(saved)==4
    assert len({s['Owner'] for s in saved})==len({s['Item'] for s in saved})==4
    assert [(s['Ability'],s['Form'])for s in saved]==[(165,164),(166,162),(167,162),(167,164)]
    for s in saved:
        assert s['Generation']==2 and s['Life']>0 and s['Mana']>=0
        gear=json.loads(s['Gear']);assert gear['NamedItemId']=='unique/254' and gear['Roll']['ItemLevel']==99 and len(gear['NamedProperties'])==7
        profile=json.loads(s['Profile']);assert len(profile['Owners'])==len(profile['Items'])==1
        assert profile['Owners'][0]['Actor']==s['Owner'] and profile['Items'][0]['Item']==s['Item']
        assert profile['Items'][0]['State']['Properties']==[dict(Packed=(195<<16)|(47<<6)|20,Value=10)]
        form=json.loads(s['Shape']);assert form['Owner']==s['Owner'] and form['Form']['Ability']==s['Form'] and form['UntilUtc']>form['StartedUtc']
    for name in ('druid-maul','druid-feral','druid-fire-claws'):assert e.load(STAGE/(name+'-fixtures.json'))
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168
    files=[ROOT/'tools/record_druid_captured_proc.py',ROOT/'docs/druid-captured-proc-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/druid-basic-proc-native-evidence.json',ROOT/'tools/reference/consume-learned-client-evidence.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'failed-build-1.tar.gz']
    result=dict(component='Captured Maul,Feral Rage and Fire Claws through shared ordered weapon contact',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=203,markers=dict(sorted(expected_markers().items())),connections=68,actualCapturedSkillWireInputs=24,retainedWireInputs=42,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,skills=3,formSkillContexts=4,completeNamedOriginals=4,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,durableAssemblyArchive='.local/class-skills/druid-captured-proc/accepted-assemblies-1.tar.gz',installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Three existing windup-allocation skills retain their captured accuracy,shield choice,critical/elements,stun,leech and payment;release now uses the ordered actor/world adapter. Actual whole Hellslayer chance,damage,post-contact charges,latched misses,cancellation and original/form/profile/wound saves pass,alongside the existing three skill cohorts. Aggregate poison retains its original deferred transaction without an invented initial rate hit. Hunger,Rabies,Fury,charged finishers,missile/incoming player contexts,other spell bodies and exact modern/graphical parity remain unfinished. Fixture-only build repair;one known WardrobeSet save warning;no deployment or newer D2R read.')
    e.write('druid-captured-proc-native-evidence.json',result,files,check)
    print('Captured form skills:203results/24actual whole-item skill inputs/2reloads;Hunger,Rabies and Fury remain next.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
