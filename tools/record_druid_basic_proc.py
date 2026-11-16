#!/usr/bin/env python3
"""Bind ordinary transformed melee proc delivery, complete originals and saves."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/druid-basic-proc';BASE='aa25c2633d943958693641e64faed007ae40a145'
sys.path.insert(0,str(ROOT/'tests'))
from check_druid_basic_proc import configuration

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
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/hellslayer/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/PlayerBasicContact.cs','tests/Sanctuary.Integration/DruidBasicProcChecks.cs','tests/Sanctuary.Integration/HellslayerChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',3)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    compiled=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-3.json')['inputs']
    assert compiled['projectInputs']==final['projectInputs'] and compiled['copiedData']==final['copiedData']
    assert all(final['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items() if p.startswith(('src/','shared/')))
    log=e.cohort(STAGE,expected_markers(),131)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-3.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==44 and 'denied by connection filter' not in log
    warnings=[line for line in log.splitlines() if 'Warning: Attempted to delete' in line]
    assert len(warnings)==1 and all('WardrobeSet' in line for line in warnings),warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz') as archive:
        for name in ('Content','Integration'):
            assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'druid-basic-proc-fixtures.json');assert len(saved)==2
    assert len({s['Owner'] for s in saved})==len({s['Item'] for s in saved})==2
    assert {json.loads(s['Form'])['Form']['Ability'] for s in saved}=={162,164} # Werewolf,Werebear
    for s in saved:
        assert s['Generation']==2 and s['Mana']==0 and s['Life']>0
        gear=json.loads(s['Gear']);assert gear['NamedItemId']=='unique/254' and gear['Roll']['ItemLevel']==99 and len(gear['NamedProperties'])==7
        profile=json.loads(s['Profile']);assert len(profile['Owners'])==len(profile['Items'])==1
        assert profile['Owners'][0]['Actor']==s['Owner'] and profile['Items'][0]['Item']==s['Item']
        assert profile['Items'][0]['State']['Properties']==[dict(Packed=(195<<16)|(47<<6)|20,Value=10)]
        form=json.loads(s['Form']);assert form['Owner']==s['Owner'] and form['UntilUtc']>form['StartedUtc']
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    prior=e.load(ROOT/'tools/reference/hellslayer-native-evidence.json')
    assert len(audit['Definitions'])==573
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==prior['backlogRows']==168
    files=[ROOT/'tools/record_druid_basic_proc.py',ROOT/'docs/druid-basic-proc-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/hellslayer-native-evidence.json',ROOT/'tools/reference/consume-learned-client-evidence.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'failed-run-1.tar.gz',STAGE/'failed-run-2.tar.gz']
    result=dict(component='Werewolf/Werebear ordinary melee item procs through shared ordered contact',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=131,markers=dict(sorted(expected_markers().items())),connections=44,actualFormWireInputs=12,retainedWireInputs=30,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,forms=2,completeNamedOriginals=2,rangedFormCarrierContexts=6,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=3,sourceSnapshot=3,nativeRun=3,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,durableAssemblyArchive='.local/class-skills/druid-basic-proc/accepted-assemblies-1.tar.gz',installed=False,namedAdmissionChanged=False,backlogRows=168,wholeHellslayerAdmitted=True,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Ordinary Werewolf/Werebear basic attacks with actual registered originals reuse the approved ordered melee/world services, existing form A1 clock, selected melee scope and outer retaliation attempt. Complete Hellslayer keeps real10percent chance; explicit normal ranged/throwing event-only fixtures establish melee/no-ammunition behavior without admitting new names. Learned form skills,charged finishers,missile/incoming player contexts,other proc bodies and exact modern/graphical parity remain unfinished. Two fixture-only shield/control-space corrections;one known WardrobeSet save warning;no deployment or newer D2R read.')
    e.write('druid-basic-proc-native-evidence.json',result,files,check)
    print('Transformed ordinary melee procs:131results/12actual form inputs/2reloads;learned form skills remain separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
