#!/usr/bin/env python3
"""Bind source weapon-poison allocation on learned Druid form attacks."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/rabies-poison-contact';BASE='ee8e64290c7ed95ee1533e66851a1a714350d3ee'
sys.path.insert(0,str(ROOT/'tests'))
from check_rabies_poison_contact import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=count
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    for n in c['extra_pre_save_commands']:r[n]+=3
    for n in ('druid-fury-stage-check','druid-fury-stage-result'):r[n]+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/druid-weapon-poison/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/DruidRabies.cs','tests/Sanctuary.Integration/DruidActionProcChecks.cs','tests/Sanctuary.Integration/DruidRabiesChecks.cs','tests/Sanctuary.Integration/DruidWeaponPoisonChecks.cs','tests/Sanctuary.Integration/RabiesPoisonContactChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',2)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    compiled=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-3.json')['inputs']
    assert e.load(STAGE/'source-snapshot-2.json')['inputs']==final
    assert compiled['projectInputs']==final['projectInputs'] and compiled['copiedData']==final['copiedData']
    assert all(final['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items() if p.startswith(('src/','shared/')))
    log=e.cohort(STAGE,expected_markers(),482)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-3.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==191 and 'denied by connection filter' not in log
    warnings=[line for line in log.splitlines() if 'Warning: Attempted to delete' in line]
    assert len(warnings)==1 and all('WardrobeSet' in line for line in warnings),warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz') as archive:
        for name in ('Content','Integration'):
            assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'druid-weapon-poison-fixtures.json');assert len(saved)==8
    assert [(r['Ability'],r['Form']) for r in saved]==[(165,164),(166,162),(167,162),(167,164),(170,162),(170,164),(168,162),(171,162)]
    for row in saved:
        assert row['Generation']==2 and row['Life']>0 and row['TargetLife']>0
        weapon,gloves=json.loads(row['Gear']);assert weapon['NamedItemId']=='unique/170' and gloves['NamedItemId']=='unique/232'
        assert len(weapon['NamedProperties'])==8 and len(gloves['NamedProperties'])==7
        shape=json.loads(row['Shape']);assert shape['Owner']==row['Owner'] and shape['Form']['Ability']==row['Form']
        poison=json.loads(row['Poison']);assert poison['Owner']==row['Target'] and poison['PoisonSource']==row['Owner'] and poison['PoisonMs']==7000 and poison['PoisonClock']['Rate256']==326
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168
    files=[ROOT/'tools/record_rabies_poison_contact.py',ROOT/'docs/rabies-poison-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/druid-weapon-poison-native-evidence.json',ROOT/'tools/reference/druid-setup-source-review.json',ROOT/'tools/reference/consume-learned-client-evidence.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'failed-run-1.tar.gz',STAGE/'failed-run-2.tar.gz',ROOT/'tools/reference/rabies-poison-contact-source-review.json']
    result=dict(component='Rabies separate forced-success skill-poison melee contact',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=482,markers=dict(sorted(expected_markers().items())),connections=191,actualRetainedWireInputs=186,focusedPoisonContactCases=8,furyPaidSaveStages=3,boots=3,saves=3,reloads=2,knownWardrobeSaveWarnings=1,skills=6,formSkillContexts=8,savedContexts=8,poisonScenarioCases=48,fullPoisonFurySeries=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=2,sourceSnapshot=3,nativeRun=3,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,durableAssemblyArchive='.local/class-skills/rabies-poison-contact/accepted-assemblies-1.tar.gz',installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Complete older do121,forced-success result,HitFlags2 skip-fill,total poison rate and second melee drain reused. Native initial poison HP/events,miss/block/immunity,stronger clock,repeat marker,lethal contact,cancellation and retained full originals/input/saves pass. One recipient-resistance fixture repair and one save-harness manifest mapping repair are frozen;Content unchanged after build1,Integration unchanged after build2. Exact modern callback,startup,lost-contact and lethal/reentrant marker chronology,other recipients/contexts and complete item/graphical parity remain unfinished. No deployment or newer D2R read.')
    e.write('rabies-poison-contact-native-evidence.json',result,files,check)
    print('Rabies skill poison:482results/186retained inputs/8focused cases/2reloads;modern and other gaps stay explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
